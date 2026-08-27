# Product Roadmap

## What is next?

LAN Portal's primary product promise is simple LAN file sharing for people who should not need much technical knowledge. That remains the center of the product. Future extensibility should expand that experience without making the first-use workflow feel like an app platform.

Current status:

- Access-request / Account polish is complete.
- Signed-out and signed-in route behavior is stable.
- Apps management is intentionally deferred.
- Host Advanced Logs and the focused Security control are complete for the accepted scope.
- The active current work is Host Advanced validation and release-readiness work.

The current client-side UI/UX implementation work is tracked in the [Client Navigation Implementation Checklist](client-navigation-implementation-checklist.md). The current Host-side operational work is tracked in the [Host Navigation And Advanced Settings Plan](host-navigation-and-advanced-settings-plan.md).

## Next tasks

These are the immediate priorities in order:

1. [x] Finish the wider Host Advanced validation pass.
   - [x] confirm saved section state behaves correctly
   - [x] verify access history still works after the redesign
   - [x] ensure local-only access checks remain enforced across the page
   - [x] keep the access-request flow stable and documented

2. Complete automatic asset versioning for release readiness.
   - replace manual `?v=...` maintenance in controller-served HTML
   - cover all referenced JavaScript and CSS assets
   - verify changed assets are loaded after deployment and restart
   - this remains required before `v1.0.0.0` general release

3. Complete the runtime settings integration work.
   - define the typed settings model and validation pattern
   - keep direct DB access inside the settings store
   - connect the remaining settings surfaces to the typed facade

4. Revisit Apps management only after the Advanced work is complete.
   - keep the current focus on Host operations and file-sharing stability
   - do not make app management the near-term priority while Advanced remains open

## Release Requirements

The following item is medium importance during normal development but is required before the `v1.0.0.0` general release:

- [ ] Add automatic cache-busting or content-based versioning for every referenced JavaScript and CSS asset in controller-served HTML.
   - cover all Host and API-served HTML pages
   - remove manual query-string version maintenance as the release approach
   - verify that updated assets are loaded after deployment and application restart
   - do not declare the product ready for general release until this is complete

## Apps and extensions

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
