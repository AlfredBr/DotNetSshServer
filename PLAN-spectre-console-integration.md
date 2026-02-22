# Plan: Spectre.Console Integration (Hybrid Approach)

## Goal

Enhance the SSH shell with Spectre.Console for rich output while keeping our custom line editor with Emacs keybindings. Spectre handles rendering (tables, panels, colors); our code handles the command prompt and basic input.

## Hybrid Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     SSH Connection                          │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│   ┌─────────────────┐         ┌─────────────────────────┐   │
│   │  Line Editor    │         │   Spectre.Console       │   │
│   │  (our code)     │         │   (rich output)         │   │
│   │                 │         │                         │   │
│   │  - Prompt "> "  │ ──────► │  - Tables               │   │
│   │  - Emacs keys   │ command │  - Panels               │   │
│   │  - Input buffer │         │  - Markup colors        │   │
│   │                 │ ◄────── │  - Selection prompts    │   │
│   └─────────────────┘ return  └─────────────────────────┘   │
│           │                             │                   │
│           └──────────┬──────────────────┘                   │
│                      ▼                                      │
│              SSH Channel Output                             │
└─────────────────────────────────────────────────────────────┘
```

## What Changes

| Component | Before | After |
|-----------|--------|-------|
| Prompt & input | Our line editor | Our line editor (unchanged) |
| Command output | Plain `SendData()` | Spectre markup, tables, panels |
| Interactive prompts | N/A | Spectre `SelectionPrompt` when needed |
| Input handling | Always our code | Our code, except during Spectre prompts |

## Implementation Plan

### Step 1: Add NuGet Package

```xml
<PackageReference Include="Spectre.Console" Version="0.49.1" />
```

### Step 2: Create `SshAnsiConsoleOutput`

Implements `IAnsiConsoleOutput` to direct Spectre rendering to SSH channel.

```csharp
public class SshAnsiConsoleOutput : IAnsiConsoleOutput
{
    private readonly Channel _channel;
    private readonly SshTextWriter _writer;
    private int _width;
    private int _height;

    public SshAnsiConsoleOutput(Channel channel, int width, int height)
    {
        _channel = channel;
        _width = width;
        _height = height;
        _writer = new SshTextWriter(channel);
    }

    public TextWriter Writer => _writer;
    public bool IsTerminal => true;
    public int Width => _width;
    public int Height => _height;

    public void SetEncoding(Encoding encoding) { }

    public void UpdateSize(int width, int height)
    {
        _width = width;
        _height = height;
    }
}
```

### Step 3: Create `SshTextWriter`

Custom `TextWriter` that sends to SSH channel with CRLF translation.

```csharp
public class SshTextWriter : TextWriter
{
    private readonly Channel _channel;

    public SshTextWriter(Channel channel) => _channel = channel;

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        _channel.SendData(Encoding.UTF8.GetBytes(new[] { value }));
    }

    public override void Write(string? value)
    {
        if (value == null) return;

        // Translate LF to CRLF for SSH terminals
        var translated = value.Replace("\n", "\r\n");
        _channel.SendData(Encoding.UTF8.GetBytes(translated));
    }
}
```

### Step 4: Create Per-Connection Console Factory

```csharp
public static class SshConsoleFactory
{
    public static IAnsiConsole Create(Channel channel, int width, int height)
    {
        var output = new SshAnsiConsoleOutput(channel, width, height);

        return AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            ColorSystem = ColorSystemSupport.TrueColor,
            Out = output,
            Interactive = InteractionSupport.No, // We handle input ourselves
        });
    }
}
```

### Step 5: Create Command Handler

Process commands and render output with Spectre.

```csharp
public class CommandHandler
{
    private readonly IAnsiConsole _console;
    private readonly string _connId;

    public CommandHandler(IAnsiConsole console, string connId)
    {
        _console = console;
        _connId = connId;
    }

    public void Execute(string command)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        switch (parts[0].ToLower())
        {
            case "help":
                ShowHelp();
                break;
            case "status":
                ShowStatus();
                break;
            case "whoami":
                _console.MarkupLine($"You are [yellow]{_connId}[/]");
                break;
            default:
                _console.MarkupLine($"[red]Unknown command:[/] {parts[0]}");
                break;
        }
    }

    private void ShowHelp()
    {
        var table = new Table();
        table.AddColumn("Command");
        table.AddColumn("Description");
        table.AddRow("help", "Show this help");
        table.AddRow("status", "Show server status");
        table.AddRow("whoami", "Show connection ID");
        table.AddRow("[dim]Ctrl-C[/]", "Disconnect");

        _console.Write(table);
    }

    private void ShowStatus()
    {
        var panel = new Panel("[green]Server is running[/]")
        {
            Header = new PanelHeader("Status"),
            Border = BoxBorder.Rounded,
        };
        _console.Write(panel);
    }
}
```

### Step 6: Update Program.cs

Wire Spectre console into the existing flow.

```csharp
void OnCommandOpened(CommandRequestedArgs e, string connId, int width, int height)
{
    e.Agreed = true;

    var channel = e.Channel;
    var lineBuffer = new StringBuilder();
    var cursorPos = 0;

    // Create Spectre console for this connection
    var console = SshConsoleFactory.Create(channel, width, height);
    var handler = new CommandHandler(console, connId);

    // Welcome message with Spectre
    console.Write(new Rule("[green]Welcome to SshServer[/]").RuleStyle("dim"));
    console.MarkupLine("Type [blue]help[/] for available commands.");
    channel.SendData("> "u8.ToArray());

    channel.DataReceived += (_, data) =>
    {
        // ... existing Emacs key handling ...

        // On Enter, execute command with Spectre
        case (byte)'\r':
        case (byte)'\n':
            if (lineBuffer.Length > 0)
            {
                var command = lineBuffer.ToString();
                lineBuffer.Clear();
                cursorPos = 0;
                channel.SendData("\r\n"u8.ToArray());

                handler.Execute(command);  // Spectre renders output

                channel.SendData("> "u8.ToArray());
            }
            else
            {
                channel.SendData("\r\n> "u8.ToArray());
            }
            break;
    };
}
```

## Phase 2: Interactive Prompts (Future)

When we need Spectre's interactive prompts (SelectionPrompt, etc.), we'll need `SshAnsiConsoleInput`. This is more complex because:

1. We temporarily hand off input to Spectre
2. Need escape sequence parsing for arrow keys
3. Return to our line editor when prompt completes

```csharp
// Future: Interactive selection
public string SelectUser(string[] users)
{
    // Temporarily switch to Spectre input mode
    var selected = _console.Prompt(
        new SelectionPrompt<string>()
            .Title("Select a user:")
            .AddChoices(users));

    return selected;
}
```

This is deferred to a later milestone.

## File Structure

```
src/SshServer.Host/
├── Program.cs                    (entry point, line editor)
├── Tui/
│   ├── SshAnsiConsoleOutput.cs   (IAnsiConsoleOutput impl)
│   ├── SshTextWriter.cs          (TextWriter impl)
│   ├── SshConsoleFactory.cs      (creates per-connection console)
│   └── CommandHandler.cs         (processes commands, uses Spectre)
```

## Milestones (Revised)

### Milestone 1: Basic Output ✓ COMPLETE
- [x] Add Spectre.Console package
- [x] Implement `SshTextWriter`
- [x] Implement `SshAnsiConsoleOutput`
- [x] Implement `SshConsoleFactory`
- [x] Render welcome message with Spectre `Rule`
- [x] Create `CommandHandler` with `help`, `status`, `whoami`, `clear`, `quit` commands
- [x] Wire into existing line editor

### Milestone 2: Rich Output
- [ ] Add more commands with tables, panels, trees
- [ ] Add color themes
- [ ] Handle terminal resize (update output dimensions)

### Milestone 3: Interactive Prompts ✓ COMPLETE
- [x] Implement `SshAnsiConsoleInput`
- [x] Implement `EscapeSequenceParser` for arrow keys, Home, End, Delete, F-keys
- [x] Support `SelectionPrompt`, `MultiSelectionPrompt`, `Confirm`, `Ask`
- [x] Input mode switching (line editor ↔ Spectre prompt)
- [x] Async command execution for non-blocking prompts

### Milestone 4: Live Displays (Future)
- [ ] Support `Progress` for long-running operations
- [ ] Support `Status` spinner
- [ ] Support `Live` for real-time updates

## Example Session

```
────────────────── Welcome to SshServer ──────────────────
Type help for available commands.
> help
┌─────────┬─────────────────────────┐
│ Command │ Description             │
├─────────┼─────────────────────────┤
│ help    │ Show this help          │
│ status  │ Show server status      │
│ whoami  │ Show connection ID      │
│ Ctrl-C  │ Disconnect              │
└─────────┴─────────────────────────┘
> status
╭────────╮
│ Status │
├────────┤
│ Server │
│ is     │
│ running│
╰────────╯
> whoami
You are 192.168.1.5:52341-a3f2
> ^C
Goodbye!
```

## Testing Strategy

1. **Output rendering**: Verify tables, panels render correctly in SSH client
2. **Color support**: Test with different terminal emulators
3. **Line editing**: Verify Emacs keys still work between commands
4. **Resize**: Verify Spectre respects updated dimensions
5. **Concurrent**: Multiple connections rendering simultaneously

## Open Questions

1. **CRLF handling**: Does Spectre emit `\n` or `\r\n`? May need translation in `SshTextWriter`.
2. **Terminal capabilities**: Should we detect client terminal type from PTY request and adjust color support?
3. **Width constraints**: How does Spectre handle output wider than terminal? Need to test.
