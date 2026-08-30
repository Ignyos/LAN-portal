# Product Roadmap

## What is next?

LAN Portal's primary product promise is simple LAN file sharing for people who should not need much technical knowledge. That remains the center of the product. Future extensibility should expand that experience without making the first-use workflow feel like an app platform.

Current status:

- Access-request / Account polish is complete.
- Signed-out and signed-in route behavior is stable.
- Apps management is intentionally deferred.
- Host Advanced Logs and the focused Security control are complete for the accepted scope.
- Host Advanced validation is complete for the accepted scope.
- The active current work is file-transfer reliability and release-readiness work.

The current client-side UI/UX implementation work is tracked in the [Client Navigation Implementation Checklist](client-navigation-implementation-checklist.md). The current Host-side operational work is tracked in the [Host Navigation And Advanced Settings Plan](host-navigation-and-advanced-settings-plan.md).

## Next tasks

These are the immediate priorities in order:

1. [x] Finish the wider Host Advanced validation pass.
   - [x] confirm saved section state behaves correctly
   - [x] verify access history still works after the redesign
   - [x] ensure local-only access checks remain enforced across the page
   - [x] keep the access-request flow stable and documented

2. Test and harden the file-transfer process.
   - reproduce, document, and resolve the current upload failures that prevent files from reaching the Host
   - add automated and manual coverage for successful uploads, rejected uploads, retries, interruptions, and large files
   - add equivalent coverage for downloads, including range behavior, missing files, permission failures, and interrupted transfers
   - document known transfer errors, their causes, and operator/user recovery steps
   - verify transfer behavior through the actual Host-managed API/Web workflow, not only isolated unit tests
   - this remains required before `v1.0.0.0` general release

3. Complete the controller HTML and asset versioning refactor for release readiness.
   - extract controller-owned HTML into maintainable Razor views in the API project
   - keep request validation and dynamic data preparation in controllers and pass values through view models
   - move inline JavaScript into separate static files where practical
   - replace manual `?v=...` maintenance with automatic content-based asset versioning
   - cover all referenced JavaScript and CSS assets across Host and API-served HTML pages
   - verify changed views and assets are loaded after deployment and restart
   - keep Host, API, Web, and published artifacts on the same release version
   - this remains required before `v1.0.0.0` general release

4. Complete the runtime settings integration work.
   - define the typed settings model and validation pattern
   - keep direct DB access inside the settings store
   - connect the remaining settings surfaces to the typed facade

5. Revisit Apps management only after the Advanced work is complete.
   - keep the current focus on Host operations and file-sharing stability
   - do not make app management the near-term priority while Advanced remains open

6. Defer mandatory update enforcement until a later product decision.
   - treat newer releases as informational for now
   - do not block startup, navigation, or normal use when a newer version is available
   - do not label an available update as required in the current product experience
   - define the user-impact, rollout, recovery, and support requirements before enabling enforcement

## Release Requirements

The following item is medium importance during normal development but is required before the `v1.0.0.0` general release:

- [ ] Extract controller-owned HTML into maintainable views and add automatic asset versioning.
   - move Host and API-served HTML out of controller source and into Razor views
   - pass dynamic values through view models rather than C# string interpolation
   - move inline JavaScript into separate static files where practical
   - cover every referenced JavaScript and CSS asset with content-based versioning
   - remove manual query-string version maintenance as the release approach
   - verify that updated views and assets are loaded after deployment and application restart
   - do not declare the product ready for general release until this is complete
- [ ] Verify version parity across local Dev-Test and dev/production publish outputs.
   - use `Ignyos.LanPortal.Host/Ignyos.LanPortal.Host.csproj` as the single checked-in version source
   - pass the resolved release version to every published project
   - verify Host, API, Web, installer metadata, and release metadata do not report conflicting versions
- [x] Consolidate Host version and update-status display before general release.
   - [x] remove the application version from the title bar
   - [x] remove the duplicate local version display from the left side of the status bar
   - [x] show the current version and update state together on the right side of the status bar
   - [x] display production versions as `vMajor.Minor.Patch`, hiding the fourth build component
   - [x] display development versions as `vMajor.Minor.Patch.Build`, including the build component
   - [x] show production `checking for updates` status before the update check completes without exposing the normalized version prematurely
   - [x] show production `Update available` only after a newer version is confirmed
   - [x] keep development display limited to the local full version and do not append update status
   - [x] keep newer releases informational only; do not show `Update required` or block normal use
   - [x] validate the implementation with a successful Host build and API regression suite
- [ ] Revisit mandatory update enforcement in a future release planning cycle.
   - explicitly decide whether and when minimum-supported-version enforcement is needed
   - specify behavior for offline startup, failed downloads, rollback, and recovery
   - require a deliberate product approval before changing informational updates into required updates
- [ ] Test and harden the file-transfer process before general release.
   - reproduce, document, and resolve the current upload failures that prevent files from reaching the Host
   - add automated and manual coverage for successful uploads, rejected uploads, retries, interruptions, and large files
   - add equivalent coverage for downloads, including range behavior, missing files, permission failures, and interrupted transfers
   - document known transfer errors, their causes, and operator/user recovery steps
   - verify transfer behavior through the actual Host-managed API/Web workflow, not only isolated unit tests
   - follow the baseline test procedure in [File Transfer Validation Plan](file-transfer-validation-plan.md)
   - do not declare the product ready for `v1.0.0.0` until upload and download paths are reliable and validated

## Apps and extensions

## Possible Future Lanes

- [ ] Evaluate a secure advanced transfer lane for interoperability-focused users. See [Advanced Transfer Lane](advanced-transfer-lane.md).
   - keep browser-based HTTP sharing as the basic-user experience
   - evaluate SFTP or FTPS rather than plain FTP
   - validate the advanced-user need, security model, operational cost, and support burden
   - consider freemium positioning only after product and customer validation
- [ ] Nice to have: chunked, resumable transfers for multi-hour single-file uploads.
   - not required for the initial offering; current uploads stream directly from the browser to the host in a single request
   - a single long request cannot resume, so a dropped connection restarts that file from the beginning
   - an access token issued at the start of a very long transfer can expire before the transfer completes
   - revisit if real usage shows single files large enough to run for hours
   - chunking would also enable pause, resume, retry of only the failed portion, and clearer progress reporting
- [ ] Nice to have: keep access tokens valid for the life of a long transfer.
   - the browser captures the access token once when a batch starts, so a transfer that outlives the token begins failing with `401`
   - this is now the practical ceiling on a single upload, since the JS interop timeout no longer applies
   - options to weigh: refresh the token mid-batch, issue a scoped short-lived upload token per file, or treat it as solved by chunked uploads above
   - no action for now; revisit alongside chunked, resumable transfers

It is viable to open LAN Portal to third-party and first-party additions, but the platform should treat this as a deliberate capability rather than loading arbitrary plug-ins into the host process.

Use these terms consistently:

- **Apps**: the user-facing term for optional installed capabilities.
- **Extensions**: the developer and platform term for the integration model.
- **App**: an individual installed package or capability.

An extension model should eventually define:

- installation, removal, enablement, and disablement
- version compatibility and update behavior
- navigation and page registration
- configuration storage
- lifecycle and error handling
- explicit permissions and capabilities
- trust, signing, and source information

The security boundary matters more than the menu. An extension that runs as arbitrary server-side code inside the main API process should be considered trusted host code. A safer long-term direction is a constrained, packaged app model with explicit capabilities such as read-only file access, write access, local network access, user-data access, and background execution.

## Recommended navigation

For the near term, add an **Apps** entry inside the existing **Portal** menu:

```text
Portal
  File Sharing
  Apps
  Admin
  Access History
  Advanced
```

The Apps page should be the predictable management home for optional capabilities. It can eventually provide:

- installed apps
- available apps
- updates
- enable or disable controls
- app descriptions and settings
- permissions and trust information
- install and remove actions

Do not begin with a separate top-level Apps menu. That would make the core product look more complicated and may imply that users are expected to install something. A separate top-level area can be reconsidered when apps become a major part of normal usage and need their own discovery or marketplace experience.

When installed apps eventually add their own pages, keep them visually grouped as optional apps rather than mixing them indistinguishably with the core workflow:

```text
Portal
  File Sharing
  Admin
  Access History
  Advanced

Apps
  Photo Gallery
  Media Library
```

A useful product rule is: **Apps may extend Portal, but they do not redefine Portal.** File Sharing must remain complete and easy to use without any installed apps.

## Staged adoption

### Stage 1: Keep the core simple

Preserve File Sharing as the unmistakable primary experience. Do not expose an empty app ecosystem just to signal future plans.

### Stage 2: Complete Host Advanced and settings maturity

The current work is the Host Advanced page, including the operational settings structure, persisted UI state, and the refined Advanced organization. This work is the immediate priority before broader platform additions are reconsidered.

### Stage 3: Add Apps management

When there is a real app-management experience, add Apps under Portal. The first version can support built-in or locally packaged apps while establishing the navigation, package, and lifecycle concepts.

### Stage 4: Add controlled app integration

Allow installed apps to contribute pages or features through a stable contract. Add permissions, compatibility checks, update handling, and clear failure isolation before opening distribution to third parties.

### Stage 5: Consider broader discovery

Only consider a separate top-level Apps area or an app catalog after several useful apps exist and users regularly discover, install, update, or manage them.

## Direction to preserve

- File Sharing is the product's center of gravity.
- Apps are optional expansion, not evidence of unfinished core functionality.
- Management belongs under Portal before Apps becomes a primary product area.
- Third-party extensions need a defined trust and permission model before distribution.
- The default experience should remain useful, understandable, and complete without extensions.
