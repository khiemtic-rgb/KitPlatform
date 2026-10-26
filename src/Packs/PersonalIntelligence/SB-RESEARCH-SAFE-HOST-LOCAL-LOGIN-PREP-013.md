# SB-RESEARCH-SAFE-HOST-LOCAL-LOGIN-PREP-013

Date: 2026-10-22
Repository: `E:\KitPlatform-sb-research`
Branch: `admin-build-blocker-001`
HEAD: `6596cfb996b3b4698917cb6f72b809931a42cc84`

No user was created. No password was chosen or hashed. The API was not started. Port 5290 was not called. Port 5432 was not opened.

## 1. Login, tenant, and permissions

| Step | Code | What it does |
|---|---|---|
| Login route | `AuthController.Login` | `POST /api/auth/login`. Anonymous. Body is `username`, `password`, and optional `tenantCode` |
| Tenant choice | `AuthService.ResolveTenantCode` | A non-empty `tenantCode` wins. If it is omitted, `Auth:DefaultTenantCode` is used, then `Assessment:EventTenantCode`, then `DEMO_PHARMACY` when the host is Development |
| User lookup | `AuthRepository.FindByCredentialsAsync` | Joins `users` to `tenants`, `user_roles`, `roles`, `role_permissions`, and `permissions`. Requires `users.status = 1`, `users.deleted_at` null, `tenants.status = 1`, and `tenants.deleted_at` null. It does not filter `roles.status` |
| Password check | `AuthService.LoginAsync` | `BCrypt.Net.BCrypt.Verify`. The project reference is `BCrypt.Net-Next` 4.0.3. A value that is not a bcrypt hash throws; that exception is not turned into 401 |
| Token | `JwtTokenService.CreateAccessToken` | Puts `sub`, `tenant_id`, `tenant_code`, one `role` claim per role code, and one `permission` claim per permission code |
| Login write | `IssueTokensAsync` | Inserts a refresh-token hash and sets `users.last_login_at`. A successful login is not read-only |
| Request permissions | `DbPermissionsClaimsTransformation` | Reloads that user's roles and permissions from the database. `CoreAuthority.HasPermission` accepts only a claim of type `permission` |
| Research policy | `PersonalIntelligenceAuthorizationExtensions` | Authenticated, and `ADMIN` or `personal_intelligence.research.read` or `personal_intelligence.trading.run`. Exchange permissions do not pass |
| Module gate | `PersonalIntelligenceModuleFilter` | Calls `IsModuleEnabledAsync`, which reads `settings.platform.enabled_modules` for the JWT tenant. `allowed_modules` is not the runtime gate. Failure is 403 with `Module is not enabled for this tenant.` |
| Research routes | `ResearchPublicationController` | Five GET routes under `/api/personal-intelligence/research`. The publication action returns the reader envelope |
| Empty publication | `FileResearchPublicationReader.Read` | A null, empty, or missing `PersonalIntelligence:ResearchPublication:Root` returns availability `MISSING` |
| Trading route | `TradingDeskController` | `GET /api/personal-intelligence/trading/summary` exists in source. `ResearchOnlyControllerConvention` removes every Personal Intelligence controller except `ResearchPublicationController`, so this host does not map that route |

`IdentityAdminService.CreateUserAsync` is not the account path. It calls `BCrypt.Net.BCrypt.HashPassword`, then also creates a Kit account and a `kit_account_memberships` row. The product code comes from `KitAccountRepository.MapProductCode` and the tenant vertical. `PI_A.business_vertical` was not read, so that product code is **UNVERIFIED**. The membership check allows only `family_os`, `pharmacy`, `clinic`, and `hybrid`. The login query does not need a Kit account: `pi-ui-run` and `pi-ui-approve` already log in without one.

`tools/HashPasswordTool` calls the same `BCrypt.HashPassword`, but it takes the password as a command argument. Do not use that form. The command line would retain the password.

## 2. Account to create after approval

One new `PI_A` user. Do not alter the users read in report 011.

| Field | Proposed value |
|---|---|
| Username | `pi-research-readonly` |
| Email | `pi-research-readonly@validation.local` |
| Tenant | `PI_A` only |
| Role | `PI_RESEARCH_READONLY` only |
| Status | 1 |
| `employee_id`, `kit_account_id` | null |
| Password | Chosen at execution, held only in that process, and not written into this report, git, or a file |

The password hash must be produced by `BCrypt.Net.BCrypt.HashPassword` from `BCrypt.Net-Next` 4.0.3, the same call as `IdentityAdminService`. Store only the hash in `users.password_hash`.

Before the insert, the same transaction rechecks `current_database()`, `inet_server_port()`, the active `PI_A` row, the absence of that username and email, and that `PI_RESEARCH_READONLY` on `PI_A` has exactly one permission, `personal_intelligence.research.read`. Then:

```sql
INSERT INTO public.users (tenant_id, username, email, password_hash, status)
SELECT t.id, 'pi-research-readonly', 'pi-research-readonly@validation.local', :hash, 1
FROM public.tenants t
WHERE t.tenant_code = 'PI_A' AND t.status = 1 AND t.deleted_at IS NULL;

INSERT INTO public.user_roles (user_id, role_id)
SELECT u.id, r.id
FROM public.users u
JOIN public.tenants t ON t.id = u.tenant_id AND t.tenant_code = 'PI_A'
JOIN public.roles r ON r.tenant_id = t.id AND r.role_code = 'PI_RESEARCH_READONLY'
WHERE u.username = 'pi-research-readonly';
```

Commit only when the new user has that single role and no `trading.run`, approval, or exchange permission. `PI_B` and existing users stay unchanged. Do not insert a Kit account or a membership.

## 3. Host launch after approval

Artifact, rechecked this session:

| Item | Value |
|---|---|
| DLL | `artifacts/sb-research-safe-host-003/api/KitPlatform.Api.dll` |
| SHA-256 | `8DB1EBC5B4B1109FA0A458BB1FA9291D5C6892FFF342608E0F0D9D03CCABF3A8` |
| `appsettings.json` and `appsettings.Development.json` | Present beside the DLL and byte-identical to the source files |

The source connection string uses port `5432` and a database other than `kit_pi_validation`. Development mode also sets `Assessment:EventTenantCode` to `NVX-CS08`. Those files must not decide the database, the JWT secret, or the tenant. No value from them is copied here.

`ASPNETCORE_ENVIRONMENT=Development` is required for this launch. Production validation rejects the JWT marker in the file and demands a production SMS setup. Development skips that check. Environment variables still override the files.

Set only these process variables. Do not load `/etc/kit-platform/api.env`. Do not set an exchange credential.

| Variable | Value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Development` |
| `ASPNETCORE_URLS` | `http://127.0.0.1:5088` |
| `ConnectionStrings__Default` | `Host=127.0.0.1;Port=5433;Database=kit_pi_validation;Username=pi_validation` |
| `Jwt__Secret` | A new value of at least 32 characters, present only in the process |
| `Jwt__Issuer` | `KitPlatform` |
| `Jwt__Audience` | `KitPlatform.Client` |
| `Auth__DefaultTenantCode` | A code confirmed absent from `tenants`, so an omitted `tenantCode` returns 401 |
| `PersonalIntelligence__ResearchPublication__Root` | Empty. Do not create a directory |
| `IntegrationOutbox__Enabled` | `false` |
| `PlatformEvents__Enabled` | `false` |
| `NotifyQueue__Enabled` | `false` |
| `Content__WorkerEnabled` | `false` |
| `FamilyOsReminder__Enabled` | `false` |
| `CustomerAppPush__Enabled` | `false` |
| `Assessment__BenchmarkAggregateEnabled` | `false` |

Port `5088` had no listener when this report was written. `5290` did. Recheck `5088` before binding. Do not use `5290`, `7224`, `5173`, `5174`, `5432`, or `5433`.

Start with `dotnet exec` of the pinned DLL. Its content root is the artifact API directory. Do not point the content root at `src/KitPlatform.Api`: from that directory the host also loads `../../.dev/national-drug.secrets.json` when the file exists. Whether the artifact-relative copy of that file exists was not opened and is **UNVERIFIED**. If it exists at launch, stop and do not print it.

`AddPersonalIntelligencePack` is not called, so `ExchangeBackfillWorker` is not registered. `LocalOsWatchWorker` has no off switch. About 15 seconds after start, when the Vietnam hour is 08:00 or later and it has not finished a scheduled run that day, it calls `RunAsync`. That can write. Whether `pack_local` exists in this database is **UNVERIFIED**. `KitSalesJourneyWorker` waits 10 minutes and then does nothing unless a Facebook page token is configured. Stop the process before that delay and as soon as the four requests are done.

## 4. Requests and expected results

Every login body includes `tenantCode`. Do not rely on the Development default.

1. `GET /api/personal-intelligence/research/publication` with no token. Expected: `401`. Authorization runs before the module filter.
2. Login as `pi-research-readonly` with `tenantCode` `PI_A`, then the same GET with that bearer token and an empty publication root. Expected: `200`, and `availability` is the string `MISSING`. The body also has `readError.code` `MISSING` and four object entries marked `MISSING`. That is the same MISSING result, not a different status.
3. The same `PI_A` token calls `GET /api/personal-intelligence/trading/summary`. Expected: `404`, because the trading controller is not mapped. This is not a trading-policy response.
4. A user whose tenant is `PI_B` calls the publication GET. Expected: `403`, because `PI_B.enabled_modules` does not contain `personal_intelligence`. `pi-b` and `pi-ui-b` have `trading.run`, which passes the research policy and then stops at the module filter. Their passwords were not read. `BCrypt.Verify` cannot use the non-bcrypt placeholder stored by the integration tests. The owner supplies an existing `PI_B` password at execution time. That login also updates `last_login_at` and inserts a refresh token for that existing user. This plan does not create a `PI_B` user.

No criterion above is changed.

## 5. Approval still required

1. Username `pi-research-readonly` and email `pi-research-readonly@validation.local`.
2. The insert transaction in section 2, including a bcrypt hash and no Kit account.
3. Login writes: refresh-token hash and `last_login_at` for the new user, and the same writes for the existing `PI_B` user used in test 4.
4. The `PI_B` password is supplied at execution and is not read from a stored credential file.
5. Start the pinned DLL on `127.0.0.1:5088` with the environment in section 3, accepting that `LocalOsWatchWorker` cannot be disabled.
6. Stop the process after the four requests.

Nothing in this session was written, started, deployed, committed, or pushed.
