# SB-RESEARCH-SAFE-HOST-OWNER-DECISIONS-006

Date: 2026-10-22
Repository: `E:\KitPlatform-sb-research`
Branch: `admin-build-blocker-001`

Release gate: **OWNER_APPROVAL_REQUIRED**

This is not `DEPLOY-READY`. HEAD and the pinned artifacts match report 005. No source change required a new test or build. The runbook was re-read and not changed. Nothing was deployed.

## 1. Git and artifacts

| Item | Value |
|---|---|
| HEAD | `6596cfb996b3b4698917cb6f72b809931a42cc84` |
| Tracked diff | Empty |
| API DLL, both `sb-research-safe-host-003` and `sb-research-safe-host-004-repro` | `8DB1EBC5B4B1109FA0A458BB1FA9291D5C6892FFF342608E0F0D9D03CCABF3A8` |
| Admin `index.html` | `12B52DBC551C5F987A6F5CEB69FECF5BC8D6D53FD2BB52FF34230F77388B9B6D` |
| Admin manifest | `0174F8E9EDB9B8C22C5D0F343D3D5127C342D9E753464A27F5AB9C945586514B` |

Prior reviews stay untracked. This file is the only addition. Nothing was pushed.

## 2. Already verified

Report 004 recorded 16 research tests, 7 publisher tests, the Release publish, and the admin build. Report 005 prepared the permission SQL and confirmed the reader does not use a database. This mission re-read those hashes. They are unchanged.

Test-host success is not a browser login. A green build is not production readiness. Prepared SQL is not an applied migration or permission change.

## 3. Decisions to answer once

Reply with the eight values in section 8. No other code change is waiting.

### A. Research access policy

Recommended, and not implemented:

- The read-only account receives `personal_intelligence.research.read` only.
- Do not grant `personal_intelligence.trading.run` in order to open Research. That permission also opens the trading desk policy.
- ADMIN and `trading.run` currently pass `PersonalIntelligenceResearchRead`. Exchange permissions do not. The module filter still applies to ADMIN.
- Leave that policy as it is for this release. Removing ADMIN or `trading.run` is a later code change and is not required to create the read-only role.

### B. Tenant entitlement

No tenant was selected.

`IsModuleEnabledAsync` reads only `settings.platform.enabled_modules`. `allowed_modules` is the ceiling used by `UpdateTenantEntitlementAsync`. That API also requires the provisioning key when tenants exist, ignores a module that is not in `platform_module_registry` with `status = 1`, and runs workspace provisioning. Do not call it for this release until that side effect is explicitly accepted.

The owner names the tenant code and states, for `personal_intelligence` only, whether it is added to `allowed_modules`, `enabled_modules`, or both. The rest of each list stays as it is. This mission does not write tenant data.

### C. Permission and role

The SQL in section 4 matches `public.permissions` and `public.role_permissions` from migration 002. It inserts one permission and one role link. It does not delete existing role permissions. Do not call `UpdateRolePermissionsAsync`: that method deletes every current row for the role, then inserts only codes that already exist, and still commits.

No id, role, or production grant was created. The owner supplies `tenant_code` and `role_code`.

### D. Publication storage

The repository does not name a publication directory. The service user in `deploy/ubuntu/kit-platform-api.service` is `www-data`. The path must be outside every `root` in `deploy/ubuntu/nginx-kit-platform.conf` and outside `/var/www/kit-platform/api`, because that directory is the deploy target.

`www-data` needs directory traversal and read access. The API process must not have write access. The owner keeps a separate copy of the tree. `publish_research_v1.py` deletes `.previous` after success, so `.previous` is not the rollback copy. The key `PersonalIntelligence__ResearchPublication__Root` is set only in the later `api.env` edit. An empty root returns `MISSING` and deletes nothing.

No directory was created. This stays **OWNER_APPROVAL_REQUIRED** until the owner names the path and the owner.

### E. Runtime

**OWNER_INPUT_REQUIRED.** Port 5290 belongs to another tree and was not called. No read-only account exists.

A later non-production run needs the pinned DLL, a loopback port other than 5290, a non-production connection string, a publication directory or an empty root, and a user whose token has only `research.read` on a tenant whose `enabled_modules` contains `personal_intelligence`. Do not load `/etc/kit-platform/api.env`. The command shape is in report 005. It was not started.

### F. Schema

**UNVERIFIED.** Run this only on the database the owner names. Do not apply migration 384.

```sql
SELECT table_schema, table_name, column_name, data_type
FROM information_schema.columns
WHERE table_schema = 'pack_content'
  AND table_name = 'brand'
  AND column_name = 'narrative_json';
```

One row means the column is present. Zero rows means it is absent. The research reader does not select it. The shared API's content brand read does.

## 4. SQL prepared and not run

Do not run `deploy/ubuntu/migration-files.personal-intelligence.txt`. Do not insert the exchange permissions from migration 391.

```sql
SELECT to_regclass('public.permissions') AS permissions_table,
       to_regclass('public.role_permissions') AS role_permissions_table,
       to_regclass('public.platform_module_registry') AS registry_table;

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

The first statement is read-only. The inserts are the text of migration 390 plus a permission row whose id uses the table default `gen_random_uuid()`. The role insert adds one link and does not delete other links. None of it was executed. It does not change `tenants.settings`.

## 5. Runbook

`SB-RESEARCH-SAFE-HOST-DELIVERY-001.md` sections 6–8 already separate the pinned revision and hashes, the backup of the API tree, admin tree, and `api.env`, the copy rules, the preservation of appsettings, uploads, and `api.env`, the hash check before restart, the separate restart approval, the smoke tests, and the rollback that restores `api.env`. No gap required an edit. The procedure was not executed. `deploy-update-vps.ps1` remains forbidden for this release.

## 6. Final gate

| Gate | Status | How it closes |
|---|---|---|
| Revision and artifact hashes | PASS | Re-read this mission. Unchanged from 005 |
| Research and publisher tests, API and admin builds | PASS | Report 004. Not re-run. Not a production approval |
| Test-host isolation | PASS | Report 004. Not a browser login |
| Research policy for the new role | OWNER_APPROVAL_REQUIRED | Accept section 3.A, or request a later policy change |
| Tenant and module lists | OWNER_APPROVAL_REQUIRED | Name the tenant and both list changes. Do not call the entitlement API until its provisioning side effect is accepted |
| Permission SQL | OWNER_APPROVAL_REQUIRED | Approve section 4 for a named database, tenant, and role. Preparing the SQL does not apply it |
| Publication directory | OWNER_APPROVAL_REQUIRED | Name the owner, the path, and the backup copy |
| `narrative_json` | UNVERIFIED | Owner runs section 3.F on a named database |
| Login on this commit | OWNER_INPUT_REQUIRED | Owner supplies the host and the read-only user from section 3.E |
| Runbook, backup, rollback | PASS as a document | Not executed |
| Restart and production deploy | OWNER_APPROVAL_REQUIRED | A later approval must name `6596cfb9` and DLL hash `8DB1EBC5B4B1109FA0A458BB1FA9291D5C6892FFF342608E0F0D9D03CCABF3A8` |

`DEPLOY-READY` starts only after every `OWNER_APPROVAL_REQUIRED` and `UNVERIFIED` row above is closed with the owner's answers and, where a database or login is required, with the result of that check. This document does not close them.

## 7. One reply

1. Accept the current research policy, with the new role limited to `personal_intelligence.research.read`? Yes or no.
2. Tenant code.
3. Add `personal_intelligence` to `allowed_modules`, `enabled_modules`, or both? Confirm the other modules stay.
4. Accept the entitlement API's workspace-provisioning side effect, or leave tenant settings unchanged until a separate settings change is approved?
5. Database name for section 4, plus `role_code`.
6. Publication owner, absolute path, and backup location.
7. Result of the `narrative_json` query: one row or zero rows, and which database was read.
8. Restart approval for commit `6596cfb9` and DLL hash `8DB1EBC5B4B1109FA0A458BB1FA9291D5C6892FFF342608E0F0D9D03CCABF3A8`, or defer.

## 8. Production

No deploy, restart, migration, permission change, tenant change, directory creation, credential change, or push was made.
