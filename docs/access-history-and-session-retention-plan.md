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

AccessHistory writes should be durable and should not depend on the in-memory decision queue. The existing in-memory decision history can remain as a temporary compatibility source until the durable implementation is complete.

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

- [ ] Define the AccessHistory event type constants.
- [ ] Define the durable AccessHistory record model.
- [ ] Define event idempotency requirements.
- [ ] Define the `AccessHistory` table schema and indexes.
- [ ] Define the AccessSessions inactive timestamp and cleanup cutoff rules.
- [ ] Define the AccessHistory retention setting key and typed representation.
- [ ] Add schema creation/migration support.

### 2. AccessHistory Store

- [ ] Add an `IAccessHistoryStore` abstraction.
- [ ] Implement SQLite persistence for AccessHistory.
- [ ] Keep AccessHistory independent from AccessSessions lookups.
- [ ] Add methods to record events idempotently.
- [ ] Add methods to query recent events with a limit/page shape.
- [ ] Add methods to find expiration events that need catch-up.
- [ ] Add methods to purge events older than the configured cutoff.
- [ ] Ensure sensitive values are redacted before persistence where applicable.

### 3. Event Producers

- [ ] Record AccessRequested when a request is created.
- [ ] Record AccessApproved when a request is approved.
- [ ] Record AccessDenied when a request is denied.
- [ ] Record SessionRevoked when access is revoked.
- [ ] Record SessionLoggedOut when a client logs out.
- [ ] Define how existing in-memory decisions are migrated or phased out.
- [ ] Ensure event recording failures are logged and their impact is documented.

### 4. Maintenance Service

- [ ] Add a hosted maintenance service.
- [ ] Add startup cleanup execution.
- [ ] Add scheduled cleanup execution.
- [ ] Implement AccessSessions cleanup using the fixed 30-day inactive retention.
- [ ] Implement AccessHistory cleanup using its configured retention.
- [ ] Implement pending-request expiration catch-up.
- [ ] Implement session-expiration catch-up.
- [ ] Make cleanup jobs independently failure-tolerant.
- [ ] Ensure repeated runs are safe and idempotent.
- [ ] Log cleanup start, completion, duration, counts, and failures.

### 5. Runtime Settings

- [ ] Add the AccessHistory retention setting with a one-year default.
- [ ] Validate a minimum of 7 days and maximum of 10 years.
- [ ] Do not offer `Never`.
- [ ] Display the setting in friendly duration units in Host Settings.
- [ ] Ensure the maintenance service reads the current runtime value.
- [ ] Audit changes to the retention setting.
- [ ] Log invalid values and fallback use.

### 6. Advanced Page Migration

- [ ] Replace the temporary in-memory Access History source with durable AccessHistory.
- [ ] Preserve the Access History section and its collapsed-state behavior.
- [ ] Display lifecycle events in a clear user-facing format.
- [ ] Add paging or bounded querying.
- [ ] Validate empty, large, and mixed event histories.
- [ ] Keep `/local/access-history` redirecting to `#access-history` during migration.
- [ ] Remove obsolete standalone page implementation after validation.

### 7. Testing And Validation

- [ ] Test each AccessHistory event type.
- [ ] Test denied requests that never create an AccessSessions row.
- [ ] Test AccessHistory queries after related AccessSessions rows are purged.
- [ ] Test duplicate event prevention.
- [ ] Test pending-request expiration catch-up after downtime.
- [ ] Test session-expiration catch-up after downtime.
- [ ] Test AccessSessions 30-day cleanup boundary.
- [ ] Test AccessHistory retention boundaries.
- [ ] Test 7-day minimum and 10-year maximum validation.
- [ ] Test that `Never` is rejected/not offered.
- [ ] Test independent cleanup failure handling.
- [ ] Test audit records for retention-policy changes.
- [ ] Test secret redaction in history, logs, and audits.
- [ ] Run the full solution build and test suite.
- [ ] Complete manual Advanced-page validation.

## Acceptance Criteria

- AccessHistory is durable and standalone.
- AccessHistory contains request decisions, request expiration, revoke, logout, and session expiration events.
- AccessSessions inactive records are retained for 30 days before cleanup.
- AccessHistory retention defaults to one year and is configurable from Host Settings.
- AccessHistory retention supports no value greater than ten years and does not offer Never.
- A shared maintenance service runs startup and scheduled cleanup.
- Expiration events are caught up after downtime without duplicates.
- AccessHistory remains available after related AccessSessions records are purged.
- Logs, audit history, AccessHistory, and operational session state remain distinct.
- JWT signing keys and other sensitive values never appear in user-facing history or diagnostics.

## Final Handoff

- [ ] After the user accepts the Access History and session-retention initiative as complete, return to [Host Navigation And Advanced Settings Plan](host-navigation-and-advanced-settings-plan.md) and continue with the next Advanced page section.
