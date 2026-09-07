# feat-001: Controller HTML and asset versioning refactor

Status: current
Source: docs/product-roadmap.md (Next tasks #6, Release Requirements)
Required before `v1.0.0.0` general release.

## Description

Extract controller-owned HTML into maintainable Razor views in the API project and replace
manual `?v=...` cache-busting with automatic content-based asset versioning.

## Acceptance criteria

- [ ] Extract controller-owned HTML into maintainable Razor views in the API project.
- [ ] Keep request validation and dynamic data preparation in controllers; pass values through view models.
- [ ] Move inline JavaScript into separate static files where practical.
- [ ] Replace manual `?v=...` maintenance with automatic content-based asset versioning.
- [ ] Cover all referenced JavaScript and CSS assets across Host and API-served HTML pages.
- [ ] Verify changed views and assets are loaded after deployment and restart.
- [ ] Keep Host, API, Web, and published artifacts on the same release version.
