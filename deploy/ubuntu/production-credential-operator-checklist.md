# Production credential operator checklist

This checklist is the stop/go record for migrations 377, 378, and 379. It does not record that any item below has been performed. A path outside the configuration directory is not off-machine evidence.

## Evidence tokens

Each token is accepted only as the exact value `true`. Unset, empty, `True`, and `false` are missing evidence.

| Token | Meaning | Required before |
|---|---|---|
| `PRODUCTION_CREDENTIAL_DB_BACKUP_VERIFIED` | A database backup was taken and its archive was checked. | Key copy and migration |
| `PRODUCTION_CREDENTIAL_DB_RESTORE_VERIFIED` | That backup was restored on another machine and the restore was checked. | Key copy and migration |
| `OFFMACHINE_CONFIRMED` | The key destination is another machine, not another directory on this machine. | Key copy |
| `PRODUCTION_CREDENTIAL_KEY_BACKUP_VERIFIED` | The key file exists on the other machine and was not left only on this machine. | Migration |
| `PRODUCTION_CREDENTIAL_KEY_RESTORE_VERIFIED` | The key-check script compared the source key with the copy brought back from the other machine and printed `PRODUCTION_CREDENTIAL_KEY_RESTORE_OK`. | Migration |
| `PRODUCTION_CREDENTIAL_MIGRATION_APPROVED` | An operator explicitly approved applying 377, 378, and 379. | Migration |

`ProductionCredentials:OffMachineBackupConfirmed` is a separate application setting. It is accepted only as the exact value `true`. It is not given a default in application settings. Setting it does not replace the tokens above.

## Stop

Stop, and do not continue to the next stage, when any required token is missing or any command prints a code other than the expected code.

| Code | Stop |
|---|---|
| `PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED` | Key copy refused. Destination or database evidence is missing. |
| `PRODUCTION_CREDENTIAL_KEY_BACKUP_SAME_MACHINE` | Destination is on this machine, including a different directory. |
| `PRODUCTION_CREDENTIAL_KEY_MISSING` | Key file or key value is absent. |
| `PRODUCTION_CREDENTIAL_KEY_INVALID` | Key is not 32 bytes. |
| `PRODUCTION_CREDENTIAL_KEY_RESTORE_MISMATCH` | Restored copy does not match the source key. |
| `PRODUCTION_CREDENTIAL_PRECONDITION_MISSING:<name>` | Migration runner refused because that token is not exact `true`. |

`PRODUCTION_CREDENTIAL_KEY_BACKUP_WRITTEN` means the remote copy command finished. It does not mean the restore check passed.

## Order

1. Take the database backup and check the archive. Only then set `PRODUCTION_CREDENTIAL_DB_BACKUP_VERIFIED=true`.
2. Restore that backup on another machine and check the restore. Only then set `PRODUCTION_CREDENTIAL_DB_RESTORE_VERIFIED=true`.
3. Confirm the key destination is another machine. Set `OFFMACHINE_CONFIRMED=true` and `OFFMACHINE_DEST=user@host:/absolute/path`. `host` must not be `localhost`, `127.0.0.1`, `::1`, or this machine. A local path, including `/tmp`, `/home`, or `/var/backups`, is refused.
4. Run `deploy/ubuntu/backup-production-credential-key.sh`. Expected line: `PRODUCTION_CREDENTIAL_KEY_BACKUP_WRITTEN`. The script does not print the key.
5. On the other machine, run `deploy/ubuntu/check-production-credential-key.sh` with `ENV_FILE` pointing at the copied file. Expected line: `PRODUCTION_CREDENTIAL_KEY_OK`.
6. Bring a copy of that file back and run the checker with `ENV_FILE` on the source and `RESTORE_ENV_FILE` on the returned copy. Expected line: `PRODUCTION_CREDENTIAL_KEY_RESTORE_OK`. Only then set `PRODUCTION_CREDENTIAL_KEY_BACKUP_VERIFIED=true` and `PRODUCTION_CREDENTIAL_KEY_RESTORE_VERIFIED=true`.
7. Run `deploy/ubuntu/check-production-credential-key.sh` against `/etc/kit-platform/api.env`. Expected line: `PRODUCTION_CREDENTIAL_KEY_OK`.
8. Set `PRODUCTION_CREDENTIAL_MIGRATION_APPROVED=true` only after a separate migration approval.
9. Run `deploy/ubuntu/run-kit-migrations-production-credentials.sh`. It applies only `377_pack_content_production_credential_domain.sql`, `378_pack_content_production_credential_secret.sql`, and `379_pack_content_production_credential_freeze.sql`. Expected ending line: `=== Production credential migrations finished ===`.

## Windows workstation pull

`backup-production-credential-key.sh` copies to an SSH server (`OFFMACHINE_DEST=user@host:/absolute/path`). The approved Windows workstation does not run an SSH server. Do not install `sshd` and do not open a Windows port for this copy.

On that workstation, stage with `deploy/ubuntu/stage-production-credential-key-for-pull.sh`, then pull and seal with `deploy/windows/production-credential-key/Pull-ProductionCredentialKeyBackup.ps1`. `PRODUCTION_CREDENTIAL_KEY_PULL_STAGED` means a short-lived file is waiting on the VPS. It is not `PRODUCTION_CREDENTIAL_KEY_BACKUP_WRITTEN`, and it is not off-machine evidence. The workstation procedure is `deploy/windows/production-credential-key/OPERATOR.md`.

Restore evidence is still `PRODUCTION_CREDENTIAL_KEY_RESTORE_OK` from `check-production-credential-key.sh`. This section does not record that a key was created, staged, sealed, or restored.

## Admin UI

`/content/ai` reads `GET /api/content/production/credentials/backup-gate`. The response contains the write-gate state, the current encryption key id, and `restoreVerification=UNVERIFIED`. It does not contain the encryption key, a provider API key, or ciphertext. ADMIN role and the content module are required. A user without that access is told to ask an administrator.

Saving a credential returns `PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED` when `ProductionCredentials:OffMachineBackupConfirmed` is missing or is any value other than the exact token `true`. The drawer keeps the unsaved provider secret in the form and shows that the credential was not stored. The action opens `/content/ai/credential-security`, which reads the backup-gate status again. That screen has no confirmation button. That code is not a verdict that the Gemini API key is wrong. The Gemini API key and the production encryption key are different secrets. Do not paste either into chat, logs, or a report.

The UI labels are `Chưa đạt` when the write gate is closed, `Chưa xác minh` when the write gate is open, and `Không kiểm tra được` when the status cannot be read. The application does not label restore as verified, because this setting is not restore evidence.

Operator order after the UI shows the code:

1. Read the encryption key id from the backup-gate response. Compare it with the key id of the off-machine copy. A copy, seal, or file for a different key id is not evidence for the id in use.
2. Complete the backup and restore checks in this checklist for that key id. Keep the expected evidence lines. Do not treat a same-machine copy as evidence.
3. Set `ProductionCredentials__OffMachineBackupConfirmed` to the exact token `true` only in the process environment, after those checks. Do not put that token in source, `appsettings.json`, or `appsettings.Development.json`, and do not add a button that writes it.
4. Return to the open drawer and choose `Thử lưu lại`. The save is sent only when someone clicks. After the credential is stored, select it on the Hình ảnh row, enable the row, and save Tenant AI Config. Reload the page and compare the status with the server.

## Not done by this record

This file does not attest that the database backup, the database restore, the key copy, the key restore, the migration, or a live probe has been executed. Live probes, including `ProductionCredentialRotationLiveProbe`, stay unwired to a new command. Do not run them until a separate approval names the database and the tenant.
