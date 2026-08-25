# Security policy

AfterThemed writes to application resources and extension files, so a security report deserves a private channel and a reproducible test case.

## Supported versions

| Version | Status |
| --- | --- |
| Latest GitHub release | Supported |
| Older releases | Update before reporting |

Security fixes are made against the latest release. A fix may require users to update before support can be provided.

## Report a vulnerability

Use [GitHub's private vulnerability reporting form](https://github.com/sorflow/afterthemed/security/advisories/new). Do not open a public issue for an unpatched vulnerability.

Include:

- the AfterThemed, Windows, and Adobe After Effects versions involved;
- the affected feature and exact steps needed to reproduce the problem;
- the security impact and the boundary that was crossed;
- sanitized logs, file names, or hashes where they help reproduce the issue; and
- whether the issue also reproduces with a clean After Effects installation.

Do not upload Adobe DLLs, licensed extensions, font files, access tokens, personal paths, or other proprietary material. A minimal synthetic reproduction is preferred.

## Scope

Reports are in scope when they affect AfterThemed itself, including unsafe path handling, unexpected writes, backup or restore integrity, privilege-boundary mistakes, installer behavior, or code execution introduced by generated panel overrides.

Issues in Adobe software, Windows, fonts, or third-party CEP and ScriptUI extensions should be reported to their respective vendors unless AfterThemed is the cause.

## Disclosure

Please allow reasonable time to investigate and publish a corrected release before public disclosure. The project aims to acknowledge a complete report within seven days, but response time is not guaranteed.
