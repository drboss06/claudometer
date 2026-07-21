# Privacy

Claude Usage Tray is designed to work without a backend, analytics, advertising, or telemetry.

## Data accessed

The application reads the Claude Code OAuth access token already present on the user's computer. It may read it from the `CLAUDE_CODE_OAUTH_TOKEN` environment variable, the native Windows Claude Code credential file, or the default WSL distribution's Claude Code credential file.

## Data transmitted

The access token is sent over HTTPS only to Anthropic's usage endpoint at `api.anthropic.com`. The response contains usage percentages and reset timestamps. No data is sent to the project maintainers or any third party by the application.

## Data stored

The application does not persist a copy of credentials or usage responses. The only optional setting written by the application is a current-user Windows startup entry when the user enables **Run at Windows startup**.

## Logs

The application does not write log files. Error messages are deliberately phrased without token values or response bodies.
