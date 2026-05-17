using System.ComponentModel;
using ModelContextProtocol.Server;
using ReDotnet.Core.Inspection;
using ReDotnet.Core.Workspace;
using ReDotnet.Server.Envelope;

namespace ReDotnet.Server.Tools;

[McpServerToolType]
public sealed class MethodTools
{
    private readonly WorkspaceRegistry _registry;
    private readonly MethodListService _methods;

    public MethodTools(WorkspaceRegistry registry, MethodListService methods)
    {
        _registry = registry;
        _methods = methods;
    }

    [McpServerTool(Name = "list_methods")]
    [Description("List methods. Supports filter_regex over 'Type::Method', type filter, modifier filter; paginated.")]
    public PageDto<MethodSummaryDto> ListMethods(
        [Description("Assembly id.")] string id,
        [Description("Optional regex over '<full_type>::<method_name>'.")] string? filter_regex = null,
        [Description("Restrict to a specific type (token or FQN).")] string? type = null,
        [Description("Filter by modifier: static|instance|virtual|abstract|public|private|internal|protected.")] string? modifier = null,
        [Description("Pagination offset.")] int? offset = null,
        [Description("Pagination limit.")] int? limit = null)
    {
        var ws = _registry.Get(id);
        var page = _methods.List(ws, filter_regex, type, modifier, offset, limit);
        var dto = new Core.Envelope.Page<MethodSummaryDto>(
            page.Items.Select(m => new MethodSummaryDto(
                m.Token, m.Name, m.DeclaringType, m.Signature, m.IsStatic, m.IsVirtual,
                m.IsAbstract, m.Visibility, m.CodeSize)).ToList(),
            page.Offset, page.Limit, page.Total, page.HasMore);
        return PageDto<MethodSummaryDto>.From(dto);
    }
}
