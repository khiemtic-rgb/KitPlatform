# SB Research Safe Host production deployment

Status: `DEPLOYED_AND_VERIFIED`

Host: `103.200.23.229`  
Service: `kit-platform-api`  
Window observed on the host: `2026-10-23T05:22:39Z` through `2026-10-23T05:39:17Z`  
Repository: `E:\KitPlatform-sb-research`  
Branch: `admin-build-blocker-001`  
HEAD: `85db8bdfd269b3eef12fb1729d95d5b59596c79e`

## Candidate

| File | Before | After |
|---|---|---|
| `KitPlatform.Api.dll` | `ec974eccdd0e6cd4d83c5e3480f6757650d8a040c2fa80a7f747e9b403b73319` | `f394e67433952a70e26f80c22f1d5b96822bfe00ed59c59d8d9a53daf24a0ba9` |
| `KitPlatform.Packs.PersonalIntelligence.Api.dll` | absent | `50253a9471143b5025bb4decb468f134740d9e90801d85d6d8a964b3b44e4ffa` |

The candidate publish differed in 26 top-level KitPlatform assemblies, including `KitPlatform.Api.deps.json` and the three Personal Intelligence assemblies. Those 26 files were installed. Third-party DLLs already matched and were left in place. `appsettings*.json`, `uploads/`, and the rest of `api.env` were not overwritten. `rsync --delete` was not used.

## Backup

Path: `/var/backups/kit-research-20261023T052648Z`  
Mode: `700`  
Outside nginx roots and outside the publication directory.

| Item | Evidence |
|---|---|
| API tree | `KitPlatform.Api.dll` in the backup is still `ec974eccdd0e6cd4d83c5e3480f6757650d8a040c2fa80a7f747e9b403b73319` |
| `api.env` | 2536 bytes, mode `600` |
| Publication | not present at backup time |
| Database | `novixa_prod`, 151665687 bytes |
| `novixa_prod.dump` | 19393644 bytes, SHA-256 `7c6af635d3d7c797ee06174847b73a73b8e851729d8189b4eae7f533cf500460` |
| `pg_restore -l` | 3396 TOC lines, exit 0 |

The API role could not `pg_dump` the whole database (`permission denied` while locking `public.family_os_demo_view`). The archive was taken with local peer `postgres` and `pg_dump -Fc`, without printing a connection string. No extra database privilege was granted. The database was not restored.

## Publication

Path: `/var/lib/kit-platform/research-publication`  
Not a symlink. `readlink -f` returns the same path.  
Owner `root`, group `www-data`, mode `750`.  
`www-data` can read and traverse. The prepare script refused to finish if `www-data` could write.  
Preflight: `PUBLICATION_OUTSIDE_WEB_ROOTS yes`, `PUBLICATION_GROUP_WRITE no`, `PUBLICATION_OTHER_WRITE no`, `PUBLICATION_SERVICE_ACCESS read-execute`.  
The directory stayed empty across the anonymous publication request (`find` entry count 1 before and after).

Environment: one line was appended to `/etc/kit-platform/api.env`:

`PersonalIntelligence__ResearchPublication__Root=/var/lib/kit-platform/research-publication`

File mode remains `600`. Size changed from 2536 to 2628 bytes. `ResearchSafeHost__SuppressBackgroundWriters` is absent.

## Migration 384

File SHA-256 `a35bd757ed66aa814f34ceb1f8ec4e564a2b1407bfae805a004a9bdbf3a52534`.  
Applied with `ON_ERROR_STOP` to `novixa_prod` after the backup and after `current_database()` was `novixa_prod`.

| Check | Result |
|---|---|
| `narrative_json` type | `jsonb` |
| Brand rows | 6 before and 6 after |
| `narrative_json.brandCode` equals `code` | 6 of 6 |
| CTAs | unchanged, including `tnlife` = `https://thainguyenlife.vn` and empty `xuanhoa` |

There was no existing narrative column, so there was no prior narrative value to preserve. The seed guard still applies on a later run.

## Preflight

Script SHA-256 `c1b38e9c7a8d32387f29d6423106cfeb70bd051f4173a27def51e71971d586ab`.

Linux self-test on this host, before changes: PASS 6, FAIL 0, NOT RUN 1 (full filesystem). Exit 0.

Before changes: `PREFLIGHT_DONE blocked`, exit 2. The only blockers were the absent narrative column and the unset publication path. Database identity was `novixa_prod|5432|127.0.0.1|5432`. Service was active. Suppress flag and exchange credential were absent.

After publication and migration, before the DLL copy: `PREFLIGHT_DONE complete`, exit 0. `PUBLICATION_CONFIGURED yes`. `NARRATIVE_TYPE_OK yes`. No `BLOCKED` line. Live DLL was still the previous hash.

## After restart

`systemctl restart kit-platform-api` only. nginx was not restarted and was `active`.

| Check | Result |
|---|---|
| `GET http://127.0.0.1:5000/api/health` | 200, repeated after the service settled |
| Anonymous publication | 401 |
| `GET /api/personal-intelligence/trading/summary` | 404 |
| Service | `active` |
| Application started | present in this restart's journal |
| Secret-pattern hits in that journal | 0 |
| `https://admin.novixa.vn/` | 200 |
| `https://app.novixa.vn/` | 200 |
| Uploads | 2213970 bytes, unchanged |
| appsettings sizes | 3074, 3490, 4324, unchanged |

## Production changes

- Created `/var/lib/kit-platform/research-publication`.
- Appended the publication root key to `/etc/kit-platform/api.env`.
- Applied `migrations/384_pack_content_brand_narrative_dna.sql` on `novixa_prod`.
- Replaced 23 existing KitPlatform assemblies and added 3 Personal Intelligence assemblies, plus `KitPlatform.Api.deps.json`.
- Restarted `kit-platform-api`.
- Created the backup directory above.

No database restore. No rollback. `DEPLOYED_AND_VERIFIED`.
