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
- **Restore recovery credential**: when the last selected snapshot is restored
  over different live bytes, those pre-restore bytes are kept in a
  transaction-only DPAPI-encrypted blob until commit or rollback completes.
- **WPF application**: presents provider tabs, account cards, capture and switch
  actions, process-blocking dialogs, and status messages.

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
   restore recovery credential is retained until that journal deletion succeeds,
   then removed on a best-effort basis.

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
