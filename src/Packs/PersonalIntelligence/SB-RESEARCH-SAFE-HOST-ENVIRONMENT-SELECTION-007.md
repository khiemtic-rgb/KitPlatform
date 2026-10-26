# SB-RESEARCH-SAFE-HOST-ENVIRONMENT-SELECTION-007

Date: 2026-10-22
Repository: `E:\KitPlatform-sb-research`
HEAD: `6596cfb996b3b4698917cb6f72b809931a42cc84`
Tracked diff: empty

Conclusion: **READY_FOR_OWNER_DECISION**

One non-production plan is supported by the repository. It is not approved, and it was not executed. Production was not changed. Tests and builds were not repeated.

## 1. Evidence

| Fact | Where it is defined | What it is not |
|---|---|---|
| Production database name `novixa_prod` | `docs/novixa/05-operations/deployment-model-v1.md` | Not a test database |
| Live pharmacy tenant `NT_XUANHOA` | `docs/novixa/06-compliance/pharmacy-audit-handoff-demo-pharmacy-v1.md` | Must not be used |
| Pharmacy audit tenant `DEMO_PHARMACY`, users `admin` and `pharmacist`, role `ADMIN` plus `STAFF` | `migrations/237_demo_pharmacy_audit_handoff.sql` | `ADMIN` is granted every permission row. Not a research read-only role |
| Isolated PI database `kit_pi_validation` on `127.0.0.1` port `5433` | `tests/KitPlatform.PersonalIntelligence.Tests/PostgresIntegrationTests.cs` refuses any other database name and refuses port `5432`. `scripts/second-brain-trading-ui-start.ps1` raises an exception unless the database and port match | Presence on this machine was not checked. `KIT_PI_TEST_CONNECTION` is unset |
| Tenants `PI_A` / `Validation A` and `PI_B` / `Validation B` | Same test file inserts them | `PI_B` is the second tenant, not the read tenant |
| Roles `PI_TRADING`, `PI_APPROVE` | `PostgresIntegrationTests.cs` | Trading roles |
| Role `PI_EXCHANGE` and users `pi-ui-approve`, `pi-ui-run`, `pi-ui-b` | `scripts/second-brain-trading-ui-start.ps1` | Exchange and trading accounts. That script sets `Root` to `E:\KitPlatform`, builds that tree, and writes an exchange key. It is not the runner for commit `6596cfb9` |
| Module gate | `IsModuleEnabledAsync` reads `settings.platform.enabled_modules` only | `allowed_modules` is the ceiling used by the entitlement update |
| Entitlement side effect | `WorkspacePackProvisioner.EnsurePacksForTenantAsync` always executes `kit_provision_pack_workspace(..., 'novixa_pharmacy')`, then other packs only if their module codes are enabled | Do not call `UpdateTenantEntitlementAsync` until this pharmacy provision is accepted |
| Registry | Migration 390 inserts `personal_intelligence` only when missing. `status` defaults to `1`. `ListModulesAsync` returns `status = 1` | 390 was not applied |
| Publication root | `PersonalIntelligence__ResearchPublication__Root`. Empty or missing directory returns `MISSING`. No path is set in the three appsettings files or in `kit-platform-api.service` | No production path exists to recommend |
| Process on port 5290 | Report 004: `KitPlatform.Api.exe` from another tree | Not called |

No production query was run. No credential file was read.

## 2. The one plan

Use the local validation database `kit_pi_validation` on `127.0.0.1:5433`, tenant `PI_A`, and a new role that is not `PI_TRADING`, `PI_APPROVE`, or `PI_EXCHANGE`.

`DEMO_PHARMACY` is the wrong tenant for this proof. It is the pharmacy audit tenant, and its `ADMIN` role is attached to every permission. `NT_XUANHOA` and `novixa_prod` stay out. The trading-ui start script stays out because it builds `E:\KitPlatform` and starts trading accounts.

| Choice | Recommendation | Why |
|---|---|---|
| Database | `kit_pi_validation` at `127.0.0.1:5433` only | The PI tests and the trading-ui script both refuse every other name and refuse port `5432` |
| Tenant for the allowed login | `PI_A` | Already defined as Validation A |
| Tenant for the module-off check | `PI_B` | Already defined as Validation B. Do not give it the read permission |
| Role | Not one of the three existing PI roles | Those roles are trading or exchange. The repository has no research read-only role code, so this mission does not invent one. The owner supplies the code |
| Permission | `personal_intelligence.research.read` only | Insert from report 006. Do not grant `trading.run` |
| Module lists | Append `personal_intelligence` to the current `enabled_modules` on `PI_A` only if it is absent. Append it to `allowed_modules` only if that array already exists and does not contain it. Leave every other code in place | The runtime check is `enabled_modules`. A partial JSON replace could drop other modules, so the current arrays must be read before any write |
| Entitlement API | Do not call it for this plan | It always provisions the `novixa_pharmacy` workspace |
| Publication path for the first login | Leave `PersonalIntelligence__ResearchPublication__Root` empty | The reader then returns `MISSING`. No directory is created. A real path is a later owner decision and is not named anywhere in configuration |
| Host | `dotnet exec` of `artifacts/sb-research-safe-host-003/api/KitPlatform.Api.dll`, hash `8DB1EBC5B4B1109FA0A458BB1FA9291D5C6892FFF342608E0F0D9D03CCABF3A8`, on a loopback port other than `5290`, `7224`, `5173`, `5174`, `5432`, and `5433` | Those ports are the ones the trading-ui script already treats as occupied or protected. Do not load `/etc/kit-platform/api.env`. Do not set exchange credential variables |

The database and the two tenants are defined in source. This mission did not connect, so their presence on this machine remains unverified.

## 3. Unverified dependencies

- `kit_pi_validation` is listening on `127.0.0.1:5433`. Not checked.
- Current `allowed_modules` and `enabled_modules` for `PI_A` and `PI_B`. Not read.
- Whether migration 390 has already been applied there. Not read.
- `pack_content.brand.narrative_json` on the Content database used by the shared API. The research reader does not need it. The shared production API does. Still unverified, and not queried.
- A user whose token contains only `personal_intelligence.research.read`. No such user is defined.
- The role code the owner will choose.

## 4. Approval required before any action

1. Confirm the login database is `kit_pi_validation` on `127.0.0.1:5433`, not `novixa_prod`.
2. Supply the new role code on `PI_A`. It must not be `PI_TRADING`, `PI_APPROVE`, or `PI_EXCHANGE`.
3. Approve the permission insert and the single `role_permissions` insert from report 006, aimed at that role. That insert does not delete other permissions.
4. Approve a read of `PI_A` and `PI_B` module arrays, then an append of `personal_intelligence` to `PI_A.enabled_modules` without removing other codes. Approve the same append to `allowed_modules` only when that array already exists.
5. Accept that `UpdateTenantEntitlementAsync` will not be used, because it provisions `novixa_pharmacy`.
6. Accept an empty publication root for the first login, expecting `MISSING`.
7. Accept that ADMIN and `trading.run` still pass the research policy. Do not grant `trading.run` to this role.
8. After that login passes, a separate approval is still required for the publication path, the `narrative_json` query on the Content database, and any restart of `kit-platform-api`.

## 5. Checklist

| Step | Status |
|---|---|
| Commit `6596cfb9` and pinned hashes | PASS, re-read, not rebuilt |
| Research test host, publisher tests, admin build | PASS in report 004. Not a login |
| Choose `kit_pi_validation` / `PI_A` and refuse production tenants | READY_FOR_OWNER_DECISION |
| Owner supplies the new role code | OWNER_APPROVAL_REQUIRED |
| Apply 390, the permission insert, and the module append on that database only | OWNER_APPROVAL_REQUIRED. Not done |
| Login on the pinned DLL, empty publication root, port other than 5290 | BLOCKED until the rows above exist |
| `narrative_json` on the Content database | UNVERIFIED. Not required for the research reader |
| Publication directory for a `FRESH` read | OWNER_APPROVAL_REQUIRED. No path is defined |
| Production copy, restart, rollback | PASS as a document in the delivery runbook. Not executed |

## 6. Production

No deploy, restart, migration, database write, permission grant, tenant change, publication directory, entitlement API call, or push was made. Port 5290 was not called.
