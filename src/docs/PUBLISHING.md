# Publishing AlfredBr.SshServer.Core to NuGet

## Build and pack locally

```bash
dotnet restore
dotnet pack src/SshServer.Core/SshServer.Core.csproj -c Release -o artifacts/nuget
```

Expected output:
- `artifacts/nuget/AlfredBr.SshServer.Core.<version>.nupkg`
- `artifacts/nuget/AlfredBr.SshServer.Core.<version>.snupkg`

## Validate locally (optional)

```bash
dotnet new console -n SmokeTest
cd SmokeTest
dotnet add package AlfredBr.SshServer.Core
dotnet build
```

## Publish via GitHub Actions (recommended)

[publish-nuget.yml](.github/workflows/publish-nuget.yml) publishes on:
- Git tag push matching `v*` (e.g. `v1.2.0`)
- Manual run via **workflow_dispatch** in the Actions tab

### One-time setup

In GitHub repo settings, add a secret named `NUGET_API_KEY` (from nuget.org → Account → API Keys).

### Release process

1. Update `<Version>` in `src/SshServer.Core/SshServer.Core.csproj`
2. Commit and push
3. Tag and push:

```bash
git tag v1.2.0
git push origin v1.2.0
```

The workflow packs and pushes both `.nupkg` and `.snupkg` with `--skip-duplicate`.

## CI package validation

[pack-validation.yml](.github/workflows/pack-validation.yml) runs on every pull request and push to `main`. It validates restore, build, and pack, then uploads the artifacts for inspection.

## NuGet package page

https://www.nuget.org/packages/AlfredBr.SshServer.Core
