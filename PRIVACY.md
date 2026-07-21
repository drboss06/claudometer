# Privacy

Claude Usage Tray is designed to work without a backend, analytics, advertising, or telemetry.

## Data accessed

The application reads the Claude Code OAuth access token already present on the user's computer. It may read it from:

- the `CLAUDE_CODE_OAUTH_TOKEN` environment variable,
- `%USERPROFILE%\.claude\.credentials.json` or `%USERPROFILE%\.config\claude\.credentials.json`,
- or the default WSL distribution's `~/.claude/.credentials.json`.

## Data transmitted

The access token is sent over HTTPS only to Anthropic's usage endpoint at `api.anthropic.com`. The response contains usage percentages and reset timestamps. No data is sent to the project maintainers or any third party by the application.

If — and only if — you enable **Check for updates on startup**, the app additionally makes an anonymous, credential-free request to the public GitHub Releases API to compare version numbers. This is off by default.

## Data stored

The application does not persist a copy of your credentials or usage responses.

It does write two things locally, both under your control:

- A current-user Windows startup entry when you enable **Run at Windows startup**.
- A small preferences file at `%APPDATA%\ClaudeUsageTray\settings.json` holding non-sensitive options only (refresh interval, alert thresholds, theme, notification and update-check toggles). It never contains your token.

## Token in memory

The access token is held only in process memory for the lifetime of a request and is never written to disk by this app. Note that, like any .NET string, it may remain on the managed heap until garbage collection and could therefore appear in a full process crash dump. The app writes no logs, and error messages are deliberately phrased without token values or response bodies.
