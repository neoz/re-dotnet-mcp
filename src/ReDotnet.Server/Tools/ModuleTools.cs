using System.ComponentModel;
using ModelContextProtocol.Server;
using ReDotnet.Core.Workspace;

namespace ReDotnet.Server.Tools;

[McpServerToolType]
public sealed class ModuleTools
{
    private readonly WorkspaceRegistry _registry;

    public ModuleTools(WorkspaceRegistry registry) => _registry = registry;

    [McpServerTool(Name = "get_module_info")]
    [Description("Module-level metadata: name, MVID, runtime/metadata versions, entry point token.")]
    public ModuleInfoDto GetModuleInfo([Description("Assembly id.")] string id)
    {
        var ws = _registry.Get(id);
        var m = ws.Module;
        return new ModuleInfoDto(
            id: ws.AssemblyId,
            name: m.Name?.ToString() ?? "<anonymous>",
            mvid: m.Mvid.ToString("D"),
            runtime_version: m.RuntimeVersion ?? "",
            metadata_version: m.DotNetDirectory?.Metadata?.MajorVersion + "." +
                              m.DotNetDirectory?.Metadata?.MinorVersion,
            entry_point: m.ManagedEntryPointMethod is { } ep ? $"0x{ep.MetadataToken.ToUInt32():X8}" : null);
    }
}

public sealed record ModuleInfoDto(
    string id,
    string name,
    string mvid,
    string runtime_version,
    string? metadata_version,
    string? entry_point);
