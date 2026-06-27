# PSTui CI/CD

PSTui's pipeline follows the [tui-cs/clet](https://github.com/tui-cs/clet)
model, adapted for a **PowerShell binary module published to the PowerShell
Gallery** (rather than a NativeAOT CLI shipped to NuGet/Homebrew/WinGet).

## Branching model

| Branch     | Role                                                            |
| ---------- | -------------------------------------------------------------- |
| `develop`  | **Default.** Day-to-day work and PRs land here.                |
| `main`     | **Release-only.** Merging `develop` → `main` ships a release.  |

## Workflows

### `ci-test.yml` — Continuous Integration
Builds and tests on every push / PR to `develop` and `main` (and merge queue)
across Windows, macOS, and Linux via `Invoke-Build Build, Test`. Packaging and
publishing are release-only activities and live in `release.yml`.

### `release.yml` — Build, Test, Version, Publish
Triggered by:
- **push to `main`** (on `src/**`, `test/**`, or the build files),
- **`workflow_dispatch`** (optional `version_override`), and
- **`repository_dispatch`** (`terminal-gui-published`) so Terminal.Gui can
  trigger a rebuild/republish when it ships a new version.

It resolves the version, builds + tests, stamps the manifest, publishes to the
PowerShell Gallery, tags the commit, and creates a GitHub Release. A
`notify-failure` job opens/updates a `release-failure` issue if anything fails.

## Versioning

The source of truth is **`PSTui.Common.props`**:

- `<VersionPrefix>` — base version, e.g. `1.0.0`.
- `<VersionSuffix>` — prerelease phase; **empty for stable**, otherwise a label
  like `rc`, `beta`, or `alpha`.

To move between phases, change these and merge to `main`:

| `VersionPrefix` | `VersionSuffix` | Produces            | PSGallery                                  |
| --------------- | --------------- | ------------------- | ------------------------------------------ |
| `1.0.0`         | `rc`            | `1.0.0-rc1`, `-rc2` | prerelease (`Install-Module -AllowPrerelease`) |
| `1.0.0`         | *(empty)*       | `1.0.0`, `1.0.1`…   | stable (default `Install-Module PSTui`)    |

> **PowerShell prerelease labels are alphanumeric only** (no dots) — so the
> pipeline emits `rc1`, `rc2`, … (not clet's `rc.1`). The build number
> auto-increments off the latest matching `v…` git tag.

## Required configuration

- **`PSGALLERY_API_KEY`** repo secret — the PowerShell Gallery API key (under the
  tui-cs org). **Until it is set, `release.yml` runs as a dry run**: it builds,
  tests, and resolves the version but skips publishing, tagging, and the GitHub
  Release. This lets the pipeline be validated safely before going live.

## Status

The pipeline is **live and exercised end-to-end** (issues #5 and #6 closed). The
`PSGALLERY_API_KEY` secret is configured and PSTui is published to the PowerShell
Gallery — merging `develop` → `main` ships the next version automatically.
