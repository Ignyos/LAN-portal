
## Release 0.2.0.262182240

### Highlights
- Corrected release version persistence so project assembly versioning remains valid while preserving full release identity.
- Hardened version parsing and validation to block invalid numeric version segments before publish.
- Updated dev-version timestamp generation to a compact numeric format compatible with version constraints.

### Added
- Added core-version extraction in publish flow to persist `major.minor.patch` into project `<Version>` while storing full release version in `<InformationalVersion>`.
- Added numeric component validation for all semantic version numeric segments before accepting publish versions.

### Changed
- Changed host project metadata defaults to separate stable assembly version (`0.2.0`) from informational release version (`0.2.0.262170003`).
- Changed dev version suggestion logic to use UTC year/day-of-year/hour/minute numeric stamping for fourth-node generation.
- Changed dev installer default version stamp generation to use the same compact UTC numeric format.

### Fixed
- Fixed publish behavior that wrote long fourth-node release values directly into project `<Version>`, which could break restore/build.
- Fixed version validation flow to reject oversized numeric nodes that do not fit required integer bounds.

### Operational Notes
- Publish workflows now maintain a stable project version core and update informational version metadata for release identity.
- Dev publish version defaults now follow a compact UTC stamp format and should remain valid for restore/build workflows.
- Existing release automation can continue using four-node versions, but only valid numeric bounds are accepted.

### Risk / Impact
- Runtime behavior risk: Version metadata interpretation now splits between assembly version and informational version, which may affect scripts or tooling that previously read only one field.
- Deployment or configuration risk: Pipelines or external tools that assume full release version is stored in `<Version>` may require adjustment to read `<InformationalVersion>`.
- User-facing risk: Displayed version strings may differ from assembly version values in diagnostics if consumers do not read informational metadata.

### Verification Notes
- Verify `dotnet restore` and `dotnet build` succeed after publish updates a new release version.
- Verify generated publish versions with four nodes are accepted only when numeric segments remain within valid integer bounds.
- Verify project file updates preserve `<Version>` as `major.minor.patch` and set `<InformationalVersion>` to the full release version.
- Verify dev publish default suggestion uses UTC year/day-of-year/hour/minute numeric stamping and remains build-valid.

