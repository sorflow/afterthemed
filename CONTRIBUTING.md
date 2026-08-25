# Contributing to AfterThemed

AfterThemed is proprietary, source-available software. Focused bug reports, compatibility findings, documentation improvements, and carefully scoped pull requests are welcome.

## Before opening a pull request

1. Search the issue tracker for related work.
2. Open an issue before making a large behavioral or architectural change.
3. Keep the change limited to one problem.
4. Test against disposable files or a clean test installation, never an irreplaceable Adobe installation.

Security problems belong in the private reporting channel described in [SECURITY.md](SECURITY.md).

## Development setup

You need:

- Windows 10 or later;
- the .NET 9 SDK;
- Visual Studio 2022 or a current command-line .NET toolchain; and
- Inno Setup 6 when building the installer.

Build the application:

```powershell
dotnet restore .\src\AfterThemed\DvauiThemeEditor.csproj
dotnet build .\src\AfterThemed\DvauiThemeEditor.csproj --configuration Release
```

Build the self-contained installer:

```powershell
.\src\AfterThemed\Installer\Build-Installer.ps1
```

The build workflow runs the same installer path on a clean Windows runner.

## Project rules

- Preserve the immutable-original, verified-backup, and restore guarantees.
- Treat file-system paths, registry changes, and elevation boundaries as security-sensitive code.
- Keep the x64 Windows ABI and fixed-width DVAUI replacements safe across supported versions.
- Do not commit generated installers, executables, DLLs, backups, analysis dumps, Adobe files, fonts, or third-party extension bundles.
- Do not weaken signature, size, hash, or compatibility checks to make a sample pass.
- Keep interface copy compact and accessible at the application's minimum window size.
- Explain any behavior that cannot be covered by an automated or synthetic smoke test.

## Pull-request checklist

- The project builds without warnings.
- Relevant smoke tests pass.
- No proprietary third-party material is included.
- User-visible behavior and `CHANGELOG.md` are updated when needed.
- Risky file operations have a verified rollback path.
- Screenshots and logs are sanitized.

## Contribution terms

By submitting a contribution, you represent that you have the right to submit it and that it does not contain material copied from Adobe software, a licensed extension, a font, or another restricted source.

You grant Drerachi a perpetual, worldwide, irrevocable, royalty-free license to use, reproduce, modify, distribute, sublicense, and incorporate the contribution into AfterThemed under the project's proprietary terms. You retain ownership of your original contribution.
