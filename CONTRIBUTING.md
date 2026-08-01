# Contributing

Thank you for contributing.

## Development setup

```powershell
dotnet restore CodingAgentAccountSwitcher.sln
dotnet test CodingAgentAccountSwitcher.sln --configuration Release
dotnet run --project .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj
```

The rolling Release workflow compiles
`installer/CodingAgentAccountSwitcher.iss` with the Inno Setup compiler bundled
on GitHub-hosted Windows runners. To build the installer locally, first publish
the self-contained application to `artifacts/publish`, copy `LICENSE` into that
directory, and then run `ISCC.exe installer\CodingAgentAccountSwitcher.iss`.

## Pull request expectations

- Keep provider-specific paths and process rules inside provider adapters.
- Add tests for every credential transaction or recovery change.
- Use random synthetic bytes and temporary directories in tests. Never read a
  real `%USERPROFILE%\.codex\auth.json` or `.claude\.credentials.json`.
- Add every new fixed interface string to all supported localization catalogs.
  Do not embed new user-facing prose directly in XAML or code-behind.
- Keep translated README files structurally aligned with `README.md`, including
  security limitations and the unofficial-project disclaimer.
- Do not add telemetry, crash upload, token decoding, email extraction, or
  automatic process termination.
- Preserve keyboard navigation, high-contrast behavior, and a minimum 44-pixel
  interaction target in UI changes.
- Do not include Apple fonts, SF Symbols, Apple artwork, or provider logos.
- Run `dotnet test` and `git diff --check` before submitting.
- When release packaging changes, verify that the installer EXE, portable EXE,
  and both SHA-256 sidecars are all attached to the rolling `latest` Release.

## Commit scope

Prefer focused commits. Security-sensitive changes should explain the threat
being addressed, failure behavior, and the tests proving rollback or no-write
behavior.
