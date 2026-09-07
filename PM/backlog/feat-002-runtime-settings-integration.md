# feat-002: Runtime settings integration

Status: backlog
Source: docs/product-roadmap.md (Next tasks #7)

## Description

Complete the runtime settings integration work: typed settings model, validation pattern, and a
typed facade over direct DB access, plus a "run at Windows startup" option.

## Acceptance criteria

- [ ] Define the typed settings model and validation pattern.
- [ ] Keep direct DB access inside the settings store.
- [ ] Connect the remaining settings surfaces to the typed facade.
- [ ] Include an option to run LAN Portal at Windows startup, defaulting to `true`.
