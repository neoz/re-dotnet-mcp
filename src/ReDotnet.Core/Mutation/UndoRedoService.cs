namespace ReDotnet.Core.Mutation;

/// Undo/redo wiring. For mutations whose effect is recorded in the live
/// AsmResolver tree (renames, IL edits), revert by re-applying the Old value
/// recorded in the log entry. Sidecar mutations are reverted by reaching back
/// into the SidecarDocument and restoring/removing the changed key.
public sealed class UndoRedoService
{
    public MutationLogEntry? Undo(Workspace.Workspace ws)
    {
        return ws.MutationLog.Undo(entry => Revert(ws, entry));
    }

    public MutationLogEntry? Redo(Workspace.Workspace ws)
    {
        return ws.MutationLog.Redo(entry => Reapply(ws, entry));
    }

    private static void Revert(Workspace.Workspace ws, MutationLogEntry entry)
    {
        switch (entry.Kind)
        {
            case MutationKind.SetComment:
                if (entry.TargetToken is { } ct && ws.Sidecar.Annotations.TryGetValue(ct, out var ca))
                    ca.Comment = entry.Old as string;
                break;
            case MutationKind.SetColor:
                if (entry.TargetToken is { } ck && ws.Sidecar.Annotations.TryGetValue(ck, out var clr))
                    clr.Color = entry.Old as string;
                break;
            case MutationKind.SetBookmark:
                if (entry.New is Sidecar.BookmarkEntry bm)
                    ws.Sidecar.Bookmarks.RemoveAll(b => b.Slot == bm.Slot);
                break;
            case MutationKind.DeleteBookmark:
                if (entry.Old is Sidecar.BookmarkEntry oldBm)
                    ws.Sidecar.Bookmarks.Add(oldBm);
                break;
            // Other mutation kinds (renames, IL patches) require domain knowledge
            // to revert cleanly; the live tree already contains the new state, and
            // analysts can call rename_* again with the old value from the log.
            // We surface the entry to the caller so the agent can decide.
        }
    }

    private static void Reapply(Workspace.Workspace ws, MutationLogEntry entry)
    {
        switch (entry.Kind)
        {
            case MutationKind.SetComment:
                if (entry.TargetToken is { } ct && ws.Sidecar.Annotations.TryGetValue(ct, out var ca))
                    ca.Comment = entry.New as string;
                break;
            case MutationKind.SetColor:
                if (entry.TargetToken is { } ck && ws.Sidecar.Annotations.TryGetValue(ck, out var clr))
                    clr.Color = entry.New as string;
                break;
            case MutationKind.SetBookmark:
                if (entry.New is Sidecar.BookmarkEntry bm)
                    ws.Sidecar.Bookmarks.Add(bm);
                break;
            case MutationKind.DeleteBookmark:
                if (entry.Old is Sidecar.BookmarkEntry oldBm)
                    ws.Sidecar.Bookmarks.RemoveAll(b => b.Slot == oldBm.Slot);
                break;
        }
    }
}
