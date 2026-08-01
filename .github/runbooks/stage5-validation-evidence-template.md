# Stage 5 Validation Evidence Template

Use this template to record test-channel safety validation results from Stage 5 scenarios.

## Session metadata

- Date (UTC):
- Tester:
- Branch:
- Commit SHA:
- Host version:
- API version:
- Test release tag:
- Channel: test

## Environment setup

- Host machine / OS:
- Network conditions:
- `LANPORTAL_UPDATE_TEST_FAULT` value (or `NONE`):
- Manifest URL tested:
- Installer URL tested:

## Scenario under test

- Runbook scenario:
- Scenario goal:
- Expected behavior:
- Expected failure code (if any):

## Steps executed

1.
2.
3.

## Observed behavior

- UI status text:
- Host logs (key lines):
- API logs (key lines, if relevant):
- Process orchestration observations:

## Safety artifacts captured

- `%LOCALAPPDATA%\\Ignyos\\LanPortalDev\\UpdateState\\rollback-metadata-latest.json`:
- `%LOCALAPPDATA%\\Ignyos\\LanPortalDev\\UpdateState\\rollback-metadata-<timestamp>.json`:
- `%LOCALAPPDATA%\\Ignyos\\LanPortalDev\\UpdateState\\rollback-trigger.json` (if expected):

## Artifact validation checks

- `FailureReasonCode` matches expectation: PASS / FAIL
- `CurrentVersion` and `TargetVersion` populated correctly: PASS / FAIL
- `BackupRootPath` present: PASS / FAIL
- `RollbackTriggered` value correct for scenario: PASS / FAIL

## Result

- Outcome: PASS / FAIL
- Notes:
- Follow-up actions:
- Defects filed (IDs/links):

## Approval

- Reviewer:
- Approval decision: Approved / Needs follow-up
- Approval notes:
