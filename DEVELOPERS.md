# Building Your Own SSH TUI Application

This guide explains how to use this project as a foundation for building your own SSH-based terminal application.

## Project Structure

```
src/
├── SshServer.Core/               # SSH protocol library (reusable)
│   ├── Ssh/
│   │   ├── Algorithms/           # Crypto: ECDSA, Ed25519, RSA, AES, HMAC
│   │   ├── Messages/             # SSH protocol messages
│   │   ├── Services/             # Auth, Connection, Channel management
│   │   ├── Session.cs            # Main session handler
│   │   └── SshServer.cs          # TCP listener and connection acceptor
│   └── HostKeyStore.cs           # Host key generation and loading
│
└── SshServer.Host/               # Demo application (use as template)
    ├── Program.cs                # Server startup and event wiring
    ├── SshShellApplication.cs    # Abstract base class for applications
    ├── DemoApp.cs                # Demo implementation
    ├── SshServerOptions.cs       # Configuration model
    ├── AuthorizedKeysStore.cs    # Public key authentication
    └── Tui/
        ├── LineEditor.cs         # Terminal line editing
        ├── SshConsoleFactory.cs  # Creates Spectre.Console per connection
        └── ...                   # Other TUI infrastructure
```

## Quick Start: Create Your Own Application

### Step 1: Inherit from SshShellApplication

Create a new class that inherits from `SshShellApplication`:

```csharp
using SshServer.Host;

public class MyApp : SshShellApplication
{
    protected override string Prompt => "myapp> ";

    protected override IEnumerable<string> Completions =>
        ["help", "greet", "quit"];

    protected override void OnWelcome()
    {
        WriteLine("[bold green]Welcome to MyApp![/]");
        WriteLine("Type [blue]help[/] for commands.");
    }

    protected override bool OnCommand(string command)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return true;

        switch (parts[0].ToLowerInvariant())
        {
            case "help":
                WriteLine("[blue]Commands:[/] help, greet, quit");
                break;

            case "greet":
                var name = Ask("What's your name?");
                WriteLine($"Hello, [green]{Escape(name)}[/]!");
                break;

            case "quit":
                return false; // Disconnect

            default:
                WriteLine($"[red]Unknown:[/] {Escape(parts[0])}");
                break;
        }

        return true; // Continue session
    }
}
```

### Step 2: Register Your Application

In `Program.cs`, replace `DemoApp` with your class:

```csharp
// In OnCommandOpened method:
var app = new MyApp();  // Changed from DemoApp
app.Run(channel, consoleContext, connInfo.ToRecord(), options, Disconnect, updateActivity, logger);
```

### Step 3: Run

```bash
dotnet run --project src/SshServer.Host/SshServer.Host.csproj
ssh -p 2222 localhost
```

## SshShellApplication Reference

### Abstract Members (must override)

| Member | Description |
|--------|-------------|
| `OnCommand(string command)` | Handle user commands. Return `false` to disconnect. |

### Virtual Members (optional overrides)

| Member | Default | Description |
|--------|---------|-------------|
| `Prompt` | `"> "` | The prompt string shown before input |
| `Completions` | `[]` | Command names for tab completion |
| `OnWelcome()` | Basic message | Called to display welcome message |
| `OnConnect()` | Empty | Called when connection established |
| `OnDisconnect()` | Empty | Called when session ends |
| `OnExec(command)` | `null` | Handle exec channel commands (scripting) |

### Built-in Properties

| Property | Description |
|----------|-------------|
| `Console` | The `IAnsiConsole` for advanced Spectre.Console rendering |
| `Connection` | Connection info (ID, username, auth method, fingerprint) |
| `Options` | Server configuration options |

### Built-in Helper Methods

| Method | Description |
|--------|-------------|
| `WriteLine(markup)` | Write a line with Spectre markup |
| `Write(markup)` | Write without newline |
| `Write(renderable)` | Write a Table, Panel, Tree, etc. |
| `Clear()` | Clear the screen |
| `Ask(prompt)` | Get text input from user |
| `Ask<T>(prompt)` | Get typed input from user |
| `Confirm(prompt)` | Get yes/no confirmation |
| `Select(title, choices)` | Single selection prompt |
| `MultiSelect(title, choices)` | Multi-selection prompt |
| `Status(message, work)` | Show spinner while working |
| `Progress(work)` | Show progress bars |
| `Disconnect(message)` | Programmatically disconnect |
| `Escape(text)` | Escape text for safe markup |

## Examples

### Interactive Menu Application

```csharp
public class MenuApp : SshShellApplication
{
    protected override bool OnCommand(string command)
    {
        // Ignore typed commands, use menu instead
        ShowMainMenu();
        return true;
    }

    protected override void OnWelcome()
    {
        ShowMainMenu();
    }

    private void ShowMainMenu()
    {
        while (true)
        {
            var choice = Select("Main Menu", new[]
            {
                "View Status",
                "Settings",
                "Exit"
            });

            switch (choice)
            {
                case "View Status":
                    WriteLine("[green]System OK[/]");
                    break;
                case "Settings":
                    ShowSettings();
                    break;
                case "Exit":
                    return;
            }
        }
    }

    private void ShowSettings()
    {
        var options = MultiSelect("Enable features:",
            new[] { "Logging", "Metrics", "Alerts" });
        WriteLine($"Enabled: {string.Join(", ", options)}");
    }
}
```

### Database-Backed Application

```csharp
public class DbApp : SshShellApplication
{
    private readonly IDbConnection _db;

    public DbApp(IDbConnection db)
    {
        _db = db;
    }

    protected override bool OnCommand(string command)
    {
        if (command == "users")
        {
            var users = _db.Query<User>("SELECT * FROM Users LIMIT 10");

            var table = new Table()
                .AddColumn("Name")
                .AddColumn("Email");

            foreach (var user in users)
                table.AddRow(user.Name, user.Email);

            Write(table);
        }
        return true;
    }
}
```

### Exec Channel (Scripting Support)

```csharp
public class ScriptableApp : SshShellApplication
{
    // Handle scripted commands: ssh user@host "status"
    protected override string? OnExec(string command)
    {
        return command switch
        {
            "status" => "OK\n",
            "version" => "1.0.0\n",
            "health" => GetHealthCheck(),
            _ => null  // Fall back to OnCommand
        };
    }

    private string GetHealthCheck()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"uptime: {Environment.TickCount64 / 1000}s");
        sb.AppendLine($"memory: {GC.GetTotalMemory(false) / 1024}KB");
        return sb.ToString();
    }

    protected override bool OnCommand(string command)
    {
        // Interactive commands here
        return true;
    }
}
```

### Long-Running Operations

```csharp
protected override bool OnCommand(string command)
{
    if (command == "process")
    {
        Status("[yellow]Processing...[/]", () =>
        {
            Thread.Sleep(3000); // Simulated work
        });
        WriteLine("[green]Done![/]");
    }

    if (command == "batch")
    {
        Progress(ctx =>
        {
            var task = ctx.AddTask("Processing items");
            for (int i = 0; i < 100; i++)
            {
                task.Increment(1);
                Thread.Sleep(50);
            }
        });
    }

    return true;
}
```

## Configuration

Settings in `appsettings.json`:

```json
{
  "SshServer": {
    "Port": 2222,
    "Banner": "SSH-2.0-MyApp",
    "HostKeyPath": "hostkey.pem",
    "MaxConnections": 100,
    "LogLevel": "Information",
    "AllowAnonymous": false,
    "AuthorizedKeysPath": "./authorized_keys",
    "SessionTimeoutMinutes": 30
  }
}
```

## Authentication

### Anonymous Access

Set `AllowAnonymous: true` in config. Good for dev/demo.

### Public Key Authentication

1. Create `authorized_keys` file with OpenSSH public keys:
   ```
   ssh-ed25519 AAAA... user@host
   ssh-rsa AAAA... another@host
   ```

2. Set `AllowAnonymous: false` and `AuthorizedKeysPath` in config.

### Custom Authentication

Modify the `UserAuth` event handler in `Program.cs`:

```csharp
authService.UserAuth += (_, args) =>
{
    if (args.AuthMethod == "publickey")
    {
        // Custom validation
        args.Result = MyAuthService.ValidateKey(args.Username, args.Key);
    }
};
```

## Troubleshooting

### Input Not Working in Prompts

The base class handles input routing automatically. If you're using raw `Console` access, ensure you're in the right mode.

### Terminal Display Issues

- The base class handles PTY setup automatically
- Use `\r\n` for line endings (handled by built-in methods)
- Test with different terminal sizes

### Session Timeout

Set `SessionTimeoutMinutes: 0` to disable, or increase the value for longer sessions.

## License

MIT
