using ReDotnet.Core.Envelope;

namespace ReDotnet.Core.Sidecar;

public sealed class SidecarReadService
{
    public string? GetComment(Workspace.Workspace ws, string token)
    {
        return ws.Sidecar.Annotations.TryGetValue(token, out var a) ? a.Comment : null;
    }

    public IReadOnlyList<BookmarkEntry> ListBookmarks(Workspace.Workspace ws)
    {
        return ws.Sidecar.Bookmarks;
    }

    public string? GetColor(Workspace.Workspace ws, string token)
    {
        return ws.Sidecar.Annotations.TryGetValue(token, out var a) ? a.Color : null;
    }

    public string? GetIlComment(Workspace.Workspace ws, string methodToken, string ilOffset)
    {
        if (!ws.Sidecar.IlComments.TryGetValue(methodToken, out var byOffset)) return null;
        return byOffset.TryGetValue(ilOffset, out var c) ? c : null;
    }
}
