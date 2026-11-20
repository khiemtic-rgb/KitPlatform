namespace KitPlatform.Packs.Sales;

public sealed record QualificationPainSignal(
    string ResponseCode,
    decimal Confidence,
    DateTimeOffset? RespondedAt);

public sealed record QualificationFacts(
    string? BusinessType,
    bool HasProvince,
    bool Reachable,
    bool DecisionMaker,
    bool NotDuplicate,
    IReadOnlyList<QualificationPainSignal> Pains,
    bool HasInteraction,
    bool OutboundOnly,
    bool HasInbound,
    bool InboundAfterOutbound,
    bool PainResponseWithin14Days);

public sealed record QualificationFactor(
    string Code,
    decimal Points,
    decimal Max,
    string Evidence,
    string Source);

public sealed record QualificationScore(
    bool Scored,
    decimal? Fit,
    decimal? Pain,
    decimal? Intent,
    decimal? Engagement,
    decimal? Total,
    string Formula,
    string Decision,
    string? Temperature,
    IReadOnlyList<QualificationFactor> Factors,
    IReadOnlyList<string> Missing);

/// <summary>
/// Deterministic Novixa qualification. Disposition (lost / paid) is not part of this score.
/// </summary>
public static class NovixaQualificationScore
{
    public const string Formula = "0.35*fit + 0.25*pain + 0.25*intent + 0.15*engagement";

    private static readonly string[] IntentCodes =
    [
        "no_response",
        "seen_no_reply",
        "replied_not_interested",
        "objection",
        "interested",
        "phc_clicked",
        "phc_started",
        "phc_completed",
    ];

    private static readonly string[] PainCodes =
    [
        "objection",
        "interested",
        "phc_clicked",
        "phc_started",
        "phc_completed",
    ];

    public static QualificationScore Evaluate(QualificationFacts facts)
    {
        var factors = new List<QualificationFactor>();
        var missing = new List<string>();
        decimal? fit = ScoreFit(facts, factors, missing);
        decimal? pain = ScorePain(facts, factors, missing);
        decimal? intent = ScoreIntent(facts, missing);
        var engagement = ScoreEngagement(facts, factors);

        if (IsOutOfIcp(facts))
            return Finish(false, 0, pain, intent, engagement, null, "disqualify", "cold", factors, missing);

        if (LatestIntent(facts) == "replied_not_interested")
            return Finish(fit is not null && pain is not null && intent is not null, fit, pain, 0, engagement, null, "disqualify", "cold", factors, missing);

        if (fit is null || pain is null || intent is null)
            return Finish(false, fit, pain, intent, engagement, null, "insufficient", null, factors, missing);

        if (fit < 40)
            return Finish(true, fit, pain, intent, engagement, Total(fit.Value, pain.Value, intent.Value, engagement), "disqualify", "cold", factors, missing);

        var total = Total(fit.Value, pain.Value, intent.Value, engagement);
        if (total >= 40 && intent >= 40)
        {
            var temperature = total >= 70 ? "hot" : "warm";
            return Finish(true, fit, pain, intent, engagement, total, "pursue_phc", temperature, factors, missing);
        }

        return Finish(true, fit, pain, intent, engagement, total, "nurture", "cold", factors, missing);
    }

    public static decimal Total(decimal fit, decimal pain, decimal intent, decimal engagement) =>
        Math.Round(0.35m * fit + 0.25m * pain + 0.25m * intent + 0.15m * engagement, 2);

    private static bool IsOutOfIcp(QualificationFacts facts)
    {
        var type = (facts.BusinessType ?? "").Trim();
        return type.Length > 0 && !type.Equals("pharmacy", StringComparison.OrdinalIgnoreCase);
    }

    private static decimal? ScoreFit(QualificationFacts facts, List<QualificationFactor> factors, List<string> missing)
    {
        var type = (facts.BusinessType ?? "").Trim();
        if (type.Length == 0)
        {
            missing.Add("fit.business_type");
            return null;
        }

        if (!type.Equals("pharmacy", StringComparison.OrdinalIgnoreCase))
        {
            factors.Add(new("fit.business_type", 0, 40, type, "pack_sales.business.business_type"));
            return 0;
        }

        decimal score = 40;
        factors.Add(new("fit.business_type", 40, 40, "pharmacy", "pack_sales.business.business_type"));
        if (facts.HasProvince)
        {
            score += 15;
            factors.Add(new("fit.province", 15, 15, "present", "pack_sales.business.province"));
        }

        if (facts.Reachable)
        {
            score += 25;
            factors.Add(new("fit.reachable", 25, 25, "contact_channel", "pack_sales.business"));
        }

        if (facts.NotDuplicate)
        {
            score += 10;
            factors.Add(new("fit.not_duplicate", 10, 10, "lead_kept", "pack_sales.lead"));
        }

        if (facts.DecisionMaker)
        {
            score += 10;
            factors.Add(new("fit.decision_maker", 10, 10, "yes", "pack_sales.contact"));
        }

        return score;
    }

    private static decimal? ScorePain(QualificationFacts facts, List<QualificationFactor> factors, List<string> missing)
    {
        if (facts.Pains.Count == 0)
        {
            missing.Add("pain.response");
            return null;
        }

        var useful = facts.Pains.Where(p => PainCodes.Contains(p.ResponseCode)).ToList();
        if (useful.Count == 0)
        {
            var silence = facts.Pains.Any(p => p.ResponseCode is "no_response" or "seen_no_reply");
            if (!silence)
            {
                missing.Add("pain.response");
                return null;
            }

            factors.Add(new("pain.confidence", 0, 100, "no_confirmed_response", "pack_sales.lead_pain"));
            return 0;
        }

        var best = useful.Max(p => p.Confidence);
        var rounded = Math.Round(Math.Clamp(best, 0, 100), 2);
        factors.Add(new("pain.confidence", rounded, 100, rounded.ToString("0.##"), "pack_sales.lead_pain.confidence"));
        return rounded;
    }

    private static decimal? ScoreIntent(QualificationFacts facts, List<string> missing)
    {
        var code = LatestIntent(facts);
        if (code is null)
        {
            missing.Add("intent.response");
            return null;
        }

        return code switch
        {
            "no_response" => 0,
            "seen_no_reply" => 10,
            "replied_not_interested" => 0,
            "objection" => 40,
            "interested" => 70,
            "phc_clicked" => 80,
            "phc_started" => 90,
            "phc_completed" => 100,
            _ => 0,
        };
    }

    private static string? LatestIntent(QualificationFacts facts) =>
        facts.Pains
            .Where(p => IntentCodes.Contains(p.ResponseCode))
            .OrderByDescending(p => p.RespondedAt ?? DateTimeOffset.MinValue)
            .Select(p => p.ResponseCode)
            .FirstOrDefault();

    private static decimal ScoreEngagement(QualificationFacts facts, List<QualificationFactor> factors)
    {
        decimal score = 0;
        var evidence = "none";
        if (facts.HasInteraction)
        {
            score = 0;
        }

        if (facts.OutboundOnly)
        {
            score = 30;
            evidence = "outbound_only";
        }

        if (facts.HasInbound)
        {
            score = 60;
            evidence = "inbound";
        }

        if (facts.InboundAfterOutbound)
        {
            score = 80;
            evidence = "inbound_after_outbound";
        }

        if (facts.PainResponseWithin14Days)
        {
            score = 100;
            evidence = "pain_response_14d";
        }

        factors.Add(new("engagement", score, 100, evidence, "pack_sales.interaction"));
        return score;
    }

    private static QualificationScore Finish(
        bool scored,
        decimal? fit,
        decimal? pain,
        decimal? intent,
        decimal? engagement,
        decimal? total,
        string decision,
        string? temperature,
        List<QualificationFactor> factors,
        List<string> missing) =>
        new(scored, fit, pain, intent, engagement, total, Formula, decision, temperature, factors, missing);
}
