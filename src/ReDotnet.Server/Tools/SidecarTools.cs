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
        => _registry.Run(id, ws => _svc.GetComment(ws, token));

    [McpServerTool(Name = "list_bookmarks")]
    [Description("List all bookmarks from the sidecar.")]
    public IReadOnlyList<BookmarkEntry> ListBookmarks([Description("Assembly id.")] string id)
        => _registry.Run(id, ws => _svc.ListBookmarks(ws));

    [McpServerTool(Name = "get_color")]
    [Description("Get the sidecar color tag for a token, if any.")]
    public string? GetColor(
        [Description("Assembly id.")] string id,
        [Description("Token to look up.")] string token)
        => _registry.Run(id, ws => _svc.GetColor(ws, token));
}
