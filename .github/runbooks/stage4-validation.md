# Stage 4 Validation Runbook

Use this runbook to validate Stage 4 update-channel behavior.

## Preconditions

- Stage 4 code is merged into the target branch.
- Repository tag push permissions are enabled.
- GitHub Pages is configured for the repository and resolves from `https://lanportal.ignyos.com`.
- Host app can reach the API endpoint at `http://localhost:5212/api/local/update/status`.

## Validation 1: Tag-driven manifest generation and release assets

From repository root:

```powershell
$testTag = "v0.1.3-test.202607311202"
git tag -a $testTag -m "Stage 4 validation tag"
git push origin $testTag
```

Then verify:

1. Publish-live workflow succeeds for the tag run.
2. GitHub Release assets include:
   - installer `.exe` + `.sha256`
   - `update-manifest-test.json`
3. `update-manifest-test.json` contains:
   - `version`
   - `url`
   - `sha256`
   - `publishedAt`
   - `minSupportedVersion`

## Validation 2: GitHub Pages manifest endpoint

1. Open `https://lanportal.ignyos.com/updates/manifest-test.json`.
2. Confirm JSON is valid and reflects latest test tag values.
3. Confirm production path is still available and not overwritten:
   - `https://lanportal.ignyos.com/updates/manifest.json`

## Validation 3: API normalized response

From local machine:

```powershell
Invoke-RestMethod "http://localhost:5212/api/local/update/status?currentVersion=0.1.0"
```

Verify response fields include:

- `currentVersion`
- `latestVersion`
- `minSupportedVersion`
- `downloadUrl`
- `updateAvailable`
- `requiredUpdate`
- `channel`
- `isTestChannel`
- `manifestUrl`
- `checkedAtUtc`
- `isStale`
- `error`

## Validation 4: Host UI behavior

1. Launch Host app.
2. Verify footer displays current version.
3. Verify File menu contains `Check For Updates`.
4. Trigger manual check and verify:
   - when update exists, footer shows `New Version Available` or `Update Required`
   - update action opens release installer URL
5. Leave app running > 1 hour and verify polling check occurs (or temporarily reduce interval in config for local test).

## Failure behavior checks

1. Temporarily break manifest URL in API config.
2. In production channel: verify silent fallback and logs only.
3. In test channel: verify non-blocking info plus logs.

## Pass Criteria

- Tag workflow publishes update manifest and attaches release copy.
- GitHub Pages serves channel-specific manifest endpoints.
- API returns normalized update status with accurate flags.
- Host UI exposes manual and automatic update checks with expected footer behavior.
