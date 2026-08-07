# CI/CD Roadmap

## Active Direction (Session Handoff)

When a new session asks "What is the next task to work on?", use this order:

1. Read `.github/runbooks/publish-lanes-vision-handoff.md` first.
2. Continue the unchecked items in that handoff file.
3. Treat that handoff as the current lane contract unless explicitly changed.

Current implementation focus:

- Publish-dev lane: `dev` branch only, no release-notes gate, version `n.n.n.(r+1)` default with `r > 0`, dev destination outputs.
- Publish-live lane: `main` branch only, release-notes gate required, version `n.n.n.0` only, production destination outputs.
- Keep assembly `<Version>` as source of truth using four-node versions (`n.n.n.0` live, `n.n.n.r` dev).
- Use LAN-portal-dev only for developer releases.
- Use the build node to choose update source/download behavior and the displayed version style in the app.

## Stage 1: Continuous Integration (Current)

- Build solution on pull requests and pushes to main.
- Run automated tests on pull requests and pushes to main.
- Fail fast on compile and test errors.
- Defer lint/format and dependency security scan gates to a later hardening step.

## Stage 1.5: Developer-Gated Release Flow (Current)

- Use VS Code Run and Debug `Publish` / `Publish Dry Run` as the release entry point.
- Enforce local publish gates in script: main branch, clean working tree, and synced with `origin/main`.
- Generate diff artifacts and AI prompt for deterministic release notes creation.
- Require explicit human approval before commit, tag, and push.

## Stage 2: Release Artifacts

- Automate API and Web release publish outputs in GitHub Actions.
- Upload zipped artifacts to the workflow run for traceability.
- On tagged releases, attach artifacts to GitHub Releases.
- Keep local `Publish` script as pre-tag release preparation and notes workflow.

Current baseline implementation:

- `.github/workflows/release-artifacts.yml` publishes API/Web, zips outputs, generates `.sha256`, uploads run artifacts, and attaches release assets on tag runs.

Stage 2 checklist:

- [x] Run `workflow_dispatch` once and verify uploaded artifacts are complete and downloadable.
- [x] Push a non-production test tag (`vX.Y.Z-test`) and verify release asset attachment behavior.
- [x] Confirm artifact naming conventions are stable for downstream installer/update workflows.
- [x] Add optional Host output artifact support for `workflow_dispatch` runs; keep tag-based Stage 2 defaults focused on API/Web.

Stage 2 exit criteria:

- Tagged runs consistently produce API/Web zip + checksum artifacts.
- Tagged runs attach artifacts to GitHub Releases without manual intervention.
- Team confirms Stage 2 artifact outputs are sufficient input for Stage 3 installer workflow.

## Stage 3: Installer Packaging

- Add Inno Setup compile step in release workflow.
- Produce installer executable.
- Publish installer to GitHub Releases.
- Defer code-signing milestone until signing certificate is available.

Stage 3 checklist:

- [x] Add installer staging payload build step for tag-triggered runs.
- [x] Install or resolve Inno Setup compiler (`iscc.exe`) on `windows-latest` runner.
- [x] Compile installer executable in GitHub Actions using tag-derived version (`vX.Y.Z` -> `X.Y.Z`).
- [x] Generate installer SHA256 file and include it in release assets.
- [x] Attach installer `.exe` to tagged GitHub Releases.
- [x] Push a non-production Stage 3 validation tag (`vX.Y.Z-stage3-test`) and verify installer + checksum are attached.
- [x] Confirm installer output naming convention is stable for Stage 4 manifest consumption.

Stage 3 exit criteria:

- Tagged runs consistently produce installer `.exe` + `.sha256` alongside Stage 2 release artifacts.
- Tagged runs attach installer assets to GitHub Releases without manual intervention.
- Team confirms installer assets are sufficient input for Stage 4 manifest publishing.

## Stage 4: Update Channel

- Publish version manifest with installer URL and checksum.
- Define manifest schema now: `version`, `url`, `sha256`, `publishedAt`, `minSupportedVersion`.
- Add app endpoint to check latest version from manifest.
- Prompt for updates in Host UI footer and File menu.

Stage 4 checklist:

- [x] Generate and publish update manifest as part of tagged release workflow.
- [x] Populate manifest fields from release outputs: `version`, `url`, `sha256`, `publishedAt`, `minSupportedVersion`.
- [x] Select and document manifest hosting location and URL strategy.
- [x] Add API endpoint to fetch and return normalized update metadata.
- [x] Add Host UI footer update indicator and File menu "Check For Updates" action.
- [x] Support minimum-version gate behavior (`minSupportedVersion`) in update check response, with `requiredUpdate` blocking update actions only.
- [x] Add failure handling for missing/invalid manifest: silent fallback + logs in production, non-blocking info + logs in test builds.
- [x] Add hourly polling for update checks with manual refresh from File menu.
- [x] Add config-based channel selection (`production` or `test`) for update checks.
- [x] Validate test-channel prerelease handling using `-test.<yyyyMMddHHmm>` SemVer pre-release identifiers.
- [x] Validate end-to-end update check against a Stage 4 test release.

Stage 4 exit criteria:

- Tagged release produces a valid manifest file with installer URL and checksum.
- App can retrieve update metadata and determine whether an update is available.
- Host UI footer and File menu update check flow clearly communicate update availability and required action.
- Team confirms manifest contract is stable enough for Stage 5 safety hardening.

Stage 4 agreed decisions:

- Host manifests on GitHub Pages under dedicated paths that do not interfere with public docs navigation.
- Pages base host for public docs and manifests: `https://lanportal.ignyos.com`.
- Use two channels from day one with stable URLs:
	- production: `/updates/manifest.json`
	- test: `/updates/manifest-test.json`
- Keep manifest as raw source of truth, but return normalized update response from API (for example `updateAvailable`, `requiredUpdate`, `latestVersion`).
- Poll for updates once per hour and provide a manual "Check For Updates" command in the Host File menu.
- Show a "New Version Available" indicator/action in the Host footer when update is detected.
- Use config-based channel selection for now (no user-facing opt-in UI in Stage 4).
- `requiredUpdate` indicates app version is below `minSupportedVersion` and should block update actions only.
- On update-check failure:
	- production builds: silent fallback with logs only
	- test builds: non-blocking info message plus logs
- Use SemVer as the release contract.
- For test releases, use pre-release suffixes with minute granularity timestamps, for example `0.2.1-test.202607311202`.
- Keep optional UI-level pre-release opt-in discovery ideas (for example About-menu surfaced prerelease checks) as a follow-up after Stage 4 baseline.

Example manifest payload:

```json
{
	"version": "1.2.0",
	"url": "https://example.com/releases/IgnyosLanPortal-1.2.0.exe",
	"sha256": "<hex-checksum>",
	"publishedAt": "2026-07-30T18:00:00Z",
	"minSupportedVersion": "1.0.0"
}
```

## Stage 5: Safe Updates

- Verify signature/checksum before applying update.
- Stop service, install update, restart service.
- Keep rollback artifact metadata for recovery.
- Define failure-path behavior:
	- If verification fails, show a clear error and do not install.
	- Retry download up to 3 times with backoff before surfacing failure.
	- Trigger rollback when install or restart fails after backup is captured.
	- Show recovery guidance and keep local logs for troubleshooting.

Stage 5 kickoff plan (pre-production focused):

- Keep Stage 5 implementation and validation on test channel first; defer production tag requirements until release readiness.
- Prioritize installer safety verification using existing Stage 4 test manifest and test installer flow.

Stage 5 kickoff checklist:

- [x] Add SHA256 verification step before installer launch in Host update flow.
- [x] Fail closed on checksum mismatch and log clear diagnostic details.
- [x] Add retry policy for installer download (3 attempts with backoff).
- [x] Add download/install status reporting in Host logs for troubleshooting.
- [x] Add safe process orchestration hooks for stop/update/restart sequence (feature-flagged for test channel).
- [x] Define rollback metadata contract (backup location, version markers, failure reason codes).
- [x] Add rollback trigger path for install/restart failures in test channel.
- [x] Create Stage 5 validation runbook for test-channel-only safety scenarios.

Stage 5 exit criteria (test channel):

- Test-channel update verifies checksum before install and blocks on mismatch.
- Retry and failure-path behaviors are exercised and logged end-to-end.
- Rollback metadata is generated and validated in failure simulation.
- Team approves readiness for production-channel Stage 5 rollout gate.

## Stage 6: DevOps/CICD Pattern Adoption Refactor Plan (No-Behavior-Change First)

Goal:

- Adopt the `Build` / `Publish-dev` / `Publish-live` operating pattern from `DevOps_CICD_EXAMPLE`.
- Preserve all existing LAN Portal release behavior from Stages 2-5.
- Reduce implementation noise by completing naming and documentation cleanup before script/workflow refactors.

Guardrails:

- Do not change release behavior during Stage 6A and 6B.
- Keep installer, checksum, manifest, and GitHub Release asset behavior intact.
- Keep branch and clean-tree gates equivalent to current publish process.

### Stage 6A: Pre-Refactor Cleanup (Noise Reduction)

Owner defaults:

- Release Lead: primary owner of release-flow decisions.
- DevOps Engineer: primary owner of workflow and secrets changes.
- QA Lead: primary owner of validation runbooks and evidence capture.

Effort scale:

- S: <= 0.5 day
- M: 1 day
- L: 2-3 days

Backlog:

- [x] S6A-01: Create CI/CD source-of-truth map doc section in this roadmap.
	- Owner: Release Lead
	- Effort: S
	- Output: table mapping current assets to future `Build` / `Publish-dev` / `Publish-live` names.
- [x] S6A-02: Freeze naming decisions before edits.
	- Owner: Release Lead
	- Effort: S
	- Output: approved naming matrix for scripts, workflow files, run/debug entries, and runbooks.
- [x] S6A-03: Reconcile release notes standards.
	- Owner: Release Lead
	- Effort: S
	- Output: one canonical style guide path and one canonical release notes output path.
- [x] S6A-04: Define workflow ownership boundaries.
	- Owner: DevOps Engineer
	- Effort: S
	- Output: explicit ownership for CI checks, dev lane publish, live release publish, and manifest publish.
- [x] S6A-05: Align runbooks to one sequence.
	- Owner: QA Lead
	- Effort: M
	- Output: Stage 2/3/4/5 runbooks indexed by lane and trigger type (manual, branch, tag).

Stage 6A exit criteria:

- Canonical names approved and documented.
- No duplicate source-of-truth docs for release notes style/output.
- Clear lane ownership documented for each automation step.

Stage 6A deliverables (frozen 2026-08-02):

#### 6A.1 Source-of-Truth Mapping (Current -> Canonical)

| Area | Current Asset | Canonical Lane Name | Decision |
|---|---|---|---|
| VS Code Run/Debug | `Publish` | `Publish-live` | Keep behavior, rename entry point in Stage 6C |
| VS Code Run/Debug | `Publish Dry Run` | `Publish-dev` | Keep behavior, rename entry point in Stage 6C |
| Script | `scripts/publish-release.ps1` | `scripts/publish-live.ps1` (target) | Introduce alias path first, then migrate in Stage 6D |
| Workflow | `.github/workflows/release-artifacts.yml` | `.github/workflows/publish-live.yml` (target) | Rename after behavior parity checks in Stage 6E |
| Workflow | `.github/workflows/ci.yml` | `.github/workflows/ci.yml` | Keep name, keep CI-only responsibility |
| Runbook set | `stage2/3/4/5-validation.md` | Lane-indexed runbook flow | Keep files, add index and lane mapping |

#### 6A.2 Canonical Release Notes Standard

Canonical release-note source-of-truth paths for LAN Portal:

- Style guide: `.github/release/release-notes-style-guide.md`
- Release notes output: `.github/release/release-notes.md`

Template-only reference paths (non-authoritative for LAN Portal operations):

- `DevOps_CICD_EXAMPLE/RELEASE_NOTES_STYLE.md`
- `DevOps_CICD_EXAMPLE/RELEASE_NOTES.md`

Rule:

- Any script/workflow changes in Stage 6D/6E must preserve `.github/release/*` as the operational release notes paths unless explicitly changed by a dedicated migration decision.

#### 6A.3 Workflow Ownership Boundaries

| Responsibility | Owner Lane | Primary Workflow/Entry | Notes |
|---|---|---|---|
| Restore/build/test gates | Build lane | `.github/workflows/ci.yml` | No release side effects |
| Local release prep, branch/clean/sync gates, AI notes prompt | Publish-live lane (local) | `scripts/publish-release.ps1` (current) | Human-gated release preparation |
| Tagged artifact packaging + installer + checksums | Publish-live lane (remote) | `.github/workflows/release-artifacts.yml` (current) | Must preserve Stages 2-5 behavior |
| Manifest generation + docs/updates publish | Publish-live lane (remote) | `.github/workflows/release-artifacts.yml` (current) | Keep channel logic and schema stable |
| Dev lane rehearsal behavior | Publish-dev lane | `Publish Dry Run` mapping (current) | To be split into explicit path in Stage 6D |

#### 6A.4 Naming Freeze Rules

- Canonical operator lane names are `Build`, `Publish-dev`, and `Publish-live`.
- During transition, old names may exist only as compatibility aliases.
- New docs and runbooks must use canonical names first, then map old names in parentheses when needed.

#### 6A.5 Runbook Sequence Index

- Runbook lane index path: `.github/runbooks/cicd-lane-index.md`
- Existing validation runbooks remain in place and are now referenced through the lane index.

### Stage 6B: Planning Baseline And Non-Regression Matrix

Backlog:

- [x] S6B-01: Capture current release behavior baseline.
	- Owner: DevOps Engineer
	- Effort: M
	- Output: checklist of gates, prompts, artifacts, and release side effects currently implemented.
- [x] S6B-02: Define non-regression acceptance matrix.
	- Owner: QA Lead
	- Effort: M
	- Output: test matrix for dry run, test tag, production tag, manifest publish, and installer checksum behavior.
- [x] S6B-03: Define migration rollback strategy.
	- Owner: Release Lead
	- Effort: S
	- Output: branch/tag rollback steps if refactor behavior deviates from baseline.

Stage 6B exit criteria:

- Baseline behavior matrix is complete and reviewed.
- Non-regression test matrix is approved before implementation edits begin.

Stage 6B deliverables (frozen 2026-08-02):

- Baseline + non-regression + rollback runbook path: `.github/runbooks/cicd-non-regression-matrix.md`
- Matrix IDs `NRM-01` through `NRM-10` define required parity checks for Stage 6D/6E.
- Rollback policy requires a dedicated rollback PR and manifest restore verification when release behavior regresses.

### Stage 6C: Operator Experience Alignment (VS Code Entry Points)

Backlog:

- [x] S6C-01: Rework Run and Debug labels to the canonical three-lane model.
	- Owner: Release Lead
	- Effort: S
	- Depends on: S6A-02
- [x] S6C-02: Keep current command behavior while renaming entry points.
	- Owner: DevOps Engineer
	- Effort: S
	- Depends on: S6B-01
- [x] S6C-03: Update README operator instructions to new lane names.
	- Owner: Release Lead
	- Effort: S
	- Depends on: S6C-01

Stage 6C exit criteria:

- Operators have clear `Build`, `Publish-dev`, and `Publish-live` launch paths.
- No behavior change from existing publish commands yet.

Stage 6C deliverables (frozen 2026-08-02):

- Updated Run and Debug entries in `.vscode/launch.json`:
	- `Build`
	- `Publish-dev`
	- `Publish-live`
- Updated operator guidance in `README.md` to match canonical lane names.
- Publish command behavior preserved with `scripts/publish-live.ps1` as canonical engine and `scripts/publish-release.ps1` retained as compatibility alias.

### Stage 6D: Script Refactor (Composable Architecture)

Backlog:

- [x] S6D-01: Extract shared git validation utilities.
	- Owner: DevOps Engineer
	- Effort: M
	- Depends on: S6B-01
- [x] S6D-02: Introduce publish-dev orchestration script path.
	- Owner: DevOps Engineer
	- Effort: M
	- Depends on: S6D-01
- [x] S6D-03: Introduce publish-live orchestration script path.
	- Owner: DevOps Engineer
	- Effort: L
	- Depends on: S6D-01
- [x] S6D-04: Preserve AI-assisted release notes gate and explicit approvals.
	- Owner: Release Lead
	- Effort: S
	- Depends on: S6D-03
- [x] S6D-05: Add script-level parity checks against Stage 6B matrix.
	- Owner: QA Lead
	- Effort: M
	- Depends on: S6D-02, S6D-03

Stage 6D exit criteria:

- Script responsibilities are separated by lane.
- All prior gates and release protections remain functionally equivalent.

Stage 6D deliverables (frozen 2026-08-02):

- Shared release utilities extracted to `scripts/release-common.ps1`.
- Canonical publish-live orchestration implemented in `scripts/publish-live.ps1`.
- Canonical publish-dev path implemented in `scripts/publish-dev.ps1` (DryRun lane).
- Backward compatibility retained via `scripts/publish-release.ps1` alias forwarding to publish-live.
- AI-assisted release notes prompt, manual confirmation gates, and final approval steps preserved in publish-live flow.
- Script-level parity validator added: `scripts/validate-publish-parity.ps1`.

### Stage 6E: Workflow Refactor (Lane Ownership)

Backlog:

- [x] S6E-01: Keep CI workflow focused on restore/build/test only.
	- Owner: DevOps Engineer
	- Effort: S
	- Depends on: S6A-04
- [x] S6E-02: Add or rename workflow for dev lane publish responsibilities.
	- Owner: DevOps Engineer
	- Effort: M
	- Depends on: S6D-02
- [x] S6E-03: Add or rename workflow for live lane release responsibilities.
	- Owner: DevOps Engineer
	- Effort: M
	- Depends on: S6D-03
- [x] S6E-04: Preserve Stage 2-5 artifact pipeline behavior in live lane.
	- Owner: DevOps Engineer
	- Effort: L
	- Depends on: S6E-03
- [x] S6E-05: Revalidate secrets and permissions model.
	- Owner: DevOps Engineer
	- Effort: S
	- Depends on: S6E-02, S6E-03

Stage 6E exit criteria:

- Workflow responsibilities are lane-specific and non-overlapping.
- Tagged release behavior still produces installer, checksums, manifest, and release assets.

Stage 6E progress snapshot (2026-08-02):

- CI ownership remains isolated to `.github/workflows/ci.yml` for restore/build/test.
- Live lane workflow identity renamed to `Publish-live` (in `.github/workflows/release-artifacts.yml`) with unchanged triggers and steps.
- Stage 2-5 behavior preserved by retaining existing live workflow implementation logic and artifact pipeline steps.
- Dev lane manual artifact rehearsal moved to dedicated workflow `.github/workflows/publish-dev.yml`.
- Permissions revalidated by lane:
	- Publish-dev workflow uses `contents: read`.
	- Publish-live workflow retains `contents: write` for manifest commit and release attachment.
- Remaining Stage 6E work:
	- None.

### Stage 6F: Validation, Cutover, And Decommissioning

Backlog:

- [ ] S6F-01: Execute dry-run and test-tag rehearsals against non-regression matrix.
	- Owner: QA Lead
	- Effort: M
	- Depends on: S6E-04
- [ ] S6F-02: Approve cutover after evidence review.
	- Owner: Release Lead
	- Effort: S
	- Depends on: S6F-01
- [ ] S6F-03: Remove deprecated names and stale references after cutover.
	- Owner: DevOps Engineer
	- Effort: S
	- Depends on: S6F-02
- [ ] S6F-04: Publish final operator runbook for lanes and escalation paths.
	- Owner: QA Lead
	- Effort: S
	- Depends on: S6F-02

Stage 6F exit criteria:

- Evidence confirms no regression for Stage 2-5 functionality.
- Team uses only the new lane naming and run paths.
- Deprecated aliases and duplicate docs are removed.

Stage 6F progress snapshot (2026-08-02):

- S6F-01 is in progress.
- Completed local rehearsal subset from non-regression matrix:
	- `NRM-01` pass (local dry run gate path + artifact generation).
	- `NRM-02` pass (approval gate reached; explicit EXIT cancel path validated).
	- `NRM-03` pass (unexpected-change clean-gate block validated).
- Completed primary remote CI evidence:
	- `NRM-04` pass (LAN-Portal PR CI run on `dev -> main`).
	- `NRM-05` pass (LAN-Portal Publish-dev manual dispatch with `include_host=true`).
- Evidence recorded in `.github/runbooks/cicd-non-regression-matrix.md` under execution rounds.
- Remaining S6F-01 scope:
	- `NRM-06` through `NRM-10` (tag-driven release/manifest validation).
- Remote execution steps for `NRM-04` through `NRM-10` documented in `.github/runbooks/cicd-non-regression-matrix.md` under `Stage 6F Remote Execution Checklist`.

## Stage 6 Risk Register

- R1: Behavioral drift in release gates during script split.
	- Mitigation: Stage 6B parity matrix plus script-level parity checks.
- R2: Trigger collisions during workflow transition.
	- Mitigation: temporary branch/tag trigger freeze window and staged rollout.
- R3: Manifest publish races on default branch updates.
	- Mitigation: explicit ref resolution and post-run verification of manifest commit.
- R4: Operator confusion while old and new names coexist.
	- Mitigation: short overlap window and README/runbook updates in same PR.

## Stage 6 Implementation PR Strategy

- PR-1: Docs and naming cleanup only (Stage 6A, 6B, partial 6C).
- PR-2: VS Code operator entry point alignment (remaining 6C).
- PR-3: Script refactor with parity checks (6D).
- PR-4: Workflow refactor preserving artifact semantics (6E).
- PR-5: Validation evidence, cutover, and decommissioning (6F).

Success gate for each PR:

- Must pass CI.
- Must include updated runbook notes for changed operator behavior.
- Must map changed behavior to a Stage 6 backlog item.
