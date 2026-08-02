# Ignyos LAN Portal

Blazor + ASP.NET Core solution for LAN-only file upload and download.

Runtime configuration is persisted in a local SQLite database (`Bootstrap:DatabasePath`).

## Projects

- `Ignyos.LanPortal.Web`: Blazor Server frontend
- `Ignyos.LanPortal.Api`: file API that reads and writes to local storage
- `Ignyos.LanPortal.Contracts`: shared DTOs

## Local Development

1. Start the API:

```powershell
cd Ignyos.LanPortal.Api
dotnet run
```

2. Start the web app in a second terminal:

```powershell
cd Ignyos.LanPortal.Web
dotnet run
```

3. Open the web app URL printed by `dotnet run` and navigate to `/files`.

4. On first run, configure storage root from Machine A:

```
http://localhost:5212/local/setup
```

## Production Layout (Machine A)

- API listens on `http://127.0.0.1:5212`
- Web listens on `http://127.0.0.1:5000`
- Caddy listens on LAN interface and routes:
  - `/api/*` -> API
  - everything else -> Web

Use this `Caddyfile` from repository root.

## Internal DNS (Split-Horizon)

For LAN-only access to `nas.ignyos.com`:

1. Add a local DNS A record `nas.ignyos.com -> <machine-a-lan-ip>`.
2. Ensure Wi-Fi clients use that DNS via DHCP.
3. Do not expose these ports on your internet router unless you explicitly want remote access.

## SQLite Settings

- JWT settings (`Issuer`, `Audience`, `SigningKey`) are stored in SQLite.
- `SigningKey` is generated automatically if missing.
- `Storage:RootPath` is configured via `http://localhost:5212/local/setup`.

The database file location is configured in `Ignyos.LanPortal.Api/appsettings*.json` under `Bootstrap:DatabasePath`.

## Developer Installer (QA Transfer)

Build transfer-ready developer artifacts:

```powershell
./scripts/build-dev-installer.ps1 -Configuration Release -Runtime win-x64 -Version 0.1.0-dev
```

Generated artifacts:

- `artifacts/dev-installer/package/Ignyos-LanPortal-QA-<version>.zip`
- `artifacts/dev-installer/package/Ignyos-LanPortal-QA-<version>.zip.sha256`
- `artifacts/dev-installer/installer/Ignyos-LanPortal-Dev-<version>.exe` (if Inno Setup is installed)

See `.github/runbooks/developer-installer.md` for full steps.

## Release Publish Workflow

Release notes and release automation are intentionally stored under `.github` so `docs/` can be reserved for the future GitHub Pages SPA.

Stage 6 naming decision:

- Canonical operator lane names are `Build`, `Publish-dev`, and `Publish-live`.
- `scripts/publish-live.ps1` is the canonical implementation engine.

### Release Files

- Style guide: `.github/release/release-notes-style-guide.md`
- AI output target: `.github/release/release-notes.md`
- Publish-live script: `scripts/publish-live.ps1`
- Publish-dev script: `scripts/publish-dev.ps1`
- Underlying implementation engine: `scripts/publish-live.ps1`

Canonical release notes source of truth for LAN Portal is always `.github/release/*`.
Files under `DevOps_CICD_EXAMPLE` are template references only.

### Run and Debug

Use these VS Code **Run and Debug** configurations:

- **Build** (local build validation)
- **Publish-dev** (dry-run release preparation)
- **Publish-live** (full release preparation and publish flow)

`Publish-live` runs:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/publish-live.ps1
```

`Publish-dev` runs:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/publish-dev.ps1
```

### What Publish Does

1. Verifies current branch matches the target publish branch (defaults to the current branch unless `-MainBranch` is provided).
2. Verifies working tree is clean.
3. Verifies local branch head matches `origin/<target-branch>`.
4. Reads current version from `Ignyos.LanPortal.Host/Ignyos.LanPortal.Host.csproj` (`<Version>` is the source of truth).
5. Prompts for publish version (defaults to current patch + 1).
6. Creates diff artifacts from the latest release tag (or root commit if no tag exists).
7. Clears `.github/release/release-notes.md`.
8. Generates an AI prompt and copies it to clipboard.
9. Waits for user confirmation after AI writes release notes.
10. Validates notes are present, shows preview, then asks final approval.
11. Commits release notes + version bump, tags commit, pushes branch and tag.

### Dry Run

To execute all pre-publish checks and artifact generation without commit/tag/push:

```powershell
./scripts/publish-dev.ps1
```
