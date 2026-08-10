using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Auth;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyCommercialService : IFamilyCommercialService
{
    private readonly IDbConnectionFactory _db;
    private readonly IAuthService _auth;
    private readonly IKitAccountService _kitAccounts;
    private readonly ITenantContext _tenant;
    private readonly FamilyOsBillingOptions _billing;
    private readonly IFamilyWriteAccessService _writeAccess;

    public FamilyCommercialService(
        IDbConnectionFactory db,
        IAuthService auth,
        IKitAccountService kitAccounts,
        ITenantContext tenant,
        Microsoft.Extensions.Options.IOptions<FamilyOsBillingOptions> billing,
        IFamilyWriteAccessService writeAccess)
    {
        _db = db;
        _auth = auth;
        _kitAccounts = kitAccounts;
        _tenant = tenant;
        _billing = billing.Value;
        _writeAccess = writeAccess;
    }

    private int TrialDays =>
        _billing.TrialDays > 0 ? _billing.TrialDays : 30;

    /// <summary>Soft Pro entitlement after trialEndsAt before Free (matches past_due grace).</summary>
    private const int TrialGraceDays = 3;

    public async Task<FamilyRegisterResponse> RegisterAsync(
        FamilyRegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var familyName = Require(request.FamilyName, "Tên gia đình");
        var parentName = Require(request.ParentDisplayName, "Tên phụ huynh");
        var username = Require(request.Username, "Tài khoản").ToLowerInvariant();
        var email = Require(request.Email, "Email").ToLowerInvariant();
        var password = request.Password?.Trim() ?? "";
        if (password.Length < 8)
            throw new InvalidOperationException("Mật khẩu tối thiểu 8 ký tự.");
        var pin = NormalizePin(request.ParentPin);
        var timezone = string.IsNullOrWhiteSpace(request.Timezone)
            ? "Asia/Ho_Chi_Minh"
            : request.Timezone.Trim();

        await _kitAccounts.AssertEmailPasswordCompatibleAsync(email, password, cancellationToken);

        var tenantCode = await AllocateTenantCodeAsync(familyName, cancellationToken);
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        var pinHash = BCrypt.Net.BCrypt.HashPassword(pin);

        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();

        var settings = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["platform"] = new Dictionary<string, object?>
            {
                ["schema_version"] = 1,
                ["vertical"] = "family",
                ["enabled_modules"] = new[] { "family_os" },
                ["allowed_modules"] = new[] { "family_os" },
                ["features"] = new Dictionary<string, object?>(),
            },
        });

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        // Trial length is runtime-configurable via payment.plan (Admin → Family OS → Billing).
        var trialDays = await ResolveTrialDaysAsync(conn, cancellationToken);
        var trialEnds = DateTimeOffset.UtcNow.AddDays(trialDays);

        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        await conn.ExecuteAsync(
            """
            INSERT INTO public.tenants (
                id, tenant_code, tenant_name, country_code, default_currency,
                business_vertical, settings, status
            )
            VALUES (
                @Id, @TenantCode, @TenantName, 'VN', 'VND',
                'hybrid', @Settings::jsonb, 1
            )
            """,
            new
            {
                Id = tenantId,
                TenantCode = tenantCode,
                TenantName = familyName,
                Settings = settings,
            },
            tx);

        await conn.ExecuteAsync(
            "SELECT set_config('app.tenant_id', @Value, true)",
            new { Value = tenantId.ToString() },
            tx);

        await conn.ExecuteAsync(
            """
            INSERT INTO public.branches (
                id, tenant_id, branch_code, branch_name, is_head_office, status
            )
            VALUES (@Id, @TenantId, 'HOME', 'Nhà', TRUE, 1)
            """,
            new { Id = branchId, TenantId = tenantId },
            tx);

        await conn.ExecuteAsync(
            """
            INSERT INTO public.employees (
                id, tenant_id, employee_code, full_name, email, status
            )
            VALUES (@Id, @TenantId, 'EMP001', @FullName, @Email, 1)
            """,
            new { Id = employeeId, TenantId = tenantId, FullName = parentName, Email = email },
            tx);

        await conn.ExecuteAsync(
            "INSERT INTO public.employee_branches (employee_id, branch_id, is_primary) VALUES (@E, @B, TRUE)",
            new { E = employeeId, B = branchId },
            tx);

        await conn.ExecuteAsync(
            """
            INSERT INTO public.users (
                id, tenant_id, employee_id, username, email, password_hash, status
            )
            VALUES (@Id, @TenantId, @EmployeeId, @Username, @Email, @PasswordHash, 1)
            """,
            new
            {
                Id = userId,
                TenantId = tenantId,
                EmployeeId = employeeId,
                Username = username,
                Email = email,
                PasswordHash = passwordHash,
            },
            tx);

        await conn.ExecuteAsync(
            "INSERT INTO public.roles (id, tenant_id, role_code, role_name) VALUES (@Id, @TenantId, 'ADMIN', 'Phụ huynh')",
            new { Id = roleId, TenantId = tenantId },
            tx);

        await conn.ExecuteAsync(
            "INSERT INTO public.user_roles (user_id, role_id) VALUES (@UserId, @RoleId)",
            new { UserId = userId, RoleId = roleId },
            tx);

        await conn.ExecuteAsync(
            """
            INSERT INTO public.role_permissions (role_id, permission_id)
            SELECT @RoleId, p.id FROM public.permissions p
            WHERE p.permission_code LIKE 'family_os.%'
            ON CONFLICT DO NOTHING
            """,
            new { RoleId = roleId },
            tx);

        await conn.ExecuteAsync(
            "SELECT kit_provision_pack_workspace(@TenantId, 'family_os')",
            new { TenantId = tenantId },
            tx);

        await conn.ExecuteAsync(
            """
            INSERT INTO pack_family.family (
                id, tenant_id, display_name, timezone, status, parent_pin_hash
            )
            VALUES (@Id, @TenantId, @DisplayName, @Timezone, 'active', @PinHash)
            """,
            new
            {
                Id = familyId,
                TenantId = tenantId,
                DisplayName = familyName,
                Timezone = timezone,
                PinHash = pinHash,
            },
            tx);

        await conn.ExecuteAsync(
            """
            INSERT INTO pack_family.membership (
                id, tenant_id, family_id, display_name, role_code, status, sort_order, user_id
            )
            VALUES (
                @Id, @TenantId, @FamilyId, @DisplayName, 'guardian', 'active', 0, @UserId
            )
            """,
            new
            {
                Id = membershipId,
                TenantId = tenantId,
                FamilyId = familyId,
                DisplayName = parentName,
                UserId = userId,
            },
            tx);

        await InsertChildIfAnyAsync(conn, tx, tenantId, familyId, request.Child1Name, 10, cancellationToken);
        await InsertChildIfAnyAsync(conn, tx, tenantId, familyId, request.Child2Name, 20, cancellationToken);

        await conn.ExecuteAsync(
            """
            INSERT INTO pack_family.family_subscription (
                id, tenant_id, family_id, plan_code, status, trial_ends_at, current_period_end
            )
            VALUES (
                @Id, @TenantId, @FamilyId, 'starter_trial', 'trial', @TrialEnds, @TrialEnds
            )
            """,
            new
            {
                Id = subscriptionId,
                TenantId = tenantId,
                FamilyId = familyId,
                TrialEnds = trialEnds.UtcDateTime,
            },
            tx);

        // Kit Payment Platform subscription (source of truth going forward)
        await conn.ExecuteAsync(
            """
            INSERT INTO payment.subscription (
                tenant_id, product_code, subject_type, subject_id,
                plan_code, status, trial_ends_at, current_period_end
            )
            VALUES (
                @TenantId, 'family_os', 'family', @FamilyId,
                'starter_trial', 'trial', @TrialEnds, @TrialEnds
            )
            ON CONFLICT (tenant_id, product_code, subject_type, subject_id) DO NOTHING
            """,
            new
            {
                TenantId = tenantId,
                FamilyId = familyId,
                TrialEnds = trialEnds.UtcDateTime,
            },
            tx);

        var memberCount = 1
            + (string.IsNullOrWhiteSpace(request.Child1Name) ? 0 : 1)
            + (string.IsNullOrWhiteSpace(request.Child2Name) ? 0 : 1);
        await conn.ExecuteAsync(
            """
            INSERT INTO public.family_os_trial_signup (
                tenant_id, tenant_code, family_id, family_name,
                parent_display_name, email, username, member_count,
                plan_code, status, trial_ends_at, source, registered_at
            )
            VALUES (
                @TenantId, @TenantCode, @FamilyId, @FamilyName,
                @ParentName, @Email, @Username, @MemberCount,
                'starter_trial', 'trial', @TrialEnds, 'self_register', NOW()
            )
            ON CONFLICT (family_id) DO UPDATE SET
                family_name = EXCLUDED.family_name,
                parent_display_name = EXCLUDED.parent_display_name,
                email = EXCLUDED.email,
                username = EXCLUDED.username,
                member_count = EXCLUDED.member_count,
                plan_code = EXCLUDED.plan_code,
                status = EXCLUDED.status,
                trial_ends_at = EXCLUDED.trial_ends_at,
                updated_at = NOW()
            """,
            new
            {
                TenantId = tenantId,
                TenantCode = tenantCode,
                FamilyId = familyId,
                FamilyName = familyName,
                ParentName = parentName,
                Email = email,
                Username = username,
                MemberCount = memberCount,
                TrialEnds = trialEnds.UtcDateTime,
            },
            tx);

        await _kitAccounts.EnsureAccountForUserAsync(
            userId,
            tenantId,
            email,
            password,
            passwordHash,
            parentName,
            conn,
            tx,
            cancellationToken);

        await tx.CommitAsync(cancellationToken);

        var login = await _auth.LoginAsync(
            new LoginRequest(username, password, tenantCode),
            null,
            cancellationToken);
        var session = login?.Session
            ?? throw new InvalidOperationException("Đăng ký xong nhưng không đăng nhập được — thử đăng nhập lại.");

        return new FamilyRegisterResponse(
            tenantCode,
            tenantId,
            familyId,
            familyName,
            MapSession(session),
            new FamilySubscriptionDto(
                familyId,
                "starter_trial",
                FamilySubscriptionStatuses.Trial,
                trialEnds,
                trialEnds,
                true,
                trialDays,
                trialDays));
    }

    private async Task<int> ResolveTrialDaysAsync(
        Npgsql.NpgsqlConnection conn,
        CancellationToken cancellationToken)
    {
        var fromPlan = await conn.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                """
                SELECT trial_days
                FROM payment.plan
                WHERE product_code = 'family_os' AND is_active = TRUE
                ORDER BY amount_vnd ASC, plan_code ASC
                LIMIT 1
                """,
                cancellationToken: cancellationToken));
        return fromPlan is int days && days >= 0 ? days : TrialDays;
    }

    public async Task<FamilyInviteDto> CreateInviteAsync(
        Guid familyId,
        FamilyInviteCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        await _writeAccess.EnsureCanMutateAsync(familyId, cancellationToken);
        await EnsureEntitledAsync(familyId, cancellationToken);

        var role = string.IsNullOrWhiteSpace(request.RoleCode)
            ? FamilyMembershipRoles.Guardian
            : request.RoleCode.Trim().ToLowerInvariant();
        if (role is not (FamilyMembershipRoles.Guardian or FamilyMembershipRoles.Caregiver or FamilyMembershipRoles.Viewer))
            throw new InvalidOperationException("roleCode phải là guardian | caregiver | viewer.");

        var maxUses = Math.Clamp(request.MaxUses ?? 3, 1, 20);
        var days = Math.Clamp(request.ValidDays ?? 7, 1, 30);
        var code = GenerateInviteCode();
        var expires = DateTimeOffset.UtcNow.AddDays(days);

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        if (!_tenant.IsAuthenticated || _tenant.TenantId == Guid.Empty)
            throw new InvalidOperationException("Cần đăng nhập để tạo mã mời.");

        var family = await conn.QuerySingleOrDefaultAsync<(Guid TenantId, Guid Id)>(
            """
            SELECT tenant_id AS TenantId, id AS Id
            FROM pack_family.family
            WHERE id = @FamilyId AND tenant_id = @TenantId AND deleted_at IS NULL
            """,
            new { FamilyId = familyId, TenantId = _tenant.TenantId });
        if (family.Id == Guid.Empty)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        var id = await conn.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO pack_family.family_invite (
                tenant_id, family_id, code, role_code, expires_at, max_uses
            )
            VALUES (@TenantId, @FamilyId, @Code, @RoleCode, @ExpiresAt, @MaxUses)
            RETURNING id
            """,
            new
            {
                family.TenantId,
                FamilyId = familyId,
                Code = code,
                RoleCode = role,
                ExpiresAt = expires.UtcDateTime,
                MaxUses = maxUses,
            });

        return new FamilyInviteDto(id, code, role, expires, maxUses, 0);
    }

    public async Task<FamilyInviteAcceptResponse> AcceptInviteAsync(
        FamilyInviteAcceptRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = (request.Code ?? "").Trim().ToUpperInvariant();
        if (code.Length < 6)
            throw new InvalidOperationException("Mã mời không hợp lệ.");

        var parentName = Require(request.ParentDisplayName, "Tên phụ huynh");
        var username = Require(request.Username, "Tài khoản").ToLowerInvariant();
        var email = Require(request.Email, "Email").ToLowerInvariant();
        var password = request.Password?.Trim() ?? "";
        if (password.Length < 8)
            throw new InvalidOperationException("Mật khẩu tối thiểu 8 ký tự.");

        await _kitAccounts.AssertEmailPasswordCompatibleAsync(email, password, cancellationToken);

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var invite = await conn.QuerySingleOrDefaultAsync<InviteRow>(
            """
            SELECT
                id AS Id,
                tenant_id AS TenantId,
                family_id AS FamilyId,
                code AS Code,
                role_code AS RoleCode,
                expires_at AS ExpiresAt,
                max_uses AS MaxUses,
                used_count AS UsedCount,
                revoked_at AS RevokedAt
            FROM pack_family.family_invite
            WHERE code = @Code
            FOR UPDATE
            """,
            new { Code = code },
            tx);

        if (invite is null)
            throw new InvalidOperationException("Không tìm thấy mã mời.");
        if (invite.RevokedAt is not null)
            throw new InvalidOperationException("Mã mời đã bị thu hồi.");
        if (invite.ExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("Mã mời đã hết hạn.");
        if (invite.UsedCount >= invite.MaxUses)
            throw new InvalidOperationException("Mã mời đã dùng hết lượt.");

        await conn.ExecuteAsync(
            "SELECT set_config('app.tenant_id', @Value, true)",
            new { Value = invite.TenantId.ToString() },
            tx);

        var tenantCode = await conn.ExecuteScalarAsync<string>(
            "SELECT tenant_code FROM public.tenants WHERE id = @Id",
            new { Id = invite.TenantId },
            tx) ?? throw new InvalidOperationException("Tenant không tồn tại.");

        var familyName = await conn.ExecuteScalarAsync<string>(
            """
            SELECT display_name FROM pack_family.family
            WHERE id = @Id AND deleted_at IS NULL
            """,
            new { Id = invite.FamilyId },
            tx) ?? throw new InvalidOperationException("Gia đình không tồn tại.");

        if (await conn.ExecuteScalarAsync<bool>(
                """
                SELECT EXISTS(
                    SELECT 1 FROM public.users
                    WHERE tenant_id = @TenantId AND lower(username) = @Username AND deleted_at IS NULL
                )
                """,
                new { TenantId = invite.TenantId, Username = username },
                tx))
            throw new InvalidOperationException("Tên tài khoản đã dùng trong nhà này.");

        var employeeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

        await conn.ExecuteAsync(
            """
            INSERT INTO public.employees (id, tenant_id, employee_code, full_name, email, status)
            VALUES (@Id, @TenantId, @Code, @FullName, @Email, 1)
            """,
            new
            {
                Id = employeeId,
                TenantId = invite.TenantId,
                Code = "EMP" + RandomNumberGenerator.GetInt32(1000, 9999),
                FullName = parentName,
                Email = email,
            },
            tx);

        var homeBranch = await conn.ExecuteScalarAsync<Guid?>(
            """
            SELECT id FROM public.branches
            WHERE tenant_id = @TenantId AND deleted_at IS NULL
            ORDER BY is_head_office DESC NULLS LAST
            LIMIT 1
            """,
            new { TenantId = invite.TenantId },
            tx);

        if (homeBranch is Guid bid)
        {
            await conn.ExecuteAsync(
                "INSERT INTO public.employee_branches (employee_id, branch_id, is_primary) VALUES (@E, @B, TRUE)",
                new { E = employeeId, B = bid },
                tx);
        }

        await conn.ExecuteAsync(
            """
            INSERT INTO public.users (id, tenant_id, employee_id, username, email, password_hash, status)
            VALUES (@Id, @TenantId, @EmployeeId, @Username, @Email, @PasswordHash, 1)
            """,
            new
            {
                Id = userId,
                TenantId = invite.TenantId,
                EmployeeId = employeeId,
                Username = username,
                Email = email,
                PasswordHash = passwordHash,
            },
            tx);

        await conn.ExecuteAsync(
            "INSERT INTO public.roles (id, tenant_id, role_code, role_name) VALUES (@Id, @TenantId, 'GUARDIAN', 'Phụ huynh')",
            new { Id = roleId, TenantId = invite.TenantId },
            tx);

        await conn.ExecuteAsync(
            "INSERT INTO public.user_roles (user_id, role_id) VALUES (@U, @R)",
            new { U = userId, R = roleId },
            tx);

        await conn.ExecuteAsync(
            """
            INSERT INTO public.role_permissions (role_id, permission_id)
            SELECT @RoleId, p.id FROM public.permissions p
            WHERE p.permission_code LIKE 'family_os.%'
            ON CONFLICT DO NOTHING
            """,
            new { RoleId = roleId },
            tx);

        var sort = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COALESCE(MAX(sort_order), 0) + 10
            FROM pack_family.membership
            WHERE family_id = @FamilyId AND deleted_at IS NULL
            """,
            new { FamilyId = invite.FamilyId },
            tx);

        await conn.ExecuteAsync(
            """
            INSERT INTO pack_family.membership (
                id, tenant_id, family_id, display_name, role_code, status, sort_order, user_id
            )
            VALUES (
                @Id, @TenantId, @FamilyId, @DisplayName, @RoleCode, 'active', @Sort, @UserId
            )
            """,
            new
            {
                Id = membershipId,
                TenantId = invite.TenantId,
                FamilyId = invite.FamilyId,
                DisplayName = parentName,
                RoleCode = invite.RoleCode,
                Sort = sort,
                UserId = userId,
            },
            tx);

        if (!string.IsNullOrWhiteSpace(request.ParentPin))
        {
            var pinHash = BCrypt.Net.BCrypt.HashPassword(NormalizePin(request.ParentPin));
            await conn.ExecuteAsync(
                """
                UPDATE pack_family.family
                SET parent_pin_hash = COALESCE(parent_pin_hash, @PinHash), updated_at = NOW()
                WHERE id = @FamilyId
                """,
                new { FamilyId = invite.FamilyId, PinHash = pinHash },
                tx);
        }

        await conn.ExecuteAsync(
            """
            UPDATE pack_family.family_invite
            SET used_count = used_count + 1
            WHERE id = @Id
            """,
            new { Id = invite.Id },
            tx);

        await _kitAccounts.EnsureAccountForUserAsync(
            userId,
            invite.TenantId,
            email,
            password,
            passwordHash,
            parentName,
            conn,
            tx,
            cancellationToken);

        await tx.CommitAsync(cancellationToken);

        var login = await _auth.LoginAsync(
            new LoginRequest(username, password, tenantCode),
            null,
            cancellationToken);
        var session = login?.Session
            ?? throw new InvalidOperationException("Tham gia xong nhưng không đăng nhập được.");

        return new FamilyInviteAcceptResponse(tenantCode, invite.FamilyId, familyName, MapSession(session));
    }

    public async Task<FamilySubscriptionDto> GetSubscriptionAsync(
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<SubscriptionRow>(
            """
            SELECT
                family_id AS FamilyId,
                plan_code AS PlanCode,
                status AS Status,
                trial_ends_at AS TrialEndsAt,
                current_period_end AS CurrentPeriodEnd,
                created_at AS CreatedAt
            FROM pack_family.family_subscription
            WHERE family_id = @FamilyId
              AND (@TenantId = '00000000-0000-0000-0000-000000000000'::uuid
                   OR tenant_id = @TenantId)
            """,
            new
            {
                FamilyId = familyId,
                TenantId = _tenant.IsAuthenticated ? _tenant.TenantId : Guid.Empty,
            });

        if (row is null)
        {
            // Missing row → Free soft tier (not forever pilot paid).
            return EnrichSubscription(
                new FamilySubscriptionDto(
                    familyId, FamilyPlanCodes.Free, FamilySubscriptionStatuses.Expired, null, null, false));
        }

        var status = NormalizeSubscriptionStatus(row);
        var entitled = status is FamilySubscriptionStatuses.Trial
            or FamilySubscriptionStatuses.TrialGrace
            or FamilySubscriptionStatuses.Active;
        if (status == FamilySubscriptionStatuses.PastDue
            && row.CurrentPeriodEnd is DateTime grace
            && grace.AddDays(3) >= DateTime.UtcNow)
        {
            entitled = true;
        }

        int? trialDaysRemaining = null;
        int? trialDaysTotal = null;
        int? trialGraceDaysRemaining = null;
        if (status == FamilySubscriptionStatuses.Trial && row.TrialEndsAt is DateTime trialEnd)
        {
            var endUtc = DateTime.SpecifyKind(trialEnd, DateTimeKind.Utc);
            trialDaysRemaining = Math.Max(0, (int)Math.Ceiling((endUtc - DateTime.UtcNow).TotalDays));

            // Prefer actual trial window from created→ends; fall back to billing config.
            if (row.CreatedAt is DateTime created)
            {
                var startUtc = DateTime.SpecifyKind(created, DateTimeKind.Utc);
                var span = (int)Math.Round((endUtc - startUtc).TotalDays);
                trialDaysTotal = span > 0 ? span : TrialDays;
            }
            else
            {
                trialDaysTotal = TrialDays;
            }

            // Never show remaining > total (clock skew / rounding).
            if (trialDaysTotal is int total && trialDaysRemaining > total)
                trialDaysRemaining = total;
        }
        else if (status == FamilySubscriptionStatuses.TrialGrace && row.TrialEndsAt is DateTime graceEnd)
        {
            var endUtc = DateTime.SpecifyKind(graceEnd, DateTimeKind.Utc);
            var graceUntil = endUtc.AddDays(TrialGraceDays);
            trialGraceDaysRemaining = Math.Max(
                0,
                (int)Math.Ceiling((graceUntil - DateTime.UtcNow).TotalDays));
            trialDaysRemaining = 0;
            trialDaysTotal = TrialDays;
            if (row.CreatedAt is DateTime created)
            {
                var startUtc = DateTime.SpecifyKind(created, DateTimeKind.Utc);
                var span = (int)Math.Round((endUtc - startUtc).TotalDays);
                if (span > 0) trialDaysTotal = span;
            }
        }

        return EnrichSubscription(
            new FamilySubscriptionDto(
                row.FamilyId,
                row.PlanCode,
                status,
                row.TrialEndsAt is DateTime t ? new DateTimeOffset(DateTime.SpecifyKind(t, DateTimeKind.Utc)) : null,
                row.CurrentPeriodEnd is DateTime p ? new DateTimeOffset(DateTime.SpecifyKind(p, DateTimeKind.Utc)) : null,
                entitled,
                trialDaysRemaining,
                trialDaysTotal,
                trialGraceDaysRemaining));
    }

    public Task<FamilyCapabilityPackDto> GetCapabilityPackAsync(
        Guid familyId,
        CancellationToken cancellationToken = default) =>
        GetCapabilityPackFromSubAsync(familyId, cancellationToken);

    public async Task EnsureEntitledAsync(
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        // Packaging v1: CoreRoutine remains available on Free after trial —
        // keep EnsureEntitled for legacy callers that mean "paid surface".
        var sub = await GetSubscriptionAsync(familyId, cancellationToken);
        if (!sub.IsEntitled)
            throw new InvalidOperationException(
                "Gói trả phí / trial đã hết — nâng Family Peace Plan để mở Coach, ROP và Letter. Free vẫn dùng được routine cơ bản.");
    }

    public async Task EnsureCapabilityAsync(
        Guid familyId,
        string capabilityCode,
        CancellationToken cancellationToken = default)
    {
        var pack = await GetCapabilityPackFromSubAsync(familyId, cancellationToken);
        var code = (capabilityCode ?? "").Trim().ToLowerInvariant();
        if (pack.Capabilities.Contains(code, StringComparer.OrdinalIgnoreCase))
            return;

        var hint = pack.UpgradeHintVi
            ?? "Nâng gói Famixa để mở tính năng này.";
        throw new InvalidOperationException(
            $"Gói {pack.DisplayNameVi} chưa gồm tính năng này. {hint}");
    }

    public async Task EnsureCanAddChildAsync(
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        var pack = await GetCapabilityPackFromSubAsync(familyId, cancellationToken);
        if (pack.MaxChildren is null)
            return;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var childCount = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int
            FROM pack_family.membership
            WHERE family_id = @FamilyId
              AND tenant_id = @TenantId
              AND LOWER(role_code) = 'child'
              AND deleted_at IS NULL
            """,
            new
            {
                FamilyId = familyId,
                TenantId = _tenant.IsAuthenticated ? _tenant.TenantId : Guid.Empty,
            });

        // When tenant filter empty (edge), recount without tenant.
        if (!_tenant.IsAuthenticated || _tenant.TenantId == Guid.Empty)
        {
            childCount = await conn.ExecuteScalarAsync<int>(
                """
                SELECT COUNT(*)::int
                FROM pack_family.membership
                WHERE family_id = @FamilyId
                  AND LOWER(role_code) = 'child'
                  AND deleted_at IS NULL
                """,
                new { FamilyId = familyId });
        }

        if (childCount >= pack.MaxChildren.Value)
        {
            throw new InvalidOperationException(
                $"Gói {pack.DisplayNameVi} tối đa {pack.MaxChildren} trẻ. {pack.UpgradeHintVi}");
        }
    }

    private async Task<FamilyCapabilityPackDto> GetCapabilityPackFromSubAsync(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        var sub = await GetSubscriptionAsync(familyId, cancellationToken);
        var tier = FamilyPlanCapabilityMatrix.ResolveTier(sub.PlanCode, sub.IsEntitled, sub.Status);
        return new FamilyCapabilityPackDto(
            familyId,
            sub.PlanCode,
            tier,
            FamilyPlanCapabilityMatrix.DisplayNameVi(tier),
            FamilyPlanCapabilityMatrix.OutcomeNameVi(tier),
            sub.IsEntitled,
            sub.Status,
            FamilyPlanCapabilityMatrix.MaxChildrenForTier(tier),
            FamilyPlanCapabilityMatrix.CapabilitiesForTier(tier),
            FamilyPlanCapabilityMatrix.RecommendedUpgrade(tier),
            FamilyPlanCapabilityMatrix.UpgradeHintVi(tier));
    }

    private static FamilySubscriptionDto EnrichSubscription(FamilySubscriptionDto sub)
    {
        var tier = FamilyPlanCapabilityMatrix.ResolveTier(sub.PlanCode, sub.IsEntitled, sub.Status);
        return sub with
        {
            TierCode = tier,
            DisplayNameVi = FamilyPlanCapabilityMatrix.DisplayNameVi(tier),
            OutcomeNameVi = FamilyPlanCapabilityMatrix.OutcomeNameVi(tier),
            MaxChildren = FamilyPlanCapabilityMatrix.MaxChildrenForTier(tier),
            Capabilities = FamilyPlanCapabilityMatrix.CapabilitiesForTier(tier),
            RecommendedUpgradePlanCode = FamilyPlanCapabilityMatrix.RecommendedUpgrade(tier),
            UpgradeHintVi = FamilyPlanCapabilityMatrix.UpgradeHintVi(tier),
        };
    }

    public async Task<FamilySubscriptionDto> ExtendTrialAsync(
        Guid familyId,
        ExtendFamilyTrialRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenant.IsAuthenticated || _tenant.TenantId == Guid.Empty)
            throw new InvalidOperationException("Cần đăng nhập.");
        if (request.ExtraDays is < 1 or > 365)
            throw new InvalidOperationException("Số ngày gia hạn phải trong khoảng 1–365.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        // Only trial / lapsed subscriptions — paid periods must be extended via payment.
        var updated = await conn.ExecuteAsync(
            """
            UPDATE pack_family.family_subscription
            SET status = 'trial',
                trial_ends_at = GREATEST(COALESCE(trial_ends_at, NOW()), NOW())
                    + make_interval(days => @ExtraDays),
                current_period_end = GREATEST(COALESCE(current_period_end, NOW()), NOW())
                    + make_interval(days => @ExtraDays),
                updated_at = NOW()
            WHERE family_id = @FamilyId
              AND tenant_id = @TenantId
              AND status IN ('trial', 'expired', 'canceled')
            """,
            new { FamilyId = familyId, TenantId = _tenant.TenantId, request.ExtraDays },
            tx);

        if (updated == 0)
        {
            var existingStatus = await conn.ExecuteScalarAsync<string?>(
                """
                SELECT status FROM pack_family.family_subscription
                WHERE family_id = @FamilyId AND tenant_id = @TenantId
                """,
                new { FamilyId = familyId, TenantId = _tenant.TenantId },
                tx);

            if (existingStatus is not null)
                throw new InvalidOperationException(
                    $"Gói đang ở trạng thái '{existingStatus}' — gia hạn qua thanh toán, không phải trial.");

            var familyExists = await conn.ExecuteScalarAsync<bool>(
                """
                SELECT EXISTS(
                    SELECT 1 FROM pack_family.family
                    WHERE id = @FamilyId AND tenant_id = @TenantId AND deleted_at IS NULL
                )
                """,
                new { FamilyId = familyId, TenantId = _tenant.TenantId },
                tx);
            if (!familyExists)
                throw new InvalidOperationException("Không tìm thấy gia đình.");

            // Legacy family without a subscription row — open a fresh trial window.
            await conn.ExecuteAsync(
                """
                INSERT INTO pack_family.family_subscription (
                    id, tenant_id, family_id, plan_code, status, trial_ends_at, current_period_end
                )
                VALUES (
                    @Id, @TenantId, @FamilyId, 'starter_trial', 'trial',
                    NOW() + make_interval(days => @ExtraDays),
                    NOW() + make_interval(days => @ExtraDays)
                )
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    TenantId = _tenant.TenantId,
                    FamilyId = familyId,
                    request.ExtraDays,
                },
                tx);
        }

        // Mirror into Kit Payment platform subscription (source of truth going forward).
        await conn.ExecuteAsync(
            """
            INSERT INTO payment.subscription (
                tenant_id, product_code, subject_type, subject_id,
                plan_code, status, trial_ends_at, current_period_end
            )
            SELECT tenant_id, 'family_os', 'family', family_id,
                   plan_code, status, trial_ends_at, current_period_end
            FROM pack_family.family_subscription
            WHERE family_id = @FamilyId AND tenant_id = @TenantId
            ON CONFLICT (tenant_id, product_code, subject_type, subject_id)
            DO UPDATE SET
                status = EXCLUDED.status,
                trial_ends_at = EXCLUDED.trial_ends_at,
                current_period_end = EXCLUDED.current_period_end,
                updated_at = NOW()
            """,
            new { FamilyId = familyId, TenantId = _tenant.TenantId },
            tx);

        await conn.ExecuteAsync(
            """
            UPDATE public.family_os_trial_signup l
            SET status = s.status,
                plan_code = s.plan_code,
                trial_ends_at = s.trial_ends_at,
                updated_at = NOW()
            FROM pack_family.family_subscription s
            WHERE l.family_id = s.family_id
              AND s.family_id = @FamilyId
              AND s.tenant_id = @TenantId
            """,
            new { FamilyId = familyId, TenantId = _tenant.TenantId },
            tx);

        await tx.CommitAsync(cancellationToken);

        return await GetSubscriptionAsync(familyId, cancellationToken);
    }

    public async Task<FamilyOsTrialSignupListDto> ListTrialSignupsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        // Refresh ledger statuses from live subscription when RLS allows (same-tenant rows).
        // Cross-tenant rows stay as last written; migration/register keep the ledger populated.
        try
        {
            await conn.ExecuteAsync(
                """
                UPDATE public.family_os_trial_signup l
                SET status = s.status,
                    plan_code = s.plan_code,
                    trial_ends_at = s.trial_ends_at,
                    member_count = GREATEST(
                        l.member_count,
                        COALESCE((
                            SELECT COUNT(*)::int
                            FROM pack_family.membership m
                            WHERE m.family_id = l.family_id AND m.deleted_at IS NULL
                        ), l.member_count)
                    ),
                    updated_at = NOW()
                FROM pack_family.family_subscription s
                WHERE s.family_id = l.family_id
                """);
        }
        catch
        {
            // Best-effort refresh — never block the ops list.
        }

        var rows = (await conn.QueryAsync<TrialSignupRow>(
            """
            SELECT
                id AS Id,
                tenant_id AS TenantId,
                tenant_code AS TenantCode,
                family_id AS FamilyId,
                family_name AS FamilyName,
                parent_display_name AS ParentDisplayName,
                email AS Email,
                username AS Username,
                member_count AS MemberCount,
                plan_code AS PlanCode,
                status AS Status,
                trial_ends_at AS TrialEndsAt,
                source AS Source,
                registered_at AS RegisteredAt
            FROM public.family_os_trial_signup
            ORDER BY registered_at DESC
            LIMIT 500
            """)).AsList();

        var now = DateTime.UtcNow;
        var items = rows.Select(r =>
        {
            int? remaining = null;
            if (r.TrialEndsAt is DateTime ends)
            {
                var endUtc = DateTime.SpecifyKind(ends, DateTimeKind.Utc);
                remaining = Math.Max(0, (int)Math.Ceiling((endUtc - now).TotalDays));
            }

            var status = (r.Status ?? "").Trim().ToLowerInvariant();
            if (status == FamilySubscriptionStatuses.Trial
                && r.TrialEndsAt is DateTime te
                && DateTime.SpecifyKind(te, DateTimeKind.Utc) < now)
            {
                status = FamilySubscriptionStatuses.Expired;
                remaining = 0;
            }

            return new FamilyOsTrialSignupDto(
                r.Id,
                r.TenantId,
                r.TenantCode ?? "",
                r.FamilyId,
                r.FamilyName ?? "",
                r.ParentDisplayName ?? "",
                r.Email ?? "",
                r.Username ?? "",
                r.MemberCount,
                r.PlanCode ?? "starter_trial",
                status,
                r.TrialEndsAt is DateTime t
                    ? new DateTimeOffset(DateTime.SpecifyKind(t, DateTimeKind.Utc))
                    : null,
                r.Source ?? "self_register",
                new DateTimeOffset(DateTime.SpecifyKind(r.RegisteredAt, DateTimeKind.Utc)),
                remaining);
        }).ToList();

        var trialActive = items.Count(i => i.Status == FamilySubscriptionStatuses.Trial);
        var trialExpired = items.Count(i => i.Status == FamilySubscriptionStatuses.Expired);
        var paidActive = items.Count(i =>
            i.Status is FamilySubscriptionStatuses.Active or FamilySubscriptionStatuses.PastDue);
        var other = items.Count - trialActive - trialExpired - paidActive;

        return new FamilyOsTrialSignupListDto(
            items.Count,
            trialActive,
            trialExpired,
            paidActive,
            Math.Max(0, other),
            items);
    }

    public async Task<DemoHousePingResponse> RecordDemoHouseViewAsync(
        RecordDemoHouseViewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenant.IsAuthenticated || _tenant.TenantId == Guid.Empty)
            throw new InvalidOperationException("Cần đăng nhập để ghi lượt xem demo.");

        var sessionId = request.SessionId is Guid sid && sid != Guid.Empty
            ? sid
            : Guid.CreateVersion7();

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<(string TenantCode, bool IsDemo)>(
            new CommandDefinition(
                """
                SELECT
                    t.tenant_code AS TenantCode,
                    (
                        t.tenant_code = 'DEMO_FAMILY'
                        OR COALESCE((t.settings->'platform'->'features'->>'demoHouse')::boolean, FALSE)
                    ) AS IsDemo
                FROM public.tenants t
                WHERE t.id = @TenantId AND t.deleted_at IS NULL
                """,
                new { TenantId = _tenant.TenantId },
                cancellationToken: cancellationToken));

        if (string.IsNullOrWhiteSpace(row.TenantCode) || !row.IsDemo)
            return new DemoHousePingResponse(sessionId);

        var clientKey = (request.ClientKey ?? "").Trim();
        if (clientKey.Length > 64) clientKey = clientKey[..64];

        try
        {
            await conn.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO public.family_os_demo_view (
                        tenant_id, tenant_code, user_id, client_key, source,
                        session_id, last_seen_at, duration_seconds
                    )
                    VALUES (
                        @TenantId, @TenantCode, @UserId, @ClientKey, 'spa_demo',
                        @SessionId, NOW(), 0
                    )
                    """,
                    new
                    {
                        TenantId = _tenant.TenantId,
                        TenantCode = row.TenantCode,
                        UserId = _tenant.UserId == Guid.Empty ? (Guid?)null : _tenant.UserId,
                        ClientKey = clientKey.Length == 0 ? null : clientKey,
                        SessionId = sessionId,
                    },
                    cancellationToken: cancellationToken));
        }
        catch
        {
            // Pre-dwell schema or missing table — never block /demo enter.
            try
            {
                await conn.ExecuteAsync(
                    new CommandDefinition(
                        """
                        INSERT INTO public.family_os_demo_view (
                            tenant_id, tenant_code, user_id, client_key, source
                        )
                        VALUES (
                            @TenantId, @TenantCode, @UserId, @ClientKey, 'spa_demo'
                        )
                        """,
                        new
                        {
                            TenantId = _tenant.TenantId,
                            TenantCode = row.TenantCode,
                            UserId = _tenant.UserId == Guid.Empty ? (Guid?)null : _tenant.UserId,
                            ClientKey = clientKey.Length == 0 ? null : clientKey,
                        },
                        cancellationToken: cancellationToken));
            }
            catch
            {
                // ignore
            }
        }

        return new DemoHousePingResponse(sessionId);
    }

    public async Task HeartbeatDemoHouseViewAsync(
        DemoHouseHeartbeatRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenant.IsAuthenticated || _tenant.TenantId == Guid.Empty)
            return;
        if (request.SessionId == Guid.Empty)
            return;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        try
        {
            await conn.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE public.family_os_demo_view v
                    SET
                        last_seen_at = NOW(),
                        duration_seconds = LEAST(
                            7200,
                            GREATEST(
                                v.duration_seconds,
                                EXTRACT(EPOCH FROM (NOW() - v.created_at))::int
                            )
                        )
                    FROM public.tenants t
                    WHERE v.session_id = @SessionId
                      AND v.tenant_id = @TenantId
                      AND t.id = v.tenant_id
                      AND t.deleted_at IS NULL
                      AND (
                          t.tenant_code = 'DEMO_FAMILY'
                          OR COALESCE((t.settings->'platform'->'features'->>'demoHouse')::boolean, FALSE)
                      )
                    """,
                    new { request.SessionId, TenantId = _tenant.TenantId },
                    cancellationToken: cancellationToken));
        }
        catch
        {
            // Columns may not exist yet.
        }
    }

    public async Task<FamilyOsDemoHouseViewsDto> GetDemoHouseViewsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        // Prefer ledger; fall back to refresh_tokens for demo viewer if table empty/missing.
        try
        {
            var stats = await conn.QuerySingleAsync<(
                int ViewsToday,
                int Views7d,
                int UniqueToday,
                int Unique7d,
                DateTime? LastViewAt,
                int AvgSecondsToday,
                int AvgSeconds7d,
                int TotalSecondsToday,
                int TotalSeconds7d)>(
                new CommandDefinition(
                    """
                    SELECT
                        COUNT(*) FILTER (
                            WHERE created_at >= date_trunc('day', NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')
                                AT TIME ZONE 'Asia/Ho_Chi_Minh'
                        )::int AS ViewsToday,
                        COUNT(*) FILTER (
                            WHERE created_at >= NOW() - INTERVAL '7 days'
                        )::int AS Views7d,
                        COUNT(DISTINCT COALESCE(NULLIF(client_key, ''), id::text)) FILTER (
                            WHERE created_at >= date_trunc('day', NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')
                                AT TIME ZONE 'Asia/Ho_Chi_Minh'
                        )::int AS UniqueToday,
                        COUNT(DISTINCT COALESCE(NULLIF(client_key, ''), id::text)) FILTER (
                            WHERE created_at >= NOW() - INTERVAL '7 days'
                        )::int AS Unique7d,
                        MAX(created_at) AS LastViewAt,
                        COALESCE(AVG(duration_seconds) FILTER (
                            WHERE duration_seconds > 0
                              AND created_at >= date_trunc('day', NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')
                                  AT TIME ZONE 'Asia/Ho_Chi_Minh'
                        ), 0)::int AS AvgSecondsToday,
                        COALESCE(AVG(duration_seconds) FILTER (
                            WHERE duration_seconds > 0
                              AND created_at >= NOW() - INTERVAL '7 days'
                        ), 0)::int AS AvgSeconds7d,
                        COALESCE(SUM(duration_seconds) FILTER (
                            WHERE created_at >= date_trunc('day', NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')
                                AT TIME ZONE 'Asia/Ho_Chi_Minh'
                        ), 0)::int AS TotalSecondsToday,
                        COALESCE(SUM(duration_seconds) FILTER (
                            WHERE created_at >= NOW() - INTERVAL '7 days'
                        ), 0)::int AS TotalSeconds7d
                    FROM public.family_os_demo_view
                    """,
                    cancellationToken: cancellationToken));

            return new FamilyOsDemoHouseViewsDto(
                "DEMO_FAMILY",
                stats.ViewsToday,
                stats.Views7d,
                stats.UniqueToday,
                stats.Unique7d,
                stats.LastViewAt is DateTime dt
                    ? new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc))
                    : null,
                stats.AvgSecondsToday,
                stats.AvgSeconds7d,
                stats.TotalSecondsToday,
                stats.TotalSeconds7d);
        }
        catch
        {
            // Table not migrated yet — approximate from shared demo viewer's refresh tokens.
        }

        var fallback = await conn.QuerySingleOrDefaultAsync<(
            int ViewsToday,
            int Views7d,
            DateTime? LastViewAt)>(
            new CommandDefinition(
                """
                SELECT
                    COUNT(*) FILTER (
                        WHERE rt.created_at >= date_trunc('day', NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')
                            AT TIME ZONE 'Asia/Ho_Chi_Minh'
                    )::int AS ViewsToday,
                    COUNT(*) FILTER (
                        WHERE rt.created_at >= NOW() - INTERVAL '7 days'
                    )::int AS Views7d,
                    MAX(rt.created_at) AS LastViewAt
                FROM public.refresh_tokens rt
                INNER JOIN public.users u ON u.id = rt.user_id
                INNER JOIN public.tenants t ON t.id = u.tenant_id
                WHERE t.tenant_code = 'DEMO_FAMILY'
                  AND u.username = 'demo'
                  AND u.deleted_at IS NULL
                """,
                cancellationToken: cancellationToken));

        return new FamilyOsDemoHouseViewsDto(
            "DEMO_FAMILY",
            fallback.ViewsToday,
            fallback.Views7d,
            fallback.ViewsToday,
            fallback.Views7d,
            fallback.LastViewAt is DateTime dt2
                ? new DateTimeOffset(DateTime.SpecifyKind(dt2, DateTimeKind.Utc))
                : null,
            0,
            0,
            0,
            0);
    }

    private sealed class TrialSignupRow
    {
        public Guid Id { get; init; }
        public Guid TenantId { get; init; }
        public string? TenantCode { get; init; }
        public Guid FamilyId { get; init; }
        public string? FamilyName { get; init; }
        public string? ParentDisplayName { get; init; }
        public string? Email { get; init; }
        public string? Username { get; init; }
        public int MemberCount { get; init; }
        public string? PlanCode { get; init; }
        public string? Status { get; init; }
        public DateTime? TrialEndsAt { get; init; }
        public string? Source { get; init; }
        public DateTime RegisteredAt { get; init; }
    }

    public async Task SetParentPinAsync(
        Guid familyId,
        SetParentPinRequest request,
        CancellationToken cancellationToken = default)
    {
        var pin = NormalizePin(request.Pin);
        var hash = BCrypt.Net.BCrypt.HashPassword(pin);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var n = await conn.ExecuteAsync(
            """
            UPDATE pack_family.family
            SET parent_pin_hash = @Hash, updated_at = NOW()
            WHERE id = @FamilyId AND tenant_id = @TenantId AND deleted_at IS NULL
            """,
            new { FamilyId = familyId, TenantId = _tenant.TenantId, Hash = hash });
        if (n == 0)
            throw new InvalidOperationException("Không tìm thấy gia đình.");
    }

    public async Task<bool> VerifyParentPinAsync(
        Guid familyId,
        VerifyParentPinRequest request,
        CancellationToken cancellationToken = default)
    {
        var pin = NormalizePin(request.Pin);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var hash = await conn.ExecuteScalarAsync<string?>(
            """
            SELECT parent_pin_hash FROM pack_family.family
            WHERE id = @FamilyId AND tenant_id = @TenantId AND deleted_at IS NULL
            """,
            new { FamilyId = familyId, TenantId = _tenant.TenantId });
        if (string.IsNullOrWhiteSpace(hash))
            return false; // no silent default PIN — must set server PIN first
        return BCrypt.Net.BCrypt.Verify(pin, hash);
    }

    private static async Task InsertChildIfAnyAsync(
        Npgsql.NpgsqlConnection conn,
        System.Data.Common.DbTransaction tx,
        Guid tenantId,
        Guid familyId,
        string? name,
        int sort,
        CancellationToken cancellationToken)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return;
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_family.membership (
                tenant_id, family_id, display_name, role_code, status, sort_order
            )
            VALUES (@TenantId, @FamilyId, @DisplayName, 'child', 'active', @Sort)
            """,
            new { TenantId = tenantId, FamilyId = familyId, DisplayName = trimmed, Sort = sort },
            tx);
    }

    private async Task<string> AllocateTenantCodeAsync(string familyName, CancellationToken cancellationToken)
    {
        var slug = new string(familyName
            .Normalize(NormalizationForm.FormD)
            .Where(c => char.IsLetterOrDigit(c))
            .Take(10)
            .ToArray())
            .ToUpperInvariant();
        if (slug.Length < 3) slug = "FAMILY";

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        for (var i = 0; i < 12; i++)
        {
            var suffix = RandomNumberGenerator.GetInt32(100, 999);
            var code = $"FOS_{slug}{suffix}";
            if (code.Length > 32) code = code[..32];
            var exists = await conn.ExecuteScalarAsync<bool>(
                "SELECT EXISTS(SELECT 1 FROM public.tenants WHERE tenant_code = @Code)",
                new { Code = code });
            if (!exists) return code;
        }

        return $"FOS_{Guid.NewGuid():N}"[..20].ToUpperInvariant();
    }

    private static string GenerateInviteCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<char> chars = stackalloc char[8];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        return new string(chars);
    }

    private static string NormalizePin(string? pin)
    {
        var value = (pin ?? "").Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^\d{4}$"))
            throw new InvalidOperationException("Mã PIN phụ huynh phải gồm đúng 4 chữ số.");
        return value;
    }

    private static string Require(string? value, string label)
    {
        var trimmed = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new InvalidOperationException($"{label} là bắt buộc.");
        return trimmed;
    }

    private static FamilyAuthSessionDto MapSession(LoginResponse session) =>
        new(
            session.AccessToken,
            session.RefreshToken,
            session.AccessTokenExpiresAt,
            session.User.Id,
            session.User.TenantId,
            session.User.TenantCode,
            session.User.Username,
            session.User.Email);

    private static string NormalizeSubscriptionStatus(SubscriptionRow row)
    {
        var status = (row.Status ?? "").Trim().ToLowerInvariant();
        if (status == FamilySubscriptionStatuses.Trial && row.TrialEndsAt is DateTime ends)
        {
            var endUtc = DateTime.SpecifyKind(ends, DateTimeKind.Utc);
            if (endUtc >= DateTime.UtcNow)
                return FamilySubscriptionStatuses.Trial;
            if (endUtc.AddDays(TrialGraceDays) >= DateTime.UtcNow)
                return FamilySubscriptionStatuses.TrialGrace;
            return FamilySubscriptionStatuses.Expired;
        }
        if (status == FamilySubscriptionStatuses.Active
            && row.CurrentPeriodEnd is DateTime periodEnd
            && periodEnd < DateTime.UtcNow)
            return FamilySubscriptionStatuses.Expired;
        if (status == FamilySubscriptionStatuses.PastDue
            && row.CurrentPeriodEnd is DateTime pastDueEnd
            && pastDueEnd.AddDays(3) < DateTime.UtcNow)
            return FamilySubscriptionStatuses.Expired;
        return status;
    }

    private sealed class InviteRow
    {
        public Guid Id { get; init; }
        public Guid TenantId { get; init; }
        public Guid FamilyId { get; init; }
        public string Code { get; init; } = "";
        public string RoleCode { get; init; } = "";
        public DateTime ExpiresAt { get; init; }
        public int MaxUses { get; init; }
        public int UsedCount { get; init; }
        public DateTime? RevokedAt { get; init; }
    }

    private sealed class SubscriptionRow
    {
        public Guid FamilyId { get; init; }
        public string PlanCode { get; init; } = "";
        public string Status { get; init; } = "";
        public DateTime? TrialEndsAt { get; init; }
        public DateTime? CurrentPeriodEnd { get; init; }
        public DateTime? CreatedAt { get; init; }
    }
}
