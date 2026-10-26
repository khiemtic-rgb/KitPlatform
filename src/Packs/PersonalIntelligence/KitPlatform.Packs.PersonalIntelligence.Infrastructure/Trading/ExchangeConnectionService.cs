using System.Data;
using System.Text.Json;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;

public sealed class ExchangeConnectionService : IExchangeConnectionService
{
    readonly ExchangeConnectionStore _store;
    readonly IExchangeAdapterRegistry _adapters;
    readonly IExchangeCredentialKeyRing _keys;
    readonly IExchangeBackfill _backfill;

    public ExchangeConnectionService(
        IDbConnectionFactory db,
        ITenantContext tenant,
        IExchangeAdapterRegistry adapters,
        IExchangeCredentialKeyRing keys,
        IExchangeBackfill backfill)
    {
        _store = new ExchangeConnectionStore(db, tenant);
        _adapters = adapters;
        _keys = keys;
        _backfill = backfill;
    }

    public async Task<ExchangeProbeDto> TestAsync(ExchangeCredentialRequest request, CancellationToken cancellationToken)
    {
        var credential = Normalize(request);
        try
        {
            var probe = await _adapters.Require(credential.ExchangeCode).ProbeAsync(ToCredential(credential), cancellationToken);
            await _store.RecordAsync(null, "test", probe.Accepted ? "ok" : "rejected", probe.Accepted ? null : "rejected", cancellationToken);
            return probe;
        }
        catch (ExchangeConnectionException ex)
        {
            await _store.RecordAsync(null, "test", "rejected", AuditCode(ex.Code), cancellationToken);
            throw;
        }
    }

    public async Task<ExchangeAccountViewDto> SaveAsync(ExchangeCredentialRequest request, CancellationToken cancellationToken)
    {
        var credential = Normalize(request);
        try
        {
            var key = _keys.RequireCurrent();
            var probe = await _adapters.Require(credential.ExchangeCode).ProbeAsync(ToCredential(credential), cancellationToken);
            if (!probe.Accepted)
                throw new ExchangeConnectionException(400, "rejected", "Kiểm tra sàn chưa hợp lệ. Không lưu credential.");
            if (!probe.ReadOnlyVerified && !request.AcknowledgeUnverifiedPermissions)
                throw new ExchangeConnectionException(400, "unverified", probe.Message);
            var envelope = ExchangeCredentialProtector.Encrypt(Pack(credential.ApiKey, credential.ApiSecret, credential.Passphrase), key);
            var connectionId = await _store.SaveAsync(probe, ExchangeCredentialProtector.Mask(credential.ApiKey), ExchangeCredentialProtector.Fingerprint(credential.ApiKey), envelope, cancellationToken);
            await _backfill.EnqueueInitialAsync(connectionId, cancellationToken);
            return await _store.GetAccountAsync(connectionId, cancellationToken);
        }
        catch (ExchangeConnectionException ex) when (ex.Code is "key_missing" or "rejected" or "unverified" or "unsupported" or "invalid_key" or "passphrase_required" or "ciphertext_invalid")
        {
            await _store.RecordAsync(null, "save", "rejected", AuditCode(ex.Code), cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<ExchangeConnectionDto>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await _store.ListAsync(cancellationToken);
        await _store.RecordAsync(null, "list", "ok", null, cancellationToken);
        return rows;
    }

    public async Task<ExchangeAccountViewDto> GetAccountAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        try
        {
            var view = await _store.GetAccountAsync(connectionId, cancellationToken);
            await _store.RecordAsync(connectionId, "read", "ok", null, cancellationToken);
            return view;
        }
        catch (ExchangeConnectionException ex)
        {
            await _store.RecordAsync(connectionId, "read", "rejected", AuditCode(ex.Code), cancellationToken);
            throw;
        }
    }

    public async Task<ExchangeAccountViewDto> RefreshAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        try
        {
            var stored = await _store.RequireSecretAsync(connectionId, cancellationToken);
            var unpacked = Unpack(ExchangeCredentialProtector.Decrypt(stored.Envelope, _keys.RequireCurrent()));
            try
            {
                var probe = await _adapters.Require(stored.ExchangeCode).ProbeAsync(new ExchangeCredential
                {
                    Environment = stored.Environment,
                    ApiKey = unpacked.ApiKey,
                    ApiSecret = unpacked.ApiSecret,
                    Passphrase = unpacked.Passphrase,
                }, cancellationToken);
                await _store.AppendSnapshotAsync(connectionId, probe, cancellationToken);
            }
            catch (ExchangeConnectionException ex)
            {
                await _store.MarkRejectedAsync(connectionId, ex.Message, cancellationToken);
                throw;
            }

            await _store.RecordAsync(connectionId, "refresh", "ok", null, cancellationToken);
            return await _store.GetAccountAsync(connectionId, cancellationToken);
        }
        catch (ExchangeConnectionException ex)
        {
            await _store.RecordAsync(connectionId, "refresh", "rejected", AuditCode(ex.Code), cancellationToken);
            throw;
        }
    }

    public async Task DisconnectAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        await _backfill.PauseOpenAsync(connectionId, "Kết nối đã ngắt. Credential đã bị xóa. Checkpoint được giữ.", cancellationToken);
        await _store.DisconnectAsync(connectionId, cancellationToken);
    }

    static NormalizedCredential Normalize(ExchangeCredentialRequest request)
    {
        var code = (request.ExchangeCode ?? "").Trim().ToUpperInvariant();
        if (code is not ("BYBIT" or "BINANCE" or "OKX"))
            throw new ExchangeConnectionException(400, "unsupported", "Sàn không được hỗ trợ. Chỉ Bybit, Binance và OKX.");
        var environment = (request.Environment ?? "").Trim().ToUpperInvariant();
        if (environment is not ("MAINNET" or "TESTNET"))
            throw new ExchangeConnectionException(400, "unsupported", "Môi trường không được hỗ trợ. Chỉ MAINNET hoặc TESTNET.");
        var apiKey = (request.ApiKey ?? "").Trim();
        var apiSecret = request.ApiSecret ?? "";
        if (apiKey.Length < 8 || apiKey.Length > 256 || apiSecret.Length < 8 || apiSecret.Length > 256)
            throw new ExchangeConnectionException(400, "invalid_key", "API key hoặc API secret không hợp lệ.");
        var passphrase = string.IsNullOrWhiteSpace(request.Passphrase) ? null : request.Passphrase.Trim();
        if (code == "OKX" && (passphrase is null || passphrase.Length > 128))
            throw new ExchangeConnectionException(400, "passphrase_required", "OKX yêu cầu passphrase.");
        if (code != "OKX")
            passphrase = null;
        return new NormalizedCredential(code, environment, apiKey, apiSecret, passphrase);
    }

    static ExchangeCredential ToCredential(NormalizedCredential credential) => new()
    {
        Environment = credential.Environment,
        ApiKey = credential.ApiKey,
        ApiSecret = credential.ApiSecret,
        Passphrase = credential.Passphrase,
    };

    static string Pack(string apiKey, string apiSecret, string? passphrase) =>
        JsonSerializer.Serialize(new ExchangeCredentialPayload { ApiKey = apiKey, ApiSecret = apiSecret, Passphrase = passphrase });

    static ExchangeCredentialPayload Unpack(string plaintext)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<ExchangeCredentialPayload>(plaintext);
            if (payload is null || string.IsNullOrWhiteSpace(payload.ApiKey) || string.IsNullOrWhiteSpace(payload.ApiSecret))
                throw new ExchangeConnectionException(503, "ciphertext_invalid", "Không giải mã được credential đã lưu.");
            return payload;
        }
        catch (JsonException)
        {
            throw new ExchangeConnectionException(503, "ciphertext_invalid", "Không giải mã được credential đã lưu.");
        }
    }

    sealed record NormalizedCredential(string ExchangeCode, string Environment, string ApiKey, string ApiSecret, string? Passphrase);

    static string AuditCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 64)
            return "rejected";
        foreach (var character in code)
        {
            if (character is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '_'))
                return "rejected";
        }

        return code;
    }
}

sealed class ExchangeCredentialPayload
{
    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";
    public string? Passphrase { get; set; }
}

sealed class ExchangeConnectionStore
{
    readonly IDbConnectionFactory _db;
    readonly ITenantContext _tenant;

    public ExchangeConnectionStore(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    Guid TenantId => _tenant.TenantId;
    Guid UserId => _tenant.UserId;

    public async Task<Guid> SaveAsync(
        ExchangeProbeDto probe,
        string mask,
        string fingerprint,
        ExchangeCredentialEnvelope envelope,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var personId = await EnsurePersonAsync(conn, tx, cancellationToken);
        var connectionId = await conn.QuerySingleAsync<Guid>(
            """
            INSERT INTO pack_personal_intelligence.exchange_connection (
                tenant_id, person_id, exchange_code, environment, status,
                api_key_mask, api_key_fingerprint, key_id, nonce, ciphertext, tag,
                read_only_verified, permission_names, external_user_id, margin_mode,
                unified_margin_status, uta, last_checked_at, last_error, last_probe_status, disconnected_at
            )
            VALUES (
                @TenantId, @PersonId, @ExchangeCode, @Environment, 'CONNECTED',
                @Mask, @Fingerprint, @KeyId, @Nonce, @Ciphertext, @Tag,
                @ReadOnlyVerified, CAST(@PermissionNames AS jsonb), @ExternalUserId, @MarginMode,
                @UnifiedMarginStatus, @Uta, @CheckedAt, @BalanceError, @ProbeStatus, NULL
            )
            ON CONFLICT (tenant_id, person_id, exchange_code, environment) DO UPDATE SET
                status = 'CONNECTED',
                api_key_mask = EXCLUDED.api_key_mask,
                api_key_fingerprint = EXCLUDED.api_key_fingerprint,
                key_id = EXCLUDED.key_id,
                nonce = EXCLUDED.nonce,
                ciphertext = EXCLUDED.ciphertext,
                tag = EXCLUDED.tag,
                read_only_verified = EXCLUDED.read_only_verified,
                permission_names = EXCLUDED.permission_names,
                external_user_id = EXCLUDED.external_user_id,
                margin_mode = EXCLUDED.margin_mode,
                unified_margin_status = EXCLUDED.unified_margin_status,
                uta = EXCLUDED.uta,
                last_checked_at = EXCLUDED.last_checked_at,
                last_error = EXCLUDED.last_error,
                last_probe_status = EXCLUDED.last_probe_status,
                disconnected_at = NULL,
                updated_at = NOW()
            RETURNING connection_id
            """,
            new
            {
                TenantId,
                PersonId = personId,
                probe.ExchangeCode,
                probe.Environment,
                Mask = mask,
                Fingerprint = fingerprint,
                envelope.KeyId,
                envelope.Nonce,
                envelope.Ciphertext,
                envelope.Tag,
                probe.ReadOnlyVerified,
                PermissionNames = JsonSerializer.Serialize(probe.PermissionNames),
                probe.ExternalUserId,
                probe.MarginMode,
                probe.UnifiedMarginStatus,
                probe.Uta,
                probe.CheckedAt,
                probe.BalanceError,
                ProbeStatus = probe.BalanceError is null ? "OK" : "BALANCE_UNAVAILABLE",
            },
            tx);
        await InsertSnapshotAsync(conn, tx, TenantId, connectionId, probe);
        await InsertAuditAsync(conn, tx, connectionId, "save", "ok", null, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return connectionId;
    }

    public async Task AppendSnapshotAsync(Guid connectionId, ExchangeProbeDto probe, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var updated = await conn.ExecuteAsync(
            """
            UPDATE pack_personal_intelligence.exchange_connection
            SET read_only_verified = @ReadOnlyVerified,
                permission_names = CAST(@PermissionNames AS jsonb),
                external_user_id = @ExternalUserId,
                margin_mode = @MarginMode,
                unified_margin_status = @UnifiedMarginStatus,
                uta = @Uta,
                last_checked_at = @CheckedAt,
                last_error = @BalanceError,
                last_probe_status = @ProbeStatus,
                updated_at = NOW()
            WHERE connection_id = @ConnectionId
              AND tenant_id = @TenantId
              AND person_id = (
                    SELECT person_id FROM pack_personal_intelligence.person
                    WHERE user_id = @UserId AND tenant_id = @TenantId AND deleted_at IS NULL
              )
              AND status = 'CONNECTED'
            """,
            new
            {
                ConnectionId = connectionId,
                TenantId,
                UserId,
                probe.ReadOnlyVerified,
                PermissionNames = JsonSerializer.Serialize(probe.PermissionNames),
                probe.ExternalUserId,
                probe.MarginMode,
                probe.UnifiedMarginStatus,
                probe.Uta,
                probe.CheckedAt,
                probe.BalanceError,
                ProbeStatus = probe.BalanceError is null ? "OK" : "BALANCE_UNAVAILABLE",
            },
            tx);
        if (updated != 1)
            throw new ExchangeConnectionException(404, "not_found", "Không tìm thấy kết nối còn hiệu lực của người dùng này.");
        await InsertSnapshotAsync(conn, tx, TenantId, connectionId, probe);
        await tx.CommitAsync(cancellationToken);
    }

    public async Task MarkRejectedAsync(Guid connectionId, string message, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            UPDATE pack_personal_intelligence.exchange_connection
            SET last_checked_at = NOW(),
                last_error = @Message,
                last_probe_status = 'REJECTED',
                updated_at = NOW()
            WHERE connection_id = @ConnectionId
              AND tenant_id = @TenantId
              AND status = 'CONNECTED'
              AND person_id = (
                    SELECT person_id FROM pack_personal_intelligence.person
                    WHERE user_id = @UserId AND tenant_id = @TenantId AND deleted_at IS NULL
              )
            """,
            new { ConnectionId = connectionId, TenantId, UserId, Message = message });
    }

    public async Task<StoredExchangeSecret> RequireSecretAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<SecretRow>(
            """
            SELECT exchange_code AS ExchangeCode,
                   environment AS Environment,
                   key_id AS KeyId,
                   nonce AS Nonce,
                   ciphertext AS Ciphertext,
                   tag AS Tag,
                   status AS Status
            FROM pack_personal_intelligence.exchange_connection
            WHERE connection_id = @ConnectionId
              AND tenant_id = @TenantId
              AND person_id = (
                    SELECT person_id FROM pack_personal_intelligence.person
                    WHERE user_id = @UserId AND tenant_id = @TenantId AND deleted_at IS NULL
              )
            """,
            new { ConnectionId = connectionId, TenantId, UserId });
        if (row is null)
            throw new ExchangeConnectionException(404, "not_found", "Không tìm thấy kết nối của người dùng này.");
        if (row.Status != "CONNECTED" || row.Nonce is null || row.Ciphertext is null || row.Tag is null || row.KeyId is null)
            throw new ExchangeConnectionException(409, "disconnected", "Kết nối đã bị ngắt. Credential không còn để làm mới.");
        return new StoredExchangeSecret(row.ExchangeCode, row.Environment, new ExchangeCredentialEnvelope(row.KeyId, row.Nonce, row.Ciphertext, row.Tag));
    }

    public async Task<IReadOnlyList<ExchangeConnectionDto>> ListAsync(CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<ConnectionRow>(ConnectionSql + "\n" + """
            WHERE c.tenant_id = @TenantId
              AND c.person_id = (
                    SELECT person_id FROM pack_personal_intelligence.person
                    WHERE user_id = @UserId AND tenant_id = @TenantId AND deleted_at IS NULL
              )
            ORDER BY c.created_at DESC
            """, new { TenantId, UserId });
        return rows.Select(Map).ToArray();
    }

    public async Task<ExchangeAccountViewDto> GetAccountAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var connection = await conn.QuerySingleOrDefaultAsync<ConnectionRow>(ConnectionSql + "\n" + """
            WHERE c.connection_id = @ConnectionId
              AND c.tenant_id = @TenantId
              AND c.person_id = (
                    SELECT person_id FROM pack_personal_intelligence.person
                    WHERE user_id = @UserId AND tenant_id = @TenantId AND deleted_at IS NULL
              )
            """, new { ConnectionId = connectionId, TenantId, UserId });
        if (connection is null)
            throw new ExchangeConnectionException(404, "not_found", "Không tìm thấy kết nối của người dùng này.");

        var snapshot = await conn.QuerySingleOrDefaultAsync<SnapshotRow>(
            """
            SELECT snapshot_id AS SnapshotId,
                   captured_at AS CapturedAt,
                   source_endpoints AS SourceEndpoints,
                   account_type AS AccountType,
                   margin_mode AS MarginMode,
                   unified_margin_status AS UnifiedMarginStatus,
                   total_equity AS TotalEquity,
                   total_wallet_balance AS TotalWalletBalance,
                   total_available_balance AS TotalAvailableBalance,
                   balance_error AS BalanceError
            FROM pack_personal_intelligence.exchange_account_snapshot
            WHERE connection_id = @ConnectionId
              AND tenant_id = @TenantId
            ORDER BY captured_at DESC
            LIMIT 1
            """,
            new { ConnectionId = connectionId, TenantId });
        var assets = new List<ExchangeAssetDto>();
        if (snapshot is not null)
        {
            var assetRows = await conn.QueryAsync<AssetRow>(
                """
                SELECT coin AS Coin,
                       wallet_balance AS WalletBalance,
                       equity AS Equity,
                       usd_value AS UsdValue,
                       available_to_withdraw AS AvailableToWithdraw
                FROM pack_personal_intelligence.exchange_asset_balance
                WHERE snapshot_id = @SnapshotId
                  AND tenant_id = @TenantId
                ORDER BY coin
                """,
                new { snapshot.SnapshotId, TenantId });
            assets.AddRange(assetRows.Select(row => new ExchangeAssetDto
            {
                Coin = row.Coin,
                WalletBalance = row.WalletBalance,
                Equity = row.Equity,
                UsdValue = row.UsdValue,
                AvailableToWithdraw = row.AvailableToWithdraw,
            }));
        }

        return new ExchangeAccountViewDto
        {
            Connection = Map(connection),
            SnapshotId = snapshot?.SnapshotId,
            CapturedAt = ToOffset(snapshot?.CapturedAt),
            Freshness = Freshness(connection.Status, snapshot?.CapturedAt),
            SourceEndpoints = snapshot?.SourceEndpoints,
            AccountType = snapshot?.AccountType,
            MarginMode = snapshot?.MarginMode ?? connection.MarginMode,
            UnifiedMarginStatus = snapshot?.UnifiedMarginStatus ?? connection.UnifiedMarginStatus,
            TotalEquity = snapshot?.TotalEquity,
            TotalWalletBalance = snapshot?.TotalWalletBalance,
            TotalAvailableBalance = snapshot?.TotalAvailableBalance,
            BalanceError = snapshot?.BalanceError,
            Assets = assets,
        };
    }

    public async Task DisconnectAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var updated = await conn.ExecuteAsync(
            """
            UPDATE pack_personal_intelligence.exchange_connection
            SET status = 'DISCONNECTED',
                nonce = NULL,
                ciphertext = NULL,
                tag = NULL,
                key_id = NULL,
                disconnected_at = NOW(),
                updated_at = NOW()
            WHERE connection_id = @ConnectionId
              AND tenant_id = @TenantId
              AND status = 'CONNECTED'
              AND person_id = (
                    SELECT person_id FROM pack_personal_intelligence.person
                    WHERE user_id = @UserId AND tenant_id = @TenantId AND deleted_at IS NULL
              )
            """,
            new { ConnectionId = connectionId, TenantId, UserId });
        if (updated != 1)
        {
            await InsertAuditAsync(conn, null, connectionId, "disconnect", "rejected", "not_found", cancellationToken);
            throw new ExchangeConnectionException(404, "not_found", "Không tìm thấy kết nối còn hiệu lực của người dùng này.");
        }

        await InsertAuditAsync(conn, null, connectionId, "disconnect", "ok", null, cancellationToken);
    }

    public Task RecordAsync(Guid? connectionId, string operation, string result, string? errorCode, CancellationToken cancellationToken) =>
        RecordCoreAsync(connectionId, operation, result, errorCode, cancellationToken);

    async Task RecordCoreAsync(Guid? connectionId, string operation, string result, string? errorCode, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await InsertAuditAsync(conn, null, connectionId, operation, result, errorCode, cancellationToken);
    }

    Task InsertAuditAsync(
        System.Data.IDbConnection conn,
        System.Data.IDbTransaction? tx,
        Guid? connectionId,
        string operation,
        string result,
        string? errorCode,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return conn.ExecuteAsync(
            """
            INSERT INTO pack_personal_intelligence.exchange_connection_audit
                (tenant_id, actor_user_id, connection_id, operation, result, error_code)
            VALUES (@TenantId, @UserId, @ConnectionId, @Operation, @Result, @ErrorCode)
            """,
            new
            {
                TenantId,
                UserId,
                ConnectionId = connectionId,
                Operation = operation,
                Result = result,
                ErrorCode = errorCode,
            },
            tx);
    }

    async Task<Guid> EnsurePersonAsync(IDbConnection conn, IDbTransaction tx, CancellationToken cancellationToken)
    {
        var personId = await conn.QuerySingleOrDefaultAsync<Guid?>(
            """
            SELECT person_id
            FROM pack_personal_intelligence.person
            WHERE user_id = @UserId AND tenant_id = @TenantId AND deleted_at IS NULL
            """,
            new { UserId, TenantId },
            tx);
        if (personId is not null)
            return personId.Value;
        return await conn.QuerySingleAsync<Guid>(
            """
            INSERT INTO pack_personal_intelligence.person (user_id, tenant_id, timezone, created_by, updated_by)
            VALUES (@UserId, @TenantId, 'UTC', @UserId, @UserId)
            RETURNING person_id
            """,
            new { UserId, TenantId },
            tx);
    }

    static async Task InsertSnapshotAsync(
        IDbConnection conn, IDbTransaction tx, Guid tenantId, Guid connectionId, ExchangeProbeDto probe)
    {
        var snapshotId = await conn.QuerySingleAsync<Guid>(
            """
            INSERT INTO pack_personal_intelligence.exchange_account_snapshot (
                connection_id, tenant_id, captured_at, source_endpoints, account_type, margin_mode,
                unified_margin_status, total_equity, total_wallet_balance, total_available_balance, balance_error
            )
            VALUES (
                @ConnectionId, @TenantId, @CheckedAt, @SourceEndpoints, @AccountType, @MarginMode,
                @UnifiedMarginStatus, @TotalEquity, @TotalWalletBalance, @TotalAvailableBalance, @BalanceError
            )
            RETURNING snapshot_id
            """,
            new
            {
                ConnectionId = connectionId,
                TenantId = tenantId,
                probe.CheckedAt,
                probe.SourceEndpoints,
                probe.AccountType,
                probe.MarginMode,
                probe.UnifiedMarginStatus,
                probe.TotalEquity,
                probe.TotalWalletBalance,
                probe.TotalAvailableBalance,
                probe.BalanceError,
            },
            tx);
        foreach (var asset in probe.Assets)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO pack_personal_intelligence.exchange_asset_balance (
                    snapshot_id, tenant_id, coin, wallet_balance, equity, usd_value, available_to_withdraw
                )
                VALUES (
                    @SnapshotId, @TenantId, @Coin, @WalletBalance, @Equity, @UsdValue, @AvailableToWithdraw
                )
                """,
                new
                {
                    SnapshotId = snapshotId,
                    TenantId = tenantId,
                    asset.Coin,
                    asset.WalletBalance,
                    asset.Equity,
                    asset.UsdValue,
                    asset.AvailableToWithdraw,
                },
                tx);
        }
    }

    static ExchangeConnectionDto Map(ConnectionRow row) => new()
    {
        ConnectionId = row.ConnectionId,
        ExchangeCode = row.ExchangeCode,
        Environment = row.Environment,
        Status = row.Status,
        ApiKeyMask = row.ApiKeyMask,
        ReadOnlyVerified = row.ReadOnlyVerified,
        PermissionNames = JsonSerializer.Deserialize<List<string>>(row.PermissionNames) ?? [],
        ExternalUserId = row.ExternalUserId,
        MarginMode = row.MarginMode,
        UnifiedMarginStatus = row.UnifiedMarginStatus,
        Uta = row.Uta,
        LastCheckedAt = ToOffset(row.LastCheckedAt),
        LastError = row.LastError,
        LastProbeStatus = row.LastProbeStatus,
        DisconnectedAt = ToOffset(row.DisconnectedAt),
    };

    static string Freshness(string status, DateTime? capturedAt)
    {
        if (capturedAt is null)
            return "NONE";
        if (status == "DISCONNECTED")
            return "DISCONNECTED";
        var captured = ToOffset(capturedAt)!.Value;
        return DateTimeOffset.UtcNow - captured <= TimeSpan.FromMinutes(15) ? "FRESH" : "STALE";
    }

    static DateTimeOffset? ToOffset(DateTime? value)
    {
        if (value is null)
            return null;
        var time = value.Value;
        if (time.Kind == DateTimeKind.Unspecified)
            time = DateTime.SpecifyKind(time, DateTimeKind.Utc);
        return new DateTimeOffset(time);
    }

    const string ConnectionSql =
        """
        SELECT c.connection_id AS ConnectionId,
               c.exchange_code AS ExchangeCode,
               c.environment AS Environment,
               c.status AS Status,
               c.api_key_mask AS ApiKeyMask,
               c.read_only_verified AS ReadOnlyVerified,
               CAST(c.permission_names AS text) AS PermissionNames,
               c.external_user_id AS ExternalUserId,
               c.margin_mode AS MarginMode,
               c.unified_margin_status AS UnifiedMarginStatus,
               c.uta AS Uta,
               c.last_checked_at AS LastCheckedAt,
               c.last_error AS LastError,
               c.last_probe_status AS LastProbeStatus,
               c.disconnected_at AS DisconnectedAt
        FROM pack_personal_intelligence.exchange_connection c
        """;

    sealed class ConnectionRow
    {
        public Guid ConnectionId { get; set; }
        public string ExchangeCode { get; set; } = "";
        public string Environment { get; set; } = "";
        public string Status { get; set; } = "";
        public string ApiKeyMask { get; set; } = "";
        public bool ReadOnlyVerified { get; set; }
        public string PermissionNames { get; set; } = "[]";
        public string? ExternalUserId { get; set; }
        public string? MarginMode { get; set; }
        public int? UnifiedMarginStatus { get; set; }
        public int? Uta { get; set; }
        public DateTime? LastCheckedAt { get; set; }
        public string? LastError { get; set; }
        public string LastProbeStatus { get; set; } = "";
        public DateTime? DisconnectedAt { get; set; }
    }

    sealed class SnapshotRow
    {
        public Guid SnapshotId { get; set; }
        public DateTime CapturedAt { get; set; }
        public string SourceEndpoints { get; set; } = "";
        public string? AccountType { get; set; }
        public string? MarginMode { get; set; }
        public int? UnifiedMarginStatus { get; set; }
        public string? TotalEquity { get; set; }
        public string? TotalWalletBalance { get; set; }
        public string? TotalAvailableBalance { get; set; }
        public string? BalanceError { get; set; }
    }

    sealed class AssetRow
    {
        public string Coin { get; set; } = "";
        public string? WalletBalance { get; set; }
        public string? Equity { get; set; }
        public string? UsdValue { get; set; }
        public string? AvailableToWithdraw { get; set; }
    }

    sealed class SecretRow
    {
        public string ExchangeCode { get; set; } = "";
        public string Environment { get; set; } = "";
        public string? KeyId { get; set; }
        public byte[]? Nonce { get; set; }
        public byte[]? Ciphertext { get; set; }
        public byte[]? Tag { get; set; }
        public string Status { get; set; } = "";
    }
}

sealed record StoredExchangeSecret(string ExchangeCode, string Environment, ExchangeCredentialEnvelope Envelope);
