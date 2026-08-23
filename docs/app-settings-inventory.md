# Application Settings Inventory

Purpose: establish the initial list of values that belong in the SQLite `AppSettings` table and can later be managed through one settings abstraction and Admin settings page.

## Ownership

All access to the `AppSettings` table should be owned by `IAppSettingsStore`, implemented by `SqliteAppSettingsStore`. Controllers and other services should use typed settings methods or a typed settings service rather than issuing SQL or duplicating key names.

The settings inventory is intentionally separate from session data. `AccessSessions`, `SessionRefreshTokens`, and `RoleChangeAudits` are operational records, not application settings.

## Settings To Store In AppSettings

| Key | Friendly name | Type | Sensitive | Default/current source | Runtime editable | Notes |
|---|---|---:|:---:|---|:---:|---|
| `Jwt:Issuer` | Token issuer | string | No | `Ignyos.LanPortal` | No | Used when creating and validating access tokens. Keep stable after deployment. |
| `Jwt:Audience` | Token audience | string | No | `Ignyos.LanPortal.Clients` | No | Used when creating and validating access tokens. Changing it invalidates existing tokens. |
| `Jwt:SigningKey` | Token signing key | protected string | Yes | Generated during first initialization | No | Must remain protected and should not be exposed through a settings UI. Changing it invalidates existing tokens. |
| `Storage:RootPath` | Shared folder | string | No | Empty until setup | Yes | Physical host path used as the shared-folder boundary. Must retain strict validation. |
| `DeviceLogin:RequestLifetimeSeconds` | Access request timeout | integer | No | `300` seconds (5 minutes) | Candidate | Controls how long a pending client access request remains available. Requires validation and should be displayed in friendly units. |
| `DeviceLogin:PollIntervalSeconds` | Access request check interval | integer | No | `3` seconds | Candidate | Controls how often the client checks a pending request. May remain startup configuration if host control is not needed. |

## Strong Candidates For Later Addition

These are not currently implemented settings. They should be added only when the behavior exists and has a clear owner.

| Proposed key | Friendly name | Type | Notes |
|---|---|---:|---|
| `FileSharing:DisplayName` | Shared folder name | string | Friendly name shown to users; distinct from the physical path. |
| `FileSharing:AllowDownloads` | Allow downloads | boolean | Global policy only if a future product decision requires it. Per-user permissions should not be replaced by this flag. |
| `FileSharing:AllowUploads` | Allow uploads | boolean | Global policy only if a future product decision requires it. |
| `FileSharing:MaximumUploadSizeBytes` | Maximum file size | long | Current upload limit is code-defined; move it here only with validation and clear units in the UI. |
| `FileSharing:EnableSearch` | Search enabled | boolean | Useful if search becomes optional by deployment. It should not replace permission checks. |
| `Search:Provider` | Search provider | string | Possible values such as `filesystem` or `everything`; should be introduced with the provider abstraction. |
| `Search:EverythingEnabled` | Use Everything when available | boolean | Only after Everything integration exists. |
| `Updates:Channel` | Update channel | string | Current update-channel configuration is in `appsettings.json`; consider runtime storage only if an Admin should change it. |
| `Updates:PollIntervalMinutes` | Update check interval | integer | Same runtime/startup decision as the update channel. |
| `Hosting:UseHttpsRedirection` | Use secure web connections | boolean | Startup/pipeline behavior; keep outside `AppSettings` unless a runtime settings workflow can safely restart or reconfigure the host. |

## Settings That Should Remain Startup Configuration

Not every configurable value belongs in the database. Keep these in `appsettings.json`, environment variables, command-line arguments, or deployment configuration unless a future runtime reconfiguration design exists:

- `Bootstrap:DatabasePath` because it is needed to locate the database before the database can be opened.
- Logging levels because they are deployment diagnostics and already follow standard .NET configuration conventions.
- API base URLs in the Client because they identify the service being connected to.
- Hosting pipeline settings such as HTTPS redirection when changing them requires application restart or process-level configuration.
- Installer and deployment settings.

## Request Timeout Migration Note

The current `DeviceLogin:RequestLifetimeSeconds` value is still consumed through `IOptions<DeviceLoginOptions>` by `InMemoryDeviceLoginStore`. Moving it into `AppSettings` will require a deliberate migration:

1. Add typed get/set methods to `IAppSettingsStore`.
2. Seed the value into `AppSettings` when missing, using `300` seconds.
3. Make the login store read the runtime value from the settings store.
4. Validate a safe range, such as 5 seconds through 24 hours.
5. Decide whether `PollIntervalSeconds` moves with it or remains startup configuration.
6. Remove the duplicate `DeviceLogin` options binding after all consumers migrate.
7. Add tests for defaulting, validation, and runtime changes.

## Future Settings Page Direction

The future settings page should consume a typed application-settings facade rather than exposing raw database keys:

```text
Settings page
    |
    v
Typed application settings service
    |
    v
IAppSettingsStore
    |
    v
SQLite AppSettings table
```

The UI should group settings by user-facing concern:

- File sharing
- Access requests
- Search
- Updates
- Advanced/deployment

Sensitive and startup-only values should not appear as ordinary editable fields. Settings that require restart should be labeled accordingly or excluded from the runtime settings page.

## Open Decisions

- Whether Request Timeout and Poll Interval should both become runtime settings.
- Whether a five-minute timeout should be stored as seconds internally or represented as minutes in the Admin UI.
- Which Admin role/permission can change runtime settings.
- Whether changing security-sensitive values should require a restart or invalidate sessions.
- Whether settings need audit history, especially security and access-request settings.
- Whether add-ins receive their own settings namespace instead of using core `AppSettings` keys.
- Whether settings values should remain string-based at the storage boundary while typed validation lives above it.
