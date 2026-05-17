using System.ComponentModel;
using ModelContextProtocol.Server;
using ReDotnet.Core.Envelope;
using ReDotnet.Core.Inspection;
using ReDotnet.Core.Workspace;
using ReDotnet.Server.Envelope;

namespace ReDotnet.Server.Tools;

[McpServerToolType]
public sealed class MemberTools
{
    private readonly WorkspaceRegistry _registry;
    private readonly MemberInspectionService _svc;

    public MemberTools(WorkspaceRegistry registry, MemberInspectionService svc)
    {
        _registry = registry;
        _svc = svc;
    }

    [McpServerTool(Name = "list_fields")]
    [Description("List fields; optionally restrict to a single declaring type. Paginated.")]
    public PageDto<FieldSummary> ListFields(
        [Description("Assembly id.")] string id,
        [Description("Optional type identifier to restrict scope.")] string? type = null,
        int? offset = null, int? limit = null)
        => _registry.Run(id, ws => PageDto<FieldSummary>.From(_svc.ListFields(ws, type, offset, limit)));

    [McpServerTool(Name = "get_field")]
    [Description("Get a field's signature, modifiers, and (if literal) constant value.")]
    public FieldDetail GetField(
        [Description("Assembly id.")] string id,
        [Description("Field identifier.")] string identifier)
        => _registry.Run(id, ws => _svc.GetField(ws, identifier));

    [McpServerTool(Name = "get_field_rva_data")]
    [Description("Read the raw bytes of a field's mapped RVA initializer (e.g. compiler-generated <PrivateImplementationDetails> blobs). Capped at max_bytes; returns hex preview + base64.")]
    public FieldRvaData GetFieldRvaData(
        [Description("Assembly id.")] string id,
        [Description("Field identifier (token, @0xRVA, {module,token}, or FQN).")] string identifier,
        [Description("Cap on bytes returned (default 4096).")] int max_bytes = 4096)
        => _registry.Run(id, ws => _svc.GetFieldRvaData(ws, identifier, max_bytes));

    [McpServerTool(Name = "list_properties")]
    [Description("List properties; optionally restrict to a single declaring type. Paginated.")]
    public PageDto<PropertySummary> ListProperties(
        [Description("Assembly id.")] string id,
        [Description("Optional type identifier.")] string? type = null,
        int? offset = null, int? limit = null)
        => _registry.Run(id, ws => PageDto<PropertySummary>.From(_svc.ListProperties(ws, type, offset, limit)));

    [McpServerTool(Name = "get_property")]
    [Description("Get a property's type and getter/setter method tokens.")]
    public PropertySummary GetProperty(
        [Description("Assembly id.")] string id,
        [Description("Property identifier.")] string identifier)
        => _registry.Run(id, ws => _svc.GetProperty(ws, identifier));

    [McpServerTool(Name = "list_events")]
    [Description("List events; optionally restrict to a single declaring type. Paginated.")]
    public PageDto<EventSummary> ListEvents(
        [Description("Assembly id.")] string id,
        [Description("Optional type identifier.")] string? type = null,
        int? offset = null, int? limit = null)
        => _registry.Run(id, ws => PageDto<EventSummary>.From(_svc.ListEvents(ws, type, offset, limit)));

    [McpServerTool(Name = "get_event")]
    [Description("Get an event's handler type and add/remove method tokens.")]
    public EventSummary GetEvent(
        [Description("Assembly id.")] string id,
        [Description("Event identifier.")] string identifier)
        => _registry.Run(id, ws => _svc.GetEvent(ws, identifier));
}
