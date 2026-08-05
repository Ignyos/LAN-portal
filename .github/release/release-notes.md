
## Release 0.2.0.20260805003

### Highlights
- Unified update-channel routing and version display behavior around the fourth version node.
- Consolidated API update configuration to a single base settings file with explicit production and development endpoints.
- Improved publish and installer scripts for clearer run-mode control and safer non-interactive handling.

### Added
- Added `ProductionBaseUrl` and `DevBaseUrl` update-channel settings and removed reliance on a single shared base URL.
- Added version-node-based installer classification for update checks (`0` = production, non-`0` = developer).
- Added `-NonInteractive` and `-DevVersionSuggestion` support in publish workflows.
- Added explicit `-Live` mode and interactive dry-run prompt selection in the dev publish wrapper.

### Changed
- Changed release artifact workflow to stamp both production and development update endpoints into staged API settings.
- Changed host UI version and channel behavior to derive from the fourth version node rather than installer flavor file detection.
- Changed release version validation and defaulting logic to support four-node version values.
- Changed dev installer version suggestion to default to `major.minor.patch.<utc-datetime>` and auto-resolve duplicate versions.
- Changed VS Code Run/Debug profiles for publish and installer workflows from terminal launch mode to PowerShell launch mode.
- Removed API environment-specific settings files and consolidated runtime update settings into the base API appsettings file.

### Fixed
- Fixed publish confirmation prompt handling to avoid switch-parameter conversion collisions during interactive version confirmation.
- Fixed dev publish wrapper argument forwarding so dry-run/live mode selection no longer depends on brittle passthrough parsing.

### Operational Notes
- Dev publish now prompts for dry-run mode unless `-DryRun` or `-Live` is explicitly provided.
- Non-interactive publish runs now require explicit approval-related switches and fail fast when required flags are missing.
- Parity validation checks were updated to enforce prompt-based dev publish behavior and dev version suggestion forwarding.

### Risk / Impact
- Runtime behavior risk: Update-channel selection now depends on the fourth version node; incorrectly formatted versions can route clients to the wrong manifest source.
- Deployment/configuration risk: Removing environment-specific API settings increases reliance on the base appsettings file being correct for all deployment contexts.
- User-facing risk: Version text and update availability state in the host UI now follow new version-node rules and may differ from prior expectations in mixed-version environments.

### Verification Notes
- Verify a production-version host build (`x.y.z.0`) queries the production manifest endpoint and reports production channel state.
- Verify a dev-version host build (`x.y.z.<timestamp>`) queries the development manifest endpoint and reports test/developer channel state.
- Verify host title/footer show three-node version for production and full four-node version for developer builds.
- Verify `scripts/publish-dev.ps1` prompts for dry-run mode when no mode switch is passed, and honors `-DryRun` and `-Live` when provided.
- Verify `scripts/publish-live.ps1` non-interactive mode exits with clear guidance when required confirmation switches are missing.
- Verify VS Code Run/Debug entries for Publish-dev, Publish-live, and Build Dev Installer exit without requiring manual stop after script completion.

