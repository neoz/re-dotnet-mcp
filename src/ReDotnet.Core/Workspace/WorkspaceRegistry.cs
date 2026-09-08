using System.Collections.Concurrent;
using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.IO;
using ReDotnet.Core.Envelope;
using ReDotnet.Core.Sidecar;

namespace ReDotnet.Core.Workspace;

public sealed class WorkspaceRegistry
{
    private readonly ConcurrentDictionary<string, Workspace> _workspaces = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _pathToId = new(StringComparer.OrdinalIgnoreCase);
    private readonly ModuleResolverFactory _resolverFactory;
    private readonly SidecarStore _sidecarStore;
    private readonly object _openLock = new();

    public WorkspaceRegistry(ModuleResolverFactory resolverFactory, SidecarStore sidecarStore)
    {
        _resolverFactory = resolverFactory;
        _sidecarStore = sidecarStore;
    }

    public Workspace Open(string path, string? requestedId = null)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw BackendError.NotFound("file", path);

        lock (_openLock)
        {
            if (_pathToId.TryGetValue(fullPath, out var existingId))
            {
                if (requestedId is not null && requestedId != existingId)
                    throw BackendError.BadInput(
                        $"path '{fullPath}' is already open as id '{existingId}'; refuse to re-open with different id",
                        new Dictionary<string, object?> { ["existing_id"] = existingId });
                return _workspaces[existingId];
            }

            var stem = requestedId ?? Path.GetFileNameWithoutExtension(fullPath);
            var id = ResolveUniqueId(stem);

            // Each workspace owns its own file service so Close() can release
            // the memory-mapped file handle without affecting other workspaces.
            var fileService = new MemoryMappedFileService();
            var readerParameters = _resolverFactory.CreateReaderParameters();
            readerParameters.PEReaderParameters.FileService = fileService;

            // AsmResolver defaults to EmptyErrorListener, which discards
            // recoverable metadata damage and lets a protected assembly look
            // intact. Collect it instead. The bag outlives the load because
            // members are read lazily, so it keeps filling as tools run.
            var diagnostics = new DiagnosticBag();
            readerParameters.PEReaderParameters.ErrorListener = diagnostics;

            ModuleDefinition module;
            try
            {
                module = ModuleDefinition.FromFile(fullPath, readerParameters);
            }
            catch (Exception ex)
            {
                // AsmResolver probes the target runtime while loading, and
                // obfuscators poison that probe: a corlib AssemblyRef versioned
                // 65535.65535.65535.65535 makes it select an impossible .NET
                // Core implementation corlib and throw. Retry without the
                // probe. The runtime context it builds only supplies default
                // AssemblyRef resolution, which is opt-in per PRD 11.5, so
                // dropping it costs nothing the inspection tools rely on.
                try
                {
                    module = ModuleDefinition.FromFile(fullPath, readerParameters, createRuntimeContext: false);
                }
                catch
                {
                    fileService.Dispose();
                    throw BackendError.BadInput(
                        $"failed to load '{fullPath}': {ex.GetType().Name}: {ex.Message}",
                        new Dictionary<string, object?>
                        {
                            ["path"] = fullPath,
                            ["exception_type"] = ex.GetType().FullName,
                        });
                }
            }
            var ws = new Workspace(id, fullPath, module, fileService, _sidecarStore, diagnostics);

            _workspaces[id] = ws;
            _pathToId[fullPath] = id;
            return ws;
        }
    }

    private string ResolveUniqueId(string stem)
    {
        if (!_workspaces.ContainsKey(stem)) return stem;
        for (var n = 2; n < 1000; n++)
        {
            var candidate = $"{stem}_{n}";
            if (!_workspaces.ContainsKey(candidate)) return candidate;
        }
        throw new InvalidOperationException($"failed to allocate unique id for '{stem}'");
    }

    public Workspace Get(string id)
    {
        if (_workspaces.TryGetValue(id, out var ws)) return ws;
        throw BackendError.NotFound("assembly", id);
    }

    public bool TryGet(string id, out Workspace ws)
    {
        return _workspaces.TryGetValue(id, out ws!);
    }

    public IReadOnlyList<Workspace> All() => _workspaces.Values.ToList();

    /// Acquire the workspace's <see cref="Workspace.ModuleLock"/> and run
    /// <paramref name="fn"/>. All tools that touch <c>ws.Module</c> must go
    /// through this gate (AsmResolver is not thread-safe on concurrent reads).
    public T Run<T>(string id, Func<Workspace, T> fn)
    {
        var ws = Get(id);
        lock (ws.ModuleLock) return fn(ws);
    }

    public void Run(string id, Action<Workspace> action)
    {
        var ws = Get(id);
        lock (ws.ModuleLock) action(ws);
    }

    /// Raised inside the workspace's <see cref="Workspace.ModuleLock"/> when
    /// the workspace is being evicted, before its module is disposed. Lets
    /// downstream caches keyed by <see cref="Workspace.AssemblyId"/> drop
    /// entries that reference the soon-to-be-disposed module.
    public event Action<Workspace>? WorkspaceEvicting;

    public bool Close(string id, bool save)
    {
        if (!_workspaces.TryGetValue(id, out var ws)) return false;
        // Drain any in-flight tool calls on this workspace before evicting.
        // Lock order matches Open: _openLock then ModuleLock (Open never
        // takes ModuleLock, so there is no inversion).
        lock (_openLock)
        lock (ws.ModuleLock)
        {
            if (!_workspaces.TryRemove(id, out _)) return false;
            _pathToId.TryRemove(ws.OriginalPath, out _);
            if (save) ws.SaveSidecar();
            try { WorkspaceEvicting?.Invoke(ws); }
            finally { ws.Dispose(); }
            return true;
        }
    }
}
