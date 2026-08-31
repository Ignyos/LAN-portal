# Mobile-Friendly Client Implementation Checklist

Purpose: make every page in the `Ignyos.LanPortal.Web` client usable on phone-sized screens without changing the accepted navigation, wording, or workflow decisions already recorded in the [Client Navigation Implementation Checklist](client-navigation-implementation-checklist.md).

Product goal: a user should be able to complete access requests, file browsing, uploads, downloads, and account management comfortably on a phone, using only touch input, without horizontal scrolling or unreadable controls.

Scope: this covers only the Blazor Server client pages (Home, Account, Files, Admin). The Host's local setup/advanced pages are out of scope for this checklist; they are tracked separately under the controller-owned HTML extraction work in [Product Roadmap](product-roadmap.md).

## 1. Baseline Audit

- [ ] Inventory every client route reachable by an end user on a phone: Home, Account, Files, Admin.
- [ ] Record current behavior at common phone widths (360px, 390px, 430px) and one small-tablet width (768px).
- [ ] Identify existing responsive groundwork already in `app.css` (for example the `640px` and `900px` breakpoints) so new work extends it instead of conflicting with it.
- [ ] Identify all elements that currently force horizontal scrolling on narrow viewports (fixed `min-width` panels, wide tables, side-by-side grids).
- [ ] Identify controls that are too small or too close together for touch (buttons, icon actions, table row actions, context menu items).

## 2. Layout And Navigation

- [ ] Verify the main nav collapses to its mobile toggle correctly and remains reachable on every page.
- [ ] Verify page headers, panels, and cards reflow to a single column on narrow viewports.
- [ ] Verify the Files explorer's tree pane and folder view stack instead of forcing a wide, scrollable grid.
- [ ] Verify tables (Admin) present usable content on narrow viewports, either by reflowing key columns or providing an explicit, clearly-signposted horizontal scroll region.
- [ ] Verify modal dialogs (transfer progress, confirmation, context menus) fit within the viewport without clipping and without requiring horizontal scrolling.

## 3. Touch Interaction

- [ ] Verify all interactive controls meet a minimum comfortable touch target size.
- [ ] Verify drag-and-drop file upload has a usable non-drag fallback on touch devices (browse/select button is already required; confirm it remains the primary path on touch).
- [ ] Verify context menus and row selection work with tap and long-press equivalents, not only mouse hover/right-click.
- [ ] Verify the upload progress modal's cancel action and per-file list are usable at phone width.
- [x] Known limitation, deferred out of `v1.0.0.0` scope: mobile multi-file selection via the browser's native picker is unreliable.
   - reproduced on Android: selecting multiple files sometimes uploads correctly, sometimes triggers Blazor Server's "Rejoining server..." reconnect overlay, and sometimes silently does nothing
   - the "Rejoining server..." overlay confirms the SignalR circuit disconnects while the OS file/photo picker is in the foreground; a longer multi-select interaction increases the odds of hitting this
   - single-file selection is unaffected because the picker interaction is short enough to avoid the disconnect
   - a reliable fix likely requires a native companion app or a non-Blazor-Server upload entry point, not a markup or event-binding change
   - no further code changes planned against this for `v1.0.0.0`; revisit if a native app or alternate hosting model is scoped later

## 4. Content And Typography

- [ ] Verify body text, labels, and status messages remain readable without manual zoom at phone widths.
- [ ] Verify long file and folder names truncate predictably instead of breaking layout.
- [ ] Verify toast notifications position and size correctly on narrow viewports and do not block key controls.

## 5. Validation

- [ ] Manually test each in-scope page on at least one real phone browser (iOS Safari and Android Chrome) in addition to desktop responsive-mode emulation.
- [ ] Re-run the existing automated test suite after markup changes to confirm no regressions.
- [ ] Document any known remaining limitations before considering this checklist complete.
