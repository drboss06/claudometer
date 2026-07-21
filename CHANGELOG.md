# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres
to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Light/dark theme that follows the Windows setting, plus a manual override.
- Tray-menu settings: refresh interval, notification toggle and thresholds, theme,
  and an optional startup update check. Preferences persist in `%APPDATA%`.
- Both weekly model windows (Sonnet and Opus) are shown when the API reports them.
- Sign-in expiry warning in the popup and a distinct "session expired" error state
  with a sign-in call to action.
- Trend sparkline per usage window.
- Russian localization (selected from the Windows display language).
- xUnit test project and expanded offline self-test.
- Release checksums (`.sha256`) and a `win-arm64` build.

### Changed
- Tray icon adapts its outline to light or dark taskbars.
- Usage colour bands now share the notification thresholds.
- Version is single-sourced from the assembly and injected from the release tag.

### Fixed
- Disposed orphaned metric controls that leaked handles on every refresh.
- WSL credential read no longer uses a login shell, drains stderr, and times out
  instead of hanging the refresh loop.
- Tray-icon click reliably toggles the popup instead of reopening it.
- Background refreshes no longer flicker an open popup.
- Unknown exceptions no longer surface raw text (and never a token) to the UI.

## [0.1.0]

### Added
- Initial tray application: 5-hour, weekly, and model usage windows with a battery
  icon, dark popup, minute refresh, low-usage notifications, and launch at startup.
