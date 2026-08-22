# Inno Setup Plan

## Goals

- Install API and Web binaries on Machine A.
- Initialize local data directory for SQLite settings DB.
- Install and register Windows service(s) or startup tasks.
- Preserve SQLite DB across app updates.

## Proposed Paths

- App binaries: `C:\Program Files\Ignyos\LanPortal`
- App data: `C:\ProgramData\Ignyos\LanPortal`
- SQLite DB: `C:\ProgramData\Ignyos\LanPortal\lanportal.db`

## Install Sequence

1. Copy binaries.
2. Create app data folder with writable permissions for service identity.
3. Write bootstrap appsettings with database path pointing to ProgramData.
4. Register service(s).
5. Start service(s).

## Upgrade Sequence

1. Stop service(s).
2. Replace binaries.
3. Run lightweight migration/upgrade check.
4. Restart service(s).

## First-Run Experience

- Installer launches the portal silently in the background.
- Browser opens automatically to `http://localhost:5212/local/setup`.
- User sets the storage root path and continues to the admin console.
- Guests on the same Wi-Fi scan QR from setup page for immediate login access.
- Default guest URL is the host LAN IP root (shown in setup/admin and QR).
- Router DNS can optionally map `lan.home.arpa` to host LAN IP for a custom root URL (`http://lan.home.arpa/`).

## Security Notes

- JWT signing key is generated and stored in SQLite automatically.
- Sensitive SQLite values are encrypted with DPAPI on Windows.
- Installer should never embed static signing keys.
