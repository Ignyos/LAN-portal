# Publish Lanes Vision Handoff

Purpose:
- Preserve the active CI/CD direction so a new session can resume immediately.
- Define exact lane behavior for `Publish-dev` and `Publish-live`.

When asked "What's next?":
- Continue implementing the lane contract in this file.
- Do not redefine requirements unless explicitly requested.

## Required Lane Contract

### 1) Publish-dev (developer iteration lane)

Scope:
- Branch: `dev` only.
- Release notes: not required.
- Outcome: trigger a full GitHub Actions dev workflow.

Behavior:
- Script entry point commits to `dev` and pushes `dev`.
- Version identity for dev builds is `n.n.n.r` where `r` is a non-zero build number.
- Default suggested dev version is `n.n.n.(r+1)` using the most recent build on the same `n.n.n` core.
- Leading zeros are not required for the build component.
- Build can happen locally or on GitHub, but dev workflow must publish the developer installer lane outputs.
- Dev artifacts/manifests are published only to LAN-portal-dev destinations.
- LAN-portal-dev is reserved for dev releases only and must never be used for production releases.

Versioning rule:
- A non-zero fourth node is developer lane identity.

### 2) Publish-live (public release lane)

Scope:
- Branch: `main` only.
- Release notes: required.
- Outcome: trigger a full GitHub Actions live workflow.

Behavior:
- Script entry point commits to `main` and pushes `main`.
- Default live version is `n.n.(patch+1).0` with user override allowed for `n.n.n`.
- Live builds always use fourth node `0`.
- Any value like `1.2.3.202608071253` is never a production release.
- Live artifacts/manifests are published to LAN-portal production destinations.

Versioning rule:
- Fourth node `0` is production lane identity.

## Technical Constraints To Preserve

- Keep assembly version (`<Version>`) as the source of truth for lane identity and application behavior.
- Use four-node assembly versions only: `n.n.n.0` for live and `n.n.n.r` (`r > 0`) for dev.
- Keep build-component values within valid .NET numeric bounds.
- Use the build node to determine update source selection and download location in the application.
- Use the build node to determine how the application displays its version:
  - `0` -> production style display
  - non-zero -> developer style display with the build node visible

## Workflow Trigger Intent

- Dev lane should trigger from dev publish action (push to `dev` and/or explicit dev workflow trigger as finalized).
- Live lane should trigger from live release action (main publish path and tag-driven release workflow).
- The implementation must ensure a new qualifying trigger event is emitted for each intended run.

## Current In-Progress Work Items

- [ ] Split publish responsibilities cleanly so `publish-dev` is no longer a thin wrapper around live semantics.
- [ ] Keep branch gates strict: `dev` for dev lane, `main` for live lane.
- [ ] Enforce lane version rules:
  - [ ] Dev defaults to `n.n.n.(r+1)`.
  - [ ] Live enforces `n.n.n.0`.
- [ ] Keep live release-notes gate in `Publish-live` only.
- [ ] Ensure dev workflow publishes dev installer/manifests to LAN-portal-dev outputs.
- [ ] Ensure live workflow publishes production installer/manifests to LAN-portal outputs.
- [ ] Validate tag/push trigger behavior so expected GitHub run is always created.

## Acceptance Criteria

- `Publish-dev` on `dev` creates/pushes a dev commit and starts the intended dev lane workflow.
- `Publish-live` on `main` requires release notes, creates/pushes a live commit, and starts the intended live lane workflow.
- Dev builds resolve as developer lane (`fourth node != 0`).
- Live builds resolve as production lane (`fourth node == 0`).
- Installer/update outputs are published to the correct lane destinations.
- LAN-portal-dev is used only for developer releases.
- The application uses the build node to decide whether to check/download production or dev updates.
- The application uses the build node to decide whether to show the production version or the full developer version.

## Operator Quick Checks

1. Dev lane quick check:
- Run `Publish-dev` from `dev`.
- Confirm workflow starts and default version format is `n.n.n.(r+1)` with a non-zero build component.
- Confirm dev installer and manifest endpoints are LAN-portal-dev only.
- Confirm the app treats the build node as the lane selector for update checks, downloads, and displayed version.

2. Live lane quick check:
- Run `Publish-live` from `main`.
- Confirm release notes are required.
- Confirm resulting version is `n.n.n.0`.
- Confirm production installer and manifest endpoints are LAN-portal.

## Canonical References

- Lane index: `.github/runbooks/cicd-lane-index.md`
- CI/CD roadmap: `.github/roadmaps/cicd-roadmap.md`
- Release notes style: `.github/release/release-notes-style-guide.md`
- Release notes output: `.github/release/release-notes.md`
