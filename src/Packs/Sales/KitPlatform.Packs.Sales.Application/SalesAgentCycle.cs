namespace KitPlatform.Packs.Sales;

public enum SalesActionClass
{
    Automatic,
    NeedsApproval,
    Forbidden,
}

public sealed record SalesLeadObservation(
    Guid LeadId,
    string ProductCode,
    string ProductStatus,
    string? ProfileStatus,
    string LeadStatus,
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
    bool PainResponseWithin14Days,
    string? LastInbound,
    bool Overdue,
    bool HasPendingApproval,
    string? Fingerprint);

public sealed record SalesAgentAction(string Code, SalesActionClass Class, string Reason);

public sealed record SalesAgentPlan(
    string Decision,
    string Fingerprint,
    bool Changed,
    IReadOnlyList<SalesAgentAction> Actions,
    QualificationScore? Score = null);

public static class SalesAgentCycle
{
    public static SalesAgentPlan Plan(SalesLeadObservation observation)
    {
        if (!ActiveCatalog(observation))
            return Once(observation, "blocked_catalog",
            [
                new("record_blocked", SalesActionClass.Automatic, "Sản phẩm hoặc profile không được chào."),
                new("create_draft", SalesActionClass.Forbidden, "Không tạo nháp chào giá."),
                new("send_outbound", SalesActionClass.Forbidden, "Không gửi khi catalog bị khóa."),
            ]);

        if (observation.LeadStatus is "lost" or "paid")
        {
            var actions = new List<SalesAgentAction>
            {
                new("hold", SalesActionClass.Automatic, "Lead đã kết thúc. Không tạo nháp và không gửi."),
                new("send_outbound", SalesActionClass.Forbidden, "Không gửi cho lead đã kết thúc."),
                new("revenue_recognized", SalesActionClass.Forbidden, "paid không phải doanh thu đã đối soát."),
            };
            return Once(observation, observation.LeadStatus, actions);
        }

        var score = NovixaQualificationScore.Evaluate(ToFacts(observation));
        var actionsForOpen = new List<SalesAgentAction>
        {
            new("record_score", SalesActionClass.Automatic, score.Decision),
        };
        if (score.Missing.Count > 0)
            actionsForOpen.Add(new("collect_missing", SalesActionClass.Automatic, string.Join(",", score.Missing)));
        if (score.Decision is "pursue_phc" or "nurture")
            actionsForOpen.Add(new("create_draft", SalesActionClass.Automatic, "Chỉ tạo bản nháp."));
        if (score.Decision is "pursue_phc" or "nurture" or "disqualify")
            actionsForOpen.Add(new("accept_score", SalesActionClass.NeedsApproval, "Điểm chưa ghi vào lead cho đến khi người nhận."));
        if (observation.Overdue)
            actionsForOpen.Add(new("flag_overdue", SalesActionClass.Automatic, "Có việc quá hạn hoặc lead không tiến triển."));
        actionsForOpen.Add(new("send_outbound", SalesActionClass.NeedsApproval, "Facebook chỉ gửi sau duyệt."));
        actionsForOpen.Add(new("link_phc", SalesActionClass.Forbidden, PhcLinkLock.Reason));
        actionsForOpen.Add(new("revenue_recognized", SalesActionClass.Forbidden, "Không ghi doanh thu đã đối soát."));
        return Once(observation, score.Decision, actionsForOpen, score);
    }

    public static IReadOnlyList<SalesAgentAction> Executable(SalesAgentPlan plan) =>
        plan.Actions.Where(action =>
            action.Class == SalesActionClass.Automatic || action.Code == "accept_score").ToList();

    private static SalesAgentPlan Once(
        SalesLeadObservation observation,
        string decision,
        IReadOnlyList<SalesAgentAction> actions,
        QualificationScore? score = null)
    {
        var fingerprint = Fingerprint(observation, decision, actions);
        if (observation.HasPendingApproval && observation.Fingerprint == fingerprint)
            return new SalesAgentPlan(decision, fingerprint, false, []);
        return new SalesAgentPlan(decision, fingerprint, true, actions, score);
    }

    private static bool ActiveCatalog(SalesLeadObservation observation) =>
        string.Equals(observation.ProductCode, "novixa", StringComparison.OrdinalIgnoreCase)
        && string.Equals(observation.ProductStatus, "active", StringComparison.OrdinalIgnoreCase)
        && string.Equals(observation.ProfileStatus, "active", StringComparison.OrdinalIgnoreCase);

    private static QualificationFacts ToFacts(SalesLeadObservation observation) =>
        new(
            observation.BusinessType,
            observation.HasProvince,
            observation.Reachable,
            observation.DecisionMaker,
            observation.NotDuplicate,
            observation.Pains,
            observation.HasInteraction,
            observation.OutboundOnly,
            observation.HasInbound,
            observation.InboundAfterOutbound,
            observation.PainResponseWithin14Days);

    private static string Fingerprint(
        SalesLeadObservation observation,
        string decision,
        IReadOnlyList<SalesAgentAction> actions)
    {
        var missing = string.Join(",", actions.Where(a => a.Code == "collect_missing").Select(a => a.Reason));
        var inbound = observation.LastInbound?.Trim() ?? "";
        var raw = string.Join("|",
            observation.LeadId.ToString("N"),
            observation.ProductCode,
            observation.LeadStatus,
            decision,
            missing,
            observation.Overdue ? "overdue" : "ok",
            inbound);
        return FacebookOutboundRules.Hash(raw);
    }
}
