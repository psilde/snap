using System.Collections.Generic;
using Snap.Models;
using Xunit;

namespace Snap.Tests;

public class BlurHistoryTests
{
    private static BlurBox MakeBox(int x = 0) => new() { X = x, Y = 0, Width = 10, Height = 10, Radius = 12 };

    [Fact]
    public void RecordAdd_AddsBoxToList()
    {
        var boxes = new List<BlurBox>();
        var history = new BlurHistory(boxes);
        var box = MakeBox();

        history.RecordAdd(box);

        Assert.Single(boxes);
        Assert.Same(box, boxes[0]);
    }

    [Fact]
    public void RecordRemove_RemovesBoxFromList()
    {
        var box = MakeBox();
        var boxes = new List<BlurBox> { box };
        var history = new BlurHistory(boxes);

        history.RecordRemove(box);

        Assert.Empty(boxes);
    }

    [Fact]
    public void Undo_OnAdd_RemovesBoxAndReportsNotPresent()
    {
        var boxes = new List<BlurBox>();
        var history = new BlurHistory(boxes);
        var box = MakeBox();
        history.RecordAdd(box);

        var change = history.Undo();

        Assert.NotNull(change);
        Assert.Same(box, change!.Box);
        Assert.False(change.IsPresentAfter);
        Assert.Empty(boxes);
    }

    [Fact]
    public void Undo_OnRemove_ReAddsBoxAndReportsPresent()
    {
        var box = MakeBox();
        var boxes = new List<BlurBox> { box };
        var history = new BlurHistory(boxes);
        history.RecordRemove(box);

        var change = history.Undo();

        Assert.NotNull(change);
        Assert.Same(box, change!.Box);
        Assert.True(change.IsPresentAfter);
        Assert.Same(box, Assert.Single(boxes));
    }

    [Fact]
    public void Redo_AfterUndoOfAdd_ReAddsBox()
    {
        var boxes = new List<BlurBox>();
        var history = new BlurHistory(boxes);
        var box = MakeBox();
        history.RecordAdd(box);
        history.Undo();

        var change = history.Redo();

        Assert.NotNull(change);
        Assert.True(change!.IsPresentAfter);
        Assert.Same(box, Assert.Single(boxes));
    }

    [Fact]
    public void Undo_OnEmptyHistory_ReturnsNull()
    {
        var history = new BlurHistory(new List<BlurBox>());

        Assert.Null(history.Undo());
    }

    [Fact]
    public void Redo_OnEmptyHistory_ReturnsNull()
    {
        var history = new BlurHistory(new List<BlurBox>());

        Assert.Null(history.Redo());
    }

    [Fact]
    public void RecordAdd_AfterUndo_ClearsRedoStack()
    {
        var boxes = new List<BlurBox>();
        var history = new BlurHistory(boxes);
        history.RecordAdd(MakeBox(0));
        history.Undo();

        history.RecordAdd(MakeBox(1));

        Assert.Null(history.Redo());
    }
}
