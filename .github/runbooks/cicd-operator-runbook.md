# CI/CD Operator Runbook

This runbook is the canonical operator guide for the `Build`, `Publish-dev`, and `Publish-live` lanes after Stage 6 cutover.

## Lane Summary

- `Build`: CI restore, build, and test validation.
- `Publish-dev`: dry-run release preparation and manual artifact rehearsal without production side effects.
- `Publish-live`: tagged production or test release automation for artifacts, installer, checksums, manifests, and GitHub Releases.

## Operator Entry Points

### Build

- VS Code Run and Debug: `Build`
- Workflow: `.github/workflows/ci.yml`

Use when:
- Validating compile/test health on branch or main changes.

### Publish-dev

- VS Code Run and Debug: `Publish-dev`
- Script: `./scripts/publish-dev.ps1`
- Workflow: `.github/workflows/publish-dev.yml`

Use when:
- Generating release prep artifacts locally without commit, tag, or push.
- Rehearsing API/Web/Host artifact packaging from GitHub Actions.

Expected behavior:
- Enforces the same local release gates through `publish-live.ps1` in dry-run mode.
- Does not commit, tag, or push.
- GitHub workflow supports `include_host=true` for Host artifact rehearsal.

### Publish-live

- VS Code Run and Debug: `Publish-live`
- Script: `./scripts/publish-live.ps1`
- Workflow: `.github/workflows/release-artifacts.yml`

Use when:
- Preparing a real release locally.
- Executing tagged release automation in GitHub Actions.

Expected behavior:
- Local script enforces branch, clean-tree, sync, SemVer, tag-collision, release-notes, and final approval gates.
- Tag pushes run remote artifact packaging, installer generation, checksum generation, manifest publishing, and GitHub Release attachment.

## Standard Release Sequence

1. Validate `Build` lane is green on the intended source branch.
2. Use `Publish-dev` locally for dry-run release preparation if needed.
3. Use `.github/workflows/publish-dev.yml` when GitHub-hosted artifact rehearsal is needed.
4. Run `Publish-live` locally to prepare and approve the release commit and tag.
5. Push the tagged commit.
6. Verify `.github/workflows/release-artifacts.yml` succeeds.
7. Verify the GitHub Release assets and the correct manifest file (`manifest.json` or `manifest-test.json`).

## Verification Sources

- CI/CD parity evidence: `.github/runbooks/cicd-non-regression-matrix.md`
- Lane-to-runbook map: `.github/runbooks/cicd-lane-index.md`
- Stage validation runbooks:
  - `.github/runbooks/stage2-validation.md`
  - `.github/runbooks/stage3-validation.md`
  - `.github/runbooks/stage4-validation.md`
  - `.github/runbooks/stage5-validation.md`

## Escalation Paths

- Release gating, versioning, or release-notes flow issues: Release Lead
- GitHub Actions permissions, workflow failures, manifest publish failures, or release asset attachment issues: DevOps Engineer
- Missing evidence, parity questions, or validation signoff issues: QA Lead

## Cutover Notes

- Deprecated operator alias `scripts/publish-release.ps1` has been removed.
- Canonical operator lane names are `Build`, `Publish-dev`, and `Publish-live`.
- `DevOps_CICD_EXAMPLE` remains reference material only and is not an operational entry point.