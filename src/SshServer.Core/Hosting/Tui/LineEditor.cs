using System.Text;

namespace AlfredBr.SshServer.Core.Tui;

/// <summary>
/// Result of processing input bytes.
/// </summary>
public enum LineEditorResult
{
    /// <summary>Input processed, continue reading.</summary>
    Continue,

    /// <summary>A complete line was submitted.</summary>
    LineSubmitted,

    /// <summary>User requested disconnect (Ctrl-C or Ctrl-D on empty line).</summary>
    Disconnect
}

/// <summary>
/// Emacs-style line editor with history, kill ring, and tab completion.
/// </summary>
public class LineEditor
{
    private readonly Action<byte[]> _sendData;
    private readonly StringBuilder _buffer = new();
    private int _cursorPos;

    // Command history
    private readonly List<string> _history = [];
    private int _historyIndex;
    private string _savedLine = "";

    // Kill ring
    private string _killRing = "";

    // Escape sequence state
    // 0=normal, 1=got ESC, 2=got ESC[, 101-104=got ESC[n waiting for ~
    private int _escapeState;

    // Tab completion
    private string[] _completions = [];

    // The last submitted line
    private string _submittedLine = "";

    /// <summary>
    /// The prompt string displayed before input.
    /// </summary>
    public string Prompt { get; set; } = "> ";

    /// <summary>
    /// The visible width of the prompt (for cursor positioning when prompt is rendered externally).
    /// If not set, defaults to Prompt.Length.
    /// </summary>
    public int PromptWidth { get; set; } = -1;

    /// <summary>
    /// Optional callback to render the prompt externally (for Spectre markup support).
    /// Called on clear screen (Ctrl-L) to re-render the prompt.
    /// </summary>
    public Action? RenderPrompt { get; set; }

    private int EffectivePromptWidth => PromptWidth >= 0 ? PromptWidth : Prompt.Length;

    /// <summary>
    /// Gets the last submitted line after LineSubmitted result.
    /// </summary>
    public string SubmittedLine => _submittedLine;

    /// <summary>
    /// Sets the available commands for tab completion.
    /// </summary>
    public string[] Completions
    {
        get => _completions;
        set => _completions = value ?? [];
    }

    /// <summary>
    /// Creates a new line editor.
    /// </summary>
    /// <param name="sendData">Action to send data to the terminal.</param>
    public LineEditor(Action<byte[]> sendData)
    {
        _sendData = sendData ?? throw new ArgumentNullException(nameof(sendData));
    }

    /// <summary>
    /// Display the prompt.
    /// </summary>
    public void ShowPrompt()
    {
        _sendData(Encoding.UTF8.GetBytes(Prompt));
    }

    /// <summary>
    /// Process a single byte of input.
    /// </summary>
    /// <returns>Result indicating whether to continue, line was submitted, or disconnect.</returns>
    public LineEditorResult ProcessByte(byte b)
    {
        // Handle escape sequences
        if (_escapeState == 1)
        {
            if (b == '[')
            {
                _escapeState = 2;
                return LineEditorResult.Continue;
            }
            // Alt+key sequences (ESC followed by letter)
            _escapeState = 0;
            switch (b)
            {
                case (byte)'b':
                case (byte)'B':
                    MoveBackWord();
                    return LineEditorResult.Continue;
                case (byte)'f':
                case (byte)'F':
                    MoveForwardWord();
                    return LineEditorResult.Continue;
                case (byte)'d':
                case (byte)'D':
                    DeleteWordForward();
                    return LineEditorResult.Continue;
            }
            // Not a recognized Alt sequence, fall through to process byte normally
        }
        else if (_escapeState == 2)
        {
            switch (b)
            {
                case (byte)'A': // Up arrow
                    HistoryPrevious();
                    _escapeState = 0;
                    return LineEditorResult.Continue;
                case (byte)'B': // Down arrow
                    HistoryNext();
                    _escapeState = 0;
                    return LineEditorResult.Continue;
                case (byte)'C': // Right arrow
                    MoveForward();
                    _escapeState = 0;
                    return LineEditorResult.Continue;
                case (byte)'D': // Left arrow
                    MoveBack();
                    _escapeState = 0;
                    return LineEditorResult.Continue;
                case (byte)'H': // Home key
                    MoveToBeginning();
                    _escapeState = 0;
                    return LineEditorResult.Continue;
                case (byte)'F': // End key
                    MoveToEnd();
                    _escapeState = 0;
                    return LineEditorResult.Continue;
                case (byte)'1': // Could be Home (\x1b[1~)
                case (byte)'3': // Delete key (\x1b[3~)
                case (byte)'4': // Could be End (\x1b[4~)
                    _escapeState = 100 + (b - '0');
                    return LineEditorResult.Continue;
                default:
                    _escapeState = 0;
                    return LineEditorResult.Continue;
            }
        }
        else if (_escapeState >= 101 && _escapeState <= 104)
        {
            if (b == '~')
            {
                switch (_escapeState)
                {
                    case 101: // \x1b[1~ - Home
                        MoveToBeginning();
                        break;
                    case 103: // \x1b[3~ - Delete
                        DeleteCharUnderCursor();
                        break;
                    case 104: // \x1b[4~ - End
                        MoveToEnd();
                        break;
                }
            }
            _escapeState = 0;
            return LineEditorResult.Continue;
        }

        // Regular key handling
        switch (b)
        {
            case 0x1B: // Escape
                _escapeState = 1;
                return LineEditorResult.Continue;

            case 0x03: // Ctrl-C
                return LineEditorResult.Disconnect;

            case 0x04: // Ctrl-D
                if (_buffer.Length == 0)
                    return LineEditorResult.Disconnect;
                DeleteCharUnderCursor();
                return LineEditorResult.Continue;

            case 0x01: // Ctrl-A
                MoveToBeginning();
                return LineEditorResult.Continue;

            case 0x05: // Ctrl-E
                MoveToEnd();
                return LineEditorResult.Continue;

            case 0x02: // Ctrl-B
                MoveBack();
                return LineEditorResult.Continue;

            case 0x06: // Ctrl-F
                MoveForward();
                return LineEditorResult.Continue;

            case 0x10: // Ctrl-P
                HistoryPrevious();
                return LineEditorResult.Continue;

            case 0x0E: // Ctrl-N
                HistoryNext();
                return LineEditorResult.Continue;

            case 0x0B: // Ctrl-K
                KillToEnd();
                return LineEditorResult.Continue;

            case 0x15: // Ctrl-U
                KillToBeginning();
                return LineEditorResult.Continue;

            case 0x19: // Ctrl-Y
                Yank();
                return LineEditorResult.Continue;

            case 0x0C: // Ctrl-L
                ClearScreen();
                return LineEditorResult.Continue;

            case 0x09: // Tab
                HandleTabCompletion();
                return LineEditorResult.Continue;

            case 0x08: // Ctrl-H (Backspace)
            case 0x7F: // DEL
                Backspace();
                return LineEditorResult.Continue;

            case (byte)'\r':
            case (byte)'\n':
                return HandleEnter();

            default:
                if (b >= 0x20 && b < 0x7F)
                    InsertChar((char)b);
                return LineEditorResult.Continue;
        }
    }

    #region Cursor Movement

    private void CursorLeft(int n)
    {
        if (n > 0)
            _sendData(Encoding.ASCII.GetBytes($"\x1b[{n}D"));
    }

    private void CursorRight(int n)
    {
        if (n > 0)
            _sendData(Encoding.ASCII.GetBytes($"\x1b[{n}C"));
    }

    private void MoveBack()
    {
        if (_cursorPos > 0)
        {
            _cursorPos--;
            CursorLeft(1);
        }
    }

    private void MoveForward()
    {
        if (_cursorPos < _buffer.Length)
        {
            _cursorPos++;
            CursorRight(1);
        }
    }

    private void MoveToBeginning()
    {
        CursorLeft(_cursorPos);
        _cursorPos = 0;
    }

    private void MoveToEnd()
    {
        CursorRight(_buffer.Length - _cursorPos);
        _cursorPos = _buffer.Length;
    }

    private void MoveBackWord()
    {
        if (_cursorPos == 0) return;

        // Skip spaces before the word
        while (_cursorPos > 0 && char.IsWhiteSpace(_buffer[_cursorPos - 1]))
            _cursorPos--;

        // Move to start of word
        while (_cursorPos > 0 && !char.IsWhiteSpace(_buffer[_cursorPos - 1]))
            _cursorPos--;

        RedrawLine();
    }

    private void MoveForwardWord()
    {
        if (_cursorPos >= _buffer.Length) return;

        // Skip current word
        while (_cursorPos < _buffer.Length && !char.IsWhiteSpace(_buffer[_cursorPos]))
            _cursorPos++;

        // Skip spaces after word
        while (_cursorPos < _buffer.Length && char.IsWhiteSpace(_buffer[_cursorPos]))
            _cursorPos++;

        RedrawLine();
    }

    #endregion

    #region Editing

    private void InsertChar(char c)
    {
        if (_cursorPos == _buffer.Length)
        {
            // Append at end
            _buffer.Append(c);
            _cursorPos++;
            _sendData([(byte)c]);
        }
        else
        {
            // Insert in middle
            _buffer.Insert(_cursorPos, c);
            RedrawFromCursor();
            _cursorPos++;
            CursorRight(1);
        }
    }

    private void Backspace()
    {
        if (_cursorPos > 0)
        {
            _cursorPos--;
            _buffer.Remove(_cursorPos, 1);
            _sendData("\b"u8.ToArray());
            RedrawFromCursor();
        }
    }

    private void DeleteCharUnderCursor()
    {
        if (_cursorPos < _buffer.Length)
        {
            _buffer.Remove(_cursorPos, 1);
            RedrawFromCursor();
        }
    }

    private void DeleteWordForward()
    {
        if (_cursorPos >= _buffer.Length) return;

        var startPos = _cursorPos;

        // Skip current word
        while (_cursorPos < _buffer.Length && !char.IsWhiteSpace(_buffer[_cursorPos]))
            _cursorPos++;

        // Skip spaces after word
        while (_cursorPos < _buffer.Length && char.IsWhiteSpace(_buffer[_cursorPos]))
            _cursorPos++;

        // Save to kill ring and delete
        _killRing = _buffer.ToString(startPos, _cursorPos - startPos);
        _buffer.Remove(startPos, _cursorPos - startPos);
        _cursorPos = startPos;
        RedrawFromCursor();
    }

    private void KillToEnd()
    {
        if (_cursorPos < _buffer.Length)
        {
            _killRing = _buffer.ToString(_cursorPos, _buffer.Length - _cursorPos);
            _buffer.Length = _cursorPos;
            _sendData("\x1b[K"u8.ToArray());
        }
    }

    private void KillToBeginning()
    {
        if (_cursorPos > 0)
        {
            _killRing = _buffer.ToString(0, _cursorPos);
            _buffer.Remove(0, _cursorPos);
            _cursorPos = 0;
            RedrawLine();
        }
    }

    private void Yank()
    {
        if (string.IsNullOrEmpty(_killRing)) return;

        _buffer.Insert(_cursorPos, _killRing);
        _cursorPos += _killRing.Length;
        RedrawLine();
    }

    #endregion

    #region Display

    private void RedrawFromCursor()
    {
        var tail = _buffer.ToString(_cursorPos, _buffer.Length - _cursorPos);
        _sendData(Encoding.UTF8.GetBytes(tail));
        _sendData("\x1b[K"u8.ToArray());
        CursorLeft(_buffer.Length - _cursorPos);
    }

    private void RedrawLine()
    {
        // Move to start, skip past prompt, write buffer, clear to end
        _sendData(Encoding.ASCII.GetBytes($"\r\x1b[{EffectivePromptWidth}C{_buffer}"));
        _sendData("\x1b[K"u8.ToArray());
        CursorLeft(_buffer.Length - _cursorPos);
    }

    private void ReplaceLine(string text)
    {
        // Move to start, skip past prompt, clear to end
        _sendData(Encoding.ASCII.GetBytes($"\r\x1b[{EffectivePromptWidth}C"));
        _sendData("\x1b[K"u8.ToArray());

        _buffer.Clear();
        _buffer.Append(text);
        _cursorPos = _buffer.Length;

        if (text.Length > 0)
            _sendData(Encoding.UTF8.GetBytes(text));
    }

    private void ClearScreen()
    {
        _sendData("\x1b[2J\x1b[H"u8.ToArray());
        // Re-render prompt
        if (RenderPrompt != null)
            RenderPrompt();
        else if (Prompt.Length > 0)
            _sendData(Encoding.UTF8.GetBytes(Prompt));
        else if (EffectivePromptWidth > 0)
            _sendData(Encoding.ASCII.GetBytes($"\x1b[{EffectivePromptWidth}C"));
        if (_buffer.Length > 0)
        {
            _sendData(Encoding.UTF8.GetBytes(_buffer.ToString()));
            CursorLeft(_buffer.Length - _cursorPos);
        }
    }

    #endregion

    #region History

    private void HistoryPrevious()
    {
        if (_history.Count == 0) return;

        if (_historyIndex == _history.Count)
            _savedLine = _buffer.ToString();

        if (_historyIndex > 0)
        {
            _historyIndex--;
            ReplaceLine(_history[_historyIndex]);
        }
    }

    private void HistoryNext()
    {
        if (_historyIndex >= _history.Count) return;

        _historyIndex++;
        if (_historyIndex == _history.Count)
            ReplaceLine(_savedLine);
        else
            ReplaceLine(_history[_historyIndex]);
    }

    private void AddToHistory(string line)
    {
        if (_history.Count == 0 || _history[^1] != line)
            _history.Add(line);

        _historyIndex = _history.Count;
        _savedLine = "";
    }

    #endregion

    #region Tab Completion

    private void HandleTabCompletion()
    {
        var input = _buffer.ToString().TrimStart();

        // Only complete first word
        if (input.Contains(' '))
            return;

        var matches = _completions
            .Where(c => c.StartsWith(input, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (matches.Length == 0)
            return;

        if (matches.Length == 1)
        {
            ReplaceLine(matches[0]);
        }
        else
        {
            _sendData("\r\n"u8.ToArray());
            var matchList = string.Join("  ", matches);
            _sendData(Encoding.UTF8.GetBytes(matchList));
            _sendData(Encoding.UTF8.GetBytes($"\r\n{Prompt}{_buffer}"));
        }
    }

    #endregion

    #region Line Submission

    private LineEditorResult HandleEnter()
    {
        _sendData("\r\n"u8.ToArray());

        if (_buffer.Length > 0)
        {
            _submittedLine = _buffer.ToString();
            AddToHistory(_submittedLine);
            _buffer.Clear();
            _cursorPos = 0;
            return LineEditorResult.LineSubmitted;
        }

        ShowPrompt();
        return LineEditorResult.Continue;
    }

    #endregion
}
