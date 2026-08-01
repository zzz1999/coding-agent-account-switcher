# Architecture

Coding Agent Account Switcher is split into a provider-independent transaction
core and a WPF presentation layer.

## Components

- **Provider adapter**: identifies one canonical authentication file and the
  process names that make switching unsafe.
- **Process guard**: returns `Clear`, `Running`, or `Unknown`. Only `Clear`
  permits a credential operation. It never terminates a process.
- **Profile vault**: stores non-secret metadata plus DPAPI-encrypted opaque
  credential bytes below the current user's local application data directory.
- **Authentication-location scope**: hashes the normalized live authentication
  path. Every path receives an independent vault, active state, journal, and
  mutex without persisting the user-profile path in metadata.
- **Switch transaction**: serializes operations per provider, captures the
  active profile, stages the target in the destination directory, flushes it,
  and atomically replaces the live authentication file.
- **Recovery journal**: records non-secret transaction phases so an interrupted
  operation can be recovered or blocked safely on the next startup. Its
  transaction ID identifies the exact plaintext replacement temporary and
  backup files owned by that operation.
- **Transaction recovery credential**: when confirmed live bytes differ from
  the saved source during either an ordinary profile switch or a saved-snapshot
  restore, those exact pre-switch bytes are kept in a transaction-only
  DPAPI-encrypted blob until commit or rollback completes.
- **WPF application**: presents provider tabs, account cards, capture and switch
  actions, process-blocking dialogs, and status messages.
- **Localization catalog**: supplies every fixed interface string for the
  supported cultures and can refresh the active window without restarting the
  application. Arabic also switches the application shell to right-to-left
  flow.
- **Application preferences**: stores only the selected UI language and other
  non-secret application choices below `%LOCALAPPDATA%\CodingAgentAccountSwitcher`.
  Preferences are separate from every path-scoped authentication vault.
- **Startup registration**: manages one application-owned value in the current
  user's `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` key. It never
  writes a machine-wide startup entry and does not require elevation.
- **Windows installer**: installs the self-contained x64 application below the
  current user's local application data, adds a Start Menu shortcut and an
  HKCU uninstall entry, and never requests elevation. Installation does not
  enable startup or read, write, or migrate application data.

## Invariants

1. Provider authentication contents are opaque bytes.
2. Ordinary provider configuration is never copied or replaced.
3. A detected or unknown process state causes zero credential writes.
4. A target snapshot is decrypted and validated before the active snapshot is
   changed.
5. Temporary plaintext staging and backup files exist only in the live
   authentication directory, have transaction-derived names, and are removed
   after replacement or rollback.
6. Logs and journal files contain no credentials, email addresses, plaintext
   paths containing a Windows user name, or credential hashes.
7. A work profile and personal profile may not share an encrypted blob.
8. A live file that differs from the last selected snapshot requires explicit
   confirmation bound to those exact bytes before the snapshot is updated or
   restored over the live file.
9. The journal is deleted only after the live file, active state, and exact
   transaction-owned temporary-file and backup-file cleanup are complete. A
   transaction recovery credential is retained until that journal deletion
   succeeds, then removed on a best-effort basis.
10. Language and startup preferences never read or modify provider authentication
    files, provider configuration, or encrypted profile blobs.
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

## Provider contracts

### Codex

- Live authentication: `%USERPROFILE%\.codex\auth.json`
- `CODEX_HOME`, when explicitly set, changes the authentication root.
- Required credential backend: `cli_auth_credentials_store = "file"`
- Shared configuration remains in the active Codex root: `%USERPROFILE%\.codex`
  by default, or `CODEX_HOME` when that override is set.

### Claude Code

- Live authentication: `%USERPROFILE%\.claude\.credentials.json`
- `CLAUDE_CONFIG_DIR`, when explicitly set, changes the credential root and
  must be resolved before constructing the adapter.
- `.claude.json` is mixed global state and must never be treated as a pure
  authentication file.

## Account identity

The application binds a user-chosen display label to an encrypted snapshot. It
does not inspect a token to prove the email address or organization stored
inside. If a user signs in or out outside the application, they must capture the
current login again under the correct label. A byte mismatch cannot distinguish
a harmless token refresh from another account, so save-before-switch pauses for
explicit confirmation and offers to capture the login separately.

## Refreshed-source switch recovery

When a confirmed live login differs from the saved source during an ordinary
profile switch, the service writes those exact bytes to a transaction-only
DPAPI-encrypted recovery blob before writing the `Prepared` journal. It updates
the source snapshot only after the final process-safety check. If interruption
occurs between those steps, recovery prefers the encrypted pre-switch bytes and
synchronizes the source snapshot when restoring them. A legacy `ProfileSwitch`
journal with no recovery blob falls back to the saved source snapshot.

The journal contains only transaction metadata; it never stores the recovery
bytes, their fingerprint, or the live authentication path. If the live file
matches neither the selected source evidence nor the target snapshot, recovery
keeps both the journal and encrypted recovery blob for manual attention.

## Last-selected snapshot restore

Selecting **Restore snapshot** on the last selected card never silently assumes
that its saved bytes are still live. Matching bytes return without a write. A
mismatch returns an in-memory SHA-256 confirmation fingerprint; the subsequent
request proceeds only if the live bytes still match that fingerprint. The
fingerprint is never written to disk or logged.

Before the saved snapshot replaces different live bytes, the service writes an
opaque DPAPI-encrypted recovery credential and a journal. Recovery can then
recognize either the pre-restore bytes or the target snapshot after an
interruption. Any third state is preserved and requires manual attention.
