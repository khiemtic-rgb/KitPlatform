# SB-RESEARCH-SAFE-HOST-FINAL-BLOCKER-CLOSURE-005

Date: 2026-10-22
Repository: `E:\KitPlatform-sb-research`
Branch: `admin-build-blocker-001`

Executive decision: **OWNER_APPROVAL_REQUIRED**

This is not `DEPLOY-READY`. The code, tests, builds, artifact hashes, and runbook from `6596cfb9` are unchanged and were not re-run. What remained was owner input. This mission prepared the SQL and the questions. It did not apply them, and it did not start a host.

## 1. State checked

| Item | Result |
|---|---|
| HEAD | `6596cfb996b3b4698917cb6f72b809931a42cc84` |
| Parent | `3d5d7b42f9ca98032c7aec6638797234650a5abd` |
| Tracked diff | Empty |
| Push | Not done |
| `artifacts/sb-research-safe-host-003/api/KitPlatform.Api.dll` | Still `8DB1EBC5B4B1109FA0A458BB1FA9291D5C6892FFF342608E0F0D9D03CCABF3A8` |
| `artifacts/sb-research-safe-host-004-repro/api/KitPlatform.Api.dll` | Same hash |
| `artifacts/sb-research-safe-host-003/admin/index.html` | Still `12B52DBC551C5F987A6F5CEB69FECF5BC8D6D53FD2BB52FF34230F77388B9B6D` |
| Admin manifest | Still `0174F8E9EDB9B8C22C5D0F343D3D5127C342D9E753464A27F5AB9C945586514B` |
| `KIT_PI_TEST_CONNECTION` | Unset |

Untracked review files were left in place. No test or build was repeated.

## 2. Closed by evidence already on this commit

| Item | Evidence |
|---|---|
| Research-only registration and hidden trading controllers | `ResearchPublicationTests`, 16 passed, recorded in report 004 |
| Publisher | 7 passed, report 004 |
| API and admin bytes | Two publishes and 312 admin files matched, report 004. Hashes re-read this mission and unchanged |
| Reader has no database | `FileResearchPublicationReader` reads the configured directory and returns `MISSING` when it is empty |
| Runbook for the shared API and shared admin | `SB-RESEARCH-SAFE-HOST-DELIVERY-001.md` sections 6–8. Confirmed below. Not executed |

## 3. Prepared SQL, not executed

Do not run `deploy/ubuntu/migration-files.personal-intelligence.txt`. That list includes trading and exchange schema and migration 391, which inserts `personal_intelligence.exchange.connect` and `personal_intelligence.exchange.read`. Do not call `UpdateRolePermissionsAsync`. That method deletes every `role_permissions` row for the role, then inserts only codes that already exist, and still commits.

Preconditions, read-only, on the database the owner names:

```sql
SELECT to_regclass('public.permissions') AS permissions_table,
       to_regclass('public.role_permissions') AS role_permissions_table,
       to_regclass('public.platform_module_registry') AS registry_table;
```

All three must be non-null. `platform_module_registry.status` defaults to `1` in migration 051. `ListModulesAsync` only returns `status = 1`. Migration 390 does not set `status`, so the inserted row is visible to the entitlement API.

After a separate approval, and only on that named database:

```sql
-- File already in the repo: migrations/390_platform_module_personal_intelligence.sql
INSERT INTO platform_module_registry (module_code, module_name, description, verticals, sort_order)
SELECT
    'personal_intelligence',
    'Personal Intelligence',
    'Second Brain. Trading is the first domain adapter inside this module.',
    ARRAY['hybrid'],
    210
WHERE NOT EXISTS (
    SELECT 1 FROM platform_module_registry m WHERE m.module_code = 'personal_intelligence'
);

INSERT INTO public.permissions (permission_code, permission_name, module_name)
VALUES (
    'personal_intelligence.research.read',
    'Đọc publication Research',
    'personal_intelligence'
)
ON CONFLICT (permission_code) DO NOTHING;

-- Owner substitutes the tenant code and the role code.
-- This insert adds one row. It does not delete other permissions on the role.
INSERT INTO public.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM public.roles r
JOIN public.tenants t ON t.id = r.tenant_id
JOIN public.permissions p ON p.permission_code = 'personal_intelligence.research.read'
WHERE t.tenant_code = :tenant_code
  AND r.role_code = :role_code
  AND NOT EXISTS (
      SELECT 1 FROM public.role_permissions rp
      WHERE rp.role_id = r.id AND rp.permission_id = p.id
  );
```

`public.permissions.id` defaults to `gen_random_uuid()`. No fixed id is invented here. The role statement changes nothing until both names are supplied, and it changes nothing if the pair already exists.

This script does not update `tenants.settings`. Adding `personal_intelligence` to `allowed_modules` or `enabled_modules` is a separate owner edit. A partial JSON update could drop other modules, so it is not scripted.

## 4. Tenant entitlement

`IsModuleEnabledAsync` returns true only when `settings.platform.enabled_modules` contains `personal_intelligence`. There is no ADMIN bypass.

`UpdateTenantEntitlementAsync` is the supported write path:

- It requires the provisioning key when any tenant already exists.
- `NormalizeAllowedModules` drops a code that is not in `platform_module_registry` with `status = 1`. Migration 390 is what makes `personal_intelligence` acceptable. Without that row, an entitlement update ignores the code.
- `allowed_modules` is the ceiling. `enabled_modules` must be inside that ceiling. `SyncEnabledModules` copies the allowed list into the enabled list.
- The same call also runs workspace provisioning. It is not a settings-only edit.

The owner must supply:

- Tenant code.
- Whether `personal_intelligence` is added to `allowed_modules`, to `enabled_modules`, or to both.
- Confirmation that the rest of the current lists stay in place.
- Confirmation that the provisioning-key path is the one they want, knowing it also runs workspace provisioning.

No tenant was selected.

## 5. Access policy

Current policy `PersonalIntelligenceResearchRead` allows an authenticated user who is ADMIN, or who holds `personal_intelligence.research.read`, or who holds `personal_intelligence.trading.run`. Exchange permissions do not pass. The module filter still applies to ADMIN.

Least privilege for the new reader: the role receives only `personal_intelligence.research.read`. It does not receive `personal_intelligence.trading.run`, `personal_intelligence.trading.approve`, `personal_intelligence.exchange.read`, or `personal_intelligence.exchange.connect`. Do not grant `trading.run` in order to open Research. `trading.run` also satisfies the trading desk policy.

Leaving ADMIN and `trading.run` inside the research policy is the current code. Removing either one is a later product decision. This mission does not change the policy. Recommended acceptance for this release: keep the policy, and do not put `trading.run` on the read-only role.

## 6. Publication directory

Contract, already in the runbook, and not created anywhere:

- Owner name.
- Path outside every `root` in `deploy/ubuntu/nginx-kit-platform.conf`.
- `www-data` can traverse and read. The API process must not have write access.
- Files: `manifest.json`, `lab/LAB_RESULT.json`, `paper/PAPER_OPS_STATUS.json`, `paper/PAPER_RESEARCH_REPORT.json`, `paper/PAPER_OPS_HEALTH.json`.
- A separate backup copy. `publish_research_v1.py` deletes `.previous` after success, so `.previous` is not the rollback copy.
- Environment key `PersonalIntelligence__ResearchPublication__Root`, set only in the later `api.env` approval. An empty value returns `MISSING` and deletes nothing.

Status stays **OWNER_APPROVAL_REQUIRED**. No path was approved and no directory was created.

## 7. Schema and runtime

`narrative_json` stays **UNVERIFIED**. No approved database connection was available. The owner runs this and does not apply migration 384:

```sql
SELECT table_schema, table_name, column_name, data_type
FROM information_schema.columns
WHERE table_schema = 'pack_content'
  AND table_name = 'brand'
  AND column_name = 'narrative_json';
```

One row means the column is present. Zero rows means it is absent. The research reader does not select it. `ContentRepository` in the same shared API does.

Runtime login stays **UNVERIFIED**. Port 5290 is a different tree and was not called. No account was created.

To exercise this commit later, the owner supplies all of the following. This mission did not start the process.

- The DLL `artifacts/sb-research-safe-host-003/api/KitPlatform.Api.dll` with hash `8DB1EBC5B4B1109FA0A458BB1FA9291D5C6892FFF342608E0F0D9D03CCABF3A8`.
- A loopback port other than 5290.
- A non-production connection string. Do not load `/etc/kit-platform/api.env`.
- A publication directory the owner created, or an empty root when `MISSING` is the expected result.
- A user whose token contains only `personal_intelligence.research.read`, on a tenant whose `enabled_modules` contains `personal_intelligence`.

Then:

```text
ASPNETCORE_URLS=http://127.0.0.1:<owner-port>
PersonalIntelligence__ResearchPublication__Root=<owner-path>
dotnet exec artifacts/sb-research-safe-host-003/api/KitPlatform.Api.dll
```

Expected on that process: anonymous publication is 401, the read-only user receives 200, trading summary is 404, and a user without the research permission receives 403. The test host in `ResearchPublicationTests` already shows those results without a database. It is not a substitute for this login.

## 8. Runbook confirmation

`SB-RESEARCH-SAFE-HOST-DELIVERY-001.md` sections 6–8 already contain:

- Preflight stops, including a missing `narrative_json` confirmation and an unnamed backup directory.
- Input commit `6596cfb9` and the hashes re-read this mission.
- Backup of the API tree, the admin tree, and `api.env` with `cp -a`.
- The files the copy may change and the files it must not change, including appsettings, uploads, `api.env`, other SPA roots, nginx, the publication tree, and the database.
- Hash checks before any restart.
- Restart as a separate approval. The command is `systemctl restart kit-platform-api`. It was not run.
- Post-restart smoke tests and a stop that rolls back when health, anonymous Research, or trading summary fails.
- Rollback that restores the API tree, the admin tree, and `api.env`.

`deploy-update-vps.ps1` and `deploy-production.ps1` stay forbidden for this release. The procedure was not executed.

## 9. What the owner still has to provide

1. Named database for migration 390, or a decision to skip it.
2. Tenant code, and whether `personal_intelligence` is added to `allowed_modules`, `enabled_modules`, or both, without dropping the current modules.
3. Role code that receives only `personal_intelligence.research.read`.
4. Acceptance that ADMIN and `trading.run` remain able to read Research, with `trading.run` not granted to the read-only role.
5. Publication owner, path, and backup location.
6. Result of the `narrative_json` catalog query.
7. A non-production host and read-only user for the login in section 7.
8. Restart approval that names commit `6596cfb9` and DLL hash `8DB1EBC5B4B1109FA0A458BB1FA9291D5C6892FFF342608E0F0D9D03CCABF3A8`.

## 10. Checklist

| # | Gate | Status |
|---|---|---|
| 1 | Architecture conformance | PASS |
| 2 | Fixed revision, tracked tree clean | PASS |
| 3 | Research and publisher tests | PASS, report 004, not re-run |
| 4 | API Release and admin build | PASS, report 004, not re-run |
| 5 | Artifact hashes | PASS, re-read and unchanged |
| 6 | Host isolation in the test host | PASS, report 004 |
| 7 | Migration 390 and permission SQL | OWNER_APPROVAL_REQUIRED. Script is in section 3 and was not run |
| 8 | Tenant module lists | OWNER_APPROVAL_REQUIRED. No tenant chosen |
| 9 | Publication directory and owner | OWNER_APPROVAL_REQUIRED |
| 10 | `narrative_json` | UNVERIFIED |
| 11 | Runtime login | UNVERIFIED. Port 5290 was not used |
| 12 | ADMIN and `trading.run` policy | OWNER_APPROVAL_REQUIRED. Code unchanged. Least-privilege role is specified |
| 13 | Backup, copy rules, rollback, separate restart | PASS as a document. Not executed |
| 14 | Production deploy | Not done |

## 11. Production

No deploy, publish to a server, restart, migration, database write, permission change, tenant change, directory creation, credential change, or push was made.
