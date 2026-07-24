using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyStarService : IFamilyStarService
{
    private readonly FamilyStarLedgerRepository _ledger;
    private readonly FamilyDayFlowRepository _dayFlows;
    private readonly FamilyGraphRepository _families;
    private readonly FamilyStarSettingsRepository _starSettings;

    public FamilyStarService(
        FamilyStarLedgerRepository ledger,
        FamilyDayFlowRepository dayFlows,
        FamilyGraphRepository families,
        FamilyStarSettingsRepository starSettings)
    {
        _ledger = ledger;
        _dayFlows = dayFlows;
        _families = families;
        _starSettings = starSettings;
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

        var award = await ComputeAwardAsync(familyId, commitment, cancellationToken);
        await _dayFlows.SetPendingStarsAsync(
            commitmentId,
            award.Delta,
            award.Tier,
            award.LateMinutes,
            cancellationToken);

        if (!FamilyCommitmentReview.NeedsParentApproval(commitment.Title, commitment.EvidenceUrl))
            return await PostPendingStarsAsync(familyId, commitmentId, cancellationToken);

        var balance = await _ledger.GetBalanceAsync(familyId, memberId, cancellationToken);
        return new StarAwardDto(
            award.Delta,
            balance,
            award.Tier,
            award.LateMinutes,
            award.LabelVi);
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

        if (commitment.StarPostedAt is not null)
        {
            var existing = await _ledger.ListForCommitmentsAsync([commitmentId], cancellationToken);
            if (existing.TryGetValue(commitmentId, out var row) && commitment.MemberId is Guid mid)
            {
                var balance = await _ledger.GetBalanceAsync(familyId, mid, cancellationToken);
                return new StarAwardDto(
                    row.Delta,
                    balance,
                    row.Tier,
                    row.LateMinutes,
                    FamilyStarCalculator.FormatLabelVi(row.Delta, row.LateMinutes, row.Tier));
            }
        }

        if (commitment.PendingStarDelta is null)
        {
            var award = await ComputeAwardAsync(familyId, commitment, cancellationToken);
            await _dayFlows.SetPendingStarsAsync(
                commitmentId,
                award.Delta,
                award.Tier,
                award.LateMinutes,
                cancellationToken);
        }

        return await PostPendingStarsAsync(familyId, commitmentId, cancellationToken);
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
                await PostPendingStarsAsync(familyId, commitmentId, cancellationToken);
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

    private async Task<StarAwardResult> ComputeAwardAsync(
        Guid familyId,
        FamilyDayFlowRepository.CommitmentRow commitment,
        CancellationToken cancellationToken)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken);
        var timezone = family?.Timezone;
        var flowDate = commitment.FlowDate
            ?? DateOnly.FromDateTime(FamilyTimeZones.NowIn(timezone).DateTime);

        var starReward = commitment.StarReward > 0
            ? commitment.StarReward
            : FamilyStarCalculator.InferStarReward(commitment.Title);

        var settingsRow = await _starSettings.GetAsync(familyId, cancellationToken);
        var tierSettings = FamilyStarSettingsService.ResolveTierSettings(settingsRow);

        return FamilyStarCalculator.Calculate(
            starReward,
            commitment.CompletedAt,
            commitment.WindowEnd,
            flowDate,
            timezone,
            tierSettings,
            commitment.OnTimeGraceMinutes);
    }

    private async Task<StarAwardDto?> PostPendingStarsAsync(
        Guid familyId,
        Guid commitmentId,
        CancellationToken cancellationToken)
    {
        var commitment = await _dayFlows.GetCommitmentForFamilyAsync(
            familyId, commitmentId, cancellationToken);
        if (commitment is null || commitment.MemberId is not Guid memberId)
            return null;

        if (commitment.PendingStarDelta is not int pendingDelta)
        {
            var award = await ComputeAwardAsync(familyId, commitment, cancellationToken);
            pendingDelta = award.Delta;
            await _dayFlows.SetPendingStarsAsync(
                commitmentId,
                award.Delta,
                award.Tier,
                award.LateMinutes,
                cancellationToken);
            commitment = await _dayFlows.GetCommitmentForFamilyAsync(
                familyId, commitmentId, cancellationToken)
                ?? commitment;
        }

        var starReward = commitment.StarReward > 0
            ? commitment.StarReward
            : FamilyStarCalculator.InferStarReward(commitment.Title);

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
            cancellationToken);

        await _dayFlows.MarkStarsPostedAsync(commitmentId, cancellationToken);
        return posted;
    }
}
