# Coding Agent Account Switcher

[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md)

An unofficial, local-first Windows account switcher for Codex and Claude Code.

Coding Agent Account Switcher stores named, encrypted snapshots of the local
authentication files used by Codex and Claude Code. It switches only the
authentication snapshot; your normal settings, MCP configuration, skills,
plugins, and project history remain in their original locations.

> [!IMPORTANT]
> This project is not affiliated with, endorsed by, or sponsored by OpenAI or
> Anthropic. It does not transfer subscriptions, bypass sign-in requirements,
> share accounts, or circumvent provider or organization policies.

The iOS 18-inspired description refers only to general visual direction. Apple
is not affiliated with this project, and no Apple fonts, symbols, artwork, or
trademarks are bundled.

## Features

- Windows-native WPF interface with an iOS 18-inspired glass-card design.
- Built-in interface languages for English, Simplified Chinese, Traditional
  Chinese, Spanish, French, German, Japanese, Korean, Brazilian Portuguese,
  Russian, Arabic, and Hindi.
- In-app settings for the display language and optional current-user startup
  with Windows.
- Named personal and work profiles for both Codex and Claude Code.
- Process guard that blocks a switch until related applications are closed.
- Credentials handled as opaque bytes: no token parsing, email extraction, or
  credential logging.
- Profile snapshots encrypted with Windows DPAPI for the current Windows user.
- Same-directory atomic credential replacement with rollback support.
- Explicit confirmation before a changed live login can overwrite the last
  selected profile snapshot.
- A safe **Restore snapshot** action for the last selected profile, with
  confirmation bound to the exact live authentication bytes being replaced.
- Local-only operation with no analytics or telemetry.
- A rolling `latest` Windows build produced automatically from `main`.

## Supported authentication files

| Provider | Default authentication file switched | Configuration left untouched |
| --- | --- | --- |
| Codex | `%USERPROFILE%\.codex\auth.json` | `%USERPROFILE%\.codex\config.toml`, skills, MCP, sessions, and other state |
| Claude Code | `%USERPROFILE%\.claude\.credentials.json` | `settings.json`, `.claude.json`, plugins, MCP, project settings, and session history |

If `CODEX_HOME` or `CLAUDE_CONFIG_DIR` is set, the application follows that
provider-specific authentication root. It still switches only `auth.json` or
`.credentials.json`; neighboring configuration files remain untouched.

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

1. Sign in to the first account through the provider's official login flow.
2. Fully close Codex/Claude Code and any related local client or extension.
3. Save the current login under a user-chosen label such as `Personal`.
4. Sign in to the second account and save it under another label such as `Work`.
5. Select a saved profile. The app checks for related processes before making
   any change. If one is running, the switch is blocked and no credential file
   is modified.
6. When switching to a different profile, the current authentication file is
   saved back to the last selected encrypted profile after a byte-bound
   confirmation, so refreshed tokens are retained.

The app labels this profile **Last selected**, not "verified current." If the
live file no longer matches its saved snapshot, the switch pauses before any
write. Confirm only when the change is a refresh for the same account. If you
signed in to a different account outside the app, choose **Save as new** (or
explicitly replace the correctly named existing profile) first.

The **Restore snapshot** button on the last selected card checks whether the
live file still matches the saved snapshot. If it differs, the app warns that
the current unsaved login will be replaced and binds approval to those exact
bytes. A different live login cannot reuse an earlier confirmation. During the
restore, a transaction-only DPAPI-encrypted recovery credential preserves the
pre-restore bytes until the operation commits or rolls back.

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

## Security model

- Encrypted profile data is stored below `%LOCALAPPDATA%\CodingAgentAccountSwitcher`.
- Each normalized authentication-file location has a separate hash-scoped
  vault, active state, recovery journal, and operation mutex. Changing
  `CODEX_HOME` or `CLAUDE_CONFIG_DIR` therefore starts an independent profile
  set instead of reusing another location's active account.
- DPAPI `CurrentUser` prevents another Windows account from directly decrypting
  the profile, but it cannot protect against malicious software already running
  as the same Windows user.
- Apart from the provider's ordinary live authentication file, decrypted
  snapshot bytes exist only briefly in memory and in the same-directory atomic
  replacement during capture or switching.
- Authentication replacement temporary and backup files use the recovery
  transaction ID. Normal completion removes both exact files. After an
  interruption, recovery restores a missing live file from an encrypted source
  snapshot or transaction-only recovery credential, then removes all exact
  transaction-owned staging files before deleting the journal.
- The application does not upload credentials and must never include them in a
  log, issue, crash report, test fixture, or repository commit.
- Process detection is defensive and best-effort. A newly started process can
  race with a switch, so users should not launch Codex or Claude Code until the
  operation completes.
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
2. Publish a self-contained Windows x64 build.
3. Create a ZIP archive and SHA-256 checksum.
4. Delete only the previous release and tag named `latest`.
5. Create a new `latest` release for the current commit.

Versioned releases are never deleted by this workflow. GitHub's **immutable
releases** option must remain disabled for the rolling `latest` tag, and branch
or tag rules must allow the workflow to delete `latest`. Repositories that
require immutable releases should change the workflow to unique build tags.

The rolling executable is currently unsigned, so Windows SmartScreen may show a
reputation warning. Review the source and verify the published SHA-256 checksum
before running it.

## Contributing

Contributions are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md). Tests must
use temporary fake credential files and must never access a developer's real
Codex or Claude Code authentication files.

## License

[MIT](LICENSE)
