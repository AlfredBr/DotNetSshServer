# .NET SSH Server

A lightweight SSH server library in C# (.NET 10) that allows terminal clients to connect
via SSH and interact with a TUI application. Inspired by charmbracelet's [wish](https://github.com/charmbracelet/wish) package in Go.

## Quick Start

### Install the Package

```bash
dotnet add package SshServer
```

### Create Your Application

```csharp
using SshServer;

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
| **SshServer** | The SSH server library — includes SSH protocol, TUI infrastructure, and builder API. |
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

## Package and Publish (NuGet)

This repository currently publishes the **SshServer** package from `src/SshServer.Core`.

### 1) Build and pack

```bash
dotnet restore
dotnet pack src/SshServer.Core/SshServer.Core.csproj -c Release -o artifacts/nuget
```

Expected output:
- `artifacts/nuget/SshServer.<version>.nupkg`
- `artifacts/nuget/SshServer.<version>.snupkg`

### 2) Validate package contents locally (optional)

```bash
dotnet new console -n SshServer.PackageSmokeTest
cd SshServer.PackageSmokeTest
dotnet add package SshServer --version <version>
dotnet build
```

### 3) Publish to nuget.org

Create an API key at <https://www.nuget.org/account/apikeys>, then run:

```bash
dotnet nuget push artifacts/nuget/SshServer.<version>.nupkg \
    --api-key <NUGET_API_KEY> \
    --source https://api.nuget.org/v3/index.json

dotnet nuget push artifacts/nuget/SshServer.<version>.snupkg \
    --api-key <NUGET_API_KEY> \
    --source https://api.nuget.org/v3/index.json
```

### 4) Publish via GitHub Actions (recommended)

This repository includes [publish-nuget.yml](.github/workflows/publish-nuget.yml), which publishes on:
- Git tag push matching `v*` (for example `v1.0.1`)
- Manual run via **workflow_dispatch**

Setup steps:
1. In GitHub repo settings, add secret `NUGET_API_KEY`.
2. Update `<Version>` in `src/SshServer.Core/SshServer.Core.csproj`.
3. Create and push a tag:

```bash
git tag v1.0.1
git push origin v1.0.1
```

The workflow packs `SshServer.Core` and pushes both `.nupkg` and `.snupkg` with `--skip-duplicate`.

### CI package validation

This repository also includes [pack-validation.yml](.github/workflows/pack-validation.yml), which runs on pull requests and pushes to `main`.

It validates package readiness by running restore, build, and pack steps for `SshServer.Core`, then uploads the generated package artifacts for inspection.

### Versioning note

Update `<Version>` in `src/SshServer.Core/SshServer.Core.csproj` before packing.

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

MIT


