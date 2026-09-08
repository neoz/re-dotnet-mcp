using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.IO;
using ReDotnet.Core.Mutation;
using ReDotnet.Core.Sidecar;

namespace ReDotnet.Core.Workspace;

public sealed class Workspace : IDisposable
{
    public string AssemblyId { get; }
    public string OriginalPath { get; }
    public DateTimeOffset OpenedAt { get; }
    public ModuleDefinition Module { get; }
    public MutationLog MutationLog { get; }

    /// Recoverable metadata errors AsmResolver reported while reading this
    /// module. It keeps filling after the initial load because members are
    /// read lazily, so the contents are "everything seen so far", not a
    /// complete audit of the file.
    public DiagnosticBag Diagnostics { get; }

    /// Serializes all access to <see cref="Module"/>. AsmResolver's lazy
    /// metadata caches are not thread-safe; concurrent reads on the same
    /// module can race in lazy initialization and either crash or spin.
    /// Tools acquire this lock via <c>WorkspaceRegistry.Run</c>.
    public object ModuleLock { get; } = new();

    private readonly IFileService _fileService;
    private SidecarDocument? _sidecar;
    private readonly object _sidecarLock = new();
    private readonly SidecarStore _sidecarStore;
    private int _disposed;

    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    public Workspace(string assemblyId, string originalPath, ModuleDefinition module, IFileService fileService, SidecarStore sidecarStore, DiagnosticBag diagnostics)
    {
        AssemblyId = assemblyId;
        OriginalPath = originalPath;
        Module = module;
        OpenedAt = DateTimeOffset.UtcNow;
        MutationLog = new MutationLog();
        _fileService = fileService;
        _sidecarStore = sidecarStore;
        Diagnostics = diagnostics;
    }

    /// Closes the memory-mapped file backing <see cref="Module"/>. After
    /// disposal the on-disk file is no longer locked by this process, so
    /// callers can rename/delete/overwrite it. Module reads after disposal
    /// have undefined behavior; the registry only disposes inside the
    /// <see cref="ModuleLock"/> so in-flight reads have already drained.
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _fileService.Dispose();
    }

    public SidecarDocument Sidecar
    {
        get
        {
            if (_sidecar is not null) return _sidecar;
            lock (_sidecarLock)
            {
                _sidecar ??= _sidecarStore.LoadOrCreate(this);
                return _sidecar;
            }
        }
    }

    public void SaveSidecar()
    {
        lock (_sidecarLock)
        {
            _sidecarStore.Save(this, Sidecar);
        }
    }
}
