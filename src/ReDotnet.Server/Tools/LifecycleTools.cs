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
        // Snapshot before any optional Close — Close disposes the module's
        // file mapping and ws.Module access afterward is undefined.
        var dto = _registry.Run(ws.AssemblyId, w =>
            new OpenAssemblyResultDto(w.AssemblyId, w.OriginalPath, w.Module.Mvid.ToString("D")));
        if (!keep_open) _registry.Close(ws.AssemblyId, save: false);
        return dto;
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
        var results = new List<OpenAssemblyResultDto>();
        foreach (var w in _registry.All())
        {
            // Each entry may race a concurrent Close; skip evicted workspaces
            // rather than reading their disposed module.
            try
            {
                results.Add(_registry.Run(w.AssemblyId, ws =>
                    new OpenAssemblyResultDto(ws.AssemblyId, ws.OriginalPath, ws.Module.Mvid.ToString("D"))));
            }
            catch (Core.Envelope.BackendError)
            {
                // Closed between All() snapshot and Run() — skip.
            }
        }
        return results;
    }

    [McpServerTool(Name = "get_assembly_info")]
    [Description("Get metadata-level information for an open assembly. " +
                 "parse_warnings counts recoverable metadata damage seen so far; " +
                 "members are read lazily, so it grows as other tools touch the " +
                 "assembly. Use list_parse_diagnostics for the messages.")]
    public AssemblyInfoDto GetAssemblyInfo(
        [Description("Assembly id.")] string id)
        => _registry.Run(id, ws =>
        {
            var snap = _info.Capture(ws);
            return new AssemblyInfoDto(
                snap.AssemblyId, snap.Path, snap.Name, snap.Runtime, snap.TargetFramework,
                snap.Machine, snap.Characteristics, snap.HasStrongName, snap.HasAuthenticode,
                snap.HasPdb, snap.Mvid, snap.Streams, snap.EntryPoint,
                snap.ParseWarnings);
        });

    [McpServerTool(Name = "list_parse_diagnostics")]
    [Description("List the recoverable metadata errors AsmResolver reported while " +
                 "reading an open assembly. Members are read lazily, so the list " +
                 "grows as other tools touch the assembly.")]
    public PageDto<string> ListParseDiagnostics(
        [Description("Assembly id.")] string id,
        int? offset = null, int? limit = null)
        => _registry.Run(id, ws => PageDto<string>.From(_info.ListParseDiagnostics(ws, offset, limit)));
}
