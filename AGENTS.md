# AGENTS.md

## Purpose

This repository uses documentation as the source of truth for product direction and implementation status.

AI assistants and contributors must not rely on memory, assumptions, or stale issue notes when answering roadmap or planning questions.

## Required workflow

Before answering any question about current product direction, next steps, status, or roadmap, do all of the following:

1. Read the current project status in the canonical docs first:
   - README.md
   - docs/product-roadmap.md
   - docs/client-navigation-implementation-checklist.md
   - docs/host-navigation-and-advanced-settings-plan.md
2. Confirm whether the docs still match reality.
3. If the docs are stale, update them in the same change before answering.
4. Answer only from the updated documentation and the current code state.

## Canonical status

The current documented status is:

- Access-request / Account polish is complete.
- The signed-out and signed-in route behavior is stable.
- Apps management is deferred.
- The active current work is the Advanced page in the Host.

## Direction rules

- Keep product documentation current with implementation reality.
- Do not describe Apps management as the active near-term direction while it is intentionally deferred.
- Keep the core LAN file-sharing experience as the primary focus.
- Treat Host Advanced work as the current operational priority while the core access flow remains stable.
- When direction changes, update the docs in the same work item instead of leaving the repo in a stale state.

## "What's next?" response rule

Any answer to "What's next?" must be based on the canonical docs and must explicitly reflect the current priorities:

- finish the current Host Advanced work
- keep the access-request flow stable and documented
- defer broader Apps management until after Advanced is complete

If documentation does not match reality, update the documentation first and then answer.
