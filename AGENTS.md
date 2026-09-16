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

## Project management

In-flight, itemized work lives under `PM/` (backlog, current, release-candidate-dev, release-candidate, completed). See [PM/workflow.md](PM/workflow.md) for lifecycle rules and [PM/project-management.md](PM/project-management.md) for the current index. The roadmap owns product direction and priority; `PM/project-management.md` owns the list of in-flight items; each PM item file owns that item's acceptance criteria, verification notes, and lifecycle status. This file records the rules for reconciling those sources and should not duplicate item-level acceptance status.

## Canonical status

The current documented status is:

- Access-request / Account polish is complete.
- The signed-out and signed-in route behavior is stable.
- Apps management is deferred.
- The active current work is the item listed in `PM/project-management.md` under `current/`, interpreted according to the priorities in `docs/product-roadmap.md`.

## Direction rules

- Keep product documentation current with implementation reality.
- Do not describe Apps management as the active near-term direction while it is intentionally deferred.
- Keep the core LAN file-sharing experience as the primary focus.
- Treat the active PM item and its documented acceptance criteria as the current operational priority while the core access flow remains stable.
- When direction changes, update the docs in the same work item instead of leaving the repo in a stale state.

## "What's next?" response rule

Any answer to "What's next?" must be based on the canonical docs and the PM index/item files. Treat the current item listed under `PM/project-management.md` as the active execution target, and use `docs/product-roadmap.md` to determine its priority and next steps.

The response must keep the access-request flow stable and documented, and defer broader Apps management until the roadmap says the active release-readiness work is complete.

If documentation does not match reality, update the documentation first and then answer.
