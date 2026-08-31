# Host Navigation And Advanced Settings Plan

Purpose: define the Host-side navigation and the staged plan for introducing Settings and expanding Advanced before moving Access History.

## Current implementation status

The Host Advanced page has already been redesigned into distinct sections: Customize URL, Access History, Logs, and Security. The structure is implemented and persisted, and the section state is stored in SQLite. In the current codebase:

- Customize URL is live and functional.
- Access History is embedded in the Advanced page and loads from the existing history endpoint.
- Logs is complete for the accepted first pass: it has a working read-only shell, endpoint integration, SQLite persistence, severity/category filtering, redaction, retention cleanup, centralized access-request and maintenance capture, and focused tests.
- Security now contains the focused JWT signing-key rotation action. The Host operator is warned that rotation immediately invalidates all currently logged-in sessions, and is told why rotation may be needed. Issuer/audience editing and other security controls remain intentionally out of scope.

This document should be read as the current source of truth for the remaining Host Advanced work, not as a future plan for an unbuilt redesign.

## Target Host Navigation

The Host menu should contain:

```text
File Sharing
Admin
----------------
Settings
Advanced
```

The separator is visual only and does not navigate. The Host menu should be available consistently across Host pages and should use the existing shared `host.css` stylesheet.

Initial routes:

| Menu item | Route | Initial state |
|---|---|---|
| File Sharing | `/local/setup` | Existing page |
| Admin | `/local/admin` | Existing page |
| Settings | `/local/settings` | New empty page |
| Advanced | `/local/advanced` | Existing page, expanded over milestones |

`/local/access-history` remains available as a compatibility route and redirects to `/local/advanced#access-history`.

## Milestone 1: Host Navigation Foundation

- [x] Define the canonical Host menu labels and route map.
- [x] Add File Sharing to the Host WinForms menu.
- [x] Add Admin to the Host WinForms menu.
- [x] Add Settings to the Host WinForms menu.
- [x] Add Advanced to the Host WinForms menu.
- [x] Keep the standalone Access History route available while migration is pending; do not include it in the Host menu.
- [x] Keep the separator between Admin and Settings.
- [x] Use the Host application's menu for page navigation; use shared Host CSS for page content.
- [ ] Verify navigation works on desktop and narrow windows.
- [x] Verify local-request protection remains active for every Host page.

Acceptance gate:
- A Host operator can move between File Sharing, Admin, Settings, and Advanced using the same visible menu.

## Milestone 2: Empty Settings Page

- [x] Add `/local/settings` endpoint.
- [x] Restrict the page to local Host requests using the existing local-request guard.
- [x] Render the shared Host menu.
- [x] Render a page-level `Settings` label/title.
- [x] Leave the settings content area intentionally empty or show a neutral placeholder.
- [x] Do not expose raw database keys.
- [x] Do not add setting-edit behavior in this milestone.
- [ ] Add endpoint/manual validation.

Acceptance gate:
- Settings is reachable from the Host menu and establishes the future settings-page location without committing to content yet.

## Milestone 3: Advanced Page Structure

- [x] Keep the Advanced page as the location for Host operational and security tools.
- [x] Add a page-level Advanced label/title outside content sections.
- [x] Split Advanced into independently collapsible sections.
- [x] Allow multiple Advanced sections to remain expanded at the same time.
- [x] Make each entire section header clickable, keyboard accessible, and expose expanded/collapsed state.
- [x] Use a CSS-rendered section marker that rotates from `>` when collapsed to `v` when expanded.
- [x] Keep all Advanced sections collapsed by default.
- [x] Persist each section's expanded/collapsed state in SQLite.
- [x] Use a batch UI-state API: `GET /api/local/ui-state?page=advanced` and `POST /api/local/ui-state`.
- [x] Use stable section keys independent of display labels: `customize-url`, `access-history`, `logs`, and `security`.
- [x] Treat persisted section state as global to the Host installation for now.
- [x] Keep persisted UI state in a dedicated `HostUiState` table rather than `AppSettings`.
- [x] Use a dedicated `IHostUiStateStore` so `IAppSettingsStore` remains responsible only for `AppSettings`.
- [x] Treat UI-state persistence failures as non-blocking and log them without interrupting the page.
- [x] Default missing section-state records to collapsed.
- [x] Leave room for a future optional operator key if Host operator identities are introduced.
- [x] Preserve existing Guest URL functionality while restructuring it.
- [x] Add an Access History section using the existing history data and endpoint behavior.
- [x] Embed Access History as a section within Advanced rather than linking to a separate Advanced subpage.
- [x] Preserve the existing Access History table and behavior during the initial move.
- [x] Keep `/local/access-history` as a compatibility route that redirects to `/local/advanced#access-history` after migration.
- [ ] Add a later configurable retention policy for Access History.
- [x] Remove the standalone Access History menu destination after the new Advanced section was added.
- [x] Add an explicit migration redirect from `/local/access-history` to `/local/advanced#access-history`.

Initial Advanced sections:
- `Customize URL` - existing guest URL settings and customization guidance.
- `Access History` - existing recent decisions table, initially preserved functionally.
- `Logs` - active read-only operational log view backed by the durable application log store.
- `Security` - focused JWT signing-key rotation action with impact guidance and confirmation.

Presentation decisions:
- Major section headings should be slightly smaller than the page title and consistent with one another.
- `Customize URL` replaces the previous visible section title `URL`.
- `Security` is the user-facing title; `Security Controls` remains a technical planning term.
- Logs and Security should remain visible as independently collapsible sections.
- Logs should remain read-only and operator-focused while capture coverage and filtering are completed.
- Security should remain a narrow, confirmation-heavy action surface.
- JWT rotation invalidates active sessions and records a redacted Security event.

Section-state persistence decision:
- Persist expansion state in SQLite for resilience across Host restarts, WebView2 profile changes, and browser-storage cleanup.
- Use a dedicated table with a shape equivalent to:

```sql
CREATE TABLE IF NOT EXISTS HostUiState (
	PageKey TEXT NOT NULL,
	SectionKey TEXT NOT NULL,
	IsExpanded INTEGER NOT NULL,
	UpdatedAtUtc TEXT NOT NULL,
	PRIMARY KEY (PageKey, SectionKey)
);
```

- The initial scope is installation-global because the Host does not yet have durable operator identities.
- A future operator identity may add an optional operator key without changing the Advanced UI contract.
- State writes should occur on user interaction, not every render.
- A future reset-layout action may clear the state for a page and restore collapsed defaults.
- The Advanced page should render collapsed defaults immediately and load persisted state asynchronously.
- A failed UI-state write should be logged without preventing the section from opening or closing.

Acceptance gate:
- Advanced clearly presents multiple concerns without losing existing guest URL or history behavior.

## Milestone 4: Logs Section

Current status: complete for the accepted first pass. The Advanced page loads redacted application log data from `/api/local/logs`. The SQLite store, retention cleanup, severity/category filtering, maintenance events, access-request lifecycle events, and focused tests are in place. More detailed time-range, correlation-ID, and broader lifecycle capture can be considered later if operator usage demonstrates a need.

Recommended design: use a hybrid model rather than relying on one log stream.

### Logs Design Recommendation

- Standard `ILogger` remains the diagnostic system for application execution, warnings, and unhandled exceptions.
- Durable log records in SQLite are reserved for Host-operator useful events and internal errors that need to be reviewable in the UI.
- AccessHistory remains separate and only captures user-visible lifecycle decisions; it does not become a general-purpose log stream.
- Internal application errors are included in the logs by default. This includes exceptions, validation failures, maintenance failures, failed database writes, failed background tasks, and unexpected API behavior.
- Log entries should be structured, not free-form paragraphs, so the UI can show a consistent table and the system can filter by severity, category, correlation ID, and time range.

### Log Entry Model

Each durable log entry should include:

```text
LogId
OccurredAtUtc
Severity (Trace, Debug, Information, Warning, Error, Critical)
Category (Host, DeviceAuth, Maintenance, App, Security, Admin)
Source (controller, service, background worker, UI, etc.)
CorrelationId (request or operation ID when available)
UserName (optional, if directly tied to the action)
DeviceName (optional)
Message (human-readable summary)
ExceptionType (optional)
ExceptionMessage (optional, redacted)
DetailsJson (optional, redacted JSON payload)
IsRedacted (boolean)
```

The UI should not display raw secrets. Any log field that may contain sensitive data should be redacted or excluded before storage or display.

### Log Categories

Recommended first-pass categories:

- Host UI
- Device Authentication
- Access Requests
- Maintenance / Cleanup
- Security / JWT
- App / Startup / Shutdown
- Admin Actions
- Background Jobs

This keeps the operator-facing log stream understandable while still capturing application failures.

### Internal Error Strategy

The log stream should capture all internal application errors, including:

- unhandled exceptions
- validation failures that block a request
- database errors or write failures
- configuration fallback or invalid-value events
- maintenance jobs that fail or partially succeed
- session or token lifecycle failures
- API calls that fail after local-request validation

These records belong in the log stream because operators need to diagnose failures without searching raw application output or event logs.

### Redaction and Security Rules

- Never log raw JWT signing keys, JWT values, refresh tokens, access tokens, or password material.
- Redact any payload field containing the above values before it reaches the UI or durable storage.
- Store a stable redacted token fingerprint only when there is a real operational need to show a value changed or was reused.
- Log only the fact that a secret was rejected, invalid, or rotated, not the secret itself.
- If a payload is too sensitive to keep in a durable record, log a redacted summary plus the failure reason.

### Durable Storage vs. ILogger

Recommended split:

- Use `ILogger` for runtime diagnostics, structured provider output, and exception details needed by developers.
- Use SQLite durable records for a host-operator UI with retention and filters.
- Keep AccessHistory focused on user-facing lifecycle decisions; do not mix it with application error events.

### Retention and Cleanup

- Default durable retention: 30 days.
- Extendable up to 365 days if the Host needs longer review windows.
- Purge by `OccurredAtUtc` to avoid excessive storage use.
- Maintenance should also prune failed or orphaned log rows while preserving the most important recent errors.
- The UI should allow both time filtering and severity filtering without reading the entire log table.

### UI Behavior

The initial Logs section should be read-only and operator-facing:

- summary cards for recent warnings and errors
- filtered table view with severity and time-range controls
- request or correlation ID search
- expandable detail panel for the selected record

This is a safe first pass and avoids building an export or data-editing surface before the system is stable.

### Minimum Useful Log Set

For the first iteration, record these events if they occur:

- startup and shutdown status
- maintenance run start/finish and result counts
- auth failures and request validation failures
- approval/denial failures and retries
- session revoke/logout failures
- configuration fallback and invalid-value warnings
- background job failures and partial success
- any unexpected exception with redacted context

### Implementation Sequence

- [x] Define the exact durable log schema and retention default.
- [x] Decide which logger categories are enabled by default.
- [x] Decide which events are stored durably versus only emitted through `ILogger`.
- [x] Define redaction rules and dependencies on the shared sanitization layer.
- [x] Implement a durable log table with a validation-safe schema.
- [x] Add a read-only Logs section to Advanced.
- [x] Add the accepted first-pass filters for severity and category.
- [x] Add host-side access checks so only local operators can view logs.
- [x] Add tests for redaction, retention, and log entry creation.
- [x] Complete operator-focused validation of the accepted Logs workflow.

Acceptance gate:
- Host operators can inspect useful operational events and internal application errors without exposing secrets or overwhelming the page.

## Milestone 5: Security Controls Section

Current status: focused first action implemented. The Security section provides a confirmed JWT signing-key rotation action, invalidates active sessions, returns only a safe key fingerprint and impact count, and records the action in the durable Security log. Broader security settings remain out of scope until they are needed.

Recommended design: keep Security as a tightly-scoped operational view, not a full security administration console.

### Security Controls Design Recommendation

- Security section should be read-mostly and confirmation-heavy.
- It should show only settings that are explicitly needed for Host operator visibility and safe runtime action.
- It should never display JWT signing keys or raw secret material.
- Security-sensitive changes should require a clear confirmation step and should invalidate all active sessions when the JWT configuration changes.

### Intended UI Surface

The current Security section includes only:

- a clear explanation that rotating the JWT signing key immediately signs out every currently logged-in user
- a concise explanation of appropriate reasons to rotate, such as suspected credential compromise or invalidating all existing tokens
- a rotation action that generates a fresh signing key and saves it via `IAppSettingsStore`
- a clear confirmation dialog before the action is executed
- a status area showing when rotation completed, how many sessions were invalidated, and a safe fingerprint

Issuer/audience display, generic security settings, and additional controls are intentionally deferred.

### Security-Sensitive Settings

The only currently required security operation is JWT signing-key rotation. Do not expose generic runtime settings, issuer/audience editing, or low-level secret values that are not required for this action.

### Change Behavior

When any security setting changes,

- all active sessions should be invalidated immediately
- refresh tokens should be invalidated and reissued as needed
- any dependent token validation state should be reloaded from the database
- the change should be recorded in a dedicated durable audit record, separate from AccessHistory and separate from ApplicationLogs

This should be treated as a security event, not a normal configuration change.

### Safety Rules

- Never log the signing key value.
- Never echo the signing key back to the UI or API response.
- If a value is displayed at all, show only a redacted fingerprint or metadata such as rotation date.
- Require a confirm step before rotating a signing key.
- Treat all key and issuer changes as high-risk, no-undo actions with explicit operator acknowledgment.

### Implementation Sequence

- [ ] Define the exact Security UI fields and safety text.
- [ ] Add a server-side security settings model using `IAppSettingsStore` and typed validation.
- [ ] Build a rotation endpoint that writes a new key, invalidates sessions, and records the event.
- [ ] Add confirmation-handling and a warning banner in the UI.
- [ ] Add audit records for issuer/audience/key changes.
- [ ] Add tests for key rotation, session invalidation, and redaction.
- [ ] Expose only masked metadata in the UI, never the raw value.

Acceptance gate:
- Security controls are explicit, auditable, and cannot accidentally expose or preserve sessions after a JWT security change.

## Milestone 6: Runtime Settings Integration

- [x] Add a first Host Settings control for running LAN Portal when Windows starts.
	- stored as `Host:RunAtWindowsStartup` in `AppSettings`, defaulting to `true`
	- applies to installed packages by writing/removing the current-user Startup Apps Run entry
	- dev/debug runs do not register a startup command when the packaged launcher is unavailable
- [ ] Finalize the typed per-setting definition model.
- [ ] Keep direct `AppSettings` table access inside `IAppSettingsStore`/`SqliteAppSettingsStore`.
- [ ] Add a typed application-settings facade above the store.
- [ ] Move Request Timeout into runtime settings with seconds at storage and minutes/seconds in the UI.
- [ ] Move Poll Interval into runtime settings with seconds at storage and a friendly UI representation.
- [ ] Add setting-specific defaults and validation.
- [ ] Add atomic write plus audit-record behavior.
- [ ] Add structured logging around reads, writes, invalid values, and fallback use.
- [ ] Define whether add-in settings use an isolated namespace and the same typed infrastructure.

Acceptance gate:
- Runtime settings can be changed through Host Settings with validation, auditing, logging, and no raw-key leakage.

## Cross-Cutting Design Rules

- Host-only settings pages remain protected by the existing local-request check.
- Shared styles belong in `Ignyos.LanPortal.Api/wwwroot/host.css`.
- Settings table access has one owner: `IAppSettingsStore`, implemented by `SqliteAppSettingsStore`.
- Runtime settings are distinct from operational records such as sessions and audit history.
- Sensitive values are protected at rest and redacted from UI, logs, and audits.
- Navigation changes should not silently delete or strand existing routes.
- Access History should move only after Advanced has a validated replacement.

## Open Questions For Discussion

- Should Logs use SQLite durable records, standard file/event logging, or a hybrid?
- What minimum log event set is useful to a Host operator?
- What settings belong in Security Controls besides JWT issuer, audience, and signing-key rotation?
- Should add-in settings share the typed setting mechanism while remaining isolated by add-in ID?
- Should Settings use sections for File Sharing, Access Requests, Updates, and add-in management from its first content pass?
