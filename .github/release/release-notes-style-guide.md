# Release Notes Style Guide

Use this guide when generating release notes for public tagged releases.

## Goals

- Keep release notes concise, accurate, and easy to scan.
- Focus on user-visible and operator-visible changes.
- Only describe changes present in the release diff.

## Required Structure

Use this exact section order:

1. `## Release <version>`
2. `### Highlights`
3. `### Added`
4. `### Changed`
5. `### Fixed`
6. `### Operational Notes`
7. `### Risk / Impact`
8. `### Verification Notes`

If a section has no items, include `- None.`

## Writing Rules

- Use short bullet points.
- Start bullets with an action verb when possible.
- Describe outcomes, not internal implementation details.
- Do not include speculative statements.
- If confidence is low, append `(Needs verification)`.
- Avoid mentioning files unless needed for clarity.

## Risk / Impact Guidance

Include at least one bullet in `Risk / Impact` that covers:

- Runtime behavior risk.
- Deployment or configuration risk.
- User-facing risk.

## Verification Notes Guidance

List concrete checks someone can run after deploy.

Examples:

- `- Verify local admin login succeeds with existing credentials.`
- `- Verify device approval flow still issues a valid token.`
- `- Verify API and Web startup logs show no migration/config errors.`

## Output Rules

- Output must be written to the draft release-notes file for the current release.
- Output must describe only the current release.
- The publishing script will prepend the current release above the existing historical journal in `.github/release/release-notes.md`.
- Do not include historical release notes in the draft output.
