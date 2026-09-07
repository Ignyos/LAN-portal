# feat-007: Native companion app for reliable mobile upload

Status: backlog
Source: docs/product-roadmap.md (Possible Future Lanes), docs/mobile-friendly-client-implementation-checklist.md

## Description

A reliable mobile upload path, likely via a native companion app. Multi-file selection through the
browser's native picker is inconsistent on Android because the OS picker backgrounds the tab long
enough to disconnect the Blazor Server SignalR circuit.

## Notes

- Out of scope for `v1.0.0.0`; a markup or event-binding fix is not expected to resolve this reliably.
- Revisit as a native app or an alternate, non-circuit-dependent upload entry point for mobile.
