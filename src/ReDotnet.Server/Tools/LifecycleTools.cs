using System.ComponentModel;
using ModelContextProtocol.Server;
using ReDotnet.Core.Inspection;
using ReDotnet.Core.Workspace;
using ReDotnet.Server.Envelope;

namespace ReDotnet.Server.Tools;

[McpServerToolType]
public sealed class LifecycleTools
{
    private readonly WorkspaceRegistry _registry;
    private readonly AssemblyInfoService _info;

    public LifecycleTools(WorkspaceRegistry registry, AssemblyInfoService info)
    {
        _registry = registry;
        _info = info;
    }

    [McpServerTool(Name = "open_assembly")]
    [Description("Load a .NET assembly (.dll/.exe/.netmodule) into a workspace.")]
    public OpenAssemblyResultDto OpenAssembly(
        [Description("File path to the assembly to load.")] string path,
        [Description("Keep the workspace open after the call (default true).")] bool keep_open = true,
        [Description("Optional explicit assembly_id to use instead of the filename stem.")] string? assembly_id = null)
    {
        var ws = _registry.Open(path, assembly_id);
        if (!keep_open) _registry.Close(ws.AssemblyId, save: false);
        return new OpenAssemblyResultDto(ws.AssemblyId, ws.OriginalPath, ws.Module.Mvid.ToString("D"));
    }

    [McpServerTool(Name = "close_assembly")]
    [Description("Evict the workspace for assembly_id. Optionally flush the sidecar first.")]
    public CloseAssemblyResultDto CloseAssembly(
        [Description("Assembly id (filename stem unless overridden).")] string id,
        [Description("If true, write the sidecar JSON before evicting.")] bool save = false)
    {
        var closed = _registry.Close(id, save);
        return new CloseAssemblyResultDto(id, closed, save);
    }

    [McpServerTool(Name = "list_assemblies")]
    [Description("List currently open workspaces.")]
    public IReadOnlyList<OpenAssemblyResultDto> ListAssemblies()
    {
        return _registry.All()
            .Select(w => new OpenAssemblyResultDto(w.AssemblyId, w.OriginalPath, w.Module.Mvid.ToString("D")))
            .ToList();
    }

    [McpServerTool(Name = "get_assembly_info")]
    [Description("Get metadata-level information for an open assembly.")]
    public AssemblyInfoDto GetAssemblyInfo(
        [Description("Assembly id.")] string id)
    {
        var ws = _registry.Get(id);
        var snap = _info.Capture(ws);
        return new AssemblyInfoDto(
            snap.AssemblyId, snap.Path, snap.Name, snap.Runtime, snap.TargetFramework,
            snap.Machine, snap.Characteristics, snap.HasStrongName, snap.HasAuthenticode,
            snap.HasPdb, snap.Mvid, snap.Streams, snap.EntryPoint);
    }
}
