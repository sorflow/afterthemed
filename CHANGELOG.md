# Changelog

All notable changes to AfterThemed are recorded here. Dates use `YYYY-MM-DD`.

## Unreleased

### Added

- After Effects installations are now discovered across the whole machine instead of only the two default `Program Files` folders. The uninstall registry, Adobe's own `InstallPath` keys, and the Adobe layout on every fixed drive are merged and de-duplicated, so an installation moved off the system drive is found.
- A startup chooser names every detected release and confirms which one to theme. It appears when there is a choice to make — several installations, or no usable remembered target — and the button beside `INSTALLED TARGET` reopens it at any time. That button previously opened a bare file dialog; the chooser lists the detected releases and still offers a manual browse.
- A `REPORT BUG` button collects a diagnostics bundle and opens a prefilled GitHub issue. AfterThemed never uploads `dvaui.dll` or `AfterFXLib.dll`: the report describes them by version, size, SHA-256, and theme-resource layout, which is what identifies a build, while Adobe's binaries stay on the user's PC. Nothing is sent until the user posts the issue from their own account.
- `--list-installs` prints what the detection engine sees, so a missed installation can be reported without screenshots.
- AfterThemed now checks GitHub for the latest release when the app opens. If a newer version is available, a popup offers the installer download and release page.

### Fixed

- Release ordering no longer follows the `dvaui.dll` file version. After Effects CC 2019 ships dvaui 16.1 while After Effects 2021 ships dvaui 15.4, so a version-ordered list offered a 2019 release ahead of a 2021 one. Ordering now follows the release year recorded in the installation folder.
- The version shown in About and in bug reports no longer includes the full commit hash appended to the informational version. The commit is kept in short form, which still identifies the exact build.
- Interface text is no longer drawn without antialiasing. Buttons, tabs, dropdown rows, and labels were rendered through GDI, which drops antialiasing when it draws into the alpha-capable buffer these double-buffered controls paint into, leaving hard-edged glyphs; GDI also ignores the `ClearTypeGridFit` hint those controls set, so the intended smoothing never applied. Text now renders through GDI+ with grayscale antialiasing, which does not fringe on light-on-dark text.
- The title bar actions are now a pill navigation: one rounded track holding fully rounded buttons, each with an outlined glyph stating what it does, and the install action filled with the brand accent.
- A tightly rounded panel no longer shows flat notches where its ends should close. The rounded outline was enforced with a clipping region, which is one bit per pixel and saws the antialiased curve into a hard step; a panel whose children stay inside the curve now paints its rounded body antialiased over the host surface instead.
- Pill labels are measured rather than assigned a fixed width, so a renamed button cannot silently ellipsize.
- The title bar no longer clips its buttons. The action strip sized itself to a fixed percentage of the window, which was narrower than the buttons needed at the default window size and clipped every one of them at the minimum size. The bar's outer columns now take exactly the width their contents need.

## 1.3.12 - 2026-08-25

### Fixed

- Installing a theme no longer fails with "Access to the path is denied" when the installed `dvaui.dll` carries the read-only attribute, which Adobe ships or a later Creative Cloud repair sets. `File.Move` refuses to overwrite a read-only destination regardless of the caller's actual permissions, so the elevated installer reached the DLL-replacement stage, made no change, and reported the target as denied. The attribute is now cleared before the target is replaced and before rollback restores it.
- A read-only installed target no longer leaves every backup read-only too. `File.Copy` carries the source's attributes onto the copy, so a read-only `dvaui.dll` was silently producing read-only backups that AfterThemed's own rollback and restore would later need to replace.

## 1.3.11 - 2026-08-25

### Added

- 16 new built-in presets modeled on popular editor and terminal color schemes: Catppuccin Mocha, Nord, Everforest, Tokyo Night, Kanagawa, Rosé Pine, Dracula, One Dark Pro, Solarized Dark, Solarized Light, Monokai, Ayu Dark, Night Owl, Oxocarbon, Synthwave '84, and Material Palenight. Each maps its own background, panel, raised surface, text, and primary/secondary/danger accent colors, and is recognized on reopen the same way the existing built-ins are.

### Fixed

- Importing a theme file that does not declare explicit roles no longer inverts dark palettes. Surfaces were classified by an HSV saturation reading that divides by brightness, which scores a dark but faintly tinted surface as a saturated accent: every one of Nord's dark surfaces was rejected, so the palette was rebuilt from the light text colors it had left, producing a light background, a purple body text, and the darkest surface returned as the primary accent. Surfaces are now measured by absolute chroma, light and dark palettes are distinguished by where the palette's own neutrals sit, and an accent must carry real color and separate from the background before it can fill an accent role.

## 1.3.10 - 2026-08-25

### Fixed

- Native `AfterFXLib.dll` theming now covers every After Effects release that stores color themes there, instead of only After Effects 2020. Releases do not report a comparable version number — CC 2019 and 2023 stamp the application version onto `dvaui.dll` while 2020 and 2021 stamp the DVA version — so the companion is now selected by the color resources it actually carries. CC 2019, which keeps no colors in `dvaui.dll` at all, previously received font changes only.
- Companion originals are now accepted on their embedded Adobe signer, because Adobe ships `AfterFXLib.dll` with an Authenticode hash that does not validate in any release from CC 2019 to 2025. Preserving and restoring the companion previously failed for every version. `dvaui.dll` still requires full Authenticode validation.
- An `AfterFXLib.dll` that AfterThemed already themed is no longer captured as though it were Adobe's original, so a themed companion cannot overwrite the preserved copy that Restore depends on.
- Native text and button labels now contrast with the surface they are actually drawn on. Foreground colors were previously chosen from the color's own name, which cannot tell which surface sits behind it, so a light raised surface kept light text and pressed-button labels were painted the same color as the button face. Each foreground role is now matched to the surface role declared for the same control, preferring the control's fill over the shadow, glow, or outline around it.

## 1.3.9 - 2026-08-24

### Fixed

- After Effects 2020 now generates, installs, restores, and rolls back its native `AfterFXLib.dll` color resources together with `dvaui.dll`, so the application frame no longer stays on Adobe's default palette while the Home surface is themed.
- Focused and selected foreground roles now retain readable contrast against accent-colored controls instead of being flattened into the same color as their background.

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

[1.3.12]: https://github.com/sorflow/afterthemed/releases/tag/v1.3.12
[1.3.11]: https://github.com/sorflow/afterthemed/releases/tag/v1.3.11
[1.3.10]: https://github.com/sorflow/afterthemed/releases/tag/v1.3.10
[1.3.9]: https://github.com/sorflow/afterthemed/releases/tag/v1.3.9
[1.3.8]: https://github.com/sorflow/afterthemed/releases/tag/v1.3.8
[1.3.7]: https://github.com/sorflow/afterthemed/releases/tag/v1.3.7
[1.3.6]: https://github.com/sorflow/afterthemed/releases/tag/v1.3.6
[1.3.1]: https://github.com/sorflow/afterthemed/releases/tag/v1.3.1
