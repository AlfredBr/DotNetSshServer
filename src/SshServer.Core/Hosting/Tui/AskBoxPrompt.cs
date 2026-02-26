using System.ComponentModel;
using System.Text;

using Spectre.Console;

namespace AlfredBr.SshServer.Core.Tui;

/// <summary>
/// A bordered, side-by-side input prompt. Displays a labeled left panel and
/// an interactive input field in the right panel with the cursor positioned
/// inside the box.
/// </summary>
/// <typeparam name="T">The type of value to collect from the user.</typeparam>
public class AskBoxPrompt<T> : IPrompt<T>
{
    private readonly string _prompt;
    private readonly int _width;

    /// <summary>
    /// Creates a new AskBoxPrompt.
    /// </summary>
    /// <param name="prompt">The label displayed in the left panel. Supports Spectre markup.</param>
    /// <param name="width">Total box width in columns. 0 (default) uses the console width.</param>
    public AskBoxPrompt(string prompt, int width = 0)
    {
        _prompt = prompt;
        _width = width;
    }

    /// <inheritdoc />
    public T Show(IAnsiConsole console)
        => ShowAsync(console, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task<T> ShowAsync(IAnsiConsole console, CancellationToken cancellationToken)
    {
        while (true)
        {
            var rawInput = await ReadInputAsync(console, cancellationToken);

            if (typeof(T) == typeof(string))
                return (T)(object)rawInput;

            try
            {
                var converter = TypeDescriptor.GetConverter(typeof(T));
                var converted = converter.ConvertFromString(rawInput);
                if (converted is T typed)
                    return typed;
            }
            catch (Exception) { }

            console.MarkupLine("[red]Invalid input — please try again.[/]");
        }
    }

    private async Task<string> ReadInputAsync(IAnsiConsole console, CancellationToken cancellationToken)
    {
        var totalWidth = _width == 0 ? console.Profile.Width : _width;
        var plainPrompt = Markup.Remove(_prompt);
        var promptCol = plainPrompt.Length + 2;           // space + text + space
        var inputCol = totalWidth - promptCol - 3;        // 3 = │ + │ + │
        if (inputCol < 4) inputCol = 4;

        // Box-drawing rows
        var top = $"┌{new string('─', promptCol)}┬{new string('─', inputCol)}┐";
        var bot = $"└{new string('─', promptCol)}┴{new string('─', inputCol)}┘";

        // Raw TextWriter for escape sequences (bypasses Spectre markup pipeline)
        var raw = console.Profile.Out.Writer;

        // ── Draw box ─────────────────────────────────────────────────────────
        raw.WriteLine(top);

        raw.Write("│ ");
        console.Markup(_prompt);                         // render markup in left panel
        raw.Write($" │{new string(' ', inputCol)}│");
        raw.WriteLine();

        raw.WriteLine(bot);
        raw.Flush();

        // ── Position cursor inside input area ────────────────────────────────
        // After WriteLine(bot): cursor is on the line below the box.
        // \x1b[2A  → up 2 rows to the mid row
        // \x1b[NG  → column N (1-indexed): promptCol+4 is first input char
        var inputStartCol = promptCol + 4;
        raw.Write($"\x1b[2A\x1b[{inputStartCol}G");
        raw.Flush();

        // ── Input loop ───────────────────────────────────────────────────────
        var buffer = new StringBuilder();
        var maxInput = inputCol - 1;   // leave 1 column gap before right border

        while (true)
        {
            var key = await console.Input.ReadKeyAsync(true, cancellationToken);
            if (key is null) throw new OperationCanceledException();

            var k = key.Value;

            if (k.Key == ConsoleKey.Enter)
                break;

            if (k.Key == ConsoleKey.C && k.Modifiers.HasFlag(ConsoleModifiers.Control))
                throw new OperationCanceledException();

            if (k.Key == ConsoleKey.D && k.Modifiers.HasFlag(ConsoleModifiers.Control)
                && buffer.Length == 0)
                throw new OperationCanceledException();

            if (k.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0)
                {
                    buffer.Length--;
                    raw.Write("\x1b[D \x1b[D");   // move left, erase, move left
                    raw.Flush();
                }
                continue;
            }

            // Printable ASCII only
            if (k.KeyChar >= ' ' && k.KeyChar <= '~' && buffer.Length < maxInput)
            {
                buffer.Append(k.KeyChar);
                raw.Write(k.KeyChar);
                raw.Flush();
            }
        }

        // ── Restore cursor below box ─────────────────────────────────────────
        // \x1b[2B → down 2 rows (back past the bot border to the line below)
        // \r      → carriage return to column 1
        raw.Write("\x1b[2B\r");
        raw.Flush();
        console.WriteLine();

        return buffer.ToString();
    }
}
