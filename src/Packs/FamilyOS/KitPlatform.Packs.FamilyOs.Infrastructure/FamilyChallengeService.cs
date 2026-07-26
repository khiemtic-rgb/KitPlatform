using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyChallengeService : IFamilyChallengeService
{
    private readonly FamilyChallengeRepository _repo;
    private readonly FamilyGraphRepository _families;
    private readonly IFamilyDayFlowService _dayFlows;
    private readonly FamilyTeamUnlockRepository _unlocks;

    public FamilyChallengeService(
        FamilyChallengeRepository repo,
        FamilyGraphRepository families,
        IFamilyDayFlowService dayFlows,
        FamilyTeamUnlockRepository unlocks)
    {
        _repo = repo;
        _families = families;
        _dayFlows = dayFlows;
        _unlocks = unlocks;
    }

    public async Task<FamilyChallengeDto?> GetCurrentAsync(
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        var family = await RequireFamilyAsync(familyId, cancellationToken);
        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var weekStart = StartOfIsoWeek(today);
        var row = await _repo.GetByWeekAsync(familyId, weekStart, cancellationToken);
        if (row is null) return null;
        return await ComposeAsync(row, today, cancellationToken);
    }

    public async Task<FamilyChallengeDto> AcceptAsync(
        Guid familyId,
        AcceptFamilyChallengeRequest request,
        CancellationToken cancellationToken = default)
    {
        var family = await RequireFamilyAsync(familyId, cancellationToken);
        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var actor = members.FirstOrDefault(m => m.Id == request.AcceptedBy)
            ?? throw new InvalidOperationException("acceptedBy không thuộc gia đình này.");
        var role = (actor.RoleCode ?? "").ToLowerInvariant();
        if (role is not (FamilyMembershipRoles.Guardian or FamilyMembershipRoles.Caregiver))
            throw new InvalidOperationException("Chỉ bố/mẹ mới mở Challenge tuần.");

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var weekStart = StartOfIsoWeek(today);

        var existing = await _repo.GetByWeekAsync(familyId, weekStart, cancellationToken);
        if (existing is not null)
            return await ComposeAsync(existing, today, cancellationToken);

        var title = string.IsNullOrWhiteSpace(request.Title)
            ? "Challenge tuần này"
            : request.Title.Trim();
        var rewardCode = string.IsNullOrWhiteSpace(request.RewardCode)
            ? "reward_choose_movie_sat"
            : request.RewardCode.Trim();
        var rewardLabel = string.IsNullOrWhiteSpace(request.RewardLabel)
            ? "Movie Night"
            : request.RewardLabel.Trim();

        var challengeId = await _repo.InsertChallengeAsync(
            familyId, weekStart, title, rewardCode, rewardLabel,
            request.AcceptedBy, cancellationToken);

        var sort = 0;
        foreach (var parent in members.Where(m =>
                     m.RoleCode is FamilyMembershipRoles.Guardian or FamilyMembershipRoles.Caregiver
                     && m.Status == "active"))
        {
            var goalTitle = await _repo.FirstActiveParentGoalTitleAsync(
                familyId, parent.Id, cancellationToken);
            var legTitle = string.IsNullOrWhiteSpace(goalTitle)
                ? $"Mục tiêu của {FirstName(parent.DisplayName)}"
                : goalTitle!;
            await _repo.InsertLegAsync(
                challengeId, parent.Id, FamilyChallengeLegKinds.Parent,
                legTitle, "🌱", targetDays: 5, sortOrder: sort++, cancellationToken);
        }

        foreach (var child in members.Where(m =>
                     m.RoleCode == FamilyMembershipRoles.Child && m.Status == "active"))
        {
            await _repo.InsertLegAsync(
                challengeId, child.Id, FamilyChallengeLegKinds.Child,
                $"Routine của {FirstName(child.DisplayName)}", "⭐",
                targetDays: 5, sortOrder: sort++, cancellationToken);
        }

        await _repo.InsertLegAsync(
            challengeId, null, FamilyChallengeLegKinds.Household,
            "Cả nhà ăn tối cùng nhau", "🍚",
            targetDays: 4, sortOrder: sort, cancellationToken);

        var row = await _repo.GetAsync(familyId, challengeId, cancellationToken)
            ?? throw new InvalidOperationException("Không tạo được challenge.");
        return await ComposeAsync(row, today, cancellationToken);
    }

    public async Task<FamilyChallengeDto> CheckinLegAsync(
        Guid familyId,
        Guid legId,
        FamilyChallengeCheckinRequest request,
        CancellationToken cancellationToken = default)
    {
        var family = await RequireFamilyAsync(familyId, cancellationToken);
        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var weekStart = StartOfIsoWeek(today);
        var challenge = await _repo.GetByWeekAsync(familyId, weekStart, cancellationToken)
            ?? throw new InvalidOperationException("Chưa có Challenge tuần này — hãy chấp nhận trước.");

        if (challenge.Status != FamilyChallengeStatuses.Active)
            throw new InvalidOperationException("Challenge tuần này đã kết thúc.");

        var leg = await _repo.GetLegAsync(challenge.Id, legId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy chân challenge.");

        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var actor = members.FirstOrDefault(m => m.Id == request.ActorMemberId)
            ?? throw new InvalidOperationException("actorMemberId không thuộc gia đình này.");

        EnsureCanCheckin(leg, actor);

        var date = request.Date ?? today;
        if (date < weekStart || date > weekStart.AddDays(6) || date > today)
            throw new InvalidOperationException("Ngày check-in không hợp lệ.");

        var status = (request.Status ?? "").Trim().ToLowerInvariant();
        switch (status)
        {
            case ParentGoalCheckinStatuses.Clear:
                await _repo.ClearCheckinAsync(legId, date, cancellationToken);
                break;
            case ParentGoalCheckinStatuses.Done:
            case ParentGoalCheckinStatuses.Skip:
                await _repo.UpsertCheckinAsync(
                    legId, date, status, actor.Id, cancellationToken);
                break;
            default:
                throw new InvalidOperationException("status phải là done | skip | clear.");
        }

        var weekEnd = weekStart.AddDays(6);
        var checkins = await _repo.ListCheckinsAsync([legId], weekStart, weekEnd, cancellationToken);
        var doneDays = checkins.Count(c => c.Status == ParentGoalCheckinStatuses.Done);
        await _repo.SyncDoneDaysAsync(legId, doneDays, cancellationToken);

        // Re-evaluate completion.
        var allLegs = await _repo.ListLegsAsync(challenge.Id, cancellationToken);
        var allCheckins = await _repo.ListCheckinsAsync(
            allLegs.Select(l => l.Id).ToArray(), weekStart, weekEnd, cancellationToken);
        var doneByLeg = allCheckins
            .Where(c => c.Status == ParentGoalCheckinStatuses.Done)
            .GroupBy(c => c.LegId)
            .ToDictionary(g => g.Key, g => g.Count());

        var allComplete = allLegs.Count > 0 && allLegs.All(l =>
            doneByLeg.GetValueOrDefault(l.Id) >= l.TargetDays);

        if (allComplete && challenge.Status == FamilyChallengeStatuses.Active)
        {
            var unlockId = await TryOpenMovieNightAsync(
                familyId, today, challenge, allLegs.Count, cancellationToken);
            await _repo.MarkCompletedAsync(challenge.Id, unlockId, cancellationToken);
        }

        var refreshed = await _repo.GetAsync(familyId, challenge.Id, cancellationToken) ?? challenge;
        return await ComposeAsync(refreshed, today, cancellationToken);
    }

    private async Task<Guid?> TryOpenMovieNightAsync(
        Guid familyId,
        DateOnly today,
        FamilyChallengeRepository.ChallengeRow challenge,
        int legsTotal,
        CancellationToken cancellationToken)
    {
        try
        {
            var flow = await _dayFlows.EnsureDayFlowAsync(
                familyId,
                new EnsureDayFlowRequest(today, null),
                cancellationToken);
            await _unlocks.UpsertPendingAsync(
                familyId,
                flow.Id,
                today,
                challenge.RewardCode,
                challenge.RewardLabel,
                agreementId: null,
                teamDone: legsTotal,
                teamTotal: legsTotal,
                teamPercent: 100,
                cancellationToken);

            var list = await _unlocks.ListAsync(familyId, today, cancellationToken);
            return list.FirstOrDefault(u =>
                u.RewardCode.Equals(challenge.RewardCode, StringComparison.OrdinalIgnoreCase))?.Id;
        }
        catch
        {
            // Challenge still completes even if unlock hook fails (e.g. no day flow).
            return null;
        }
    }

    private async Task<FamilyChallengeDto> ComposeAsync(
        FamilyChallengeRepository.ChallengeRow row,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var weekEnd = row.WeekStart.AddDays(6);
        var legs = await _repo.ListLegsAsync(row.Id, cancellationToken);
        var checkins = await _repo.ListCheckinsAsync(
            legs.Select(l => l.Id).ToArray(), row.WeekStart, weekEnd, cancellationToken);

        var doneByLeg = checkins
            .Where(c => c.Status == ParentGoalCheckinStatuses.Done)
            .GroupBy(c => c.LegId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.CheckinDate).ToHashSet());

        var todayDone = checkins
            .Where(c => c.CheckinDate == today && c.Status == ParentGoalCheckinStatuses.Done)
            .Select(c => c.LegId)
            .ToHashSet();

        var legDtos = legs.Select(l =>
        {
            var done = doneByLeg.TryGetValue(l.Id, out var set) ? set.Count : l.DoneDays;
            return new FamilyChallengeLegDto(
                l.Id,
                l.MemberId,
                l.MemberName,
                l.LegKind,
                l.Title,
                l.Emoji,
                l.TargetDays,
                done,
                todayDone.Contains(l.Id),
                done >= l.TargetDays,
                l.SortOrder);
        }).ToList();

        var completeCount = legDtos.Count(l => l.IsComplete);
        var status = row.Status;
        if (status == FamilyChallengeStatuses.Active && today > weekEnd)
            status = FamilyChallengeStatuses.Expired;

        return new FamilyChallengeDto(
            row.Id,
            row.FamilyId,
            row.WeekStart,
            weekEnd,
            status,
            row.Title,
            row.RewardCode,
            row.RewardLabel,
            row.AcceptedBy,
            row.CompletedAt,
            row.UnlockId,
            completeCount,
            legDtos.Count,
            legDtos);
    }

    private static void EnsureCanCheckin(
        FamilyChallengeRepository.LegRow leg,
        FamilyGraphRepository.MembershipRow actor)
    {
        var role = (actor.RoleCode ?? "").ToLowerInvariant();
        if (leg.LegKind == FamilyChallengeLegKinds.Household)
        {
            if (role is not (FamilyMembershipRoles.Guardian or FamilyMembershipRoles.Caregiver))
                throw new InvalidOperationException("Chân cả nhà do bố/mẹ ghi nhận.");
            return;
        }

        if (leg.MemberId is Guid owner && owner != actor.Id)
        {
            // Parents may check in a child's leg (gentle co-play); children only their own.
            if (role == FamilyMembershipRoles.Child)
                throw new InvalidOperationException("Con chỉ check-in chân của mình.");
        }
    }

    private async Task<FamilyGraphRepository.FamilyRow> RequireFamilyAsync(
        Guid familyId,
        CancellationToken cancellationToken) =>
        await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

    private static DateOnly StartOfIsoWeek(DateOnly date)
    {
        var offset = date.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)date.DayOfWeek - 1;
        return date.AddDays(-offset);
    }

    private static string FirstName(string displayName)
    {
        var trimmed = (displayName ?? "").Trim();
        if (string.IsNullOrEmpty(trimmed)) return "mình";
        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts[^1];
    }
}
