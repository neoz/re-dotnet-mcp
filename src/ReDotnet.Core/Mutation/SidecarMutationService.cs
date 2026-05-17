using ReDotnet.Core.Sidecar;

namespace ReDotnet.Core.Mutation;

public sealed class SidecarMutationService
{
    public SetResult SetComment(Workspace.Workspace ws, string token, string? text)
    {
        var sidecar = ws.Sidecar;
        sidecar.Annotations.TryGetValue(token, out var existing);
        var old = existing?.Comment;
        if (!sidecar.Annotations.TryGetValue(token, out var entry))
            sidecar.Annotations[token] = entry = new AnnotationEntry();
        entry.Comment = text;
        ws.MutationLog.Record(new MutationLogEntry(
            MutationKind.SetComment, ws.AssemblyId, token, old, text, DateTimeOffset.UtcNow));
        return new SetResult(token, old, text);
    }

    public SetResult SetIlComment(Workspace.Workspace ws, string methodToken, string ilOffset, string? text)
    {
        var sidecar = ws.Sidecar;
        if (!sidecar.IlComments.TryGetValue(methodToken, out var byOffset))
            sidecar.IlComments[methodToken] = byOffset = new();
        byOffset.TryGetValue(ilOffset, out var old);
        if (text is null) byOffset.Remove(ilOffset);
        else byOffset[ilOffset] = text;
        ws.MutationLog.Record(new MutationLogEntry(
            MutationKind.SetMethodIlComment, ws.AssemblyId, methodToken,
            new { offset = ilOffset, text = old }, new { offset = ilOffset, text }, DateTimeOffset.UtcNow));
        return new SetResult($"{methodToken}@{ilOffset}", old, text);
    }

    public BookmarkEntry SetBookmark(Workspace.Workspace ws, string token, string? description)
    {
        var sidecar = ws.Sidecar;
        var slot = sidecar.Bookmarks.Count == 0 ? 0 : sidecar.Bookmarks.Max(b => b.Slot) + 1;
        var bm = new BookmarkEntry { Slot = slot, Token = token, Description = description };
        sidecar.Bookmarks.Add(bm);
        ws.MutationLog.Record(new MutationLogEntry(
            MutationKind.SetBookmark, ws.AssemblyId, token, null, bm, DateTimeOffset.UtcNow));
        return bm;
    }

    public bool DeleteBookmark(Workspace.Workspace ws, int slot)
    {
        var sidecar = ws.Sidecar;
        var existing = sidecar.Bookmarks.FirstOrDefault(b => b.Slot == slot);
        if (existing is null) return false;
        sidecar.Bookmarks.Remove(existing);
        ws.MutationLog.Record(new MutationLogEntry(
            MutationKind.DeleteBookmark, ws.AssemblyId, existing.Token, existing, null, DateTimeOffset.UtcNow));
        return true;
    }

    public SetResult SetColor(Workspace.Workspace ws, string token, string? color)
    {
        var sidecar = ws.Sidecar;
        sidecar.Annotations.TryGetValue(token, out var existing);
        var old = existing?.Color;
        if (!sidecar.Annotations.TryGetValue(token, out var entry))
            sidecar.Annotations[token] = entry = new AnnotationEntry();
        entry.Color = color;
        ws.MutationLog.Record(new MutationLogEntry(
            MutationKind.SetColor, ws.AssemblyId, token, old, color, DateTimeOffset.UtcNow));
        return new SetResult(token, old, color);
    }
}

public sealed record SetResult(string Target, string? OldValue, string? NewValue);
