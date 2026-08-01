using KitPlatform.Application.Abstractions;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyTeamUnlockService : IFamilyTeamUnlockService
{
    private readonly FamilyTeamUnlockRepository _repo;
    private readonly FamilyGraphRepository _families;
    private readonly FamilyDayFlowRepository _dayFlows;
    private readonly IFamilyMemoryService _memories;
    private readonly ITenantContext _tenant;

    public FamilyTeamUnlockService(
        FamilyTeamUnlockRepository repo,
        FamilyGraphRepository families,
        FamilyDayFlowRepository dayFlows,
        IFamilyMemoryService memories,
        ITenantContext tenant)
    {
        _repo = repo;
        _families = families;
        _dayFlows = dayFlows;
        _memories = memories;
        _tenant = tenant;
    }

    public async Task<FamilyTeamDayDto> GetTeamDayAsync(
        Guid familyId,
        DateOnly? flowDate = null,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var date = flowDate ?? today;
        var flow = await _dayFlows.GetByDateAsync(familyId, date, cancellationToken);
        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var children = members.Where(m =>
                m.RoleCode.Equals("child", StringComparison.OrdinalIgnoreCase))
            .ToList();

        IReadOnlyList<FamilyDayFlowRepository.CommitmentRow> commitments =
            flow is null
                ? Array.Empty<FamilyDayFlowRepository.CommitmentRow>()
                : await _dayFlows.ListCommitmentsAsync(flow.Id, cancellationToken);

        var childIds = children.Select(c => c.Id).ToHashSet();
        var slices = children.Select(ch =>
        {
            var mine = commitments.Where(c => c.MemberId == ch.Id).ToList();
            var done = mine.Count(c => c.Status == FamilyCommitmentStatuses.Done);
            var skipped = mine.Count(c => c.Status == FamilyCommitmentStatuses.Skipped);
            var open = mine.Count(c =>
                c.Status is not FamilyCommitmentStatuses.Done
                    and not FamilyCommitmentStatuses.Skipped);
            return new FamilyTeamChildSliceDto(
                ch.Id, ch.DisplayName, done, mine.Count, open, skipped);
        }).ToList();

        // Unassigned commitments still count toward house total if any (rare)
        var orphan = commitments
            .Where(c => c.MemberId is null || !childIds.Contains(c.MemberId.Value))
            .ToList();
        if (orphan.Count > 0 && slices.Count == 0)
        {
            var done = orphan.Count(c => c.Status == FamilyCommitmentStatuses.Done);
            var skipped = orphan.Count(c => c.Status == FamilyCommitmentStatuses.Skipped);
            var open = orphan.Count - done - skipped;
            slices.Add(new FamilyTeamChildSliceDto(
                Guid.Empty, "Nhà", done, orphan.Count, open, skipped));
        }

        var active = slices.Where(s => s.Total > 0).ToList();
        var teamDone = active.Sum(s => s.Done);
        var teamTotal = active.Sum(s => s.Total);
        var remaining = active.Sum(s => Math.Max(0, s.Open));
        var percent = teamTotal > 0 ? (int)Math.Round(100.0 * teamDone / teamTotal) : 0;
        var complete = teamTotal > 0 && remaining == 0;

        string line;
        if (teamTotal == 0)
            line = "Hôm nay nhà chưa có Mission — mình nghỉ vui cũng được!";
        else if (complete)
            line = "🎉 Mission Complete! Cả đội đã xong ngày hôm nay.";
        else if (remaining == 1)
            line = "🎯 Cả đội còn 1 Mission nữa để hoàn thành ngày hôm nay.";
        else
            line = $"🎯 Cả đội còn {remaining} Mission nữa để hoàn thành ngày hôm nay.";

        return new FamilyTeamDayDto(
            date,
            flow?.Id,
            teamDone,
            teamTotal,
            percent,
            remaining,
            complete,
            line,
            slices);
    }

    public async Task<IReadOnlyList<FamilyTeamUnlockDto>> ListAsync(
        Guid familyId,
        DateOnly? flowDate = null,
        CancellationToken cancellationToken = default)
    {
        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        var rows = await _repo.ListAsync(familyId, flowDate, cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<FamilyTeamUnlockDto?> EnsurePendingAsync(
        Guid familyId,
        DateOnly? flowDate = null,
        CancellationToken cancellationToken = default)
    {
        var team = await GetTeamDayAsync(familyId, flowDate, cancellationToken);
        if (!team.TeamComplete || team.DayFlowId is null)
            return null;

        var pick = await _repo.PickFamilyRewardAsync(familyId, cancellationToken);
        var rewardCode = string.IsNullOrWhiteSpace(pick?.RewardCode)
            ? "reward_choose_movie_sat"
            : pick!.RewardCode.Trim();
        var label = string.IsNullOrWhiteSpace(pick?.LabelVi)
            ? "Movie Night"
            : pick!.LabelVi.Trim();

        // Brand Movie Night when movie-ish codes
        if (rewardCode.Contains("movie", StringComparison.OrdinalIgnoreCase)
            || label.Contains("phim", StringComparison.OrdinalIgnoreCase))
        {
            label = "Movie Night";
        }

        await _repo.UpsertPendingAsync(
            familyId,
            team.DayFlowId.Value,
            team.FlowDate,
            rewardCode,
            label,
            pick?.AgreementId,
            team.TeamDone,
            team.TeamTotal,
            team.TeamPercent,
            cancellationToken);

        var list = await _repo.ListAsync(familyId, team.FlowDate, cancellationToken);
        var row = list.FirstOrDefault(r =>
            r.RewardCode.Equals(rewardCode, StringComparison.OrdinalIgnoreCase));
        return row is null ? null : Map(row);
    }

    public async Task<FamilyTeamUnlockDto?> EnsureSiblingComboPendingAsync(
        Guid familyId,
        DateOnly? flowDate = null,
        CancellationToken cancellationToken = default)
    {
        var team = await GetTeamDayAsync(familyId, flowDate, cancellationToken);
        if (team.DayFlowId is null)
            return null;

        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");
        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var date = flowDate ?? today;
        var flow = await _dayFlows.GetByDateAsync(familyId, date, cancellationToken);
        if (flow is null)
            return null;

        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var childIds = members
            .Where(m => m.RoleCode.Equals("child", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Id)
            .ToHashSet();
        if (childIds.Count < 2)
            return null;

        var commitments = await _dayFlows.ListCommitmentsAsync(flow.Id, cancellationToken);
        var pair = FindSiblingPairDone(commitments, childIds);
        if (pair is null)
            return null;

        var rewardCode = FamilySiblingComboUnlock.RewardCode;
        var label = string.IsNullOrWhiteSpace(pair.Value.Title)
            ? FamilySiblingComboUnlock.DefaultLabelVi
            : $"High-five: {pair.Value.Title}";
        if (label.Length > 120)
            label = label[..117] + "…";

        await _repo.UpsertPendingAsync(
            familyId,
            team.DayFlowId.Value,
            team.FlowDate,
            rewardCode,
            label,
            agreementId: null,
            teamDone: pair.Value.MemberCount,
            teamTotal: pair.Value.MemberCount,
            teamPercent: 100,
            cancellationToken);

        var list = await _repo.ListAsync(familyId, team.FlowDate, cancellationToken);
        var row = list.FirstOrDefault(r =>
            r.RewardCode.Equals(rewardCode, StringComparison.OrdinalIgnoreCase));
        return row is null ? null : Map(row);
    }

    /// <summary>
    /// Same normalized mission title, done by ≥2 distinct children today = "commitment cặp".
    /// </summary>
    private static (string Title, int MemberCount)? FindSiblingPairDone(
        IReadOnlyList<FamilyDayFlowRepository.CommitmentRow> commitments,
        HashSet<Guid> childIds)
    {
        var done = commitments
            .Where(c =>
                c.Status == FamilyCommitmentStatuses.Done
                && c.MemberId is Guid mid
                && childIds.Contains(mid)
                && !string.IsNullOrWhiteSpace(c.Title))
            .ToList();
        if (done.Count < 2)
            return null;

        var best = done
            .GroupBy(c => NormalizeMissionTitle(c.Title))
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .Select(g =>
            {
                var members = g.Select(c => c.MemberId!.Value).Distinct().ToList();
                var title = g.Select(c => (c.Title ?? "").Trim())
                    .OrderByDescending(t => t.Length)
                    .FirstOrDefault() ?? "";
                return (Title: title, MemberCount: members.Count, Key: g.Key);
            })
            .Where(x => x.MemberCount >= 2)
            .OrderByDescending(x => x.MemberCount)
            .ThenByDescending(x => x.Title.Length)
            .FirstOrDefault();

        if (best.MemberCount < 2)
            return null;
        return (best.Title, best.MemberCount);
    }

    private static string NormalizeMissionTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "";
        var t = title.Trim().ToLowerInvariant();
        while (t.Contains("  ", StringComparison.Ordinal))
            t = t.Replace("  ", " ", StringComparison.Ordinal);
        return t.TrimEnd('.', '!', '?', '…');
    }

    public async Task<FamilyTeamUnlockDto> DecideAsync(
        Guid familyId,
        Guid unlockId,
        FamilyTeamUnlockDecideRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        var status = (request.Status ?? "").Trim().ToLowerInvariant();
        if (!FamilyTeamUnlockStatuses.Decide.Contains(status))
            throw new InvalidOperationException("status phải là confirmed | deferred.");

        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var actor = members.FirstOrDefault(m => m.Id == request.ConfirmedBy)
            ?? throw new InvalidOperationException("confirmedBy không thuộc gia đình này.");
        if (actor.RoleCode.Equals("child", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chỉ phụ huynh mới xác nhận Team Unlock.");

        var existing = await _repo.GetAsync(familyId, unlockId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy Team Unlock.");
        if (existing.Status != FamilyTeamUnlockStatuses.PendingConfirm)
            throw new InvalidOperationException("Team Unlock đã được quyết định.");

        var note = string.IsNullOrWhiteSpace(request.DecisionNote)
            ? null
            : request.DecisionNote.Trim();

        var updated = await _repo.DecideAsync(
            familyId, unlockId, status, request.ConfirmedBy, note, cancellationToken)
            ?? throw new InvalidOperationException("Không cập nhật được Team Unlock.");

        if (status == FamilyTeamUnlockStatuses.Confirmed)
        {
            try
            {
                var label = string.IsNullOrWhiteSpace(updated.LabelVi) ? "Movie Night" : updated.LabelVi;
                await _memories.TryCaptureAsync(
                    _tenant.TenantId,
                    familyId,
                    updated.FlowDate,
                    FamilyMemoryKinds.TeamUnlock,
                    $"{label} cả nhà",
                    noteVi: "Cả nhà cùng làm được — phần thưởng chung đã mở.",
                    icon: "🍿",
                    sourceRef: updated.Id.ToString("D"),
                    cancellationToken: cancellationToken);
            }
            catch
            {
                // Memory capture is best-effort — never block the unlock decision.
            }
        }

        return Map(updated);
    }

    private static FamilyTeamUnlockDto Map(FamilyTeamUnlockRepository.UnlockRow row) =>
        new(
            row.Id,
            row.FamilyId,
            row.DayFlowId,
            row.FlowDate,
            row.RewardCode,
            row.LabelVi,
            row.AgreementId,
            row.TeamDone,
            row.TeamTotal,
            row.TeamPercent,
            row.Status,
            row.ConfirmedBy,
            row.ConfirmedAt,
            row.DecisionNote,
            row.CreatedAt);
}
