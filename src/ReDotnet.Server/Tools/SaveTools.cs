using System.ComponentModel;
using ModelContextProtocol.Server;
using ReDotnet.Core.Save;
using ReDotnet.Core.Workspace;

namespace ReDotnet.Server.Tools;

[McpServerToolType]
public sealed class SaveTools
{
    private readonly WorkspaceRegistry _registry;
    private readonly SaveService _save;

    public SaveTools(WorkspaceRegistry registry, SaveService save)
    {
        _registry = registry;
        _save = save;
    }

    [McpServerTool(Name = "save_assembly")]
    [Description("Write the assembly to disk. Default output is <stem>.patched.<ext> in the same directory.")]
    public SaveResult SaveAssembly(
        [Description("Assembly id.")] string id,
        [Description("Optional output path. If null, writes alongside the original as <stem>.patched.<ext>.")] string? path = null,
        [Description("Strong-name strategy: preserve|strip|resign|force-invalid.")] string strong_name_strategy = "preserve",
        [Description("Required for 'resign': path to .snk file.")] string? strong_name_key = null)
        => _registry.Run(id, ws => _save.SaveAssembly(ws, path, strong_name_strategy, strong_name_key));

    [McpServerTool(Name = "save_assembly_as")]
    [Description("Alias of save_assembly with an explicit required path.")]
    public SaveResult SaveAssemblyAs(
        [Description("Assembly id.")] string id,
        [Description("Output path.")] string path,
        [Description("Strong-name strategy: preserve|strip|resign|force-invalid.")] string strong_name_strategy = "preserve",
        [Description("Required for 'resign': path to .snk file.")] string? strong_name_key = null)
        => _registry.Run(id, ws => _save.SaveAssembly(ws, path, strong_name_strategy, strong_name_key));

    [McpServerTool(Name = "save_sidecar_only")]
    [Description("Flush the sidecar JSON without writing the assembly.")]
    public string SaveSidecarOnly([Description("Assembly id.")] string id)
        => _registry.Run(id, ws => _save.SaveSidecarOnly(ws));
}
