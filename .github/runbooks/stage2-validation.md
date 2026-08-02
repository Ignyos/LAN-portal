# Stage 2 Validation Runbook

Use this runbook to validate Stage 2 release artifact automation.

## Preconditions

- Repository has at least one commit.
- Branch and tag pushes to origin are allowed.
- GitHub Actions is enabled for the repository.

## Validation 1: Publish-dev manual rehearsal with Host enabled

1. Open GitHub repository Actions tab.
2. Select workflow: Publish-dev.
3. Click Run workflow.
4. For include_host, select true.
5. Run on the target branch (for example dev).
6. Wait for completion and open the run artifacts.
7. Verify artifact package contains:
   - Ignyos-LanPortal-Api-manual-<run>.zip
   - Ignyos-LanPortal-Web-manual-<run>.zip
   - Ignyos-LanPortal-Host-manual-<run>.zip
   - Matching .sha256 files
   - release-artifacts-manifest.json

## Validation 2: tagged release asset attachment

From repository root:

```powershell
$testTag = "v0.1.1-test"
git tag -a $testTag -m "Stage 2 validation tag"
git push origin $testTag
```

Then:

1. Open GitHub Actions and confirm Publish-live workflow runs for the tag.
2. Open the created/updated GitHub Release for the tag.
3. Verify attached files include:
   - API zip + sha256
   - Web zip + sha256
   - release-artifacts-manifest.json

## Cleanup (optional)

```powershell
git push origin :refs/tags/v0.1.1-test
git tag -d v0.1.1-test
```

## Pass Criteria

- Publish-dev manual run succeeds with include_host=true and produces host artifact.
- tag run succeeds and attaches expected assets to GitHub Release.
- checksums and manifest are present and downloadable.
