# Contributing

Thank you for helping improve Claude Usage Tray.

1. Open an issue describing the bug or proposed behavior.
2. Fork the repository and create a focused branch.
3. Build with `dotnet build ClaudeUsageTray.sln --configuration Release`.
4. Run the offline checks with `dotnet run --project src/ClaudeUsageTray -- --self-test`.
5. Use `--demo` when working on the interface without Claude credentials.
6. Open a pull request that explains the behavior change and its security implications, if any.

Do not commit credentials, usage responses copied from a real account, generated `bin`/`obj` directories, or unrelated formatting changes.
