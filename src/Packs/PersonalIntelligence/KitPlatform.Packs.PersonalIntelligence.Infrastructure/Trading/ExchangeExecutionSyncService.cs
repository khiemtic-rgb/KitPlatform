using System.Text.Json;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;

public sealed class ExchangeExecutionSyncService : IExchangeExecutionSync
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    readonly IDbConnectionFactory _db;
    readonly ITenantContext _tenant;
    readonly IExchangeConnector _connector;
    readonly IExchangeCredentialKeyRing _keys;

    public ExchangeExecutionSyncService(
        IDbConnectionFactory db,
        ITenantContext tenant,
        IExchangeConnector connector,
        IExchangeCredentialKeyRing keys)
    {
        _db = db;
        _tenant = tenant;
        _connector = connector;
        _keys = keys;
    }

    public Task<ExchangeExecutionSyncResult> PreviewAsync(
        Guid connectionId, ExchangeExecutionSyncRequest request, CancellationToken cancellationToken) =>
        RunAsync(connectionId, request, commit: false, cancellationToken);

    public Task<ExchangeExecutionSyncResult> CommitAsync(
        Guid connectionId, ExchangeExecutionSyncRequest request, CancellationToken cancellationToken) =>
        RunAsync(connectionId, request, commit: true, cancellationToken);

    public async Task<ExchangeExecutionSyncResult?> LatestAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var visible = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM pack_personal_intelligence.exchange_connection
            WHERE connection_id = @ConnectionId
              AND tenant_id = @TenantId
              AND person_id = (
                    SELECT person_id FROM pack_personal_intelligence.person
                    WHERE user_id = @UserId AND tenant_id = @TenantId AND deleted_at IS NULL
              )
            """,
            new { ConnectionId = connectionId, TenantId = _tenant.TenantId, UserId = _tenant.UserId });
        if (visible != 1)
            throw new ExchangeConnectionException(404, "not_found", "Không tìm thấy kết nối của người dùng này.");
        var row = await conn.QuerySingleOrDefaultAsync<SyncRow>(
            """
            SELECT s.sync_id AS SyncId,
                   s.trading_account_id AS TradingAccountId,
                   s.exchange_code AS ExchangeCode,
                   s.environment AS Environment,
                   s.category AS Category,
                   s.requested_start AS RequestedStart,
                   s.requested_end AS RequestedEnd,
                   s.earliest_exec_at AS EarliestExecAt,
                   s.latest_exec_at AS LatestExecAt,
                   s.pages AS Pages,
                   s.page_limit AS PageLimit,
                   s.fetched_count AS FetchedCount,
                   s.inserted_count AS InsertedCount,
                   s.duplicate_count AS DuplicateCount,
                   s.unmapped_count AS UnmappedCount,
                   s.truncated AS Truncated,
                   s.report::text AS Report,
                   s.created_at AS CreatedAt
            FROM pack_personal_intelligence.exchange_execution_sync s
            JOIN pack_personal_intelligence.exchange_connection c
              ON c.connection_id = s.connection_id AND c.tenant_id = s.tenant_id
            WHERE s.connection_id = @ConnectionId
              AND s.tenant_id = @TenantId
              AND c.person_id = (
                    SELECT person_id FROM pack_personal_intelligence.person
                    WHERE user_id = @UserId AND tenant_id = @TenantId AND deleted_at IS NULL
              )
            ORDER BY s.created_at DESC
            LIMIT 1
            """,
            new { ConnectionId = connectionId, TenantId = _tenant.TenantId, UserId = _tenant.UserId });
        return row is null ? null : MapStored(row);
    }

    async Task<ExchangeExecutionSyncResult> RunAsync(
        Guid connectionId, ExchangeExecutionSyncRequest request, bool commit, CancellationToken cancellationToken)
    {
        if (request.Start is null || request.End is null)
            throw new ExchangeConnectionException(400, "window", "Cần chọn khoảng thời gian bắt đầu và kết thúc.");
        var category = BybitExecutionMapper.NormalizeCategory(request.Category);
        var windows = BybitExecutionMapper.Windows(request.Start.Value, request.End.Value);
        var stored = await LoadAsync(connectionId, cancellationToken);
        if (!string.Equals(stored.ExchangeCode, "BYBIT", StringComparison.Ordinal))
            throw new ExchangeConnectionException(400, "unsupported", "Đồng bộ lịch sử V1 chỉ hỗ trợ Bybit.");
        if (!stored.ReadOnlyVerified)
            throw new ExchangeConnectionException(409, "not_read_only", "Kết nối chưa được xác minh chỉ đọc. Không đồng bộ lịch sử.");

        var (apiKey, apiSecret) = Unpack(ExchangeCredentialProtector.Decrypt(stored.Envelope, _keys.RequireCurrent()));
        await _connector.EnsureReadOnlyAsync(stored.Environment, apiKey, apiSecret, cancellationToken);
        var fetched = await FetchAsync(stored.Environment, apiKey, apiSecret, category, windows, cancellationToken);
        var shaped = Shape(fetched, category, stored.Environment, request.Start.Value, request.End.Value);
        var accountKey = BybitExecutionMapper.ExternalAccountId(stored.Environment, stored.ExternalUserId, connectionId);

        if (!commit)
        {
            var existing = await FindAccountAsync(stored.PersonId, accountKey, cancellationToken);
            var alreadyStored = existing is null ? 0 : await CountExistingAsync(existing.Value, shaped.Unique, cancellationToken);
            shaped.Result.TradingAccountId = existing;
            shaped.Result.DuplicateCount = alreadyStored + shaped.Collapsed;
            shaped.Result.InsertedCount = shaped.Unique.Count - alreadyStored;
            shaped.Result.Committed = false;
            return shaped.Result;
        }

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        Guid? accountId = await FindAccountAsync(conn, tx, stored.PersonId, accountKey);
        var insertedIds = new List<Guid>();
        var storedDup = 0;
        if (shaped.Unique.Count > 0)
        {
            accountId ??= await EnsureAccountAsync(conn, tx, stored, accountKey);
            foreach (var item in shaped.Unique)
            {
                var tradeId = await InsertTradeAsync(conn, tx, accountId.Value, connectionId, stored.Environment, category, item);
                if (tradeId is null)
                    storedDup++;
                else
                    insertedIds.Add(tradeId.Value);
            }
        }

        var result = shaped.Result;
        result.Committed = true;
        result.TradingAccountId = accountId;
        result.DuplicateCount = storedDup + shaped.Collapsed;
        result.InsertedCount = insertedIds.Count;
        result.TradeIds = insertedIds.Take(100).ToList();
        if (insertedIds.Count > result.TradeIds.Count)
            result.Notes.Add("Phản hồi chỉ gồm 100 mã dòng mới. Sổ lọc theo tài khoản, nguồn Đồng bộ từ sàn và khoảng thời gian để xem phần còn lại.");
        result.SyncId = await InsertSyncAsync(conn, tx, connectionId, stored, accountId, result);
        result.CreatedAt = DateTimeOffset.UtcNow;
        await tx.CommitAsync(cancellationToken);
        return result;
    }

    async Task<FetchedExecutions> FetchAsync(
        string environment,
        string apiKey,
        string apiSecret,
        string category,
        IReadOnlyList<(long Start, long End)> windows,
        CancellationToken cancellationToken)
    {
        var items = new List<ExchangeExecutionItemDto>();
        var pages = 0;
        var truncated = false;
        foreach (var (windowStart, windowEnd) in windows)
        {
            string? cursor = null;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (true)
            {
                if (pages >= BybitExecutionMapper.MaxPages)
                {
                    truncated = true;
                    break;
                }

                var page = await _connector.ListExecutionsAsync(
                    environment, apiKey, apiSecret, category, windowStart, windowEnd,
                    BybitExecutionMapper.PageSize, cursor, cancellationToken);
                pages++;
                items.AddRange(page.Items);
                if (string.IsNullOrWhiteSpace(page.NextPageCursor))
                    break;
                if (!seen.Add(page.NextPageCursor))
                {
                    truncated = true;
                    break;
                }

                cursor = page.NextPageCursor;
            }

            if (truncated)
                break;
        }

        return new FetchedExecutions(items, pages, truncated);
    }

    static Shaped Shape(FetchedExecutions fetched, string category, string environment, DateTimeOffset start, DateTimeOffset end)
    {
        var unique = new List<ExchangeExecutionItemDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var collapsed = 0;
        foreach (var item in fetched.Items.Where(item => item.Mappable))
        {
            if (!seen.Add(item.ExternalTradeId))
            {
                collapsed++;
                continue;
            }

            unique.Add(item);
        }

        var issues = fetched.Items.Where(item => !item.Mappable).Select(item => new ExchangeExecutionIssueDto
        {
            ExecId = item.ExecId,
            ExecType = item.ExecType,
            Reason = item.Reason,
            MissingFields = item.MissingFields,
        }).ToList();
        var times = fetched.Items.Where(item => item.ExecutedAt is not null).Select(item => item.ExecutedAt!.Value).ToList();
        DateTimeOffset? earliest = times.Count == 0 ? null : times.Min();
        DateTimeOffset? latest = times.Count == 0 ? null : times.Max();
        var missing = unique.SelectMany(item => item.MissingFields).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var notes = Notes(category, earliest, latest, fetched.Truncated, fetched.Pages, earliest, collapsed, missing, issues.Count);
        return new Shaped(unique, collapsed, new ExchangeExecutionSyncResult
        {
            ExchangeCode = "BYBIT",
            Environment = environment,
            Category = category,
            Source = TradeSource.Exchange,
            RequestedStart = start,
            RequestedEnd = end,
            EarliestExecAt = earliest,
            LatestExecAt = latest,
            ResumeBefore = fetched.Truncated ? earliest : null,
            Pages = fetched.Pages,
            PageLimit = BybitExecutionMapper.PageSize,
            FetchedCount = fetched.Items.Count,
            UnmappedCount = issues.Count,
            Truncated = fetched.Truncated,
            Notes = notes,
            MissingFields = missing,
            Issues = issues.Take(30).ToList(),
        });
    }

    static List<string> Notes(
        string category,
        DateTimeOffset? earliest,
        DateTimeOffset? latest,
        bool truncated,
        int pages,
        DateTimeOffset? resume,
        int collapsed,
        IReadOnlyList<string> missing,
        int unmapped)
    {
        var notes = new List<string>
        {
            "API đã dùng: GET /v5/execution/list. Không gọi closed-pnl, transaction-log, nạp tiền hoặc rút tiền.",
            $"Loại sản phẩm: {category}. Hỗ trợ spot, linear, inverse, option.",
            "Chỉ ghi execType Trade, AdlTrade, BustTrade và BlockTrade. Funding không vào sổ và không cộng vào phí.",
            "Phí khớp lệnh chỉ lưu trên dòng giao dịch. Không ghi thêm bảng fee.",
            "Không dựng position, portfolio, PnL, tỷ lệ thắng hay market snapshot.",
            "Không tạo nhận định, chính sách hoặc giả lập.",
            "Đây không phải toàn bộ lịch sử tài khoản.",
            "Các trường sàn không có cột riêng (isMaker, feeRate, closedSize) được giữ trong metadata khi API trả về. Không lưu nguyên JSON.",
        };
        notes.Add(earliest is null
            ? "Sàn không trả execution có thời điểm đọc được trong khoảng này."
            : $"Sàn trả dữ liệu từ {earliest:O} đến {latest:O}.");
        if (truncated)
        {
            notes.Add(resume is null
                ? $"Đã dừng sau {pages} trang, mỗi trang tối đa {BybitExecutionMapper.PageSize}. Khoảng yêu cầu chưa được lấy hết."
                : $"Đã dừng sau {pages} trang, mỗi trang tối đa {BybitExecutionMapper.PageSize}. Đặt mốc kết thúc tại {resume:O} để lấy phần cũ hơn. Dòng đã lưu không bị ghi đè. Execution cùng đúng một timestamp với mốc này có thể cần lấy lại; dòng trùng không bị ghi đè.");
        }

        if (collapsed > 0)
            notes.Add($"{collapsed} execution lặp trong các trang đã lấy và chỉ được xét một lần.");
        if (missing.Count > 0)
            notes.Add("Trường thiếu trên các execution ghi được: " + string.Join(", ", missing) + ".");
        if (unmapped > 30)
            notes.Add($"Có {unmapped} execution không ghi được. Báo cáo chỉ giữ 30 dòng đầu.");
        return notes;
    }

    async Task<ConnectionSecret> LoadAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<ConnectionSecret>(
            """
            SELECT person_id AS PersonId,
                   exchange_code AS ExchangeCode,
                   environment AS Environment,
                   external_user_id AS ExternalUserId,
                   read_only_verified AS ReadOnlyVerified,
                   status AS Status,
                   key_id AS KeyId,
                   nonce AS Nonce,
                   ciphertext AS Ciphertext,
                   tag AS Tag
            FROM pack_personal_intelligence.exchange_connection
            WHERE connection_id = @ConnectionId
              AND tenant_id = @TenantId
              AND person_id = (
                    SELECT person_id FROM pack_personal_intelligence.person
                    WHERE user_id = @UserId AND tenant_id = @TenantId AND deleted_at IS NULL
              )
            """,
            new { ConnectionId = connectionId, TenantId = _tenant.TenantId, UserId = _tenant.UserId });
        if (row is null)
            throw new ExchangeConnectionException(404, "not_found", "Không tìm thấy kết nối của người dùng này.");
        if (row.Status != "CONNECTED" || row.Nonce is null || row.Ciphertext is null || row.Tag is null || row.KeyId is null)
            throw new ExchangeConnectionException(409, "disconnected", "Kết nối đã bị ngắt. Không còn credential để đồng bộ.");
        row.Envelope = new ExchangeCredentialEnvelope(row.KeyId, row.Nonce, row.Ciphertext, row.Tag);
        return row;
    }

    async Task<Guid?> FindAccountAsync(Guid personId, string externalAccountId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await FindAccountAsync(conn, null, personId, externalAccountId);
    }

    async Task<Guid?> FindAccountAsync(System.Data.IDbConnection conn, System.Data.IDbTransaction? tx, Guid personId, string externalAccountId)
    {
        var row = await conn.QuerySingleOrDefaultAsync<AccountRow>(
            """
            SELECT a.trading_account_id AS TradingAccountId, a.deleted_at AS DeletedAt
            FROM pack_personal_intelligence.trading_account a
            JOIN pack_personal_intelligence.exchange e
              ON e.exchange_id = a.exchange_id AND e.tenant_id = a.tenant_id
            WHERE a.tenant_id = @TenantId
              AND a.person_id = @PersonId
              AND e.exchange_code = 'BYBIT'
              AND a.external_account_id = @ExternalAccountId
            """,
            new { TenantId = _tenant.TenantId, PersonId = personId, ExternalAccountId = externalAccountId },
            tx);
        if (row is null)
            return null;
        if (row.DeletedAt is not null)
            throw new ExchangeConnectionException(409, "account_closed", "Sổ Bybit của kết nối này đang bị đóng. Không ghi execution vào sổ đã đóng và không tự mở lại.");
        return row.TradingAccountId;
    }

    async Task<int> CountExistingAsync(Guid accountId, IReadOnlyList<ExchangeExecutionItemDto> unique, CancellationToken cancellationToken)
    {
        if (unique.Count == 0)
            return 0;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM pack_personal_intelligence.trade
            WHERE tenant_id = @TenantId
              AND trading_account_id = @AccountId
              AND external_trade_id = ANY(@Ids)
            """,
            new { TenantId = _tenant.TenantId, AccountId = accountId, Ids = unique.Select(item => item.ExternalTradeId).ToArray() });
    }

    async Task<Guid> EnsureAccountAsync(
        System.Data.IDbConnection conn,
        System.Data.IDbTransaction tx,
        ConnectionSecret stored,
        string externalAccountId)
    {
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_personal_intelligence.exchange (
                tenant_id, exchange_code, exchange_name, exchange_type, external_provider, created_by)
            VALUES (@TenantId, 'BYBIT', 'Bybit', 'crypto', 'bybit', @UserId)
            ON CONFLICT (tenant_id, exchange_code) DO NOTHING
            """,
            new { TenantId = _tenant.TenantId, UserId = _tenant.UserId },
            tx);
        var exchangeId = await conn.QuerySingleAsync<Guid>(
            """
            SELECT exchange_id FROM pack_personal_intelligence.exchange
            WHERE tenant_id = @TenantId AND exchange_code = 'BYBIT'
            """,
            new { TenantId = _tenant.TenantId },
            tx);
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_personal_intelligence.trading_account (
                person_id, tenant_id, exchange_id, external_account_id, account_name, created_by, updated_by)
            VALUES (
                @PersonId, @TenantId, @ExchangeId, @ExternalAccountId, @AccountName, @UserId, @UserId)
            ON CONFLICT (person_id, exchange_id, external_account_id) DO NOTHING
            """,
            new
            {
                stored.PersonId,
                TenantId = _tenant.TenantId,
                ExchangeId = exchangeId,
                ExternalAccountId = externalAccountId,
                AccountName = "Bybit " + stored.Environment,
                UserId = _tenant.UserId,
            },
            tx);
        var accountId = await FindAccountAsync(conn, tx, stored.PersonId, externalAccountId);
        return accountId ?? throw new ExchangeConnectionException(409, "account_closed", "Không mở được sổ Bybit cho kết nối này.");
    }

    async Task<Guid?> InsertTradeAsync(
        System.Data.IDbConnection conn,
        System.Data.IDbTransaction tx,
        Guid accountId,
        Guid connectionId,
        string environment,
        string category,
        ExchangeExecutionItemDto item)
    {
        var metadata = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["exchange"] = "BYBIT",
            ["environment"] = environment,
            ["category"] = category,
            ["connectionId"] = connectionId,
            ["exchangeOrderId"] = item.OrderId,
            ["execType"] = item.ExecType,
            ["sourceEndpoint"] = BybitExecutionMapper.Endpoint,
            ["syncedAt"] = DateTimeOffset.UtcNow,
            ["missingFields"] = item.MissingFields,
            ["feeStoredOnTrade"] = item.FeeAmount is not null,
            ["feeTableWritten"] = false,
            ["totalValueBasis"] = item.TotalValueBasis,
            ["execPrice"] = item.ExecPriceText,
            ["execQty"] = item.ExecQtyText,
            ["execFee"] = item.ExecFeeText,
            ["execValue"] = item.ExecValueText,
            ["isMaker"] = item.IsMaker,
            ["feeRate"] = item.FeeRate,
            ["closedSize"] = item.ClosedSize,
            ["marketType"] = item.MarketType,
            ["productSubtype"] = item.ProductSubtype,
            ["realizedPnl"] = item.RealizedPnl,
            ["pnlComputed"] = false,
        });
        return await conn.QuerySingleOrDefaultAsync<Guid?>(
            """
            INSERT INTO pack_personal_intelligence.trade (
                person_id, tenant_id, trading_account_id, external_trade_id, symbol, side,
                quantity, price, total_value, fee_amount, fee_currency, executed_at,
                source, source_type, is_observed, metadata, created_by,
                market_type, product_subtype, exchange_code)
            SELECT a.person_id, a.tenant_id, a.trading_account_id, @ExternalTradeId, @Symbol, @Side,
                   @Quantity, @Price, @Total, @FeeAmount, @FeeCurrency, @ExecutedAt,
                   'EXCHANGE', 'EXCHANGE', TRUE, CAST(@Metadata AS jsonb), @UserId,
                   @MarketType, @ProductSubtype, 'BYBIT'
            FROM pack_personal_intelligence.trading_account a
            WHERE a.trading_account_id = @AccountId AND a.tenant_id = @TenantId AND a.deleted_at IS NULL
            ON CONFLICT (trading_account_id, external_trade_id) DO NOTHING
            RETURNING trade_id
            """,
            new
            {
                TenantId = _tenant.TenantId,
                AccountId = accountId,
                UserId = _tenant.UserId,
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
            },
            tx);
    }

    async Task<Guid> InsertSyncAsync(
        System.Data.IDbConnection conn,
        System.Data.IDbTransaction tx,
        Guid connectionId,
        ConnectionSecret stored,
        Guid? accountId,
        ExchangeExecutionSyncResult result)
    {
        var report = JsonSerializer.Serialize(new StoredSyncReport
        {
            Notes = result.Notes,
            Issues = result.Issues,
            MissingFields = result.MissingFields,
            ResumeBefore = result.ResumeBefore,
        }, JsonOptions);
        return await conn.QuerySingleAsync<Guid>(
            """
            INSERT INTO pack_personal_intelligence.exchange_execution_sync (
                tenant_id, connection_id, person_id, trading_account_id, exchange_code, environment, category,
                requested_start, requested_end, earliest_exec_at, latest_exec_at, pages, page_limit,
                fetched_count, inserted_count, duplicate_count, unmapped_count, truncated, report, created_by)
            VALUES (
                @TenantId, @ConnectionId, @PersonId, @TradingAccountId, 'BYBIT', @Environment, @Category,
                @RequestedStart, @RequestedEnd, @EarliestExecAt, @LatestExecAt, @Pages, @PageLimit,
                @FetchedCount, @InsertedCount, @DuplicateCount, @UnmappedCount, @Truncated, CAST(@Report AS jsonb), @UserId)
            RETURNING sync_id
            """,
            new
            {
                TenantId = _tenant.TenantId,
                ConnectionId = connectionId,
                stored.PersonId,
                TradingAccountId = accountId,
                stored.Environment,
                result.Category,
                result.RequestedStart,
                result.RequestedEnd,
                result.EarliestExecAt,
                result.LatestExecAt,
                result.Pages,
                result.PageLimit,
                result.FetchedCount,
                result.InsertedCount,
                result.DuplicateCount,
                result.UnmappedCount,
                result.Truncated,
                Report = report,
                UserId = _tenant.UserId,
            },
            tx);
    }

    static ExchangeExecutionSyncResult MapStored(SyncRow row)
    {
        StoredSyncReport? report = null;
        if (!string.IsNullOrWhiteSpace(row.Report))
        {
            try
            {
                report = JsonSerializer.Deserialize<StoredSyncReport>(row.Report, JsonOptions);
            }
            catch (JsonException)
            {
                report = null;
            }
        }

        return new ExchangeExecutionSyncResult
        {
            Committed = true,
            SyncId = row.SyncId,
            TradingAccountId = row.TradingAccountId,
            ExchangeCode = row.ExchangeCode,
            Environment = row.Environment,
            Category = row.Category,
            Source = TradeSource.Exchange,
            RequestedStart = ToOffset(row.RequestedStart),
            RequestedEnd = ToOffset(row.RequestedEnd),
            EarliestExecAt = ToOffset(row.EarliestExecAt),
            LatestExecAt = ToOffset(row.LatestExecAt),
            ResumeBefore = report?.ResumeBefore,
            CreatedAt = ToOffset(row.CreatedAt),
            Pages = row.Pages,
            PageLimit = row.PageLimit,
            FetchedCount = row.FetchedCount,
            InsertedCount = row.InsertedCount,
            DuplicateCount = row.DuplicateCount,
            UnmappedCount = row.UnmappedCount,
            Truncated = row.Truncated,
            Notes = report?.Notes ?? ["Không đọc được báo cáo JSON của lần đồng bộ này."],
            MissingFields = report?.MissingFields ?? [],
            Issues = report?.Issues ?? [],
        };
    }

    static DateTimeOffset ToOffset(DateTime value)
    {
        var time = value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();
        return new DateTimeOffset(time);
    }

    static DateTimeOffset? ToOffset(DateTime? value) => value is null ? null : ToOffset(value.Value);

    static (string ApiKey, string ApiSecret) Unpack(string plaintext)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<CredentialPayload>(plaintext, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (payload is null || string.IsNullOrWhiteSpace(payload.ApiKey) || string.IsNullOrWhiteSpace(payload.ApiSecret))
                throw new ExchangeConnectionException(503, "ciphertext_invalid", "Không giải mã được credential đã lưu.");
            return (payload.ApiKey, payload.ApiSecret);
        }
        catch (JsonException)
        {
            throw new ExchangeConnectionException(503, "ciphertext_invalid", "Không giải mã được credential đã lưu.");
        }
    }

    sealed class CredentialPayload
    {
        public string ApiKey { get; set; } = "";
        public string ApiSecret { get; set; } = "";
    }

    sealed class StoredSyncReport
    {
        public List<string> Notes { get; set; } = [];
        public List<ExchangeExecutionIssueDto> Issues { get; set; } = [];
        public List<string> MissingFields { get; set; } = [];
        public DateTimeOffset? ResumeBefore { get; set; }
    }

    sealed class ConnectionSecret
    {
        public Guid PersonId { get; set; }
        public string ExchangeCode { get; set; } = "";
        public string Environment { get; set; } = "";
        public string? ExternalUserId { get; set; }
        public bool ReadOnlyVerified { get; set; }
        public string Status { get; set; } = "";
        public string? KeyId { get; set; }
        public byte[]? Nonce { get; set; }
        public byte[]? Ciphertext { get; set; }
        public byte[]? Tag { get; set; }
        public ExchangeCredentialEnvelope Envelope { get; set; } = null!;
    }

    sealed class AccountRow
    {
        public Guid TradingAccountId { get; set; }
        public DateTime? DeletedAt { get; set; }
    }

    sealed class SyncRow
    {
        public Guid SyncId { get; set; }
        public Guid? TradingAccountId { get; set; }
        public string ExchangeCode { get; set; } = "";
        public string Environment { get; set; } = "";
        public string Category { get; set; } = "";
        public DateTime RequestedStart { get; set; }
        public DateTime RequestedEnd { get; set; }
        public DateTime? EarliestExecAt { get; set; }
        public DateTime? LatestExecAt { get; set; }
        public int Pages { get; set; }
        public int PageLimit { get; set; }
        public int FetchedCount { get; set; }
        public int InsertedCount { get; set; }
        public int DuplicateCount { get; set; }
        public int UnmappedCount { get; set; }
        public bool Truncated { get; set; }
        public string Report { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    sealed record FetchedExecutions(List<ExchangeExecutionItemDto> Items, int Pages, bool Truncated);

    sealed record Shaped(List<ExchangeExecutionItemDto> Unique, int Collapsed, ExchangeExecutionSyncResult Result);
}
