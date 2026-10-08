# Changelog

All notable changes to AfterThemed are recorded here. Dates use `YYYY-MM-DD`.

## 2.0.0 - 2026-10-08

### Interface

- Restyle the editor with a distinct identity in the existing blue and ice colors: Familjen Grotesk headings, the native Segoe UI Variable for controls, and IBM Plex Mono for hex codes, contrast ratios, paths, and logs.
- Give shape a hierarchy: **Install theme** is the only pill; panels and controls use small, square-shouldered corners.
- Show the seven color roles as one continuous specimen bar with each role's hex code underneath.
- Replace slogans with plain descriptions, show the target After Effects version above the preview, and name the palette a theme is based on.
- Remove decorative sparkle icons, the pulsing "Live" dot, and icons on plain text buttons.

### Themes and installation

- Add **Theme a DLL file**: theme a `dvaui.dll` (and optionally `AfterFXLib.dll`) copied from another PC into a new folder beside it, with install instructions. The chosen files are never changed, and files that fail the Adobe signature check are themed only after a warning.
- **Install to every detected version** now generates each version from its own preserved original and installs all of them with a single Windows permission prompt. A version that cannot be themed is skipped and reported; a failed install rolls back the whole set.

## 1.3.13 - 2026-10-08

### AEP Downgrader

- Add an **AEP Downgrader** tab. Drop `.aep` projects in (or pick them), choose After Effects 24.x, 23.x, 22.x, or 18.x, and AfterThemed writes a converted copy beside each original as `name (AE 23.x).aep`. Originals are never modified, existing files are never replaced, and a failed conversion leaves nothing behind.
- Conversions reproduce After Effects' own *Save As*: the version header is rewritten; Material Options › Shadow Color is removed for 23.x and older; Light Transmission, the four-byte layer-record field, and the spatial property flag added in 23.x are removed for 22.x and older; and a preview setting added after 18.x is removed for 18.x. Everything else is copied byte for byte. 19.x, 20.x, and 21.x use the 18.x format.
- Verified against After Effects 25.6, 24.6, 23.6, 22.6, and 18.4 saves, and by opening converted projects in After Effects 23.6 and 18.4.
- Add `--downgrade-aep <input> <output> <major>` for command-line conversion.

### Palette tools

- Show a WCAG contrast score for interface text and each accent, list failing pairs, and fix them with **Fix** or **Fix all** by adjusting only lightness.
- Add **Match** (build Background, Panels, Raised, and text from the primary color), **Shuffle** with per-role locks, and **Image** (extract a palette from an image or a dropped file).
- Hovering a color role highlights every place it appears in the live preview. The preview now shows the Danger color as an expression-error marker.

### Sharing and history

- Share a theme as a short `AT1-…` code and apply codes from others.
- Export and import `.afterthemed` files, including a PNG thumbnail. Imported files are validated strictly before anything is applied.
- Keep a history of the last ten installed themes with **Load** and **Reinstall**.
- Add a community **Gallery** read from `gallery/index.json`, with **Submit current theme**. Gallery themes never apply text replacements.

### Installation

- Make **Restore stock After Effects** a prominent action with a confirmation dialog.
- Optionally install to every detected After Effects version at once.
- Notice when an After Effects update replaced an installed theme and offer **Re-apply**.
- Remember the theme being edited and reopen it on the next launch.

### Interface

- Add a Themes | AEP Downgrader switch to the title bar.
- Add the **Fall** appearance: ember and cream with a cocoa preview, gold and mustard accents, a paper texture, a leaf logo, a leaf-colored Install animation, and suggested fall palettes.
- Add 28 built-in palettes (50 in total): Titanium, Deep Sea, Plum Studio, Copper, Moss, Monochrome, Electric Violet, Arctic, Warm Paper, Cherry Graphite, Catppuccin Macchiato, Catppuccin Frappé, Catppuccin Latte, Rosé Pine Moon, Rosé Pine Dawn, Tokyo Night Storm, Everforest Light, Ayu Mirage, Nightfox, Poimandres, Vesper, Graphite Amber, Sunset Dusk, Sakura, Harvest, Maple, Forest Floor, and Golden Hour.
- Replace the logo and application icon with the new pixel-ring mark.
- Rework the update prompt in the editor's style with **Download update**, **View release**, and **Skip this version**.
- The Install button fills with a pixel scanline on hover and loops it while installing; the animation also runs when Windows animation effects are turned off.
- Fix cramped controls in the project panel and the double focus ring on dropdowns.

### Installer

- Brand the installer with the new artwork and colors.
- Offer **Install for me only** or **Install for all users**, folder and Start menu pages on a fresh install, and an optional sign-in launch.
- Associate `.afterthemed` files with AfterThemed and add **Downgrade with AfterThemed** to the Explorer menu for `.aep` files.

### Earlier unreleased changes

#### Interface

- Redesign the editor with a consistent charcoal workspace, layered panels, clearer live preview, and compact color cards that fit at the minimum window size.
- Make color swatches reachable by keyboard with a visible focus outline.
- Match the reference's rounded cards with a 40px radius on project and controls panels and a 17px radius on compact color cards. Keep the live preview's tighter frame for its text and sample.
- Paint rounded card edges without a hard clipping region so square corner artifacts do not show through the antialiased curves.

#### Releases and updates

- Publish tested Windows installers, SHA-256 checksums, and license files to GitHub Releases on matching version tags. Keep releases in draft until every asset is uploaded; prevent overwriting published releases.
- Read the installer version from the app project and reject mismatched release tags.
- Add Update and Ignore actions to the startup popup. Remember ignored versions across restarts and notify again for a newer release; Escape dismisses for the current session only.
- Treat three- and four-component versions with trailing zeroes as equal and keep the popup open if the browser cannot launch.

#### Import themes from DVAUI DLLs

- Add `.dll` to Import Theme. Read supported native color tables and embedded Spectrum JSON resources directly from donor DLL data, including themed DLLs, and load an editable palette into the existing preview.
- Recognize built-in palettes and retain their mapping settings; infer roles from frequently occurring visible colors for unknown palettes. Generate and Install apply the imported palette using the selected target's own supported layout.
- Keep imported accent and foreground settings when reading the editor's custom/imported palette.

#### Original recovery

- Replace the blocked native-generation path with a recovery dialog that searches current and legacy AfterThemed backups and accepts a user-selected backup folder or DLL.
- Import only Adobe-verified backups matching the selected DLL's exact file version, identity, architecture, linker timestamp, and section layout. Verify the staged bytes again, preserve them with recovery protection, and resume the requested operation.
- Restore can recover a missing original from an older backup before creating restore output. Cancel exits cleanly without another error/report popup.
- Correct the distinction between a missing snapshot and a present snapshot that failed verification; include recognized built-in theme information for rejected snapshots.

#### Originals library

- Group original snapshots by After Effects release, with readable DLL and exact-build subfolders and collision-safe capture IDs.
- Automatically organize old hash-named folders while preserving DLL bytes, protection records, and active restore selection; retain conflicting or malformed records for inspection.
- Recover active pointers after interrupted organization, retain compatibility with unmigrated snapshots, and serialize library access during capture, lookup, restore, and migration.
- Add library instructions and an explicitly labeled AE-version library entry point. Organized libraries require 1.3.13 or later.

#### Protected originals and recovery

- Captures now validate the stored bytes, serialize simultaneous captures, and publish the DLL, metadata, Windows DPAPI protection record, and recovery copy together. Published files are read-only.
- Restore verifies authenticated metadata and SHA-256, uses the recovery copy if the primary is damaged or missing, and recreates a missing installed DLL. A damaged installed PE no longer prevents restore.
- Unprotected legacy backups must pass Adobe Authenticode verification before they are promoted. A matching JSON hash alone no longer allows a modified legacy backup to become an original. Invalid backups are retained; incomplete same-key captures are quarantined when replaced by a valid capture.
- Originals captured by the new engine are bound to the installation path and AfterFX.exe hash to prevent recovery into a different Adobe host build.
- Native rollback also handles a replacement that throws after changing the target. Complete GUI operations are serialized across windows to protect shared inputs.
- AE 2025's V2/V4/V5 companion layout is supported; recognized but incomplete companion layouts now stop generation instead of silently skipping native colors.
- Activity and exception details persist locally in the Logs folder; startup initialization failures are handled in the UI. CI now runs the CEP apply/restore smoke test.

#### Added

- After Effects installations are now discovered across the whole machine instead of only the two default `Program Files` folders. The uninstall registry, Adobe's own `InstallPath` keys, and the Adobe layout on every fixed drive are merged and de-duplicated, so an installation moved off the system drive is found.
- A startup chooser names every detected release and confirms which one to theme. It appears when there is a choice to make — several installations, or no usable remembered target — and the button beside `INSTALLED TARGET` reopens it at any time. That button previously opened a bare file dialog; the chooser lists the detected releases and still offers a manual browse.
- A `REPORT BUG` button collects a diagnostics bundle and opens a prefilled GitHub issue. AfterThemed never uploads `dvaui.dll` or `AfterFXLib.dll`: the report describes them by version, size, SHA-256, and theme-resource layout, which is what identifies a build, while Adobe's binaries stay on the user's PC. Nothing is sent until the user posts the issue from their own account.
- `--list-installs` prints what the detection engine sees, so a missed installation can be reported without screenshots.
- AfterThemed now checks GitHub for the latest release when the app opens. If a newer version is available, a popup offers the installer download and release page.

#### Fixed

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
