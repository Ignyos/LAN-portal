# CI/CD Lane Runbook Index

This index maps existing validation runbooks to the canonical operator lanes:

- Build
- Publish-dev
- Publish-live

Use this file as the first stop before running validation.

Final cutover runbook:
- `.github/runbooks/cicd-operator-runbook.md`

## Build Lane

Purpose:
- Verify compile and test gates for pull requests and main pushes.

Primary automation:
- `.github/workflows/ci.yml`

Runbooks:
- (No separate lane-specific runbook yet; CI logs are the source of truth.)

## Publish-dev Lane

Purpose:
- Rehearse release preparation and non-production verification without production publish side effects.

Primary entry points:
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

Escalation:
- Release preparation gate issues: Release Lead
- Workflow or permissions issues: DevOps Engineer
- Validation evidence gaps: QA Lead

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
