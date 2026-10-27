using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Options;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;

public sealed class ExchangeAdapterRegistry : IExchangeAdapterRegistry
{
    readonly Dictionary<string, IExchangeAdapter> _adapters;

    public ExchangeAdapterRegistry(IEnumerable<IExchangeAdapter> adapters)
    {
        _adapters = new Dictionary<string, IExchangeAdapter>(StringComparer.OrdinalIgnoreCase);
        foreach (var adapter in adapters)
            _adapters[adapter.ExchangeCode] = adapter;
    }

    public IExchangeAdapter Require(string exchangeCode) =>
        _adapters.TryGetValue(exchangeCode, out var adapter)
            ? adapter
            : throw new ExchangeConnectionException(400, "unsupported", "Sàn không được hỗ trợ. Chỉ Bybit, Binance và OKX.");

    public IReadOnlyList<IExchangeAdapter> All => _adapters.Values.ToList();
}

public sealed class ExchangeBackfillService : IExchangeBackfill
{
    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    static readonly HashSet<string> TerminalRetry = new(StringComparer.Ordinal) { "FAILED", "INTERRUPTED", "PARTIAL" };
    static readonly HashSet<string> AuthCodes = new(StringComparer.Ordinal)
    {
        "invalid_key", "signature", "passphrase", "permission", "timestamp", "ip", "expired",
        "withdrawal", "transfer", "passphrase_required", "ciphertext_invalid", "key_missing", "key_invalid",
    };

    readonly IDbConnectionFactory _db;
    readonly ITenantContext _tenant;
    readonly IExchangeAdapterRegistry _adapters;
    readonly IExchangeCredentialKeyRing _keys;
    readonly ExchangeBackfillOptions _options;

    public ExchangeBackfillService(
        IDbConnectionFactory db,
        ITenantContext tenant,
        IExchangeAdapterRegistry adapters,
        IExchangeCredentialKeyRing keys,
        IOptions<ExchangeBackfillOptions> options)
    {
        _db = db;
        _tenant = tenant;
        _adapters = adapters;
        _keys = keys;
        _options = options.Value;
    }

    public async Task EnqueueInitialAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var owned = await RequireOwnedAsync(connectionId, cancellationToken);
        foreach (var stream in Streams(owned))
            await EnsureJobAsync(owned, stream, null, cancellationToken);
    }

    public async Task<IReadOnlyList<ExchangeBackfillJobDto>> RequestAsync(Guid connectionId, ExchangeBackfillRequest request, CancellationToken cancellationToken)
    {
        var mode = (request.Mode ?? "INCREMENTAL").Trim().ToUpperInvariant();
        if (mode is not ("FULL" or "INCREMENTAL"))
            throw new ExchangeConnectionException(400, "mode", "Chế độ đồng bộ chỉ là FULL hoặc INCREMENTAL.");
        var owned = await RequireOwnedAsync(connectionId, cancellationToken);
        var streams = Streams(owned);
        if (!string.IsNullOrWhiteSpace(request.ProductSubtype))
        {
            streams = streams.Where(stream => string.Equals(stream.ProductSubtype, request.ProductSubtype.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            if (streams.Count == 0)
                throw new ExchangeConnectionException(400, "unsupported_category", "Loại sản phẩm này không có luồng đồng bộ.");
        }

        foreach (var stream in streams)
            await EnsureJobAsync(owned, stream, mode, cancellationToken);
        return await ListAsync(connectionId, null, cancellationToken);
    }

    public Task<IReadOnlyList<ExchangeBackfillJobDto>> ListAsync(Guid? connectionId, string? marketType, CancellationToken cancellationToken) =>
        QueryJobsAsync(connectionId, marketType, cancellationToken);

    public Task<ExchangeBackfillJobDto> PauseAsync(Guid jobId, CancellationToken cancellationToken) =>
        SetStatusAsync(jobId, ["QUEUED", "RUNNING", "INTERRUPTED"], ExchangeBackfillStatus.Paused, resetAttempt: false, cancellationToken);

    public Task<ExchangeBackfillJobDto> ResumeAsync(Guid jobId, CancellationToken cancellationToken) =>
        SetStatusAsync(jobId, ["PAUSED"], ExchangeBackfillStatus.Queued, resetAttempt: false, cancellationToken);

    public Task<ExchangeBackfillJobDto> RetryAsync(Guid jobId, CancellationToken cancellationToken) =>
        SetStatusAsync(jobId, ["FAILED", "INTERRUPTED", "PARTIAL"], ExchangeBackfillStatus.Queued, resetAttempt: true, cancellationToken);

    public async Task PauseOpenAsync(Guid connectionId, string reason, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            UPDATE pack_personal_intelligence.exchange_backfill_job
            SET status = 'PAUSED', last_error = @Reason, lease_until = NULL, updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND connection_id = @ConnectionId
              AND status IN ('QUEUED', 'RUNNING', 'INTERRUPTED')
              AND person_id = (
                    SELECT person_id FROM pack_personal_intelligence.person
                    WHERE user_id = @UserId AND tenant_id = @TenantId AND deleted_at IS NULL
              )
            """,
            new { TenantId = _tenant.TenantId, UserId = _tenant.UserId, ConnectionId = connectionId, Reason = reason });
    }

    public async Task<bool> PumpAsync(CancellationToken cancellationToken)
    {
        await using (var conn = await _db.CreateOpenConnectionAsync(cancellationToken))
        {
            await conn.ExecuteAsync(
                """
                UPDATE pack_personal_intelligence.exchange_backfill_job
                SET status = 'INTERRUPTED', lease_until = NULL, updated_at = NOW()
                WHERE status = 'RUNNING' AND (lease_until IS NULL OR lease_until < NOW())
                """);
        }

        var worked = false;
        var steps = Math.Max(1, _options.StepsPerPump);
        for (var i = 0; i < steps; i++)
        {
            var job = await ClaimAsync(cancellationToken);
            if (job is null)
                break;
            worked = true;
            try
            {
                await StepAsync(job, cancellationToken);
            }
            catch (ExchangeConnectionException ex)
            {
                await HandleFailureAsync(job, ex, cancellationToken);
            }
            catch (Exception)
            {
                await HandleFailureAsync(job, new ExchangeConnectionException(500, "exchange", "Lỗi xử lý đồng bộ. Checkpoint được giữ."), cancellationToken);
            }

            if (_options.DelayMilliseconds > 0)
                await Task.Delay(_options.DelayMilliseconds, cancellationToken);
        }

        return worked;
    }

    async Task EnsureJobAsync(OwnedConnection owned, ExchangeStreamDefinition stream, string? forcedMode, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var open = await conn.QuerySingleOrDefaultAsync<OpenJob>(
            """
            SELECT job_id AS JobId, status AS Status
            FROM pack_personal_intelligence.exchange_backfill_job
            WHERE tenant_id = @TenantId AND connection_id = @ConnectionId
              AND market_type = @MarketType AND product_subtype = @ProductSubtype
              AND status IN ('QUEUED', 'RUNNING', 'PAUSED', 'INTERRUPTED')
            """,
            new { owned.TenantId, owned.ConnectionId, stream.MarketType, stream.ProductSubtype });
        if (open is not null && forcedMode is null)
        {
            if (open.Status == ExchangeBackfillStatus.Paused)
            {
                await conn.ExecuteAsync(
                    """
                    UPDATE pack_personal_intelligence.exchange_backfill_job
                    SET status = 'QUEUED', next_retry_at = NULL, last_error = NULL, updated_at = NOW()
                    WHERE job_id = @JobId AND tenant_id = @TenantId
                    """,
                    new { open.JobId, owned.TenantId });
            }

            return;
        }

        if (open is not null)
            return;

        var latest = await conn.QuerySingleOrDefaultAsync<LatestJob>(
            """
            SELECT status AS Status, newest_exec_at AS NewestExecAt, checkpoint AS Checkpoint
            FROM pack_personal_intelligence.exchange_backfill_job
            WHERE tenant_id = @TenantId AND connection_id = @ConnectionId
              AND market_type = @MarketType AND product_subtype = @ProductSubtype
            ORDER BY created_at DESC
            LIMIT 1
            """,
            new { owned.TenantId, owned.ConnectionId, stream.MarketType, stream.ProductSubtype });
        if (forcedMode is null && latest?.Status == ExchangeBackfillStatus.Failed)
        {
            await conn.ExecuteAsync(
                """
                UPDATE pack_personal_intelligence.exchange_backfill_job
                SET status = 'QUEUED', attempt_count = 0, next_retry_at = NULL, finished_at = NULL, updated_at = NOW()
                WHERE tenant_id = @TenantId AND connection_id = @ConnectionId
                  AND market_type = @MarketType AND product_subtype = @ProductSubtype
                  AND status = 'FAILED'
                """,
                new { owned.TenantId, owned.ConnectionId, stream.MarketType, stream.ProductSubtype });
            return;
        }

        var mode = forcedMode ?? (latest?.NewestExecAt is not null || latest?.Status is "PARTIAL" or "COMPLETED" ? "INCREMENTAL" : "FULL");
        DateTimeOffset? watermark = mode == "INCREMENTAL" ? latest?.NewestExecAt : null;
        var blocked = BlockReason(owned, stream);
        if (blocked is not null)
        {
            await InsertTerminalAsync(conn, owned, stream, mode, ExchangeBackfillStatus.Partial, blocked);
            return;
        }

        var checkpoint = ExchangeBackfillMachine.Start(stream, DateTimeOffset.UtcNow, watermark, mode);
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_personal_intelligence.exchange_backfill_job (
                tenant_id, connection_id, person_id, exchange_code, environment,
                market_type, product_subtype, status, mode, checkpoint, history_floor
            )
            VALUES (
                @TenantId, @ConnectionId, @PersonId, @ExchangeCode, @Environment,
                @MarketType, @ProductSubtype, 'QUEUED', @Mode, CAST(@Checkpoint AS jsonb), @Floor
            )
            ON CONFLICT (tenant_id, connection_id, market_type, product_subtype)
            WHERE status IN ('QUEUED', 'RUNNING', 'PAUSED', 'INTERRUPTED')
            DO NOTHING
            """,
            new
            {
                owned.TenantId,
                owned.ConnectionId,
                owned.PersonId,
                owned.ExchangeCode,
                owned.Environment,
                stream.MarketType,
                stream.ProductSubtype,
                Mode = mode,
                Checkpoint = JsonSerializer.Serialize(checkpoint, JsonOptions),
                Floor = DateTimeOffset.FromUnixTimeMilliseconds(checkpoint.VenueFloorMs),
            });
    }

    async Task StepAsync(JobRow job, CancellationToken cancellationToken)
    {
        var stored = await LoadSecretAsync(job, cancellationToken);
        if (stored is null)
        {
            await MarkAsync(job, ExchangeBackfillStatus.Paused, "Kết nối đã ngắt hoặc credential không còn. Checkpoint được giữ.", null, cancellationToken);
            return;
        }

        var checkpoint = JsonSerializer.Deserialize<BackfillCheckpoint>(job.Checkpoint, JsonOptions);
        if (checkpoint is null || checkpoint.Windows.Count == 0)
        {
            await MarkAsync(job, ExchangeBackfillStatus.Failed, "Checkpoint không đọc được. Không xóa giao dịch đã lưu.", null, cancellationToken);
            return;
        }

        var stream = ExchangeCatalog.Require(job.ExchangeCode, job.ProductSubtype, stored.Uta);
        if (checkpoint.DiscoveryComplete && checkpoint.RequiresSymbol && checkpoint.SymbolIndex >= checkpoint.Symbols.Count)
        {
            await FinishAsync(job, checkpoint, ExchangeBackfillMachine.Advance(checkpoint, new ExchangeHistoryPage()), 0, 0, 0, 0, null, cancellationToken);
            return;
        }

        var query = ExchangeBackfillMachine.Query(checkpoint, job.ProductSubtype, stream.PageSize);
        if (query.Phase == "fills" && checkpoint.RequiresSymbol && string.IsNullOrWhiteSpace(query.Symbol))
        {
            var done = ExchangeBackfillMachine.Advance(checkpoint, new ExchangeHistoryPage());
            await FinishAsync(job, done.Checkpoint, done, 0, 0, 0, 0, null, cancellationToken);
            return;
        }

        var adapter = _adapters.Require(job.ExchangeCode);
        ExchangeHistoryPage page;
        try
        {
            page = await adapter.ReadHistoryAsync(stored.Credential, query, cancellationToken);
        }
        catch (ExchangeConnectionException ex) when (ex.Code == "unknown_symbol")
        {
            checkpoint.SymbolIndex++;
            checkpoint.WindowIndex = 0;
            checkpoint.ActiveWindowEnd = null;
            checkpoint.Cursor = null;
            checkpoint.Note = string.IsNullOrWhiteSpace(checkpoint.Note)
                ? "Một symbol không tồn tại trên sàn và được bỏ qua."
                : checkpoint.Note + " Một symbol không tồn tại trên sàn và được bỏ qua.";
            var terminal = checkpoint.RequiresSymbol && checkpoint.SymbolIndex >= checkpoint.Symbols.Count;
            var step = terminal
                ? new BackfillStep
                {
                    Checkpoint = checkpoint,
                    Terminal = true,
                    TerminalStatus = ExchangeBackfillStatus.Partial,
                    CutoffReason = checkpoint.RetentionNote + " " + checkpoint.Note,
                    CutoffMs = checkpoint.VenueFloorMs,
                }
                : new BackfillStep { Checkpoint = checkpoint };
            await FinishAsync(job, checkpoint, step, 0, 0, 0, 1, null, cancellationToken);
            return;
        }

        Guid? accountId = null;
        var inserted = 0;
        var duplicate = 0;
        var skipped = 0;
        DateTimeOffset? oldest = null;
        DateTimeOffset? newest = null;
        if (query.Phase == "fills")
        {
            await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
            await using var tx = await conn.BeginTransactionAsync(cancellationToken);
            foreach (var item in page.Items)
            {
                if (!item.Mappable || item.MarketType is null || item.ExecutedAt is null)
                {
                    skipped++;
                    continue;
                }

                accountId ??= await EnsureAccountAsync(conn, tx, job, stored, cancellationToken);
                var id = await InsertTradeAsync(conn, tx, job, accountId.Value, item, cancellationToken);
                if (id is null)
                    duplicate++;
                else
                    inserted++;
                if (oldest is null || item.ExecutedAt < oldest)
                    oldest = item.ExecutedAt;
                if (newest is null || item.ExecutedAt > newest)
                    newest = item.ExecutedAt;
            }

            await tx.CommitAsync(cancellationToken);
        }

        var advanced = ExchangeBackfillMachine.Advance(checkpoint, page);
        await FinishAsync(job, advanced.Checkpoint, advanced, page.Items.Count, inserted, duplicate, skipped, accountId, cancellationToken, oldest, newest, query);
    }

    async Task FinishAsync(
        JobRow job,
        BackfillCheckpoint checkpoint,
        BackfillStep step,
        int fetched,
        int inserted,
        int duplicate,
        int skipped,
        Guid? accountId,
        CancellationToken cancellationToken,
        DateTimeOffset? oldest = null,
        DateTimeOffset? newest = null,
        ExchangeHistoryQuery? query = null)
    {
        var status = step.Terminal ? step.TerminalStatus ?? ExchangeBackfillStatus.Partial : ExchangeBackfillStatus.Queued;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            UPDATE pack_personal_intelligence.exchange_backfill_job
            SET status = @Status,
                checkpoint = CAST(@Checkpoint AS jsonb),
                trading_account_id = COALESCE(trading_account_id, @AccountId),
                window_start = COALESCE(@WindowStart, window_start),
                window_end = COALESCE(@WindowEnd, window_end),
                oldest_exec_at = CASE
                    WHEN oldest_exec_at IS NULL THEN @Oldest
                    WHEN @Oldest IS NULL THEN oldest_exec_at
                    ELSE LEAST(oldest_exec_at, @Oldest) END,
                newest_exec_at = CASE
                    WHEN newest_exec_at IS NULL THEN @Newest
                    WHEN @Newest IS NULL THEN newest_exec_at
                    ELSE GREATEST(newest_exec_at, @Newest) END,
                pages = pages + 1,
                batches = batches + 1,
                fetched_count = fetched_count + @Fetched,
                inserted_count = inserted_count + @Inserted,
                duplicate_count = duplicate_count + @Duplicate,
                skipped_count = skipped_count + @Skipped,
                last_error = NULL,
                next_retry_at = NULL,
                lease_until = NULL,
                cutoff_at = CASE WHEN @Terminal THEN @Cutoff ELSE cutoff_at END,
                cutoff_reason = CASE WHEN @Terminal THEN @Reason ELSE cutoff_reason END,
                finished_at = CASE WHEN @Terminal THEN NOW() ELSE NULL END,
                updated_at = NOW()
            WHERE job_id = @JobId AND tenant_id = @TenantId
            """,
            new
            {
                job.JobId,
                job.TenantId,
                Status = status,
                Checkpoint = JsonSerializer.Serialize(step.Checkpoint.Windows.Count == 0 ? checkpoint : step.Checkpoint, JsonOptions),
                AccountId = accountId,
                WindowStart = query is null ? (DateTimeOffset?)null : DateTimeOffset.FromUnixTimeMilliseconds(query.WindowStartMs),
                WindowEnd = query is null ? (DateTimeOffset?)null : DateTimeOffset.FromUnixTimeMilliseconds(query.WindowEndMs),
                Oldest = oldest,
                Newest = newest,
                Fetched = fetched,
                Inserted = inserted,
                Duplicate = duplicate,
                Skipped = skipped,
                Terminal = step.Terminal,
                Cutoff = step.CutoffMs is long ms ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : (DateTimeOffset?)null,
                Reason = step.CutoffReason,
            });
    }

    async Task HandleFailureAsync(JobRow job, ExchangeConnectionException ex, CancellationToken cancellationToken)
    {
        var attempt = job.AttemptCount + 1;
        var auth = AuthCodes.Contains(ex.Code);
        var giveUp = auth || attempt >= 12;
        var delaySeconds = Math.Min(300, (int)Math.Pow(2, Math.Min(8, attempt)));
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            UPDATE pack_personal_intelligence.exchange_backfill_job
            SET status = @Status,
                attempt_count = @Attempt,
                error_count = error_count + 1,
                last_error = @Message,
                next_retry_at = @RetryAt,
                lease_until = NULL,
                finished_at = CASE WHEN @Terminal THEN NOW() ELSE NULL END,
                updated_at = NOW()
            WHERE job_id = @JobId AND tenant_id = @TenantId
            """,
            new
            {
                job.JobId,
                job.TenantId,
                Status = giveUp ? ExchangeBackfillStatus.Failed : ExchangeBackfillStatus.Queued,
                Attempt = attempt,
                Message = ex.Message,
                RetryAt = giveUp ? (DateTimeOffset?)null : DateTimeOffset.UtcNow.AddSeconds(delaySeconds),
                Terminal = giveUp,
            });
    }

    async Task MarkAsync(JobRow job, string status, string message, BackfillStep? step, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            UPDATE pack_personal_intelligence.exchange_backfill_job
            SET status = @Status, last_error = @Message, lease_until = NULL,
                finished_at = CASE WHEN @Status IN ('FAILED', 'PARTIAL', 'COMPLETED', 'PAUSED') THEN NOW() ELSE NULL END,
                updated_at = NOW()
            WHERE job_id = @JobId AND tenant_id = @TenantId
            """,
            new { job.JobId, job.TenantId, Status = status, Message = message });
    }

    async Task<JobRow?> ClaimAsync(CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<JobRow>(
            """
            UPDATE pack_personal_intelligence.exchange_backfill_job AS j
            SET status = 'RUNNING', lease_until = NOW() + INTERVAL '2 minutes', updated_at = NOW()
            WHERE j.job_id = (
                SELECT job_id
                FROM pack_personal_intelligence.exchange_backfill_job
                WHERE status IN ('QUEUED', 'INTERRUPTED')
                  AND (next_retry_at IS NULL OR next_retry_at <= NOW())
                ORDER BY updated_at
                FOR UPDATE SKIP LOCKED
                LIMIT 1
            )
            RETURNING j.job_id AS JobId, j.tenant_id AS TenantId, j.connection_id AS ConnectionId,
                      j.person_id AS PersonId, j.exchange_code AS ExchangeCode, j.environment AS Environment,
                      j.market_type AS MarketType, j.product_subtype AS ProductSubtype, j.mode AS Mode,
                      CAST(j.checkpoint AS text) AS Checkpoint, j.attempt_count AS AttemptCount
            """,
            transaction: tx);
        await tx.CommitAsync(cancellationToken);
        return row;
    }

    async Task<SecretLoad?> LoadSecretAsync(JobRow job, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<SecretLoadRow>(
            """
            SELECT environment AS Environment, exchange_code AS ExchangeCode, key_id AS KeyId,
                   nonce AS Nonce, ciphertext AS Ciphertext, tag AS Tag, status AS Status,
                   external_user_id AS ExternalUserId, uta AS Uta
            FROM pack_personal_intelligence.exchange_connection
            WHERE connection_id = @ConnectionId AND tenant_id = @TenantId
            """,
            new { job.ConnectionId, job.TenantId });
        if (row is null || row.Status != "CONNECTED" || row.Nonce is null || row.Ciphertext is null || row.Tag is null || row.KeyId is null)
            return null;
        var plain = ExchangeCredentialProtector.Decrypt(new ExchangeCredentialEnvelope(row.KeyId, row.Nonce, row.Ciphertext, row.Tag), _keys.RequireCurrent());
        var payload = JsonSerializer.Deserialize<CredentialPayload>(plain);
        if (payload is null || string.IsNullOrWhiteSpace(payload.ApiKey) || string.IsNullOrWhiteSpace(payload.ApiSecret))
            throw new ExchangeConnectionException(503, "ciphertext_invalid", "Không giải mã được credential đã lưu.");
        return new SecretLoad(
            new ExchangeCredential
            {
                Environment = row.Environment,
                ApiKey = payload.ApiKey,
                ApiSecret = payload.ApiSecret,
                Passphrase = payload.Passphrase,
            },
            row.ExternalUserId,
            row.Uta);
    }

    async Task<Guid> EnsureAccountAsync(IDbConnection conn, IDbTransaction tx, JobRow job, SecretLoad stored, CancellationToken cancellationToken)
    {
        var external = ExternalAccount(job.ExchangeCode, job.Environment, stored.ExternalUserId, job.ConnectionId);
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_personal_intelligence.exchange (
                tenant_id, exchange_code, exchange_name, exchange_type, external_provider)
            VALUES (@TenantId, @ExchangeCode, @ExchangeName, 'crypto', @Provider)
            ON CONFLICT (tenant_id, exchange_code) DO NOTHING
            """,
            new
            {
                job.TenantId,
                job.ExchangeCode,
                ExchangeName = Name(job.ExchangeCode),
                Provider = job.ExchangeCode.ToLowerInvariant(),
            },
            tx);
        var exchangeId = await conn.QuerySingleAsync<Guid>(
            """
            SELECT exchange_id FROM pack_personal_intelligence.exchange
            WHERE tenant_id = @TenantId AND exchange_code = @ExchangeCode
            """,
            new { job.TenantId, job.ExchangeCode },
            tx);
        var existing = await conn.QuerySingleOrDefaultAsync<(Guid Id, DateTime? Deleted)>(
            """
            SELECT trading_account_id, deleted_at
            FROM pack_personal_intelligence.trading_account
            WHERE tenant_id = @TenantId AND person_id = @PersonId AND exchange_id = @ExchangeId
              AND external_account_id = @External
            """,
            new { job.TenantId, job.PersonId, ExchangeId = exchangeId, External = external },
            tx);
        if (existing.Id != Guid.Empty && existing.Deleted is not null)
            throw new ExchangeConnectionException(409, "account_closed", "Sổ của kết nối này đang bị đóng. Không ghi đè và không tự mở lại.");
        if (existing.Id != Guid.Empty)
            return existing.Id;
        var created = await conn.QuerySingleOrDefaultAsync<Guid?>(
            """
            INSERT INTO pack_personal_intelligence.trading_account (
                person_id, tenant_id, exchange_id, external_account_id, account_name)
            VALUES (@PersonId, @TenantId, @ExchangeId, @External, @AccountName)
            ON CONFLICT (person_id, exchange_id, external_account_id) DO NOTHING
            RETURNING trading_account_id
            """,
            new
            {
                job.PersonId,
                job.TenantId,
                ExchangeId = exchangeId,
                External = external,
                AccountName = Name(job.ExchangeCode) + " " + job.Environment,
            },
            tx);
        if (created is not null)
            return created.Value;
        return await conn.QuerySingleAsync<Guid>(
            """
            SELECT trading_account_id
            FROM pack_personal_intelligence.trading_account
            WHERE tenant_id = @TenantId AND person_id = @PersonId AND exchange_id = @ExchangeId
              AND external_account_id = @External AND deleted_at IS NULL
            """,
            new { job.TenantId, job.PersonId, ExchangeId = exchangeId, External = external },
            tx);
    }

    static async Task<Guid?> InsertTradeAsync(
        IDbConnection conn, IDbTransaction tx, JobRow job, Guid accountId, ExchangeExecutionItemDto item, CancellationToken cancellationToken)
    {
        var metadata = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["exchange"] = job.ExchangeCode,
            ["environment"] = job.Environment,
            ["category"] = item.ProductSubtype,
            ["marketType"] = item.MarketType,
            ["productSubtype"] = item.ProductSubtype,
            ["connectionId"] = job.ConnectionId,
            ["exchangeOrderId"] = item.OrderId,
            ["execType"] = item.ExecType,
            ["sourceEndpoint"] = item.SourceEndpoint,
            ["syncedAt"] = DateTimeOffset.UtcNow,
            ["missingFields"] = item.MissingFields,
            ["feeStoredOnTrade"] = item.FeeAmount is not null,
            ["feeTableWritten"] = false,
            ["totalValueBasis"] = item.TotalValueBasis,
            ["quantityUnit"] = item.QuantityUnit,
            ["realizedPnl"] = item.RealizedPnl,
            ["positionSide"] = item.PositionSide,
            ["execPrice"] = item.ExecPriceText,
            ["execQty"] = item.ExecQtyText,
            ["execFee"] = item.ExecFeeText,
            ["execValue"] = item.ExecValueText,
            ["pnlComputed"] = false,
        });
        return await conn.QuerySingleOrDefaultAsync<Guid?>(
            """
            INSERT INTO pack_personal_intelligence.trade (
                person_id, tenant_id, trading_account_id, external_trade_id, symbol, side,
                quantity, price, total_value, fee_amount, fee_currency, executed_at,
                source, source_type, is_observed, metadata, market_type, product_subtype, exchange_code)
            SELECT a.person_id, a.tenant_id, a.trading_account_id, @ExternalTradeId, @Symbol, @Side,
                   @Quantity, @Price, @Total, @FeeAmount, @FeeCurrency, @ExecutedAt,
                   'EXCHANGE', 'EXCHANGE', TRUE, CAST(@Metadata AS jsonb), @MarketType, @ProductSubtype, @ExchangeCode
            FROM pack_personal_intelligence.trading_account a
            WHERE a.trading_account_id = @AccountId AND a.tenant_id = @TenantId AND a.deleted_at IS NULL
            ON CONFLICT (trading_account_id, external_trade_id) DO NOTHING
            RETURNING trade_id
            """,
            new
            {
                job.TenantId,
                AccountId = accountId,
                item.ExternalTradeId,
                item.Symbol,
                item.Side,
                item.Quantity,
                item.Price,
                Total = item.TotalValue,
                item.FeeAmount,
                item.FeeCurrency,
                item.ExecutedAt,
                Metadata = metadata,
                item.MarketType,
                item.ProductSubtype,
                job.ExchangeCode,
            },
            tx);
    }

    async Task<OwnedConnection> RequireOwnedAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<OwnedConnection>(
            """
            SELECT c.connection_id AS ConnectionId, c.tenant_id AS TenantId, c.person_id AS PersonId,
                   c.exchange_code AS ExchangeCode, c.environment AS Environment, c.uta AS Uta,
                   c.status AS Status, CAST(c.permission_names AS text) AS PermissionJson
            FROM pack_personal_intelligence.exchange_connection c
            WHERE c.connection_id = @ConnectionId
              AND c.tenant_id = @TenantId
              AND c.status = 'CONNECTED'
              AND c.person_id = (
                    SELECT person_id FROM pack_personal_intelligence.person
                    WHERE user_id = @UserId AND tenant_id = @TenantId AND deleted_at IS NULL
              )
            """,
            new { ConnectionId = connectionId, TenantId = _tenant.TenantId, UserId = _tenant.UserId });
        if (row is null)
            throw new ExchangeConnectionException(404, "not_found", "Không tìm thấy kết nối còn hiệu lực của người dùng này.");
        row.PermissionNames = JsonSerializer.Deserialize<List<string>>(row.PermissionJson) ?? [];
        return row;
    }

    async Task<IReadOnlyList<ExchangeBackfillJobDto>> QueryJobsAsync(Guid? connectionId, string? marketType, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<JobDtoRow>(
            """
            SELECT j.job_id AS JobId, j.connection_id AS ConnectionId, j.exchange_code AS ExchangeCode,
                   j.environment AS Environment, j.market_type AS MarketType, j.product_subtype AS ProductSubtype,
                   j.status AS Status, j.mode AS Mode, j.window_start AS WindowStart, j.window_end AS WindowEnd,
                   j.oldest_exec_at AS OldestExecAt, j.newest_exec_at AS NewestExecAt, j.cutoff_at AS CutoffAt,
                   j.cutoff_reason AS CutoffReason, j.pages AS Pages, j.batches AS Batches,
                   j.fetched_count AS FetchedCount, j.inserted_count AS InsertedCount,
                   j.duplicate_count AS DuplicateCount, j.skipped_count AS SkippedCount, j.error_count AS ErrorCount,
                   j.last_error AS LastError, j.updated_at AS UpdatedAt, j.finished_at AS FinishedAt
            FROM pack_personal_intelligence.exchange_backfill_job j
            WHERE j.tenant_id = @TenantId
              AND j.person_id = (
                    SELECT person_id FROM pack_personal_intelligence.person
                    WHERE user_id = @UserId AND tenant_id = @TenantId AND deleted_at IS NULL
              )
              AND (@ConnectionId IS NULL OR j.connection_id = @ConnectionId)
              AND (@MarketType IS NULL OR j.market_type = @MarketType)
            ORDER BY j.updated_at DESC
            """,
            new
            {
                TenantId = _tenant.TenantId,
                UserId = _tenant.UserId,
                ConnectionId = connectionId,
                MarketType = string.IsNullOrWhiteSpace(marketType) ? null : marketType.Trim().ToUpperInvariant(),
            });
        return rows.Select(MapJob).ToList();
    }

    async Task<ExchangeBackfillJobDto> SetStatusAsync(
        Guid jobId, string[] from, string to, bool resetAttempt, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var updated = await conn.ExecuteAsync(
            """
            UPDATE pack_personal_intelligence.exchange_backfill_job
            SET status = @To,
                attempt_count = CASE WHEN @ResetAttempt THEN 0 ELSE attempt_count END,
                next_retry_at = NULL,
                lease_until = NULL,
                finished_at = NULL,
                updated_at = NOW()
            WHERE job_id = @JobId
              AND tenant_id = @TenantId
              AND status = ANY(@From)
              AND person_id = (
                    SELECT person_id FROM pack_personal_intelligence.person
                    WHERE user_id = @UserId AND tenant_id = @TenantId AND deleted_at IS NULL
              )
            """,
            new { JobId = jobId, TenantId = _tenant.TenantId, UserId = _tenant.UserId, From = from, To = to, ResetAttempt = resetAttempt });
        if (updated != 1)
            throw new ExchangeConnectionException(409, "job_state", "Tác vụ không ở trạng thái có thể đổi, hoặc không thuộc người dùng này.");
        var rows = await QueryJobsAsync(null, null, cancellationToken);
        return rows.First(row => row.JobId == jobId);
    }

    static async Task InsertTerminalAsync(
        IDbConnection conn, OwnedConnection owned, ExchangeStreamDefinition stream, string mode, string status, string reason)
    {
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_personal_intelligence.exchange_backfill_job (
                tenant_id, connection_id, person_id, exchange_code, environment,
                market_type, product_subtype, status, mode, cutoff_reason, finished_at
            )
            VALUES (
                @TenantId, @ConnectionId, @PersonId, @ExchangeCode, @Environment,
                @MarketType, @ProductSubtype, @Status, @Mode, @Reason, NOW()
            )
            """,
            new
            {
                owned.TenantId,
                owned.ConnectionId,
                owned.PersonId,
                owned.ExchangeCode,
                owned.Environment,
                stream.MarketType,
                stream.ProductSubtype,
                Status = status,
                Mode = mode,
                Reason = reason,
            });
    }

    static string? BlockReason(OwnedConnection owned, ExchangeStreamDefinition stream)
    {
        if (owned.ExchangeCode == "BINANCE" && stream.ProductSubtype is "usd_m" or "coin_m")
            return BinancePermissionGate.FuturesHistoryBlock(owned.PermissionNames);
        if (owned.ExchangeCode == "BINANCE" && stream.ProductSubtype == "spot")
            return BinancePermissionGate.SpotHistoryBlock(owned.PermissionNames);
        if (owned.ExchangeCode == "OKX")
            return OkxPermissionGate.HistoryBlock(owned.PermissionNames, stream.ProductSubtype);
        return null;
    }

    static List<ExchangeStreamDefinition> Streams(OwnedConnection owned) =>
        ExchangeCatalog.Describe(owned.Uta).First(entry => entry.ExchangeCode == owned.ExchangeCode).Streams;

    static string ExternalAccount(string exchange, string environment, string? externalUserId, Guid connectionId)
    {
        if (exchange == "BYBIT")
            return BybitExecutionMapper.ExternalAccountId(environment, externalUserId, connectionId);
        var user = new string((externalUserId ?? "").Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').ToArray());
        if (user.Length > 80)
            user = user[..80];
        var id = user.Length == 0
            ? $"{exchange}:{environment}:connection:{connectionId:N}"
            : $"{exchange}:{environment}:{user}";
        return id.Length <= 200 ? id : $"{exchange}:{environment}:connection:{connectionId:N}";
    }

    static string Name(string exchange) => exchange switch
    {
        "BYBIT" => "Bybit",
        "BINANCE" => "Binance",
        "OKX" => "OKX",
        _ => exchange,
    };

    static ExchangeBackfillJobDto MapJob(JobDtoRow row) => new()
    {
        JobId = row.JobId,
        ConnectionId = row.ConnectionId,
        ExchangeCode = row.ExchangeCode,
        Environment = row.Environment,
        MarketType = row.MarketType,
        ProductSubtype = row.ProductSubtype,
        Status = row.Status,
        Mode = row.Mode,
        WindowStart = ToOffset(row.WindowStart),
        WindowEnd = ToOffset(row.WindowEnd),
        OldestExecAt = ToOffset(row.OldestExecAt),
        NewestExecAt = ToOffset(row.NewestExecAt),
        CutoffAt = ToOffset(row.CutoffAt),
        CutoffReason = row.CutoffReason,
        Pages = row.Pages,
        Batches = row.Batches,
        FetchedCount = row.FetchedCount,
        InsertedCount = row.InsertedCount,
        DuplicateCount = row.DuplicateCount,
        SkippedCount = row.SkippedCount,
        ErrorCount = row.ErrorCount,
        LastError = row.LastError,
        UpdatedAt = ToOffset(row.UpdatedAt),
        FinishedAt = ToOffset(row.FinishedAt),
    };

    static DateTimeOffset? ToOffset(DateTime? value)
    {
        if (value is null)
            return null;
        var time = value.Value;
        if (time.Kind == DateTimeKind.Unspecified)
            time = DateTime.SpecifyKind(time, DateTimeKind.Utc);
        return new DateTimeOffset(time);
    }

    sealed class OwnedConnection
    {
        public Guid ConnectionId { get; set; }
        public Guid TenantId { get; set; }
        public Guid PersonId { get; set; }
        public string ExchangeCode { get; set; } = "";
        public string Environment { get; set; } = "";
        public int? Uta { get; set; }
        public string Status { get; set; } = "";
        public string PermissionJson { get; set; } = "[]";
        public List<string> PermissionNames { get; set; } = [];
    }

    sealed class OpenJob
    {
        public Guid JobId { get; set; }
        public string Status { get; set; } = "";
    }

    sealed class LatestJob
    {
        public string Status { get; set; } = "";
        public DateTime? NewestExecAt { get; set; }
        public string? Checkpoint { get; set; }
    }

    sealed class JobRow
    {
        public Guid JobId { get; set; }
        public Guid TenantId { get; set; }
        public Guid ConnectionId { get; set; }
        public Guid PersonId { get; set; }
        public string ExchangeCode { get; set; } = "";
        public string Environment { get; set; } = "";
        public string MarketType { get; set; } = "";
        public string ProductSubtype { get; set; } = "";
        public string Mode { get; set; } = "";
        public string Checkpoint { get; set; } = "{}";
        public int AttemptCount { get; set; }
    }

    sealed class SecretLoadRow
    {
        public string Environment { get; set; } = "";
        public string ExchangeCode { get; set; } = "";
        public string? KeyId { get; set; }
        public byte[]? Nonce { get; set; }
        public byte[]? Ciphertext { get; set; }
        public byte[]? Tag { get; set; }
        public string Status { get; set; } = "";
        public string? ExternalUserId { get; set; }
        public int? Uta { get; set; }
    }

    sealed record SecretLoad(ExchangeCredential Credential, string? ExternalUserId, int? Uta);

    sealed class CredentialPayload
    {
        public string ApiKey { get; set; } = "";
        public string ApiSecret { get; set; } = "";
        public string? Passphrase { get; set; }
    }

    sealed class JobDtoRow
    {
        public Guid JobId { get; set; }
        public Guid ConnectionId { get; set; }
        public string ExchangeCode { get; set; } = "";
        public string Environment { get; set; } = "";
        public string MarketType { get; set; } = "";
        public string ProductSubtype { get; set; } = "";
        public string Status { get; set; } = "";
        public string Mode { get; set; } = "";
        public DateTime? WindowStart { get; set; }
        public DateTime? WindowEnd { get; set; }
        public DateTime? OldestExecAt { get; set; }
        public DateTime? NewestExecAt { get; set; }
        public DateTime? CutoffAt { get; set; }
        public string? CutoffReason { get; set; }
        public int Pages { get; set; }
        public int Batches { get; set; }
        public int FetchedCount { get; set; }
        public int InsertedCount { get; set; }
        public int DuplicateCount { get; set; }
        public int SkippedCount { get; set; }
        public int ErrorCount { get; set; }
        public string? LastError { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
    }
}
