# Coding Agent Account Switcher

[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md)

An unofficial, local-first Windows account and API-site switcher for Codex,
Claude Code, and OpenCode.

Coding Agent Account Switcher stores named, encrypted snapshots of local
credentials together with only the API-provider fields needed to reconnect the
same profile. Unrelated settings, MCP configuration, skills, plugins, and
project history remain in their original locations.

> [!IMPORTANT]
> This project is not affiliated with, endorsed by, or sponsored by OpenAI or
> Anthropic. It does not transfer subscriptions, bypass sign-in requirements,
> share accounts, or circumvent provider or organization policies.

The iOS 18-inspired description refers only to general visual direction. Apple
is not affiliated with this project, and no Apple fonts, symbols, artwork, or
trademarks are bundled.

## Download

Download the current build from the [latest release](https://github.com/zzz1999/coding-agent-account-switcher/releases/latest):

- **Recommended installer:** `coding-agent-account-switcher-setup-win-x64.exe`
  installs for the current Windows user without administrator privileges and
  creates Start menu and uninstall entries.
- **Portable executable:** download
  `coding-agent-account-switcher-portable-win-x64.exe` and run it directly;
  no installation is required.
- Each executable has a matching SHA-256 checksum file in the release.

The installer does not enable **Start with Windows** automatically and does not
touch Codex, Claude Code, or OpenCode authentication or configuration files. Uninstalling
the app preserves encrypted account snapshots and application settings so they
remain available after reinstalling. The installer and portable executable are
currently unsigned, so Windows SmartScreen may show a reputation warning.

## Features

- Windows-native WPF interface with an iOS 18-inspired glass-card design.
- Built-in interface languages for English, Simplified Chinese, Traditional
  Chinese, Spanish, French, German, Japanese, Korean, Brazilian Portuguese,
  Russian, Arabic, and Hindi.
- In-app settings for the display language and optional current-user startup
  with Windows.
- Named personal, work, and API-site profiles for Codex, Claude Code, and
  OpenCode.
- Process guard that blocks a switch until related applications are closed.
- Authentication files remain opaque bytes. Only the documented managed
  configuration fields listed below are parsed and merged; secrets are never
  displayed or logged.
- Profile snapshots encrypted with Windows DPAPI for the current Windows user.
- Same-directory atomic managed-file replacement with rollback support.
- Explicit confirmation before a changed live login can overwrite the last
  selected profile snapshot.
- A safe **Restore snapshot** action for the last selected profile, with
  confirmation bound to the exact live managed snapshot bytes being replaced.
- Local-only operation with no analytics or telemetry.
- A rolling `latest` Windows build produced automatically from `main`.

## Supported account and API configuration

| Provider | Managed files | Selectively managed configuration |
| --- | --- | --- |
| Codex | `%USERPROFILE%\.codex\auth.json` and `config.toml` | `model_provider`, `openai_base_url`, `model`, `review_model`, `model_reasoning_effort`, `disable_response_storage`, the selected active `model_providers` table, and `features.responses_websockets_v2` |
| Claude Code | `%USERPROFILE%\.claude\.credentials.json` and `settings.json` | Only `env.ANTHROPIC_BASE_URL`, `env.ANTHROPIC_API_KEY`, `env.ANTHROPIC_AUTH_TOKEN`, `env.CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC`, and the compatibility field `env.CLAUDE_CODE_ATTRIBUTION_HEADER` |
| OpenCode | Optional `<data-root>\opencode\auth.json`; global `<config-root>\opencode\config.json`, `opencode.json`, and `opencode.jsonc`; then `OPENCODE_CONFIG` when set | The opaque `/connect` credential file plus the effective top-level `provider`, `model`, and `small_model` API profile |

The application merges those fields into the live configuration instead of
replacing the whole file. For Codex, `network_access`,
`windows_wsl_setup_acknowledged`, `features.goals`,
`cli_auth_credentials_store`, MCP configuration, skills, sessions, and all
other unrelated semantic values remain unchanged. For Claude Code, every other
`env` entry plus `.claude.json`, plugins, MCP, project settings, and session
history remain unchanged. For OpenCode, unrelated semantic values in every
participating global or custom configuration file remain unchanged.

OpenCode's default configuration root is `%USERPROFILE%\.config` and its
default data root is `%USERPROFILE%\.local\share`. An absolute
`XDG_CONFIG_HOME` or `XDG_DATA_HOME` replaces the corresponding default root;
blank values do not. Every nonblank OpenCode path override
(`XDG_CONFIG_HOME`, `XDG_DATA_HOME`, `OPENCODE_CONFIG`, and
`OPENCODE_CONFIG_DIR`) must be absolute; a relative value blocks capture and
switch before any file is written. Global configuration is captured in load
order from `<config-root>\opencode\config.json`, then `opencode.json`, then
`opencode.jsonc`; later layers override earlier ones. `OPENCODE_CONFIG`, when
set, is loaded afterward and has the highest managed-file precedence. The app
captures the resulting effective `provider`, `model`, and `small_model` values
as one API profile. `/connect` authentication is read from
`<data-root>\opencode\auth.json`.

OpenCode supports both credential styles: credentials created by `/connect` in
its separate `auth.json`, and API keys embedded under the managed `provider`
object. A snapshot includes whichever style is present, or both, while leaving
unrelated OpenCode configuration keys alone.

When applying an OpenCode profile, the app clears `provider`, `model`, and
`small_model` from the other global layers so a stale lower-layer endpoint
cannot override the target. With `OPENCODE_CONFIG`, target managed values are
written there and those fields are cleared from all three global layers.
Without it, non-empty target values are canonicalized into global
`opencode.jsonc` while being cleared from `config.json` and `opencode.json`,
even if only a JSON or legacy file existed before. An authentication-only or
empty-managed snapshot is valid: it clears existing managed values but does not
create a new empty `opencode.jsonc`.

Selective merging preserves unrelated semantic values. It does not promise
byte-for-byte formatting or comment preservation when a JSON or TOML file must
be reserialized.

Before capturing or switching OpenCode, the app performs read-only checks of
known higher-precedence environment overrides. A nonblank
`OPENCODE_AUTH_CONTENT` blocks the operation. `OPENCODE_CONFIG_CONTENT` is
allowed only when it is valid JSON or JSONC and contains none of the managed
top-level keys `provider`, `model`, or `small_model`; invalid content or any
managed key blocks the operation. When `OPENCODE_CONFIG_DIR` is set, its
`opencode.json` and `opencode.jsonc` are checked; unreadable or invalid files,
or either file containing a managed key, block the operation. Directory or
inline configuration containing only unrelated keys is allowed. The app does
not modify any of these environment-provided sources.

If `CODEX_HOME` or `CLAUDE_CONFIG_DIR` is set, the application follows that
provider-specific root. OpenCode project-level configuration, centrally
managed sources, and provider-specific environment variables remain unmanaged
and may still override the selected global profile after a switch. The app
does not search for or modify them. The OpenCode process guard checks both
`opencode` and `opencode-cli`.

`disable_response_storage`, `features.responses_websockets_v2`, and
`CLAUDE_CODE_ATTRIBUTION_HEADER` are retained as compatibility fields for API
sites that still use them; their presence here is not a claim that every
current provider release documents them.

Codex must use file-backed credential storage. Add the following setting to
`config.toml` in the active Codex root (`%USERPROFILE%\.codex` by default, or
`CODEX_HOME` when set) if your installation uses the operating system
credential store:

```toml
cli_auth_credentials_store = "file"
```

See the official [Codex authentication documentation](https://developers.openai.com/codex/auth)
and [Claude Code authentication documentation](https://code.claude.com/docs/en/authentication)
for the current storage contracts.

## How it works

1. Sign in through the provider's official login flow, or configure a supported
   API site with placeholder-safe values in the provider's normal files.
2. Fully close Codex, Claude Code, OpenCode, and related local clients or
   extensions.
3. Save the current account and managed API settings under a label such as
   `Personal`.
4. Sign in to the second account or configure another API site, then save it
   under another label such as `Work`.
5. Select a saved profile. The app checks for related processes before making
   any change. If one is running, the switch is blocked and no managed file is
   modified.
6. When switching to a different profile, the current managed snapshot is
   saved back to the last selected encrypted profile after a content-bound
   confirmation, so refreshed tokens and intentional API changes are retained.
   Before the journal becomes durable, that exact confirmed pre-switch snapshot
   is also preserved in a transaction-only DPAPI-encrypted recovery blob.

Raw credential-only Codex and Claude Code profiles created by older releases
remain readable. They are interpreted as credentials plus an empty managed API
configuration. Activating one therefore clears the currently managed API-route
and model fields so its credentials cannot silently reuse a third-party
endpoint. After activation, configure the desired model/API settings and
recapture the profile. When switching away from an active legacy source, the
application upgrades it to the composite format.

The app labels this profile **Last selected**, not "verified current." If the
live managed snapshot no longer matches its saved snapshot, the switch pauses
before any write. Confirm only when the change is a refresh for the same account. If you
signed in to a different account outside the app, choose **Save as new** (or
explicitly replace the correctly named existing profile) first.

Profile rename and delete actions operate only on the local encrypted snapshot
vault. Renaming changes the saved label and metadata without changing snapshot
contents. Deleting removes only the selected encrypted snapshot; deleting the
last-selected profile also clears the app's active-profile association, but it
does not sign out or modify any live provider authentication or configuration
file. Save the current account before switching again. Both actions are refused
while an interrupted switch transaction is pending recovery.

The **Restore snapshot** button on the last selected card checks whether the
live managed snapshot still matches the saved snapshot. If it differs, the app
warns that the current unsaved state will be replaced and binds approval to
those exact managed snapshot bytes. A different live managed snapshot cannot
reuse an earlier confirmation. During the restore, a transaction-only
DPAPI-encrypted recovery blob preserves the pre-restore managed snapshot until
the operation commits or rolls back.

Every composite or multi-file profile-switch transaction writes the exact
current managed snapshot to the encrypted recovery blob before the journal,
even when it matches the saved source. This guarantees exact rollback for a
partial multi-file write and allows a legacy credential-only source profile to
be upgraded safely. After an interruption, recovery prefers that exact
pre-switch snapshot and synchronizes the source profile if it restores it.
Older journals without a recovery blob remain compatible by falling back to the
saved source snapshot. If the live managed snapshot matches neither the
preserved source nor the target, the journal and encrypted recovery blob remain
available for manual recovery.

Multi-file commits are fail-closed. When a provider has a separate live
authentication file, the transaction removes that file first, applies the
selective configuration merge atomically, and installs the target
authentication file last. An interruption can temporarily leave authentication
absent, but cannot pair one profile's credentials with the opposite API
endpoint. Recovery uses the encrypted pre-switch snapshot to finish or roll
back safely.

The application never promises permanent login. Provider-side revocation,
organization policy, SSO, MFA, or token expiry can still require a normal
sign-in through the official client.

## Settings

Open **Settings** from the application window to choose a display language or
control whether the app starts with Windows. The selected language is stored
locally for the current Windows user and can be changed again at any time.

**Start with Windows** adds an entry for this application under
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. It applies only to the
current Windows user and does not require administrator privileges. Turning the
option off removes only the startup entry owned by Coding Agent Account
Switcher; it does not alter other startup applications.

**Check for updates** is entirely user initiated. Only after the user clicks it,
the app sends one anonymous HTTPS `GET` request to the official GitHub API for
this repository. There are no startup, background, or periodic update checks,
and the request does not upload credentials, settings, profile labels, machine
identifiers, or application telemetry. The app only compares release metadata;
it never automatically downloads or executes an installer or portable build.

## Security model

- Encrypted profile data is stored below `%LOCALAPPDATA%\CodingAgentAccountSwitcher`.
- Each normalized managed-file location set has a separate hash-scoped
  vault, active state, recovery journal, and operation mutex. Changing
  `CODEX_HOME`, `CLAUDE_CONFIG_DIR`, `XDG_CONFIG_HOME`, `XDG_DATA_HOME`, or
  `OPENCODE_CONFIG` therefore starts an independent profile set instead of
  reusing another location's active account.
- DPAPI `CurrentUser` prevents another Windows account from directly decrypting
  the profile, but it cannot protect against malicious software already running
  as the same Windows user.
- Apart from the provider's ordinary live files, decrypted snapshot bytes exist
  only briefly in memory and in same-directory atomic replacements during
  capture or switching.
- Authentication replacement temporary and backup files use the recovery
  transaction ID. Normal completion removes both exact files. After an
  interruption, recovery restores a missing live file from an encrypted source
  snapshot or transaction-only recovery credential, then removes all exact
  transaction-owned staging files before deleting the journal. The encrypted
  recovery blob is deleted only after the journal, on a best-effort basis.
- The application does not upload credentials and must never include them in a
  log, issue, crash report, test fixture, or repository commit.
- Profile rename and delete actions change only the local encrypted vault.
  Deleting the last-selected profile clears only its local association and does
  not modify the live provider files. A pending recovery transaction blocks both
  actions.
- Update discovery is a single anonymous request to the official GitHub API
  after an explicit user click. It never runs in the background and never
  automatically downloads or executes release assets.
- Process detection is defensive and best-effort. A newly started process can
  race with a switch, so users should not launch Codex, Claude Code, or OpenCode
  until the operation completes.
- If a work account is managed by an organization, obtain approval before
  retaining an additional encrypted local authentication snapshot.

Read [SECURITY.md](SECURITY.md) and [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
before changing credential-handling code.

## Build from source

Requirements:

- Windows 10 or Windows 11
- .NET SDK 8.0.400 or a newer .NET 8 feature band

```powershell
dotnet restore CodingAgentAccountSwitcher.sln
dotnet test CodingAgentAccountSwitcher.sln --configuration Release
dotnet run --project .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj
```

Create a self-contained Windows x64 build:

```powershell
dotnet publish .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output .\artifacts\publish
```

## Rolling latest release

`.github/workflows/latest-release.yml` runs for every push to `main`:

1. Restore and test the solution.
2. Publish a self-contained portable Windows x64 build.
3. Build the per-user Windows x64 installer.
4. Prepare the portable executable and SHA-256 checksums for both executables.
5. Delete only the previous release and tag named `latest`.
6. Publish a new `latest` release with the installer, portable executable, and
   checksums for the current commit. One workflow `APP_VERSION` value versions
   the executable and writes this exact machine-readable Release-note marker:
   `<!-- coding-agent-account-switcher-version: 0.1.N -->`.

The `latest` tag is intentionally rolling, so the application reads that marker
from the official GitHub Release API response when the user explicitly checks
for updates. It does not check in the background or automatically download or
execute any asset.

Versioned releases are never deleted by this workflow. GitHub's **immutable
releases** option must remain disabled for the rolling `latest` tag, and branch
or tag rules must allow the workflow to delete `latest`. Repositories that
require immutable releases should change the workflow to unique build tags.

The rolling installer and portable executable are currently unsigned, so
Windows SmartScreen may show a reputation warning. Review the source and verify
the appropriate published SHA-256 checksum before running either executable.

## Contributing

Contributions are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md). Tests must
use temporary synthetic credential and configuration files and must never
access a developer's real Codex, Claude Code, or OpenCode files.

## License

[MIT](LICENSE)
