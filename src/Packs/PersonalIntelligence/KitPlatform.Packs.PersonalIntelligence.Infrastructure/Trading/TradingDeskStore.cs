using System.Text.Json;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;

public sealed class TradingDeskStore : ITradingDesk
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public TradingDeskStore(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<TradingSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                (SELECT COUNT(*) FROM pack_personal_intelligence.trade WHERE tenant_id = @TenantId) AS TradeCount,
                (SELECT COUNT(*) FROM pack_personal_intelligence.trade WHERE tenant_id = @TenantId AND UPPER(source) = 'FIXTURE') AS FixtureTradeCount,
                (SELECT COUNT(*) FROM pack_personal_intelligence.trade WHERE tenant_id = @TenantId AND UPPER(source) <> 'FIXTURE') AS OtherSourceTradeCount,
                (SELECT COUNT(*) FROM pack_personal_intelligence.trade WHERE tenant_id = @TenantId AND UPPER(source) = 'IMPORT') AS ImportTradeCount,
                (SELECT COUNT(*) FROM pack_personal_intelligence.trade WHERE tenant_id = @TenantId AND UPPER(source) = 'EXCHANGE') AS ExchangeTradeCount,
                (SELECT COUNT(*) FROM pack_personal_intelligence.pi_decision WHERE tenant_id = @TenantId) AS DecisionCount,
                (SELECT COUNT(*) FROM pack_personal_intelligence.trading_execution WHERE tenant_id = @TenantId AND mode = 'PAPER') AS PaperExecutionCount,
                (SELECT COUNT(*) FROM pack_personal_intelligence.pi_operation
                    WHERE tenant_id = @TenantId AND COALESCE(result->>'RiskAllowed', result->>'riskAllowed') = 'false') AS RiskVetoCount,
                (SELECT source FROM pack_personal_intelligence.trade WHERE tenant_id = @TenantId ORDER BY recorded_at DESC LIMIT 1) AS LatestSource,
                (SELECT recorded_at FROM pack_personal_intelligence.trade WHERE tenant_id = @TenantId ORDER BY recorded_at DESC LIMIT 1) AS LatestRecordedAt,
                (SELECT COUNT(*) FROM pack_personal_intelligence.trade WHERE tenant_id = @TenantId AND fee_amount IS NULL) AS MissingFeeCount
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleAsync<SummaryRow>(sql, new { TenantId = _tenant.TenantId });
        var missing = new List<string>();
        if (row.TradeCount == 0)
            missing.Add("Chưa có giao dịch nào được ghi nhận.");
        if (row.MissingFeeCount > 0)
            missing.Add($"{row.MissingFeeCount} giao dịch chưa có phí.");
        if (row.TradeCount > 0 && row.OtherSourceTradeCount == 0)
            missing.Add("Chưa có dữ liệu sàn thực. Toàn bộ giao dịch đang là FIXTURE.");
        if (row.TradeCount > 0 && row.ExchangeTradeCount == 0 && row.FixtureTradeCount < row.TradeCount)
            missing.Add("Chưa có execution đồng bộ từ sàn. CSV không được tính là dữ liệu sàn.");
        return new TradingSummaryDto(
            row.TradeCount,
            row.FixtureTradeCount,
            row.OtherSourceTradeCount,
            row.DecisionCount,
            row.PaperExecutionCount,
            row.RiskVetoCount,
            row.LatestSource,
            row.LatestRecordedAt,
            row.TradeCount > 0 && row.OtherSourceTradeCount == 0,
            missing,
            row.ImportTradeCount,
            row.ExchangeTradeCount);
    }

    public async Task<IReadOnlyList<TradingAccountDto>> ListAccountsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                a.trading_account_id AS TradingAccountId,
                a.person_id AS PersonId,
                p.display_name AS PersonName,
                a.external_account_id AS ExternalAccountId,
                e.exchange_code AS ExchangeCode,
                e.exchange_name AS ExchangeName,
                a.status AS Status
            FROM pack_personal_intelligence.trading_account a
            JOIN pack_personal_intelligence.person p ON p.person_id = a.person_id AND p.tenant_id = a.tenant_id
            JOIN pack_personal_intelligence.exchange e ON e.exchange_id = a.exchange_id AND e.tenant_id = a.tenant_id
            WHERE a.tenant_id = @TenantId AND a.deleted_at IS NULL
            ORDER BY a.external_account_id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<TradingAccountDto>(sql, new { TenantId = _tenant.TenantId });
        return rows.ToList();
    }

    public async Task<TradingPage<TradeRowDto>> ListTradesAsync(TradeListQuery query, CancellationToken cancellationToken = default)
    {
        var (page, size) = Page(query.Page, query.PageSize);
        const string where = """
            WHERE t.tenant_id = @TenantId
              AND (@AccountId IS NULL OR t.trading_account_id = @AccountId)
              AND (@Symbol IS NULL OR t.symbol ILIKE @Symbol)
              AND (@Side IS NULL OR t.side = @Side)
              AND (@Source IS NULL OR UPPER(t.source) = UPPER(@Source))
              AND (@From IS NULL OR t.executed_at >= @From)
              AND (@To IS NULL OR t.executed_at < @To)
              AND (@MarketType IS NULL OR t.market_type = @MarketType)
              AND (@ProductSubtype IS NULL OR t.product_subtype = @ProductSubtype)
            """;
        var args = new
        {
            TenantId = _tenant.TenantId,
            AccountId = query.TradingAccountId,
            Symbol = string.IsNullOrWhiteSpace(query.Symbol) ? null : "%" + query.Symbol.Trim() + "%",
            Side = string.IsNullOrWhiteSpace(query.Side) ? null : query.Side.Trim().ToUpperInvariant(),
            Source = string.IsNullOrWhiteSpace(query.Source) ? null : query.Source.Trim(),
            query.From,
            query.To,
            MarketType = string.IsNullOrWhiteSpace(query.MarketType) ? null : query.MarketType.Trim().ToUpperInvariant(),
            ProductSubtype = string.IsNullOrWhiteSpace(query.ProductSubtype) ? null : query.ProductSubtype.Trim().ToLowerInvariant(),
            Offset = (page - 1) * size,
            Limit = size,
        };
        var sql = $"""
            SELECT COUNT(*) FROM pack_personal_intelligence.trade t {where};
            SELECT
                t.trade_id AS TradeId, t.executed_at AS ExecutedAt, e.exchange_code AS ExchangeCode,
                a.external_account_id AS ExternalAccountId, t.symbol AS Symbol, t.side AS Side,
                t.price AS Price, t.quantity AS Quantity, t.fee_amount AS FeeAmount, t.fee_currency AS FeeCurrency,
                t.source AS Source, t.source_type AS SourceType, t.is_observed AS IsObserved,
                t.external_trade_id AS ExternalTradeId, t.metadata->>'category' AS Category,
                t.market_type AS MarketType, t.product_subtype AS ProductSubtype,
                COALESCE(t.metadata->>'realizedPnl', NULL) AS RealizedPnl
            FROM pack_personal_intelligence.trade t
            JOIN pack_personal_intelligence.trading_account a
                ON a.trading_account_id = t.trading_account_id AND a.tenant_id = t.tenant_id
            JOIN pack_personal_intelligence.exchange e
                ON e.exchange_id = a.exchange_id AND e.tenant_id = a.tenant_id
            {where}
            ORDER BY t.executed_at DESC
            LIMIT @Limit OFFSET @Offset
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        using var grid = await conn.QueryMultipleAsync(sql, args);
        var total = await grid.ReadSingleAsync<int>();
        var items = (await grid.ReadAsync<TradeRowDto>()).ToList();
        return new TradingPage<TradeRowDto>(items, total, page, size);
    }

    public async Task<TradeRowDto?> GetTradeAsync(Guid tradeId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                t.trade_id AS TradeId, t.executed_at AS ExecutedAt, e.exchange_code AS ExchangeCode,
                a.external_account_id AS ExternalAccountId, t.symbol AS Symbol, t.side AS Side,
                t.price AS Price, t.quantity AS Quantity, t.fee_amount AS FeeAmount, t.fee_currency AS FeeCurrency,
                t.source AS Source, t.source_type AS SourceType, t.is_observed AS IsObserved,
                t.external_trade_id AS ExternalTradeId, t.metadata->>'category' AS Category,
                t.market_type AS MarketType, t.product_subtype AS ProductSubtype,
                t.metadata->>'realizedPnl' AS RealizedPnl
            FROM pack_personal_intelligence.trade t
            JOIN pack_personal_intelligence.trading_account a
                ON a.trading_account_id = t.trading_account_id AND a.tenant_id = t.tenant_id
            JOIN pack_personal_intelligence.exchange e
                ON e.exchange_id = a.exchange_id AND e.tenant_id = a.tenant_id
            WHERE t.tenant_id = @TenantId AND t.trade_id = @TradeId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<TradeRowDto>(sql, new { TenantId = _tenant.TenantId, TradeId = tradeId });
    }

    public async Task<TradingPage<DecisionRowDto>> ListDecisionsAsync(
        int page, int pageSize, string? search, CancellationToken cancellationToken = default)
    {
        var (p, size) = Page(page, pageSize);
        const string where = """
            WHERE d.tenant_id = @TenantId
              AND (@Search IS NULL OR d.thesis ILIKE @Search)
            """;
        var args = new
        {
            TenantId = _tenant.TenantId,
            Search = string.IsNullOrWhiteSpace(search) ? null : "%" + search.Trim() + "%",
            Offset = (p - 1) * size,
            Limit = size,
        };
        var sql = $"""
            SELECT COUNT(*) FROM pack_personal_intelligence.pi_decision d {where};
            SELECT
                d.decision_id AS DecisionId, d.thesis AS Thesis, d.confidence AS Confidence,
                d.epistemic_kind AS EpistemicKind, d.status AS Status,
                op.operation_key AS OperationKey, op.result::text AS ResultJson
            FROM pack_personal_intelligence.pi_decision d
            LEFT JOIN LATERAL (
                SELECT operation_key, result FROM pack_personal_intelligence.pi_operation
                WHERE tenant_id = d.tenant_id AND decision_id = d.decision_id
                ORDER BY created_at DESC LIMIT 1
            ) op ON TRUE
            {where}
            ORDER BY d.decision_id DESC
            LIMIT @Limit OFFSET @Offset
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        using var grid = await conn.QueryMultipleAsync(sql, args);
        var total = await grid.ReadSingleAsync<int>();
        var rows = await grid.ReadAsync<DecisionJoin>();
        var items = rows.Select(MapDecision).ToList();
        return new TradingPage<DecisionRowDto>(items, total, p, size);
    }

    public async Task<DecisionDetailDto?> GetDecisionAsync(Guid decisionId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                d.decision_id AS DecisionId, d.thesis AS Thesis, d.confidence AS Confidence,
                d.epistemic_kind AS EpistemicKind, d.status AS Status,
                d.expected_outcome AS ExpectedOutcome, d.invalidation AS Invalidation,
                c.summary AS ContextSummary, c.epistemic_kind AS ContextKind, c.source AS ContextSource,
                a.intended AS Intended, a.actual AS Actual, a.status AS ActionStatus,
                o.summary AS OutcomeSummary, r.note AS ReflectionNote,
                x.statement AS ExperienceStatement, x.status AS ExperienceStatus,
                op.operation_key AS OperationKey, op.result::text AS ResultJson
            FROM pack_personal_intelligence.pi_decision d
            LEFT JOIN pack_personal_intelligence.pi_context c
                ON c.context_id = d.context_id AND c.tenant_id = d.tenant_id
            LEFT JOIN pack_personal_intelligence.pi_action a
                ON a.decision_id = d.decision_id AND a.tenant_id = d.tenant_id
            LEFT JOIN pack_personal_intelligence.pi_outcome o
                ON o.decision_id = d.decision_id AND o.tenant_id = d.tenant_id
            LEFT JOIN pack_personal_intelligence.pi_reflection r
                ON r.outcome_id = o.outcome_id AND r.tenant_id = d.tenant_id
            LEFT JOIN pack_personal_intelligence.pi_experience x
                ON x.reflection_id = r.reflection_id AND x.tenant_id = d.tenant_id
            LEFT JOIN LATERAL (
                SELECT operation_key, result FROM pack_personal_intelligence.pi_operation
                WHERE tenant_id = d.tenant_id AND decision_id = d.decision_id
                ORDER BY created_at DESC LIMIT 1
            ) op ON TRUE
            WHERE d.tenant_id = @TenantId AND d.decision_id = @DecisionId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<DecisionDetailJoin>(sql, new { TenantId = _tenant.TenantId, DecisionId = decisionId });
        if (row is null)
            return null;
        var evidence = (await conn.QueryAsync<EvidenceRowDto>(
            """
            SELECT ev.source_ref AS SourceRef, ev.epistemic_kind AS EpistemicKind
            FROM pack_personal_intelligence.pi_evidence ev
            JOIN pack_personal_intelligence.pi_decision d
                ON d.context_id = ev.context_id AND d.tenant_id = ev.tenant_id
            WHERE d.decision_id = @DecisionId AND d.tenant_id = @TenantId
            ORDER BY ev.source_ref
            """,
            new { TenantId = _tenant.TenantId, DecisionId = decisionId })).ToList();
        var parsed = Parse(row.ResultJson);
        return new DecisionDetailDto(
            MapDecision(row),
            row.ContextSummary,
            row.ContextKind,
            row.ContextSource,
            evidence,
            row.Intended,
            row.Actual,
            row.ActionStatus,
            row.OutcomeSummary,
            row.ReflectionNote,
            row.ExperienceStatement,
            row.ExperienceStatus,
            parsed?.PolicyStatus,
            null,
            parsed?.Unknowns ?? []);
    }

    public async Task<TradingPage<OperationRowDto>> ListOperationsAsync(
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var (p, size) = Page(page, pageSize);
        const string sql = """
            SELECT COUNT(*) FROM pack_personal_intelligence.pi_operation WHERE tenant_id = @TenantId;
            SELECT operation_id AS OperationId, operation_key AS OperationKey, decision_id AS DecisionId,
                   created_at AS CreatedAt, result::text AS ResultJson
            FROM pack_personal_intelligence.pi_operation
            WHERE tenant_id = @TenantId
            ORDER BY created_at DESC
            LIMIT @Limit OFFSET @Offset
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        using var grid = await conn.QueryMultipleAsync(sql, new
        {
            TenantId = _tenant.TenantId,
            Limit = size,
            Offset = (p - 1) * size,
        });
        var total = await grid.ReadSingleAsync<int>();
        var rows = await grid.ReadAsync<OperationJoin>();
        return new TradingPage<OperationRowDto>(rows.Select(MapOperation).ToList(), total, p, size);
    }

    public async Task<OperationRowDto?> GetOperationAsync(string operationKey, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT operation_id AS OperationId, operation_key AS OperationKey, decision_id AS DecisionId,
                   created_at AS CreatedAt, result::text AS ResultJson
            FROM pack_personal_intelligence.pi_operation
            WHERE tenant_id = @TenantId AND operation_key = @OperationKey
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<OperationJoin>(sql, new
        {
            TenantId = _tenant.TenantId,
            OperationKey = operationKey.Trim(),
        });
        return row is null ? null : MapOperation(row);
    }

    public async Task<TradingPage<PaperExecutionRowDto>> ListPaperExecutionsAsync(
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var (p, size) = Page(page, pageSize);
        const string sql = """
            SELECT COUNT(*) FROM pack_personal_intelligence.trading_execution
            WHERE tenant_id = @TenantId AND mode = 'PAPER';
            SELECT
                e.execution_id AS ExecutionId, o.decision_id AS DecisionId, e.mode AS Mode,
                o.symbol AS Symbol, o.side AS Side, o.quantity AS Quantity, o.limit_price AS Price,
                o.status AS OrderStatus, e.executed_at AS ExecutedAt
            FROM pack_personal_intelligence.trading_execution e
            JOIN pack_personal_intelligence.trading_order o
                ON o.order_id = e.order_id AND o.tenant_id = e.tenant_id
            WHERE e.tenant_id = @TenantId AND e.mode = 'PAPER'
            ORDER BY e.executed_at DESC
            LIMIT @Limit OFFSET @Offset
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        using var grid = await conn.QueryMultipleAsync(sql, new
        {
            TenantId = _tenant.TenantId,
            Limit = size,
            Offset = (p - 1) * size,
        });
        var total = await grid.ReadSingleAsync<int>();
        var items = (await grid.ReadAsync<PaperExecutionRowDto>()).ToList();
        return new TradingPage<PaperExecutionRowDto>(items, total, p, size);
    }

    public async Task<TradeImportPreviewDto> PreviewImportAsync(
        TradeImportRequest request, CancellationToken cancellationToken = default)
    {
        var parsed = await ParseImportAsync(request, cancellationToken);
        return new TradeImportPreviewDto(
            parsed.Accepted.Count,
            parsed.Duplicates.Count,
            parsed.Duplicates,
            parsed.Accepted.Take(20).Select(row => new TradePreviewRowDto(
                row.ExternalTradeId, row.Symbol, row.Side, row.Quantity, row.Price,
                row.ExecutedAt, row.FeeAmount, row.FeeCurrency)).ToList());
    }

    public async Task<TradeImportCommitDto> CommitImportAsync(
        TradeImportRequest request, CancellationToken cancellationToken = default)
    {
        var parsed = await ParseImportAsync(request, cancellationToken);
        if (parsed.Accepted.Count == 0)
            return new TradeImportCommitDto(0, parsed.Duplicates.Count);
        var source = NormalizeSource(request.Source);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var inserted = 0;
        foreach (var trade in parsed.Accepted)
        {
            inserted += await conn.ExecuteAsync(
                """
                INSERT INTO pack_personal_intelligence.trade (
                    person_id, tenant_id, trading_account_id, external_trade_id, symbol, side,
                    quantity, price, total_value, fee_amount, fee_currency, executed_at, source, source_type, is_observed)
                SELECT a.person_id, a.tenant_id, a.trading_account_id, @ExternalTradeId, @Symbol, @Side,
                       @Quantity, @Price, @Total, @FeeAmount, @FeeCurrency, @ExecutedAt, @Source, 'IMPORT', TRUE
                FROM pack_personal_intelligence.trading_account a
                WHERE a.trading_account_id = @AccountId AND a.tenant_id = @TenantId AND a.deleted_at IS NULL
                ON CONFLICT (trading_account_id, external_trade_id) DO NOTHING
                """,
                new
                {
                    TenantId = _tenant.TenantId,
                    AccountId = request.TradingAccountId,
                    trade.ExternalTradeId,
                    trade.Symbol,
                    trade.Side,
                    trade.Quantity,
                    trade.Price,
                    Total = trade.Quantity * trade.Price,
                    trade.FeeAmount,
                    trade.FeeCurrency,
                    trade.ExecutedAt,
                    Source = source,
                });
        }

        return new TradeImportCommitDto(inserted, parsed.Duplicates.Count + (parsed.Accepted.Count - inserted));
    }

    public async Task<PaperApprovalDto> ApprovePaperAsync(Guid decisionId, CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var existing = await conn.QuerySingleOrDefaultAsync<(Guid ExecutionId, string Mode)>(
            """
            SELECT e.execution_id AS ExecutionId, e.mode AS Mode
            FROM pack_personal_intelligence.trading_execution e
            JOIN pack_personal_intelligence.trading_order o ON o.order_id = e.order_id AND o.tenant_id = e.tenant_id
            WHERE o.tenant_id = @TenantId AND o.decision_id = @DecisionId
            LIMIT 1
            """,
            new { TenantId = _tenant.TenantId, DecisionId = decisionId });
        if (existing.ExecutionId != Guid.Empty)
        {
            await MarkPaperRecordedAsync(conn, decisionId, null);
            return new PaperApprovalDto(decisionId, existing.ExecutionId, existing.Mode, true);
        }

        var stored = await conn.QuerySingleOrDefaultAsync<string>(
            """
            SELECT result::text FROM pack_personal_intelligence.pi_operation
            WHERE tenant_id = @TenantId AND decision_id = @DecisionId
            ORDER BY created_at DESC LIMIT 1
            """,
            new { TenantId = _tenant.TenantId, DecisionId = decisionId });
        var result = Parse(stored) ?? throw new InvalidOperationException("Không tìm thấy quyết định trong tenant hiện tại.");
        if (!result.RiskAllowed)
            throw new InvalidOperationException("Risk Engine đã từ chối. Không tạo paper execution.");
        if (result.TradingAccountId == Guid.Empty || string.IsNullOrWhiteSpace(result.Symbol))
            throw new InvalidOperationException("Quyết định này chưa lưu đủ đề xuất để phê duyệt paper.");

        var executionId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        await conn.ExecuteAsync(
            PaperExecutionSql.InsertOrder,
            new
            {
                OrderId = orderId,
                TenantId = _tenant.TenantId,
                PersonId = result.PersonId,
                TradingAccountId = result.TradingAccountId,
                ExternalOrderId = decisionId.ToString("N"),
                Symbol = result.Symbol,
                Side = result.Side,
                Quantity = result.Quantity,
                Price = result.Price,
                Status = "paper",
                DecisionId = decisionId,
            },
            tx);
        await conn.ExecuteAsync(
            PaperExecutionSql.InsertExecution,
            new
            {
                ExecutionId = executionId,
                TenantId = _tenant.TenantId,
                TradingAccountId = result.TradingAccountId,
                OrderId = orderId,
                ExternalExecutionId = decisionId.ToString("N") + ":fill",
                Mode = "PAPER",
            },
            tx);
        await conn.ExecuteAsync(
            """
            UPDATE pack_personal_intelligence.pi_action
            SET actual = 'paper_filled', status = 'paper_filled'
            WHERE tenant_id = @TenantId AND decision_id = @DecisionId AND status <> 'paper_filled'
            """,
            new { TenantId = _tenant.TenantId, DecisionId = decisionId },
            tx);
        await MarkPaperRecordedAsync(conn, decisionId, tx);
        await tx.CommitAsync(cancellationToken);
        return new PaperApprovalDto(decisionId, executionId, "PAPER", false);
    }

    private Task MarkPaperRecordedAsync(System.Data.IDbConnection conn, Guid decisionId, System.Data.IDbTransaction? tx) =>
        conn.ExecuteAsync(
            """
            UPDATE pack_personal_intelligence.pi_outcome
            SET summary = 'Đã ghi giả lập. Không gửi lệnh lên sàn.'
            WHERE tenant_id = @TenantId AND decision_id = @DecisionId;
            UPDATE pack_personal_intelligence.pi_operation
            SET result = jsonb_set(
                jsonb_set(result, '{PaperExecuted}', 'true'::jsonb, true),
                '{ExecutionMode}', '"PAPER"'::jsonb, true)
            WHERE tenant_id = @TenantId AND decision_id = @DecisionId;
            """,
            new { TenantId = _tenant.TenantId, DecisionId = decisionId },
            tx);

    private async Task<ImportParse> ParseImportAsync(TradeImportRequest request, CancellationToken cancellationToken)
    {
        if (request.Csv.Length > 1_000_000)
            throw new InvalidOperationException("Tệp CSV vượt quá 1 MB.");
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var owned = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*) FROM pack_personal_intelligence.trading_account
            WHERE trading_account_id = @AccountId AND tenant_id = @TenantId AND deleted_at IS NULL
            """,
            new { AccountId = request.TradingAccountId, TenantId = _tenant.TenantId });
        if (owned != 1)
            throw new InvalidOperationException("Tài khoản giao dịch không thuộc tenant hiện tại.");
        var stored = (await conn.QueryAsync<string>(
            """
            SELECT external_trade_id FROM pack_personal_intelligence.trade
            WHERE tenant_id = @TenantId AND trading_account_id = @AccountId
            """,
            new { TenantId = _tenant.TenantId, AccountId = request.TradingAccountId })).ToHashSet(StringComparer.Ordinal);
        var parsed = HistoricalTradeImport.ImportCsv(request.TradingAccountId, request.Csv, stored);
        return new ImportParse(parsed.Accepted, parsed.DuplicateExternalIds);
    }

    private static string NormalizeSource(string? source)
    {
        var value = source?.Trim().ToUpperInvariant();
        return value is "FIXTURE" or "IMPORT" ? value : throw new InvalidOperationException("Nguồn chỉ được là FIXTURE hoặc IMPORT.");
    }

    private static (int Page, int Size) Page(int page, int pageSize)
    {
        var p = page < 1 ? 1 : page;
        var size = pageSize is < 1 or > 100 ? 50 : pageSize;
        return (p, size);
    }

    private static TradingLoopResult? Parse(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<TradingLoopResult>(json, JsonOptions);

    private static DecisionRowDto MapDecision(DecisionJoin row)
    {
        var parsed = Parse(row.ResultJson);
        return new DecisionRowDto(
            row.DecisionId, row.Thesis, row.Confidence, row.EpistemicKind, row.Status,
            parsed?.RiskAllowed, parsed?.PaperExecuted, row.OperationKey);
    }

    private static OperationRowDto MapOperation(OperationJoin row)
    {
        var parsed = Parse(row.ResultJson);
        return new OperationRowDto(
            row.OperationId, row.OperationKey, row.DecisionId, row.CreatedAt,
            parsed?.RiskAllowed, parsed?.PaperExecuted, parsed?.ExecutionMode, parsed?.Autonomy,
            parsed?.EvidenceBasis, parsed?.Unknowns ?? []);
    }

    private sealed class SummaryRow
    {
        public int TradeCount { get; set; }
        public int FixtureTradeCount { get; set; }
        public int OtherSourceTradeCount { get; set; }
        public int DecisionCount { get; set; }
        public int PaperExecutionCount { get; set; }
        public int RiskVetoCount { get; set; }
        public string? LatestSource { get; set; }
        public DateTimeOffset? LatestRecordedAt { get; set; }
        public int MissingFeeCount { get; set; }
        public int ImportTradeCount { get; set; }
        public int ExchangeTradeCount { get; set; }
    }

    private class DecisionJoin
    {
        public Guid DecisionId { get; set; }
        public string Thesis { get; set; } = "";
        public decimal Confidence { get; set; }
        public string EpistemicKind { get; set; } = "";
        public string Status { get; set; } = "";
        public string? OperationKey { get; set; }
        public string? ResultJson { get; set; }
    }

    private sealed class DecisionDetailJoin : DecisionJoin
    {
        public string? ContextSummary { get; set; }
        public string? ContextKind { get; set; }
        public string? ContextSource { get; set; }
        public string? Intended { get; set; }
        public string? Actual { get; set; }
        public string? ActionStatus { get; set; }
        public string? OutcomeSummary { get; set; }
        public string? ReflectionNote { get; set; }
        public string? ExperienceStatement { get; set; }
        public string? ExperienceStatus { get; set; }
    }

    private sealed class OperationJoin
    {
        public Guid OperationId { get; set; }
        public string OperationKey { get; set; } = "";
        public Guid? DecisionId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string? ResultJson { get; set; }
    }

    private sealed record ImportParse(IReadOnlyList<ObservedTrade> Accepted, IReadOnlyList<string> Duplicates);
}
