# Claude Usage Tray

An unofficial, open-source Windows system tray monitor for Claude Code subscription limits.

![Claude Usage Tray popup](docs/screenshot.png)

It shows the same usage windows that Claude Code exposes for subscription accounts: the current 5-hour session, the weekly limit, and the model-specific weekly limit when available.

> [!IMPORTANT]
> This project is not affiliated with, endorsed by, or supported by Anthropic. “Claude” and “Claude Code” are trademarks of Anthropic. The usage endpoint is not a documented public API and may change.

## Features

- Native Windows 10/11 tray application; no Electron or browser window.
- Dynamic battery icon based on the most-used quota window.
- Compact dark popup with percentages and reset countdowns.
- Automatic refresh every minute and manual refresh from the tray menu.
- Notifications when usage crosses 80% and 95%.
- Optional launch at Windows sign-in.
- Reads Claude Code OAuth credentials from native Windows or the default WSL distribution.
- Does not collect telemetry, run a server, or store a copy of your token.

## Install

1. Install Claude Code and sign in with a Claude Pro or Max account:

   ```powershell
   claude auth login
   ```

2. Download `ClaudeUsageTray-win-x64.zip` from the repository's Releases page.
3. Extract it and run `ClaudeUsageTray.exe`.
4. If Windows SmartScreen appears, inspect the publisher/repository and choose **Run anyway** only if you trust this build. Community builds are not code-signed yet.

The release is self-contained, so users do not need to install .NET separately.

## Credential lookup

The application checks, in order:

1. `CLAUDE_CODE_OAUTH_TOKEN` in the current process environment.
2. `%USERPROFILE%\.claude\.credentials.json` from native Windows Claude Code.
3. `~/.claude/.credentials.json` inside the default WSL distribution.

Tokens are held only in process memory and sent only to `https://api.anthropic.com/api/oauth/usage`. They are never included in logs, notifications, or local application settings. See [PRIVACY.md](PRIVACY.md).

API-key, Amazon Bedrock, Google Vertex AI, and Anthropic Console billing accounts do not expose Claude subscription windows through this endpoint and are currently out of scope.

## Build from source

Requirements: Windows 10/11 and the .NET 8 SDK or newer.

```powershell
dotnet build ClaudeUsageTray.sln --configuration Release
dotnet run --project src/ClaudeUsageTray -- --demo
```

Run the built-in offline parser checks:

```powershell
dotnet run --project src/ClaudeUsageTray -- --self-test
```

Create a self-contained executable:

```powershell
dotnet publish src/ClaudeUsageTray/ClaudeUsageTray.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output publish/win-x64
```

## Release workflow

Push a tag such as `v0.1.0`. The GitHub Actions release workflow builds a self-contained x64 package, creates a zip archive, and publishes a GitHub Release.

Before the first public release:

- Enable GitHub Actions for the repository.
- Review the generated binary with your preferred antivirus service.
- Consider Authenticode code signing to avoid SmartScreen warnings.

## Русский

Это небольшое нативное приложение для области уведомлений Windows. Оно показывает расход 5-часового и недельного лимитов Claude Code, время до сброса, обновляется раз в минуту и может запускаться вместе с Windows.

Для работы сначала войдите в Claude Code командой `claude auth login`, затем запустите готовый `.exe` из Releases. Приложение не копирует токен и не отправляет его никуда, кроме официального API Anthropic.

## Contributing

Bug reports and pull requests are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) and [SECURITY.md](SECURITY.md) first.

## License

[MIT](LICENSE)
