using System.ComponentModel;
using ModelContextProtocol.Server;
using ReDotnet.Core.Resources;
using ReDotnet.Core.Workspace;
using ReDotnet.Server.Envelope;

namespace ReDotnet.Server.Tools;

[McpServerToolType]
public sealed class ResourceTools
{
    private readonly WorkspaceRegistry _registry;
    private readonly ResourceService _svc;

    public ResourceTools(WorkspaceRegistry registry, ResourceService svc)
    {
        _registry = registry;
        _svc = svc;
    }

    [McpServerTool(Name = "list_resources")]
    [Description("List manifest resources (embedded or linked) with sizes.")]
    public PageDto<ManifestResourceInfo> ListResources(
        [Description("Assembly id.")] string id,
        int? offset = null, int? limit = null)
        => _registry.Run(id, ws => PageDto<ManifestResourceInfo>.From(_svc.ListResources(ws, offset, limit)));

    [McpServerTool(Name = "read_resource")]
    [Description("Read a manifest resource, capped at max_bytes; returns hex preview + auto-detected encoding.")]
    public ResourceData ReadResource(
        [Description("Assembly id.")] string id,
        [Description("Resource name (matches the manifest entry exactly).")] string name,
        [Description("Cap on bytes returned (default 4096).")] int max_bytes = 4096)
        => _registry.Run(id, ws => _svc.ReadResource(ws, name, max_bytes));

    [McpServerTool(Name = "list_custom_attributes")]
    [Description("List custom attributes by attribute-type substring and/or owner token. Both filters optional.")]
    public PageDto<CustomAttrInfo> ListCustomAttributes(
        [Description("Assembly id.")] string id,
        [Description("Optional owner identifier to scope the listing.")] string? target = null,
        [Description("Optional substring filter on the attribute type's full name.")] string? type_filter = null,
        int? offset = null, int? limit = null)
        => _registry.Run(id, ws => PageDto<CustomAttrInfo>.From(_svc.ListCustomAttributes(ws, target, type_filter, offset, limit)));
}
