# AlfredBr.SshServer.Core — API Manual

**Package:** `AlfredBr.SshServer.Core`
**Namespace:** `AlfredBr.SshServer.Core`
**Target Framework:** .NET 8.0+

This document is the complete API reference for NuGet consumers of `AlfredBr.SshServer.Core`. It covers every public type, method, property, and event needed to build SSH-hosted terminal applications without reading the library source code.

---

## Table of Contents

1. [Overview](#overview)
2. [Installation](#installation)
3. [Quick Start](#quick-start)
4. [SshServerHost](#sshserverhost)
5. [SshServerBuilder](#sshserverbuilder)
6. [SshServerOptions](#sshserveroptions)
7. [SshShellApplication](#sshshellapplication)
   - [Lifecycle Callbacks](#lifecycle-callbacks)
   - [Output Helpers](#output-helpers)
   - [Interactive Prompt Helpers](#interactive-prompt-helpers)
   - [Direct Spectre.Console Access](#direct-spectreconsole-access)
8. [ConnectionInfo](#connectioninfo)
9. [AppMenuConfiguration](#appmenuconfiguration)
10. [AuthorizedKeysStore](#authorizedkeysstore)
11. [HostKeyStore](#hostkeystore)
12. [LineEditor](#lineeditor)
13. [Configuration File Reference](#configuration-file-reference)
14. [Authentication Guide](#authentication-guide)
15. [Exec Channel (Non-Interactive)](#exec-channel-non-interactive)
16. [Graceful Shutdown](#graceful-shutdown)
17. [Complete Examples](#complete-examples)

---

## Overview

`AlfredBr.SshServer.Core` is a library for hosting terminal UI applications over SSH — inspired by [charmbracelet/wish](https://github.com/charmbracelet/wish) in Go. Clients connect with a standard `ssh` client and interact with a [Spectre.Console](https://spectreconsole.net/) powered TUI.

The library handles:
- SSH transport (key exchange, encryption, MAC, channel multiplexing)
- Anonymous and public-key authentication
- PTY allocation and window-resize events
- Emacs-style line editing with history and tab completion
- Routing connections to application classes
- Per-connection `IAnsiConsole` instances so every SSH session renders independently

You write a class that inherits `SshShellApplication`, override a few methods, and the framework handles the rest.

---

## Installation

```bash
dotnet add package AlfredBr.SshServer.Core
```

---

## Quick Start

```csharp
using AlfredBr.SshServer.Core;

// 1. Define your application
public class MyApp : SshShellApplication
{
    protected override string Prompt => "myapp> ";

    protected override IEnumerable<string> Completions => ["help", "quit"];

    protected override bool OnCommand(string command)
    {
        switch (command.ToLower())
        {
            case "help":
                WriteLine("Commands: [blue]help[/], [blue]quit[/]");
                break;
            case "quit":
            case "exit":
                return false;           // returning false disconnects the client
            default:
                WriteLine($"Unknown: {Escape(command)}");
                break;
        }
        return true;
    }
}

// 2. Start the server
await SshServerHost.CreateBuilder()
    .UsePort(2222)
    .AllowAnonymous()
    .UseMaxConnections(100)
    .UseApplication<MyApp>()
    .Build()
    .RunAsync();
```

Connect from any terminal:

```bash
ssh -p 2222 localhost
```

---

## SshServerHost

```
namespace AlfredBr.SshServer.Core
sealed class SshServerHost : IAsyncDisposable
```

The top-level server object. Obtain one via `SshServerHost.CreateBuilder()` — do not construct it directly.

### Factory

| Method | Returns | Description |
|--------|---------|-------------|
| `static CreateBuilder()` | `SshServerBuilder` | Returns a new fluent builder. |

### Instance Methods

| Method | Description |
|--------|-------------|
| `RunAsync(CancellationToken ct = default)` | Start listening and block until `ct` is cancelled. |
| `StopAsync()` | Gracefully stop the server, closing all active sessions. |
| `DisposeAsync()` | Release all resources (`await using` compatible). |

**Typical pattern:**

```csharp
await using var host = SshServerHost.CreateBuilder()
    /* ... configure ... */
    .Build();

await host.RunAsync(cancellationToken);
```

---

## SshServerBuilder

```
namespace AlfredBr.SshServer.Core
class SshServerBuilder
```

Fluent builder returned by `SshServerHost.CreateBuilder()`. All configuration methods return `this` for chaining. Call `Build()` once to obtain the `SshServerHost`.

### Network & Identity

| Method | Default | Description |
|--------|---------|-------------|
| `UsePort(int port)` | `2222` | TCP port to listen on. |
| `UseBanner(string banner)` | `"SSH-2.0-SshServer"` | SSH protocol identification banner sent to clients. |
| `UseHostKeyPath(string path)` | `"hostkey_ecdsa_nistp256.pem"` | Path to the PEM host key file. Created automatically if absent. |

### Limits & Timeouts

| Method | Default | Description |
|--------|---------|-------------|
| `UseMaxConnections(int max)` | `100` | Maximum simultaneous SSH connections. `0` = unlimited. |
| `UseSessionTimeout(TimeSpan timeout)` | (none) | Idle session timeout. Sessions with no input for this long are disconnected. |

### Authentication

| Method | Default | Description |
|--------|---------|-------------|
| `AllowAnonymous(bool allow = true)` | `true` | Accept connections without any credentials. |
| `UseAuthorizedKeysFile(string path)` | (none) | Path to an OpenSSH-format `authorized_keys` file for public-key authentication. |

See [Authentication Guide](#authentication-guide) for details.

### Application Registration

| Method | Description |
|--------|-------------|
| `UseApplication<TApp>()` | Register a single application class. All connections launch `TApp`. `TApp` must inherit `SshShellApplication` and have a parameterless constructor. |
| `UseApplication(Func<SshShellApplication> factory)` | Register a factory function instead of a type. Useful when you need to inject dependencies. |
| `MapUser<TApp>(string username)` | Route a specific SSH username directly to `TApp`. Users connecting as this username bypass the menu. |
| `MapUser(string username, Func<SshShellApplication> factory)` | Same as above with a factory function. |
| `UseApplicationMenu(Action<AppMenuConfiguration> configure)` | Show an interactive selection menu to users who are not covered by a `MapUser` mapping. See [AppMenuConfiguration](#appmenuconfiguration). |

> **Note:** `Build()` throws `InvalidOperationException` if no application is registered (via `UseApplication`, `MapUser`, or `UseApplicationMenu`).

### Logging

| Method | Description |
|--------|-------------|
| `UseLogLevel(LogLevel level)` | Set the minimum log level (from `Microsoft.Extensions.Logging`). |
| `ConfigureLogging(Action<ILoggingBuilder> configure)` | Full control over the logging pipeline (add providers, filters, etc.). |

### External Configuration

| Method | Description |
|--------|-------------|
| `UseConfiguration(IConfiguration configuration)` | Supply a pre-built `IConfiguration` instance. The builder reads the `"SshServer"` section. |
| `UseDefaultConfiguration(string[]? args = null)` | Load `appsettings.json`, environment variables (`SSHSERVER_*`), and optional CLI args. This is the recommended approach for production apps. |

### Finalising

| Method | Description |
|--------|-------------|
| `Build()` | Validate configuration and return a ready-to-run `SshServerHost`. |

---

## SshServerOptions

```
namespace AlfredBr.SshServer.Core
class SshServerOptions
```

Plain configuration POCO. Populated by the builder from code, `appsettings.json`, environment variables, or CLI arguments. Exposed as `Options` inside every `SshShellApplication`.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Port` | `int` | `2222` | Listening TCP port. |
| `Banner` | `string` | `"SSH-2.0-SshServer"` | SSH protocol banner. |
| `HostKeyPath` | `string` | `"hostkey_ecdsa_nistp256.pem"` | Host key PEM file path. |
| `MaxConnections` | `int` | `100` | Max concurrent connections. `0` = unlimited. |
| `LogLevel` | `string` | `"Debug"` | Minimum log level string (`"Trace"`, `"Debug"`, `"Information"`, `"Warning"`, `"Error"`). |
| `AllowAnonymous` | `bool` | `true` | Whether anonymous connections are accepted. |
| `AuthorizedKeysPath` | `string?` | `null` | Path to `authorized_keys` file. `null` = public-key auth disabled. |
| `SessionTimeoutMinutes` | `int` | `0` | Idle timeout in minutes. `0` = disabled. |

**Configuration section name:** `"SshServer"` (`SshServerOptions.SectionName`)

---

## SshShellApplication

```
namespace AlfredBr.SshServer.Core
abstract class SshShellApplication
```

The base class for every SSH application. Inherit from this class and override the members you need. The framework creates one instance per connection, so instance fields are safe for per-session state.

### Properties available to subclasses

| Property | Type | Description |
|----------|------|-------------|
| `Console` | `IAnsiConsole` | Spectre.Console instance wired to this SSH session. Use for direct Spectre API calls. |
| `Connection` | `ConnectionInfo` | Details about the current connection (username, auth method, etc.). See [ConnectionInfo](#connectioninfo). |
| `Options` | `SshServerOptions` | The server's configuration at the time it was built. |

---

### Lifecycle Callbacks

Override these virtual methods to respond to connection events.

```csharp
protected virtual void OnConnect()
```
Called once when a client connects and the SSH handshake is complete, **before** `OnWelcome`. Use for per-session initialisation (e.g., incrementing a connection counter).

```csharp
protected virtual void OnWelcome()
```
Called after `OnConnect`. Override to display a welcome banner. The default implementation does nothing.

```csharp
protected virtual void OnDisconnect()
```
Called when the session ends for any reason (client disconnect, timeout, `Disconnect()` call, or `OnCommand` returning `false`). Use for cleanup.

```csharp
protected abstract bool OnCommand(string command)
```
**Required override.** Called for every command line the user submits. `command` is the trimmed input string.

- Return `true` to keep the session alive and show the prompt again.
- Return `false` to disconnect the client gracefully.

```csharp
protected virtual string? OnExec(string command)
```
Called when a client runs a non-interactive (exec) command, e.g. `ssh host cmd`. Return the string output to send back, or `null` to fall through to `OnCommand`. See [Exec Channel](#exec-channel-non-interactive).

---

### Interactive Prompt Override

```csharp
protected virtual string Prompt => "> ";
```
The prompt string displayed before each command line. Supports Spectre markup (e.g., `"[red]admin[/]> "`). The visible character count is used for correct cursor positioning.

```csharp
protected virtual IEnumerable<string> Completions => [];
```
Command names offered for Tab completion. Return any `IEnumerable<string>`. Prefix-matching is applied automatically.

---

### Output Helpers

These are thin wrappers around `Console.Write`/`Console.WriteLine` that accept Spectre.Console markup.

| Method | Description |
|--------|-------------|
| `void WriteLine(string markup)` | Write a markup string followed by a newline. |
| `void Write(string markup)` | Write a markup string with no newline. |
| `void WriteLine()` | Write a blank line. |
| `void Write(IRenderable renderable)` | Write any Spectre `IRenderable` — `Table`, `Panel`, `Tree`, `BarChart`, `Rule`, etc. |
| `void Clear()` | Clear the terminal screen. |
| `void Disconnect(string? message = null)` | Programmatically end the session. Optionally write `message` (supports markup) before disconnecting. |
| `static string Escape(string text)` | Escape user-supplied text so markup characters (`[`, `]`) are not interpreted as Spectre markup. **Always escape untrusted input before embedding in markup strings.** |

**Markup syntax** follows Spectre.Console conventions: `[bold]`, `[red]`, `[green on black]`, `[link=https://...]`, `[/]` to close, etc.

---

### Interactive Prompt Helpers

These wrap Spectre.Console prompts and work correctly over SSH.

```csharp
string Ask(string prompt)
```
Display a text input prompt. Returns the typed string.

```csharp
T Ask<T>(string prompt)
```
Display a typed input prompt. `T` must be parseable by Spectre.Console (e.g., `int`, `double`, `DateTime`).

```csharp
bool Confirm(string prompt, bool defaultValue = true)
```
Display a yes/no confirmation. Returns `true` if the user confirms.

```csharp
T Select<T>(string title, IEnumerable<T> choices) where T : notnull
```
Display an interactive single-selection list. Returns the chosen item.

```csharp
IReadOnlyList<T> MultiSelect<T>(string title, IEnumerable<T> choices) where T : notnull
```
Display an interactive multi-selection list. Returns all chosen items.

```csharp
void Status(string message, Action work)
```
Show a spinner with `message` while `work` executes synchronously.

```csharp
void Progress(Action<ProgressContext> work)
```
Show one or more progress bars while `work` executes. Use `ProgressContext.AddTask` to register tasks, then `ProgressTask.Increment` / `ProgressTask.Value` to update them.

---

### Direct Spectre.Console Access

The `Console` property is a full `IAnsiConsole` instance. Use it for anything not covered by the helpers:

```csharp
// Custom selection prompt with page size
var item = Console.Prompt(
    new SelectionPrompt<string>()
        .Title("Choose:")
        .PageSize(10)
        .AddChoices(["Alpha", "Beta", "Gamma"]));

// Text prompt with validation
var age = Console.Prompt(
    new TextPrompt<int>("Your age?")
        .Validate(n => n > 0 && n < 150
            ? ValidationResult.Success()
            : ValidationResult.Error("Invalid age")));

// Live-updating table
Console.Live(table)
    .Start(ctx => { /* update table; ctx.Refresh(); */ });

// Status with spinner control
Console.Status()
    .Spinner(Spinner.Known.Dots)
    .Start("Working...", ctx =>
    {
        ctx.Status("Step 2...");
        ctx.Spinner(Spinner.Known.Star);
    });

// Progress bars with full control
Console.Progress()
    .Columns([new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn()])
    .Start(ctx =>
    {
        var t = ctx.AddTask("[green]Downloading[/]");
        while (!ctx.IsFinished) { t.Increment(5); Thread.Sleep(50); }
    });
```

Add `using Spectre.Console;` for access to `SelectionPrompt<T>`, `TextPrompt<T>`, `Table`, `Panel`, `Tree`, `BarChart`, etc.

---

## ConnectionInfo

```
namespace AlfredBr.SshServer.Core
public record ConnectionInfo(
    string ConnectionId,
    string Username,
    string AuthMethod,
    string? KeyFingerprint = null)
```

Immutable record describing the current SSH session. Accessed via `Connection` inside `SshShellApplication`.

| Property | Type | Description |
|----------|------|-------------|
| `ConnectionId` | `string` | Unique identifier for this connection in the format `"IP:PORT-GUID"`. Use for logging. |
| `Username` | `string` | SSH username supplied by the client. May be empty or `"anonymous"` for anonymous auth. |
| `AuthMethod` | `string` | Authentication method used: `"none"` (anonymous) or `"publickey"`. |
| `KeyFingerprint` | `string?` | SHA-256 fingerprint of the client's public key when `AuthMethod == "publickey"`. `null` otherwise. |

**Example:**

```csharp
protected override void OnWelcome()
{
    WriteLine($"Welcome, [blue]{Escape(Connection.Username)}[/]!");
    if (Connection.AuthMethod == "publickey")
        WriteLine($"[dim]Key: {Escape(Connection.KeyFingerprint ?? "")}[/]");
}
```

---

## AppMenuConfiguration

```
namespace AlfredBr.SshServer.Core
class AppMenuConfiguration
```

Configures the interactive application selection menu shown to users who do not match a `MapUser` route. Pass a configuration action to `SshServerBuilder.UseApplicationMenu`.

All methods return `this` for chaining.

| Method | Description |
|--------|-------------|
| `WithTitle(string title)` | Set the menu heading. Supports Spectre markup. |
| `Add<TApp>(string name, string description = "")` | Register an application by name and optional description. `TApp` must inherit `SshShellApplication` and have a parameterless constructor. |
| `Add(string name, string description, Func<SshShellApplication> factory)` | Register an application with a factory (for dependency injection scenarios). |
| `SetDefaultForExec(string name)` | When a client runs `ssh host <command>` without a `"appname:command"` prefix, route the exec to the app registered under this name. |
| `ReturnToMenuOnExit(bool returnToMenu)` | If `true` (the default), when an app's `OnCommand` returns `false` the user is returned to the menu instead of being disconnected. |

**Multi-app routing example:**

```csharp
await SshServerHost.CreateBuilder()
    .MapUser<AdminApp>("admin")      // ssh admin@host  → AdminApp directly
    .MapUser<DevApp>("dev")          // ssh dev@host    → DevApp directly
    .UseApplicationMenu(menu => menu
        .WithTitle("[bold]Select Application[/]")
        .Add<DemoApp>("Demo", "Interactive demo")
        .Add<AdminApp>("Admin", "Server administration")
        .SetDefaultForExec("Demo")
        .ReturnToMenuOnExit(true))
    .Build()
    .RunAsync();
```

**Exec routing with prefixes:**

```bash
ssh host "Demo:status"    # Run 'status' command in DemoApp
ssh host "Admin:users"    # Run 'users' command in AdminApp
ssh host status           # SetDefaultForExec("Demo") → DemoApp.OnExec("status")
```

---

## AuthorizedKeysStore

```
namespace AlfredBr.SshServer.Core
class AuthorizedKeysStore
```

Loads and validates OpenSSH public keys. The builder uses this internally when `UseAuthorizedKeysFile` is called, but you can also use it directly for custom auth logic.

### Constructor

```csharp
AuthorizedKeysStore(ILogger? logger = null)
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Count` | `int` | Number of keys currently loaded. |

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `LoadFromFile(string path)` | `bool` | Load keys from an `authorized_keys` file. Returns `true` on success. Safe to call repeatedly — replaces previously loaded keys. |
| `IsAuthorized(string keyType, byte[] keyData)` | `bool` | Return `true` if the key matches any loaded entry. |

### Supported Key Types

`ssh-rsa`, `ssh-ed25519`, `ecdsa-sha2-nistp256`, `ecdsa-sha2-nistp384`, `ecdsa-sha2-nistp521`, `ssh-dss`

---

## HostKeyStore

```
namespace AlfredBr.SshServer.Core
static class HostKeyStore
```

Manages the server's ECDSA nistp256 host key. Called automatically by the builder, but exposed publicly if you need manual control.

| Method | Description |
|--------|-------------|
| `static string LoadOrGenerate(string path = "hostkey_ecdsa_nistp256.pem")` | Load the PEM key from `path`, or generate and save a new one if absent. Returns the PEM string. |

**Note:** The host key file should be stored persistently. Regenerating it causes SSH clients to warn about a changed host key identity.

---

## LineEditor

```
namespace AlfredBr.SshServer.Core.Tui
class LineEditor
```

The Emacs-style line editor used internally for the shell prompt. Exposed publicly for advanced scenarios where you want to handle raw input yourself (e.g., a custom REPL that does not use `SshShellApplication`).

### Constructor

```csharp
LineEditor(Action<byte[]> sendData)
```

`sendData` is a callback invoked to write bytes back to the SSH channel (for echoing characters, cursor movement, etc.).

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Prompt` | `string` | Prompt string to display. |
| `PromptWidth` | `int` | Visible character width of the prompt for cursor math. `-1` = use `Prompt.Length`. Set this when `Prompt` contains markup or invisible characters. |
| `RenderPrompt` | `Action?` | Optional callback invoked when the prompt must be redrawn (e.g., after Ctrl-L). |
| `Completions` | `string[]` | Command names available for Tab completion. |
| `SubmittedLine` | `string` | The last complete line submitted by the user (populated after `ProcessByte` returns `LineSubmitted`). |

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `ShowPrompt()` | `void` | Write the prompt to the terminal. Call once at the start of each input cycle. |
| `ProcessByte(byte b)` | `LineEditorResult` | Feed one byte of input. Drive this from your `DataReceived` event handler. |

### LineEditorResult Enum

| Value | Meaning |
|-------|---------|
| `Continue` | Input processed; keep reading. |
| `LineSubmitted` | The user pressed Enter. Read `SubmittedLine`. |
| `Disconnect` | The user pressed Ctrl-C or Ctrl-D on an empty line. Close the channel. |

### Keyboard Bindings

| Key | Action |
|-----|--------|
| Ctrl-A / Home | Move to beginning of line |
| Ctrl-E / End | Move to end of line |
| Ctrl-B / ← | Move back one character |
| Ctrl-F / → | Move forward one character |
| Alt-B | Move back one word |
| Alt-F | Move forward one word |
| Backspace | Delete character before cursor |
| Ctrl-D / Delete | Delete character at cursor (Ctrl-D on empty line = Disconnect) |
| Ctrl-K | Kill (cut) to end of line |
| Ctrl-U | Kill (cut) to beginning of line |
| Alt-D | Kill word forward |
| Ctrl-Y | Yank (paste) from kill ring |
| Ctrl-P / ↑ | Previous history entry |
| Ctrl-N / ↓ | Next history entry |
| Tab | Tab completion (prefix match against `Completions`) |
| Ctrl-L | Clear screen and redraw prompt |
| Ctrl-C | Disconnect |
| Enter | Submit line |

---

## Configuration File Reference

When using `UseDefaultConfiguration(args)`, the builder reads from `appsettings.json` (in the working directory), environment variables with the `SSHSERVER_` prefix, and optional command-line arguments.

**`appsettings.json` example:**

```json
{
  "SshServer": {
    "Port": 2222,
    "Banner": "SSH-2.0-MyApp",
    "HostKeyPath": "hostkey_ecdsa_nistp256.pem",
    "MaxConnections": 100,
    "LogLevel": "Information",
    "AllowAnonymous": true,
    "AuthorizedKeysPath": "./authorized_keys",
    "SessionTimeoutMinutes": 30
  }
}
```

**Environment variable equivalents** (override `appsettings.json`):

```
SSHSERVER_PORT=2222
SSHSERVER_BANNER=SSH-2.0-MyApp
SSHSERVER_ALLOWANONYMOUS=true
SSHSERVER_AUTHORIZEDKEYSPATH=/etc/myapp/authorized_keys
SSHSERVER_MAXCONNECTIONS=100
SSHSERVER_SESSIONTIMEOUTMINUTES=30
```

**Command-line argument equivalents:**

```
--SshServer:Port 2222
--SshServer:AllowAnonymous false
--SshServer:MaxConnections 100
```

If the active session count reaches `MaxConnections`, additional connections are rejected with SSH disconnect reason `TooManyConnections`.

---

## Authentication Guide

### Anonymous Access (default)

Any client can connect with no credentials. Enabled by default or explicitly via `.AllowAnonymous()`.

```bash
ssh -p 2222 localhost              # no username required
ssh -p 2222 anyname@localhost      # username is passed to Connection.Username
```

`Connection.AuthMethod` will be `"none"`.

### Public Key Authentication

1. Disable anonymous auth and point to an `authorized_keys` file:
   ```csharp
   .AllowAnonymous(false)
   .UseAuthorizedKeysFile("authorized_keys")
   ```

2. Populate `authorized_keys` in OpenSSH format (one key per line):
   ```
   ssh-ed25519 AAAA...base64... user@machine
   ecdsa-sha2-nistp256 AAAA...base64... another-key
   ssh-rsa AAAA...base64... legacy-key
   ```

3. Connect with a matching key:
   ```bash
   ssh -i ~/.ssh/id_ed25519 -p 2222 localhost
   ```

`Connection.AuthMethod` will be `"publickey"` and `Connection.KeyFingerprint` will contain the `SHA256:...` fingerprint.

### Mixed-mode (key required, anonymous fallback)

```csharp
.AllowAnonymous(true)                        // fallback for unkeyed clients
.UseAuthorizedKeysFile("authorized_keys")    // elevated access for keyed clients
```

Inspect `Connection.AuthMethod` at runtime to decide what to allow:

```csharp
protected override void OnConnect()
{
    if (Connection.AuthMethod == "publickey")
        // grant full access
    else
        // grant limited access
}
```

---

## Exec Channel (Non-Interactive)

Clients can run a single command without entering an interactive session:

```bash
ssh -p 2222 localhost status
ssh -p 2222 localhost "whoami"
result=$(ssh -p 2222 localhost status)   # scriptable
```

The framework calls `OnExec(command)` before entering the interactive loop. If `OnExec` returns a non-null string, that string is sent back to the client and the session ends. If it returns `null`, the framework falls back to `OnCommand` in single-shot mode.

```csharp
protected override string? OnExec(string command)
{
    return command.ToLower() switch
    {
        "status"  => $"running (PID {Environment.ProcessId})\n",
        "version" => "1.0.0\n",
        "whoami"  => $"{Connection.Username} via {Connection.AuthMethod}\n",
        _         => null   // fall through to OnCommand
    };
}
```

### Exec routing with multi-app menus

When `UseApplicationMenu` is configured, exec commands can be prefixed with an app name separated by a colon:

```bash
ssh host "Admin:users"    # routes to AdminApp.OnExec("users")
ssh host status           # routes to SetDefaultForExec app
```

---

## Graceful Shutdown

The recommended pattern hooks both Ctrl-C (`SIGINT`) and process exit (`SIGTERM`):

```csharp
using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;    // prevent immediate termination
    cts.Cancel();
};

AppDomain.CurrentDomain.ProcessExit += (_, _) =>
{
    if (!cts.IsCancellationRequested)
        cts.Cancel();
};

await using var host = SshServerHost.CreateBuilder()
    /* ... */
    .Build();

await host.RunAsync(cts.Token);
```

When `cts.Cancel()` is called, `RunAsync` stops accepting new connections and existing sessions close cleanly before the method returns.

---

## Complete Examples

### Minimal echo server

```csharp
using AlfredBr.SshServer.Core;

public class EchoApp : SshShellApplication
{
    protected override string Prompt => "echo> ";
    protected override IEnumerable<string> Completions => ["quit"];

    protected override bool OnCommand(string command)
    {
        if (command is "quit" or "exit") return false;
        WriteLine(Escape(command));
        return true;
    }
}

await SshServerHost.CreateBuilder()
    .UsePort(2222)
    .AllowAnonymous()
    .UseApplication<EchoApp>()
    .Build()
    .RunAsync();
```

---

### Spectre.Console rich output

```csharp
using Spectre.Console;

public class RichApp : SshShellApplication
{
    protected override string Prompt => "rich> ";
    protected override IEnumerable<string> Completions => ["table", "chart", "panel", "quit"];

    protected override bool OnCommand(string command)
    {
        switch (command)
        {
            case "table":
                var t = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn("[blue]Name[/]")
                    .AddColumn("[blue]Score[/]");
                t.AddRow("Alice", "[green]98[/]");
                t.AddRow("Bob",   "[yellow]72[/]");
                Write(t);
                break;

            case "chart":
                Write(new BarChart()
                    .Label("[bold]Results[/]")
                    .AddItem("Pass", 75, Color.Green)
                    .AddItem("Fail", 25, Color.Red));
                break;

            case "panel":
                Write(new Panel("[bold]Hello from SSH![/]")
                {
                    Header = new PanelHeader("[blue]Greeting[/]"),
                    Border = BoxBorder.Rounded,
                });
                break;

            case "quit":
                return false;
        }
        return true;
    }
}
```

---

### Interactive prompts

```csharp
protected override bool OnCommand(string command)
{
    if (command == "survey")
    {
        var name    = Ask("Your [green]name[/]?");
        var age     = Ask<int>("Your [green]age[/]?");
        var lang    = Select("Favourite language?", ["C#", "F#", "VB.NET"]);
        var tools   = MultiSelect("Tools you use:", ["VS Code", "Rider", "Visual Studio"]);
        var confirm = Confirm($"Save survey for [blue]{Escape(name)}[/]?");

        if (confirm)
            WriteLine($"[green]Saved.[/] {Escape(name)}, {age}, {lang}");
        else
            WriteLine("[dim]Discarded.[/]");
    }
    return command != "quit";
}
```

---

### Per-user app routing with fallback menu

```csharp
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

await using var host = SshServerHost.CreateBuilder()
    .UseDefaultConfiguration(args)

    .MapUser<AdminApp>("admin")      // ssh admin@host  → AdminApp
    .MapUser<MonitorApp>("monitor")  // ssh monitor@host → MonitorApp

    .UseApplicationMenu(menu => menu
        .WithTitle("[bold]Choose an Application[/]")
        .Add<DemoApp>("Demo",    "Interactive demo")
        .Add<AdminApp>("Admin",  "Server administration")
        .Add<MonitorApp>("Monitor", "Live metrics")
        .SetDefaultForExec("Demo")
        .ReturnToMenuOnExit(true))

    .Build();

await host.RunAsync(cts.Token);
```

---

### Session lifecycle hooks

```csharp
public class TrackedApp : SshShellApplication
{
    private static int _activeCount = 0;
    private DateTime _connectedAt;

    protected override void OnConnect()
    {
        _connectedAt = DateTime.UtcNow;
        Interlocked.Increment(ref _activeCount);
    }

    protected override void OnWelcome()
    {
        WriteLine($"Welcome [blue]{Escape(Connection.Username)}[/]!");
        WriteLine($"[dim]Active sessions: {_activeCount}[/]");
    }

    protected override void OnDisconnect()
    {
        var duration = DateTime.UtcNow - _connectedAt;
        Interlocked.Decrement(ref _activeCount);
        // Log session duration, clean up resources, etc.
    }

    protected override bool OnCommand(string cmd) => cmd != "quit";
}
```

---

### Exec + interactive dual mode

```csharp
public class DualApp : SshShellApplication
{
    protected override string Prompt => "srv> ";

    // Handles: ssh host status
    protected override string? OnExec(string command) => command switch
    {
        "status"  => $"OK pid={Environment.ProcessId}\n",
        "version" => "2.0.0\n",
        _         => null   // fall through to interactive OnCommand
    };

    // Handles interactive mode
    protected override bool OnCommand(string command)
    {
        if (command == "status")  { ShowStatus(); return true; }
        if (command == "quit")    return false;
        WriteLine($"[red]Unknown:[/] {Escape(command)}");
        return true;
    }

    private void ShowStatus()
    {
        Write(new Panel($"[green]Running[/] — PID {Environment.ProcessId}")
        {
            Header = new PanelHeader("Status"),
            Border = BoxBorder.Rounded,
        });
    }
}
```

---

### Configuration via appsettings.json

```csharp
// Program.cs
await SshServerHost.CreateBuilder()
    .UseDefaultConfiguration(args)   // reads appsettings.json
    .UseMaxConnections(50)
    .UseApplication<MyApp>()
    .Build()
    .RunAsync();
```

```json
// appsettings.json
{
  "SshServer": {
    "Port": 2222,
    "AllowAnonymous": false,
    "AuthorizedKeysPath": "authorized_keys",
    "SessionTimeoutMinutes": 60,
    "MaxConnections": 50,
    "LogLevel": "Information"
  }
}
```

```bash
# Override at runtime without editing appsettings.json
SSHSERVER_PORT=2223 dotnet run
dotnet run -- --SshServer:Port 2223
```

---

## Spectre.Console Markup Quick Reference

Use within any `WriteLine`, `Write`, `Ask`, `Select`, or prompt title string.

| Syntax | Effect |
|--------|--------|
| `[bold]text[/]` | Bold |
| `[italic]text[/]` | Italic |
| `[red]text[/]` | Red foreground |
| `[green on black]text[/]` | Green on black background |
| `[dim]text[/]` | Dimmed/subtle |
| `[blue link=https://example.com]text[/]` | Hyperlink (terminal permitting) |
| `[/]` | Close last open style tag |

**Always call `Escape(userInput)` before embedding user-supplied text in a markup string** to prevent markup injection:

```csharp
// Safe:
WriteLine($"Hello, {Escape(Connection.Username)}!");

// Unsafe — username containing '[' can break rendering:
WriteLine($"Hello, {Connection.Username}!");
```

---

*For the project source, issue tracker, and contribution guide, see [github.com/AlfredBr/DotNetSshServer](https://github.com/AlfredBr/DotNetSshServer).*
