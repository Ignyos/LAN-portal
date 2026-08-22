# Admin Active Users Implementation Checklist

Purpose: simplify the active-user management experience on both the Client and Host Admin surfaces, one logical component at a time.

This is a one-time implementation checklist. Remove or archive it after the feature is complete and the final behavior is documented elsewhere if needed.

## Accepted Product Direction

- [ ] Rename the active-user section to `Manage Active Users`.
- [ ] Keep the `User` and `Device` columns.
- [ ] Replace `Roles` with `Admin`.
- [ ] Show an `Admin` badge only when the user has the `Admin` role.
- [ ] Replace text action buttons with compact icon actions.
- [ ] Add an `Edit` icon that opens a user-details modal.
- [ ] Add a `Revoke` icon that removes one user's access.
- [ ] Keep revoke-by-filter controls out of the UI.
- [ ] Preserve the broader RBAC design: the Admin checkbox controls only the existing Admin role; future granular File Sharing permissions get their own controls.

## User Details Modal

- [ ] Show the selected User and Device.
- [ ] Show `Access Granted`.
- [ ] Show `Access Expires`.
- [ ] Provide an Admin checkbox.
- [ ] Preserve all non-Admin roles when the Admin checkbox changes.
- [ ] Use explicit `Save` and `Cancel` actions.
- [ ] Prevent duplicate submissions while saving.
- [ ] Show actionable success and error feedback.

## Revoke Behavior

### Client

- [ ] Use an icon button with an accessible label and tooltip.
- [ ] Open an inline confirmation modal before revoking.
- [ ] Identify the User and Device in the confirmation.
- [ ] Use a stronger self-revocation warning for the current user.
- [ ] On confirmed self-revocation, clear the local session and navigate to Account.
- [ ] On confirmed revoke of another user, refresh the active-user list and show status.

### Host

- [ ] Use an icon button with an accessible label and tooltip.
- [ ] Revoke immediately without a confirmation dialog.
- [ ] Refresh the active-user list and show status.
- [ ] Preserve current-session handling already supported by the Host page.

## Implementation Slices

1. **Client table structure** (Completed)
   - [x] Rename the section and columns.
   - [x] Add the Admin badge.
   - [x] Replace text actions with Edit and Revoke icon buttons.
   - [x] Keep existing actions wired until the modal actions replace them.
   - [x] Wire the Edit icon to the Edit modal flow.
   - [x] Replace direct Revoke icon wiring with the Revoke confirmation modal.

2. **Client Edit modal** (Completed)
   - [x] Add selected-user state and modal visibility.
   - [x] Add access details and Admin checkbox.
   - [x] Save through the existing role-update API.
   - [x] Preserve non-Admin roles.
   - [x] Add explicit Save and Cancel actions.
   - [x] Add final modal accessibility and responsive polish.

3. **Client Revoke confirmation** (Completed)
   - [x] Add confirmation modal and self-revocation warning.
   - [x] Wire confirmed revoke and refresh behavior.

4. **Host table structure** (Completed when applicable in the local admin page)
   - [x] Rename the section and columns.
   - [x] Add the Admin badge.
   - [x] Replace text actions with Edit and Revoke icon buttons.

5. **Host Edit modal** (Completed in the local admin page)
   - [x] Add generated HTML/JavaScript modal behavior.
   - [x] Add access details and Admin checkbox.
   - [x] Save through the existing role-update API.

6. **Host Revoke action** (Completed in the local admin page)
   - [x] Add immediate individual revoke behavior.
   - [x] Refresh the list and preserve current-session behavior.

7. **Accessibility and responsive pass** (Completed for the implemented UI)
   - [x] Add tooltips and accessible labels.
   - [x] Support keyboard activation and Escape-to-close.
   - [x] Manage modal focus.
   - [x] Verify narrow-width table and modal behavior.
   - [x] Respect reduced-motion preferences.

8. **Validation and cleanup**
   - [x] Verify ordinary-user edit.
   - [x] Verify Admin promotion and removal.
   - [x] Verify preservation of other roles.
   - [x] Verify client self-revocation.
   - [x] Verify Host self-revocation.
   - [x] Verify cancellation and error states.
   - [x] Run the full solution build and relevant tests.
   - [x] Remove or archive this one-time checklist.
