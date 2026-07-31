# Stage 3 Validation Runbook

Use this runbook to validate Stage 3 installer packaging automation.

## Preconditions

- Repository has at least one commit.
- Branch and tag pushes to origin are allowed.
- GitHub Actions is enabled for the repository.
- `.github/workflows/release-artifacts.yml` includes Stage 3 installer steps.

## Validation 1: workflow_dispatch baseline check

1. Open GitHub repository Actions tab.
2. Select workflow: Release Artifacts.
3. Click Run workflow.
4. For include_host, select true.
5. Run on the target branch (for example dev).
6. Wait for completion and open the run artifacts.
7. Verify Stage 2 package output still contains API/Web/Host zips, matching `.sha256` files, and `release-artifacts-manifest.json`.
8. Confirm no installer `.exe` is expected for `workflow_dispatch` runs.

## Validation 2: tagged installer build and release attachment

From repository root:

```powershell
$testTag = "v0.1.2-stage3-test"
git tag -a $testTag -m "Stage 3 validation tag"
git push origin $testTag
```

Then:

1. Open GitHub Actions and confirm Release Artifacts workflow runs for the tag.
2. Open the created or updated GitHub Release for the tag.
3. Verify attached files include:
   - API zip + `.sha256`
   - Web zip + `.sha256`
   - Installer `.exe` + `.sha256`
   - `release-artifacts-manifest.json`
4. Confirm installer filename pattern is `Ignyos-LanPortal-Dev-<version>.exe`.
5. Confirm installer checksum file uses matching filename `Ignyos-LanPortal-Dev-<version>.exe.sha256`.

## Cleanup (optional)

```powershell
git push origin :refs/tags/v0.1.2-stage3-test
git tag -d v0.1.2-stage3-test
```

## Pass Criteria

- workflow_dispatch run succeeds and preserves Stage 2 artifact behavior.
- tag run succeeds and attaches installer `.exe` + `.sha256` to GitHub Release.
- installer file naming is stable and suitable for Stage 4 manifest generation.
