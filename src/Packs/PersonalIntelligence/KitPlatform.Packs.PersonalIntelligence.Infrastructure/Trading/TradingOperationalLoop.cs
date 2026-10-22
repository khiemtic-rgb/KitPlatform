using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Core;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.PersonalIntelligence;
using KitPlatform.Packs.PersonalIntelligence.Infrastructure.Intelligence;
using KitPlatform.Packs.PersonalIntelligence.Trading;
using Npgsql;

namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;

internal sealed class TradingOperationalLoop : ITradingOperationalLoop
{
    public const string FixtureSource = "FIXTURE";

    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public TradingOperationalLoop(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public Task<TradingLoopResult> RunFixtureAsync(TradingLoopRequest request, CancellationToken cancellationToken = default) =>
        RunAsync(request, failBeforeCommit: false, cancellationToken);

    internal Task<TradingLoopResult> ProbeRollbackAsync(TradingLoopRequest request, CancellationToken cancellationToken = default) =>
        RunAsync(request, failBeforeCommit: true, cancellationToken);

    private async Task<TradingLoopResult> RunAsync(
        TradingLoopRequest request,
        bool failBeforeCommit,
        CancellationToken cancellationToken)
    {
        TenantScope.RequireCurrent(_tenant.TenantId);
        if (request.RequestedTenantId is Guid requested)
            CoreAuthority.RejectTenantSwitch(_tenant.TenantId, requested);
        if (request.Live)
            throw new InvalidOperationException("Live execution is not available.");
        var operationKey = request.OperationKey?.Trim() ?? "";
        if (operationKey.Length is 0 or > 128)
            throw new InvalidOperationException("Operation key is required.");
        var payloadHash = HashPayload(request);

        var usingStoredTrades = request.TradeIds is { Length: > 0 };
        var imported = usingStoredTrades
            ? new TradeImportResult([], [])
            : HistoricalTradeImport.ImportCsv(request.TradingAccountId, request.FixtureCsv, new HashSet<string>());
        var proposal = new TradeProposal(
            request.Symbol.Trim().ToUpperInvariant(),
            request.Side.Trim().ToUpperInvariant(),
            request.Quantity,
            request.Price,
            request.Leverage,
            request.Confidence,
            ExecutionMode.Paper);
        var limits = new RiskLimits(request.MaxNotional, request.MaxLeverage, request.MinConfidence);
        var recommendation = TradingAgent.Recommend(
            _tenant.TenantId,
            imported.Accepted,
            proposal,
            limits,
            usingStoredTrades
                ? ["Không có dữ liệu thị trường live. Kết quả chỉ đối chiếu giao dịch đã lưu với ngưỡng rủi ro."]
                : ["Fixture tape is not a live market.", "No exchange export was supplied."]);
        var approval = string.IsNullOrWhiteSpace(request.ApprovedBy)
            ? null
            : new ExecutionApproval(Guid.NewGuid(), request.ApprovedBy.Trim());
        var paper = DemoPaperExecution.Execute(recommendation.Proposal, recommendation.Verdict, approval);
        var plan = PaperPersistencePlanBuilder.From(paper);

        var loopId = Guid.NewGuid();
        var contextId = Guid.NewGuid();
        var decisionId = approval is null ? Guid.NewGuid() : approval.DecisionId;
        var outcomeId = Guid.NewGuid();
        var reflectionId = Guid.NewGuid();
        var experienceId = Guid.NewGuid();
        var policyId = Guid.NewGuid();
        var eventTypes = new List<string>();

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        try
        {
            var operationId = await conn.ExecuteScalarAsync<Guid?>(
                """
                INSERT INTO pack_personal_intelligence.pi_operation (
                    operation_id, tenant_id, operation_key, payload_hash, result)
                VALUES (@OperationId, @TenantId, @OperationKey, @PayloadHash, '{}'::jsonb)
                ON CONFLICT (tenant_id, operation_key) DO NOTHING
                RETURNING operation_id
                """,
                new
                {
                    OperationId = Guid.NewGuid(),
                    TenantId = _tenant.TenantId,
                    OperationKey = operationKey,
                    PayloadHash = payloadHash,
                },
                tx);
            if (operationId is null)
            {
                var existing = await conn.QuerySingleAsync<(string PayloadHash, string ResultJson)>(
                    """
                    SELECT payload_hash AS PayloadHash, result::text AS ResultJson
                    FROM pack_personal_intelligence.pi_operation
                    WHERE tenant_id = @TenantId AND operation_key = @OperationKey
                    """,
                    new { TenantId = _tenant.TenantId, OperationKey = operationKey },
                    tx);
                if (!string.Equals(existing.PayloadHash, payloadHash, StringComparison.Ordinal))
                    throw new TradingOperationConflictException();

                await tx.CommitAsync(cancellationToken);
                return JsonSerializer.Deserialize<TradingLoopResult>(existing.ResultJson, JsonOptions)
                    ?? throw new InvalidOperationException("The stored operation result is empty.");
            }

            var owned = await conn.ExecuteScalarAsync<int>(
                """
                SELECT COUNT(*) FROM pack_personal_intelligence.trading_account
                WHERE trading_account_id = @AccountId
                  AND person_id = @PersonId
                  AND tenant_id = @TenantId
                  AND deleted_at IS NULL
                """,
                new { AccountId = request.TradingAccountId, request.PersonId, TenantId = _tenant.TenantId },
                tx);
            if (owned != 1)
                throw new InvalidOperationException("The trading account is not in the current tenant.");

            IReadOnlyList<ObservedTrade> evidence = imported.Accepted;
            var contextSource = FixtureSource;
            if (usingStoredTrades)
            {
                var ids = request.TradeIds!.Distinct().ToArray();
                if (ids.Length > 30)
                    throw new InvalidOperationException("Chọn tối đa 30 giao dịch cho một phiên.");
                var selected = (await conn.QueryAsync<StoredTrade>(
                    """
                    SELECT trade_id AS TradeId,
                           external_trade_id AS ExternalTradeId,
                           symbol AS Symbol,
                           side AS Side,
                           quantity AS Quantity,
                           price AS Price,
                           executed_at AS ExecutedAt,
                           source AS Source,
                           fee_amount AS FeeAmount
                    FROM pack_personal_intelligence.trade
                    WHERE tenant_id = @TenantId
                      AND trading_account_id = @AccountId
                      AND trade_id = ANY(@Ids)
                    """,
                    new { TenantId = _tenant.TenantId, AccountId = request.TradingAccountId, Ids = ids },
                    tx)).ToArray();
                if (selected.Length != ids.Length)
                    throw new InvalidOperationException("Một số giao dịch không thuộc sổ đã chọn.");
                var labels = selected.Select(row => (row.Source ?? "").Trim().ToUpperInvariant()).Distinct().ToArray();
                contextSource = labels.Length == 1 ? labels[0] : "MIXED";
                evidence = selected.Select(row => new ObservedTrade(
                    request.TradingAccountId,
                    row.ExternalTradeId,
                    row.Symbol,
                    row.Side,
                    row.Quantity,
                    row.Price,
                    ToOffset(row.ExecutedAt),
                    row.FeeAmount,
                    null,
                    EpistemicKind.Observation)).ToArray();
                recommendation = TradingAgent.Recommend(
                    _tenant.TenantId,
                    evidence,
                    proposal,
                    limits,
                    UnknownsForStored(selected));
                paper = DemoPaperExecution.Execute(recommendation.Proposal, recommendation.Verdict, approval);
                plan = PaperPersistencePlanBuilder.From(paper);
            }

            foreach (var trade in usingStoredTrades ? [] : evidence)
            {
                await conn.ExecuteAsync(
                    """
                    INSERT INTO pack_personal_intelligence.trade (
                        person_id, tenant_id, trading_account_id, external_trade_id, symbol, side,
                        quantity, price, total_value, executed_at, source, source_type, is_observed)
                    VALUES (
                        @PersonId, @TenantId, @AccountId, @ExternalTradeId, @Symbol, @Side,
                        @Quantity, @Price, @Total, @ExecutedAt, @Source, 'IMPORT', TRUE)
                    ON CONFLICT (trading_account_id, external_trade_id) DO NOTHING
                    """,
                    new
                    {
                        request.PersonId,
                        TenantId = _tenant.TenantId,
                        AccountId = request.TradingAccountId,
                        trade.ExternalTradeId,
                        trade.Symbol,
                        trade.Side,
                        trade.Quantity,
                        trade.Price,
                        Total = trade.Quantity * trade.Price,
                        trade.ExecutedAt,
                        Source = FixtureSource,
                    },
                    tx);
            }

            await WriteAsync(conn, tx, IntelligenceSql.InsertContext, new
            {
                ContextId = contextId,
                TenantId = _tenant.TenantId,
                request.PersonId,
                Summary = usingStoredTrades
                    ? "Các giao dịch đã lưu được chọn làm dữ liệu quan sát. Đây chưa phải một quyết định."
                    : "Fixture trading context",
                EpistemicKind = EpistemicKindSql.ToDb(EpistemicKind.Observation),
                Source = contextSource,
            });
            await EventAsync(conn, tx, request.PersonId, "ContextObserved", EpistemicKind.Observation, contextId, eventTypes);

            foreach (var trade in evidence)
            {
                var evidenceId = Guid.NewGuid();
                await WriteAsync(conn, tx, IntelligenceSql.InsertEvidence, new
                {
                    EvidenceId = evidenceId,
                    TenantId = _tenant.TenantId,
                    request.PersonId,
                    ContextId = contextId,
                    SourceRef = trade.ExternalTradeId,
                    EpistemicKind = EpistemicKindSql.ToDb(EpistemicKind.Observation),
                });
                await EventAsync(conn, tx, request.PersonId, "EvidenceRecorded", EpistemicKind.Observation, evidenceId, eventTypes);
            }

            await WriteAsync(conn, tx, IntelligenceSql.InsertDecision, new
            {
                DecisionId = decisionId,
                TenantId = _tenant.TenantId,
                request.PersonId,
                ContextId = contextId,
                request.Thesis,
                request.Confidence,
                request.ExpectedOutcome,
                request.Invalidation,
                EpistemicKind = EpistemicKindSql.ToDb(EpistemicKind.Hypothesis),
            });
            await EventAsync(conn, tx, request.PersonId, "DecisionMade", EpistemicKind.Hypothesis, decisionId, eventTypes);

            if (plan.WriteExecution)
            {
                var orderId = Guid.NewGuid();
                await WriteAsync(conn, tx, IntelligenceSql.InsertAction, new
                {
                    ActionId = Guid.NewGuid(),
                    TenantId = _tenant.TenantId,
                    request.PersonId,
                    DecisionId = decisionId,
                    Intended = $"{proposal.Side} {proposal.Quantity} {proposal.Symbol}",
                    Actual = "paper_filled",
                    Status = plan.ActionStatus,
                });
                await WriteAsync(conn, tx, PaperExecutionSql.InsertOrder, new
                {
                    OrderId = orderId,
                    TenantId = _tenant.TenantId,
                    request.PersonId,
                    TradingAccountId = request.TradingAccountId,
                    ExternalOrderId = decisionId.ToString("N"),
                    proposal.Symbol,
                    proposal.Side,
                    proposal.Quantity,
                    proposal.Price,
                    Status = plan.OrderStatus,
                    DecisionId = decisionId,
                });
                await WriteAsync(conn, tx, PaperExecutionSql.InsertExecution, new
                {
                    ExecutionId = Guid.NewGuid(),
                    TenantId = _tenant.TenantId,
                    TradingAccountId = request.TradingAccountId,
                    OrderId = orderId,
                    ExternalExecutionId = decisionId.ToString("N") + ":fill",
                    Mode = plan.ExecutionMode,
                });
            }

            await EventAsync(conn, tx, request.PersonId, "ActionTaken", EpistemicKind.Observation, decisionId, eventTypes, plan.ActionStatus);

            var stored = (await conn.QueryAsync<StoredTrade>(
                """
                SELECT external_trade_id AS ExternalTradeId, symbol AS Symbol, side AS Side,
                       quantity AS Quantity, price AS Price, executed_at AS ExecutedAt
                FROM pack_personal_intelligence.trade
                WHERE tenant_id = @TenantId
                  AND trading_account_id = @AccountId
                  AND external_trade_id = ANY(@Ids)
                """,
                new
                {
                    TenantId = _tenant.TenantId,
                    AccountId = request.TradingAccountId,
                    Ids = evidence.Select(row => row.ExternalTradeId).ToArray(),
                },
                tx)).ToArray();
            var evaluation = FixturePerformanceEvaluation.Evaluate(stored.Select(row => new ObservedTrade(
                request.TradingAccountId,
                row.ExternalTradeId,
                row.Symbol,
                row.Side,
                row.Quantity,
                row.Price,
                row.ExecutedAt,
                null,
                null,
                EpistemicKind.Observation)).ToArray());

            await WriteAsync(conn, tx, IntelligenceSql.InsertOutcome, new
            {
                OutcomeId = outcomeId,
                TenantId = _tenant.TenantId,
                request.PersonId,
                DecisionId = decisionId,
                Summary = paper.Executed
                    ? "Đã ghi giả lập. Không gửi lệnh lên sàn."
                    : "Chưa ghi giả lập. " + ExplainRisk(paper.Reasons.FirstOrDefault()),
                EpistemicKind = EpistemicKindSql.ToDb(EpistemicKind.Observation),
            });
            await EventAsync(conn, tx, request.PersonId, "OutcomeObserved", EpistemicKind.Observation, outcomeId, eventTypes);

            if (!string.IsNullOrWhiteSpace(request.ReflectionNote))
            {
                await WriteAsync(conn, tx, IntelligenceSql.InsertReflection, new
                {
                    ReflectionId = reflectionId,
                    TenantId = _tenant.TenantId,
                    request.PersonId,
                    OutcomeId = outcomeId,
                    Note = request.ReflectionNote,
                    EpistemicKind = EpistemicKindSql.ToDb(EpistemicKind.Inference),
                });
                await EventAsync(conn, tx, request.PersonId, "ReflectionCreated", EpistemicKind.Inference, reflectionId, eventTypes);
            }

            if (!usingStoredTrades)
            {
                await WriteAsync(conn, tx, IntelligenceSql.InsertExperience, new
                {
                    ExperienceId = experienceId,
                    TenantId = _tenant.TenantId,
                    request.PersonId,
                    Statement = "Fixture loop candidate. Not a verified trading rule.",
                    Status = "candidate",
                    EpistemicKind = EpistemicKindSql.ToDb(EpistemicKind.Hypothesis),
                });
                await EventAsync(conn, tx, request.PersonId, "ExperienceDerived", EpistemicKind.Hypothesis, experienceId, eventTypes);
            }

            if (!string.IsNullOrWhiteSpace(request.PolicyRule))
            {
                await WriteAsync(conn, tx, IntelligenceSql.InsertPolicy, new
                {
                    PolicyId = policyId,
                    TenantId = _tenant.TenantId,
                    request.PersonId,
                    RuleText = request.PolicyRule,
                    Status = "proposed",
                });
                await EventAsync(conn, tx, request.PersonId, "PolicyProposed", EpistemicKind.Policy, policyId, eventTypes);
            }

            if (failBeforeCommit)
            {
                await conn.ExecuteAsync(
                    """
                    INSERT INTO pack_personal_intelligence.pi_events (
                        tenant_id, person_id, event_type, occurred_at, actor_type, correlation_id, source, epistemic_kind)
                    VALUES (
                        @TenantId, @PersonId, 'NOT_A_REAL_EVENT', NOW(), 'HUMAN', @LoopId, 'operational-loop', 'OBSERVATION')
                    """,
                    new { TenantId = _tenant.TenantId, request.PersonId, LoopId = loopId },
                    tx);
            }

            var result = new TradingLoopResult(
                loopId,
                decisionId,
                "proposed",
                recommendation.Verdict.Allowed,
                paper.Executed,
                plan.ExecutionMode,
                recommendation.Autonomy,
                evaluation.Basis.ToString(),
                evaluation.ReconstructedNetQuantity,
                stored.Length,
                eventTypes,
                recommendation.Unknowns,
                request.TradingAccountId,
                request.PersonId,
                proposal.Symbol,
                proposal.Side,
                proposal.Quantity,
                proposal.Price,
                recommendation.Verdict.Reasons);
            await conn.ExecuteAsync(
                """
                UPDATE pack_personal_intelligence.pi_operation
                SET decision_id = @DecisionId, result = @Result::jsonb
                WHERE operation_id = @OperationId AND tenant_id = @TenantId
                """,
                new
                {
                    OperationId = operationId,
                    TenantId = _tenant.TenantId,
                    DecisionId = decisionId,
                    Result = JsonSerializer.Serialize(result, JsonOptions),
                },
                tx);
            await tx.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static string HashPayload(TradingLoopRequest request)
    {
        var canonical = string.Join('\n',
            request.PersonId.ToString("N"),
            request.TradingAccountId.ToString("N"),
            request.FixtureCsv ?? "",
            request.Thesis ?? "",
            request.ExpectedOutcome ?? "",
            request.Invalidation ?? "",
            request.Symbol ?? "",
            request.Side ?? "",
            request.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
            request.Price.ToString(System.Globalization.CultureInfo.InvariantCulture),
            request.Leverage.ToString(System.Globalization.CultureInfo.InvariantCulture),
            request.Confidence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            request.MaxNotional.ToString(System.Globalization.CultureInfo.InvariantCulture),
            request.MaxLeverage.ToString(System.Globalization.CultureInfo.InvariantCulture),
            request.MinConfidence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            request.ApprovedBy ?? "",
            request.ReflectionNote ?? "",
            request.PolicyRule ?? "",
            request.Live ? "1" : "0");
        if (request.TradeIds is { Length: > 0 })
            canonical += "\n" + string.Join(',', request.TradeIds.Distinct().OrderBy(id => id));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static Task WriteAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, object args) =>
        conn.ExecuteAsync(sql, args, tx);

    private async Task EventAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        Guid personId,
        string eventType,
        EpistemicKind kind,
        Guid correlationId,
        List<string> eventTypes,
        string? suffix = null)
    {
        await conn.ExecuteAsync(
            IntelligenceSql.InsertEvent,
            new
            {
                EventId = Guid.NewGuid(),
                TenantId = _tenant.TenantId,
                PersonId = personId,
                EventType = eventType,
                OccurredAt = DateTimeOffset.UtcNow,
                ActorType = "HUMAN",
                CorrelationId = correlationId,
                Source = "operational-loop",
                EpistemicKind = EpistemicKindSql.ToDb(kind),
                ExternalEventId = correlationId.ToString("N") + ":" + eventType + (suffix is null ? "" : ":" + suffix),
            },
            tx);
        eventTypes.Add(eventType);
    }

    private sealed class StoredTrade
    {
        public string ExternalTradeId { get; set; } = "";
        public string Symbol { get; set; } = "";
        public string Side { get; set; } = "";
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public DateTime ExecutedAt { get; set; }
        public Guid TradeId { get; set; }
        public string Source { get; set; } = "";
        public decimal? FeeAmount { get; set; }
    }

    static DateTimeOffset ToOffset(DateTime value)
    {
        var time = value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value;
        return new DateTimeOffset(time);
    }

    static List<string> UnknownsForStored(IReadOnlyList<StoredTrade> rows)
    {
        var unknowns = new List<string>
        {
            "Không có dữ liệu thị trường live. Kết quả chỉ đối chiếu giao dịch đã lưu với ngưỡng rủi ro.",
        };
        if (rows.All(row => string.Equals(row.Source, "FIXTURE", StringComparison.OrdinalIgnoreCase)))
            unknowns.Add("Các giao dịch được chọn đang mang nhãn kiểm thử.");
        if (rows.Any(row => row.FeeAmount is null))
            unknowns.Add("Một số giao dịch chưa có phí.");
        return unknowns;
    }

    static string ExplainRisk(string? reason) => reason switch
    {
        "Notional exceeds the configured maximum." => "Quy mô lệnh vượt trần đã đặt.",
        "Leverage exceeds the configured maximum." => "Đòn bẩy vượt trần đã đặt.",
        "Confidence is below the configured minimum." => "Mức tin cậy thấp hơn ngưỡng đã đặt.",
        "Live execution is outside Trading Agent V1." => "Không gửi lệnh thật.",
        "Quantity and price must be positive." => "Thiếu giá hoặc số lượng.",
        "Symbol is required." => "Thiếu mã giao dịch.",
        "Side must be BUY or SELL." => "Chiều giao dịch không hợp lệ.",
        "Paper execution requires a separate approval." => "Giả lập cần một bước phê duyệt riêng.",
        null or "" => "Rủi ro hoặc phê duyệt chưa cho phép ghi giả lập.",
        _ => reason,
    };
}
