# task-002: Version parity verification across publish outputs

Status: backlog
Source: docs/product-roadmap.md (Release Requirements)
Required before `v1.0.0.0` general release.

## Acceptance criteria

- [ ] Use `Ignyos.LanPortal.Host/Ignyos.LanPortal.Host.csproj` as the single checked-in version source.
- [ ] Pass the resolved release version to every published project.
- [ ] Verify Host, API, Web, installer metadata, and release metadata do not report conflicting versions.
