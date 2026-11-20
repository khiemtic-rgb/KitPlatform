# Windows off-machine copy of the production credential key

This procedure is the backup path for the workstation that does not run an SSH server. It does not create a production key, write `/etc/kit-platform/api.env`, set `ProductionCredentials__OffMachineBackupConfirmed`, set migration tokens, apply migrations 377–379, or restart the API.

The admin screen `/content/ai` reports `PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED` when that confirmation is absent. A sealed file on this workstation is evidence only for the key id inside that seal. If the backup-gate response names a different encryption key id, this seal does not satisfy the gate. The provider API key entered in the drawer is a separate secret and was not saved. See `deploy/ubuntu/production-credential-operator-checklist.md`.

The push script `deploy/ubuntu/backup-production-credential-key.sh` needs an SSH server at `OFFMACHINE_DEST`. Do not install `sshd` on this workstation and do not open a Windows port for the copy.

## Where the key lives

| Copy | Location | Protection |
|---|---|---|
| Primary | VPS `103.200.23.229`, `/etc/kit-platform/api.env`, names `ProductionCredentials__DataEncryptionKey` and `ProductionCredentials__DataEncryptionKeyId` | Existing VPS file permissions. This procedure does not create the key. |
| Staged pull | VPS `/run/kit-production-credential-pull/key-<32 hex>.env` for the duration of the pull | Mode `0400`, owned by `PULL_USER`, tmpfs. Not an off-machine backup. Delete it in the same sitting. |
| Workstation | `%LOCALAPPDATA%\KitPlatform\production-credential-backup\production-credential-key.dpapi` | DPAPI `CurrentUser` for the logged-in operator, NTFS ACL limited to that user. |

The manifest next to the sealed file stores the non-secret key id, the sealed-file length, and the SHA-256 of the ciphertext. It does not store the key or a hash of the key.

`C:`, `D:`, and `E:` are partitions of one NVMe disk. A second folder on `D:` is not a second machine. EFS is not available on this Windows 11 Home edition.

## Transfer

1. On the VPS, with the three evidence variables set to the exact value `true` and `PULL_USER` set to the SSH account, run `deploy/ubuntu/stage-production-credential-key-for-pull.sh`. Expected first line: `PRODUCTION_CREDENTIAL_KEY_PULL_STAGED`. That line is not `PRODUCTION_CREDENTIAL_KEY_BACKUP_WRITTEN`.
2. On the workstation, in the same sitting, run `Pull-ProductionCredentialKeyBackup.ps1` with `-SshUser`, `-SshHost 103.200.23.229`, and `-RemotePath` set to the printed `PULL_PATH`. The host key for that IP is already pinned. `novixa.vietnix.vn` is not pinned; do not switch SSH to trust-on-first-use.
3. The pull seals the two-line file, deletes the local plaintext, then deletes the remote staged file. Expected lines: `PRODUCTION_CREDENTIAL_KEY_BACKUP_SEALED` and `PRODUCTION_CREDENTIAL_KEY_PULL_REMOTE_DISCARDED`.
4. This workstation has no stored SSH private key. Add `-AllowInteractivePassword` only while an operator is present. Do not write the password into a file, a script, or a log. Batch mode is the default and fails with `PRODUCTION_CREDENTIAL_KEY_PULL_AUTH_REQUIRED` when no key and no interactive password are available.

`-PrecheckOnly` checks the host, the path, and the known-host pin. It does not open a connection.

## Integrity

On the workstation, `Open-ProductionCredentialKeyBackupCheck.ps1` prints `PRODUCTION_CREDENTIAL_KEY_OK` when the sealed blob decrypts to a 32-byte key whose id matches the manifest. Pass `-ComparePlaintextPath` only for a two-line file outside this repository. A match prints `PRODUCTION_CREDENTIAL_KEY_RESTORE_OK`.

The migration token still requires the VPS checker. Export a temp file with `-ExportTo` under `%TEMP%`, copy that file back with `scp` to a path outside `/etc/kit-platform`, and run `deploy/ubuntu/check-production-credential-key.sh` with `ENV_FILE` on the VPS source and `RESTORE_ENV_FILE` on the returned file. The expected line is `PRODUCTION_CREDENTIAL_KEY_RESTORE_OK`. Delete the temp file on both sides after the check. Only then may an operator set `PRODUCTION_CREDENTIAL_KEY_BACKUP_VERIFIED` and `PRODUCTION_CREDENTIAL_KEY_RESTORE_VERIFIED`. This document does not set them.

## Loss

| Loss | Action |
|---|---|
| Workstation or sealed file, VPS key still present | Repeat the pull and seal. Do not generate a replacement key. |
| VPS key, sealed file still present | Export a temp file, copy it back to the VPS, and install it into `api.env` under a separate approval. Delete the temp files. Confirm with `check-production-credential-key.sh`. |
| VPS key and sealed file | Credential ciphertext cannot be decrypted. There is no third copy. |
| Staged file left on the VPS | Run `deploy/ubuntu/discard-production-credential-key-pull-stage.sh` with the staged path. The script refuses `/etc/kit-platform/api.env`. |

## Rollback of this preparation

The empty vault is removed with `Undo-ProductionCredentialKeyBackupPreparation.ps1`. The script stops with `PRODUCTION_CREDENTIAL_KEY_BACKUP_EXISTS` when a sealed file is present. Removing scripts from the working tree does not change the VPS. No database backup is part of this rollback.
