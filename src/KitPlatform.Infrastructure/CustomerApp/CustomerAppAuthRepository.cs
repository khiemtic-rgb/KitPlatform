using System.Security.Cryptography;
using System.Text;
using Dapper;
using KitPlatform.Infrastructure.Auth;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Infrastructure.Kernel.Party;

namespace KitPlatform.Infrastructure.CustomerApp;

internal sealed class CustomerAppAuthRepository
{
    private readonly IDbConnectionFactory _db;

    public CustomerAppAuthRepository(IDbConnectionFactory db) => _db = db;

    public async Task<TenantPhoneRow?> ResolveTenantAsync(string tenantCode, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS TenantId, tenant_code AS TenantCode
            FROM tenants
            WHERE tenant_code = @TenantCode AND deleted_at IS NULL AND status = 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<TenantPhoneRow>(sql, new { TenantCode = tenantCode });
    }

    public async Task<TenantPhoneRow?> ResolveTenantByIdAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS TenantId, tenant_code AS TenantCode
            FROM tenants
            WHERE id = @TenantId AND deleted_at IS NULL AND status = 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<TenantPhoneRow>(sql, new { TenantId = tenantId });
    }

    public async Task<CustomerAccountRecord?> FindAccountByPhoneAsync(
        Guid tenantId,
        string phone,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                ca.id AS AccountId,
                ca.customer_id AS CustomerId,
                ca.tenant_id AS TenantId,
                t.tenant_code AS TenantCode,
                c.full_name AS FullName,
                ca.phone AS Phone,
                ca.preferred_locale AS PreferredLocale,
                COALESCE(NULLIF(TRIM(c.pharmacy_relation), ''), 'member') AS PharmacyRelation,
                NULLIF(TRIM(c.acquisition_source), '') AS AcquisitionSource,
                NULLIF(TRIM(c.avatar_url), '') AS AvatarUrl
            FROM customer_accounts ca
            INNER JOIN customers c ON c.id = ca.customer_id AND c.deleted_at IS NULL
            INNER JOIN tenants t ON t.id = ca.tenant_id
            WHERE ca.tenant_id = @TenantId
              AND ca.phone = @Phone
              AND ca.status = 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<CustomerAccountRecord>(sql, new { TenantId = tenantId, Phone = phone });
    }

    public async Task<CustomerAccountRecord?> EnsureAccountForCustomerPhoneAsync(
        Guid tenantId,
        string tenantCode,
        string phone,
        CancellationToken cancellationToken)
    {
        var existing = await FindAccountByPhoneAsync(tenantId, phone, cancellationToken);
        if (existing is not null)
            return existing;

        const string findCustomerSql = """
            SELECT
                id AS CustomerId,
                full_name AS FullName,
                COALESCE(NULLIF(TRIM(pharmacy_relation), ''), 'member') AS PharmacyRelation,
                NULLIF(TRIM(acquisition_source), '') AS AcquisitionSource
            FROM customers
            WHERE tenant_id = @TenantId AND phone = @Phone AND deleted_at IS NULL
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        var customer = await conn.QuerySingleOrDefaultAsync<(
            Guid CustomerId,
            string FullName,
            string PharmacyRelation,
            string? AcquisitionSource)>(
            findCustomerSql,
            new { TenantId = tenantId, Phone = phone });

        if (customer.CustomerId == Guid.Empty)
            return null;

        const string insertSql = """
            INSERT INTO customer_accounts (tenant_id, customer_id, phone, is_verified)
            VALUES (@TenantId, @CustomerId, @Phone, FALSE)
            RETURNING id
            """;

        var accountId = await conn.QuerySingleAsync<Guid>(
            insertSql,
            new { TenantId = tenantId, customer.CustomerId, Phone = phone });

        return new CustomerAccountRecord(
            accountId,
            customer.CustomerId,
            tenantId,
            tenantCode,
            customer.FullName,
            phone,
            null,
            customer.PharmacyRelation,
            customer.AcquisitionSource,
            null);
    }

    public async Task<DateTime?> GetLatestOtpCreatedAtAsync(
        Guid tenantId,
        string phone,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT created_at
            FROM customer_otp_challenges
            WHERE tenant_id = @TenantId AND phone = @Phone
            ORDER BY created_at DESC
            LIMIT 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<DateTime?>(sql, new { TenantId = tenantId, Phone = phone });
    }

    public async Task<Guid> InsertOtpChallengeAsync(
        Guid tenantId,
        string phone,
        string codeHash,
        DateTime expiresAt,
        string? pilotCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO customer_otp_challenges (tenant_id, phone, code_hash, expires_at, pilot_code)
            VALUES (@TenantId, @Phone, @CodeHash, @ExpiresAt, @PilotCode)
            RETURNING id
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<Guid>(sql, new
        {
            TenantId = tenantId,
            Phone = phone,
            CodeHash = codeHash,
            ExpiresAt = expiresAt,
            PilotCode = pilotCode,
        });
    }

    public async Task<CustomerPhoneLookupRow?> FindCustomerByPhoneAsync(
        Guid tenantId,
        string phone,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS CustomerId,
                full_name AS FullName,
                COALESCE(NULLIF(TRIM(pharmacy_relation), ''), 'member') AS PharmacyRelation
            FROM customers
            WHERE tenant_id = @TenantId AND phone = @Phone AND deleted_at IS NULL
            LIMIT 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<CustomerPhoneLookupRow>(
            sql,
            new { TenantId = tenantId, Phone = phone });
    }

    public async Task<TenantCustomerAppAuthRow?> GetTenantAppAuthAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                counter_pin_hash AS CounterPinHash,
                invite_code_hash AS InviteCodeHash,
                invite_code_hint AS InviteCodeHint
            FROM tenant_customer_app_auth
            WHERE tenant_id = @TenantId
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<TenantCustomerAppAuthRow>(
            sql,
            new { TenantId = tenantId });
    }

    public async Task UpsertTenantAppAuthAsync(
        Guid tenantId,
        string? counterPinHash,
        string? inviteCodeHash,
        string? inviteCodeHint,
        bool clearCounterPin,
        bool clearInviteCode,
        Guid? updatedByUserId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO tenant_customer_app_auth (
                tenant_id, counter_pin_hash, invite_code_hash, invite_code_hint, updated_at, updated_by_user_id
            )
            VALUES (
                @TenantId, @CounterPinHash, @InviteCodeHash, @InviteCodeHint, NOW(), @UpdatedBy
            )
            ON CONFLICT (tenant_id) DO UPDATE SET
                counter_pin_hash = CASE
                    WHEN @ClearCounterPin THEN NULL
                    WHEN @CounterPinHash IS NOT NULL THEN @CounterPinHash
                    ELSE tenant_customer_app_auth.counter_pin_hash
                END,
                invite_code_hash = CASE
                    WHEN @ClearInviteCode THEN NULL
                    WHEN @InviteCodeHash IS NOT NULL THEN @InviteCodeHash
                    ELSE tenant_customer_app_auth.invite_code_hash
                END,
                invite_code_hint = CASE
                    WHEN @ClearInviteCode THEN NULL
                    WHEN @InviteCodeHint IS NOT NULL THEN @InviteCodeHint
                    ELSE tenant_customer_app_auth.invite_code_hint
                END,
                updated_at = NOW(),
                updated_by_user_id = @UpdatedBy
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            TenantId = tenantId,
            CounterPinHash = clearCounterPin ? null : counterPinHash,
            InviteCodeHash = clearInviteCode ? null : inviteCodeHash,
            InviteCodeHint = clearInviteCode ? null : inviteCodeHint,
            ClearCounterPin = clearCounterPin,
            ClearInviteCode = clearInviteCode,
            UpdatedBy = updatedByUserId,
        });
    }

    public async Task<Guid> CreateProspectCustomerAsync(
        Guid tenantId,
        string phone,
        string fullName,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        const string nextCodeSql = """
            SELECT COALESCE(MAX(
                CASE
                    WHEN customer_code ~ '^KH[0-9]+$'
                    THEN CAST(SUBSTRING(customer_code FROM 3) AS INT)
                END
            ), 0) + 1
            FROM customers
            WHERE tenant_id = @TenantId
            """;
        var next = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(nextCodeSql, new { TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        var customerCode = $"KH{next:D3}";
        var customerId = Guid.NewGuid();

        var partyId = await KernelPartyWriter.CreateCustomerPartyFirstAsync(
            conn,
            tx,
            tenantId,
            customerId,
            customerCode,
            fullName,
            phone,
            email: null,
            workspaceId: null,
            cancellationToken);

        const string insertSql = """
            INSERT INTO customers (
                id, tenant_id, party_id, customer_code, full_name, phone, email, status,
                allow_credit, credit_limit,
                acquisition_source, pharmacy_relation
            )
            VALUES (
                @CustomerId, @TenantId, @PartyId, @CustomerCode, @FullName, @Phone, NULL, 1,
                TRUE, NULL,
                'app_self', 'prospect'
            )
            """;

        await conn.ExecuteAsync(new CommandDefinition(insertSql, new
        {
            CustomerId = customerId,
            TenantId = tenantId,
            PartyId = partyId,
            CustomerCode = customerCode,
            FullName = fullName,
            Phone = phone,
        }, tx, cancellationToken: cancellationToken));

        await tx.CommitAsync(cancellationToken);
        return customerId;
    }

    public async Task MarkCustomerAsCounterMemberAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE customers
            SET acquisition_source = 'counter',
                pharmacy_relation = 'member',
                pharmacy_verified_at = COALESCE(pharmacy_verified_at, NOW()),
                pharmacy_verified_via = COALESCE(pharmacy_verified_via, 'staff_mark')
            WHERE id = @CustomerId AND tenant_id = @TenantId AND deleted_at IS NULL
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { CustomerId = customerId, TenantId = tenantId });
    }

    public async Task<CustomerAppLoginRequestRow?> FindPendingLoginRequestAsync(
        Guid tenantId,
        string phone,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                tenant_id AS TenantId,
                phone AS Phone,
                customer_id AS CustomerId,
                channel AS Channel,
                status AS Status,
                referral_code_used AS ReferralCodeUsed,
                otp_challenge_id AS OtpChallengeId,
                requested_at AS RequestedAt,
                reviewed_at AS ReviewedAt,
                reviewed_by_user_id AS ReviewedByUserId,
                reject_reason AS RejectReason
            FROM customer_app_login_requests
            WHERE tenant_id = @TenantId
              AND phone = @Phone
              AND status = 'pending'
            ORDER BY requested_at DESC
            LIMIT 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<CustomerAppLoginRequestRow>(
            sql,
            new { TenantId = tenantId, Phone = phone });
    }

    public async Task<Guid> InsertLoginRequestAsync(
        Guid tenantId,
        string phone,
        Guid? customerId,
        string channel,
        string status,
        string? referralCodeUsed,
        Guid? otpChallengeId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO customer_app_login_requests (
                tenant_id, phone, customer_id, channel, status, referral_code_used, otp_challenge_id
            )
            VALUES (
                @TenantId, @Phone, @CustomerId, @Channel, @Status, @ReferralCodeUsed, @OtpChallengeId
            )
            RETURNING id
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<Guid>(sql, new
        {
            TenantId = tenantId,
            Phone = phone,
            CustomerId = customerId,
            Channel = channel,
            Status = status,
            ReferralCodeUsed = referralCodeUsed,
            OtpChallengeId = otpChallengeId,
        });
    }

    public async Task<IReadOnlyList<CustomerAppLoginRequestListRow>> ListLoginRequestsAsync(
        Guid tenantId,
        string? status,
        CancellationToken cancellationToken)
    {
        var sql = """
            SELECT
                r.id AS Id,
                r.phone AS Phone,
                r.customer_id AS CustomerId,
                c.full_name AS CustomerName,
                r.channel AS Channel,
                r.status AS Status,
                r.referral_code_used AS ReferralCodeUsed,
                r.requested_at AS RequestedAt,
                r.reviewed_at AS ReviewedAt,
                r.reject_reason AS RejectReason
            FROM customer_app_login_requests r
            LEFT JOIN customers c ON c.id = r.customer_id AND c.deleted_at IS NULL
            WHERE r.tenant_id = @TenantId
            """;
        if (!string.IsNullOrWhiteSpace(status))
            sql += " AND r.status = @Status";
        sql += " ORDER BY r.requested_at DESC LIMIT 100";

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<CustomerAppLoginRequestListRow>(
            sql,
            new { TenantId = tenantId, Status = status?.Trim().ToLowerInvariant() });
        return rows.AsList();
    }

    public async Task<CustomerAppLoginRequestRow?> GetLoginRequestAsync(
        Guid tenantId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                tenant_id AS TenantId,
                phone AS Phone,
                customer_id AS CustomerId,
                channel AS Channel,
                status AS Status,
                referral_code_used AS ReferralCodeUsed,
                otp_challenge_id AS OtpChallengeId,
                requested_at AS RequestedAt,
                reviewed_at AS ReviewedAt,
                reviewed_by_user_id AS ReviewedByUserId,
                reject_reason AS RejectReason
            FROM customer_app_login_requests
            WHERE tenant_id = @TenantId AND id = @Id
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<CustomerAppLoginRequestRow>(
            sql,
            new { TenantId = tenantId, Id = requestId });
    }

    public async Task MarkLoginRequestApprovedAsync(
        Guid requestId,
        Guid otpChallengeId,
        Guid reviewedByUserId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE customer_app_login_requests
            SET status = 'approved',
                otp_challenge_id = @OtpChallengeId,
                reviewed_at = NOW(),
                reviewed_by_user_id = @ReviewedBy
            WHERE id = @Id AND status = 'pending'
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            Id = requestId,
            OtpChallengeId = otpChallengeId,
            ReviewedBy = reviewedByUserId,
        });
    }

    public async Task MarkLoginRequestRejectedAsync(
        Guid requestId,
        Guid reviewedByUserId,
        string? reason,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE customer_app_login_requests
            SET status = 'rejected',
                reviewed_at = NOW(),
                reviewed_by_user_id = @ReviewedBy,
                reject_reason = @Reason
            WHERE id = @Id AND status = 'pending'
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            Id = requestId,
            ReviewedBy = reviewedByUserId,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
        });
    }

    public static string HashSecret(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim())));

    public static string NormalizeInviteCode(string? code) =>
        (code ?? string.Empty).Trim().ToUpperInvariant();

    public async Task<PilotOtpRow?> GetActivePilotOtpAsync(
        Guid tenantId,
        string phone,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT pilot_code AS Code, expires_at AS ExpiresAt, created_at AS CreatedAt
            FROM customer_otp_challenges
            WHERE tenant_id = @TenantId
              AND phone = @Phone
              AND consumed_at IS NULL
              AND expires_at > NOW()
              AND pilot_code IS NOT NULL
            ORDER BY created_at DESC
            LIMIT 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<PilotOtpRow>(sql, new { TenantId = tenantId, Phone = phone });
    }

    public async Task<OtpChallengeRow?> GetActiveOtpChallengeAsync(
        Guid tenantId,
        string phone,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS Id, code_hash AS CodeHash, expires_at AS ExpiresAt, attempt_count AS AttemptCount
            FROM customer_otp_challenges
            WHERE tenant_id = @TenantId
              AND phone = @Phone
              AND consumed_at IS NULL
              AND expires_at > NOW()
            ORDER BY created_at DESC
            LIMIT 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<OtpChallengeRow>(sql, new { TenantId = tenantId, Phone = phone });
    }

    public async Task IncrementOtpAttemptAsync(Guid challengeId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE customer_otp_challenges
            SET attempt_count = attempt_count + 1
            WHERE id = @Id
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { Id = challengeId });
    }

    public async Task ConsumeOtpChallengeAsync(Guid challengeId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE customer_otp_challenges
            SET consumed_at = NOW(),
                pilot_code = NULL
            WHERE id = @Id
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { Id = challengeId });
    }

    public async Task MarkAccountVerifiedAsync(Guid accountId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE customer_accounts
            SET is_verified = TRUE,
                last_login_at = NOW(),
                first_login_at = COALESCE(first_login_at, NOW())
            WHERE id = @AccountId
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { AccountId = accountId });
    }

    public async Task StoreRefreshTokenAsync(
        Guid accountId,
        string tokenHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO customer_refresh_tokens (account_id, token_hash, expires_at)
            VALUES (@AccountId, @TokenHash, @ExpiresAt)
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { AccountId = accountId, TokenHash = tokenHash, ExpiresAt = expiresAt });
    }

    public async Task<Guid?> FindAccountIdByRefreshTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT account_id
            FROM customer_refresh_tokens
            WHERE token_hash = @TokenHash
              AND revoked_at IS NULL
              AND expires_at > NOW()
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Guid?>(sql, new { TokenHash = tokenHash });
    }

    public async Task RevokeRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE customer_refresh_tokens
            SET revoked_at = NOW()
            WHERE token_hash = @TokenHash AND revoked_at IS NULL
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { TokenHash = tokenHash });
    }

    public async Task<CustomerAccountRecord?> FindAccountByIdAsync(Guid accountId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                ca.id AS AccountId,
                ca.customer_id AS CustomerId,
                ca.tenant_id AS TenantId,
                t.tenant_code AS TenantCode,
                c.full_name AS FullName,
                ca.phone AS Phone,
                ca.preferred_locale AS PreferredLocale,
                COALESCE(NULLIF(TRIM(c.pharmacy_relation), ''), 'member') AS PharmacyRelation,
                NULLIF(TRIM(c.acquisition_source), '') AS AcquisitionSource,
                NULLIF(TRIM(c.avatar_url), '') AS AvatarUrl
            FROM customer_accounts ca
            INNER JOIN customers c ON c.id = ca.customer_id AND c.deleted_at IS NULL
            INNER JOIN tenants t ON t.id = ca.tenant_id
            WHERE ca.id = @AccountId AND ca.status = 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<CustomerAccountRecord>(sql, new { AccountId = accountId });
    }

    /// <summary>CRM pharmacy_relation for Soft Refill / app gates (default member if unset).</summary>
    public async Task<string> GetPharmacyRelationAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(NULLIF(TRIM(c.pharmacy_relation), ''), 'member')
            FROM customers c
            WHERE c.tenant_id = @TenantId
              AND c.id = @CustomerId
              AND c.deleted_at IS NULL
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var relation = await conn.ExecuteScalarAsync<string?>(
            sql,
            new { TenantId = tenantId, CustomerId = customerId });
        return string.IsNullOrWhiteSpace(relation) ? "member" : relation.Trim().ToLowerInvariant();
    }

    public async Task<bool> UpdatePreferredLocaleAsync(
        Guid accountId,
        string locale,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE customer_accounts ca
            SET preferred_locale = @Locale
            WHERE ca.id = @AccountId
              AND ca.status = 1
              AND EXISTS (
                  SELECT 1
                  FROM platform_locales pl
                  WHERE pl.locale_code = @Locale
                    AND pl.status = 1
              )
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync(sql, new { AccountId = accountId, Locale = locale });
        return rows > 0;
    }

    public async Task<bool> UpdateCustomerFullNameAsync(
        Guid accountId,
        string fullName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE customers c
            SET full_name = @FullName,
                updated_at = NOW()
            FROM customer_accounts ca
            WHERE ca.id = @AccountId
              AND ca.customer_id = c.id
              AND ca.tenant_id = c.tenant_id
              AND ca.status = 1
              AND c.deleted_at IS NULL
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync(sql, new { AccountId = accountId, FullName = fullName });
        return rows > 0;
    }

    public async Task<bool> UpdateCustomerAvatarUrlAsync(
        Guid accountId,
        string avatarUrl,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE customers c
            SET avatar_url = @AvatarUrl,
                updated_at = NOW()
            FROM customer_accounts ca
            WHERE ca.id = @AccountId
              AND ca.customer_id = c.id
              AND ca.tenant_id = c.tenant_id
              AND ca.status = 1
              AND c.deleted_at IS NULL
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync(sql, new { AccountId = accountId, AvatarUrl = avatarUrl });
        return rows > 0;
    }

    /// <summary>App self-claim (QR / invite): promote to member; keep counter acquisition if already set.</summary>
    public async Task<bool> MarkPharmacyMemberFromAppAsync(
        Guid tenantId,
        Guid customerId,
        string verifiedVia,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE customers
            SET pharmacy_relation = 'member',
                pharmacy_verified_at = COALESCE(pharmacy_verified_at, NOW()),
                pharmacy_verified_via = @VerifiedVia,
                acquisition_source = CASE
                    WHEN COALESCE(NULLIF(TRIM(acquisition_source), ''), '') IN ('', 'app_self')
                        THEN 'qr_claim'
                    ELSE acquisition_source
                END,
                updated_at = NOW()
            WHERE id = @CustomerId
              AND tenant_id = @TenantId
              AND deleted_at IS NULL
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync(
            sql,
            new { CustomerId = customerId, TenantId = tenantId, VerifiedVia = verifiedVia });
        return rows > 0;
    }

    public static string HashOtp(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim())));

    public static string NormalizePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("84") && digits.Length >= 11)
            digits = "0" + digits[2..];
        return digits;
    }

    public static string GenerateOtpCode()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString("D6");
    }
}
