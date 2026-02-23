# Building Your Own SSH TUI Application

This guide explains how to use this project as a foundation for building your own SSH-based terminal application.

## Project Structure

```
src/
├── SshServer.Core/           # SSH protocol library (reusable)
│   ├── Ssh/
│   │   ├── Algorithms/       # Crypto: ECDSA, Ed25519, RSA, AES, HMAC
│   │   ├── Messages/         # SSH protocol messages
│   │   ├── Services/         # Auth, Connection, Channel management
│   │   ├── Session.cs        # Main session handler
│   │   └── SshServer.cs      # TCP listener and connection acceptor
│   └── HostKeyStore.cs       # Host key generation and loading
│
└── SshServer.Host/           # Demo application (copy this)
    ├── Program.cs            # Entry point and event wiring
    ├── SshServerOptions.cs   # Configuration model
    ├── AuthorizedKeysStore.cs # Public key authentication
    └── Tui/
        ├── CommandHandler.cs     # Your application logic goes here
        ├── LineEditor.cs         # Terminal line editing
        ├── SshConsoleFactory.cs  # Creates Spectre.Console per connection
        ├── SshAnsiConsoleInput.cs    # Routes SSH input to Spectre
        ├── SshAnsiConsoleOutput.cs   # Routes Spectre output to SSH
        └── SshTextWriter.cs      # CRLF translation for SSH
```

## Quick Start: Create Your Own Application

### Step 1: Copy the Demo Project

```bash
# Clone the repository
git clone https://github.com/yourusername/sshserver.git
cd sshserver

# Copy the demo as your starting point
cp -r src/SshServer.Host src/MyApp.Host

# Update the project file
mv src/MyApp.Host/SshServer.Host.csproj src/MyApp.Host/MyApp.Host.csproj
```

Edit `MyApp.Host.csproj`:
```xml
<RootNamespace>MyApp.Host</RootNamespace>
<AssemblyName>MyApp.Host</AssemblyName>
```

### Step 2: Implement Your Command Handler

The `CommandHandler` class is where your application logic lives. Replace it with your own:

```csharp
// Tui/CommandHandler.cs
public class CommandHandler
{
    private readonly IAnsiConsole _console;
    private readonly ConnectionInfo _connInfo;

    public CommandHandler(IAnsiConsole console, ConnectionInfo connInfo, MyAppOptions options)
    {
        _console = console;
        _connInfo = connInfo;
    }

    /// <summary>
    /// Execute a command. Return false to disconnect the session.
    /// </summary>
    public bool Execute(string command)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return true;

        switch (parts[0].ToLowerInvariant())
        {
            case "help":
                ShowHelp();
                break;

            case "quit":
            case "exit":
                return false; // Disconnect

            default:
                _console.MarkupLine($"[red]Unknown command:[/] {Markup.Escape(parts[0])}");
                break;
        }

        return true; // Continue session
    }

    private void ShowHelp()
    {
        _console.MarkupLine("[blue]Available commands:[/]");
        _console.MarkupLine("  help  - Show this message");
        _console.MarkupLine("  quit  - Disconnect");
    }

    public void ShowWelcome()
    {
        _console.MarkupLine("[green]Welcome to MyApp![/]");
        _console.MarkupLine("Type [blue]help[/] for commands.");
    }
}
```

### Step 3: Update Tab Completions

In `Program.cs`, update the completions list to match your commands:

```csharp
var lineEditor = new LineEditor(data => channel.SendData(data))
{
    Completions = ["help", "status", "quit", "exit"]  // Your commands
};
```

### Step 4: Run Your Application

```bash
dotnet run --project src/MyApp.Host/MyApp.Host.csproj
```

Connect:
```bash
ssh -p 2222 localhost
```

## Key Extension Points

### Custom Authentication

To add custom authentication logic, modify the `UserAuth` event handler in `Program.cs`:

```csharp
authService.UserAuth += (_, args) =>
{
    switch (args.AuthMethod)
    {
        case "publickey":
            // Custom public key validation
            args.Result = MyAuthService.ValidateKey(args.Username, args.Key);
            break;

        case "password":
            // Password authentication (if implemented)
            args.Result = MyAuthService.ValidatePassword(args.Username, args.Password);
            break;

        case "none":
            // Anonymous access
            args.Result = options.AllowAnonymous;
            break;
    }
};
```

### Interactive Prompts

Use Spectre.Console prompts for interactive input:

```csharp
// Selection prompt
var choice = _console.Prompt(
    new SelectionPrompt<string>()
        .Title("Select an option:")
        .AddChoices(["Option 1", "Option 2", "Option 3"]));

// Text input
var name = _console.Ask<string>("Enter your name:");

// Confirmation
var confirmed = _console.Confirm("Are you sure?");

// Multi-select
var items = _console.Prompt(
    new MultiSelectionPrompt<string>()
        .Title("Select items:")
        .AddChoices(["Item A", "Item B", "Item C"]));
```

### Rich Output

```csharp
// Tables
var table = new Table()
    .AddColumn("Name")
    .AddColumn("Value");
table.AddRow("Status", "[green]Active[/]");
_console.Write(table);

// Panels
var panel = new Panel("Content here")
{
    Header = new PanelHeader("Title"),
    Border = BoxBorder.Rounded
};
_console.Write(panel);

// Progress bars
_console.Progress().Start(ctx =>
{
    var task = ctx.AddTask("Processing...");
    while (!ctx.IsFinished)
    {
        task.Increment(10);
        Thread.Sleep(100);
    }
});

// Trees
var tree = new Tree("Root");
tree.AddNode("Child 1").AddNode("Grandchild");
tree.AddNode("Child 2");
_console.Write(tree);
```

### Per-Connection State

Each SSH connection gets its own instances of `CommandHandler`, `LineEditor`, and `IAnsiConsole`. Store per-connection state in your `CommandHandler`:

```csharp
public class CommandHandler
{
    private readonly Dictionary<string, object> _sessionData = new();

    public void SetData(string key, object value) => _sessionData[key] = value;
    public T? GetData<T>(string key) => _sessionData.TryGetValue(key, out var v) ? (T)v : default;
}
```

### Configuration

Add custom settings to `SshServerOptions.cs`:

```csharp
public class MyAppOptions : SshServerOptions
{
    public string DatabaseConnectionString { get; set; } = "";
    public int MaxItemsPerPage { get; set; } = 20;
}
```

## Architecture Deep Dive

### Connection Lifecycle

```
1. TCP connection accepted
   └── Session created
       └── SSH handshake (key exchange, encryption)
           └── Authentication (UserAuth event)
               └── Shell channel opened (CommandOpened event)
                   └── Your CommandHandler receives input
                       └── LineEditor handles line editing
                           └── Commands executed
                               └── Spectre.Console renders output
```

### Event Flow

| Event | Source | Description |
|-------|--------|-------------|
| `ConnectionAccepted` | SshServer | New TCP connection |
| `ServiceRegistered` | Session | Auth or connection service ready |
| `UserAuth` | UserAuthService | Authentication request |
| `PtyReceived` | ConnectionService | Terminal dimensions |
| `WindowChange` | ConnectionService | Terminal resized |
| `CommandOpened` | ConnectionService | Shell channel ready |
| `DataReceived` | Channel | Input from client |
| `CloseReceived` | Channel | Client disconnecting |

### Thread Safety

- Each connection runs on its own thread
- `CommandHandler.Execute()` is called from a background task
- Spectre.Console prompts block the input thread during interaction
- The `inPromptMode` flag routes input appropriately

## Common Patterns

### Database-Backed Application

```csharp
public class CommandHandler
{
    private readonly IDbConnection _db;

    public CommandHandler(IAnsiConsole console, ConnectionInfo connInfo, IDbConnection db)
    {
        _db = db;
    }

    public bool Execute(string command)
    {
        if (command == "users")
        {
            var users = _db.Query<User>("SELECT * FROM Users");
            var table = new Table().AddColumn("Name").AddColumn("Email");
            foreach (var user in users)
                table.AddRow(user.Name, user.Email);
            _console.Write(table);
        }
        return true;
    }
}
```

### Menu-Driven Application

```csharp
public bool Execute(string command)
{
    while (true)
    {
        var choice = _console.Prompt(
            new SelectionPrompt<string>()
                .Title("Main Menu")
                .AddChoices(["View Items", "Add Item", "Settings", "Exit"]));

        switch (choice)
        {
            case "View Items": ViewItems(); break;
            case "Add Item": AddItem(); break;
            case "Settings": Settings(); break;
            case "Exit": return false;
        }
    }
}
```

### Long-Running Operations

```csharp
public bool Execute(string command)
{
    if (command == "process")
    {
        _console.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Processing...", ctx =>
            {
                // Long operation here
                Thread.Sleep(3000);
            });
        _console.MarkupLine("[green]Done![/]");
    }
    return true;
}
```

## Troubleshooting

### Input Not Working in Prompts

Ensure the `inPromptMode` flag is set before executing commands:

```csharp
inPromptMode = true;
Task.Run(() =>
{
    try { commandHandler.Execute(command); }
    finally { inPromptMode = false; }
});
```

### Terminal Display Issues

- Ensure PTY dimensions are passed to `SshConsoleFactory.Create()`
- Handle `WindowChange` events to update console size
- Use `\r\n` for line endings (handled by `SshTextWriter`)

### Authentication Failures

- Check `authorized_keys` file format (OpenSSH format)
- Verify file path resolution (relative to executable, not working directory)
- Enable `Trace` logging to see authentication details

## Dependencies

| Package | Purpose | Required |
|---------|---------|----------|
| `SshServer.Core` | SSH protocol | Yes |
| `Spectre.Console` | TUI rendering | Recommended |
| `Microsoft.Extensions.Hosting` | Configuration | Optional |
| `Microsoft.Extensions.Logging` | Logging | Optional |

## License

MIT
