# Versioning Strategy & Release Notes

## Versioning Policy

PayBridge follows semantic versioning for package publication.

- MAJOR: breaking API or behavior changes.
- MINOR: backward-compatible features (new gateway, new optional capability).
- PATCH: backward-compatible fixes, security hardening, and documentation corrections.

## Release Triggers

- Tag pattern: v*.*.*
- NuGet publish workflow builds and pushes package from tagged commit.
- GitHub release is created from the same tag.

## Release Readiness Gate

Before tagging, confirm:

1. Unit tests pass in CI.
2. Integration gate passes with at least one real sandbox provider.
3. Security checks pass (secret scanning + dependency checks).
4. Docs build strict mode passes.
5. README, package metadata, and docs claims align with implementation.

## Versioned Docs Strategy

Current state:

- Primary docs are published at latest (master) on GitHub Pages.

Recommended expansion:

1. Keep latest docs at root (/).
2. Generate version snapshots per release tag under /v/{version}/.
3. Include a version selector in navigation.
4. Maintain migration guides for breaking changes.

## Minimal implementation approach

- On release tag workflow:
  - Build docs for the tagged commit.
  - Publish artifacts into /v/{version}/ in Pages artifact.
  - Keep root pointing to latest stable.

## Release Notes

## Current line highlights

## v1.2.x

- PeachPayments gateway support and related operational/security hardening.
- Webhook security enforcement and replay-safety improvements.
- Startup configuration validation improvements for enabled/default gateways.
- Documentation portal redesign and quality gates.

## Template for next release

```markdown
## vX.Y.Z - Release title

### Added
- New capabilities

### Changed
- Behavior changes and compatibility notes

### Fixed
- Bug fixes and security remediations

### Operational Notes
- Required config changes
- Migration steps
```
