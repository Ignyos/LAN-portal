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
- [ ] Push a non-production Stage 3 validation tag (`vX.Y.Z-stage3-test`) and verify installer + checksum are attached.
- [ ] Confirm installer output naming convention is stable for Stage 4 manifest consumption.

Stage 3 exit criteria:

- Tagged runs consistently produce installer `.exe` + `.sha256` alongside Stage 2 release artifacts.
- Tagged runs attach installer assets to GitHub Releases without manual intervention.
- Team confirms installer assets are sufficient input for Stage 4 manifest publishing.

## Stage 4: Update Channel

- Publish version manifest with installer URL and checksum.
- Define manifest schema now: `version`, `url`, `sha256`, `publishedAt`, `minSupportedVersion`.
- Add app endpoint to check latest version from manifest.
- Prompt for updates in local setup/admin page.

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
