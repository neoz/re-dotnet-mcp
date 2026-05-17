namespace ReDotnet.Core.Mutation;

public sealed class MutationLog
{
    private readonly Stack<MutationLogEntry> _undo = new();
    private readonly Stack<MutationLogEntry> _redo = new();

    public IReadOnlyCollection<MutationLogEntry> Undoable => _undo;
    public IReadOnlyCollection<MutationLogEntry> Redoable => _redo;

    public void Record(MutationLogEntry entry)
    {
        _undo.Push(entry);
        _redo.Clear();
    }

    public MutationLogEntry? Undo(Action<MutationLogEntry> revert)
    {
        if (_undo.Count == 0) return null;
        var e = _undo.Pop();
        revert(e);
        _redo.Push(e);
        return e;
    }

    public MutationLogEntry? Redo(Action<MutationLogEntry> reapply)
    {
        if (_redo.Count == 0) return null;
        var e = _redo.Pop();
        reapply(e);
        _undo.Push(e);
        return e;
    }
}
