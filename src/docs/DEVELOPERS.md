# Building Your Own SSH TUI Application

This guide explains how to build your own SSH-based terminal application using the SshServer library.

## Quick Start

### Step 1: Create a New Project

```bash
dotnet new console -n MySSHApp
cd MySSHApp
dotnet add package SshServer
```

### Step 2: Create Your Application

Create a class that inherits from `SshShellApplication`:

```csharp
using SshServer;

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

### Step 3: Configure and Run the Server

```csharp
using SshServer;

await SshServerHost.CreateBuilder()
    .UsePort(2222)
    .AllowAnonymous()
    .UseApplication<MyApp>()
    .Build()
    .RunAsync();
```

### Step 4: Connect

```bash
ssh -p 2222 localhost
```

## Builder API Reference

The `SshServerBuilder` provides a fluent API for configuring your server:

```csharp
await SshServerHost.CreateBuilder()
    // Network
    .UsePort(2222)                              // TCP port to listen on
    .UseBanner("SSH-2.0-MyApp")                 // SSH protocol banner

    // Security
    .UseHostKeyPath("hostkey.pem")              // Path to host key file
    .AllowAnonymous()                           // Enable anonymous authentication
    .UseAuthorizedKeysFile("authorized_keys")   // Path to authorized_keys file

    // Sessions
    .UseSessionTimeout(TimeSpan.FromMinutes(30)) // Idle timeout
    .UseMaxConnections(100)                      // Max concurrent connections

    // Logging
    .UseLogLevel(LogLevel.Information)           // Minimum log level
    .ConfigureLogging(builder => {               // Custom logging configuration
        builder.AddConsole();
    })

    // Configuration
    .UseDefaultConfiguration(args)               // Load appsettings.json + env vars
    .UseConfiguration(configuration)             // Use custom IConfiguration

    // Application
    .UseApplication<MyApp>()                     // Register your app class
    .UseApplication(() => new MyApp(deps))       // Or use a factory

    .Build()
    .RunAsync();
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
        ShowMainMenu();
        return true;
    }

    protected override void OnWelcome() => ShowMainMenu();

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

// Usage with factory:
await SshServerHost.CreateBuilder()
    .UsePort(2222)
    .UseApplication(() => new DbApp(CreateConnection()))
    .Build()
    .RunAsync();
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

## Multi-Application Support

Host multiple applications with username-based routing and/or an interactive menu.

### Username-Based Routing

Map specific usernames directly to applications:

```csharp
await SshServerHost.CreateBuilder()
    .MapUser<AdminApp>("admin")      // ssh admin@host → AdminApp
    .MapUser<MonitorApp>("monitor")  // ssh monitor@host → MonitorApp
    .UseApplication<DefaultApp>()    // Fallback for other users
    .Build()
    .RunAsync();
```

### Interactive Menu

Show a selection menu for unmapped users:

```csharp
await SshServerHost.CreateBuilder()
    .MapUser<AdminApp>("admin")      // Direct access for admin

    .UseApplicationMenu(menu => menu
        .WithTitle("Select an application:")
        .Add<DemoApp>("Demo", "Interactive demos and examples")
        .Add<AdminApp>("Admin", "Server administration")
        .Add<MonitorApp>("Monitor", "Live system metrics")
        .SetDefaultForExec("Demo")   // Default app for exec commands
        .ReturnToMenuOnExit(true))   // Return to menu instead of disconnect

    .Build()
    .RunAsync();
```

### Exec Command Routing

For menu users, exec commands can target specific apps with `appname:command` syntax:

```bash
# Direct user mapping
ssh admin@host "users"           # Runs 'users' in AdminApp

# Menu users with app prefix
ssh guest@host "admin:users"     # Runs 'users' in AdminApp
ssh guest@host "monitor:health"  # Runs 'health' in MonitorApp
ssh guest@host "status"          # Runs 'status' in default app (Demo)
```

### Complete Multi-App Example

```csharp
public class AdminApp : SshShellApplication
{
    protected override string Prompt => "[red]admin[/]> ";

    protected override IEnumerable<string> Completions =>
        ["help", "users", "logs", "config", "exit"];

    protected override void OnWelcome()
    {
        WriteLine("[bold red]Admin Console[/]");
        WriteLine("[dim]Type 'help' for commands.[/]");
    }

    protected override string? OnExec(string command)
    {
        return command switch
        {
            "users" => $"Active connections: {GetConnectionCount()}\n",
            "config" => GetConfigSummary(),
            _ => null  // Fall back to OnCommand
        };
    }

    protected override bool OnCommand(string command)
    {
        // Interactive command handling
        return command != "exit";
    }
}
```

## Configuration

### Using appsettings.json

Create `appsettings.json`:

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

Load it in your app:

```csharp
await SshServerHost.CreateBuilder()
    .UseDefaultConfiguration(args)  // Loads appsettings.json + env + CLI
    .UseApplication<MyApp>()
    .Build()
    .RunAsync();
```

### Environment Variables

Override settings with `SSHSERVER_` prefix:

```bash
SSHSERVER_PORT=3333 dotnet run
SSHSERVER_ALLOWANONYMOUS=false dotnet run
```

## Authentication

### Anonymous Access

```csharp
await SshServerHost.CreateBuilder()
    .AllowAnonymous()
    .UseApplication<MyApp>()
    .Build()
    .RunAsync();
```

### Public Key Authentication

1. Create `authorized_keys` file with OpenSSH public keys:
   ```
   ssh-ed25519 AAAA... user@host
   ssh-rsa AAAA... another@host
   ```

2. Configure the server:
   ```csharp
   await SshServerHost.CreateBuilder()
       .AllowAnonymous(false)
       .UseAuthorizedKeysFile("authorized_keys")
       .UseApplication<MyApp>()
       .Build()
       .RunAsync();
   ```

## Project Structure

```
src/
├── SshServer.Core/               # The SshServer NuGet package
│   ├── Hosting/
│   │   ├── SshServerHost.cs      # Server lifecycle management
│   │   ├── SshServerBuilder.cs   # Fluent builder API
│   │   ├── SshShellApplication.cs # Base class for apps
│   │   ├── SshServerOptions.cs   # Configuration model
│   │   ├── AuthorizedKeysStore.cs # Public key auth
│   │   ├── ConnectionInfo.cs     # Connection metadata
│   │   └── Tui/
│   │       ├── LineEditor.cs     # Terminal line editing
│   │       ├── SshConsoleFactory.cs # Spectre.Console per connection
│   │       └── ...
│   ├── Ssh/
│   │   ├── Algorithms/           # Crypto: ECDSA, Ed25519, RSA, AES, HMAC
│   │   ├── Messages/             # SSH protocol messages
│   │   ├── Services/             # Auth, Connection, Channel management
│   │   ├── Session.cs            # Main session handler
│   │   └── SshServer.cs          # TCP listener
│   └── HostKeyStore.cs           # Host key generation and loading
│
└── SshServer.Demo/               # Demo application (not published)
    ├── Program.cs                # Simple builder usage example
    ├── DemoApp.cs                # Demo implementation
    └── appsettings.json          # Demo configuration
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
