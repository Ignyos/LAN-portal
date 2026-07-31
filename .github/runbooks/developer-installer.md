# Developer Installer Flow

This project supports a developer/QA installer flow that produces transfer-ready artifacts.

## Prerequisites

- .NET SDK 9.x
- Optional: Inno Setup compiler (`iscc.exe`) for `.exe` installer output

## Build Artifacts

Run from repository root:

```powershell
./scripts/build-dev-installer.ps1 -Configuration Release -Runtime win-x64 -Version 0.1.0-dev
```

Output is generated under:

- `artifacts/dev-installer/package/Ignyos-LanPortal-QA-<version>.zip`
- `artifacts/dev-installer/package/Ignyos-LanPortal-QA-<version>.zip.sha256`
- `artifacts/dev-installer/installer/Ignyos-LanPortal-Dev-<version>.exe` (if Inno Setup is installed)

## Transfer to QA Machine

1. Copy the zip or installer to QA machine.
2. If using zip: extract and run `Launch-LanPortal.ps1`.
3. The browser opens to `http://localhost:5212/local/setup`.
4. Save the storage root path, then continue to the admin console.
5. Guests on the same Wi-Fi can scan the setup-page QR code to open login immediately.
6. Default guest access is the host LAN IP login URL (for example `http://192.168.1.240/login`) shown in setup/admin.
7. Optional customization: configure router DNS so `http://lan.home.arpa/login` resolves to the host machine LAN IP.

## Notes

- JWT signing and active session tracking are persisted in SQLite at runtime.
- Developer installer binaries are self-contained to reduce runtime dependencies on QA hosts.
- The web app listens on port 80 and the API listens on port 5212 in the packaged launcher flow.
- Editing the host machine hosts file does not configure guest devices; use router DNS for friendly-name access across the network.
