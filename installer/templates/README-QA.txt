Ignyos LAN Portal - Developer QA Package

Contents:
- api\  (published API binaries)
- web\  (published Web binaries)
- host\ (embedded admin/setup shell)
- Launch-LanPortal.ps1
- Open-LanPortal-Admin.cmd

Default local URLs:
- First-run setup: http://localhost:5212/local/setup
- Admin console:   http://localhost:5212/local/admin
- Guest login:     http://<host-lan-ip>/login (default)
- Optional custom: http://lan.home.arpa/login

LAN behavior:
- The launcher binds Web on port 80 and API on port 5212 for same-Wi-Fi access.
- The setup page shows a QR code that guests can scan for immediate login access.
- Router DNS is optional and only needed for custom URL: map lan.home.arpa to this host for all guests.
- If DNS is not configured yet, guests can use the fallback LAN-IP URL shown on setup.

Notes:
- The installer opens the embedded host app (WebView2) for setup/admin when available.
- If host app launch is unavailable, the launcher falls back to opening setup in the default browser.
- Use the launcher shortcut to start the portal again after closing it.
- JWT + access session data are persisted in SQLite using app bootstrap settings.
