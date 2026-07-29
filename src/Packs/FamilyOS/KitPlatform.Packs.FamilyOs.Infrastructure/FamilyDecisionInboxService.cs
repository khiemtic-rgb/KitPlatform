using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyDecisionInboxService : IFamilyDecisionInboxService
{
    private readonly FamilyGraphRepository _families;
    private readonly IFamilyDayFlowService _dayFlows;
    private readonly IFamilyConsequenceService _consequences;
    private readonly IFamilyTeamUnlockService _team;
    private readonly IFamilyRewardService _rewards;
    private readonly IFamilyChildRequestService _requests;
    private readonly IFamilyAiProposalService _proposals;

    public FamilyDecisionInboxService(
        FamilyGraphRepository families,
        IFamilyDayFlowService dayFlows,
        IFamilyConsequenceService consequences,
        IFamilyTeamUnlockService team,
        IFamilyRewardService rewards,
        IFamilyChildRequestService requests,
        IFamilyAiProposalService proposals)
    {
        _families = families;
        _dayFlows = dayFlows;
        _consequences = consequences;
        _team = team;
        _rewards = rewards;
        _requests = requests;
        _proposals = proposals;
    }

    public async Task<FamilyDecisionInboxDto> GetInboxAsync(
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var items = new List<FamilyDecisionItemDto>();

        try
        {
            var flow = await _dayFlows.GetDayFlowAsync(familyId, today, cancellationToken);
            if (flow is not null)
            {
                foreach (var c in flow.Commitments.Where(c =>
                             string.Equals(c.Status, "done", StringComparison.OrdinalIgnoreCase)
                             && !c.StarPosted
                             && c.StarReward > 0))
                {
                    var name = ShortName(c.MemberName ?? "Con");
                    items.Add(new FamilyDecisionItemDto(
                        FamilyDecisionKinds.AwaitingStars,
                        c.Id.ToString("D"),
                        $"{name} · duyệt sao",
                        $"「{c.Title}」 đã xong — chạm để cộng sao.",
                        "approve",
                        c.MemberId,
                        c.MemberName,
                        c.CompletedAt ?? DateTimeOffset.UtcNow,
                        "commitment",
                        c.Id));
                }
            }
        }
        catch
        {
            // optional
        }

        try
        {
            var events = await _consequences.ListAsync(
                familyId, today, FamilyConsequenceEventStatuses.PendingConfirm, cancellationToken);
            foreach (var e in events)
            {
                items.Add(new FamilyDecisionItemDto(
                    FamilyDecisionKinds.ConsequenceConfirm,
                    e.Id.ToString("D"),
                    "Xác nhận thỏa thuận màn hình / hậu quả",
                    string.IsNullOrWhiteSpace(e.LabelVi) ? e.ConsequenceCode : e.LabelVi,
                    "approve",
                    e.MemberId,
                    e.MemberName,
                    e.CreatedAt,
                    "consequence",
                    e.Id));
            }
        }
        catch
        {
            // optional
        }

        try
        {
            var unlocks = await _team.ListAsync(familyId, today, cancellationToken);
            foreach (var u in unlocks.Where(u =>
                         string.Equals(u.Status, FamilyTeamUnlockStatuses.PendingConfirm, StringComparison.OrdinalIgnoreCase)))
            {
                items.Add(new FamilyDecisionItemDto(
                    FamilyDecisionKinds.TeamUnlock,
                    u.Id.ToString("D"),
                    "Movie Night / Team Unlock",
                    u.LabelVi,
                    "approve",
                    null,
                    null,
                    u.CreatedAt,
                    "team_unlock",
                    u.Id));
            }
        }
        catch
        {
            // optional
        }

        try
        {
            var redemptions = await _rewards.ListRedemptionsAsync(familyId, null, cancellationToken);
            foreach (var r in redemptions.Where(r =>
                         string.Equals(r.Status, "pending", StringComparison.OrdinalIgnoreCase)))
            {
                items.Add(new FamilyDecisionItemDto(
                    FamilyDecisionKinds.RewardFulfill,
                    r.Id.ToString("D"),
                    $"Đổi quà · {r.Title}",
                    $"{r.Icon} {r.StarCost} sao — con đang chờ nhận.",
                    "approve",
                    null,
                    null,
                    r.CreatedAt,
                    "redemption",
                    r.Id));
            }
        }
        catch
        {
            // optional
        }

        try
        {
            var reqs = await _requests.ListAsync(
                familyId, FamilyChildRequestStatuses.Pending, null, 20, cancellationToken);
            foreach (var r in reqs)
            {
                var isMission = string.Equals(
                    r.Kind, FamilyChildRequestKinds.DayMission, StringComparison.OrdinalIgnoreCase);
                var title = isMission
                    ? $"{ShortName(r.MemberName)} đề xuất: {r.TitleVi ?? "việc hôm nay"}"
                    : $"{ShortName(r.MemberName)} xin +{r.AmountMinutes ?? 0} phút";
                items.Add(new FamilyDecisionItemDto(
                    FamilyDecisionKinds.ChildRequest,
                    r.Id.ToString("D"),
                    title,
                    r.AiSummaryVi ?? "Con gửi đề xuất — AI đã tổng hợp lý do.",
                    r.AiRecommend,
                    r.MemberId,
                    r.MemberName,
                    r.CreatedAt,
                    "child_request",
                    r.Id));
            }
        }
        catch
        {
            // optional
        }

        try
        {
            var props = await _proposals.ListPendingAsync(familyId, cancellationToken);
            foreach (var p in props)
            {
                items.Add(new FamilyDecisionItemDto(
                    FamilyDecisionKinds.AiProposal,
                    p.Id.ToString("D"),
                    p.TitleVi,
                    p.BodyVi,
                    "approve",
                    p.MemberId,
                    p.MemberName,
                    p.CreatedAt,
                    "ai_proposal",
                    p.Id));
            }
        }
        catch
        {
            // optional
        }

        items = items
            .OrderByDescending(i => i.Kind == FamilyDecisionKinds.ChildRequest
                                    || i.Kind == FamilyDecisionKinds.AiProposal)
            .ThenByDescending(i => i.CreatedAt)
            .GroupBy(i =>
                i.Kind == FamilyDecisionKinds.AiProposal
                    ? $"{i.Kind}:{NormalizeTitle(i.TitleVi)}"
                    : $"{i.Kind}:{i.Id}")
            .Select(g => g.First())
            .Take(30)
            .ToList();

        var n = items.Count;
        var headline = n == 0
            ? "Không việc cần duyệt — nghỉ ngơi đi."
            : n == 1
                ? "AI cần bạn · 1 việc · khoảng 3 giây"
                : $"AI cần bạn · {n} việc · khoảng 15 giây";

        return new FamilyDecisionInboxDto(n, headline, items);
    }

    private static string NormalizeTitle(string? title) =>
        (title ?? string.Empty).Trim().ToLowerInvariant();

    private static string ShortName(string name)
    {
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? name : parts[^1];
    }
}
