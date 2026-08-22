# Client Navigation Implementation Checklist

Purpose: implement the accepted end-user navigation and authentication UX for the client-side portal.

Product goal: make LAN Portal immediately understandable for users who have little technical knowledge. The default entry point should lead users to the right next action without changing the core menu based on authentication state.

## Accepted Direction

- [x] Keep the main client menu stable for all users:
  - Home
  - Files
  - Account
  - Admin (administrators only)
- [ ] Make Account the single user-facing access and session-management page.
- [ ] Show the access-request workflow on Account when signed out.
- [ ] Show identity, session details, and sign-out on Account when signed in.
- [x] Keep Files visible when signed out, but make it visually unavailable.
- [x] When signed-out users activate Files, keep them on the current page and direct their attention to Account.
- [x] Use the message: `Start by requesting access in Account.`
- [x] Make `/` the conditional entry point:
  - signed out -> `/account`
  - signed in -> `/files`
- [x] Keep the informational Home page available at `/home`.
- [x] Change QR codes and shared URLs to point to `/`.
- [ ] Make `/account` the canonical user-facing route for access requests.
- [ ] Remove Login from visible product language and navigation.

## 1. Route And Entry-Point Design

- [ ] Decide whether the root conditional behavior is implemented by a route component or redirect logic at the current Home route.
- [x] Add or preserve an explicit `/home` route for the informational Home page.
- [x] Define behavior while session state is still loading so the root does not briefly navigate to the wrong destination.
- [ ] Preserve safe `returnUrl` values when unauthenticated users are sent to Account.
- [x] Confirm the authenticated root destination is `/files` regardless of the last visited path, unless a later product decision changes this.
- [x] Confirm the unauthenticated root destination is `/account`.
- [x] Decide whether `/login` should be removed or become a redirect to `/account` during cleanup. `/login` remains as a compatibility redirect.

## 2. Account Page

### Signed-Out State

- [ ] Move the current Login page request workflow into Account.
- [ ] Change the page title and heading to Account / Request access language.
- [ ] Use clear introductory copy, such as:
  - `Request access to {networkName}`
  - `Enter your name and device to request access from the host.`
- [ ] Rename the primary action from `Request Login` to `Request Access`.
- [ ] Preserve device detection and the existing approval polling workflow.
- [ ] Preserve request status, request ID, user code, and expiration details.
- [ ] Preserve safe return navigation to the originally requested page after approval.
- [ ] Ensure the signed-out page does not present as an empty account profile with a buried login action.

### Signed-In State

- [ ] Preserve the current account details display.
- [ ] Preserve role and session-expiration information.
- [ ] Preserve the sign-out action.
- [ ] Keep Account as the stable destination after sign-out and when access is required.

## 3. Stable Navigation

- [x] Always render Home, Files, and Account in the main client menu.
- [x] Keep Admin conditional on the existing administrator role check.
- [x] Remove the authentication-based Login/Account menu swap.
- [x] Update active-link styling so Account is correctly highlighted on `/account`.
- [x] Ensure Home points to `/home` rather than the conditional `/` route.

## 4. Signed-Out Files Interaction

- [x] Keep Files visible when signed out.
- [x] Prevent navigation to `/files` while signed out.
- [x] Use a visually unavailable treatment that remains keyboard accessible.
- [x] Add `aria-disabled="true"` and an appropriate accessible label or description.
- [x] Do not use a native disabled control that cannot receive focus or explain the reason.
- [x] On click or keyboard activation:
  - keep the current page unchanged
  - show `Start by requesting access in Account.`
  - apply a short attention treatment to Account
- [x] Make the attention treatment brief and non-looping.
- [x] Support `prefers-reduced-motion` by removing or simplifying the animation.
- [ ] Ensure the message is readable on narrow screens and does not overlap the menu.
- [ ] Ensure the interaction works whether the user is on Home, Account, or another public page.

## 5. QR Codes And Shared URLs

- [x] Update the host-generated guest URL from `/login` to `/`.
- [x] Update the client Home page share URL from `/login` to `/`.
- [x] Update QR-code accompanying text to describe the portal entry point rather than a Login page.
- [ ] Confirm a signed-out scan lands on Account.
- [ ] Confirm a signed-in scan lands on Files.
- [ ] Confirm development and production URL/port behavior remains correct.
- [x] Search the repository for remaining user-facing `/login` share links and update them. Remaining `/login` references are compatibility or internal API names.

## 6. Terminology Cleanup

- [x] Replace visible `Login` labels with `Account`, `Request access`, or `Access request` as appropriate.
- [ ] Keep technical method and API names unchanged unless a separate cleanup is useful.
- [x] Update user-facing status messages to match the access-request model.
- [x] Update Home instructions and QR descriptions.
- [ ] Update relevant documentation and release notes if this ships as a user-visible change.

## 7. Validation Matrix

### Unauthenticated User

- [ ] Opening `/` navigates to `/account`.
- [ ] Opening `/home` shows the informational Home page.
- [ ] Opening `/account` shows the Request access experience.
- [ ] Files is visible but does not navigate to `/files`.
- [ ] Activating Files shows the guidance message and highlights Account.
- [ ] The guidance interaction works with keyboard navigation.
- [ ] The guidance interaction respects reduced-motion preferences.
- [ ] Requesting access and receiving approval navigates to the intended return path.

### Authenticated User

- [ ] Opening `/` navigates to `/files`.
- [ ] Opening `/home` shows the informational Home page.
- [ ] Files navigates normally.
- [ ] Account shows session details and sign-out.
- [ ] Signing out returns to Account in the signed-out state.
- [ ] Admin appears only for administrators.

### QR And Link Entry

- [ ] The host QR code points to `/`.
- [ ] The client-generated QR code points to `/`.
- [ ] Signed-out QR entry reaches Account.
- [ ] Signed-in QR entry reaches Files.

## 8. Quality Gates

- [ ] Add or update focused component tests for route and menu behavior where the project test setup supports them.
- [x] Run the client build.
- [x] Run the relevant automated tests. API suite: 5 passed, 0 failed.
- [ ] Manually validate desktop and narrow/mobile layouts.
- [ ] Manually validate keyboard focus and activation for the signed-out Files item.
- [ ] Manually validate reduced-motion behavior.
- [ ] Search for stale visible Login terminology and old QR destinations.
- [ ] Confirm no unrelated host or API behavior changed.

## Suggested Implementation Slices

1. **Canonical Account experience**: move the request workflow and update signed-out copy. (Completed)
  - Account now owns the request-access form, device suggestion, approval polling, and return-path handling.
  - `/login` remains as a compatibility route and redirects to `/account` while preserving query parameters.
2. **Stable menu**: always show Home, Files, and Account; keep Admin conditional. (Completed)
  - Home remains on `/` until the explicit `/home` route is introduced with the conditional root behavior.
3. **Files guidance interaction**: visually unavailable Files item, message, Account attention treatment, and reduced-motion support. (Completed)
  - Signed-out Files is an accessible button that does not navigate.
  - Account receives a four-second guidance callout and one-shot attention treatment.
4. **Conditional root and explicit Home route**: `/` to Account or Files, `/home` for informational content. (Completed)
  - The root waits for client session initialization before navigating.
  - Both client and host-generated share URLs now target `/`.
5. **QR/link update**: point all default share destinations to `/`.
6. **Terminology and validation**: remove visible Login language, test the state matrix, and close quality gates. (Completed for implementation and automated checks)
  - Visible client and host wording now uses Account, access request, or portal language.
  - Protected-page redirects now target `/account?returnUrl=...`.
  - `/login` remains only as a compatibility route and in internal auth/API identifiers.
