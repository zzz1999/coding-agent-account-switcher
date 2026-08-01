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

- Keep provider-specific paths, managed-field whitelists, merge behavior, and
  process rules inside provider adapters.
- Add tests for every account/API snapshot, selective merge, transaction, or
  recovery change. Tests must prove unrelated configuration survives unchanged.
- OpenCode adapter tests must cover `config.json` -> `opencode.json` ->
  `opencode.jsonc` -> `OPENCODE_CONFIG` precedence, stale managed-key removal
  from non-target layers, canonical writes to the custom path or global
  `opencode.jsonc`, and the authentication-only no-empty-file case. They must
  also cover absolute and relative `XDG_CONFIG_HOME` / `XDG_DATA_HOME` values;
  fail-closed `OPENCODE_AUTH_CONTENT`; invalid, managed-key, and unrelated-only
  `OPENCODE_CONFIG_CONTENT`; and invalid, managed-key, and unrelated-only
  `OPENCODE_CONFIG_DIR\opencode.json` / `opencode.jsonc` cases.
- Use random synthetic bytes, obvious placeholder API tokens, and temporary
  directories in tests. Never read a real `%USERPROFILE%\.codex\auth.json`,
  `.codex\config.toml`, `.claude\.credentials.json`, `.claude\settings.json`, or
  OpenCode configuration file.
- Add every new fixed interface string to all supported localization catalogs.
  Do not embed new user-facing prose directly in XAML or code-behind.
- Keep translated README files structurally aligned with `README.md`, including
  security limitations and the unofficial-project disclaimer.
- Do not add telemetry, crash upload, authentication-file decoding, email
  extraction, secret logging, or automatic process termination. Parsing is
  limited to the documented selective configuration whitelist.
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
