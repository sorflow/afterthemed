<p align="center">
  <img src="src/AfterThemed/Assets/AfterThemed-Mark.svg" width="112" alt="AfterThemed mark">
</p>

<h1 align="center">AfterThemed</h1>

<p align="center"><strong>A controlled way to rebuild the After Effects interface around your own palette.</strong></p>

<p align="center">
  AfterThemed brings native colors, interface typography, and CEP panel styling into one Windows workspace—with a preview before the first file is touched and a verified path back to the original.
</p>

<p align="center">
  <a href="https://github.com/sorflow/afterthemed/actions/workflows/build.yml"><img src="https://github.com/sorflow/afterthemed/actions/workflows/build.yml/badge.svg" alt="Build status"></a>
  <a href="https://github.com/sorflow/afterthemed/releases/latest"><img src="https://img.shields.io/github/v/release/sorflow/afterthemed?display_name=tag&style=flat-square" alt="Latest release"></a>
  <a href="LICENSE.txt"><img src="https://img.shields.io/badge/license-proprietary-7657ff?style=flat-square" alt="Proprietary license"></a>
  <img src="https://img.shields.io/badge/platform-Windows%20x64-27b7f5?style=flat-square" alt="Windows x64">
</p>

<p align="center">
  <a href="https://github.com/sorflow/afterthemed/releases/latest"><strong>Download the latest release</strong></a>
  ·
  <a href="CHANGELOG.md">Changelog</a>
  ·
  <a href="CONTRIBUTING.md">Contributing</a>
  ·
  <a href="SECURITY.md">Security</a>
</p>

<!--
PRODUCT VISUAL SLOT
Reserve this position for the finished 16:9 overview artwork. A 1600 × 900
image works well here at full README width.
-->

## One editor, three surfaces

| Native interface | Extension panels | Typography |
| --- | --- | --- |
| Map validated DVAUI color roles to a custom palette without changing the DLL's size or architecture. | Discover CEP panels, rewrite supported HTML and CSS colors, and maintain a persistent host-theme override. | Choose from installed Windows font families that fit the fixed-width DVAUI font slots safely. |

ScriptUI panels are inventoried separately. AfterThemed does not pretend compiled JSX or JSXBIN code is CSS.

## The workflow

```text
Choose installation
        ↓
Preserve the signed original
        ↓
Build or import a palette
        ↓
Preview native UI and panel roles
        ↓
Generate, install, restart After Effects
        ↓
Restore from verified backups whenever needed
```

Every native edit begins from an immutable Adobe-signed original captured on the user's machine. Generated variants are checked before installation, the current target is backed up before replacement, and restore output is verified by hash.

## What it handles

- Structurally validated Windows x64 DVAUI releases in the CC 2018–2026 range, including the 14.6 `DROVER-VARS` and 26.3 `DROVER-DNA-VARS` layouts.
- Native DVAUI background, panel, raised-surface, text, primary, secondary, and danger roles, including the companion native color resources used by After Effects 2020.
- Built-in palettes plus imports from `.theme`, `.css`, `.json`, and `.xml` files.
- CSS hexadecimal, RGB, HSL, ARGB, and gradient color discovery.
- Fixed-length UI text replacement with explicit size checks.
- Installed-font discovery filtered for DVAUI-compatible family names.
- CEP discovery across the selected After Effects installation and standard user and system locations.
- Runtime CSS variables for panels that rebuild their interface after launch.
- Inline SVG, CSS-in-JS, ordinary canvas-stroke, and host-theme color adaptation.
- Per-file panel backups, operation reports, and byte-exact restoration.

## Safety model

AfterThemed works on files that can stop After Effects from launching when handled carelessly. The application keeps the risky parts narrow and visible:

1. The selected original must pass structure and Adobe-signature checks.
2. Original snapshots are stored outside the application installation directory.
3. A generated DLL must keep the original architecture, length, and expected structure.
4. For After Effects 2020, the validated DVAUI and companion native color file are installed or restored as one rollback-safe set.
5. A newer setup removes the registered older app version before installing and leaves snapshots, backups, and settings in the separate user-data directory.
6. Installation is blocked while After Effects is running.
7. Existing targets and every panel file are backed up before replacement.
8. Restore operations verify the recovered bytes instead of assuming the copy succeeded.

Keep an external backup of important installations. Product updates, security software, permissions, disk failures, and third-party extension behavior remain outside AfterThemed's control.

## Install

1. Download `AfterThemed-Setup-1.3.10.exe` from the [latest release](https://github.com/sorflow/afterthemed/releases/latest).
2. Review and accept the proprietary EULA in Setup.
3. Launch AfterThemed and confirm the detected After Effects installation.
4. Build a palette or import an existing theme.
5. Close After Effects, generate the variant, and install it.
6. Restart After Effects after applying native or panel changes.

The installer and every other compiled binary live in [GitHub Releases](https://github.com/sorflow/afterthemed/releases), not in repository history.

## Compatibility

| Requirement | Current support |
| --- | --- |
| Operating system | Windows 10 or later, x64 |
| Runtime | Self-contained .NET 9 desktop build |
| Host application | Adobe After Effects installations that pass AfterThemed's structural validation |
| Native target | x64 `dvaui.dll` selected from a licensed local installation; After Effects 2020 also uses its validated companion `AfterFXLib.dll` color resources |
| Web panels | CEP extensions with writable HTML, CSS, SVG, or supported runtime styles |
| ScriptUI | Discovery and inventory; native ScriptUI execution remains owned by the panel author |

Compatibility is validated against the selected file rather than inferred from a folder name or marketing version.

## Build from source

The repository is source-available under proprietary terms. Building requires Windows, the .NET 9 SDK, and optionally Inno Setup 6.

```powershell
dotnet restore .\src\AfterThemed\DvauiThemeEditor.csproj
dotnet build .\src\AfterThemed\DvauiThemeEditor.csproj --configuration Release
```

Build the self-contained installer:

```powershell
.\src\AfterThemed\Installer\Build-Installer.ps1
```

GitHub Actions runs the same Windows build for every push and pull request. Workflow artifacts are unsigned development builds; public downloads should come from Releases.

## Repository guide

| Path | Contents |
| --- | --- |
| `src/AfterThemed` | WinForms application, theming engine, backup logic, and installer scripts |
| `.github/workflows` | Reproducible Windows x64 build |
| `.github/ISSUE_TEMPLATE` | Structured bug and feature reports |
| `CHANGELOG.md` | Release history |
| `SECURITY.md` | Private vulnerability reporting policy |
| `EULA.txt` | End-user agreement shown by Setup |
| `LICENSE.txt` | Proprietary repository notice |

## Contributing

Start with [CONTRIBUTING.md](CONTRIBUTING.md). Do not submit Adobe binaries, modified DLLs, fonts, paid extensions, private paths, or generated installers. Security reports belong in the [private advisory form](https://github.com/sorflow/afterthemed/security/advisories/new).

## Legal

AfterThemed is independent software created by Drerachi. It is not affiliated with, sponsored by, authorized by, certified by, or endorsed by Adobe Inc.

Adobe, Adobe After Effects, and Creative Cloud are trademarks or registered trademarks of Adobe Inc. This repository contains no Adobe software and grants no rights in Adobe products, `dvaui.dll`, fonts, product icons, or third-party extensions. Users are responsible for determining whether their intended modifications are permitted by the terms and laws that apply to them.

Use and redistribution of AfterThemed are governed by [EULA.txt](EULA.txt) and [LICENSE.txt](LICENSE.txt).
