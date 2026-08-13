# Files Explorer Decision Checklist

Purpose: review the Files explorer redesign and indexing direction one decision at a time before creating an implementation backlog.

How to use this file:
- Work top to bottom.
- Mark each item once the decision is understood and accepted.
- Add notes under any item where you want alternatives, constraints, or follow-up questions captured.

## 1. Scope And Milestone Shape

- [x] Confirm the first milestone is focused on the end-user Files experience only.
- [x] Confirm this milestone does not redesign unrelated pages.
- [x] Confirm Windows + NTFS is the initial supported host platform.
- [x] Confirm non-Windows support is deferred, not rejected.

Notes:

## 2. User Experience Goals

- [x] Confirm the Files page should feel similar to Windows File Explorer.
- [x] Confirm the layout should be two-pane: folder tree on the left and folder contents on the right.
- [x] Confirm breadcrumbs should appear whenever the current folder is not the root shared folder.
- [x] Confirm double-clicking a folder should navigate into that folder.
- [x] Confirm back and forward navigation should be part of the first design.
- [x] Confirm the current path should always be visible.

Notes:

## 3. First-Pass Actions In The Explorer

- [x] Confirm users should be able to create a new folder.
- [x] Confirm users should be able to rename items.
- [x] Confirm users should be able to delete items.
- [x] Confirm users should be able to upload files into the current folder.
- [x] Confirm drag-and-drop upload is part of the desired experience.
- [x] Confirm move is part of the intended action set for the first implementation.

Notes:

## 4. Multi-Select Rules

- [x] Confirm multi-select is part of the first Files milestone.
- [x] Confirm the action bar should react to both selection shape and permissions.
- [x] Confirm rename should be single-item only.
- [x] Confirm delete should support multi-select.
- [x] Confirm move should support multi-select.
- [x] Confirm new folder is a current-folder action, not a selected-item action.
- [x] Confirm upload is a current-folder action, not a selected-item action.
- [x] Confirm download can stay single-file only in the first pass.

Open question:
- [x] Decide whether any additional multi-select actions belong in the first milestone. Decision: no additional multi-select actions in milestone 1.

Notes:

## 5. Permissions Model Direction

- [x] Confirm coarse roles and fine-grained permissions should remain separate concepts.
- [x] Confirm the UI should be designed around action-specific permissions such as `file:add`, `file:rename`, `file:move`, and `file:delete`.
- [x] Confirm permission keys should be namespaced so future add-ons can participate cleanly.
- [x] Confirm the first implementation should be compatible with future fine-grained file permissions even if the current role model is simpler.
- [x] Confirm the server-side persistence model should be the source of truth for permissions.
- [x] Confirm JWT claims should represent a snapshot of effective permissions for the current session, not the only source of truth.
- [x] Confirm permissions in JWTs should be emitted as repeated claims rather than a single CSV permission string.
- [x] Confirm coarse roles may still appear in JWT claims, but add-ons should depend on permissions rather than hardcoded built-in roles.
- [x] Confirm the server must enforce permissions regardless of what the UI shows.
- [x] Confirm the UI may hide or disable actions depending on grants.

Recommended direction to review:
- Persist coarse roles separately from fine-grained permissions.
- Persist fine-grained permissions server-side in a normalized form that can evolve later.
- Use repeated permission claims such as `perm=file:read` and `perm=file:move` in JWTs.
- Reserve roles for broad identity categories such as `User` and `Admin`.
- Design future add-ons to register or depend on namespaced permissions instead of built-in roles.

Notes:

## 6. Search Behavior

- [x] Confirm search should be explicitly started by the user.
- [x] Confirm search-as-you-type is not desired.
- [x] Confirm search can be part of the overall contract even if the richer UX arrives after core explorer parity.

Notes:

## 7. Real-Time Update Strategy

- [x] Confirm the client should receive server-side file changes quickly.
- [x] Confirm WebSocket-style updates are desired for this feature set.
- [x] Confirm optimistic UI updates with reconcile-on-event is the preferred interaction model.
- [x] Confirm the client should refresh only affected folders when possible, not the whole explorer.

Notes:

## 8. Indexing Strategy

- [x] Confirm the server should avoid repeated full recursive scans during normal use.
- [x] Confirm an indexed lookup model is preferred over live full-directory enumeration for the long term.
- [x] Confirm Everything by Voidtools is the preferred indexing engine to evaluate first.
- [x] Confirm the index must still be scoped strictly to the configured storage root.

Notes:

## 9. Everything Dependency Decision

- [x] Confirm Everything should be required only if it can be packaged or installed reliably with the product installer.
- [x] Confirm that if packaging is awkward or unreliable, we should revisit the dependency plan instead of forcing it.
- [x] Confirm fallback behavior should be discussed if Everything cannot be made a clean required dependency.

Notes:
- We can also consider building our own library or service worker to replace Everything by Voidtools if that path becomes a better fit.

## 10. Host Change Detection

- [x] Confirm a worker-style background service is the right place to detect filesystem changes.
- [x] Confirm the worker should prefer the least expensive update needed instead of forcing broad rescans.
- [x] Confirm change batching or debounce behavior is acceptable for bursty file operations.
- [x] Confirm the design should distinguish between single-file updates and broader folder refreshes.

Notes:

## 11. API Shape Expectations

- [x] Confirm the API should support folder listing by current path.
- [x] Confirm the API should support lazy loading of tree nodes.
- [x] Confirm the API should support explicit search requests.
- [x] Confirm the API should support create folder, rename, move, delete, upload, and download.
- [x] Confirm path values should be normalized relative paths under the shared root.

Notes:

## 12. Event Contract Expectations

- [x] Confirm the client should subscribe to file-change events for relevant paths.
- [x] Confirm event types should distinguish created, updated, deleted, renamed, moved, and batch changes.
- [x] Confirm the event payload should support optimistic reconciliation using a correlation identifier or equivalent.

Notes:
- Locking question: do not require hard file/folder locks in milestone 1.
- Recommended approach for milestone 1: optimistic concurrency + reconcile-on-event + conflict responses.
- Later feature candidate: advisory "soft lock" (checkout/edit intent) for high-collision workflows.

## 13. Performance And Safety Expectations

- [x] Confirm path traversal protection must remain strict regardless of indexing approach.
- [x] Confirm the system must not expose files outside the configured shared root.
- [x] Confirm the design should optimize for large folder trees and large file collections.
- [x] Confirm reduced scanning overhead is a key success metric.

Notes:

## 14. First-Milestone Nice-To-Haves vs Must-Haves

Must-haves to confirm:
- [x] Two-pane explorer layout
- [x] Breadcrumbs
- [x] Double-click folder navigation
- [x] Back and forward navigation
- [x] New folder
- [x] Rename (single-item)
- [x] Delete (supports multi-select)
- [x] Move (supports multi-select)
- [x] Upload into current folder
- [x] Drag/drop upload
- [x] Download (single-file in first pass)
- [x] Multi-select with constrained actions
- [x] Explicit search capability
- [x] Real-time update notifications
- [x] Incremental refresh of affected folders (not full explorer refresh)
- [x] Strict shared-root path safety and traversal protection

Nice-to-haves to confirm:
- [x] Advanced sorting controls by name, type, size, and modified date can wait if needed.
- [x] Additional bulk actions can wait if needed.
- [x] Richer search UX beyond explicit-trigger search can wait.

Notes:
- This section reflects accepted decisions from Sections 1-13.
- Locking remains a later-feature candidate (advisory soft lock), not a milestone-1 requirement.

## 15. Readiness Before Implementation Checklist

- [x] We understand the intended end-user workflow.
- [x] We understand which actions are single-item versus multi-item.
- [x] We understand the permission direction.
- [x] We understand the platform assumption.
- [x] We understand the Everything dependency decision.
- [x] We understand the real-time update direction.
- [x] We understand what belongs in milestone 1 versus later milestones.

Notes:
- Readiness gate complete. Next step: create the implementation checklist document.

Once this section is fully checked, the next document should be an implementation checklist.