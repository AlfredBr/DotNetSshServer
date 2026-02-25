# .NET SSH Server

[![NuGet](https://img.shields.io/nuget/v/AlfredBr.SshServer.Core.svg)](https://www.nuget.org/packages/AlfredBr.SshServer.Core)
[![NuGet Downloads](https://img.shields.io/nuget/dt/AlfredBr.SshServer.Core.svg)](https://www.nuget.org/packages/AlfredBr.SshServer.Core)
[![Build](https://github.com/AlfredBr/DotNetSshServer/actions/workflows/pack-validation.yml/badge.svg)](https://github.com/AlfredBr/DotNetSshServer/actions/workflows/pack-validation.yml)

A lightweight SSH server library in C# (.NET 10) that allows terminal clients to connect
via SSH and interact with a TUI application. Inspired by charmbracelet's [wish](https://github.com/charmbracelet/wish) package in Go.

## Quick Start

### Install the Package

```bash
dotnet add package AlfredBr.SshServer.Core
```

[View on NuGet: AlfredBr.SshServer.Core](https://www.nuget.org/packages/AlfredBr.SshServer.Core) — see [PUBLISHING.md](PUBLISHING.md) for release instructions.

### Create Your Application

```csharp
using AlfredBr.SshServer.Core;

public class MyApp : SshShellApplication
{
    protected override string Prompt => "myapp> ";

    protected override IEnumerable<string> Completions => ["help", "quit"];

    protected override bool OnCommand(string command)
    {
        if (command == "quit") return false;
        WriteLine($"You said: {Escape(command)}");
        return true;
    }
}
```

### Run the Server

```csharp
await SshServerHost.CreateBuilder()
    .UsePort(2222)
    .AllowAnonymous()
    .UseApplication<MyApp>()
    .Build()
    .RunAsync();
```

### Connect

```bash
ssh -p 2222 localhost
```

See [DEVELOPERS.md](DEVELOPERS.md) for the full guide.

## Project Structure

| Project | Description |
|---------|-------------|
| **AlfredBr.SshServer.Core** | The SSH server library — includes SSH protocol, TUI infrastructure, and builder API. |
| **SshServer.Demo** | Demo application showcasing Spectre.Console features. |

### Key Classes

| Class | Description |
|-------|-------------|
| `SshServerHost` | Main server host with fluent builder API. |
| `SshServerBuilder` | Fluent builder for configuring the server. |
| `SshShellApplication` | Abstract base class for SSH applications. |

## Builder API

```csharp
await SshServerHost.CreateBuilder()
    .UsePort(2222)                              // TCP port (default: 2222)
    .UseBanner("SSH-2.0-MyApp")                 // SSH protocol banner
    .UseHostKeyPath("mykey.pem")                // Host key file path
    .AllowAnonymous()                           // Enable anonymous auth
    .UseAuthorizedKeysFile("authorized_keys")   // Public key auth file
    .UseSessionTimeout(TimeSpan.FromMinutes(30)) // Idle timeout
    .UseMaxConnections(100)                     // Connection limit
    .UseLogLevel(LogLevel.Information)          // Minimum log level
    .UseApplication<MyApp>()                    // Your application class
    .ConfigureLogging(builder => { ... })       // Custom logging config
    .UseDefaultConfiguration(args)              // Load from appsettings.json
    .Build()
    .RunAsync();
```

## Multi-Application Support

Host multiple applications with username-based routing or an interactive menu:

```csharp
await SshServerHost.CreateBuilder()
    // Map usernames directly to apps
    .MapUser<DemoApp>("demo")       // ssh demo@host → DemoApp
    .MapUser<AdminApp>("admin")     // ssh admin@host → AdminApp

    // Unmapped users see a selection menu
    .UseApplicationMenu(menu => menu
        .Add<DemoApp>("Demo", "Spectre.Console showcase")
        .Add<AdminApp>("Admin", "Server administration")
        .Add<MonitoringApp>("Monitor", "Live metrics dashboard")
        .SetDefaultForExec("Demo")
        .ReturnToMenuOnExit(true))

    .Build()
    .RunAsync();
```

**Usage:**
```bash
ssh demo@localhost -p 2222       # Direct to DemoApp
ssh admin@localhost -p 2222      # Direct to AdminApp
ssh localhost -p 2222            # Shows app selection menu
ssh guest@localhost "admin:logs" # Exec 'logs' in AdminApp
```

## Features

### Authentication
- **Public key authentication** — supports ssh-rsa, ssh-ed25519, ecdsa-sha2-nistp256/384/521
- **Anonymous access** — configurable via `AllowAnonymous()` builder method
- **Authorized keys** — standard OpenSSH `authorized_keys` file format

### Host Keys
- **ECDSA** — auto-generated nistp256 key on first run
- **Ed25519** — client key verification (via NSec.Cryptography)
- **RSA** — sha2-256/512 support

### Terminal Emulation
- **PTY support** — full pseudo-terminal with resize handling
- **Emacs-style line editing**:
  - Ctrl-A/E: beginning/end of line
  - Ctrl-B/F: back/forward character
  - Ctrl-P/N: previous/next history
  - Ctrl-D: delete char or disconnect
  - Ctrl-K/U: kill to end/beginning
  - Ctrl-Y: yank (paste) from kill ring
  - Ctrl-L: clear screen
  - Alt-B/F: back/forward word
  - Alt-D: delete word forward
  - Home/End, Delete, Arrow keys
- **Tab completion** — command name auto-completion
- **Command history** — per-connection with navigation

### TUI Integration (Spectre.Console)
- **Rich output** — tables, panels, rules, trees, bar charts
- **Interactive prompts** — selection, multi-select, confirmation, text input
- **Live displays** — progress bars, spinners, live-updating tables

## Run the Demo

```bash
dotnet run --project src/SshServer.Demo/SshServer.Demo.csproj
ssh -p 2222 localhost
```

### Demo Commands
| Command | Description |
|---------|-------------|
| `help` | Show available commands |
| `status` | Server status with process ID |
| `whoami` | Connection info, auth method, key fingerprint |
| `config` | Server configuration (hostname, IPs, settings) |
| `clear` | Clear the screen |
| `menu` | Interactive menu selection |
| `select` | Choose from a list |
| `multi` | Multi-select from a list |
| `confirm` | Yes/No confirmation |
| `ask` | Text input with validation |
| `demo` | Run all interactive demos |
| `progress` | Progress bar demo |
| `spinner` | Status spinner demo |
| `live` | Live-updating table demo |
| `tree` | Hierarchical tree display |
| `chart` | Bar chart visualization |
| `quit` | Disconnect |

### With Public Key Authentication
1. Add your public key to `authorized_keys`:
   ```bash
   cat ~/.ssh/id_ed25519.pub >> src/SshServer.Demo/authorized_keys
   ```
2. Set `AllowAnonymous` to `false` in `appsettings.json`
3. Connect: `ssh -p 2222 localhost`

### Scripted Commands (Exec Channel)
```bash
# Run a single command without entering interactive mode
ssh -p 2222 localhost status
ssh -p 2222 localhost whoami
ssh -p 2222 localhost config

# Use in scripts
result=$(ssh -p 2222 localhost status)
echo "Server says: $result"
```

## Configuration

Settings can be configured via `appsettings.json`:

```json
{
  "SshServer": {
    "Port": 2222,
    "Banner": "SSH-2.0-SshServer",
    "HostKeyPath": "hostkey_ecdsa_nistp256.pem",
    "MaxConnections": 100,
    "LogLevel": "Debug",
    "AllowAnonymous": true,
    "AuthorizedKeysPath": "./authorized_keys",
    "SessionTimeoutMinutes": 0
  }
}
```

Load configuration in your app:
```csharp
await SshServerHost.CreateBuilder()
    .UseDefaultConfiguration(args)  // Loads appsettings.json + env vars + CLI args
    .UseApplication<MyApp>()
    .Build()
    .RunAsync();
```

| Setting | Description |
|---------|-------------|
| `Port` | TCP port to listen on (default: 2222) |
| `Banner` | SSH protocol banner |
| `HostKeyPath` | Path to host key PEM file |
| `MaxConnections` | Max concurrent connections (0 = unlimited) |
| `LogLevel` | Minimum log level (Trace, Debug, Information, Warning, Error) |
| `AllowAnonymous` | Allow connections without authentication |
| `AuthorizedKeysPath` | Path to OpenSSH authorized_keys file |
| `SessionTimeoutMinutes` | Idle timeout in minutes (0 = disabled) |

Override via environment variables (`SSHSERVER_` prefix) or command-line arguments.

## Architecture

```
┌─────────────────────────────┐
│  Your Application           │  extends SshShellApplication
├─────────────────────────────┤
│  SshServerHost + Builder    │  Fluent API, lifecycle management
├─────────────────────────────┤
│  SshShellApplication        │  Base class with helpers
├─────────────────────────────┤
│  TUI Infrastructure         │  Spectre.Console, LineEditor
├─────────────────────────────┤
│  SSH Server Library         │  FxSsh (modernised)
├─────────────────────────────┤
│  TCP Listener               │  System.Net.Sockets
└─────────────────────────────┘
```

## Dependencies

| Package | Purpose |
|---------|---------|
| [FxSsh](https://github.com/Aimeast/FxSsh) | Core SSH server (vendored source) |
| [Microsoft.Extensions.Hosting](https://github.com/dotnet/runtime) | Configuration and DI |
| [Microsoft.Extensions.Logging](https://github.com/dotnet/runtime) | Structured logging |
| [Spectre.Console](https://github.com/spectreconsole/spectre.console) | TUI rendering and prompts |
| [NSec.Cryptography](https://github.com/ektrah/nsec) | Ed25519 key support |

## Project Status

### Completed
- [x] SSH transport (key exchange, encryption, MAC)
- [x] Anonymous and public key authentication
- [x] PTY requests and window resize
- [x] Emacs-style line editing with history
- [x] Tab completion
- [x] Spectre.Console integration (rendering + interactive prompts)
- [x] Configuration via appsettings.json
- [x] Graceful shutdown (Ctrl+C)
- [x] Structured logging with connection IDs
- [x] Ed25519 client key support
- [x] Session timeout
- [x] SshShellApplication base class for easy app development
- [x] Fluent builder API
- [x] NuGet package structure

### Roadmap
- [ ] Password authentication
- [ ] Rate limiting / connection limits
- [ ] Unit tests

## Key RFCs

| RFC | Topic |
|-----|-------|
| 4251 | SSH Protocol Architecture |
| 4252 | SSH Authentication Protocol |
| 4253 | SSH Transport Layer Protocol |
| 4254 | SSH Connection Protocol (channels, PTY) |
| 5656 | ECDH key exchange |
| 8032 | Ed25519 signing |

## Acknowledgements

This project builds upon the work of several excellent open source projects:

| Project | Author | Site | License | Role |
|---------|--------|---------|------|------|
| [FxSsh](https://github.com/Aimeast/FxSsh) | Aimeast | | MIT | Core SSH protocol implementation (vendored and extended) |
| [Spectre.Console](https://github.com/spectreconsole/spectre.console) | Patrik Svensson et al. | [https://spectreconsole.net/](https://spectreconsole.net/) | MIT | TUI rendering, interactive prompts, and live displays |
| [NSec.Cryptography](https://www.nuget.org/packages/NSec.Cryptography) | Nicholas Walther | [nsec.rocks](https://nsec.rocks) | MIT | Ed25519 client key verification |
| [Microsoft.Extensions.*](https://github.com/dotnet/extensions) | Microsoft / .NET Foundation | | MIT | Hosting, configuration, dependency injection, and logging |
| [charmbracelet/wish](https://github.com/charmbracelet/wish) | Charmbracelet | https://charm.land/ | MIT | Original inspiration — SSH app framework for Go |

## License

[MIT](LICENSE) — Copyright (c) 2026 Alfred Broderick


