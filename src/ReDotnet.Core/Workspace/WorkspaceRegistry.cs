using System.Collections.Concurrent;
using AsmResolver.DotNet;
using ReDotnet.Core.Envelope;
using ReDotnet.Core.Sidecar;

namespace ReDotnet.Core.Workspace;

public sealed class WorkspaceRegistry
{
    private readonly ConcurrentDictionary<string, Workspace> _workspaces = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _pathToId = new(StringComparer.OrdinalIgnoreCase);
    private readonly ModuleResolverFactory _resolverFactory;
    private readonly SidecarStore _sidecarStore;

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

        var readerParameters = _resolverFactory.CreateReaderParameters();
        ModuleDefinition module;
        try
        {
            module = ModuleDefinition.FromFile(fullPath, readerParameters);
        }
        catch (Exception ex)
        {
            throw BackendError.BadInput(
                $"failed to load '{fullPath}': {ex.GetType().Name}: {ex.Message}",
                new Dictionary<string, object?>
                {
                    ["path"] = fullPath,
                    ["exception_type"] = ex.GetType().FullName,
                });
        }
        var ws = new Workspace(id, fullPath, module, _sidecarStore);

        _workspaces[id] = ws;
        _pathToId[fullPath] = id;
        return ws;
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

    public bool Close(string id, bool save)
    {
        if (!_workspaces.TryRemove(id, out var ws)) return false;
        _pathToId.TryRemove(ws.OriginalPath, out _);
        if (save) ws.SaveSidecar();
        return true;
    }
}
