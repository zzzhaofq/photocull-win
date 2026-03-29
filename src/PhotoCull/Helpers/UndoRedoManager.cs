namespace PhotoCull.Helpers;

public class UndoRedoManager
{
    private readonly Stack<(Action Undo, Action Redo)> _undoStack = new();
    private readonly Stack<(Action Undo, Action Redo)> _redoStack = new();

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public void RegisterUndo(Action undoAction, Action redoAction)
    {
        _undoStack.Push((undoAction, redoAction));
        _redoStack.Clear();
    }

    public void Undo()
    {
        if (_undoStack.Count == 0) return;
        var (undoAction, redoAction) = _undoStack.Pop();
        undoAction();
        _redoStack.Push((undoAction, redoAction));
    }

    public void Redo()
    {
        if (_redoStack.Count == 0) return;
        var (undoAction, redoAction) = _redoStack.Pop();
        redoAction();
        _undoStack.Push((undoAction, redoAction));
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}
