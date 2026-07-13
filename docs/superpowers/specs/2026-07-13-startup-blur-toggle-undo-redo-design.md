# Startup Toggle, Consecutive Blur Toggle, Blur Undo/Redo — Design

Date: 2026-07-13

## Summary

Three additions to the existing Snap screenshot/blur tool:

1. A "Launch Snap at startup" setting, backed by the Windows registry Run key.
2. A fix to the existing Blur toolbar button so it behaves as a true toggle,
   allowing multiple blur boxes to be dragged consecutively without
   re-clicking Blur each time (the original design intended this; the current
   code auto-disarms after one box).
3. Undo/redo for adding and removing blur boxes during a capture session.

## 1. Launch at startup

- New `Snap.Services.StartupService`:
  - `bool IsEnabled()` — reads `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`,
    value name `Snap`, and returns true only if the stored path matches the
    current executable's path (`Environment.ProcessPath`).
  - `void SetEnabled(bool enabled)` — on true, writes the current executable
    path to that value; on false, deletes the value if present (no error if
    already absent).
- `SettingsWindow` gains a `CheckBox` "Launch Snap at startup" below the
  existing "Copy file path to clipboard after saving" checkbox.
  - Initialized from `StartupService.IsEnabled()` when the window opens.
  - Applied via `StartupService.SetEnabled(...)` in `SaveButton_Click`,
    alongside the existing settings save.
- The registry is the sole source of truth for this setting — it is **not**
  stored in `AppSettings`/`settings.json`. This avoids the json file and the
  actual Windows startup state ever disagreeing (e.g. if the user moves the
  exe or edits the registry directly).

## 2. Blur toggle (consecutive blurring)

Current behavior (bug relative to the original design intent): clicking
**Blur** arms `_blurModeArmed`, but `OverlayWindow.CommitBlurBox` sets it back
to `false` immediately after the first box is committed, forcing the user to
click **Blur** again for every box.

New behavior:

- Clicking **Blur** toggles `_blurModeArmed` (on → off, off → on) instead of
  only ever arming it. `CommitBlurBox` no longer disarms it as a side effect.
- While armed, the **Blur** button shows a visually distinct pressed/active
  state (background/border change) so the user can see the mode is live.
- Clicking **Copy**, **Save**, or **Cancel** disarms blur mode as a side
  effect (prevents a stray drag from starting a blur box after the user has
  moved on to exporting).
- Net effect: the user can drag any number of blur boxes back-to-back after a
  single click of **Blur**.

## 3. Undo / redo for blur boxes

New standalone class, `Snap.Models.BlurHistory` (no WPF/Canvas dependency —
depends only on a `List<BlurBox>`, so it is independently unit-testable):

```csharp
public enum BlurActionType { Add, Remove }

public class BlurHistory
{
    public BlurHistory(List<BlurBox> boxes);

    // Applies the change to the list, records it, and clears the redo stack.
    public void RecordAdd(BlurBox box);
    public void RecordRemove(BlurBox box);

    // Applies the inverse of the last action to the list and moves it to the
    // other stack. Returns null if there is nothing to undo/redo.
    // IsPresentAfter tells the caller whether to render or remove the box's
    // on-canvas visual.
    public BlurHistoryChange? Undo();
    public BlurHistoryChange? Redo();
}

public record BlurHistoryChange(BlurBox Box, bool IsPresentAfter);
```

- `OverlayWindow` calls `RecordAdd` from `CommitBlurBox` (instead of calling
  `BlurBoxes.Add` directly) and `RecordRemove` from the existing right-click
  removal handler (instead of calling `BlurBoxes.Remove` directly).
- Scroll-wheel radius changes are **not** recorded in history — only add and
  remove are undoable, matching the current interaction model and keeping
  `Ctrl+Z` from requiring many presses to undo one box's radius tweaks.
- `OverlayWindow` tracks each box's current on-canvas preview in a
  `Dictionary<BlurBox, Image>` so that after an `Undo()`/`Redo()` call it can
  look up and remove, or re-render, the correct visual using
  `BlurHistoryChange.Box` and `.IsPresentAfter` — without re-deriving which
  visual belongs to which box.
- `Ctrl+Z` / `Ctrl+Y` are wired into `OverlayWindow`'s existing `KeyDown`
  handler (alongside the existing `Esc` handling). Both are no-ops when their
  respective stack is empty.
- History is scoped to a single capture session — a fresh `BlurHistory` is
  created per `OverlayWindow` instance and discarded when it closes.

## Testing

- `BlurHistory`: unit tests covering add/remove/undo/redo, redo-stack
  clearing on a new action after an undo, and undo/redo on empty stacks being
  safe no-ops.
- `StartupService`: unit test only the pure path-resolution logic; actual
  registry read/write is verified manually (tests should not mutate the real
  `Run` key).
- Blur toggle visual state, keyboard shortcuts (`Ctrl+Z`/`Ctrl+Y`), and
  registry startup behavior end-to-end: manual verification in a live run,
  since they need a real window/display/registry.

## Out of scope

- Undo/redo for blur box move/resize (not yet implemented in the app at all —
  only add/remove/radius exist today).
- Making radius changes undoable.
- Any UI indication of undo/redo availability (e.g. greyed-out state) beyond
  the toggle button's own pressed/unpressed visual.
