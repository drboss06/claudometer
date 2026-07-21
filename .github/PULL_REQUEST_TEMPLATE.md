<!-- Thanks for contributing! Please keep the change focused. -->

## Summary

<!-- What does this change do, and why? -->

## Security implications

<!-- Required. Does this change touch credential handling, network requests, the
     update check, or persisted settings? If not, write "None". -->

## Checklist

- [ ] `dotnet test ClaudeUsageTray.sln --configuration Release` passes.
- [ ] `dotnet run --project src/ClaudeUsageTray -- --self-test` passes.
- [ ] No credentials, real usage responses, or `bin`/`obj` output are committed.
- [ ] User-facing strings are added to `Resources/Strings.cs` (EN + RU).
- [ ] `CHANGELOG.md` is updated under **Unreleased** if behavior changed.
