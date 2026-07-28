using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyStarService : IFamilyStarService
{
    private readonly FamilyStarLedgerRepository _ledger;
    private readonly FamilyDayFlowRepository _dayFlows;
    private readonly FamilyGraphRepository _families;
    private readonly FamilyStarSettingsRepository _starSettings;
    private readonly FamilyCurrencySettingsRepository _currencySettings;
    private readonly IFamilyBadgeService _badges;

    public FamilyStarService(
        FamilyStarLedgerRepository ledger,
        FamilyDayFlowRepository dayFlows,
        FamilyGraphRepository families,
        FamilyStarSettingsRepository starSettings,
        FamilyCurrencySettingsRepository currencySettings,
        IFamilyBadgeService badges)
    {
        _ledger = ledger;
        _dayFlows = dayFlows;
        _families = families;
        _starSettings = starSettings;
        _currencySettings = currencySettings;
        _badges = badges;
    }

    public async Task<StarAwardDto?> SyncCommitmentStarsAsync(
        Guid familyId,
        Guid commitmentId,
        string newStatus,
        CancellationToken cancellationToken = default)
    {
        if (newStatus != FamilyCommitmentStatuses.Done)
        {
            await RevokeCommitmentStarsAsync(commitmentId, cancellationToken);
            return null;
        }

        var commitment = await _dayFlows.GetCommitmentForFamilyAsync(
            familyId, commitmentId, cancellationToken);
        if (commitment is null || commitment.MemberId is not Guid memberId)
            return null;

        var (award, kind, message) = await ComputeAwardAsync(familyId, commitment, cancellationToken);
        await _dayFlows.SetPendingStarsAsync(
            commitmentId,
            award.Delta,
            award.Tier,
            award.LateMinutes,
            cancellationToken);

        if (!FamilyCommitmentReview.NeedsParentApproval(commitment.Title, commitment.EvidenceUrl))
            return await PostPendingStarsAsync(familyId, commitmentId, kind, message, cancellationToken);

        var balances = await _ledger.GetBalancesByKindAsync(familyId, memberId, cancellationToken);
        return new StarAwardDto(
            award.Delta,
            balances.Total,
            award.Tier,
            award.LateMinutes,
            award.LabelVi,
            kind,
            balances.Growth,
            balances.Responsibility,
            balances.Kindness,
            message);
    }

    public async Task<StarAwardDto?> ApprovePendingStarsAsync(
        Guid familyId,
        Guid commitmentId,
        CancellationToken cancellationToken = default)
    {
        var commitment = await _dayFlows.GetCommitmentForFamilyAsync(
            familyId, commitmentId, cancellationToken);
        if (commitment is null)
            return null;

        if (commitment.Status != FamilyCommitmentStatuses.Done)
            throw new InvalidOperationException("Chỉ duyệt sao khi nhiệm vụ đã hoàn thành.");

        string? kind = FamilyCurrencyStarKinds.Normalize(commitment.StarKind);
        string? message = null;

        if (commitment.StarPostedAt is not null)
        {
            var existing = await _ledger.ListForCommitmentsAsync([commitmentId], cancellationToken);
            if (existing.TryGetValue(commitmentId, out var row) && commitment.MemberId is Guid mid)
            {
                var balances = await _ledger.GetBalancesByKindAsync(familyId, mid, cancellationToken);
                return new StarAwardDto(
                    row.Delta,
                    balances.Total,
                    row.Tier,
                    row.LateMinutes,
                    FamilyStarCalculator.FormatLabelVi(row.Delta, row.LateMinutes, row.Tier),
                    row.StarKind,
                    balances.Growth,
                    balances.Responsibility,
                    balances.Kindness,
                    null);
            }
        }

        if (commitment.PendingStarDelta is null)
        {
            var computed = await ComputeAwardAsync(familyId, commitment, cancellationToken);
            kind = computed.Kind;
            message = computed.MessageVi;
            await _dayFlows.SetPendingStarsAsync(
                commitmentId,
                computed.Award.Delta,
                computed.Award.Tier,
                computed.Award.LateMinutes,
                cancellationToken);
        }

        return await PostPendingStarsAsync(familyId, commitmentId, kind, message, cancellationToken);
    }

    public async Task RevokeCommitmentStarsAsync(
        Guid commitmentId,
        CancellationToken cancellationToken = default)
    {
        await _ledger.RemoveForCommitmentAsync(commitmentId, cancellationToken);
        await _dayFlows.ClearPendingStarsAsync(commitmentId, cancellationToken);
    }

    public Task<int> GetMemberBalanceAsync(
        Guid familyId,
        Guid memberId,
        CancellationToken cancellationToken) =>
        _ledger.GetBalanceAsync(familyId, memberId, cancellationToken);

    public async Task<MemberStarBalanceDto> GetMemberBalancesAsync(
        Guid familyId,
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        var b = await _ledger.GetBalancesByKindAsync(familyId, memberId, cancellationToken);
        return new MemberStarBalanceDto(memberId, b.Total, b.Growth, b.Responsibility, b.Kindness);
    }

    public async Task RepairMissingPendingStarsAsync(
        Guid familyId,
        Guid commitmentId,
        CancellationToken cancellationToken = default)
    {
        var commitment = await _dayFlows.GetCommitmentForFamilyAsync(
            familyId, commitmentId, cancellationToken);
        if (commitment is null || commitment.Status != FamilyCommitmentStatuses.Done)
            return;
        if (commitment.StarPostedAt is not null)
            return;

        if (commitment.PendingStarDelta is not null)
        {
            if (!FamilyCommitmentReview.NeedsParentApproval(commitment.Title, commitment.EvidenceUrl))
            {
                await PostPendingStarsAsync(
                    familyId,
                    commitmentId,
                    FamilyCurrencyStarKinds.Normalize(commitment.StarKind),
                    null,
                    cancellationToken);
            }
            return;
        }

        await SyncCommitmentStarsAsync(
            familyId, commitmentId, FamilyCommitmentStatuses.Done, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, StarAwardResult>> GetCommitmentAwardsAsync(
        IEnumerable<Guid> commitmentIds,
        CancellationToken cancellationToken)
    {
        var rows = await _ledger.ListForCommitmentsAsync(commitmentIds, cancellationToken);
        return rows.ToDictionary(
            kv => kv.Key,
            kv => new StarAwardResult(
                kv.Value.Delta,
                kv.Value.Tier,
                kv.Value.LateMinutes,
                FamilyStarCalculator.FormatLabelVi(
                    kv.Value.Delta,
                    kv.Value.LateMinutes,
                    kv.Value.Tier)));
    }

    private async Task<(StarAwardResult Award, string Kind, string? MessageVi)> ComputeAwardAsync(
        Guid familyId,
        FamilyDayFlowRepository.CommitmentRow commitment,
        CancellationToken cancellationToken)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken);
        var timezone = family?.Timezone;
        var flowDate = commitment.FlowDate
            ?? DateOnly.FromDateTime(FamilyTimeZones.NowIn(timezone).DateTime);

        var settingsRow = await _starSettings.GetAsync(familyId, cancellationToken);
        var tierSettings = FamilyStarSettingsService.ResolveTierSettings(settingsRow);

        var currencyRow = await _currencySettings.GetAsync(familyId, cancellationToken);
        var currencyEnabled = currencyRow?.Enabled ?? true;

        if (!currencyEnabled || commitment.MemberId is not Guid memberId)
        {
            var legacyReward = commitment.StarReward > 0
                ? commitment.StarReward
                : FamilyStarCalculator.InferStarReward(commitment.Title);
            var legacy = FamilyStarCalculator.Calculate(
                legacyReward,
                commitment.CompletedAt,
                commitment.WindowEnd,
                flowDate,
                timezone,
                tierSettings,
                commitment.OnTimeGraceMinutes);
            return (legacy, FamilyCurrencyStarKinds.Growth, null);
        }

        var config = FamilyCurrencyPreset.Resolve(currencyRow?.PresetId);
        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(timezone).DateTime);
        string ageBand;
        if (!string.IsNullOrWhiteSpace(currencyRow?.AgeBand)
            && currencyRow!.AgeBand != FamilyCurrencyAgeBands.Custom)
        {
            ageBand = currencyRow.AgeBand!;
        }
        else
        {
            var dob = await _currencySettings.GetMemberDobAsync(memberId, cancellationToken);
            ageBand = FamilyCurrencyAgeBands.FromDateOfBirth(dob, today);
        }

        var budget = FamilyCurrencyPreset.ResolveBudget(
            config, ageBand, currencyRow?.DailyBudgetOverride);

        var siblings = await _dayFlows.ListCommitmentsAsync(commitment.DayFlowId, cancellationToken);
        var memberTasks = siblings
            .Where(c => c.MemberId == memberId)
            .ToList();

        var selfStarted = await _currencySettings.HasSelfStartEventAsync(
            commitment.Id, cancellationToken);

        var inputs = memberTasks.Select(c => new FamilyCurrencyAllocator.TaskInput(
            c.Id,
            c.Title,
            c.HabitStage,
            c.CurrencyCategory,
            c.StarKind,
            c.EligibleForStars,
            c.PlanTarget,
            c.Id == commitment.Id ? c.ActualProgress : c.ActualProgress,
            c.Id == commitment.Id && selfStarted,
            c.HabitStreakDays,
            RelativeWeight: Math.Max(1, (c.ExpectedDurationMinutes ?? 30) / 15))).ToList();

        var allocation = FamilyCurrencyAllocator.ForTask(config, budget, inputs, commitment.Id);
        var kind = allocation?.StarKind ?? FamilyCurrencyStarKinds.Normalize(commitment.StarKind);
        var message = allocation?.MessageVi;
        var baseStars = allocation?.TotalBeforeLate ?? 0;

        // Cap against remaining daily budget (exclude this commitment's prior post)
        var postedToday = await _currencySettings.SumPostedStarsTodayAsync(
            familyId, memberId, flowDate, cancellationToken);
        var softCap = budget
            + (int)Math.Floor(budget * config.StretchOverflowMaxPctOfBudget / 100.0);
        var remaining = Math.Max(0, softCap - postedToday);
        if (baseStars > remaining)
            baseStars = remaining;

        if (baseStars <= 0)
        {
            var zeroLabel = message
                ?? (allocation?.Graduated == true
                    ? "Tốt nghiệp sao — 0"
                    : "0 sao (ngân sách / trách nhiệm)");
            return (
                new StarAwardResult(0, FamilyStarTiers.OnTime, null, zeroLabel),
                kind,
                message);
        }

        var award = FamilyStarCalculator.Calculate(
            baseStars,
            commitment.CompletedAt,
            commitment.WindowEnd,
            flowDate,
            timezone,
            tierSettings,
            commitment.OnTimeGraceMinutes);

        // Late calc uses baseStars as star_reward; if late reduces below 0, keep as-is.
        if (!string.IsNullOrWhiteSpace(message) && award.Delta == 0)
        {
            return (award with { LabelVi = message }, kind, message);
        }

        return (award, kind, message);
    }

    private async Task<StarAwardDto?> PostPendingStarsAsync(
        Guid familyId,
        Guid commitmentId,
        string? starKind,
        string? currencyMessage,
        CancellationToken cancellationToken)
    {
        var commitment = await _dayFlows.GetCommitmentForFamilyAsync(
            familyId, commitmentId, cancellationToken);
        if (commitment is null || commitment.MemberId is not Guid memberId)
            return null;

        var kind = FamilyCurrencyStarKinds.Normalize(starKind ?? commitment.StarKind);
        string? message = currencyMessage;

        if (commitment.PendingStarDelta is not int pendingDelta)
        {
            var computed = await ComputeAwardAsync(familyId, commitment, cancellationToken);
            pendingDelta = computed.Award.Delta;
            kind = computed.Kind;
            message = computed.MessageVi;
            await _dayFlows.SetPendingStarsAsync(
                commitmentId,
                computed.Award.Delta,
                computed.Award.Tier,
                computed.Award.LateMinutes,
                cancellationToken);
            commitment = await _dayFlows.GetCommitmentForFamilyAsync(
                familyId, commitmentId, cancellationToken)
                ?? commitment;
        }

        var starReward = commitment.AllocatedBaseStars
            ?? (commitment.StarReward > 0
                ? commitment.StarReward
                : Math.Abs(commitment.PendingStarDelta ?? pendingDelta));

        var awardResult = new StarAwardResult(
            commitment.PendingStarDelta ?? pendingDelta,
            commitment.PendingStarTier ?? FamilyStarTiers.OnTime,
            commitment.PendingStarLateMinutes,
            FamilyStarCalculator.FormatLabelVi(
                commitment.PendingStarDelta ?? pendingDelta,
                commitment.PendingStarLateMinutes,
                commitment.PendingStarTier ?? FamilyStarTiers.OnTime));

        var posted = await _ledger.ApplyDoneAsync(
            familyId,
            commitmentId,
            memberId,
            awardResult,
            starReward,
            kind,
            cancellationToken);

        await _dayFlows.MarkStarsPostedAsync(commitmentId, cancellationToken);

        try
        {
            await _badges.EvaluateAfterCommitmentDoneAsync(
                familyId,
                memberId,
                commitmentId,
                commitment.Title,
                commitment.CurrencyCategory,
                commitment.HabitStreakDays,
                cancellationToken);
        }
        catch
        {
            // Badge evaluation must not block star posting.
        }

        if (posted is null)
            return null;

        return posted with { CurrencyMessageVi = message };
    }
}
