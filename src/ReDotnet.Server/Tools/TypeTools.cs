using System.ComponentModel;
using ModelContextProtocol.Server;
using ReDotnet.Core.Inspection;
using ReDotnet.Core.Workspace;
using ReDotnet.Server.Envelope;

namespace ReDotnet.Server.Tools;

[McpServerToolType]
public sealed class TypeTools
{
    private readonly WorkspaceRegistry _registry;
    private readonly TypeListService _types;

    public TypeTools(WorkspaceRegistry registry, TypeListService types)
    {
        _registry = registry;
        _types = types;
    }

    [McpServerTool(Name = "list_types")]
    [Description("List type definitions in an assembly. Supports filter_regex, namespace, kind, visibility filters; paginated.")]
    public PageDto<TypeSummaryDto> ListTypes(
        [Description("Assembly id.")] string id,
        [Description("Optional regex over the type's full name.")] string? filter_regex = null,
        [Description("Filter by exact namespace match.")] string? @namespace = null,
        [Description("Filter by kind: class|struct|interface|enum|delegate.")] string? kind = null,
        [Description("Filter by visibility: public|internal|nested_public|...")] string? visibility = null,
        [Description("Pagination offset (default 0).")] int? offset = null,
        [Description("Pagination limit (default 100, max 1000).")] int? limit = null)
    {
        var ws = _registry.Get(id);
        var page = _types.List(ws, filter_regex, @namespace, kind, visibility, offset, limit);
        var dto = new Core.Envelope.Page<TypeSummaryDto>(
            page.Items.Select(s => new TypeSummaryDto(
                s.Token, s.Name, s.Namespace, s.FullName, s.Kind, s.Visibility, s.MethodCount, s.FieldCount)).ToList(),
            page.Offset, page.Limit, page.Total, page.HasMore);
        return PageDto<TypeSummaryDto>.From(dto);
    }
}
