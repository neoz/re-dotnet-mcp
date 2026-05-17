using AsmResolver.DotNet;
using ReDotnet.Core.Mutation;
using ReDotnet.Core.Sidecar;

namespace ReDotnet.Core.Workspace;

public sealed class Workspace
{
    public string AssemblyId { get; }
    public string OriginalPath { get; }
    public DateTimeOffset OpenedAt { get; }
    public ModuleDefinition Module { get; }
    public MutationLog MutationLog { get; }

    private SidecarDocument? _sidecar;
    private readonly object _sidecarLock = new();
    private readonly SidecarStore _sidecarStore;

    public Workspace(string assemblyId, string originalPath, ModuleDefinition module, SidecarStore sidecarStore)
    {
        AssemblyId = assemblyId;
        OriginalPath = originalPath;
        Module = module;
        OpenedAt = DateTimeOffset.UtcNow;
        MutationLog = new MutationLog();
        _sidecarStore = sidecarStore;
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
