# Plan: Emacs-style Line Editing

## Overview

Implement readline-like editing for the SSH shell, supporting common Emacs control characters for cursor navigation and line manipulation.

## Control Characters to Implement

| Key | Code | Action | Status |
|-----|------|--------|--------|
| Ctrl-A | 0x01 | Move cursor to beginning of line | DONE |
| Ctrl-E | 0x05 | Move cursor to end of line | DONE |
| Ctrl-B | 0x02 | Move cursor back (left) one char | DONE |
| Ctrl-F | 0x06 | Move cursor forward (right) one char | DONE |
| Ctrl-D | 0x04 | Delete char under cursor (EOF if empty) | DONE |
| Ctrl-H | 0x08 | Backspace (delete char before cursor) | DONE |
| Ctrl-K | 0x0B | Kill from cursor to end of line | DONE |
| Ctrl-U | 0x15 | Kill from beginning to cursor | DONE |
| Ctrl-C | 0x03 | Disconnect session | DONE |
| Ctrl-L | 0x0C | Clear screen, redraw prompt and line | DONE |
| DEL | 0x7F | Backspace (same as Ctrl-H) | DONE |

## Architecture Change

### Current (simple, cursor always at end)
```csharp
var lineBuffer = new StringBuilder();
```

### Required (cursor can be anywhere)
```csharp
var lineBuffer = new StringBuilder();
var cursorPos = 0;  // position within lineBuffer (0 to lineBuffer.Length)
```

## Terminal Escape Sequences

| Action | Sequence | Notes |
|--------|----------|-------|
| Move cursor left | `\x1b[D` | Or `\x1b[{N}D` for N chars |
| Move cursor right | `\x1b[C` | Or `\x1b[{N}C` for N chars |
| Clear from cursor to EOL | `\x1b[K` | Used after deletions |
| Clear entire screen | `\x1b[2J` | For Ctrl-L |
| Move cursor to top-left | `\x1b[H` | For Ctrl-L |

## Implementation Notes

### Mid-line Editing

When inserting or deleting in the middle of a line:
1. Write all characters from cursor position to end of buffer
2. Send `\x1b[K` to clear any trailing garbage
3. Move cursor back to correct position with `\x1b[{N}D`

### Ctrl-D Dual Behavior

- If `lineBuffer.Length == 0`: treat as EOF, disconnect
- If `lineBuffer.Length > 0` and `cursorPos < lineBuffer.Length`: delete char at cursorPos

### Redraw Helper

Create a helper function to redraw from cursor to end:
```csharp
void RedrawFromCursor()
{
    // Send chars from cursorPos to end
    // Send \x1b[K to clear
    // Move cursor back (lineBuffer.Length - cursorPos) positions
}
```

## Implemented Features (Deferred → Done)

### Command History ✓
- Ctrl-P: previous command
- Ctrl-N: next command
- History stored per-connection
- Duplicate consecutive commands not added
- Current line saved when navigating, restored with Ctrl-N

## Deferred Features

These are out of scope for the initial implementation:

### Kill Ring
- Ctrl-K and Ctrl-U would save deleted text
- Ctrl-Y would yank (paste) the last killed text
- Could use a simple `string? killRing` variable

### Command History
- Ctrl-P: previous command
- Ctrl-N: next command
- Requires `List<string> history` and `int historyIndex`

### Word Movements
- Alt-B (back word): arrives as `\x1b b` (escape then 'b')
- Alt-F (forward word): arrives as `\x1b f`
- Alt-D (delete word forward): arrives as `\x1b d`
- Requires escape sequence parsing with timeout or state machine

### UTF-8 Support
- Cursor position != byte position for multi-byte chars
- Would need to track "display width" vs "byte length"
- Consider using `System.Globalization.StringInfo` for grapheme clusters

### Line Wrapping
- When line exceeds terminal width, cursor math becomes complex
- Would need to track terminal width from PTY request
- Cursor movement wraps to next/previous line

## Testing Checklist

- [ ] Type "hello", press Ctrl-A, verify cursor at start
- [ ] Press Ctrl-E, verify cursor at end
- [ ] Type "hello", Ctrl-A, Ctrl-F twice, verify cursor after "he"
- [ ] Type "hello", Ctrl-B twice, verify cursor before "lo"
- [ ] Type "hello", Ctrl-A, Ctrl-D, verify "ello" remains
- [ ] Type "hello", Ctrl-A, Ctrl-K, verify line is empty
- [ ] Type "hello", Ctrl-E, Ctrl-U, verify line is empty
- [ ] Empty line, Ctrl-D, verify disconnect
- [ ] Ctrl-C at any point, verify disconnect
- [ ] Ctrl-L, verify screen clears and prompt redraws
