# .NET SSH Server

A lightweight SSH server library in C# (.NET 10) that allows terminal clients to connect
via SSH and interact with a TUI application. Inspired by charmbracelet's [wish](https://github.com/charmbracelet/wish) package in Go.

## Project Structure

| Project | Description |
|---------|-------------|
| **SshServer.Core** | The SSH server library — handles connections, authentication, PTY, and channels. Use this as a dependency for your own SSH-based applications. |
| **SshServer.Host** | A demo application showcasing the library's capabilities. Use this as a starting point for building your own SSH TUI programs. |

The demo application (`SshServer.Host`) demonstrates:
- Spectre.Console integration for rich terminal output
- Interactive prompts and live displays
- Line editing with history and tab completion
- Public key and anonymous authentication

To build your own SSH TUI application, copy `SshServer.Host` as a template and replace the `CommandHandler` with your own application logic.

## Features

### Authentication
- **Public key authentication** — supports ssh-rsa, ssh-ed25519, ecdsa-sha2-nistp256/384/521
- **Anonymous access** — configurable via `AllowAnonymous` setting
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

### Shell Commands
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

## Quick Start

### Run the Server
```bash
dotnet run --project src/SshServer.Host/SshServer.Host.csproj
```

### Connect
```bash
ssh -p 2222 localhost
```

### With Public Key Authentication
1. Add your public key to `authorized_keys`:
   ```bash
   cat ~/.ssh/id_ed25519.pub >> src/SshServer.Host/authorized_keys
   ```
2. Set `AllowAnonymous` to `false` in `appsettings.json`
3. Connect: `ssh -p 2222 localhost`

## Configuration

Settings in `appsettings.json`:

```json
{
  "SshServer": {
    "Port": 2222,
    "Banner": "SSH-2.0-SshServer",
    "HostKeyPath": "hostkey_ecdsa_nistp256.pem",
    "MaxConnections": 100,
    "LogLevel": "Debug",
    "AllowAnonymous": true,
    "AuthorizedKeysPath": "./authorized_keys"
  }
}
```

Override via environment variables (`SSHSERVER_` prefix) or command-line arguments.

## Architecture

```
┌─────────────────────────────┐
│  TUI Application Layer      │  Spectre.Console
├─────────────────────────────┤
│  SSH Shell/PTY Glue         │  LineEditor, CommandHandler
├─────────────────────────────┤
│  SSH Server Library         │  FxSsh (modernised)
├─────────────────────────────┤
│  TCP Listener               │  System.Net.Sockets
└─────────────────────────────┘
```

## Dependencies

| Package | Purpose |
|---------|---------|
| `FxSsh` | Core SSH server (vendored source) |
| `Microsoft.Extensions.Hosting` | Configuration and DI |
| `Microsoft.Extensions.Logging` | Structured logging |
| `Spectre.Console` | TUI rendering and prompts |
| `NSec.Cryptography` | Ed25519 key support |

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

### Roadmap
- [ ] Password authentication
- [ ] Rate limiting / connection limits
- [ ] Unit tests
- [ ] NuGet package

## Key RFCs

| RFC | Topic |
|-----|-------|
| 4251 | SSH Protocol Architecture |
| 4252 | SSH Authentication Protocol |
| 4253 | SSH Transport Layer Protocol |
| 4254 | SSH Connection Protocol (channels, PTY) |
| 5656 | ECDH key exchange |
| 8032 | Ed25519 signing |

## License

MIT
