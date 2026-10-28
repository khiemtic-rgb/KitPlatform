using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.PersonalIntelligence.Intent;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;

public sealed class PaperDecisionRecorder : IPaperDecisionRecorder
{
    readonly IDbConnectionFactory _db;
    readonly ITenantContext _tenant;
    readonly ITradingOperationalLoop _loop;

    public PaperDecisionRecorder(IDbConnectionFactory db, ITenantContext tenant, ITradingOperationalLoop loop)
    {
        _db = db;
        _tenant = tenant;
        _loop = loop;
    }

    public async Task<RecordedPaperDecision> RecordProposedAsync(
        BusinessCaller caller,
        IReadOnlyDictionary<string, string> slots,
        CancellationToken cancellationToken)
    {
        if (caller.TenantId != _tenant.TenantId)
            throw new InvalidOperationException("Phiên đăng nhập không khớp tenant của quyết định.");
        if (!string.Equals(slots.GetValueOrDefault("mode"), "paper", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chỉ lệnh paper đã nêu mode mới được lưu thành decision.");

        var symbol = Required(slots, "symbol").ToUpperInvariant();
        var side = Required(slots, "side").ToUpperInvariant();
        var quantity = RequiredDecimal(slots, "quantity");
        var price = RequiredDecimal(slots, "price");
        var leverage = RequiredDecimal(slots, "leverage");
        var confidence = RequiredDecimal(slots, "confidence");
        var maxNotional = RequiredDecimal(slots, "maxNotional");
        var maxLeverage = RequiredDecimal(slots, "maxLeverage");
        var minConfidence = RequiredDecimal(slots, "minConfidence");
        if (confidence is < 0 or > 1 || minConfidence is < 0 or > 1)
            throw new InvalidOperationException("Tin cậy phải nằm trong khoảng 0 đến 1. Decision không được lưu.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var anchor = await conn.QuerySingleOrDefaultAsync<(Guid PersonId, Guid TradingAccountId)>(
            """
            SELECT p.person_id AS PersonId, a.trading_account_id AS TradingAccountId
            FROM pack_personal_intelligence.person p
            JOIN public.tenants t ON t.id = p.tenant_id
            JOIN pack_personal_intelligence.trading_account a
              ON a.person_id = p.person_id AND a.tenant_id = p.tenant_id AND a.deleted_at IS NULL
            JOIN pack_personal_intelligence.exchange e
              ON e.exchange_id = a.exchange_id AND e.tenant_id = p.tenant_id
            WHERE p.user_id = @UserId
              AND p.tenant_id = @TenantId
              AND p.deleted_at IS NULL
              AND t.tenant_code = 'KIT_MKT'
              AND e.exchange_code = 'PAPER_LEDGER'
              AND a.external_account_id = 'KIT_MKT-PAPER-LEDGER'
            """,
            new { UserId = caller.UserId, TenantId = caller.TenantId });
        if (anchor.PersonId == Guid.Empty)
            throw new InvalidOperationException("Tenant này không có neo sổ paper nội bộ. Decision không được lưu.");

        var key = OperationKey(caller.TenantId, symbol, side, quantity, price, leverage, confidence, maxNotional, maxLeverage, minConfidence);
        var result = await _loop.RunFixtureAsync(new TradingLoopRequest
        {
            PersonId = anchor.PersonId,
            TradingAccountId = anchor.TradingAccountId,
            FixtureCsv = "",
            Thesis = "Paper ledger proposal " + symbol + " " + side + " " + quantity.ToString(CultureInfo.InvariantCulture) + " @ " + price.ToString(CultureInfo.InvariantCulture),
            ExpectedOutcome = "Separate approval required before any paper fill.",
            Invalidation = "Rejected decisions are not filled.",
            Symbol = symbol,
            Side = side,
            Quantity = quantity,
            Price = price,
            Leverage = leverage,
            Confidence = confidence,
            MaxNotional = maxNotional,
            MaxLeverage = maxLeverage,
            MinConfidence = minConfidence,
            ApprovedBy = null,
            Live = false,
            OperationKey = key,
        }, cancellationToken);
        var status = await conn.QuerySingleAsync<string>(
            """
            SELECT status FROM pack_personal_intelligence.pi_decision
            WHERE tenant_id = @TenantId AND decision_id = @DecisionId
            """,
            new { TenantId = caller.TenantId, result.DecisionId });
        return new RecordedPaperDecision(result.DecisionId, key, status);
    }

    static string OperationKey(
        Guid tenantId,
        string symbol,
        string side,
        decimal quantity,
        decimal price,
        decimal leverage,
        decimal confidence,
        decimal maxNotional,
        decimal maxLeverage,
        decimal minConfidence)
    {
        var canonical = string.Join('\n',
            tenantId.ToString("N"),
            PlatformCapabilityCatalog.TradingPaperEvaluate,
            symbol,
            side,
            quantity.ToString(CultureInfo.InvariantCulture),
            price.ToString(CultureInfo.InvariantCulture),
            "paper",
            leverage.ToString(CultureInfo.InvariantCulture),
            confidence.ToString(CultureInfo.InvariantCulture),
            maxNotional.ToString(CultureInfo.InvariantCulture),
            maxLeverage.ToString(CultureInfo.InvariantCulture),
            minConfidence.ToString(CultureInfo.InvariantCulture));
        return "pi-paper-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    static string Required(IReadOnlyDictionary<string, string> slots, string name)
    {
        if (!slots.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Thiếu " + name + ". Decision không được lưu.");
        return value.Trim();
    }

    static decimal RequiredDecimal(IReadOnlyDictionary<string, string> slots, string name)
    {
        var text = Required(slots, name);
        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            throw new InvalidOperationException("Không đọc được " + name + ". Decision không được lưu.");
        return value;
    }
}
