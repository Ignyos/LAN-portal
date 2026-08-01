# Public Site Roadmap

## Goal

Split the public-facing GitHub Pages site into a separate repository so the main application repo can stay focused on app code, release automation, and internal docs.

This roadmap is a prerequisite to `v1.0.0`.

## Why this exists

- Keep public pages clean and limited to end-user content.
- Avoid exposing developer-only release notes, runbooks, and automation details on the public site.
- Provide a dedicated place for test installers, staging download links, and temporary release notes.
- Preserve the main repo as the source of truth for application code, workflows, scripts, artifacts, and developer documentation.

## Target repo split

- Main repo: application source, GitHub Actions, scripts, artifacts, release automation, internal docs.
- Public site repo: generated Pages content only.
- Optional test site repo: same pattern for staging/test content if the public site needs a separate sandbox.

## Roadmap checklist

- [ ] Define the exact public site repository name and GitHub Pages source branch.
- [ ] Decide whether the test site repo is required or whether one public site repo is enough.
- [ ] Move public-facing Pages content into generated site output only.
- [ ] Keep developer-only release notes and runbooks out of the public site repo.
- [ ] Publish dev/test installer links from the separate site repo.
- [ ] Publish temporary release notes only in the staging/test site surface.
- [ ] Keep production release notes and installer links separate from staging content.
- [ ] Update release workflows so tagged builds can deploy site output to the site repo.
- [ ] Confirm the public site can be rebuilt from generated artifacts only.
- [ ] Treat this split as a prerequisite gate before `v1.0.0`.

## Suggested phases

### Phase 1: Site boundary

- Freeze the current public site shape.
- Decide what belongs on the public site and what stays internal.
- Create the public site repo scaffold.

### Phase 2: Staging flow

- Publish dev/test site output from the main repo to the separate site repo.
- Add temporary download and release-note surfaces for validation.
- Verify the staging site does not expose internal documentation.

### Phase 3: Public release flow

- Promote the site repo to the official public Pages target.
- Publish end-user docs, download links, and troubleshooting content only.
- Remove any staging-only content from the public surface.

### Phase 4: v1.0.0 gate

- Require the public site split to be complete before `v1.0.0`.
- Verify the public site and the app release workflow are decoupled cleanly.
- Confirm the site repo can host the final public installer and support content.

## Notes

- This roadmap is intentionally about the website boundary, not splitting the application into multiple code repositories.
- The application repo should remain the release and automation source of truth until the public site is stable.