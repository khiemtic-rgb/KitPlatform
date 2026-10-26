# SB-RESEARCH-SAFE-HOST-DEPLOY-READINESS-004

Date: 2026-10-22
Repository: `E:\KitPlatform-sb-research`
Branch: `admin-build-blocker-001`

Executive decision: **OWNER_APPROVAL_REQUIRED**

The host at `6596cfb9` was tested and built again. A second Release publish produced the same API DLL hash, and a second admin build reproduced all 312 pinned admin files. The runbook now names the files the copy may change. Production was not deployed.

This is not `DEPLOY-READY`. The remaining items are owner decisions and owner operations: migration 390, the tenant module lists, the `research.read` permission and role, the publication directory, a read-only check of `pack_content.brand.narrative_json`, a login on an approved non-production host, and a separate restart approval. No safe local path could verify the database or a login for this commit.

## 1. Git

| Item | Value |
|---|---|
| Base | `3d5d7b42f9ca98032c7aec6638797234650a5abd` |
| HEAD | `6596cfb996b3b4698917cb6f72b809931a42cc84` |
| Parent | `3d5d7b42f9ca98032c7aec6638797234650a5abd` |
| Upstream | Not configured |
| Remote | `https://github.com/khiemtic-rgb/KitPlatform.git` |
| Tracked diff | Empty |
| Push | Not done |

HEAD contains the same nine isolation and test files recorded in `SB-RESEARCH-SAFE-HOST-RELEASE-PREP-003.md`. Untracked review files were left in place. This mission edited the delivery runbook and added this report. It did not commit them.

## 2. What this mission did

- Re-ran `ResearchPublicationTests`, the publisher tests, and both UI smokes on `6596cfb9`.
- Published the API a second time with SDK `10.0.301` and compared it with the pinned DLL.
- Rebuilt the admin app and compared all 312 files with `admin-SHA256SUMS.txt`.
- Confirmed the publication reader source has no database call.
- Confirmed `KIT_PI_TEST_CONNECTION` is unset.
- Saw `KitPlatform.Api.exe` listening on port 5290 from a different tree. It was not called and is not this commit.
- Added the allowed and forbidden file list to the existing runbook.

No migration, permission, tenant, credential, server, or production change was made.

## 3. Tests and builds

| Command | Exit | Result |
|---|---|---|
| `dotnet test tests/KitPlatform.PersonalIntelligence.Tests/KitPlatform.PersonalIntelligence.Tests.csproj --filter FullyQualifiedName~ResearchPublicationTests --nologo` | 0 | 16 passed, 0 failed. Debug test host. |
| `python -m unittest tests.test_publish_research_v1` in `tools/research-publication` | 0 | 7 tests, OK |
| `npx tsx src/modules/second-brain/research-view.smoke.ts` | 0 | `research view ok` |
| `npx tsx src/modules/second-brain/trading-routes.smoke.ts` | 0 | `trading routes ok` |
| `dotnet publish src/KitPlatform.Api/KitPlatform.Api.csproj -c Release -o artifacts/sb-research-safe-host-004-repro/api` | 0 | SDK `10.0.301`, target `net10.0` |
| `npm run build` in `client/admin` | 0 | `tsc --noEmit && vite build`, 15.64s |

The test host, not a browser, is the access-control evidence. It covers anonymous 401, `research.read` without `trading.run`, ADMIN, `trading.run`, exchange-only 403, module-off 403 for both `research.read` and ADMIN, hidden trading routes, and the absence of trading repositories, `IExchangeBackfill`, `IExchangeCredentialKeyRing`, and `ExchangeBackfillWorker`.

## 4. Artifacts

Deploy input remains the first pinned tree. The second build is the repeat check.

| Item | SHA-256 |
|---|---|
| `artifacts/sb-research-safe-host-003/api/KitPlatform.Api.dll` | `8DB1EBC5B4B1109FA0A458BB1FA9291D5C6892FFF342608E0F0D9D03CCABF3A8` |
| `artifacts/sb-research-safe-host-004-repro/api/KitPlatform.Api.dll` | same hash |
| `artifacts/sb-research-safe-host-003/admin/index.html` and the rebuilt `client/admin/dist/index.html` | `12B52DBC551C5F987A6F5CEB69FECF5BC8D6D53FD2BB52FF34230F77388B9B6D` |
| `artifacts/sb-research-safe-host-003/admin-SHA256SUMS.txt` | `0174F8E9EDB9B8C22C5D0F343D3D5127C342D9E753464A27F5AB9C945586514B` |

The rebuilt admin tree matched the manifest on 312 files, with 0 mismatches. The second API DLL contains `ResearchOnlyControllerConvention` and `AddPersonalIntelligenceResearch`, and does not contain `ExchangeBackfillWorker`.

This is a repeat on one machine and one SDK. It is not a clean clone on another computer. The earlier dirty-tree DLL `11B04205E9E8F88DFA44107043F0E0FF5992595C1701FF33E1B70C16D70D41F9` stays withdrawn.

## 5. Architecture and runtime

The conformance judgment in prep-003 still holds: one shared API, one shared admin app, module filter on the research controller, trading controllers hidden, trading code still present in the infrastructure DLL and in the admin bundle. Presence in the bundle does not map the trading API.

`FileResearchPublicationReader` has no database, `pack_content`, or `pack_personal_intelligence` reference. An empty publication root returns `MISSING`. The column `narrative_json` is read by `ContentRepository` in the same shared process. It is not a research-reader dependency. Its presence on the target database is `UNVERIFIED`. `KIT_PI_TEST_CONNECTION` was unset, and no production catalog query was run.

Port 5290 is a `KitPlatform.Api.exe` from another tree. It is not `6596cfb9`. No request was sent to it. No read-only account exists for this host. Browser login is `UNVERIFIED`.

## 6. Blockers

| Item | State | Next owner action |
|---|---|---|
| Migration 390 | OWNER_APPROVAL_REQUIRED | Approve or reject the single registry insert on a named database. Do not apply `deploy/ubuntu/migration-files.personal-intelligence.txt`. |
| Tenant `allowed_modules` and `enabled_modules` | OWNER_APPROVAL_REQUIRED | Name the tenant and both lists. The runtime gate reads `enabled_modules` only. |
| `personal_intelligence.research.read` | OWNER_APPROVAL_REQUIRED | Insert the permission row first. Then attach it to a named role with an insert that does not delete the role's other permissions. `UpdateRolePermissionsAsync` deletes every current row for the role and ignores unknown codes while still committing. |
| ADMIN and `trading.run` inside the research policy | OWNER_APPROVAL_REQUIRED | Accept the current policy or ask for a later code change. Do not grant `trading.run` as a substitute for read access. |
| Publication directory | OWNER_APPROVAL_REQUIRED | Name the owner and a path outside every nginx `root`. Give `www-data` read-only access. Keep a separate copy. The publisher deletes `.previous`. |
| `narrative_json` | UNVERIFIED | Content database owner runs the read-only `information_schema.columns` query from review 002. Do not run migration 384 to discover the column. |
| Login on this commit | UNVERIFIED | Provide a non-production host built from `6596cfb9` and a user whose token has only `research.read`. The process on port 5290 is not that host. |
| Restart | OWNER_APPROVAL_REQUIRED | A later approval must name `6596cfb9`, DLL hash `8DB1EBC5B4B1109FA0A458BB1FA9291D5C6892FFF342608E0F0D9D03CCABF3A8`, and the window. |

Prepared for that later review, and not executed:

1. Run only `migrations/390_platform_module_personal_intelligence.sql` after the owner confirms `platform_module_registry` exists.
2. Insert `personal_intelligence.research.read` into `public.permissions` with an owner-supplied id, using `ON CONFLICT (permission_code) DO NOTHING`, in the same shape as migration 391. Do not copy the exchange permission rows from 391.
3. Insert one `role_permissions` row for the named role. Do not call the role-update method that deletes the existing set.
4. Set the named tenant's `allowed_modules` and `enabled_modules` through the approved entitlement path. Do not seed them in this repository.

## 7. Runbook

Operative procedure: `src/Packs/PersonalIntelligence/SB-RESEARCH-SAFE-HOST-DELIVERY-001.md` sections 6–8.

This mission added the allowed and forbidden file list and the second-build hash note. The procedure still backs up the API tree, the admin tree, and `api.env`; excludes appsettings, uploads, and `api.env` from the copy; checks hashes before restart; treats restart as a separate approval; and restores `api.env` on rollback. It forbids `deploy-update-vps.ps1` and `deploy-production.ps1`. It was not executed.

## 8. Release checklist

| # | Gate | Status | Evidence | Next action |
|---|---|---|---|---|
| 1 | Architecture conformance | PASS | Prep-003 matrix, reconfirmed against this commit | None in code |
| 2 | Fixed revision, tracked tree clean | PASS | HEAD `6596cfb9`, `git diff` empty. Review notes stay untracked | Do not commit secrets or artifacts |
| 3 | Research and publisher tests | PASS | 16 and 7, both exit 0, this mission | None |
| 4 | API Release and admin build | PASS | Publish exit 0, `npm run build` exit 0 | None |
| 5 | Artifact hash | PASS | Second DLL hash matches. Admin manifest matches 312/312 on this SDK | Do not claim another machine without a repeat there |
| 6 | Host isolation and access control | PASS | Test host only | Browser still open under item 9 |
| 7 | Migration, tenant, permission, schema | OWNER_APPROVAL_REQUIRED | 390 and the permission insert are prepared and not run. `narrative_json` is UNVERIFIED | Section 6 |
| 8 | Publication directory and owner | OWNER_APPROVAL_REQUIRED | Contract is in the runbook. No path is named | Name owner, path, and backup |
| 9 | Runtime login | UNVERIFIED | No approved host for this commit. Port 5290 is another tree and was not called | Owner supplies the host and the read-only user |
| 10 | Backup, deploy procedure, rollback | PASS as a document | Delivery sections 6–8, including `api.env` | Do not execute until the restart approval |
| 11 | Approvals still open | OWNER_APPROVAL_REQUIRED | Section 6 | Off-hours deploy approval is a later step, not this document |

## 9. Production

No production deploy, publish, restart, migration, database write, permission change, tenant change, credential change, or server edit was made. Nothing was pushed.
