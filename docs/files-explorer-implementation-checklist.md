# Files Explorer Implementation Checklist

Purpose: convert accepted design decisions into an execution-ready implementation backlog.

Scope guardrails:
- End-user Files experience only.
- Windows + NTFS host target for milestone 1.
- No hard lock requirement in milestone 1; use optimistic concurrency and conflict handling.
- Defer host-side UI refactor until milestone-1 Files functionality is implemented and validated.

## Immediate Next Steps (Execution Order)

1. Stabilize contracts first (no UI churn yet):
- [x] Finalize permission keys, repeated permission claims, and contract DTOs.
- [x] Finalize event envelope and event type payload schema.

2. Ship backend safety and enforcement baseline:
- [x] Implement strict path normalization and shared-root boundary enforcement for all Files endpoints.
- [x] Implement server-side permission enforcement mapping for each action.

3. Deliver a thin vertical slice end-to-end:
- [x] Folder listing by current path.
- [x] Tree lazy loading.
- [x] Explicit search request.
- [x] Create folder + rename + delete + single-file download.

4. Add real-time baseline and optimistic reconciliation:
- [x] WebSocket/SignalR transport with scoped path subscriptions.
- [x] Created/updated/deleted events with correlationId support.
- [x] Client reconciliation for affected-folder refresh only.

5. Add remaining milestone-1 actions:
- [ ] Move (including multi-select support).
- [ ] Upload + drag/drop upload.

6. Close milestone quality gates:
- [ ] Performance baseline for list/search and event latency.
- [ ] Integration tests for action set, permissions, events, and path safety.
- [ ] Manual validation against milestone exit criteria.

Suggested first PR slices:
- PR1: Contracts + permission model + path normalization utilities. (Completed)
- PR2: Files API baseline endpoints (list/tree/search/create/rename/delete/download) with authorization. (Completed)
- PR3: Real-time transport + event contract + minimal client reconciliation. (Baseline completed)
- PR4: Move + upload/drag-drop + multi-select action constraints.
- PR5: Performance instrumentation + integration tests + exit-criteria hardening.

## 0. Delivery Setup

- [ ] Create implementation branch and define PR slicing strategy.
- [ ] Confirm feature flag or rollout toggle for the new Files experience.
- [ ] Define milestone-1 acceptance criteria and demo script.
- [ ] Confirm telemetry events required for rollout confidence.

## 1. Contracts And Models

### 1.1 Permission Model Contract
- [x] Define permission key namespace and initial keys:
  - file:read
  - file:add
  - file:rename
  - file:move
  - file:delete
  - file:upload
  - file:download
- [x] Define claim format as repeated permission claims, not CSV.
- [ ] Keep coarse roles and fine-grained permissions as separate concepts in model types.
- [ ] Define response shapes so UI can hide/disable actions based on grants.

### 1.2 API DTOs
- [x] Define/extend DTOs for:
  - folder listing by current path
  - lazy tree-node loading
  - explicit search request/response
  - create folder, rename, move, delete, upload, download
- [x] Normalize all paths as shared-root-relative values.
- [ ] Include conflict/error DTO shape for optimistic concurrency reconciliation.

### 1.3 Event Contract DTOs
- [x] Define file event envelope fields:
  - eventId
  - eventType
  - occurredAtUtc
  - scopePath
  - correlationId (optional)
  - batchId (optional)
- [x] Define event payload variants for created, updated, deleted, renamed, moved, batch.
- [x] Define schema version field for forward compatibility.

## 2. API Endpoints And Authorization

### 2.1 Files API Surface
- [x] Implement/extend endpoint: list folder contents by current path.
- [x] Implement/extend endpoint: lazy load child nodes for folder tree.
- [x] Implement endpoint: explicit search.
- [x] Implement/extend endpoint: create folder.
- [x] Implement/extend endpoint: rename item.
- [x] Implement/extend endpoint: move item(s).
- [x] Implement/extend endpoint: delete item(s).
- [x] Implement/extend endpoint: upload to current folder.
- [x] Implement/extend endpoint: download single file.

### 2.2 Permission Enforcement
- [x] Enforce server-side permission checks for each action endpoint.
- [x] Ensure enforcement does not rely on UI state.
- [ ] Ensure coarse roles can still exist while action checks use permissions.

### 2.3 Safety Controls
- [x] Enforce strict path traversal prevention on all path-bearing endpoints.
- [x] Enforce shared-root boundary guarantees across all read/write operations.
- [ ] Add validation for invalid/ambiguous path inputs and return safe error responses.

## 3. Indexing And Change Detection

### 3.1 Indexing Strategy
- [ ] Implement indexed lookup path for listing/search to avoid repeated recursive scans.
- [ ] Integrate Everything by Voidtools behind an adapter interface.
- [ ] Ensure index queries are scoped to configured shared root only.
- [ ] Define fallback behavior if Everything is unavailable or degraded.

### 3.2 Alternative Engine Readiness
- [ ] Define abstraction boundaries so Everything can be replaced.
- [ ] Add implementation note/todo for in-house library or service worker replacement path.

### 3.3 Host Worker Updates
- [ ] Implement worker/background service for filesystem change detection.
- [ ] Add batching/debounce for burst changes.
- [ ] Emit least-expensive update signals (single file vs folder scope) where possible.

## 4. Real-Time Event Pipeline

### 4.1 Transport
- [x] Implement WebSocket/SignalR channel for file-change notifications.
- [x] Add path-scope subscription support (subscribe/unsubscribe by relevant paths).
- [x] Ensure reconnect behavior restores active subscriptions.

### 4.2 Event Publication
- [x] Publish created/updated/deleted/renamed/moved/batch events from server.
- [x] Include correlationId when event is tied to a client-initiated action.
- [ ] Ensure event ordering guarantees are documented (or lack thereof is explicit).

### 4.3 Reconciliation Behavior
- [ ] Define optimistic action state machine on client (pending, confirmed, conflicted, reverted).
- [x] Reconcile incoming events using correlationId where available.
- [x] Refresh only affected folders/nodes, avoid full explorer refresh by default.

## 5. Web UI: Explorer Experience

### 5.1 Layout And Navigation
- [ ] Build two-pane explorer layout (tree left, contents right).
- [ ] Show current path and breadcrumbs when not at root.
- [ ] Implement double-click folder navigation.
- [ ] Implement back/forward navigation history.

### 5.2 Selection And Actions
- [ ] Implement multi-select behavior.
- [ ] Enforce selection rules in action bar:
  - rename single-item only
  - delete multi-select allowed
  - move multi-select allowed
  - new folder current-folder action
  - upload current-folder action
  - download single-file first pass
- [ ] Show disabled/hidden actions based on effective permission grants.

### 5.3 Search UX
- [ ] Implement explicit-trigger search flow (no search-as-you-type).
- [ ] Render search results in a way that preserves path context.
- [ ] Ensure search action obeys same permission and root-boundary rules.

### 5.4 Upload UX
- [ ] Support file upload picker into current folder.
- [ ] Support drag-and-drop upload into current folder.
- [ ] Show progress and actionable errors.

## 6. Concurrency And Conflict Handling

- [ ] Implement optimistic concurrency responses for rename/move/delete/update races.
- [ ] Define user-facing conflict messages and recovery actions.
- [ ] Add retry path for recoverable conflicts.
- [ ] Keep hard file/folder locking out of milestone 1.
- [ ] Track advisory soft-lock concept as post-milestone candidate.

## 7. Performance

- [ ] Add benchmarks for large folder trees and large file collections.
- [ ] Measure and compare scan overhead before/after indexing strategy.
- [ ] Set performance budgets for list/search latency and UI update time.
- [ ] Add instrumentation for event lag, dropped reconnects, and reconciliation cost.

## 8. Testing

### 8.1 Unit Tests
- [ ] Path normalization and traversal protection.
- [ ] Permission evaluation and enforcement mapping.
- [ ] Event payload serialization and schema compatibility.
- [ ] Conflict handling and optimistic state transitions.

### 8.2 Integration Tests
- [ ] End-to-end list/create/rename/move/delete/upload/download flows.
- [ ] Explicit search request flow.
- [ ] Multi-select action constraints.
- [ ] Real-time event subscription and scoped refresh behavior.
- [ ] Shared-root boundary guarantees under mixed operations.

### 8.3 Manual Validation
- [ ] Validate explorer parity UX on desktop target environment.
- [ ] Validate behavior during burst file operations.
- [ ] Validate reconnect/resubscribe behavior for real-time channel.
- [ ] Validate fallback behavior when indexing backend is unavailable.

## 9. Milestone 1 Exit Criteria

- [ ] Two-pane explorer with breadcrumbs, double-click navigation, and back/forward works.
- [ ] Action set complete for milestone 1: new folder, rename, move, delete, upload, single-file download.
- [ ] Multi-select constraints and permission-driven UI behavior are correct.
- [ ] Explicit search works without search-as-you-type.
- [ ] Real-time updates and optimistic reconciliation behave correctly.
- [ ] Incremental affected-folder refresh is working (no routine full refreshes).
- [ ] Safety requirements hold under test: root boundaries and traversal protections.
- [ ] Performance baseline and telemetry are captured for rollout decision.

## 10. Post-Milestone Backlog Candidates

- [ ] Advisory soft-lock feature for high-collision workflows.
- [ ] Additional bulk actions beyond milestone-1 set.
- [ ] Richer search UX beyond explicit-trigger baseline.
- [ ] Advanced sorting controls (name, type, size, modified date) if deferred.
- [ ] Non-Windows host support path.
- [ ] In-house indexing replacement for Everything adapter if warranted.
