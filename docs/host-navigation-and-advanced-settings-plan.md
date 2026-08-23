# Host Navigation And Advanced Settings Plan

Purpose: define the Host-side navigation and the staged plan for introducing Settings and expanding Advanced before moving Access History.

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

Access History currently remains at `/local/access-history` until the Advanced refactor is implemented.

## Milestone 1: Host Navigation Foundation

- [x] Define the canonical Host menu labels and route map.
- [x] Add the menu to File Sharing.
- [x] Add the menu to Admin.
- [x] Add the menu to Settings.
- [x] Add the menu to Advanced.
- [x] Keep the standalone Access History route available while migration is pending; do not include it in the Host menu.
- [x] Keep the separator between Admin and Settings.
- [x] Use shared Host CSS for menu layout, spacing, typography, and active state.
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

- [ ] Keep the Advanced page as the location for Host operational and security tools.
- [ ] Add a page-level Advanced label/title outside content sections.
- [ ] Split Advanced into independently styled sections.
- [ ] Preserve existing Guest URL functionality while restructuring it.
- [ ] Add an Access History section using the existing history data and endpoint behavior.
- [ ] Decide whether Access History is embedded in Advanced or linked as an Advanced subsection.
- [ ] Remove the standalone Access History menu destination only after the new Advanced section is validated.
- [ ] Add an explicit migration/redirect from `/local/access-history` if the standalone route is retired.

Acceptance gate:
- Advanced clearly presents multiple concerns without losing existing guest URL or history behavior.

## Milestone 4: Logs Section

- [ ] Define the operational logging goals before implementing the UI.
- [ ] Decide which events are retained in durable storage versus standard `ILogger` output.
- [ ] Define log severity, category, timestamp, correlation, and redaction rules.
- [ ] Ensure secrets and JWT signing keys never appear in logs.
- [ ] Define retention and cleanup behavior.
- [ ] Decide whether Logs is a read-only viewer, export tool, or both.
- [ ] Define paging/filtering for large log sets.
- [ ] Add a Logs section to Advanced.
- [ ] Add tests for redaction, access boundaries, and retention.

Acceptance gate:
- Host operators can inspect useful operational events without exposing secrets or overwhelming the page.

## Milestone 5: Security Controls Section

- [ ] Define the security settings that belong in the Host UI.
- [ ] Define which settings are startup-only and which can change at runtime.
- [ ] Add JWT issuer/audience controls only if their operational behavior is fully defined.
- [ ] Add a safe action for generating a new signing key.
- [ ] Require confirmation before security-sensitive changes.
- [ ] Invalidate all sessions when any JWT setting changes, including signing-key rotation.
- [ ] Record security-setting changes in durable audit history.
- [ ] Never display or log the signing-key value.
- [ ] Define restart requirements for settings that cannot be safely reloaded.
- [ ] Add tests for session invalidation and secret redaction.

Acceptance gate:
- Security controls are explicit, auditable, and cannot accidentally expose or preserve sessions after a JWT security change.

## Milestone 6: Runtime Settings Integration

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

- Should Access History be a full Advanced section or a link to a dedicated Advanced subpage?
- Should Logs use SQLite durable records, standard file/event logging, or a hybrid?
- What minimum log event set is useful to a Host operator?
- What settings belong in Security Controls besides JWT issuer, audience, and signing-key rotation?
- Should a new signing key invalidate sessions immediately or require a confirmation/restart sequence?
- Should add-in settings share the typed setting mechanism while remaining isolated by add-in ID?
- Should Settings use sections for File Sharing, Access Requests, Updates, and add-in management from its first content pass?
