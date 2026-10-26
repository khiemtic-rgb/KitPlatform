# SB-RESEARCH-SAFE-HOST-LOCAL-VALIDATION-011

Date: 2026-10-22
Repository: `E:\KitPlatform-sb-research`
Branch: `admin-build-blocker-001`
HEAD: `6596cfb996b3b4698917cb6f72b809931a42cc84`

Read only. No row was inserted, updated, or deleted. No API was started. Port 5432 was not opened.

## 1. Identity

Connected to `127.0.0.1:5433`, database `kit_pi_validation`, role `pi_validation`.

| current_database | inet_server_port |
|---|---|
| `kit_pi_validation` | `5433` |

The values match. The reads below used that same connection.

## 2. What was read

### Migration 390

`public.kit_schema_migrations` does not exist. No relation whose name contains `migration` exists in any schema. The repository ledger is that table (`migrations/098_schema_migrations.sql`). File `390_platform_module_personal_intelligence.sql` is therefore **not recorded as applied**.

The registry effect of that file is present. How the row was inserted is **UNVERIFIED**.

### Module registry

| module_code | module_name | status | sort_order | verticals | description matches file 390 |
|---|---|---|---|---|---|
| `personal_intelligence` | Personal Intelligence | 1 | 210 | `{hybrid}` | yes |

`ListModulesAsync` keeps rows with `status = 1`, so this row is visible. Columns match migration 051 plus the 390 insert. No extra required column.

### Tenants

Both rows are `status = 1` and `deleted_at` is null. `settings` and `settings.platform` are JSON objects. Neither tenant has an `allowed_modules` key.

| Tenant | platform keys | enabled_modules | allowed_modules |
|---|---|---|---|
| `PI_A` | `enabled_modules` | `["personal_intelligence"]` | key absent |
| `PI_B` | `enabled_modules`, `features`, `i18n`, `schema_version`, `vertical` | `["inventory", "procurement", "sales", "loyalty", "customer_app", "medication", "health_wallet", "reservations", "reports"]` | key absent |

`PI_A` also has the settings key `allow_negative_stock`. Values under `PI_B` keys other than `enabled_modules` were not read.

`IsModuleEnabledAsync` reads `enabled_modules` only. On `PI_A` the module is already on. On `PI_B` it is off. When `allowed_modules` is absent, the settings parser uses the enabled array as the ceiling. It does not use the pharmacy default list once `enabled_modules` is present.

### Role, permission, and links

`PI_RESEARCH_READONLY` is absent on `PI_A` and on `PI_B`.

`personal_intelligence.research.read` is absent. These four codes exist:

- `personal_intelligence.exchange.connect`
- `personal_intelligence.exchange.read`
- `personal_intelligence.trading.approve`
- `personal_intelligence.trading.run`

| Tenant | Role | Permissions |
|---|---|---|
| `PI_A` | `PI_APPROVE` | `personal_intelligence.trading.approve` |
| `PI_A` | `PI_EXCHANGE` | `personal_intelligence.exchange.connect`, `personal_intelligence.exchange.read` |
| `PI_A` | `PI_TRADING` | `personal_intelligence.trading.run` |
| `PI_B` | `PI_APPROVE` | `personal_intelligence.trading.approve` |
| `PI_B` | `PI_TRADING` | `personal_intelligence.trading.run` |

All five roles have `status = 1`.

### Users and how a login attaches

Active users, with role codes only:

| Tenant | Username | Roles |
|---|---|---|
| `PI_A` | `pi-a` | `PI_APPROVE`, `PI_TRADING` |
| `PI_A` | `pi-a-noperm` | none |
| `PI_A` | `pi-a-runonly` | `PI_TRADING` |
| `PI_A` | `pi-ui-approve` | `PI_APPROVE`, `PI_EXCHANGE`, `PI_TRADING` |
| `PI_A` | `pi-ui-run` | `PI_TRADING` |
| `PI_B` | `pi-b` | `PI_APPROVE`, `PI_TRADING` |
| `PI_B` | `pi-ui-b` | `PI_TRADING` |

No password hash, email, or token was selected.

A role is attached through `public.user_roles (user_id, role_id)`, unique on that pair. `public.users` requires `tenant_id`, `username`, `email`, and `password_hash`. `employee_id` and `kit_account_id` are nullable. Username and email are unique per tenant.

`pi-a` and `pi-b` are the only two of these users with a kit account. `kit_account_memberships.product_code` allows only `family_os`, `pharmacy`, `clinic`, and `hybrid`. That table is not the research-role link. The UI accounts `pi-ui-approve`, `pi-ui-run`, and `pi-ui-b` have no kit account, so a `users` row plus one `user_roles` row is the existing shape.

`UpdateRolePermissionsAsync` deletes every `role_permissions` row for the role, then inserts the supplied codes. Do not use it. `UpdateTenantEntitlementAsync` is not used.

## 3. What would make the report 008 writes fail or change data

| Report 008 statement | Live result |
|---|---|
| Registry insert | Does not fail. The `NOT EXISTS` guard matches the existing row, so it would insert nothing. It is not needed |
| Permission insert | The code is absent. `uq_permissions_code` exists, so `ON CONFLICT (permission_code)` is valid. `permission_code` is `varchar(100)` and `module_name` is `varchar(50)`; the new values fit |
| Role insert | `PI_A` is one active row. The code is absent. `uq_roles_tenant_code` is `(tenant_id, role_code)`. `status` defaults to 1 |
| Role-permission insert | Parents are absent today, so this must run after the two inserts. `uq_role_permissions` is `(role_id, permission_id)`. The `NOT EXISTS` guard does not delete other links. The join limits the role to `PI_A` |
| Tenant update | This is the statement to replace. `allowed_modules` is absent and `enabled_modules` already contains `personal_intelligence`, so the `NOT (both contain the code)` guard still matches `PI_A`. The statement would append a second `personal_intelligence` onto `enabled_modules` and create `allowed_modules` as `["personal_intelligence"]`. It would not touch `PI_B` |

No row-level security is enabled on these tables. Role `pi_validation` has insert and update on them. That is why this mission stopped at reads.

## 4. Minimal change, after approval

Do not run the registry insert. Do not run the report 008 tenant update. Do not update `PI_B`. Do not grant `trading.run`.

### Permission

Before: no row for `personal_intelligence.research.read`.
After: one row, not attached to any existing role.

```sql
INSERT INTO public.permissions (permission_code, permission_name, module_name)
VALUES ('personal_intelligence.research.read', 'Đọc publication Research', 'personal_intelligence')
ON CONFLICT (permission_code) DO NOTHING;
```

### Role on PI_A only

Before: no `PI_RESEARCH_READONLY`.
After: one role on `PI_A`, status 1, no permissions until the next statement. `PI_B` stays unchanged.

```sql
INSERT INTO public.roles (tenant_id, role_code, role_name)
SELECT t.id, 'PI_RESEARCH_READONLY', 'PI research read only'
FROM public.tenants t
WHERE t.tenant_code = 'PI_A'
  AND t.deleted_at IS NULL
  AND t.status = 1
  AND NOT EXISTS (
      SELECT 1 FROM public.roles r
      WHERE r.tenant_id = t.id AND r.role_code = 'PI_RESEARCH_READONLY'
  );
```

### One link

Before: no link.
After: that new role has only `personal_intelligence.research.read`.

```sql
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
```

### Tenant settings

Recommended: leave both tenants unchanged. `PI_A.enabled_modules` is already `["personal_intelligence"]`, which is what the module filter reads. Creating `allowed_modules` is not required for that filter.

If the ceiling key must be stored, the only safe write copies the enabled array that was read. It does not append to `enabled_modules`. It matches zero rows if that array has changed.

Before, if approved: `PI_A` has no `allowed_modules`; `enabled_modules` stays `["personal_intelligence"]`.
After: `allowed_modules` is that same array. `enabled_modules`, `allow_negative_stock`, and `PI_B` stay as read.

```sql
UPDATE public.tenants t
SET settings = jsonb_set(
        t.settings,
        '{platform,allowed_modules}',
        t.settings->'platform'->'enabled_modules',
        true)
WHERE t.tenant_code = 'PI_A'
  AND t.deleted_at IS NULL
  AND t.status = 1
  AND NOT (t.settings->'platform' ? 'allowed_modules')
  AND t.settings->'platform'->'enabled_modules' = '["personal_intelligence"]'::jsonb;
```

### User

No user statement is ready. Every current `PI_A` account already carries `trading.run`, carries approval or exchange permissions, or is the no-permission fixture `pi-a-noperm`. Reusing one would change an existing account. `PI_B` accounts stay without research access.

A later approval still has to name the username. The insert then needs `tenant_id` of `PI_A`, `username`, `email`, and `password_hash`, plus one `user_roles` row pointing only at `PI_RESEARCH_READONLY`. No password is chosen here.

## 5. Risks and the next test

- Running report 008's tenant update as written duplicates `personal_intelligence` inside `PI_A.enabled_modules`.
- The three inserts above do not by themselves create a login.
- `pi_validation` can write these tables. The next session has to run only the approved statements, then re-read.
- If `PI_A.enabled_modules` changes before an approved ceiling update, the guarded statement matches zero rows and must be read again.
- Publication root, host port, and the login user are still not chosen.

After approval, the next test is: run only the approved statements, then read back the permission, the `PI_A` role, its single permission code, and both tenants' module arrays. `PI_B` and the existing users must be unchanged. Do not start the API in that same step.

## 6. Unverified

| Item | Why |
|---|---|
| Which statement inserted the registry row | The migration ledger table is absent |
| Password, email, and token values | Not selected |
| Login behavior of a new user | No user was created and the API was not started |
| Publication root | Not a row in this database |

## 7. Confirmation

No insert, update, delete, migration, seed, or DDL was run. The Windows service was not changed. Nothing was deployed, committed, or pushed.

Status: waiting for approval before any write.
