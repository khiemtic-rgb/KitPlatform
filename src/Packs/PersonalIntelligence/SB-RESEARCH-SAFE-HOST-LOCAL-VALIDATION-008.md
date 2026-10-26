# SB-RESEARCH-SAFE-HOST-LOCAL-VALIDATION-008

Date: 2026-10-22
Repository: `E:\KitPlatform-sb-research`
HEAD: `6596cfb996b3b4698917cb6f72b809931a42cc84`

Preflight result: **STOPPED**

`127.0.0.1:5433` has no listener. The database identity was not proven, so no SQL was sent. Production was not contacted. Port 5290 was not called.

## 1. Git and artifacts

| Item | Result |
|---|---|
| Branch | `admin-build-blocker-001` |
| HEAD | `6596cfb996b3b4698917cb6f72b809931a42cc84` |
| Tracked diff | Empty |
| API DLL, `sb-research-safe-host-003` and `sb-research-safe-host-004-repro` | `8DB1EBC5B4B1109FA0A458BB1FA9291D5C6892FFF342608E0F0D9D03CCABF3A8` |
| Admin `index.html` | `12B52DBC551C5F987A6F5CEB69FECF5BC8D6D53FD2BB52FF34230F77388B9B6D` |
| Admin manifest | `0174F8E9EDB9B8C22C5D0F343D3D5127C342D9E753464A27F5AB9C945586514B` |

Untracked review files were left in place. Nothing was pushed.

## 2. Database identity

| Check | Result |
|---|---|
| `Test-NetConnection 127.0.0.1:5433` | TcpTestSucceeded false |
| TCP connect to `127.0.0.1:5433`, 2 second wait | Timeout |
| Listener on local port 5433 | None |
| `psql` present at `C:\Program Files\PostgreSQL\18\bin\psql.exe` | Yes. It was not run |

`current_database()` and `inet_server_port()` were not read. Migration 390, tenants `PI_A` and `PI_B`, module lists, and role `PI_RESEARCH_READONLY` are therefore **UNVERIFIED**. No credential was read.

## 3. SQL prepared and not run

This text is the intended change after a later preflight proves `current_database() = kit_pi_validation` and `inet_server_port() = 5433`. It was not executed. The duplicate check for `PI_RESEARCH_READONLY` was not possible, so that code is only a proposal.

Do not run `deploy/ubuntu/migration-files.personal-intelligence.txt`. Do not call `UpdateTenantEntitlementAsync`. Do not grant Research to `PI_B`. Do not grant `trading.run`.

```sql
-- Identity gate. Stop unless both values match.
SELECT current_database() AS database_name, inet_server_port() AS server_port;

-- Read before any write. Record the rows. Do not print password hashes.
SELECT tenant_code,
       settings->'platform'->'allowed_modules' AS allowed_modules,
       settings->'platform'->'enabled_modules' AS enabled_modules
FROM public.tenants
WHERE tenant_code IN ('PI_A', 'PI_B') AND deleted_at IS NULL;

SELECT module_code FROM platform_module_registry
WHERE module_code = 'personal_intelligence';

SELECT role_code FROM public.roles r
JOIN public.tenants t ON t.id = r.tenant_id
WHERE t.tenant_code = 'PI_A' AND r.role_code = 'PI_RESEARCH_READONLY';

SELECT permission_code FROM public.permissions
WHERE permission_code = 'personal_intelligence.research.read';
```

Writes, only after those reads and a separate approval. Each statement is additive.

| Statement | Effect |
|---|---|
| Registry insert from migration 390 | Inserts `personal_intelligence` only when that `module_code` is absent. Does not change tenant settings |
| Permission insert | Inserts `personal_intelligence.research.read` only when that code is absent. Id uses `gen_random_uuid()`. Does not attach it to a role |
| Role insert | Inserts role code `PI_RESEARCH_READONLY` on `PI_A` only when that tenant and code pair is absent. Does not change `PI_B` |
| Role-permission insert | Adds one link from that role to that permission when the link is absent. Does not delete other permissions |
| Module append on `PI_A` | Adds `personal_intelligence` to `allowed_modules` and `enabled_modules` with `\|\|` only when the code is not already present. Other codes stay. `PI_B` is not updated |

```sql
INSERT INTO platform_module_registry (module_code, module_name, description, verticals, sort_order)
SELECT 'personal_intelligence', 'Personal Intelligence',
       'Second Brain. Trading is the first domain adapter inside this module.',
       ARRAY['hybrid'], 210
WHERE NOT EXISTS (
    SELECT 1 FROM platform_module_registry m WHERE m.module_code = 'personal_intelligence'
);

INSERT INTO public.permissions (permission_code, permission_name, module_name)
VALUES ('personal_intelligence.research.read', 'Đọc publication Research', 'personal_intelligence')
ON CONFLICT (permission_code) DO NOTHING;

INSERT INTO public.roles (tenant_id, role_code, role_name)
SELECT t.id, 'PI_RESEARCH_READONLY', 'PI research read only'
FROM public.tenants t
WHERE t.tenant_code = 'PI_A' AND t.deleted_at IS NULL
  AND NOT EXISTS (
      SELECT 1 FROM public.roles r
      WHERE r.tenant_id = t.id AND r.role_code = 'PI_RESEARCH_READONLY'
  );

INSERT INTO public.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM public.roles r
JOIN public.tenants t ON t.id = r.tenant_id AND t.tenant_code = 'PI_A'
JOIN public.permissions p ON p.permission_code = 'personal_intelligence.research.read'
WHERE r.role_code = 'PI_RESEARCH_READONLY'
  AND NOT EXISTS (
      SELECT 1 FROM public.role_permissions rp
      WHERE rp.role_id = r.id AND rp.permission_id = p.id
  );

UPDATE public.tenants t
SET settings = jsonb_set(
        jsonb_set(
            COALESCE(t.settings, '{}'::jsonb),
            '{platform,allowed_modules}',
            COALESCE(t.settings->'platform'->'allowed_modules', '[]'::jsonb)
                || '["personal_intelligence"]'::jsonb,
            true),
        '{platform,enabled_modules}',
        COALESCE(t.settings->'platform'->'enabled_modules', '[]'::jsonb)
            || '["personal_intelligence"]'::jsonb,
        true)
WHERE t.tenant_code = 'PI_A'
  AND t.deleted_at IS NULL
  AND NOT (
      COALESCE(t.settings->'platform'->'allowed_modules', '[]'::jsonb) ? 'personal_intelligence'
      AND COALESCE(t.settings->'platform'->'enabled_modules', '[]'::jsonb) ? 'personal_intelligence'
  );
```

The module update is the statement that needs a live before-image. If `allowed_modules` is null, this creates an array that contains only `personal_intelligence` for that key. That is an append from empty, not a deletion of other keys. If the live row already has other modules, those elements remain because `||` concatenates. `PI_B` has no matching `WHERE`.

No user row is included. A login still needs an approved user attached to `PI_RESEARCH_READONLY` and not to `PI_TRADING`, `PI_APPROVE`, or `PI_EXCHANGE`.

## 4. Approval still required

1. Start or expose `kit_pi_validation` on `127.0.0.1:5433` and show `current_database()` plus `inet_server_port()` before any write.
2. Re-read the four read-only queries and confirm `PI_RESEARCH_READONLY` is absent.
3. Approve the five write statements for that database only.
4. Approve creation of one user on `PI_A` with only that role. This mission does not define the username or a password.
5. Leave the publication root empty for the first login and expect `MISSING`.
6. Run `dotnet exec artifacts/sb-research-safe-host-003/api/KitPlatform.Api.dll` from this commit, on a loopback port other than 5290, 7224, 5173, 5174, 5432, and 5433. Do not load `/etc/kit-platform/api.env`.

Expected after that process starts: anonymous publication is 401, the read-only user on `PI_A` is 200 with `MISSING`, the same user against trading summary is 404, and `PI_B` with the module off is 403.

## 5. Remaining blockers

| Blocker | State |
|---|---|
| Database `kit_pi_validation` on port 5433 | BLOCKED. No listener. Identity not proven |
| Migration 390, `PI_A`, `PI_B`, module lists, role collision | UNVERIFIED |
| Writes in section 3 | Not run. Waiting on identity and approval |
| Login user | OWNER_APPROVAL_REQUIRED |
| `narrative_json` on the Content database | UNVERIFIED. Not required for the research reader |
| Publication directory for a `FRESH` read | OWNER_APPROVAL_REQUIRED. No path is defined |
| Production restart | Not requested and not done |

## 6. Production

No database write, migration, permission grant, tenant change, directory creation, deploy, restart, or push was made.
