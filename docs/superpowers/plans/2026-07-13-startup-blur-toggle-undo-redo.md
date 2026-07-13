# Startup Toggle, Blur Consecutive-Toggle, Undo/Redo Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a "launch at Windows startup" setting, fix the Blur toolbar
button so it stays armed across multiple blur boxes instead of disarming
after one, and add undo/redo for adding/removing blur boxes.

**Architecture:** Three independent additions to the existing WPF app
(`src/Snap`): a `StartupService` wrapping the `HKCU\...\Run` registry key
wired into `SettingsWindow`; a toggle-behavior fix to the existing
`_blurModeArmed` flag in `OverlayWindow`/`CaptureToolbar`; and a new
`BlurHistory` model (pure C#, no WPF dependency) wired into
`OverlayWindow`'s existing add/remove blur-box code paths, with `Ctrl+Z`
/ `Ctrl+Y` added to the existing `KeyDown` handler.

**Tech Stack:** C# / .NET 8, WPF, xunit (existing test project
`tests/Snap.Tests`), `Microsoft.Win32.Registry` (part of the Windows
base class library already available via `net8.0-windows`, no new
NuGet package needed).

## Global Constraints

- Target framework: `net8.0-windows` (matches `src/Snap/Snap.csproj` and
  `tests/Snap.Tests/Snap.Tests.csproj` — do not change).
- No new NuGet packages required for any task in this plan.
- Follow existing code style: nullable enabled, file-scoped namespaces,
  no comments except where a non-obvious constraint needs explaining.
- Undo/redo only tracks add/remove of blur boxes — radius (scroll-wheel)
  changes are never recorded in history.
- The "launch at startup" setting is derived live from the registry — it
  is never written to `AppSettings`/`settings.json`.

---

### Task 1: `BlurHistory` model + unit tests

**Files:**
- Create: `src/Snap/Models/BlurHistory.cs`
- Test: `tests/Snap.Tests/BlurHistoryTests.cs`

**Interfaces:**
- Consumes: `Snap.Models.BlurBox` (existing — has public `X`, `Y`,
  `Width`, `Height`, `Radius` int properties).
- Produces (used by Task 2):
  - `public class BlurHistory(List<BlurBox> boxes)`
  - `public void RecordAdd(BlurBox box)`
  - `public void RecordRemove(BlurBox box)`
  - `public BlurHistoryChange? Undo()`
  - `public BlurHistoryChange? Redo()`
  - `public record BlurHistoryChange(BlurBox Box, bool IsPresentAfter);`

- [ ] **Step 1: Write the failing tests**

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Snap.Tests --filter BlurHistoryTests`
Expected: FAIL / build error — `Snap.Models.BlurHistory` does not exist yet.

- [ ] **Step 3: Implement `BlurHistory`**

```csharp
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Snap.Tests --filter BlurHistoryTests`
Expected: PASS (8 tests)

- [ ] **Step 5: Commit**

```bash
git add src/Snap/Models/BlurHistory.cs tests/Snap.Tests/BlurHistoryTests.cs
git commit -m "feat: add BlurHistory for blur box undo/redo"
```

---

### Task 2: Wire `BlurHistory` into `OverlayWindow` (add/remove/undo/redo)

**Files:**
- Modify: `src/Snap/Views/OverlayWindow.xaml.cs`

**Interfaces:**
- Consumes: `Snap.Models.BlurHistory`, `Snap.Models.BlurHistoryChange`
  from Task 1.
- Produces: no new public surface — internal wiring only.

This task changes existing private methods `CommitBlurBox` and
`RenderBlurBox`, and the right-click removal handler and `KeyDown`
handler, all in `OverlayWindow.xaml.cs`. There is no separate WPF-free
unit under test here (it requires a live window), so verification is
manual, per the design's testing section.

- [ ] **Step 1: Add the history field and visual-tracking dictionary**

In `OverlayWindow.xaml.cs`, find this existing field block (around line
153-159):

```csharp
    private CaptureToolbar? _toolbar;
    protected readonly List<BlurBox> BlurBoxes = new();

    private bool _blurModeArmed;
    private bool _isDraggingBlurBox;
    private System.Windows.Point _blurDragStart;
    private System.Windows.Shapes.Rectangle? _pendingBlurRect;
```

Replace it with:

```csharp
    private CaptureToolbar? _toolbar;
    protected readonly List<BlurBox> BlurBoxes = new();
    private readonly BlurHistory _blurHistory;
    private readonly Dictionary<BlurBox, System.Windows.Controls.Image> _blurVisuals = new();

    private bool _blurModeArmed;
    private bool _isDraggingBlurBox;
    private System.Windows.Point _blurDragStart;
    private System.Windows.Shapes.Rectangle? _pendingBlurRect;
```

Then initialize `_blurHistory` at the end of the constructor. Find the
constructor's last line before its closing brace:

```csharp
        Loaded += OverlayWindow_Loaded;
        MouseLeftButtonDown += OverlayWindow_MouseLeftButtonDown;
        MouseMove += OverlayWindow_MouseMove;
        MouseLeftButtonUp += OverlayWindow_MouseLeftButtonUp;
    }
```

Replace it with:

```csharp
        Loaded += OverlayWindow_Loaded;
        MouseLeftButtonDown += OverlayWindow_MouseLeftButtonDown;
        MouseMove += OverlayWindow_MouseMove;
        MouseLeftButtonUp += OverlayWindow_MouseLeftButtonUp;

        _blurHistory = new BlurHistory(BlurBoxes);
    }
```

- [ ] **Step 2: Replace `CommitBlurBox` to record history instead of adding directly**

Find the existing `CommitBlurBox` method:

```csharp
    private void CommitBlurBox(System.Windows.Point end)
    {
        if (_pendingBlurRect is null)
        {
            return;
        }

        var rect = NormalizeRect(_blurDragStart, end);
        RootCanvas.Children.Remove(_pendingBlurRect);
        _pendingBlurRect = null;
        _blurModeArmed = false;

        var selectionRect = new Rect(Selection.X, Selection.Y, Selection.Width, Selection.Height);
        rect.Intersect(selectionRect);

        if (rect.Width < 4 || rect.Height < 4)
        {
            return;
        }

        var box = new BlurBox
        {
            X = (int)(rect.X - Selection.X),
            Y = (int)(rect.Y - Selection.Y),
            Width = (int)rect.Width,
            Height = (int)rect.Height,
            Radius = 12
        };

        BlurBoxes.Add(box);
        RenderBlurBox(box, rect);
    }
```

Replace it with (note `_blurModeArmed = false;` is intentionally removed
here — Task 3 makes disarming a toggle/toolbar concern instead):

```csharp
    private void CommitBlurBox(System.Windows.Point end)
    {
        if (_pendingBlurRect is null)
        {
            return;
        }

        var rect = NormalizeRect(_blurDragStart, end);
        RootCanvas.Children.Remove(_pendingBlurRect);
        _pendingBlurRect = null;

        var selectionRect = new Rect(Selection.X, Selection.Y, Selection.Width, Selection.Height);
        rect.Intersect(selectionRect);

        if (rect.Width < 4 || rect.Height < 4)
        {
            return;
        }

        var box = new BlurBox
        {
            X = (int)(rect.X - Selection.X),
            Y = (int)(rect.Y - Selection.Y),
            Width = (int)rect.Width,
            Height = (int)rect.Height,
            Radius = 12
        };

        _blurHistory.RecordAdd(box);
        RenderBlurBox(box);
    }
```

- [ ] **Step 3: Replace `RenderBlurBox` to derive its own screen rect and track its visual**

Find the existing `RenderBlurBox` method:

```csharp
    private void RenderBlurBox(BlurBox box, Rect screenRect)
    {
        using var regionCrop = BitmapUtil.Crop(FullCapture, new Rectangle(Selection.X, Selection.Y, Selection.Width, Selection.Height));
        using var blurred = BlurService.ApplyBlur(regionCrop, box.ToRectangle(), box.Radius);
        using var boxCrop = BitmapUtil.Crop(blurred, box.ToRectangle());

        var preview = new System.Windows.Controls.Image
        {
            Source = BitmapUtil.ToBitmapSource(boxCrop),
            Width = box.Width,
            Height = box.Height
        };

        preview.MouseWheel += (_, args) =>
        {
            box.Radius = Math.Clamp(box.Radius + (args.Delta > 0 ? 2 : -2), 1, 40);
            RootCanvas.Children.Remove(preview);
            RenderBlurBox(box, screenRect);
            args.Handled = true;
        };

        preview.MouseRightButtonDown += (_, args) =>
        {
            BlurBoxes.Remove(box);
            RootCanvas.Children.Remove(preview);
            args.Handled = true;
        };

        Canvas.SetLeft(preview, screenRect.X);
        Canvas.SetTop(preview, screenRect.Y);
        RootCanvas.Children.Add(preview);
    }
```

Replace it with:

```csharp
    private void RenderBlurBox(BlurBox box)
    {
        var screenRect = new Rect(Selection.X + box.X, Selection.Y + box.Y, box.Width, box.Height);

        using var regionCrop = BitmapUtil.Crop(FullCapture, new Rectangle(Selection.X, Selection.Y, Selection.Width, Selection.Height));
        using var blurred = BlurService.ApplyBlur(regionCrop, box.ToRectangle(), box.Radius);
        using var boxCrop = BitmapUtil.Crop(blurred, box.ToRectangle());

        var preview = new System.Windows.Controls.Image
        {
            Source = BitmapUtil.ToBitmapSource(boxCrop),
            Width = box.Width,
            Height = box.Height
        };

        preview.MouseWheel += (_, args) =>
        {
            box.Radius = Math.Clamp(box.Radius + (args.Delta > 0 ? 2 : -2), 1, 40);
            RemoveBlurVisual(box);
            RenderBlurBox(box);
            args.Handled = true;
        };

        preview.MouseRightButtonDown += (_, args) =>
        {
            _blurHistory.RecordRemove(box);
            RemoveBlurVisual(box);
            args.Handled = true;
        };

        Canvas.SetLeft(preview, screenRect.X);
        Canvas.SetTop(preview, screenRect.Y);
        RootCanvas.Children.Add(preview);
        _blurVisuals[box] = preview;
    }

    private void RemoveBlurVisual(BlurBox box)
    {
        if (_blurVisuals.Remove(box, out var preview))
        {
            RootCanvas.Children.Remove(preview);
        }
    }

    private void UndoBlur()
    {
        var change = _blurHistory.Undo();
        if (change is not null)
        {
            ApplyHistoryChange(change);
        }
    }

    private void RedoBlur()
    {
        var change = _blurHistory.Redo();
        if (change is not null)
        {
            ApplyHistoryChange(change);
        }
    }

    private void ApplyHistoryChange(BlurHistoryChange change)
    {
        if (change.IsPresentAfter)
        {
            RenderBlurBox(change.Box);
        }
        else
        {
            RemoveBlurVisual(change.Box);
        }
    }
```

- [ ] **Step 4: Add `Ctrl+Z` / `Ctrl+Y` to the existing `KeyDown` handler**

Find the existing handler:

```csharp
    private void OverlayWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
```

Replace it with:

```csharp
    private void OverlayWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            return;
        }

        if (Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        if (e.Key == Key.Z)
        {
            UndoBlur();
        }
        else if (e.Key == Key.Y)
        {
            RedoBlur();
        }
    }
```

- [ ] **Step 5: Add the missing `using` for `Dictionary`**

At the top of `OverlayWindow.xaml.cs`, confirm `using
System.Collections.Generic;` is present (it already is, for `List<>`) —
no change needed here, just verify before building.

- [ ] **Step 6: Build to verify no compile errors**

Run: `dotnet build src/Snap`
Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Manual verification (live app)**

Run: `dotnet run --project src/Snap`

1. Press `Ctrl+Shift+S`, drag a region, click **Blur**, drag two
   separate blur boxes one after another (without clicking Blur again —
   this still requires Task 3 to stay armed across boxes, but a single
   box's add/remove/undo/redo can be verified now).
2. Right-click a blur box to remove it, then press `Ctrl+Z` — the box
   should reappear blurred at the same location.
3. Press `Ctrl+Y` — the box should disappear again.
4. Add a new blur box, then press `Ctrl+Z` — it should disappear.
5. Press `Ctrl+Z` repeatedly with no more history — nothing should
   happen or throw.

- [ ] **Step 8: Commit**

```bash
git add src/Snap/Views/OverlayWindow.xaml.cs
git commit -m "feat: wire blur box undo/redo into OverlayWindow"
```

---

### Task 3: Blur toggle fix (consecutive blurring)

**Files:**
- Modify: `src/Snap/Views/CaptureToolbar.xaml.cs`
- Modify: `src/Snap/Views/OverlayWindow.xaml.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `CaptureToolbar.SetBlurArmed(bool armed)` (used by
  `OverlayWindow`).

No isolated unit to test here (toolbar visuals + armed-state require a
live window) — verified manually per the design's testing section.

- [ ] **Step 1: Add `SetBlurArmed` to `CaptureToolbar`**

In `src/Snap/Views/CaptureToolbar.xaml.cs`, replace the full file
content with:

```csharp
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Snap.Views;

public partial class CaptureToolbar : UserControl
{
    private static readonly Brush ArmedBrush = Brushes.DarkOrange;

    public event Action? BlurClicked;
    public event Action? CopyClicked;
    public event Action? SaveClicked;
    public event Action? CancelClicked;

    public CaptureToolbar()
    {
        InitializeComponent();
    }

    public void SetBlurArmed(bool armed)
    {
        if (armed)
        {
            BlurButton.Background = ArmedBrush;
        }
        else
        {
            BlurButton.ClearValue(BackgroundProperty);
        }
    }

    private void BlurButton_Click(object sender, RoutedEventArgs e) => BlurClicked?.Invoke();
    private void CopyButton_Click(object sender, RoutedEventArgs e) => CopyClicked?.Invoke();
    private void SaveButton_Click(object sender, RoutedEventArgs e) => SaveClicked?.Invoke();
    private void CancelButton_Click(object sender, RoutedEventArgs e) => CancelClicked?.Invoke();
}
```

- [ ] **Step 2: Make the Blur click handler in `OverlayWindow` a toggle, and disarm on the other toolbar actions**

Find the existing wiring in `OnSelectionLocked`:

```csharp
    private void OnSelectionLocked(Rect selectionRect)
    {
        _toolbar = new CaptureToolbar();
        _toolbar.BlurClicked += OnBlurClicked;
        _toolbar.CopyClicked += () => Export(copyToClipboard: true);
        _toolbar.SaveClicked += () => Export(copyToClipboard: false);
        _toolbar.CancelClicked += Close;

        Canvas.SetLeft(_toolbar, selectionRect.X);
        Canvas.SetTop(_toolbar, selectionRect.Y + selectionRect.Height + 6);
        RootCanvas.Children.Add(_toolbar);
    }

    private void OnBlurClicked()
    {
        _blurModeArmed = true;
    }
```

Replace it with:

```csharp
    private void OnSelectionLocked(Rect selectionRect)
    {
        _toolbar = new CaptureToolbar();
        _toolbar.BlurClicked += OnBlurClicked;
        _toolbar.CopyClicked += () => { DisarmBlurMode(); Export(copyToClipboard: true); };
        _toolbar.SaveClicked += () => { DisarmBlurMode(); Export(copyToClipboard: false); };
        _toolbar.CancelClicked += () => { DisarmBlurMode(); Close(); };

        Canvas.SetLeft(_toolbar, selectionRect.X);
        Canvas.SetTop(_toolbar, selectionRect.Y + selectionRect.Height + 6);
        RootCanvas.Children.Add(_toolbar);
    }

    private void OnBlurClicked()
    {
        _blurModeArmed = !_blurModeArmed;
        _toolbar?.SetBlurArmed(_blurModeArmed);
    }

    private void DisarmBlurMode()
    {
        _blurModeArmed = false;
        _toolbar?.SetBlurArmed(false);
    }
```

- [ ] **Step 3: Build to verify no compile errors**

Run: `dotnet build src/Snap`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Manual verification (live app)**

Run: `dotnet run --project src/Snap`

1. Capture a region, click **Blur** — button should visibly change
   color (armed).
2. Drag a blur box — after it commits, the Blur button should still
   show armed, and dragging a second box immediately (no extra click)
   should work.
3. Click **Blur** again — button should return to its default color
   (disarmed); dragging inside the selection should no longer create a
   blur box.
4. Arm blur mode, then click **Copy** or **Save** — capture completes
   normally (button state doesn't matter once the window closes).

- [ ] **Step 5: Commit**

```bash
git add src/Snap/Views/CaptureToolbar.xaml.cs src/Snap/Views/OverlayWindow.xaml.cs
git commit -m "fix: blur toggle stays armed for consecutive blur boxes"
```

---

### Task 4: `StartupService` + unit tests

**Files:**
- Create: `src/Snap/Services/StartupService.cs`
- Test: `tests/Snap.Tests/StartupServiceTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces (used by Task 5):
  - `public class StartupService`
  - `public bool IsEnabled()`
  - `public void SetEnabled(bool enabled)`
  - `public static bool PathsMatch(string? stored, string current)`
    (pure logic, unit tested directly; also used internally by
    `IsEnabled`)

- [ ] **Step 1: Write the failing test for the pure path-matching logic**

```csharp
using Snap.Services;
using Xunit;

namespace Snap.Tests;

public class StartupServiceTests
{
    [Fact]
    public void PathsMatch_WhenStoredEqualsCurrent_ReturnsTrue()
    {
        Assert.True(StartupService.PathsMatch(@"C:\Apps\Snap.exe", @"C:\Apps\Snap.exe"));
    }

    [Fact]
    public void PathsMatch_IsCaseInsensitive()
    {
        Assert.True(StartupService.PathsMatch(@"C:\Apps\Snap.exe", @"c:\apps\snap.exe"));
    }

    [Fact]
    public void PathsMatch_WhenStoredIsNull_ReturnsFalse()
    {
        Assert.False(StartupService.PathsMatch(null, @"C:\Apps\Snap.exe"));
    }

    [Fact]
    public void PathsMatch_WhenStoredIsEmpty_ReturnsFalse()
    {
        Assert.False(StartupService.PathsMatch(string.Empty, @"C:\Apps\Snap.exe"));
    }

    [Fact]
    public void PathsMatch_WhenPathsDiffer_ReturnsFalse()
    {
        Assert.False(StartupService.PathsMatch(@"C:\Apps\Other.exe", @"C:\Apps\Snap.exe"));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Snap.Tests --filter StartupServiceTests`
Expected: FAIL / build error — `Snap.Services.StartupService` does not
exist yet.

- [ ] **Step 3: Implement `StartupService`**

```csharp
using System;
using Microsoft.Win32;

namespace Snap.Services;

public class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Snap";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        var stored = key?.GetValue(ValueName) as string;
        return PathsMatch(stored, GetExecutablePath());
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (enabled)
        {
            key.SetValue(ValueName, GetExecutablePath());
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    public static bool PathsMatch(string? stored, string current)
    {
        return !string.IsNullOrEmpty(stored) && string.Equals(stored, current, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetExecutablePath()
    {
        return Environment.ProcessPath ?? throw new InvalidOperationException("Unable to determine the current executable path.");
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Snap.Tests --filter StartupServiceTests`
Expected: PASS (5 tests)

- [ ] **Step 5: Commit**

```bash
git add src/Snap/Services/StartupService.cs tests/Snap.Tests/StartupServiceTests.cs
git commit -m "feat: add StartupService for Windows Run-key registration"
```

---

### Task 5: Wire startup toggle into `SettingsWindow`

**Files:**
- Modify: `src/Snap/Views/SettingsWindow.xaml`
- Modify: `src/Snap/Views/SettingsWindow.xaml.cs`

**Interfaces:**
- Consumes: `Snap.Services.StartupService.IsEnabled()` /
  `.SetEnabled(bool)` from Task 4.
- Produces: nothing new (leaf UI wiring).

Registry read/write is a live-Windows-session concern — verified
manually, per the design's testing section.

- [ ] **Step 1: Add the checkbox to `SettingsWindow.xaml`**

Find the existing checkbox line:

```xml
        <CheckBox x:Name="CopyPathCheckBox" Grid.Row="2" Content="Copy file path to clipboard after saving" Margin="0,12,0,0"/>
```

The window currently has 5 rows (`Auto, Auto, Auto, *, Auto`) with the
checkbox in row 2 and the Save/Cancel buttons in row 4. Add a new row
for the startup checkbox by changing the `Window` height and
`Grid.RowDefinitions`, and inserting a second checkbox in a new row.

Replace:

```xml
Title="Snap Settings" Height="180" Width="420"
        WindowStartupLocation="CenterScreen" ResizeMode="NoResize">
    <Grid Margin="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
```

with:

```xml
Title="Snap Settings" Height="210" Width="420"
        WindowStartupLocation="CenterScreen" ResizeMode="NoResize">
    <Grid Margin="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
```

Then replace:

```xml
        <CheckBox x:Name="CopyPathCheckBox" Grid.Row="2" Content="Copy file path to clipboard after saving" Margin="0,12,0,0"/>

        <StackPanel Grid.Row="4" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,16,0,0">
            <Button x:Name="SaveButton" Content="Save" Width="80" Margin="0,0,8,0" Click="SaveButton_Click" IsDefault="True"/>
            <Button x:Name="CancelButton" Content="Cancel" Width="80" Click="CancelButton_Click" IsCancel="True"/>
        </StackPanel>
```

with:

```xml
        <CheckBox x:Name="CopyPathCheckBox" Grid.Row="2" Content="Copy file path to clipboard after saving" Margin="0,12,0,0"/>

        <CheckBox x:Name="LaunchAtStartupCheckBox" Grid.Row="3" Content="Launch Snap at startup" Margin="0,8,0,0"/>

        <StackPanel Grid.Row="5" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,16,0,0">
            <Button x:Name="SaveButton" Content="Save" Width="80" Margin="0,0,8,0" Click="SaveButton_Click" IsDefault="True"/>
            <Button x:Name="CancelButton" Content="Cancel" Width="80" Click="CancelButton_Click" IsCancel="True"/>
        </StackPanel>
```

- [ ] **Step 2: Wire the checkbox in `SettingsWindow.xaml.cs`**

Find the existing file content:

```csharp
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Snap.Models;
using Snap.Services;

namespace Snap.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;

    public SettingsWindow(SettingsService settingsService, AppSettings settings)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _settings = settings;
        FolderTextBox.Text = _settings.SaveFolder;
        CopyPathCheckBox.IsChecked = _settings.CopyPathOnSave;

        using var iconStream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Snap.Resources.app.ico");
        if (iconStream is not null)
        {
            Icon = BitmapFrame.Create(iconStream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        }
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            InitialDirectory = Directory.Exists(FolderTextBox.Text) ? FolderTextBox.Text : AppSettings.GetDefaultSaveFolder(),
            Title = "Choose screenshot save folder"
        };

        if (dialog.ShowDialog() == true)
        {
            FolderTextBox.Text = dialog.FolderName;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.SaveFolder = FolderTextBox.Text;
        _settings.CopyPathOnSave = CopyPathCheckBox.IsChecked ?? true;
        _settingsService.Save(_settings);
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
```

Replace it with:

```csharp
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Snap.Models;
using Snap.Services;

namespace Snap.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly StartupService _startupService = new();

    public SettingsWindow(SettingsService settingsService, AppSettings settings)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _settings = settings;
        FolderTextBox.Text = _settings.SaveFolder;
        CopyPathCheckBox.IsChecked = _settings.CopyPathOnSave;
        LaunchAtStartupCheckBox.IsChecked = _startupService.IsEnabled();

        using var iconStream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Snap.Resources.app.ico");
        if (iconStream is not null)
        {
            Icon = BitmapFrame.Create(iconStream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        }
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            InitialDirectory = Directory.Exists(FolderTextBox.Text) ? FolderTextBox.Text : AppSettings.GetDefaultSaveFolder(),
            Title = "Choose screenshot save folder"
        };

        if (dialog.ShowDialog() == true)
        {
            FolderTextBox.Text = dialog.FolderName;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.SaveFolder = FolderTextBox.Text;
        _settings.CopyPathOnSave = CopyPathCheckBox.IsChecked ?? true;
        _settingsService.Save(_settings);
        _startupService.SetEnabled(LaunchAtStartupCheckBox.IsChecked ?? false);
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
```

- [ ] **Step 3: Build to verify no compile errors**

Run: `dotnet build src/Snap`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Manual verification (live app)**

Run: `dotnet run --project src/Snap`

1. Open tray icon → **Settings**. "Launch Snap at startup" should be
   unchecked (assuming it was never enabled before).
2. Check it, click **Save**. Reopen Settings — it should still be
   checked.
3. Open `regedit` (or `reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Run"`)
   and confirm a `Snap` value now points at the running executable's path.
4. Uncheck it, click **Save**. Confirm the registry value is gone.

- [ ] **Step 5: Commit**

```bash
git add src/Snap/Views/SettingsWindow.xaml src/Snap/Views/SettingsWindow.xaml.cs
git commit -m "feat: add launch-at-startup toggle to Settings"
```

---

## Final Verification

- [ ] Run the full test suite: `dotnet test`
  Expected: all tests pass, including the 8 new `BlurHistoryTests` and
  5 new `StartupServiceTests`.
- [ ] Run through the manual verification steps from Tasks 2, 3, and 5
  once more in a single session to confirm the three features work
  together (e.g. arm Blur, add multiple boxes, undo one, redo it, then
  toggle Blur off, Copy, and separately confirm the startup checkbox
  round-trips through the registry).
