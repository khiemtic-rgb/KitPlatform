# SB-RESEARCH-SAFE-HOST-LOCAL-VALIDATION-012

Date: 2026-10-22
Repository: `E:\KitPlatform-sb-research`
Branch: `admin-build-blocker-001`
HEAD: `6596cfb996b3b4698917cb6f72b809931a42cc84`

The three approved inserts were committed on `kit_pi_validation` at `127.0.0.1:5433`. No user, tenant setting, migration, or API change was made.

## 1. Checks before the write

Connection role: `pi_validation`.

| Check | Result |
|---|---|
| `current_database()` | `kit_pi_validation` |
| `inet_server_port()` | `5433` |
| `PI_A` | One row, `status = 1`, `deleted_at` null |
| `personal_intelligence.research.read` | Absent |
| `PI_RESEARCH_READONLY` | Absent on every tenant |
| `uq_permissions_code` | `UNIQUE (permission_code)` |
| `uq_roles_tenant_code` | `UNIQUE (tenant_id, role_code)` |
| `uq_role_permissions` | `UNIQUE (role_id, permission_id)` |
| `roles_tenant_id_fkey` | `tenant_id` references `tenants(id)` |
| `role_permissions_role_id_fkey` | `role_id` references `roles(id)` on delete cascade |
| `role_permissions_permission_id_fkey` | `permission_id` references `permissions(id)` on delete cascade |
| `trg_roles_updated` | `BEFORE UPDATE` only, calls `set_updated_at()`. An insert does not fire it |

Before-image:

| Item | Value |
|---|---|
| Permissions / roles / role-permission links | 4 / 5 / 6 |
| Users / user-role links / tenants | 7 / 10 / 2 |
| Existing link hash | `9af86ef456ab91bfa0577ee1ade5ef0c` |
| `PI_A.enabled_modules` | `["personal_intelligence"]` |
| `PI_A.allowed_modules` | key absent |
| `PI_B.enabled_modules` | `["inventory", "procurement", "sales", "loyalty", "customer_app", "medication", "health_wallet", "reservations", "reports"]` |
| `PI_B.allowed_modules` | key absent |

This matches report 011. The write proceeded.

## 2. Transaction

One transaction. The block checked the identity, `PI_A`, the missing permission, the missing role, and both module arrays again, then ran the three inserts. It rolled the transaction back by raising an exception unless each insert affected one row and the after-image matched. The exception was not raised.

| Statement | Rows inserted |
|---|---|
| Permission `personal_intelligence.research.read` | 1 |
| Role `PI_RESEARCH_READONLY` on `PI_A` | 1 |
| One `role_permissions` link | 1 |

`BEGIN` completed, the block completed, and `COMMIT` completed. Exit code 0. Nothing was rolled back.

## 3. Read-back after commit

Same endpoint. `current_database()` is still `kit_pi_validation` and `inet_server_port()` is still `5433`.

| Check | Result |
|---|---|
| Permission rows for `personal_intelligence.research.read` | 1 |
| Permission name and module | Match the inserted values. Module is `personal_intelligence` |
| Role | One row, tenant `PI_A`, `status = 1` |
| Permissions on that role | Only `personal_intelligence.research.read` |
| `trading.run`, approval, or exchange permission on that role | None |
| Users attached to that role | 0 |
| `PI_B` roles | Still `PI_APPROVE` and `PI_TRADING` only |
| `PI_A.enabled_modules` | Still `["personal_intelligence"]` |
| `PI_B.enabled_modules` | Still the nine-module list above |
| `allowed_modules` on `PI_A` and `PI_B` | Still absent |
| Permissions / roles / links | 5 / 6 / 7 |
| Users / user-role links / tenants | 7 / 10 / 2 |
| Hash of the six links that already existed | `9af86ef456ab91bfa0577ee1ade5ef0c` |

## 4. Left unchanged

No user row was created or edited. No password, email, or token was written. Tenant settings were not updated. `PI_B` was not updated. No migration, seed, or DDL ran. `UpdateRolePermissionsAsync` and `UpdateTenantEntitlementAsync` were not called. The API was not started. Port 5432 was not opened. Nothing was deployed, committed, or pushed.

## 5. Status

The read-only Research permission and the `PI_A` role exist, and they are linked only to each other. A login user does not exist yet.

Waiting for a separate approval before creating an account or starting an API test.
