# CI/CD Roadmap

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

- [ ] Add SHA256 verification step before installer launch in Host update flow.
- [ ] Fail closed on checksum mismatch and log clear diagnostic details.
- [ ] Add retry policy for installer download (3 attempts with backoff).
- [ ] Add download/install status reporting in Host logs for troubleshooting.
- [ ] Add safe process orchestration hooks for stop/update/restart sequence (feature-flagged for test channel).
- [ ] Define rollback metadata contract (backup location, version markers, failure reason codes).
- [ ] Add rollback trigger path for install/restart failures in test channel.
- [ ] Create Stage 5 validation runbook for test-channel-only safety scenarios.

Stage 5 exit criteria (test channel):

- Test-channel update verifies checksum before install and blocks on mismatch.
- Retry and failure-path behaviors are exercised and logged end-to-end.
- Rollback metadata is generated and validated in failure simulation.
- Team approves readiness for production-channel Stage 5 rollout gate.
