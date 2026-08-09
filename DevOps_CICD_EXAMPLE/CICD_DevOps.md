# CI/CD + DevOps Template

> Template-only reference for LAN Portal adoption planning.
> Operational source-of-truth for this repository remains under `.github/` and root `README.md`.

## Purpose
Use this pattern as a reusable reference for two related deployment models:

1. Static-site style repositories that publish from a docs folder.
2. Compiled application repositories that publish installer artifacts, update manifests, and Pages content from separate lanes.

The current LAN Portal work is the second model: a compiled application with two repositories, a production lane and a dev lane, plus separate deployment responsibilities for Docs, Live application releases, and Dev application releases.

## Repository Pattern
Expected key files and folders:
- Build.ps1
- Publish-dev.ps1
- Publish-live.ps1
- RELEASE_NOTES.md
- RELEASE_NOTES_STYLE.md
- docs/
- release/

## Run And Debug Pattern
Provide a small set of Run and Debug options that map to the deployment model clearly.

For the static-site variant:
1. Build
  - Runs Build.ps1.
  - Performs local tasks only.
  - Useful for local developer iteration.
2. Publish-live
  - Runs Publish-live.ps1.
  - Executes the live release workflow.
3. Publish-dev
  - Runs Publish-dev.ps1.
  - Executes the dev/test publish workflow.

For the compiled-application variant:
1. Docs-Deploy
  - Publishes the root docs folder into the target Pages repositories.
  - Used for splash page and docs updates only.
2. Live-App-Deploy
  - Builds and publishes the production release payload and manifest.
  - Mirrors the live release to the dev repo when appropriate.
3. Dev-App-Deploy
  - Builds and publishes the dev-only payload and manifest.
  - Targets the dev repo only.

## Developer Scenarios
Use these common cases to keep the workflow UX obvious:
- Local iteration: run Build to validate changes without publishing or changing branches.
- Dev lane publish: run Publish-dev from the dev branch to build, tag, and push to the dev site.
- Live release prep: run Publish-live from main to create the release path; if run elsewhere, it should clearly say it is test-only.

UX goals for these scenarios:
- Show the detected branch, target lane, and expected outcome before any changes are made.
- Make branch mismatch messages say what branch was found and what branch is required.
- Make test-only release runs explain what was skipped and how to get a real release.
- Keep the dev-site banner visible so visitors know they are on a non-production page.

## Two-Repo Domain Pattern
Use two repositories for clear lane separation:
- Main repo: production lane.
- Dev-lane repo: pre-production validation lane.

Each repo must own a unique docs/CNAME value:
- Main repo CNAME example: test.ignyos.com
- Dev repo CNAME example: test-dev.ignyos.com

Rules:
- Never use the same CNAME in both repos.
- Treat CNAME edits as high-risk config changes and require review.
- Verify DNS records exist before first publish.
- Keep branch/tag conventions aligned across repos for traceability.

## Workflow Summary
### Common Pattern
The reusable pattern is:
1. Separate docs deployment from application deployment.
2. Keep a production lane and a dev lane.
3. Use one workflow for Pages/docs publishing.
4. Use one workflow for production application publishing.
5. Use one workflow for dev application publishing.

### 1) Build Validation
Script: Build.ps1
- Validates the deployment source exists (docs folder).
- Supports WhatIf mode for dry-run validation.
- Fails fast if required structure is missing.

### 2) Dev Publish Path
Script: Publish-dev.ps1
- Intended branch: dev (configurable).
- Optional flags:
  - SkipBuild: skip build step.
  - NoPush: commit locally without pushing.
  - CommitMessage: override default commit message.
- Steps:
  1. Verify current branch matches required branch.
  2. Run build script unless skipped.
  3. Generate UTC version stamp (yyyy-MM-dd-HH-mm).
  4. Cache-bust CSS/JS references in index.html files using ?v=<timestamp>.
  5. Update service worker cache name with same version stamp.
  6. Git add/commit/push.
- Outcome: push to dev triggers deployment automation.

### 3) Release Path
Script: Publish-live.ps1
- Full release is allowed only on main.
- Non-main branches run in test mode (no commit/tag/push).
- Optional flags:
  - WhatIfMode: dry run for git + file side effects.
  - NoPush: create local release commit/tag but do not push.
- Steps:
  1. Resolve repo root and verify release metadata files exist.
  2. Warn on dirty working tree and request confirmation.
  3. Generate UTC timestamp and use it as tag name.
  4. Update asset version references.
  5. Run build script.
  6. Generate release diff file in release/ as rel-<timestamp>.txt.
     - If no previous tags: diff from repo root to HEAD.
     - Else: diff from last tag to HEAD.
  7. Build and copy an AI prompt to generate RELEASE_NOTES.md from:
     - release diff file
     - RELEASE_NOTES_STYLE.md
  8. Pause for human confirmation after release notes update.
  9. Commit release changes if present.
  10. Validate tag uniqueness locally and on origin.
  11. Create annotated tag and push branch + tag (unless NoPush).

## Release Notes Pattern
### Inputs
- RELEASE_NOTES_STYLE.md: format and tone constraints.
- release/rel-<timestamp>.txt: source of truth for changes.

### Rules
- Notes must be factual and user-facing.
- Include only changes visible in the diff.
- Keep sections consistent (Overview, New Features, Improvements, Bug Fixes, Technical Changes, Installation, Requirements, Documentation).
- Use a versioned header: Release v<version>.

## DevOps Safeguards
- Strict mode and stop-on-error in PowerShell scripts.
- Preflight summary messaging for branch, lane, and expected result.
- Branch gates:
  - Publish-dev requires configured dev branch.
  - Publish-live allows real publishing only on main.
- Tag collision checks:
  - local tags
  - remote origin tags
- Dry-run support with WhatIf for safe rehearsal.
- Clean tree awareness and interactive confirmation for risky states.
- CNAME ownership checks to prevent cross-repo domain overlap.

## CI/CD Integration Expectations
Connect these scripts to your CI system:
- On dev branch push:
  - Run build validation.
  - Validate docs/CNAME is present and matches the dev domain.
  - Publish docs artifact or deploy docs folder.
- On main release tag:
  - Validate docs/CNAME is present and matches the production domain.
  - Trigger production release pipeline.
  - Attach generated release notes.
  - Publish release artifacts.

## GitHub Main Repo Setup
For this pattern to work from the main repo, configure:
- Settings > Secrets and variables > Actions > Repository secrets

Required secrets in the main repo:
- DEV_PAGES_TOKEN
  - Personal access token used by the dev deployment workflow to push docs output into the dev-lane repository.
  - Recommended token scope: repository write access to the target dev-lane repo.
- DEV_PAGES_REPO
  - Target repository in owner/name format.
  - Example: your-org/your-project-dev
- PROD_PAGES_TOKEN
  - Personal access token used by the production Pages deployment workflow to push docs output into the production Pages repo.
  - Recommended token scope: repository write access to the target Pages repo.
- PROD_PAGES_REPO
  - Target production Pages repository in owner/name format.
  - Example: your-org/your-project

Related workflow behavior:
- The docs deployment workflow uses the Pages secrets to publish the root docs folder into the target Pages repositories.
- The live app deployment workflow builds the production release payload and updates the live manifest.
- The dev app deployment workflow builds the dev payload and updates the dev manifest in the dev repo only.
- For PAT creation steps, see [GITHUB_PAT_SETUP.md](GITHUB_PAT_SETUP.md).

Validation steps after setup:
1. Trigger Publish-dev from the dev branch or workflow_dispatch.
2. Confirm docs content is pushed to the external dev-lane repository main branch.
3. Confirm docs/CNAME in published output matches the dev subdomain.

## Minimum Working Setup
Keep the example lean by treating these as the only required moving parts:
- One docs folder with a unique CNAME per repo.
- Build.ps1 for local validation.
- Publish-dev.ps1 for dev-lane publishing from dev.
- Publish-live.ps1 for main-branch release flow.
- DEV_PAGES_TOKEN and DEV_PAGES_REPO in the main repo Actions secrets.
- A simple dev-site banner only if you want visible non-production labeling.

Quick verification checklist:
- Build runs locally without publishing.
- Publish-dev is blocked unless the branch is dev.
- Publish-live is test-only unless the branch is main.
- Dev pages show the banner and the correct CNAME domain.

## Environment Banner Pattern (Optional)
Use a lightweight client-side banner so visitors can immediately recognize non-production environments.

Implementation pattern:
- Add a small script file in docs (example: docs/dev-site-banner.js).
- In docs/index.html, define a config object before loading the script.
- Script checks window.location.hostname against configured patterns.
- If matched, render a persistent top banner (example message: DEVELOPER PAGE).

Suggested config fields:
- enabled: true/false toggle.
- patterns: list of hostname substrings or regex-like patterns (example: test-dev).
- message: visible banner text.
- backgroundColor and textColor: visual emphasis.
- heightPx: fixed banner height.
- skipOnLocalhost: optional local development behavior.

Operational guidance:
- Keep production domains out of the patterns list.
- Reuse the same script in both repos; only the config should differ by environment.
- Treat banner text and color as part of your environment safety controls.

## Template Commands
Typical local commands:
- Build:
  - ./Build.ps1
- Publish-dev:
  - ./Publish-dev.ps1
  - ./Publish-dev.ps1 -NoPush
  - ./Publish-dev.ps1 -SkipBuild -CommitMessage "chore: quick publish"
- Publish-live:
  - ./Publish-live.ps1
  - ./Publish-live.ps1 -WhatIfMode
  - ./Publish-live.ps1 -NoPush

## Adoption Checklist For Another Project
1. Copy this file and the deployment workflow pattern, then choose which variant you need:
   - static-site variant
   - compiled-application variant
2. Set up two repos (main and dev lane) or equivalent two-lane workflow.
3. Create docs/CNAME in each repo with unique subdomains.
4. Set your deployment source folder (docs or equivalent).
5. Set required dev and release branches.
6. Confirm asset cache-busting targets (css/js and service worker strategy) if the project has a web frontend.
7. Add or adapt release notes style guide.
8. Wire branch and tag triggers in CI platform.
9. Configure main repo Actions secrets for the Pages deployment and app deployment workflows.
10. Add CI checks that validate CNAME presence and expected domain values.
11. Test with the relevant variant flow:
   - static-site variant: Publish-live.ps1 -WhatIfMode and Publish-dev.ps1 -NoPush
   - compiled-app variant: Docs-Deploy, Live-App-Deploy, and Dev-App-Deploy
12. Perform first real release from main.

## Customization Points
- Timestamp format and tag format.
- Commit message conventions.
- Release notes section schema.
- CI triggers and environment promotion rules.
- Manual approval gates before release tagging.

## Minimal Success Criteria
A project correctly implements this pattern when:
- Dev pushes are fast and reproducible.
- Release creation is gated, traceable, and tag-based.
- Release notes are generated from diff evidence and a style guide.
- Cache invalidation is automatic per publish/release stamp.
- Main and dev lanes publish to separate subdomains via repo-specific CNAME files.
