# CI/CD Non-Regression Matrix (Stage 6B)

Purpose:
- Capture the current release behavior baseline before Stage 6D and Stage 6E refactors.
- Define pass/fail parity checks for dry run, test tags, production tags, manifest publish, and installer safety artifacts.
- Provide a rollback procedure if behavior deviates during migration.

Scope:
- Local release preparation script behavior.
- GitHub Actions workflow behavior for CI and tagged release automation.
- Artifact, checksum, manifest, and release-asset outcomes.

Repository evidence scope:
- Primary evidence source: `LAN-Portal` repository (this runbook's workflow paths and matrix IDs are validated here).
- Supplemental context only: `LAN-Portal-dev` repository runs may be attached as supporting notes but do not replace required primary evidence links.

Stage 6D script parity quick check:
- `./scripts/validate-publish-parity.ps1`

## Baseline Snapshot (2026-08-02)

Primary current assets:
- Local release prep: `scripts/publish-release.ps1`
- CI workflow: `.github/workflows/ci.yml`
- Dev lane artifact rehearsal workflow: `.github/workflows/publish-dev.yml`
- Live release automation workflow: `.github/workflows/release-artifacts.yml`
- Release notes paths: `.github/release/release-notes-style-guide.md`, `.github/release/release-notes.md`

### Baseline Behavior Checklist

#### Local release preparation (`scripts/publish-release.ps1`)

- Enforces branch gate to target branch (defaults to current unless overridden).
- Enforces clean working tree before proceeding.
- Fetches `origin/<target>` and enforces local/remote sync gate.
- Reads version from `Ignyos.LanPortal.Host/Ignyos.LanPortal.Host.csproj` `<Version>`.
- Validates SemVer and prompts for publish version.
- Performs local and remote tag collision checks.
- Generates release diff artifacts under `artifacts/release-publish/`:
  - `release-diff-<stamp>.patch`
  - `release-diff-summary-<stamp>.txt`
  - `ai-release-prompt-<stamp>.md`
  - `publish-<stamp>.log`
- Clears `.github/release/release-notes.md` and requires human confirmation.
- Performs explicit final approval before commit/tag/push.
- Commits only expected files (`<Version>` source + release notes path) and blocks unexpected file changes.
- Supports `-DryRun` to skip commit/tag/push while still generating prep artifacts.

#### CI workflow (`.github/workflows/ci.yml`)

- Runs on pull requests to `main` and pushes to `main`.
- Performs solution restore, build, and test.
- Fails fast on compilation or test failures.

#### Dev workflow (`.github/workflows/publish-dev.yml`)

- Triggers on `workflow_dispatch`.
- Publishes API and Web artifacts.
- Optionally publishes Host artifact on manual runs (`include_host=true`).
- Packages artifacts and uploads workflow artifact bundle.

#### Live workflow (`.github/workflows/release-artifacts.yml`)

- Triggers on tag push matching `v*`.
- Publishes API and Web artifacts on tag runs.
- Host publish and installer pipeline run on tag flow.
- On tag runs:
  - Builds installer staging payload.
  - Resolves/install Inno Setup compiler (`iscc.exe`).
  - Builds installer executable and `.sha256`.
  - Generates update manifest (`manifest.json` or `manifest-test.json`) from tag version.
  - Publishes manifest JSON into `docs/updates` on default branch.
  - Packages release artifacts and uploads workflow artifact bundle.
  - Attaches `.zip`, `.exe`, `.sha256`, and manifest files to GitHub Release.

## Non-Regression Test Matrix

Status values:
- Pending: not executed yet in migration branch.
- Pass: parity confirmed.
- Fail: parity regression detected.

| ID | Scenario | Trigger/Command | Expected Result | Evidence | Status |
|---|---|---|---|---|---|
| NRM-01 | Local dry run gate path | `./scripts/publish-release.ps1 -DryRun` | Branch, clean-tree, sync, SemVer, and tag checks execute; diff/prompt/log artifacts generated; no commit/tag/push | Pass run at 2026-08-02 13:37 local time in isolated runner (`.tmp/nrm-runner2`), exit code `0`, artifacts: `release-diff-20260802-133735.patch`, `release-diff-summary-20260802-133735.txt`, `ai-release-prompt-20260802-133735.md`, `publish-20260802-133735.log` | Pass |
| NRM-02 | Local release approval gates | `./scripts/publish-release.ps1` | AI release-notes pause + explicit final approval required; cancel path exits without publish | Pass run at 2026-08-02 13:38 local time in isolated runner (`.tmp/nrm-runner2`): CONTINUE prompt displayed, operator entered `EXIT`, script logged `User exited before publish.` with no publish action | Pass |
| NRM-03 | Unexpected-change block | Run publish with extra modified file outside allowed set | Script aborts before commit with clean-gate style error | Pass run at 2026-08-02 13:37 local time in isolated runner (`.tmp/nrm-runner2`): extra file `.nrm-extra-change.txt` caused clean-gate failure and exit code `11`; `git status --porcelain` showed `?? .nrm-extra-change.txt` | Pass |
| NRM-04 | CI branch protection parity | PR to `main` | Restore/build/test executed and required to pass | Actions run link | Pending |
| NRM-05 | Manual artifact rehearsal with Host | Run `.github/workflows/publish-dev.yml` via `workflow_dispatch` with `include_host=true` | API/Web/Host zip outputs plus `.sha256` and release-artifacts manifest | Actions artifact listing | Pending |
| NRM-06 | Test tag release assets | Push `vX.Y.Z-test` tag | Tagged run generates installer `.exe`, `.sha256`, zips, and attaches all release assets | Release asset listing | Pending |
| NRM-07 | Manifest channel routing (test) | Test tag with `-test` suffix | `manifest-test.json` generated and published with tag-matching URL/checksum | Commit in default branch + file content | Pending |
| NRM-08 | Manifest channel routing (production) | Production tag `vX.Y.Z` | `manifest.json` generated and published with tag-matching URL/checksum | Commit in default branch + file content | Pending |
| NRM-09 | Installer checksum integrity artifact | Any tagged run | Installer checksum file present and parseable in release package | `*.exe.sha256` in package assets | Pending |
| NRM-10 | Release artifact package naming stability | Any tagged run | API/Web/Host zip naming and `release-artifacts-manifest.json` format unchanged | Package file list + JSON schema diff | Pending |

## Rollback Strategy (Migration Safety)

Use this if Stage 6D or 6E introduces regression in any NRM scenario.

### Immediate response

1. Stop further publish runs from migration branch.
2. Mark failed matrix items and collect evidence links.
3. Do not delete generated test tags until root cause is captured.

### Source rollback options

1. Preferred: revert the refactor PR commit(s) on the integration branch.
2. Alternate: hard switch to last known-good release workflow/script revision by revert PR.
3. Keep rollback changes isolated in one PR with title prefix `rollback:`.

### Tag and release rollback (if bad release tag was pushed)

1. Disable release promotion communications immediately.
2. Create corrective follow-up tag only after fix validation on test tag.
3. If required, delete bad remote tag and local tag only after team approval and runbook note.

### Manifest rollback

1. Restore prior known-good `docs/updates/manifest*.json` values in a dedicated commit.
2. Verify API update-check behavior against restored manifests.
3. Record restored commit SHA in migration incident notes.

### Exit conditions for rollback completion

- All failed NRM items are either fixed and passing or explicitly deferred with approval.
- Team confirms next migration attempt scope and guardrails before re-run.

## Evidence Log Template

Use this per execution round:

- Date:
- Branch:
- Commit SHA:
- Matrix IDs executed:
- Pass IDs:
- Fail IDs:
- Actions run links:
- Release links:
- Notes:

## Evidence Log (Execution Rounds)

- Date: 2026-08-02
- Branch: dev (isolated test origin/runner)
- Commit SHA: 4e35e71
- Matrix IDs executed: NRM-01, NRM-02, NRM-03
- Pass IDs: NRM-01, NRM-02, NRM-03
- Fail IDs: None
- Actions run links: N/A (local script rehearsal only)
- Release links: N/A (no tag publish in this round)
- Notes: Used non-interactive CLI overrides (`-PublishVersion`, `-ConfirmVersion`) for deterministic rehearsal; NRM-02 cancel path validated by explicit `EXIT` at CONTINUE gate.

- Date: 2026-08-02
- Branch: N/A (supplemental external lane)
- Commit SHA: d9ec8f2 (LAN-Portal-dev)
- Matrix IDs executed: Supplemental context only
- Pass IDs: N/A
- Fail IDs: N/A
- Actions run links: https://github.com/Ignyos/LAN-Portal-dev/actions/runs/30757938543
- Release links: N/A
- Notes: Supplemental evidence from LAN-Portal-dev pages workflow. Per repository targeting rule, this does not replace required primary evidence from LAN-Portal for NRM-04 through NRM-10.

## Stage 6F Remote Execution Checklist (NRM-04 through NRM-10)

Use this section to execute and capture evidence for the remaining GitHub-hosted scenarios.

Repository targeting rule:

1. Execute NRM-04 through NRM-10 in the `LAN-Portal` repository.
2. If a related `LAN-Portal-dev` run exists, record it in Notes as supplemental context only.

### NRM-04 (CI branch protection parity)

1. Open a PR to `main` from the Stage 6 branch.
2. Confirm `.github/workflows/ci.yml` run includes restore, build, and test.
3. Record the Actions run URL in evidence.

### NRM-05 (manual dev artifact rehearsal with Host)

1. Manually run workflow `.github/workflows/publish-dev.yml`.
2. Set input `include_host=true`.
3. Confirm uploaded artifact contains:
  - `Ignyos-LanPortal-Api-manual-<run>.zip` + `.sha256`
  - `Ignyos-LanPortal-Web-manual-<run>.zip` + `.sha256`
  - `Ignyos-LanPortal-Host-manual-<run>.zip` + `.sha256`
  - `release-artifacts-manifest.json`
4. Record the Actions run URL and artifact listing screenshot/link.

### NRM-06, NRM-07, NRM-09, NRM-10 (test tag run)

1. Create and push a test tag in SemVer test form:
  - Example: `v0.1.4-test.20260802`
2. Confirm `.github/workflows/release-artifacts.yml` executes successfully.
3. In the GitHub Release for that tag, confirm assets include:
  - Installer `.exe`
  - Installer `.exe.sha256`
  - API/Web zip artifacts and `.sha256`
  - `release-artifacts-manifest.json`
  - `update-manifest-test.json`
4. Confirm `docs/updates/manifest-test.json` is updated on default branch with matching version, URL, and checksum.
5. Record Actions run URL, Release URL, and manifest commit SHA.

### NRM-08 (production manifest routing)

1. Create and push a production tag:
  - Example: `v0.1.4`
2. Confirm `.github/workflows/release-artifacts.yml` succeeds.
3. Confirm `docs/updates/manifest.json` is updated on default branch with matching version, URL, and checksum.
4. Record Actions run URL, Release URL, and manifest commit SHA.

### Evidence capture format

For each scenario, append a new execution-round entry with:

- Date
- Branch
- Commit SHA
- Matrix IDs executed
- Pass IDs
- Fail IDs
- Actions run links
- Release links
- Notes
