using System.ComponentModel;
using ModelContextProtocol.Server;
using ReDotnet.Core.Envelope;
using ReDotnet.Core.Inspection;
using ReDotnet.Core.Workspace;
using ReDotnet.Server.Envelope;

namespace ReDotnet.Server.Tools;

[McpServerToolType]
public sealed class ReferenceTools
{
    private readonly WorkspaceRegistry _registry;
    private readonly ReferenceService _svc;

    public ReferenceTools(WorkspaceRegistry registry, ReferenceService svc)
    {
        _registry = registry;
        _svc = svc;
    }

    [McpServerTool(Name = "list_assembly_refs")]
    [Description("List AssemblyReferences with version, culture, public-key-token.")]
    public PageDto<AssemblyRefInfo> ListAssemblyRefs(
        [Description("Assembly id.")] string id,
        string? filter_regex = null, int? offset = null, int? limit = null)
        => PageDto<AssemblyRefInfo>.From(_svc.ListAssemblyRefs(_registry.Get(id), filter_regex, offset, limit));

    [McpServerTool(Name = "list_module_refs")]
    [Description("List ModuleReferences (typically populated by P/Invokes).")]
    public PageDto<ModuleRefInfo> ListModuleRefs(
        [Description("Assembly id.")] string id, int? offset = null, int? limit = null)
        => PageDto<ModuleRefInfo>.From(_svc.ListModuleRefs(_registry.Get(id), offset, limit));

    [McpServerTool(Name = "list_type_refs")]
    [Description("List TypeReferences (external types referenced by this module).")]
    public PageDto<TypeRefInfo> ListTypeRefs(
        [Description("Assembly id.")] string id,
        string? filter_regex = null, int? offset = null, int? limit = null)
        => PageDto<TypeRefInfo>.From(_svc.ListTypeRefs(_registry.Get(id), filter_regex, offset, limit));

    [McpServerTool(Name = "list_member_refs")]
    [Description("List MemberReferences (external member references).")]
    public PageDto<MemberRefInfo> ListMemberRefs(
        [Description("Assembly id.")] string id,
        string? filter_regex = null, int? offset = null, int? limit = null)
        => PageDto<MemberRefInfo>.From(_svc.ListMemberRefs(_registry.Get(id), filter_regex, offset, limit));

    [McpServerTool(Name = "get_xrefs_to")]
    [Description("Find IL sites that reference the given type/method/field/string.")]
    public PageDto<XrefEdge> GetXrefsTo(
        [Description("Assembly id.")] string id,
        [Description("Identifier of the target.")] string identifier,
        int? offset = null, int? limit = null)
        => PageDto<XrefEdge>.From(_svc.GetXrefsTo(_registry.Get(id), identifier, offset, limit));

    [McpServerTool(Name = "get_xrefs_from")]
    [Description("Get every external token referenced inside one method's IL.")]
    public PageDto<XrefEdge> GetXrefsFrom(
        [Description("Assembly id.")] string id,
        [Description("Method identifier.")] string method_identifier,
        int? offset = null, int? limit = null)
        => PageDto<XrefEdge>.From(_svc.GetXrefsFrom(_registry.Get(id), method_identifier, offset, limit));

    [McpServerTool(Name = "get_call_graph")]
    [Description("Build a callee tree rooted at the given method, up to depth 3.")]
    public CallGraphNode GetCallGraph(
        [Description("Assembly id.")] string id,
        [Description("Method identifier.")] string method_identifier,
        [Description("Recursion depth (1-3).")] int depth = 1)
        => _svc.GetCallGraph(_registry.Get(id), method_identifier, depth);
}
