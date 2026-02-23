# Plan: Line Wrapping Support

## The Challenge

When input exceeds terminal width, cursor math becomes complex because the terminal wraps text to multiple rows.

## Current Assumption

```
Terminal width: 80 columns
Prompt: "> " (2 chars)
Available: 78 chars for input

> hello world_                 <- cursor at column 14, all on row 1
```

## The Problem

```
> this is a very long line that exceeds the terminal width and wraps to the next
 line_
```

Cursor movements now need to:
1. Track which row the cursor is on
2. Wrap to next line when moving right past column 80
3. Wrap to previous line when moving left past column 1
4. Redraw multiple rows when inserting/deleting in the middle

## Key Complications

| Operation | Single Line | With Wrapping |
|-----------|-------------|---------------|
| Cursor left | `\x1b[D` | May need `\x1b[A` (up) + move to end of previous row |
| Cursor right | `\x1b[C` | May need `\x1b[B` (down) + move to start of next row |
| Insert char | Redraw to EOL | Redraw to EOL, possibly spanning multiple rows |
| Delete char | Redraw to EOL | Recalculate all wrapped rows |
| Ctrl-A | Move left N | Move up M rows, then to prompt position |

## Data Needed

```csharp
public class LineEditor
{
    private int _terminalWidth = 80;  // From PTY request
    private int _promptLength = 2;    // "> " = 2 chars

    // Calculate cursor row/column from buffer position
    private (int row, int col) GetCursorPosition(int bufferPos)
    {
        var totalPos = _promptLength + bufferPos;
        var row = totalPos / _terminalWidth;
        var col = totalPos % _terminalWidth;
        return (row, col);
    }
}
```

## Approach Options

### Option A: Simple (current)
Ignore wrapping, let terminal handle it.
- Works for display, but cursor movement breaks when wrapped
- Current implementation

### Option B: Track and compensate (recommended)
Calculate row changes for all cursor movements.
- Add terminal width to LineEditor
- Compute row/col for cursor movements
- Use `\x1b[A` (up) and `\x1b[B` (down) when crossing row boundaries

### Option C: Redraw entire input
Simpler logic, more output.
- On any edit, clear all input rows and redraw
- Less efficient but easier to implement correctly

## Implementation Steps (Option B)

1. Add `TerminalWidth` property to LineEditor
2. Update `MoveBack()` / `MoveForward()` to handle row wrapping
3. Update `MoveToBeginning()` / `MoveToEnd()` to handle multiple rows
4. Update `RedrawFromCursor()` to clear/redraw across row boundaries
5. Wire up window resize events to update width

## Terminal Escape Sequences for Multi-Row

| Action | Sequence |
|--------|----------|
| Move cursor up | `\x1b[A` or `\x1b[{N}A` |
| Move cursor down | `\x1b[B` or `\x1b[{N}B` |
| Move to column N | `\x1b[{N}G` |
| Save cursor position | `\x1b[s` |
| Restore cursor position | `\x1b[u` |

## Status

Deferred - saved for future implementation.
