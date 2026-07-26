using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyScoreService : IFamilyScoreService
{
    private readonly FamilyGraphRepository _families;
    private readonly IFamilyAccountabilityGlanceService _glance;
    private readonly IFamilyDayFlowService _dayFlows;
    private readonly IFamilyChallengeService _challenges;

    public FamilyScoreService(
        FamilyGraphRepository families,
        IFamilyAccountabilityGlanceService glance,
        IFamilyDayFlowService dayFlows,
        IFamilyChallengeService challenges)
    {
        _families = families;
        _glance = glance;
        _dayFlows = dayFlows;
        _challenges = challenges;
    }

    public async Task<FamilyScoreDto> GetWeekScoreAsync(
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        var glance = await _glance.GetGlanceAsync(familyId, cancellationToken: cancellationToken);
        var beautiful = glance.Days.Count(d => d.IsBeautifulDay);
        var streak = glance.CurrentStreak;

        var routinePct = 0;
        try
        {
            var flow = await _dayFlows.GetDayFlowAsync(familyId, glance.Today, cancellationToken);
            if (flow is { TotalCommitments: > 0 })
                routinePct = (int)Math.Round(100.0 * flow.DoneCount / flow.TotalCommitments);
        }
        catch
        {
            // optional
        }

        var challengeActive = false;
        try
        {
            var ch = await _challenges.GetCurrentAsync(familyId, cancellationToken);
            challengeActive = ch is not null;
        }
        catch
        {
            // optional
        }

        // Weighted light score 0–100
        var score =
            Math.Min(40, beautiful * 8) +
            Math.Min(30, streak * 5) +
            (int)(routinePct * 0.25) +
            (challengeActive ? 5 : 0);
        score = Math.Clamp(score, 0, 100);

        var band = score >= 80 ? "high" : score >= 55 ? "mid" : "low";
        var allowBonus = score >= 55;
        var headline = band switch
        {
            "high" => $"Family Score {score} — nhà đang giữ nhịp đẹp.",
            "mid" => $"Family Score {score} — ổn định, đủ điều kiện thưởng phút nhẹ.",
            _ => $"Family Score {score} — ưu tiên hoàn thành Routine trước khi thưởng phút.",
        };

        return new FamilyScoreDto(
            score,
            band,
            headline,
            allowBonus,
            beautiful,
            streak,
            routinePct,
            challengeActive);
    }
}
