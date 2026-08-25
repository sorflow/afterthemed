# Changelog

All notable changes to AfterThemed are recorded here. Dates use `YYYY-MM-DD`.

## Unreleased

## 1.3.8 - 2026-08-24

### Fixed

- Transitional DVA builds now patch Spectrum JSON and the native base-theme engine together, preventing Adobe's dark frame from remaining around a themed Home surface.
- Legacy native color discovery now recognizes the SSE register variants used by DVA 14.6 and the AVX encoding used by current DVA builds.
- DLL rollback is pinned to the originally verified backup hash so a backup changed after verification is never restored.

## 1.3.7 - 2026-08-24

### Fixed

- New installers now remove strictly older AfterThemed versions before installing, refuse accidental downgrades, verify old registration cleanup, and block upgrade/uninstall while the application is running.
- Elevated DLL installation failures now retain their failing stage and rollback result instead of being masked as a final SHA-256 mismatch.
- After Effects updates installed into an existing version folder now capture the new Adobe original, including immediately before Restore, instead of reusing a stale older or same-version DVAUI snapshot.
- CEP panel conflicts now surface as partial failures instead of being logged as successful operations.
- DVA 14.6's legacy `DROVER-VARS` theme resource is now recognized alongside current `DROVER-DNA-VARS` resources, with JSON array colors included in validation inventories.

## 1.3.6 - 2026-08-24

### Changed

- The repository now tracks source and documentation instead of compiled installers.
- Windows builds are verified by GitHub Actions; official binaries remain in GitHub Releases.
- Repository history and release references no longer retain packaged executables.

## 1.3.5 - 2026-08-24

### Added

- A proprietary end-user license agreement presented during installation.
- Installed copies of `EULA.txt` and `LICENSE.txt`.
- Direct access to legal notices from the About window.

### Changed

- Product version and installer metadata advanced to 1.3.5.

## 1.3.4 - 2026-08-24

### Changed

- Moved About AfterThemed from the editor tabs to the top application bar.
- Rebuilt About as a separate rounded product window based on the supplied AfterThemed SVG identity.

## 1.3.3 - 2026-08-24

### Added

- Blank server acknowledgements, special thanks, and project hashtags.

## 1.3.2 - 2026-08-24

### Added

- Creator credit, year and version information, social links, and the embedded AfterThemed SVG mark.

## 1.3.1 - 2026-08-24

### Added

- Signed CEP bundle theming through Adobe CEP developer mode.
- Persistent runtime palette overrides for host-theme scripts, CSS-in-JS, inline SVG colors, and canvas strokes.
- Verified panel backups with byte-exact restoration.

[1.3.8]: https://github.com/sorflow/afterthemed/releases/tag/v1.3.8
[1.3.7]: https://github.com/sorflow/afterthemed/releases/tag/v1.3.7
[1.3.6]: https://github.com/sorflow/afterthemed/releases/tag/v1.3.6
[1.3.1]: https://github.com/sorflow/afterthemed/releases/tag/v1.3.1
