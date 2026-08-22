# Product Roadmap

## What is next?

LAN Portal's primary product promise is simple LAN file sharing for people who should not need much technical knowledge. That remains the center of the product. Future extensibility should expand that experience without making the first-use workflow feel like an app platform.

The current client-side UI/UX implementation work is tracked in the [Client Navigation Implementation Checklist](client-navigation-implementation-checklist.md).

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

### Stage 2: Add Apps management

When there is a real app-management experience, add Apps under Portal. The first version can support built-in or locally packaged apps while establishing the navigation, package, and lifecycle concepts.

### Stage 3: Add controlled app integration

Allow installed apps to contribute pages or features through a stable contract. Add permissions, compatibility checks, update handling, and clear failure isolation before opening distribution to third parties.

### Stage 4: Consider broader discovery

Only consider a separate top-level Apps area or an app catalog after several useful apps exist and users regularly discover, install, update, or manage them.

## Direction to preserve

- File Sharing is the product's center of gravity.
- Apps are optional expansion, not evidence of unfinished core functionality.
- Management belongs under Portal before Apps becomes a primary product area.
- Third-party extensions need a defined trust and permission model before distribution.
- The default experience should remain useful, understandable, and complete without extensions.
