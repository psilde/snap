using System.Collections.Generic;

namespace Snap.Models;

public enum BlurActionType
{
    Add,
    Remove
}

public record BlurHistoryChange(BlurBox Box, bool IsPresentAfter);

public class BlurHistory
{
    private readonly List<BlurBox> _boxes;
    private readonly Stack<(BlurActionType Type, BlurBox Box)> _undo = new();
    private readonly Stack<(BlurActionType Type, BlurBox Box)> _redo = new();

    public BlurHistory(List<BlurBox> boxes)
    {
        _boxes = boxes;
    }

    public void RecordAdd(BlurBox box)
    {
        _boxes.Add(box);
        _undo.Push((BlurActionType.Add, box));
        _redo.Clear();
    }

    public void RecordRemove(BlurBox box)
    {
        _boxes.Remove(box);
        _undo.Push((BlurActionType.Remove, box));
        _redo.Clear();
    }

    public BlurHistoryChange? Undo()
    {
        if (_undo.Count == 0)
        {
            return null;
        }

        var action = _undo.Pop();
        var isPresentAfter = ApplyInverse(action);
        _redo.Push(action);
        return new BlurHistoryChange(action.Box, isPresentAfter);
    }

    public BlurHistoryChange? Redo()
    {
        if (_redo.Count == 0)
        {
            return null;
        }

        var action = _redo.Pop();
        var isPresentAfter = Apply(action);
        _undo.Push(action);
        return new BlurHistoryChange(action.Box, isPresentAfter);
    }

    private bool Apply((BlurActionType Type, BlurBox Box) action)
    {
        if (action.Type == BlurActionType.Add)
        {
            _boxes.Add(action.Box);
            return true;
        }

        _boxes.Remove(action.Box);
        return false;
    }

    private bool ApplyInverse((BlurActionType Type, BlurBox Box) action)
    {
        if (action.Type == BlurActionType.Add)
        {
            _boxes.Remove(action.Box);
            return false;
        }

        _boxes.Add(action.Box);
        return true;
    }
}
