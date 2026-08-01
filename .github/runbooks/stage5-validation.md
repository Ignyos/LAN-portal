# Stage 5 Validation Runbook

Use this runbook to validate Stage 5 update safety behavior on test channel before production rollout.

For each scenario below, record evidence using [.github/runbooks/stage5-validation-evidence-template.md](stage5-validation-evidence-template.md).

## Preconditions

- Stage 5 code is merged into the target branch.
- Host version uses a `-test` prerelease suffix so test-channel behavior is active.
- API and Web services can be launched by Host.
- A test release exists with installer `.exe` and matching `.sha256`.
- Update status endpoint returns `expectedSha256` in the payload.

## Optional deterministic fault injection (test channel only)

Set `LANPORTAL_UPDATE_TEST_FAULT` before launching Host to force a specific failure path:

- `DOWNLOAD`: forces all download attempts to fail.
- `CHECKSUM`: forces checksum mismatch after download.
- `ORCHESTRATION`: forces pre-install orchestration hook failure.
- `LAUNCH`: forces installer launch failure after orchestration.

PowerShell example:

```powershell
$env:LANPORTAL_UPDATE_TEST_FAULT = "DOWNLOAD"
```

Clear after test:

```powershell
Remove-Item Env:LANPORTAL_UPDATE_TEST_FAULT
```

## Validation 1: Checksum gate (happy path)

1. Launch Host and trigger `File -> Check For Updates`.
2. Confirm footer shows update available.
3. Trigger update action.
4. Verify:
   - installer download completes
   - checksum verification succeeds
   - installer launches
5. Confirm logs include download attempts and verified installer path.

## Validation 2: Missing checksum metadata blocks install

1. Temporarily remove or blank `expectedSha256` from the update response.
2. Trigger update action in Host.
3. Verify install is blocked.
4. Verify rollback metadata file is written with failure code `MISSING_EXPECTED_SHA`.

## Validation 3: Retry policy and backoff

1. Force transient download failure (for example, set `LANPORTAL_UPDATE_TEST_FAULT=DOWNLOAD`).
2. Trigger update action.
3. Verify logs show up to 3 attempts with backoff delays.
4. If all attempts fail, verify failure code `DOWNLOAD_FAILED` is captured in rollback metadata.

## Validation 4: Checksum mismatch blocks install

1. Publish or inject an incorrect checksum for the test manifest, or set `LANPORTAL_UPDATE_TEST_FAULT=CHECKSUM`.
2. Trigger update action.
3. Verify Host blocks installer launch.
4. Verify rollback metadata includes failure code `CHECKSUM_MISMATCH`.

## Validation 5: Orchestration stop hooks

1. Ensure Host launched and is managing API/Web child processes.
2. Trigger update action.
3. Verify pre-install hooks stop managed Web then API process.
4. Verify Host logs orchestration start/completion.

## Validation 6: Rollback trigger marker on install/restart failure path

1. Simulate post-orchestration failure (for example, set `LANPORTAL_UPDATE_TEST_FAULT=ORCHESTRATION` or `LANPORTAL_UPDATE_TEST_FAULT=LAUNCH`).
2. Trigger update action.
3. Verify rollback metadata is written.
4. Verify `rollback-trigger.json` is written for test channel when failure code is:
   - `ORCHESTRATION_FAILED`, or
   - `INSTALLER_LAUNCH_FAILED`.

## Artifact locations

Under `%LOCALAPPDATA%\\Ignyos\\LanPortalDev\\UpdateState` verify:

- `rollback-metadata-latest.json`
- `rollback-metadata-<timestamp>.json`
- `rollback-trigger.json` (only for install/restart failure path)

## Pass Criteria

- Host blocks unsafe installs and logs failure reasons.
- Retry/backoff behavior is visible and bounded to 3 attempts.
- Rollback metadata contract is consistently generated with version markers and backup path reference.
- Rollback trigger marker is generated only for install/restart failure path in test channel.
- Team confirms Stage 5 safety behavior before production-channel rollout.

## Evidence handoff

Create one completed copy of the evidence template per scenario and store it with your validation notes for release gate review.
