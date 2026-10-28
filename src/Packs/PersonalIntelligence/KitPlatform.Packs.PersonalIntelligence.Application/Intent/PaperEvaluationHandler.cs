using System.Globalization;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Packs.PersonalIntelligence.Intent;

public sealed class PaperEvaluationHandler : IBusinessCapabilityHandler
{
    public string CapabilityId => PlatformCapabilityCatalog.TradingPaperEvaluate;

    public Task<CapabilityExecution> ExecuteAsync(
        BusinessCaller caller,
        IReadOnlyDictionary<string, string> slots,
        CancellationToken cancellationToken)
    {
        if (!TryMode(slots, out var mode))
            return Task.FromResult(Stop("invalid_slot", new { status = "invalid_slot", slot = "mode" }));
        if (!TryDecimal(slots, "quantity", out var quantity) || quantity <= 0)
            return Task.FromResult(Stop("invalid_slot", new { status = "invalid_slot", slot = "quantity" }));
        if (!TryDecimal(slots, "price", out var price) || price <= 0)
            return Task.FromResult(Stop("invalid_slot", new { status = "invalid_slot", slot = "price" }));

        var side = slots.TryGetValue("side", out var rawSide) ? rawSide.Trim().ToUpperInvariant() : "";
        if (side is not ("BUY" or "SELL"))
            return Task.FromResult(Stop("invalid_slot", new { status = "invalid_slot", slot = "side" }));

        var symbol = slots.TryGetValue("symbol", out var rawSymbol) ? rawSymbol.Trim() : "";
        if (symbol.Length == 0)
            return Task.FromResult(Stop("invalid_slot", new { status = "invalid_slot", slot = "symbol" }));

        string limitsSource;
        RiskLimits limits;
        decimal leverage;
        decimal confidence;
        if (mode == ExecutionMode.Live)
        {
            limitsSource = "live_probe_not_a_policy";
            limits = new RiskLimits(decimal.MaxValue, decimal.MaxValue, 0);
            leverage = 1;
            confidence = 0;
        }
        else if (!TryRequestLimits(slots, out limits, out leverage, out confidence))
        {
            return Task.FromResult(Stop("risk_policy_absent", new
            {
                status = "risk_policy_absent",
                missing = new[] { "maxNotional", "maxLeverage", "minConfidence", "leverage", "confidence" },
                approvalAccepted = false,
                approvalReason = DemoPaperExecution.SeparateApprovalRequired,
                wroteData = false,
                externalCall = false,
            }));
        }
        else
        {
            limitsSource = "request";
        }

        var proposal = new TradeProposal(symbol, side, quantity, price, leverage, confidence, mode);
        var verdict = RiskEngine.Evaluate(proposal, limits);
        var paper = DemoPaperExecution.Execute(proposal, verdict, approval: null);
        var status = mode == ExecutionMode.Live
            ? "live_refused"
            : verdict.Allowed ? "approval_required" : "risk_veto";
        var json = BusinessIntentJson.Serialize(new
        {
            status,
            riskLimitsSource = limitsSource,
            riskAllowed = verdict.Allowed,
            riskReasons = verdict.Reasons,
            paperStatus = paper.Status,
            paperReasons = paper.Reasons,
            fillRecorded = paper.Fill is not null,
            approvalAccepted = false,
            wroteData = false,
            externalCall = false,
            persisted = false,
        });
        var completed = paper.Fill is null && !paper.Executed;
        return Task.FromResult(new CapabilityExecution(status, completed, json, false, false));
    }

    static CapabilityExecution Stop(string status, object body) =>
        new(status, false, BusinessIntentJson.Serialize(body), false, false);

    static bool TryMode(IReadOnlyDictionary<string, string> slots, out ExecutionMode mode)
    {
        mode = ExecutionMode.Paper;
        if (!slots.TryGetValue("mode", out var text))
            return false;
        if (text.Equals("paper", StringComparison.OrdinalIgnoreCase))
        {
            mode = ExecutionMode.Paper;
            return true;
        }

        if (text.Equals("live", StringComparison.OrdinalIgnoreCase))
        {
            mode = ExecutionMode.Live;
            return true;
        }

        return false;
    }

    static bool TryRequestLimits(
        IReadOnlyDictionary<string, string> slots,
        out RiskLimits limits,
        out decimal leverage,
        out decimal confidence)
    {
        limits = new RiskLimits(0, 0, 0);
        leverage = 0;
        confidence = 0;
        if (!TryDecimal(slots, "maxNotional", out var maxNotional)
            || !TryDecimal(slots, "maxLeverage", out var maxLeverage)
            || !TryDecimal(slots, "minConfidence", out var minConfidence)
            || !TryDecimal(slots, "leverage", out leverage)
            || !TryDecimal(slots, "confidence", out confidence))
            return false;
        limits = new RiskLimits(maxNotional, maxLeverage, minConfidence);
        return true;
    }

    static bool TryDecimal(IReadOnlyDictionary<string, string> slots, string name, out decimal value)
    {
        value = 0;
        return slots.TryGetValue(name, out var text)
            && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
