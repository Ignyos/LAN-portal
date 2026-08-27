# Access History And Session Retention Plan

Purpose: define the durable AccessHistory model, AccessSessions cleanup policy, shared maintenance mechanism, audit boundaries, and implementation sequence.

## Confirmed Direction

- `AccessSessions` and `AccessHistory` are separate data concerns.
- `AccessHistory` must not depend on or join to `AccessSessions` for its meaning or retention.
- `AccessSessions` stores current and recently inactive session state.
- `AccessHistory` stores durable, human-readable access lifecycle events.
- Both policies are executed by the same maintenance mechanism but use separate retention rules.
- AccessSessions retention: retain inactive records for 30 days.
- AccessHistory retention: configurable, default one year, maximum ten years, and no `Never` option.
- Audit history and application logging remain separate from AccessHistory.

## Data Responsibilities

### AccessSessions

`AccessSessions` remains the operational session table. It supports current authorization and recently inactive-session troubleshooting.

A session is eligible for cleanup when:

```text
RevokedAtUtc is not null
OR
ExpiresAtUtc is not null and ExpiresAtUtc has passed
```

The record is not deleted immediately. It is retained for 30 days after it becomes inactive. The exact cutoff should use the most appropriate inactive timestamp:

- `RevokedAtUtc` for revoked sessions.
- `ExpiresAtUtc` for naturally expired sessions.
- A documented fallback timestamp if an older record lacks the expected value.

The first implementation should preserve inactive records for the full 30-day grace period.

### AccessHistory

`AccessHistory` is a standalone durable event table. Its records should contain enough information to be displayed and understood without requiring an `AccessSessions` row to exist.

The initial event scope is:

```text
AccessRequested
AccessApproved
AccessDenied
AccessRequestExpired
SessionRevoked
SessionLoggedOut
SessionExpired
```

An access request expiration and a session expiration are distinct events:

- `AccessRequestExpired`: a pending request expired before approval.
- `SessionExpired`: an approved access session reached its expiration time.

Suggested record shape:

```text
AccessHistory
  HistoryId
  EventType
  RequestId (nullable)
  SessionId (nullable, informational only)
  UserName (nullable)
  DeviceName
  Roles (nullable)
  Reason (nullable)
  OccurredAtUtc
  RecordedAtUtc
```

`SessionId` may be copied into a history event for reference, but AccessHistory must remain complete and meaningful if the related AccessSessions row is later purged. No foreign-key dependency is required for the initial design.

## AccessRequests Lifecycle Store

Pending access requests should become durable before expiration catch-up is considered complete. This is a separate implementation path from AccessHistory because it owns request state, while AccessHistory owns immutable lifecycle events.

Request states:

```text
Pending
Approved
Denied
Expired
```

The maintenance service may transition a request to `Expired` only when:

```text
Status = Pending
AND
ExpiresAtUtc <= current time
```

Completed requests (Approved, Denied, Expired) are retained for 30 days after the decision timestamp, then purged by the shared maintenance service.

Approval and denial must use atomic conditional transitions that require an unexpired `Pending` request. A request that is already approved or denied must never later produce an expiration event.

Implemented table shape:

```sql
CREATE TABLE IF NOT EXISTS AccessRequests (
  RequestId TEXT NOT NULL PRIMARY KEY,
  UserCode TEXT NOT NULL UNIQUE,
  RequestedUserName TEXT NOT NULL,
  DeviceName TEXT NOT NULL,
  SourceIp TEXT NULL,
  UserAgent TEXT NULL,
  CreatedAtUtc TEXT NOT NULL,
  ExpiresAtUtc TEXT NOT NULL,
  Status TEXT NOT NULL,
  DecidedAtUtc TEXT NULL,
  DecisionReason TEXT NULL,
  ApprovedUserName TEXT NULL,
  ApprovedRoles TEXT NULL,
  ApprovedTokenMinutes INTEGER NULL,
  IssuedAccessToken TEXT NULL,
  IssuedAccessTokenExpiresAtUtc TEXT NULL,
  IssuedRefreshToken TEXT NULL,
  IssuedRefreshTokenExpiresAtUtc TEXT NULL
);

CREATE INDEX IF NOT EXISTS IX_AccessRequests_StatusExpires
  ON AccessRequests(Status, ExpiresAtUtc);
```

The request store should remain independent from AccessHistory. The two stores are related by copied identifiers and coordinated transactions, not by a foreign-key dependency.

## Retention Policies

### AccessSessions Policy

```text
Policy: inactive session cleanup
Retention: 30 days after inactive timestamp
Configuration: fixed initially
```

Recommended triggers:

1. Scheduled maintenance service as the primary trigger.
2. Startup cleanup as a secondary trigger.
3. Optional future Host action for manual cleanup.

The scheduled service should run often enough that cleanup does not depend on user activity. A daily run is sufficient for a 30-day retention policy unless operational measurements suggest otherwise.

### AccessHistory Policy

```text
Policy: configurable event retention
Default: 1 year
Minimum: 7 days
Maximum: 10 years
Never: not offered
Storage: runtime application setting
```

The future Settings page should present friendly duration choices, for example:

```text
30 days
90 days
6 months
1 year
2 years
5 years
10 years
Custom
```

The storage representation should be decided during settings implementation. The value should be validated centrally and applied by the maintenance service without coupling it to AccessSessions retention.

## Shared Maintenance Mechanism

A single background maintenance service should coordinate independent cleanup jobs:

```text
Maintenance service
    |
    +-- AccessSessions cleanup
    |     Fixed 30-day inactive retention
    |
    +-- AccessHistory cleanup
          Configurable retention, default 1 year
```

The service should:

- Run on a scheduled interval.
- Run a cleanup pass during application startup.
- Execute each cleanup policy independently.
- Log start, completion, duration, row counts, and failures.
- Avoid blocking request processing.
- Avoid deleting records newer than the applicable cutoff.
- Be safe to run repeatedly.
- Keep failures in one cleanup job from preventing the other job.

A first implementation can use a daily timer or hosted service loop. The interval itself does not need to be runtime-editable initially.

## Expiration Catch-Up

The maintenance service will not normally run at the exact instant a request or session expires. Expiration events therefore need a catch-up mechanism.

The recommended approach is **lazy catch-up during maintenance**, not an exact-time timer for every record:

1. The maintenance service queries pending access requests and active sessions whose expiration time has passed and whose expiration event has not been recorded.
2. It writes the corresponding `AccessRequestExpired` or `SessionExpired` history event.
3. It then applies the relevant retention/cleanup policy.
4. The operation is idempotent so rerunning it does not create duplicate expiration events.

This requires an idempotency strategy. Options include:

- A unique event key composed of event type plus request/session identifier.
- An `ExpirationRecordedAtUtc` marker on the source record.
- A separate lifecycle-event key table.

The preferred first design is a unique event key in AccessHistory, because it keeps AccessHistory standalone and makes duplicate prevention explicit. The exact schema constraint should be finalized before implementation.

The pending-request portion of catch-up now depends on the durable `AccessRequests` lifecycle store. Pending requests are recovered after process downtime through the shared maintenance service expiration catch-up.

Expiration catch-up should cover:

```text
Pending access request expired -> AccessRequestExpired
Approved session passed expiry -> SessionExpired
```

The maintenance service should also record events for records discovered after downtime. Their `OccurredAtUtc` should represent the configured expiration time, while `RecordedAtUtc` represents when the catch-up process discovered and recorded the event.

## Event Recording Rules

AccessHistory events should be recorded at the point where the application knows the event occurred whenever practical:

- Access request created: record `AccessRequested`.
- Host approves: record `AccessApproved`.
- Host denies: record `AccessDenied`.
- Host revokes: record `SessionRevoked`.
- Client logs out: record `SessionLoggedOut`.
- Maintenance discovers expiration: record `AccessRequestExpired` or `SessionExpired`.

The expiration events are the exception because exact-time execution is not required. The catch-up process records them later with the original expiration time.

AccessHistory writes should be durable and should not depend on the in-memory decision queue. The existing in-memory decision history remains as a temporary compatibility source for the decision list display until a future migration replaces it with durable AccessHistory queries.

## Audit History And Application Logging

The three categories remain separate:

```text
AccessSessions
    Operational current/recent session state

AccessHistory
    Durable access lifecycle events for Host display

Application logs
    Diagnostic and operational records
```

Settings changes should produce audit records independently:

```text
ApplicationSettingAudit
  AuditId
  Key
  PreviousValueRedacted
  NewValueRedacted
  ChangedBy
  Reason
  ChangedAtUtc
```

Rules:

- AccessHistory is not a general application log.
- Application logs should record maintenance runs, cleanup counts, failures, and durations.
- Audit history should record policy changes and security-sensitive actions.
- JWT signing keys and other secrets must never appear in AccessHistory, audits, or logs.
- Cleanup failures should be logged without stopping the application.
- AccessHistory retention must not immediately erase the audit trail for changing that retention setting.

## Advanced Page Integration

The Advanced page should display AccessHistory using the durable AccessHistory source once the migration is complete.

Initial display behavior:

- Preserve the existing Recent Decisions presentation where practical.
- Include the confirmed event types in a user-readable form.
- Use local time for display and UTC for storage.
- Show user/device, event, reason, and event time.
- Do not expose database keys or internal implementation details.
- Add paging once history volume requires it.

The standalone `/local/access-history` route should remain a compatibility redirect until the Advanced section is validated.

## Implementation Checklist

### 1. Contracts And Schema

### 1.1 Durable AccessRequests Lifecycle

- [x] Define the `AccessRequestStatus` values: Pending, Approved, Denied, and Expired.
- [x] Define the durable AccessRequests record model.
- [x] Define the `IAccessRequestStore` abstraction.
- [x] Add the AccessRequests table and status/expiration index.
- [x] Persist a request at creation time with its expiration timestamp.
- [x] Implement polling from durable request state.
- [x] Implement atomic approval transition for unexpired Pending requests.
- [x] Implement atomic denial transition for Pending requests.
- [x] Implement atomic expiration transition for overdue Pending requests.
- [x] Keep approved and denied requests ineligible for expiration.
- [x] Define operational retention for old Approved, Denied, and Expired request rows using the shared 30-day maintenance window.
- [x] Define maximum lengths and validation for user name, device name, reason, source IP, and user agent.
- [x] Define and implement the transaction boundary between AccessRequests state changes and AccessHistory event writes.

### 2. AccessHistory Store

- [x] Add an `IAccessHistoryStore` abstraction.
- [x] Implement SQLite persistence for AccessHistory.
- [x] Keep AccessHistory independent from AccessSessions lookups.
- [x] Add methods to record events idempotently.
- [x] Add methods to query recent events with a limit/page shape.
- [x] Add methods to find expiration events that need catch-up through the AccessRequests and AccessSessions lifecycle stores.
- [x] Add methods to purge events older than the configured cutoff.
- [ ] Ensure sensitive values are redacted before persistence where applicable. (Deferred with audit/logging design.)

### 3. Event Producers

- [x] Record AccessRequested when a request is created.
- [x] Record AccessApproved when a request is approved.
- [x] Record AccessDenied when a request is denied.
- [x] Record SessionRevoked when access is revoked.
- [x] Record SessionLoggedOut when a client logs out.
- [ ] Define how existing in-memory decisions are migrated or phased out.
- [x] Ensure AccessRequests event recording failures roll back the request state change and are logged.
- [x] Harden AccessSessions event recording so session state changes and their AccessHistory events use an atomic transaction where practical.

Session lifecycle transaction implementation:
- Added `ISessionLifecycleService` with a transaction-aware implementation for revoke, logout, and revoke-by-filter.
- Added shared SQLite connection creation through `ISqliteConnectionFactory`.
- Session state and its AccessHistory event now commit or roll back together for these lifecycle operations.
- Functional implementation complete; dedicated session transaction tests remain as part of the final validation gate.

### 4. Maintenance Service

- [x] Add a hosted maintenance service.
- [x] Add startup cleanup execution.
- [x] Add scheduled cleanup execution.
- [x] Implement AccessSessions cleanup using the fixed 30-day inactive retention.
- [x] Implement AccessHistory cleanup using its configured retention.
- [x] Implement pending-request expiration catch-up after the AccessRequests lifecycle store is durable.
- [x] Implement session-expiration catch-up.
- [x] Make cleanup jobs independently failure-tolerant.
- [x] Ensure repeated runs are safe and idempotent.
- [x] Log cleanup start, completion, duration, counts, and failures.

### 5. Runtime Settings

- [x] Add the AccessHistory retention setting with a one-year default in AppSettings.
- [x] Validate a minimum of 7 days and maximum of 10 years.
- [x] Do not offer `Never` at the storage policy boundary.
- [ ] Display the setting in friendly duration units in Host Settings.
- [x] Ensure the maintenance service reads the current runtime value.
- [ ] Audit changes to the retention setting.
- [ ] Log invalid values and fallback use.
- [x] Move Request Timeout into AppSettings with a five-minute default.
- [x] Move Poll Interval into AppSettings with a three-second default.
- [ ] Add typed per-setting definitions and Host Settings UI behavior.

Runtime configuration note:
- Request Timeout and Poll Interval are stored internally as seconds and read through `IAppSettingsStore`.
- Host Settings should display Request Timeout as minutes/seconds and Poll Interval as seconds.

### 6. Advanced Page Migration

- [x] Replace the temporary in-memory Access History source with durable AccessHistory.
- [ ] Preserve the Access History section and its collapsed-state behavior. (Functional; manual validation pending.)
- [x] Display lifecycle events in a clear user-facing format.
- [ ] Add paging or bounded querying. (Deferred.)
- [ ] Validate empty, large, and mixed event histories. (Manual validation pending.)
- [x] Keep `/local/access-history` redirecting to `#access-history` during migration.
- [x] Remove obsolete standalone page implementation after validation.

### 7. Testing And Validation

- [ ] Add unit tests for AccessHistory, AccessRequests, session lifecycle, retention, and maintenance behavior. (Deferred.)
- [ ] Add integration tests for lifecycle transactions, expiration catch-up, cleanup, and Advanced API integration. (Deferred.)
- [ ] Run the full solution build and test suite as part of the later test pass. (Deferred.)
- [ ] Complete manual Advanced-page validation.

Testing decision:
- Unit and integration tests are intentionally deferred for now.
- The implementation may continue with build validation and manual review, but the initiative is not considered production-ready until the deferred tests are added and passing.

## Manual Validation Checklist

Before accepting the Access History initiative as complete, verify the following on a running instance:

### Access History Section Behavior

- [ ] Navigate to `/local/advanced` (or `/local/setup` and select Advanced).
- [ ] Verify the page loads with all sections collapsed by default.
- [ ] Click the Access History section header to expand it.
- [ ] Verify the section expands smoothly and displays a table.
- [ ] Close the browser or navigate away and return to `/local/advanced`.
- [ ] Verify that the Access History section remains expanded after page reload (persisted UI state).
- [ ] Click the header again to collapse the section.
- [ ] Verify the collapse persists on page reload.

### Access History Table Display

- [ ] Verify the table has exactly five columns: Time | User | Device | Action | Reason.
- [ ] Verify each row displays:
  - Time in local timezone (e.g., "8/24/2026, 3:42 PM")
  - User name (e.g., "admin" or "(n/a)" if not applicable)
  - Device name (e.g., "Desktop" or "Laptop")
  - Action/event type (e.g., "AccessRequested", "SessionRevoked", "SessionLoggedOut")
  - Reason (e.g., "Revoked by admin." or "(n/a)" if blank)

### Event Type Coverage

Test each event type appears correctly when triggered:

- [ ] Create a new access request (client-initiated) → verify `AccessRequested` event appears.
- [ ] Approve the request as Admin → verify `AccessApproved` event appears with approver name and roles.
- [ ] Create another request and deny it → verify `AccessDenied` event appears with reason.
- [ ] Create a session and revoke it from Admin → verify `SessionRevoked` event appears with revocation reason.
- [ ] Create a session and have the client log out → verify `SessionLoggedOut` event appears.
- [ ] Wait for or trigger session/request expiration → verify `SessionExpired` and/or `AccessRequestExpired` events appear.

### Empty History

- [ ] Open Advanced in a fresh environment or after purging history.
- [ ] Verify the Access History section shows "No recent decisions." or similar message (not an error).

### Large History

- [ ] Trigger many events (e.g., 50+ entries).
- [ ] Verify the table scrolls and remains readable.
- [ ] Verify performance is acceptable (no noticeable lag when expanding/collapsing).
- [ ] Note: Paging/bounded query optimization is deferred; verify current behavior is acceptable or identify as future work.

### Compatibility Redirect

- [ ] Navigate to `/local/access-history`.
- [ ] Verify the page redirects to `/local/advanced` and automatically scrolls to or highlights the Access History section.
- [ ] Verify the URL changes to include `#access-history` or similar anchor.

### Responsive Behavior

- [ ] Test on desktop and tablet/narrow-window layouts.
- [ ] Verify the table remains readable and sections collapse properly on narrow screens.

After all checks pass, mark the section as validated and proceed to handoff.

## Implementation Status Summary

Functionally complete (build-validated, tests deferred):
- Durable AccessRequests lifecycle store with atomic state transitions and history writes.
- Durable AccessHistory store independent from AccessSessions.
- Transactional session lifecycle service for revoke, logout, and revoke-by-filter operations.
- Request and session expiration catch-up during maintenance.
- Shared 30-day maintenance cleanup for inactive sessions and completed requests.
- Runtime AccessHistory retention configurable through AppSettings (one-year default, 7-day minimum, 10-year maximum).
- Runtime Request Timeout and Poll Interval stored in AppSettings.
- Shared input validation for request and approval boundaries.
- Advanced page with Access History section showing durable events in five-column table format (Time, User, Device, Action, Reason).

Manual validation required:
- Advanced page section expansion persistence.
- Advanced page with large, empty, and mixed event histories.
- Access History compatibility redirect behavior.
- Correct event labels in the durable history source.

## Acceptance Criteria

- AccessHistory is durable and standalone.
- AccessHistory contains request decisions, request expiration, revoke, logout, and session expiration events.
- AccessSessions inactive records are retained for 30 days before cleanup.
- AccessRequests completed records are retained for 30 days after decision/expiration.
- AccessHistory retention defaults to one year and is configurable from Host Settings.
- AccessHistory retention supports no value greater than ten years and does not offer Never.
- A shared maintenance service runs startup and scheduled cleanup.
- Expiration events are caught up after downtime without duplicates.
- AccessHistory remains available after related AccessSessions records are purged.
- Logs, audit history, AccessHistory, and operational session state remain distinct.
- JWT signing keys and other sensitive values never appear in user-facing history or diagnostics.

## Final Handoff

- [ ] After the user accepts the Access History and session-retention initiative as complete, return to [Host Navigation And Advanced Settings Plan](host-navigation-and-advanced-settings-plan.md) and continue with the next Advanced page section.
