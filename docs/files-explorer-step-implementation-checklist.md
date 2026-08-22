# Files Explorer Step-Based Implementation Checklist

Purpose: drive the File Explorer refactor in small, verifiable implementation slices.

Scope:
- End-user Client Files experience only.
- Windows + NTFS host target for the first implementation.
- Preserve existing server-side path safety and permission enforcement.
- Preserve Move in the backend, but defer the user-facing Move action.
- Do not introduce a core user database or add-in permission UI in this work.

## 0. Baseline And Guardrails

- [ ] Confirm the solution builds before the refactor begins.
- [ ] Confirm the existing API tests pass before the refactor begins.
- [ ] Capture the current Files page behavior for rollback comparison.
- [ ] Confirm the current shared-root path validation remains unchanged.
- [ ] Confirm the current SignalR file-event behavior remains unchanged.
- [ ] Define a manual test folder with representative files and folders.

Acceptance gate:
- Existing behavior is recorded and the baseline build/tests are green.

## 1. Confirm User-Facing Permission Scope

- [ ] Display only these five File Explorer permissions:
  - Upload
  - Download
  - Rename
  - Delete
  - New Folder
- [ ] Keep Move out of the user-facing action bar, context menu, and permission strip.
- [ ] Keep Search out of the five-action permission strip.
- [ ] Keep Read as an underlying prerequisite rather than a visible file action.
- [ ] Grant Read and Search as basic internal permissions to the User role.
- [ ] Grant Move only to the Admin role for now; keep it hidden from the User experience.
- [ ] Show every permission in the status area, whether enabled or disabled.
- [ ] Use stronger themed text for enabled permissions.
- [ ] Use light gray text for disabled permissions.
- [ ] Add tooltips for disabled permissions.
- [ ] Confirm `User` receives the five visible core permissions plus basic Read and Search permissions by default.
- [ ] Keep API permission checks authoritative regardless of UI state.

Permission policy:
- User: Read, Search, Upload, Download, Rename, Delete, New Folder.
- Admin: User permissions plus Move and other current administrative permissions.
- Visible File Explorer permission strip: Upload, Download, Rename, Delete, New Folder.

Acceptance gate:
- A standard User sees five permissions and can only invoke the actions granted by the API.

## 2. Build The Explorer Layout

- [ ] Preserve the File Explorer page-level header and existing application shell.
- [ ] Create a two-pane layout with folders on the left and contents on the right.
- [ ] Add a top action area for file upload and navigation.
- [ ] Add the file-only drop area text: `Drag and drop files or`.
- [ ] Add a multi-file `Browse` button.
- [ ] Add the explicit Search input and Search button.
- [ ] Keep the explorer content area dynamically sized.
- [ ] Set a minimum usable explorer height.
- [ ] Support vertical and horizontal scrolling without hiding the permissions area.
- [ ] Keep the layout usable at narrow desktop and mobile widths.
- [x] Confirm the first implementation slice is layout/navigation only, with existing upload/search behavior preserved temporarily.

Acceptance gate:
- The page presents a stable two-pane explorer without losing the permissions area on shorter displays.

## 3. Rework Folder Navigation

- [ ] Keep lazy-loaded folder tree behavior.
- [ ] Use `~/` for the shared-folder starting location instead of `Root`.
- [ ] Render slash separators between breadcrumb segments.
- [ ] Make completed breadcrumb segments clickable.
- [ ] Make completed breadcrumb segments look clickable only on hover/focus.
- [ ] Render the current folder as plain text.
- [ ] Keep physical Host paths out of the UI.
- [ ] Preserve the full logical path for assistive technology.
- [ ] Keep the current folder visible at all times.
- [ ] Keep the rightmost/current path visible when breadcrumbs overflow.
- [ ] Preserve Back and Forward navigation.
- [ ] Preserve single-click folder selection.
- [ ] Preserve double-click folder navigation.
- [x] Confirm breadcrumb visual overflow should keep the current/rightmost location visible.

Acceptance gate:
- A user can navigate the tree, breadcrumbs, Back, Forward, single-click, and double-click without exposing physical paths.

## 4. Simplify The File List

- [ ] Remove the visible Action column.
- [ ] Display only Name, Type, and Size columns, plus selection controls as needed.
- [ ] Display file extensions.
- [ ] Display an empty Size value for folders.
- [ ] Sort folders before files.
- [ ] Sort alphabetically within folders and files.
- [ ] Keep multi-select with Ctrl/Command and Shift behavior.
- [ ] Keep folder selection separate from double-click navigation.
- [ ] Preserve selection when unaffected file events arrive.
- [ ] Reconcile selection when selected items are renamed, deleted, or moved by another action.

Acceptance gate:
- The list resembles a simple file manager and displays no technical or redundant columns.

## 5. Implement The Five User Actions

- [ ] Keep Upload as a current-folder action.
- [ ] Keep Download as a single-file action initially.
- [ ] Keep Rename for one selected file or folder.
- [ ] Keep Delete for one or more selected files or folders.
- [ ] Keep New Folder as a current-folder action.
- [ ] Remove Move from the first user-facing action set.
- [ ] Disable Download for folders.
- [ ] Disable actions that the current user lacks permission to use.
- [ ] Provide accessible labels and tooltips for disabled actions.
- [ ] Preserve confirmation for destructive deletion.
- [ ] Preserve actionable success, failure, and conflict feedback.
- [x] Confirm New Folder should remain an inline form near the toolbar.
- [x] Confirm Delete should name one selected item or count multiple selected items, and warn when deleting folder contents.

Acceptance gate:
- Each of the five actions respects selection rules, permission state, and server-side authorization.

## 6. Add The Context Menu

- [ ] Open the context menu on right-click.
- [ ] Open the context menu with the Context Menu keyboard key.
- [ ] Open the context menu with Shift+F10.
- [ ] Select an unselected row before opening its context menu.
- [ ] Show Download, Rename, and Delete options.
- [ ] Keep New Folder outside the item context menu as a current-folder action.
- [ ] Do not show Move in the first user-facing context menu.
- [ ] Disable Download for folders.
- [ ] Disable options unavailable under the current permissions.
- [ ] Add tooltips or accessible descriptions for disabled options.
- [ ] Close the menu on outside click, Escape, or action completion.
- [ ] Do not add a touch overflow control in this milestone.
- [x] Confirm pointer context menus should open at the pointer location and keyboard context menus at the focused row.

Acceptance gate:
- Mouse and keyboard users can reach all applicable item actions without the old visible Action column.

## 7. Implement File-Only Upload

- [ ] Configure Browse for multiple files.
- [ ] Accept files from the drop area.
- [ ] Detect folders in a drop operation.
- [ ] Reject the entire drop when a folder is present.
- [ ] Do not upload any files from a rejected mixed drop.
- [ ] Show: `Folders cannot be uploaded here. Please select files instead.`
- [ ] Upload accepted files immediately into the current folder.
- [ ] Show upload progress.
- [ ] Show an upload result summary.
- [ ] Keep the current folder and selection stable during upload.
- [ ] Preserve server-side file-name and path validation.
- [ ] Keep the upload size limit enforced by the API.
- [x] Confirm multi-file uploads should use one overall progress display rather than per-file progress rows.
- [x] Confirm the first upload implementation should favor the most cross-browser-stable approach: sequential requests through the existing single-file API.
- [x] Confirm accepted files should upload immediately without a review list.

Acceptance gate:
- Multi-file Browse and file-only drag/drop work, and folder drops are rejected atomically.

## 8. Implement Explicit Search UX

- [ ] Search the entire shared folder, not only the current folder.
- [ ] Search file and folder names/extensions only.
- [ ] Start search with the Search button or Enter.
- [ ] Do not search while typing.
- [ ] Do not add debounce in the first implementation.
- [ ] Select all existing text when the search input receives focus.
- [ ] Display results in an overlay dropdown.
- [ ] Keep the dropdown from moving the explorer layout.
- [ ] Display result name, type, and relative path.
- [ ] Support Arrow Up/Down selection.
- [ ] Open the selected result with Enter.
- [ ] Close the dropdown with Escape.
- [ ] Close the dropdown when the user clicks outside it.
- [ ] Close the dropdown for empty search text.
- [ ] Preserve shared-root path restrictions for every search result.
- [x] Confirm selecting a file search result should navigate to its containing folder and select the file rather than download it.
- [x] Confirm selecting a folder search result should navigate directly into that folder.
- [x] Confirm the initial search dropdown should show up to 10 visible results with scrolling.

Acceptance gate:
- Search is explicit, keyboard accessible, overlay-based, and scoped to the shared folder.

## 9. Introduce Search Provider Boundary

- [ ] Define a provider interface for name/path search.
- [ ] Adapt the existing filesystem search to that interface.
- [ ] Keep the existing provider as the first working implementation.
- [ ] Define an Everything provider behind the same interface.
- [ ] Detect whether Everything is installed and available.
- [ ] Restrict Everything queries to the configured shared folder.
- [ ] Ensure only names and extensions are searched.
- [ ] Fall back cleanly when Everything is unavailable or degraded.
- [ ] Measure search latency before adding search-as-you-type behavior.
- [ ] Investigate bundling Everything with the installer.
- [ ] Confirm licensing, installation, update, and uninstall implications before making it required.
- [ ] Keep Everything inaccessible directly from Client devices.

Acceptance gate:
- Search works without Everything, and Everything can be added or bundled without changing the Client contract.

## 10. Real-Time Reconciliation

- [ ] Refresh only affected folders after file events.
- [ ] Preserve the current folder when unrelated events arrive.
- [ ] Reconcile optimistic actions using correlation IDs.
- [ ] Handle deleted selected items gracefully.
- [ ] Handle renamed selected items gracefully.
- [ ] Handle changed folder contents without resetting unrelated UI state.
- [ ] Preserve the search dropdown and search state appropriately during events.
- [ ] Document event ordering assumptions.

Acceptance gate:
- Real-time updates do not cause broad page refreshes or unexpected selection/navigation loss.

## 11. Accessibility And Responsive Validation

- [ ] Verify all controls have accessible names.
- [ ] Verify keyboard navigation through toolbar, tree, list, breadcrumbs, and context menu.
- [ ] Verify focus is visible on interactive elements.
- [ ] Verify disabled permissions and actions are distinguishable without relying only on color.
- [ ] Verify tooltips/descriptions are available for disabled actions.
- [ ] Verify long names and paths do not overlap adjacent controls.
- [ ] Verify breadcrumb overflow keeps the current location visible.
- [ ] Verify the explorer works at narrow widths.
- [ ] Verify the permissions area remains visible on short screens.

Acceptance gate:
- The first implementation is usable with keyboard navigation and common narrow-window sizes.

## 12. Tests And Release Gate

- [ ] Add API tests for the five action permissions.
- [ ] Add API tests proving Move is still available only to backend-supported callers.
- [ ] Add tests for shared-root traversal protection.
- [ ] Add tests for file-only upload rejection.
- [ ] Add tests for multi-file upload behavior.
- [ ] Add tests for folder-first alphabetical sorting.
- [ ] Add tests for empty folder sizes.
- [ ] Add tests for search scoping to the shared folder.
- [ ] Add tests for search provider fallback.
- [ ] Add tests for context-menu selection behavior.
- [ ] Add tests for event reconciliation where practical.
- [ ] Run the full solution build.
- [ ] Run the full solution test suite.
- [ ] Complete manual desktop validation.
- [ ] Complete manual keyboard validation.
- [ ] Record Everything packaging results.

Final acceptance gate:
- The File Explorer matches the confirmed UX decisions, all server-side safety checks remain active, and build/tests/manual validation are complete.
