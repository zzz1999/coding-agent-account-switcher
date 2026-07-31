# Contributing

Thank you for contributing.

## Development setup

```powershell
dotnet restore CodingAgentAccountSwitcher.sln
dotnet test CodingAgentAccountSwitcher.sln --configuration Release
dotnet run --project .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj
```

## Pull request expectations

- Keep provider-specific paths and process rules inside provider adapters.
- Add tests for every credential transaction or recovery change.
- Use random synthetic bytes and temporary directories in tests. Never read a
  real `%USERPROFILE%\.codex\auth.json` or `.claude\.credentials.json`.
- Do not add telemetry, crash upload, token decoding, email extraction, or
  automatic process termination.
- Preserve keyboard navigation, high-contrast behavior, and a minimum 44-pixel
  interaction target in UI changes.
- Do not include Apple fonts, SF Symbols, Apple artwork, or provider logos.
- Run `dotnet test` and `git diff --check` before submitting.

## Commit scope

Prefer focused commits. Security-sensitive changes should explain the threat
being addressed, failure behavior, and the tests proving rollback or no-write
behavior.
