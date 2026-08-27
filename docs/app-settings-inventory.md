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
| `DeviceLogin:RequestLifetimeSeconds` | Access request timeout | integer | No | `300` seconds (5 minutes) | Yes | Stored in AppSettings and read at request creation time; display as minutes/seconds in the Host Settings UI. |
| `DeviceLogin:PollIntervalSeconds` | Access request check interval | integer | No | `3` seconds | Yes | Stored in AppSettings and read when a request is created; display as seconds in the Host Settings UI. |
| `AccessHistory:RetentionDays` | Access history retention | integer | No | `365` days (1 year) | Yes | Now seeded and read from AppSettings; valid range is 7 through 3650 days and Never is not offered. |

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

## Completed Settings Migrations

The following settings have been migrated from startup configuration to SQLite `AppSettings`:

### AccessHistory Retention
- Key: `AccessHistory:RetentionDays`
- Storage: integer seconds
- Default: 365 days
- Validation: 7 days minimum, 3650 days maximum
- Consumer: `AccessHistoryMaintenanceService`

### Request Timeout and Poll Interval
- Keys: `DeviceLogin:RequestLifetimeSeconds` and `DeviceLogin:PollIntervalSeconds`
- Storage: integer seconds
- Defaults: 300 seconds (Request Timeout) and 3 seconds (Poll Interval)
- Validation: 5-86400 seconds (Request Timeout) and 1-60 seconds (Poll Interval)
- Consumer: `InMemoryDeviceLoginStore` reads through `IAppSettingsStore`
- Removed: `DeviceLoginOptions` class and `DeviceLogin` configuration binding

Future Host Settings UI will display Request Timeout as minutes/seconds and Poll Interval as seconds while storage remains in seconds at the boundary.

## Future Typed Settings Architecture

When Host Settings adds the ability to edit these values at runtime, the following pattern should be followed. The database boundary remains string-based while each setting owns its key, type conversion, fallback, validation, and display rules.

```text
Host Settings UI
        |
        v
Typed setting definition (RequestTimeoutSetting, PollIntervalSetting, etc.)
        |
        v
Typed application settings facade
        |
        v
IAppSettingsStore / SqliteAppSettingsStore
        |
        v
SQLite AppSettings string value
```

The facade should validate before writing and return the setting's default when a value is missing or invalid. Invalid stored values should also be logged so a damaged configuration is visible without preventing the application from starting where a safe fallback exists.

Example setting class:

```csharp
public class RequestTimeoutSetting
{
    public string Key => "DeviceLogin:RequestLifetimeSeconds";
    public int DefaultValue => 300;
    public int Deserialize(string? value) => int.TryParse(value, out var seconds) 
        ? Math.Clamp(seconds, 5, 24 * 60 * 60) : DefaultValue;
    public string Serialize(int value) => Math.Clamp(value, 5, 24 * 60 * 60).ToString();
}
```

## Audit History And Logging

Audit history and operational logging should be designed together before runtime settings are implemented.

Recommended separation:

- Audit history records who changed a setting, which key changed, the old and new values when safe, when it changed, and why.
- Application logs record execution details such as validation failures, fallback use, database errors, and invalid configuration attempts.
- Sensitive values such as `Jwt:SigningKey` must never appear in audit records or logs. Record that the value changed, optionally with a redacted fingerprint, not the value itself.
- Audit records should be durable in SQLite; diagnostic logs should continue using `ILogger` and the configured logging providers.

Proposed audit record:

```text
ApplicationSettingAudit
    AuditId
    Key
    OldValueRedacted
    NewValueRedacted
    ChangedBy
    Reason
    ChangedAtUtc
    RequiresRestart
```

The audit writer should be part of the settings service/store transaction so a successful setting change cannot be committed without its audit record. Logging should happen around the operation as structured events, without duplicating the audit record as the only source of diagnostics.

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

- Final design for the audit transaction boundary and audit retention/query UI.
- Which application log events and structured fields accompany settings reads, writes, validation failures, and fallback use.
- Confirm the operational behavior for JWT setting changes: invalidate all sessions when any `Jwt:*` value changes.
- Whether add-ins receive their own settings namespace instead of using core `AppSettings` keys.
- Whether add-in settings should use the same typed setting infrastructure with an add-in-owned namespace and isolated storage policy.
