# Security policy

## Reporting a vulnerability

Please do not open a public issue for a suspected vulnerability involving credential exposure. Use GitHub's private vulnerability reporting feature for this repository. If that feature is unavailable, contact the repository owner privately.

Include the affected version, a minimal reproduction, and the expected impact. Never include a real Claude credential in a report.

## Security boundaries

- Credentials must never be logged, copied to application settings, or included in exception messages.
- Network requests containing credentials must only target HTTPS endpoints under `api.anthropic.com`.
- Pull requests adding analytics, telemetry, auto-update executables, or third-party network services require explicit maintainer review and documentation.
- Official release binaries are built by the workflow in `.github/workflows/release.yml` from a tagged commit.
