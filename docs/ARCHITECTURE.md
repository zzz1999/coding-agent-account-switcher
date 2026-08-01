# Architecture

Coding Agent Account Switcher is split into a provider-independent transaction
core and a WPF presentation layer.

## Components

- **Provider adapter**: identifies the provider's managed file set, reads a
  canonical account-and-API snapshot, selectively merges managed configuration
  fields, and declares the process names that make switching unsafe.
- **Process guard**: returns `Clear`, `Running`, or `Unknown`. Only `Clear`
  permits a credential operation. It never terminates a process.
- **Profile vault**: stores non-secret metadata plus a DPAPI-encrypted composite
  snapshot below the current user's local application data directory. The
  snapshot contains opaque authentication bytes and only the provider-specific
  configuration fields in the whitelist below.
- **Profile management**: renames only a snapshot's local label and metadata;
  deletes only the selected local encrypted snapshot. Deleting the last-selected
  profile also clears the local active-profile association, while all live
  provider authentication and configuration files remain untouched. A pending
  recovery transaction blocks both operations.
- **Managed-location scope**: hashes the normalized live managed-file set. Every
  location set receives an independent vault, active state, journal, and mutex
  without persisting user-profile paths in metadata.
- **Switch transaction**: serializes operations per provider, captures the
  active profile, stages target files in their destination directories, and
  commits multi-file state fail-closed: remove separate authentication first,
  apply configuration atomically, and install target authentication last.
- **Recovery journal**: records non-secret transaction phases so an interrupted
  operation can be recovered or blocked safely on the next startup. Its
  transaction ID identifies the exact plaintext replacement temporary and
  backup files owned by that operation.
- **Transaction recovery snapshot**: every composite or multi-file transaction
  stores the exact canonical pre-switch managed snapshot in a transaction-only
  DPAPI-encrypted blob before its journal, even when it matches the saved
  source. This supports exact partial-write rollback and legacy-source upgrade.
- **WPF application**: presents provider tabs, account cards, capture and switch
  actions, process-blocking dialogs, and status messages.
- **Localization catalog**: supplies every fixed interface string for the
  supported cultures and can refresh the active window without restarting the
  application. Arabic also switches the application shell to right-to-left
  flow.
- **Application preferences**: stores only the selected UI language, light/dark
  appearance, and other non-secret application choices below
  `%LOCALAPPDATA%\CodingAgentAccountSwitcher`.
  Preferences are separate from every path-scoped authentication vault.
- **Startup registration**: manages one application-owned value in the current
  user's `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` key. It never
  writes a machine-wide startup entry and does not require elevation.
- **Update discovery**: after an explicit user click, sends one anonymous HTTPS
  `GET` to the official GitHub Release API and compares the machine-readable
  version marker in the rolling Release notes. It has no startup, background,
  or periodic scheduler and does not download or execute Release assets.
- **Windows installer**: installs the self-contained x64 application below the
  current user's local application data, adds a Start Menu shortcut and an
  HKCU uninstall entry, and never requests elevation. Installation does not
  enable startup or read, write, or migrate application data.

## Invariants

1. Provider authentication contents are opaque bytes. Managed configuration
   values are treated as credentials and are never displayed or logged.
2. Only the explicit provider whitelist is captured and merged. Every unrelated
   configuration value remains semantically unchanged. Byte-for-byte formatting
   and comment preservation are not invariants when a file is reserialized.
3. A detected or unknown process state causes zero credential writes.
4. A target snapshot is decrypted and validated before the active snapshot is
   changed.
5. Temporary plaintext staging and backup files exist only beside their live
   destination files, have transaction-derived names, and are removed after
   replacement or rollback.
6. Logs and journal files contain no credentials, email addresses, plaintext
   paths containing a Windows user name, or credential hashes.
7. A work profile and personal profile may not share an encrypted blob.
8. A live managed snapshot that differs from the last selected snapshot
   requires explicit confirmation bound to that exact canonical content before
   the source profile is updated or the saved snapshot is restored.
9. The journal is deleted only after the live file, active state, and exact
   transaction-owned temporary-file and backup-file cleanup are complete. A
   transaction recovery credential is retained until that journal deletion
   succeeds, then removed on a best-effort basis.
10. Language, appearance, and startup preferences never read or modify provider
    files or encrypted profile blobs.
11. Reading startup state never writes to the registry. Disabling startup removes
    only the exact registration owned by the current executable. Ownership
    requires an unexpanded `REG_SZ` value whose command matches byte-for-byte;
    an unexpected type, empty value, or command is preserved rather than
    overwritten or deleted.
12. Explicitly enabling startup may repair a strict, single-executable command
    for a known application filename only after the previously registered
    executable path is confirmed missing. An existing or inaccessible path is
    preserved.
13. Uninstall preserves `%LOCALAPPDATA%\CodingAgentAccountSwitcher`, including
    preferences and encrypted account snapshots. It invokes the application in
    a non-UI cleanup mode that removes the startup value only when the raw type
    and command exactly match the installed executable.
14. Credential-only Codex and Claude Code blobs written by older releases remain
    readable as credentials plus an empty managed API configuration. Activating
    one clears currently managed API-route and model fields. An active legacy
    source is upgraded to the composite format when switched away from.
15. A multi-file interruption may leave separate authentication absent, but
    cannot pair one profile's credentials with another profile's endpoint.
16. An OpenCode `/connect` authentication-only snapshot is valid and does not
    cause creation of an empty global `opencode.jsonc`.
17. OpenCode capture resolves global `config.json`, `opencode.json`, and
    `opencode.jsonc`, followed by `OPENCODE_CONFIG`. Apply clears managed keys
    from non-target layers and canonicalizes non-empty targets into the custom
    path when set, otherwise global `opencode.jsonc`.
18. OpenCode resolves its default roots through absolute `XDG_CONFIG_HOME` and
    `XDG_DATA_HOME` values when present. Blank values retain the
    `%USERPROFILE%` defaults. Any nonblank relative `XDG_CONFIG_HOME`,
    `XDG_DATA_HOME`, `OPENCODE_CONFIG`, or `OPENCODE_CONFIG_DIR` value blocks
    capture and switch before any managed-file write.
19. OpenCode capture and switch fail closed before writing when known inline or
    directory environment overrides could supply credentials or managed
    `provider`, `model`, or `small_model` values.
20. Profile rename changes only local vault metadata. Profile delete removes
    only the selected local encrypted snapshot and metadata. Deleting the
    last-selected profile clears its local active association but never changes
    live provider files. A pending recovery journal blocks both operations.
21. Update discovery performs one anonymous request only after an explicit user
    action. It never runs at startup or in the background and never downloads or
    executes an installer or portable asset automatically.
22. The rolling workflow uses one `APP_VERSION` value for the executable and
    Release notes. The notes contain exactly one marker in the format
    `<!-- coding-agent-account-switcher-version: 0.1.N -->`; the rolling
    `latest` tag itself is not treated as a semantic version.

## Provider contracts

### Codex

- Live authentication: `%USERPROFILE%\.codex\auth.json`
- Managed configuration: `%USERPROFILE%\.codex\config.toml`
- `CODEX_HOME`, when explicitly set, changes both paths' root.
- Required credential backend: `cli_auth_credentials_store = "file"`
- Managed top-level keys: `model_provider`, `openai_base_url`, `model`,
  `review_model`, `model_reasoning_effort`, and `disable_response_storage`.
- The selected active `model_providers` table and
  `features.responses_websockets_v2` are also managed.
- `network_access`, `windows_wsl_setup_acknowledged`, `features.goals`,
  `cli_auth_credentials_store`, MCP, skills, sessions, and all other keys and
  tables remain live and are not part of a profile.

### Claude Code

- Live authentication: `%USERPROFILE%\.claude\.credentials.json`
- Managed configuration: `%USERPROFILE%\.claude\settings.json`
- `CLAUDE_CONFIG_DIR`, when explicitly set, changes the credential root and
  must be resolved before constructing the adapter.
- Only these keys inside `settings.json`'s `env` object are managed:
  `ANTHROPIC_BASE_URL`, `ANTHROPIC_API_KEY`, `ANTHROPIC_AUTH_TOKEN`,
  `CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC`, and the compatibility field
  `CLAUDE_CODE_ATTRIBUTION_HEADER`.
- All other settings, including unrelated `env` values, remain live and are not
  part of a profile.
- `.claude.json` is mixed global state and must never be treated as a pure
  authentication file.

### OpenCode

- Optional opaque `/connect` authentication:
  `<data-root>\opencode\auth.json`.
- Configuration and data roots default to `%USERPROFILE%\.config` and
  `%USERPROFILE%\.local\share`. Absolute `XDG_CONFIG_HOME` and `XDG_DATA_HOME`
  values replace the corresponding defaults; blank values do not. Every
  nonblank OpenCode path override (`XDG_CONFIG_HOME`, `XDG_DATA_HOME`,
  `OPENCODE_CONFIG`, and `OPENCODE_CONFIG_DIR`) must be absolute; relative
  values fail closed before capture or switch writes any managed file.
- Managed global configuration loads in increasing precedence from
  `<config-root>\opencode\config.json`, `opencode.json`, and
  `opencode.jsonc`. The exact `OPENCODE_CONFIG` path, when set, loads last.
- Managed top-level values: `provider`, `model`, and `small_model`.
- Capture merges those layers and stores the effective managed result as one
  API profile.
- Apply preserves unrelated semantic values in every file while clearing the
  managed keys from lower or other global layers. With `OPENCODE_CONFIG`, that
  custom path receives target managed values and all global layers are cleared.
  Without it, non-empty target values are canonicalized into global
  `opencode.jsonc`, even if only `opencode.json` or legacy `config.json`
  previously existed; managed keys are removed from those lower layers.
- This covers both OpenCode credential styles: a separate `/connect`
  `auth.json` and API keys embedded in the managed `provider` object. A snapshot
  may contain either style or both.
- An authentication-only or empty-managed snapshot clears existing managed
  values but does not create an empty `opencode.jsonc`.
- A nonblank `OPENCODE_AUTH_CONTENT` blocks capture and switch.
  `OPENCODE_CONFIG_CONTENT` is inspected read-only and blocks when invalid
  JSON/JSONC or when it contains top-level `provider`, `model`, or
  `small_model`; unrelated-only content is allowed.
- When `OPENCODE_CONFIG_DIR` is set, its `opencode.json` and `opencode.jsonc`
  are inspected read-only. An unreadable or invalid file, or a managed key in
  either file, blocks capture and switch; unrelated-only directory
  configuration is allowed. None of these environment-provided sources is
  modified.
- Project-level configuration, centrally managed OpenCode sources, and
  provider-specific environment variables remain unmanaged and are never
  searched or modified. Their higher precedence may override the global result
  after a switch.
- Blocking process rules include both `opencode` and `opencode-cli`.

`disable_response_storage`, `features.responses_websockets_v2`, and
`CLAUDE_CODE_ATTRIBUTION_HEADER` are compatibility fields retained for existing
API-site profiles; they are not evidence that every current upstream release
documents those names.

## Account identity

The application binds a user-chosen display label to an encrypted composite
snapshot. It does not inspect a token to prove the email address, organization,
or API service stored inside. If a user signs in, signs out, or changes a
managed API field outside the application, they must capture the current state
again under the correct label. A content mismatch cannot distinguish a harmless
token refresh from another account or API site, so save-before-switch pauses
for explicit confirmation and offers to capture the state separately.

Renaming changes only that display label and its local metadata. Deleting a
profile removes only its encrypted local snapshot. When the deleted profile was
last selected, the application clears its local association, but this is not a
sign-out operation and the live provider files remain exactly as they were.
Because no source profile is then associated with that live state, the user must
save the current account before switching again. Neither rename nor delete is
available while interrupted transaction recovery is pending.

## Refreshed-source switch recovery

For every composite or multi-file ordinary profile-switch transaction, the
service writes the exact canonical live managed snapshot to a transaction-only
DPAPI-encrypted recovery blob before writing the `Prepared` journal. It does so
even when the live snapshot matches the saved source, because a partial
multi-file write still requires exact rollback and a credential-only legacy
source may need an in-place format upgrade. The service updates the source
snapshot only after the final process-safety check. If interruption occurs
between those steps, recovery prefers the encrypted pre-switch snapshot and
synchronizes the source profile when restoring it. A legacy `ProfileSwitch`
journal with no recovery blob falls back to the saved source snapshot.

The journal contains only transaction metadata; it never stores the recovery
snapshot, its fingerprint, or the live managed paths. If the live snapshot
matches neither the selected source evidence nor the target snapshot, recovery
keeps both the journal and encrypted recovery blob for manual attention.

## Last-selected snapshot restore

Selecting **Restore snapshot** on the last selected card never silently assumes
that its saved snapshot is still live. Matching canonical content returns
without a write. A mismatch returns an in-memory SHA-256 confirmation
fingerprint; the subsequent
request proceeds only if the live snapshot still matches that fingerprint. The
fingerprint is never written to disk or logged.

Before a composite or multi-file restore transaction writes its target, the
service stores the exact live managed snapshot in an opaque DPAPI-encrypted
recovery blob and then writes the journal. This applies even when the source
profile already matches that live snapshot. Recovery can then recognize either
the pre-restore snapshot or the target snapshot after an interruption. Any
third state is preserved and requires manual attention.
