# SB Research Safe Host — deploy closure 015

Script version `sb-research-safe-host-016` supersedes the preflight hash below. The current preflight SHA-256 is `c1b38e9c7a8d32387f29d6423106cfeb70bd051f4173a27def51e71971d586ab`. Use `deploy/sb-research-safe-host-014/RUNBOOK.md` and `MANIFEST.sha256`. Do not run `ce10a09925f9b032700aa90ef11a5b82c4661e7daf9ac1c380fc02de82724267`.

Status: **READY FOR PRODUCTION PREFLIGHT**

This is not `READY FOR APPROVED DEPLOYMENT`. No production backup was created. The live host was not observed. Copy, migration, configuration change, and `systemctl restart kit-platform-api` still need a separate approval that names the candidate hash and the time.

## Repository

- Path: `E:\KitPlatform-sb-research`
- Branch: `admin-build-blocker-001`
- HEAD: `85db8bdfd269b3eef12fb1729d95d5b59596c79e`
- Working tree: not clean. `deploy/sb-research-safe-host-014/` and the Personal Intelligence closure reports are untracked. Pre-existing untracked files were left in place. Nothing was reset, checked out, committed, or pushed.

## Package

| File | Lines | Bytes | SHA-256 |
|---|---:|---:|---|
| `deploy/sb-research-safe-host-014/sb-research-preflight.sh` | 1219 | 42900 | `ce10a09925f9b032700aa90ef11a5b82c4661e7daf9ac1c380fc02de82724267` |
| `deploy/sb-research-safe-host-014/sb-research-linux-selftest.sh` | 95 | 3693 | `4e9edfc8c1009ec011c03d18a96ad4a8b5b54a74240d84133999266cac2968cb` |
| `deploy/sb-research-safe-host-014/sb-research-preflight-harness.sh` | 563 | 24781 | `3b5d16402ecd6dcbd3f3993ef20ee81a34a95117c4b461bcb2b090ef446a46f2` |
| `artifacts/sb-research-safe-host-014/api/KitPlatform.Api.dll` | | 1807872 | `f394e67433952a70e26f80c22f1d5b96822bfe00ed59c59d8d9a53daf24a0ba9` |
| `artifacts/sb-research-safe-host-014/api/KitPlatform.Packs.PersonalIntelligence.Api.dll` | | 59904 | `50253a9471143b5025bb4decb468f134740d9e90801d85d6d8a964b3b44e4ffa` |

`sha256sum -c deploy/sb-research-safe-host-014/MANIFEST.sha256` from `deploy/sb-research-safe-host-014` reported all five files `OK`. The preflight is version `sb-research-safe-host-015`, LF, no CR, no BOM, final newline present. `bash -n` exit 0. Embedded Perl `perl -c` exit 0.

The executable runbook is `deploy/sb-research-safe-host-014/RUNBOOK.md`. Section 9.4 of closure 014 is withdrawn as the deploy source. Its old hash `1741bf8493ce7370f4394acf30a96e058e84b0e4b3aa5e9be224401817863bc2` must not be run.

## Publisher

Threat model: other users. The stage is created `O_CREAT|O_EXCL|O_NOFOLLOW` mode 0600 inside a mode-0700 directory owned by this user. `/tmp` is opened `O_DIRECTORY|O_NOFOLLOW`. Group or other write is accepted only when `/tmp` is sticky. Publication is one `rename`. On Linux, `rename` replaces the final directory entry and does not follow a symlink; that property was not executed on this PC. A name check before `rename` is not a TOCTOU barrier. A same-UID process is the operator. This package does not claim that window is closed, and it does not claim protection from an attacker who already runs as root or as the operator.

`SIGKILL` cannot run the trap. The next run removes a leftover private directory only when this user owns it, its mode is exactly 0700, it is not a symlink, and its children are only regular files named `stage`, `acl`, or `psql-err`.

`--publish-self-test` ran on this PC. Exit 0. Stdout and the result file contained `STALE_PRIVATE cleaned=0 left=0` and `PUBLISH_SELFTEST marker`. It does not call `systemctl`, nginx, or `psql`. Exit 0 of that mode is not `PREFLIGHT_DONE complete`.

## Tests

Local harness, executed on the final preflight bytes above:

| Result | Count | Items |
|---|---:|---|
| PASS | 55 | Syntax, Perl syntax, LF, publish of a normal file, directory destination, missing perl, TERM cleanup, stage identity, secrets, wrong database, ambiguous localhost, remote host, TCP failure, explicit socket, wrong socket, `ss` failure, nginx, ACL write, missing getfacl, missing psql, stale private directory, drop-in path with a space, unreadable unit text, publication byte count |
| FAIL | 0 | |
| NOT RUN | 5 | Durable mode 0600. Symlink destination. Symlink-to-directory. Stage write on a full filesystem. Backup parent inside a web root with a real `/var` and `/etc` fixture |

Linux self-test on this PC, final self-test bytes:

| Result | Count | Items |
|---|---:|---|
| PASS | 2 | `path_is_inside`, including the prefix case `/var/backup` is not inside `/var/backups`. `syswrite` to `/dev/full` returns an error. That device is not the production stage file |
| FAIL | 0 | |
| NOT RUN | 4 | Symlink destination. Symlink-to-directory. Durable mode 0600. Stage file on a full filesystem. `/tmp` was not filled |

`NOT RUN` is not `PASS`.

## What this PC can prove

- The script parses, the publisher opens a private directory and renames a regular result, and a directory destination is not written into.
- Replacing the stage inode is detected. TERM removes the private directory. A well-formed stale directory is removed; an unexpected one is left.
- Secrets in the environment fixture, systemd `Environment`, and `psql` stderr do not appear in stdout, stderr, or the result.
- Catalog is not queried for a wrong database name, an ambiguous localhost, a remote host, a failed TCP identity, a wrong socket, or an unreadable listener list.
- An extended ACL write is blocked. A missing `getfacl` while the `+` marker is present is blocked. The local proof hid `getfacl.exe` from `PATH`.
- `path_is_inside` rejects a path that only shares a prefix with the root.
- A write to `/dev/full` fails. That does not prove the stage file on a full disk.

## What only the host preflight can prove

- The live service, user, working directory, systemd drop-ins, nginx roots, publication path, database endpoint, and `novixa_prod` identity.
- Free space at the real backup parent, and that the parent is outside every live nginx root.
- The live DLL hash and the live sizes of `api.env`, appsettings, and uploads.
- Symlink and symlink-to-directory refusal, and mode 0600 as seen by a second process. Run `sb-research-linux-selftest.sh` on the host. It stays in `/tmp` and does not touch the service.

The stage-on-a-full-filesystem case stays `NOT RUN` on the host as well. Filling `/tmp` on the production machine is not an acceptable test. The `/dev/full` probe is the write-error evidence.

## What was not done on production

No connection to `103.200.23.229`. No preflight on the host. No DLL copy. No change to data, tenants, permissions, credentials, secrets, or production configuration. No migration. `ResearchSafeHost__SuppressBackgroundWriters` was not set. Background workers were not disabled. Trading UI, the exchange worker, and the trading flow were not added. `scripts/deploy-update-vps.ps1`, `scripts/deploy-production.ps1`, and `scripts/second-brain-trading-ui-start.ps1` were not run. No process was started on ports 5290, 7224, 5173, 5174, 5432, or 5433. No backup directory was created. `BACKUP_CREATED no` is the current truth.

## Next step

On the existing host, follow `deploy/sb-research-safe-host-014/RUNBOOK.md` section “On the existing Linux host”. Copy the two scripts to `/tmp`, confirm the two hashes, run the Linux self-test, run the preflight, paste only `/tmp/sb-research-preflight-result.txt`, and stop.

A later approval may run the runbook section “Separate deployment approval” only when it names hash `f394e67433952a70e26f80c22f1d5b96822bfe00ed59c59d8d9a53daf24a0ba9` and the time. Until that approval, do not copy, back up, migrate, or restart.
