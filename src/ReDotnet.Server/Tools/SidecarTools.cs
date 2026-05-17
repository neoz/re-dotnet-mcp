using System.ComponentModel;
using ModelContextProtocol.Server;
using ReDotnet.Core.Sidecar;
using ReDotnet.Core.Workspace;

namespace ReDotnet.Server.Tools;

[McpServerToolType]
public sealed class SidecarTools
{
    private readonly WorkspaceRegistry _registry;
    private readonly SidecarReadService _svc;

    public SidecarTools(WorkspaceRegistry registry, SidecarReadService svc)
    {
        _registry = registry;
        _svc = svc;
    }

    [McpServerTool(Name = "get_comment")]
    [Description("Get the sidecar comment for a token, if any.")]
    public string? GetComment(
        [Description("Assembly id.")] string id,
        [Description("Token to look up (e.g. '0x06000123').")] string token)
        => _svc.GetComment(_registry.Get(id), token);

    [McpServerTool(Name = "list_bookmarks")]
    [Description("List all bookmarks from the sidecar.")]
    public IReadOnlyList<BookmarkEntry> ListBookmarks([Description("Assembly id.")] string id)
        => _svc.ListBookmarks(_registry.Get(id));

    [McpServerTool(Name = "get_color")]
    [Description("Get the sidecar color tag for a token, if any.")]
    public string? GetColor(
        [Description("Assembly id.")] string id,
        [Description("Token to look up.")] string token)
        => _svc.GetColor(_registry.Get(id), token);
}
