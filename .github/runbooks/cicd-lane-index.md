# CI/CD Lane Runbook Index

This index maps existing validation runbooks to the canonical operator lanes:

- Build
- Publish-dev
- Publish-live

Use this file as the first stop before running validation.

## Current Direction (Session Handoff)

- Current in-progress implementation contract: `.github/runbooks/publish-lanes-vision-handoff.md`
- Use that handoff file as the first reference when a new session asks "What's next?"

## Build Lane

Purpose:
- Verify compile and test gates for pull requests and main pushes.

Primary automation:
- `.github/workflows/ci.yml`

Runbooks:
- (No separate lane-specific runbook yet; CI logs are the source of truth.)

## Publish-dev Lane

Purpose:
- Execute fast developer iterations on `dev` with full dev workflow outputs and no production publish side effects.

Versioning contract:
- Dev lane uses `n.n.n.r` where `r > 0`.
- Default suggestion is `n.n.n.(r+1)` from the latest build on the same core version.
- Leading zeros are formatting only and are not required.
- LAN-portal-dev is reserved for dev releases only.
- The build node selects the update source/download lane and the version style shown in the app.

Current entry points:
- VS Code Run and Debug: `Publish-dev`
- Script: `./scripts/publish-dev.ps1`
- Workflow: `.github/workflows/publish-dev.yml`

Runbooks:
- `.github/runbooks/stage2-validation.md` (manual workflow_dispatch rehearsal)
- `.github/runbooks/stage4-validation.md` (test channel behavior and prerelease verification)
- `.github/runbooks/stage5-validation.md` (test-channel safety validation)
- `.github/runbooks/cicd-non-regression-matrix.md` (Stage 6B parity matrix and rollback policy)

## Publish-live Lane

Purpose:
- Execute tagged release automation with artifact packaging, installer output, checksums, and manifest publishing.

Primary automation:
- `.github/workflows/release-artifacts.yml`

Runbooks:
- `.github/runbooks/stage2-validation.md`
- `.github/runbooks/stage3-validation.md`
- `.github/runbooks/stage4-validation.md`
- `.github/runbooks/stage5-validation.md`
- `.github/runbooks/stage5-validation-evidence-template.md`
- `.github/runbooks/cicd-non-regression-matrix.md`

## Trigger-Type Matrix

- Manual trigger (`workflow_dispatch`): Publish-dev lane artifact rehearsal via `.github/workflows/publish-dev.yml`.
- Branch push (`main`): Build lane CI gates.
- Tag push (`v*`): Publish-live release artifact and manifest pipeline.

## Source-of-Truth Notes

- Canonical release notes paths:
  - `.github/release/release-notes-style-guide.md`
  - `.github/release/release-notes.md`
- `DevOps_CICD_EXAMPLE` remains a template reference and does not override operational paths.
