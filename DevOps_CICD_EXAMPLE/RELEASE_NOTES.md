# Release v2026-08-01-16-59

## Overview
This release standardizes publish workflow naming, adds clearer CI/CD documentation, and introduces a visible developer-environment banner for dev-hosted pages.

## New Features
- **Dev Environment Banner**: Adds a configurable top-of-page indicator that displays on matching hostnames (for example, test-dev) so visitors can clearly see they are on a developer page.

## Improvements
- **Run And Debug Profiles**: Adds and aligns three launch options (Build, Publish-live, Publish-dev) for faster local workflow selection.
- **Publish Workflow Naming**: Renames publish scripts and GitHub Actions workflow files to match live and dev lane terminology for clearer operations.

## Bug Fixes
- **None**: No user-facing defects are fixed in this release.

## Technical Changes
- Renames script files to Build.ps1, Publish-dev.ps1, and Publish-live.ps1 and updates internal script references.
- Renames workflow files to Publish-dev.yml and Publish-live.yml and updates workflow display names.
- Updates dev publish workflow to call Build.ps1.
- Adds docs/dev-site-banner.js and wires docs/index.html configuration to conditionally render the developer banner.
- Adds CICD_DevOps.md with release workflow, two-repo domain pattern, GitHub secrets setup, and optional environment-banner guidance.

## Installation
- Pull the latest changes and use the renamed scripts and workflow files for build and publish operations.

## Requirements
- GitHub Actions repository secrets DEV_PAGES_TOKEN and DEV_PAGES_REPO must be configured in the main repository for dev-lane publishing.

## Documentation
- See CICD_DevOps.md for the consolidated CI/CD, release, two-repo domain, and GitHub setup template.
