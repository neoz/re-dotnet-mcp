using System.ComponentModel;
using ModelContextProtocol.Server;
using ReDotnet.Core.Inspection;
using ReDotnet.Core.Workspace;

namespace ReDotnet.Server.Tools;

[McpServerToolType]
public sealed class TypeDetailTools
{
    private readonly WorkspaceRegistry _registry;
    private readonly TypeDetailService _svc;

    public TypeDetailTools(WorkspaceRegistry registry, TypeDetailService svc)
    {
        _registry = registry;
        _svc = svc;
    }

    [McpServerTool(Name = "get_type")]
    [Description("Get detailed info for a type: base, interfaces, members, nested types, custom-attribute count.")]
    public TypeDetail GetType(
        [Description("Assembly id.")] string id,
        [Description("Type identifier (token, FQN, or @0xRVA).")] string identifier)
        => _registry.Run(id, ws => _svc.GetType(ws, identifier));

    [McpServerTool(Name = "get_type_layout")]
    [Description("Get the explicit/sequential/auto layout for a type with field offsets.")]
    public TypeLayout GetTypeLayout(
        [Description("Assembly id.")] string id,
        [Description("Type identifier.")] string identifier)
        => _registry.Run(id, ws => _svc.GetTypeLayout(ws, identifier));

    [McpServerTool(Name = "list_namespaces")]
    [Description("List distinct namespaces with their type counts.")]
    public IReadOnlyList<NamespaceInfo> ListNamespaces([Description("Assembly id.")] string id)
        => _registry.Run(id, ws => _svc.ListNamespaces(ws));
}
