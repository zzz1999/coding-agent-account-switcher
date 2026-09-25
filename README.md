# Coding Agent Account Switcher

<p align="center">
  <img src="src/CodingAgentAccountSwitcher.App/Assets/CodingAgentAccountSwitcher.png" width="144" alt="Coding Agent Account Switcher icon">
</p>

[English](README.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md)

A simple Windows app for switching between saved Codex, Claude Code, and
OpenCode accounts or API-site setups.

It changes only the account information and supported API settings saved with a
profile. Your MCP servers, skills, plugins, project settings, and history stay
where they are.

> [!IMPORTANT]
> This is an unofficial community project. It is not affiliated with OpenAI,
> Anthropic, Apple, or the OpenCode project. It does not transfer
> subscriptions, bypass sign-in requirements, or override organization policy.

## Download

Get the current version from the
[latest release](https://github.com/zzz1999/coding-agent-account-switcher/releases/latest):

- **Installer:** <code>CAAS-vX.Y.Z-Setup-x64.exe</code>
- **Portable app:** <code>CAAS-vX.Y.Z-Portable-x64.exe</code>
- **Checksums:** a matching SHA-256 file is provided for each executable

The installer is per-user, does not require administrator privileges, and does
not enable **Start with Windows** unless you choose it. The current builds are
unsigned, so Windows SmartScreen may show a warning.

## What you can do

- Save personal, work, and API-site profiles with clear names.
- Switch Codex, Claude Code, and OpenCode account information in a few clicks.
- Keep supported API endpoints, keys, and provider routing with the correct
  profile.
- Double-click a profile name to rename it, or delete a saved local snapshot.
- See a clear confirmation showing which profile is active after a switch.
- Block switching while related apps are still running.
- Use English, Spanish, French, German, Japanese, Korean, Portuguese,
  Russian, Arabic, or Hindi.
- Keep your light/dark appearance and language choice between launches.
- In the same Windows sign-in session, opening the app again restores and brings
  forward the existing window instead of creating a duplicate.
- Optionally start the app with Windows.
- Check for updates manually from Settings.

Everything is local. The app has no analytics or telemetry.

## Supported apps

| App | What switches | What stays unchanged |
| --- | --- | --- |
| Codex | Login and selected API provider connection settings | Models, review/reasoning options, features, MCP, skills, sessions, history, and other settings |
| Claude Code | Login and supported API endpoint/key settings | Plugins, MCP, projects, history, and unrelated settings |
| OpenCode | Saved login and supported provider/model settings | Project configuration and unrelated settings |

Only account-related fields supported by this project are switched. See
[Architecture](docs/ARCHITECTURE.md) for the exact file and field list.

## Quick start

1. Sign in normally, or configure the API site you want to use.
2. Open Coding Agent Account Switcher and choose the matching app.
3. Select **Save current account** and give it a name such as <code>Personal</code>.
4. Sign in to your second account or configure another API site.
5. Save it under another name such as <code>Work</code>.
6. Fully close the related app, then select a saved profile whenever you want to switch.

You can save an account while its app is open. Before switching, the app checks
for running processes. If something is still open, it asks you to close it first
and makes no change.

If the current account has changed since it was saved, the app asks for
confirmation before replacing anything. If it is actually a different account,
save it as a new profile first.

## Profiles and settings

- **Rename:** double-click a profile name.
- **Delete:** use the trash button. This removes only the app's encrypted local
  snapshot; it does not delete the provider account or sign you out.
- **Last selected:** identifies the profile most recently activated by this app.
- **Damaged snapshot:** a missing or unreadable snapshot is hidden from the
  switchable list while healthy profiles remain available.
- **Theme and language:** both are remembered for the current Windows user.
- **Start with Windows:** optional, current-user only, and does not require
  administrator privileges.
- **Check for updates:** runs only when you click it. The app never downloads or
  installs an update automatically.

## Privacy and security

- Saved profiles are encrypted with Windows DPAPI for the current Windows user.
- Credentials and API keys are never displayed or written to application logs.
- Profiles stay under <code>%LOCALAPPDATA%\CodingAgentAccountSwitcher</code>.
- Switching uses guarded file replacement and recovery to reduce the risk of a
  partial change.
- The app does not upload credentials, profile names, device identifiers, or
  telemetry.
- Work accounts may be subject to organization policy. Obtain approval before
  keeping an additional local login snapshot.

Read [SECURITY.md](SECURITY.md) before reporting a security issue.

## Limitations

- Provider-side logout, token expiry, SSO, MFA, or organization policy can still
  require a normal sign-in.
- Existing Codex conversations stay bound to the provider used when they were
  created. After switching providers, start a new conversation, or switch back
  to the original provider to continue it.
- The app cannot move a subscription between accounts or make an account
  permanently signed in.
- Only Windows 10 and Windows 11 x64 are currently supported.

## Build from source

Requires Windows and .NET SDK 8.0.400 or a newer .NET 8 feature band.

~~~powershell
dotnet restore CodingAgentAccountSwitcher.sln
dotnet test CodingAgentAccountSwitcher.sln --configuration Release
dotnet run --project .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj
~~~

Every push to <code>main</code> publishes a new versioned Release through GitHub
Actions. Only the newest Release keeps the installer and portable downloads.

## Contributing

Contributions are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md).
Never include real credentials or API keys in issues, logs, tests, or commits.

## Third-party notices

This app includes [Tomlyn](https://github.com/xoofx/Tomlyn), which is licensed
under the BSD 2-Clause License.

The installer includes [Inno Setup](https://jrsoftware.org/) translations, used
under the [Inno Setup License](installer/Languages/LICENSE.txt).
See [translation credits](installer/Languages/SOURCES.md) and
[full third-party notices](THIRD-PARTY-NOTICES.md).

<details>
<summary>Tomlyn BSD 2-Clause License notice</summary>

Copyright (c) 2019-2026, Alexandre Mutel
All rights reserved.

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

</details>

## License

[MIT](LICENSE)
