# feat-001: Controller HTML and asset versioning refactor

Status: current
Source: docs/product-roadmap.md (Next tasks #6, Release Requirements)
Required before `v1.0.0.0` general release.

## Description

Extract controller-owned HTML into maintainable Razor views in the API project and replace
manual `?v=...` cache-busting with automatic content-based asset versioning.

## Acceptance criteria

- [x] Extract controller-owned HTML into maintainable Razor views in the API project.
- [x] Keep request validation and dynamic data preparation in controllers; pass values through view models.
- [x] Move inline JavaScript into separate static files where practical.
- [x] Replace manual `?v=...` maintenance with automatic content-based asset versioning.
- [x] Cover all referenced JavaScript and CSS assets across Host and API-served HTML pages.
- [x] Verify changed views and assets are loaded after deployment and restart.
- [ ] Keep Host, API, Web, and published artifacts on the same release version.

## Verification notes

- API build succeeds without API warnings.
- `AssetVersionServiceTests`: 2 passed, 0 failed.
- `scripts/dev-test.ps1`: passed.
- The active Setup, Admin, Settings, About, and Advanced routes use Razor views and versioned static assets.
- The obsolete Advanced controller HTML and inline script were physically removed in the confirmed cleanup slice.
- Fresh API-process verification returned HTTP 200 for all extracted pages and every rendered hashed CSS/JavaScript asset.
- A full solution build with Host version `0.3.0.9` produced matching API, Web, and Host assembly/file versions.
- Published artifact version parity remains covered by the existing Host-version propagation path, but no installer/package was produced in this slice.

## Confirmed slices

- 2026-09-07: Developer confirmed the Advanced cleanup slice after API build success and two passing `AssetVersionServiceTests`.
- 2026-09-07: Developer confirmed fresh-process page/asset verification and solution version-parity verification.
