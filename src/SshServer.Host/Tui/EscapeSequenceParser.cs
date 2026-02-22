namespace SshServer.Host.Tui;

/// <summary>
/// Parses ANSI escape sequences from SSH input into ConsoleKeyInfo.
/// Handles arrow keys, Home, End, Delete, and other special keys.
/// </summary>
public class EscapeSequenceParser
{
    private readonly List<byte> _buffer = [];
    private ParserState _state = ParserState.Normal;

    private enum ParserState
    {
        Normal,
        Escape,      // Received 0x1B
        Bracket,     // Received 0x1B [
        BracketNum,  // Received 0x1B [ followed by digits
    }

    /// <summary>
    /// Feed a byte to the parser and get back any completed key info.
    /// </summary>
    /// <returns>A ConsoleKeyInfo if a complete key was parsed, null if more bytes needed.</returns>
    public ConsoleKeyInfo? Feed(byte b)
    {
        switch (_state)
        {
            case ParserState.Normal:
                if (b == 0x1B) // Escape
                {
                    _state = ParserState.Escape;
                    _buffer.Clear();
                    return null;
                }
                return ByteToKeyInfo(b);

            case ParserState.Escape:
                if (b == '[')
                {
                    _state = ParserState.Bracket;
                    return null;
                }
                // Alt+key comes as ESC followed by the key
                _state = ParserState.Normal;
                return ByteToKeyInfo(b, alt: true);

            case ParserState.Bracket:
                if (char.IsDigit((char)b))
                {
                    _buffer.Add(b);
                    _state = ParserState.BracketNum;
                    return null;
                }
                // CSI followed by letter: arrow keys, etc.
                _state = ParserState.Normal;
                return CsiLetterToKeyInfo((char)b);

            case ParserState.BracketNum:
                if (char.IsDigit((char)b))
                {
                    _buffer.Add(b);
                    return null;
                }
                if (b == '~')
                {
                    // Numeric code like ESC[3~ (Delete)
                    _state = ParserState.Normal;
                    var code = ParseNumber();
                    return NumericCodeToKeyInfo(code);
                }
                if (b == ';')
                {
                    // Modifier sequence like ESC[1;5C (Ctrl+Right)
                    // For now, skip modifier and wait for final letter
                    _buffer.Clear();
                    return null;
                }
                // Letter terminates the sequence
                _state = ParserState.Normal;
                return CsiLetterToKeyInfo((char)b);

            default:
                _state = ParserState.Normal;
                return ByteToKeyInfo(b);
        }
    }

    /// <summary>
    /// Reset parser state (e.g., on timeout).
    /// Returns Escape key if we were in the middle of an escape sequence.
    /// </summary>
    public ConsoleKeyInfo? Reset()
    {
        var wasInEscape = _state != ParserState.Normal;
        _state = ParserState.Normal;
        _buffer.Clear();

        if (wasInEscape)
            return new ConsoleKeyInfo('\x1b', ConsoleKey.Escape, false, false, false);

        return null;
    }

    private int ParseNumber()
    {
        var s = new string(_buffer.Select(b => (char)b).ToArray());
        return int.TryParse(s, out var n) ? n : 0;
    }

    private static ConsoleKeyInfo CsiLetterToKeyInfo(char c) => c switch
    {
        'A' => new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, false, false, false),
        'B' => new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false),
        'C' => new ConsoleKeyInfo('\0', ConsoleKey.RightArrow, false, false, false),
        'D' => new ConsoleKeyInfo('\0', ConsoleKey.LeftArrow, false, false, false),
        'H' => new ConsoleKeyInfo('\0', ConsoleKey.Home, false, false, false),
        'F' => new ConsoleKeyInfo('\0', ConsoleKey.End, false, false, false),
        'Z' => new ConsoleKeyInfo('\t', ConsoleKey.Tab, true, false, false), // Shift+Tab
        _ => new ConsoleKeyInfo(c, ConsoleKey.None, false, false, false),
    };

    private static ConsoleKeyInfo NumericCodeToKeyInfo(int code) => code switch
    {
        1 => new ConsoleKeyInfo('\0', ConsoleKey.Home, false, false, false),
        2 => new ConsoleKeyInfo('\0', ConsoleKey.Insert, false, false, false),
        3 => new ConsoleKeyInfo('\0', ConsoleKey.Delete, false, false, false),
        4 => new ConsoleKeyInfo('\0', ConsoleKey.End, false, false, false),
        5 => new ConsoleKeyInfo('\0', ConsoleKey.PageUp, false, false, false),
        6 => new ConsoleKeyInfo('\0', ConsoleKey.PageDown, false, false, false),
        11 => new ConsoleKeyInfo('\0', ConsoleKey.F1, false, false, false),
        12 => new ConsoleKeyInfo('\0', ConsoleKey.F2, false, false, false),
        13 => new ConsoleKeyInfo('\0', ConsoleKey.F3, false, false, false),
        14 => new ConsoleKeyInfo('\0', ConsoleKey.F4, false, false, false),
        15 => new ConsoleKeyInfo('\0', ConsoleKey.F5, false, false, false),
        17 => new ConsoleKeyInfo('\0', ConsoleKey.F6, false, false, false),
        18 => new ConsoleKeyInfo('\0', ConsoleKey.F7, false, false, false),
        19 => new ConsoleKeyInfo('\0', ConsoleKey.F8, false, false, false),
        20 => new ConsoleKeyInfo('\0', ConsoleKey.F9, false, false, false),
        21 => new ConsoleKeyInfo('\0', ConsoleKey.F10, false, false, false),
        23 => new ConsoleKeyInfo('\0', ConsoleKey.F11, false, false, false),
        24 => new ConsoleKeyInfo('\0', ConsoleKey.F12, false, false, false),
        _ => new ConsoleKeyInfo('\0', ConsoleKey.None, false, false, false),
    };

    private static ConsoleKeyInfo ByteToKeyInfo(byte b, bool alt = false)
    {
        var control = b < 0x20 && b != 0x1B && b != 0x0D && b != 0x0A && b != 0x09;

        var (c, key) = b switch
        {
            0x00 => ('\0', ConsoleKey.None),
            0x01 => ('a', ConsoleKey.A), // Ctrl+A
            0x02 => ('b', ConsoleKey.B), // Ctrl+B
            0x03 => ('c', ConsoleKey.C), // Ctrl+C
            0x04 => ('d', ConsoleKey.D), // Ctrl+D
            0x05 => ('e', ConsoleKey.E), // Ctrl+E
            0x06 => ('f', ConsoleKey.F), // Ctrl+F
            0x07 => ('\a', ConsoleKey.None), // Bell
            0x08 => ('\b', ConsoleKey.Backspace),
            0x09 => ('\t', ConsoleKey.Tab),
            0x0A => ('\n', ConsoleKey.Enter),
            0x0B => ('k', ConsoleKey.K), // Ctrl+K
            0x0C => ('l', ConsoleKey.L), // Ctrl+L
            0x0D => ('\r', ConsoleKey.Enter),
            0x15 => ('u', ConsoleKey.U), // Ctrl+U
            0x1B => ('\x1b', ConsoleKey.Escape),
            0x7F => ('\b', ConsoleKey.Backspace), // DEL
            _ when b >= 0x20 && b < 0x7F => ((char)b, CharToConsoleKey((char)b)),
            _ => ('\0', ConsoleKey.None),
        };

        return new ConsoleKeyInfo(c, key, shift: false, alt: alt, control: control);
    }

    private static ConsoleKey CharToConsoleKey(char c) => char.ToUpper(c) switch
    {
        >= 'A' and <= 'Z' => ConsoleKey.A + (char.ToUpper(c) - 'A'),
        >= '0' and <= '9' => ConsoleKey.D0 + (c - '0'),
        ' ' => ConsoleKey.Spacebar,
        '\r' or '\n' => ConsoleKey.Enter,
        '\t' => ConsoleKey.Tab,
        _ => ConsoleKey.None,
    };
}
