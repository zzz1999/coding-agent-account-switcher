# Contributing

Thank you for contributing.

## Development setup

```powershell
dotnet restore CodingAgentAccountSwitcher.sln
dotnet test CodingAgentAccountSwitcher.sln --configuration Release
dotnet run --project .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj
```

The versioned Release workflow compiles
`installer/CodingAgentAccountSwitcher.iss` with the Inno Setup compiler bundled
on GitHub-hosted Windows runners. To build the installer locally, first publish
the self-contained application to `artifacts/publish`, copy `LICENSE` into that
directory, and then run `ISCC.exe installer\CodingAgentAccountSwitcher.iss`.
Each push to `main` uses the workflow run number as the next `1.0.N` patch
version. The workflow's single `APP_VERSION` value must version the executable,
the `v1.0.N` Release tag, and the Release notes. Preserve the exact
machine-readable marker contract
`<!-- coding-agent-account-switcher-version: 1.0.N -->` when changing release
packaging or update discovery.

## Pull request expectations

- Keep provider-specific paths, managed-field whitelists, merge behavior, and
  process rules inside provider adapters.
- Add tests for every account/API snapshot, selective merge, transaction, or
  recovery change. Tests must prove unrelated configuration survives unchanged.
- Saving a profile must remain a provider-file read plus an encrypted vault
  write and must not inspect or terminate provider processes. Switching and
  recovery must retain their fail-closed process checks.
- Profile-management tests must prove rename changes only local metadata;
  delete removes only the selected encrypted snapshot; deleting the active
  profile clears only its local association and leaves live provider files
  unchanged; and pending transaction recovery blocks rename and delete.
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
- Keep update discovery explicitly user initiated: one anonymous HTTPS `GET` to
  the official GitHub API, no startup/background/periodic checks, and no
  automatic download or execution of Release assets. Tests must verify these
  network and execution boundaries.
- Preserve keyboard navigation, high-contrast behavior, and a minimum 44-pixel
  interaction target in UI changes.
- Keep the executable Per-Monitor V2 DPI aware. The Windows 10 layered-window
  fallback must keep its main shell opaque and use transparency only for the
  antialiased outer corners. Keep the outer outline thin at fractional scaling;
  do not reintroduce shadow margins, whole-window effects, scale transforms, or
  bitmap-cached interface layers.
- Do not include Apple fonts, SF Symbols, Apple artwork, or provider logos.
- Run `dotnet test` and `git diff --check` before submitting.
- When release packaging changes, verify that the installer EXE, portable EXE,
  and both SHA-256 sidecars are attached to the new versioned Release; its notes
  contain the exact marker produced from the same `APP_VERSION` used by
  `dotnet publish`; and older Release entries retain no uploaded assets.

## Commit scope

Prefer focused commits. Security-sensitive changes should explain the threat
being addressed, failure behavior, and the tests proving rollback or no-write
behavior.
