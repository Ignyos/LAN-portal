# Pages deployment secrets

This repository uses separate GitHub Actions workflows for:

- installer and manifest publishing
- static site publishing for GitHub Pages

## Required secrets

### Dev Pages deployment
For the current setup, set these secrets in the source repository that triggers the dev workflow: `Ignyos/LAN-portal`.

- `DEV_PAGES_TOKEN`: a GitHub personal access token that can write to the target Pages repository.
- `DEV_PAGES_REPO`: the target Pages repository name in `owner/repo` form, for example `Ignyos/LAN-Portal-dev`.

### Production Pages deployment
For the current setup, set these secrets in the source repository that triggers the production workflow: `Ignyos/LAN-portal`.

- `PROD_PAGES_TOKEN`: a GitHub personal access token that can write to the target Pages repository.
- `PROD_PAGES_REPO`: the target Pages repository name in `owner/repo` form, for example `Ignyos/LAN-Portal`.

## Recommended token permissions

Use a token with at least:

- `repo` access for private repositories
- or `public_repo` for public repositories if that is sufficient for your target setup

## Expected workflow behavior

- The installer workflow publishes the release payload and manifests into the target site repository.
- The Pages workflow publishes the static site content from the `docs` folder into the target Pages repository.

## Notes

If you later reuse this setup in another project, copy this document alongside the workflow files and keep the secrets naming consistent.
