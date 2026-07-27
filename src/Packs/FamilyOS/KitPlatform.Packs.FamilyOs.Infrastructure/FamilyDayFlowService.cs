using KitPlatform.Application.Abstractions;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyDayFlowService : IFamilyDayFlowService
{
    private readonly FamilyDayFlowRepository _repo;
    private readonly FamilyRoutineRepository _routines;
    private readonly FamilyGraphRepository _families;
    private readonly IFamilyConsequenceService _consequences;
    private readonly IFamilyTeamUnlockService _teamUnlocks;
    private readonly IFamilyStarService _stars;
    private readonly FamilyStarSettingsRepository _starSettings;
    private readonly IFamilyCommercialService _commercial;
    private readonly IFamilyMemoryService _memories;
    private readonly IFamilyScreenWalletService _wallet;
    private readonly IFamilyBehaviorService _behavior;
    private readonly FamilyValueRepository _value;
    private readonly FamilyBehaviorRepository _behaviorRepo;
    private readonly ITenantContext _tenant;

    public FamilyDayFlowService(
        FamilyDayFlowRepository repo,
        FamilyRoutineRepository routines,
        FamilyGraphRepository families,
        IFamilyConsequenceService consequences,
        IFamilyTeamUnlockService teamUnlocks,
        IFamilyStarService stars,
        FamilyStarSettingsRepository starSettings,
        IFamilyCommercialService commercial,
        IFamilyMemoryService memories,
        IFamilyScreenWalletService wallet,
        IFamilyBehaviorService behavior,
        FamilyValueRepository value,
        FamilyBehaviorRepository behaviorRepo,
        ITenantContext tenant)
    {
        _repo = repo;
        _routines = routines;
        _families = families;
        _consequences = consequences;
        _teamUnlocks = teamUnlocks;
        _stars = stars;
        _starSettings = starSettings;
        _commercial = commercial;
        _memories = memories;
        _wallet = wallet;
        _behavior = behavior;
        _value = value;
        _behaviorRepo = behaviorRepo;
        _tenant = tenant;
    }

    public async Task<DayFlowDto> EnsureDayFlowAsync(
        Guid familyId,
        EnsureDayFlowRequest request,
        CancellationToken cancellationToken = default)
    {
        await _commercial.EnsureEntitledAsync(familyId, cancellationToken);

        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var localNow = FamilyTimeZones.NowIn(family.Timezone);
        var flowDate = request.FlowDate ?? DateOnly.FromDateTime(localNow.DateTime);

        var routine = await _repo.PickRoutineForDateAsync(
            familyId, flowDate, request.RoutineId, cancellationToken)
            ?? throw new InvalidOperationException(
                "Chưa có routine active — tạo routine trước khi mở Daily Flow.");

        var existing = await _repo.GetByDateAsync(familyId, flowDate, cancellationToken);
        if (existing is not null)
        {
            if (!request.ForceRebuild && existing.RoutineId == routine.Id)
                return await MapDayFlowAsync(existing, family.Timezone, cancellationToken);

            // ForceRebuild or calendar/period now maps a different routine for this date.
            var rebuilt = await _repo.RebuildDayFlowCommitmentsAsync(
                familyId, existing.Id, routine.Id, routine.DisplayName, cancellationToken);
            return await MapDayFlowAsync(rebuilt, family.Timezone, cancellationToken);
        }

        var (created, _) = await _repo.CreateDayFlowWithCommitmentsAsync(
            familyId, routine.Id, routine.DisplayName, flowDate, cancellationToken);

        return await MapDayFlowAsync(created, family.Timezone, cancellationToken);
    }

    public async Task<DayFlowDto?> GetDayFlowAsync(
        Guid familyId,
        DateOnly flowDate,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var flow = await _repo.GetByDateAsync(familyId, flowDate, cancellationToken);
        if (flow is null) return null;
        return await MapDayFlowAsync(flow, family.Timezone, cancellationToken);
    }

    public async Task<CommitmentDto> UpdateCommitmentProgressAsync(
        Guid familyId,
        Guid commitmentId,
        UpdateCommitmentProgressRequest request,
        CancellationToken cancellationToken = default)
    {
        await _commercial.EnsureEntitledAsync(familyId, cancellationToken);

        var status = (request.Status ?? "").Trim().ToLowerInvariant();
        if (!FamilyCommitmentStatuses.All.Contains(status))
            throw new InvalidOperationException(
                "status phải là pending | in_progress | done | skipped.");

        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var existing = await _repo.GetCommitmentForFamilyAsync(familyId, commitmentId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy commitment.");

        string? skipReason = null;
        if (status == FamilyCommitmentStatuses.Skipped)
        {
            var reason = (request.SkipReason ?? "").Trim().ToLowerInvariant();
            if (!FamilySkipReasons.All.Contains(reason))
            {
                throw new InvalidOperationException(
                    "skipReason bắt buộc khi skipped: forgot | busy | need_help | not_ready | sick | other.");
            }

            skipReason = reason;
        }

        var localNow = FamilyTimeZones.NowIn(family.Timezone);
        var localTime = TimeOnly.FromTimeSpan(localNow.TimeOfDay);

        string? evidenceUrl = null;
        if (status == FamilyCommitmentStatuses.Done)
        {
            evidenceUrl = NormalizeEvidenceUrl(request.EvidenceUrl, _tenant.TenantId);

            if (!request.ParentOverride &&
                FamilyCommitmentTiming.IsTooEarlyToComplete(
                    existing.AllowEarlyComplete,
                    existing.EarlyLeadMinutes,
                    existing.WindowStart,
                    localTime))
            {
                throw new InvalidOperationException(
                    FamilyCommitmentTiming.EarlyCompleteMessageVi(
                        existing.Title,
                        existing.WindowStart,
                        existing.AllowEarlyComplete,
                        existing.EarlyLeadMinutes));
            }
        }

        var updated = await _repo.UpdateCommitmentStatusAsync(
            existing.Id, status, skipReason, evidenceUrl, cancellationToken)
            ?? throw new InvalidOperationException("Không cập nhật được commitment.");

        var reloaded = await _repo.GetCommitmentForFamilyAsync(familyId, updated.Id, cancellationToken)
            ?? updated;

        var flowDate = reloaded.FlowDate
            ?? DateOnly.FromDateTime(localNow.DateTime);

        if (status == FamilyCommitmentStatuses.InProgress
            && existing.Status == FamilyCommitmentStatuses.Pending)
        {
            try
            {
                await _behavior.RecordSelfStartAsync(familyId, reloaded.Id, cancellationToken);
            }
            catch
            {
                // Self-start signal is best-effort
            }
        }

        if (status == FamilyCommitmentStatuses.Skipped && skipReason is not null)
        {
            await _consequences.SuggestFromSkipAsync(
                familyId,
                new SkipConsequenceSuggestRequest(
                    reloaded.DayFlowId,
                    reloaded.Id,
                    reloaded.TemplateId,
                    reloaded.MemberId,
                    reloaded.Title,
                    flowDate,
                    skipReason),
                cancellationToken);
        }

        if (status is FamilyCommitmentStatuses.Done or FamilyCommitmentStatuses.Skipped)
        {
            try
            {
                await _behavior.SyncHabitAfterProgressAsync(
                    familyId, reloaded.Id, status, flowDate, cancellationToken);
            }
            catch
            {
                // Habit lifecycle is best-effort — never block progress updates
            }

            try
            {
                await _teamUnlocks.EnsurePendingAsync(familyId, flowDate, cancellationToken);
            }
            catch
            {
                // Unlock ensure is best-effort — never block progress updates
            }
        }

        if (status == FamilyCommitmentStatuses.Done && !string.IsNullOrWhiteSpace(evidenceUrl))
        {
            try
            {
                await _memories.TryCaptureAsync(
                    _tenant.TenantId,
                    familyId,
                    flowDate,
                    FamilyMemoryKinds.Photo,
                    reloaded.Title,
                    noteVi: null,
                    icon: "📸",
                    photoUrl: evidenceUrl,
                    sourceRef: reloaded.Id.ToString("D"),
                    memberId: reloaded.MemberId,
                    cancellationToken: cancellationToken);
            }
            catch
            {
                // Memory capture is best-effort — never block progress updates
            }
        }

        if (status == FamilyCommitmentStatuses.Done && reloaded.MemberId is Guid earnMemberId)
        {
            var earn = EarnMinutesForTitle(reloaded.Title);
            if (earn > 0)
            {
                try
                {
                    await _wallet.ApplyEarnAsync(
                        familyId,
                        earnMemberId,
                        earn,
                        sourceRef: $"earn_commitment:{reloaded.Id:D}",
                        noteVi: $"+{earn} phút · {reloaded.Title}",
                        cancellationToken);
                }
                catch
                {
                    // Wallet earn is best-effort
                }
            }
        }

        StarAwardDto? starAward = null;
        try
        {
            starAward = await _stars.SyncCommitmentStarsAsync(
                familyId, reloaded.Id, status, cancellationToken);
        }
        catch
        {
            // Star ledger is best-effort — never block progress updates
        }

        reloaded = await _repo.GetCommitmentForFamilyAsync(familyId, reloaded.Id, cancellationToken)
            ?? reloaded;

        var memberBalance = starAward?.Balance;
        if (memberBalance is null && reloaded.MemberId is Guid mid)
        {
            try
            {
                memberBalance = await _stars.GetMemberBalanceAsync(familyId, mid, cancellationToken);
            }
            catch
            {
                // ignore balance lookup failures
            }
        }

        StarAwardResult? awardForMap = starAward is not null
            ? new StarAwardResult(starAward.Delta, starAward.Tier, starAward.LateMinutes, starAward.LabelVi)
            : null;
        if (awardForMap is null)
        {
            var ledgerAwards = await _stars.GetCommitmentAwardsAsync(
                [reloaded.Id], cancellationToken);
            ledgerAwards.TryGetValue(reloaded.Id, out awardForMap);
        }

        var tierSettings = await ResolveTierSettingsAsync(familyId, cancellationToken);
        return MapCommitment(
            reloaded,
            TimeOnly.FromTimeSpan(localNow.TimeOfDay),
            flowDate,
            family.Timezone,
            awardForMap,
            tierSettings,
            localNow,
            memberBalance);
    }

    public async Task<CommitmentDto> ApproveCommitmentStarsAsync(
        Guid familyId,
        Guid commitmentId,
        CancellationToken cancellationToken = default)
    {
        await _commercial.EnsureEntitledAsync(familyId, cancellationToken);

        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var existing = await _repo.GetCommitmentForFamilyAsync(familyId, commitmentId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy commitment.");

        StarAwardDto? starAward;
        try
        {
            starAward = await _stars.ApprovePendingStarsAsync(
                familyId, commitmentId, cancellationToken)
                ?? throw new InvalidOperationException("Không duyệt được sao cho nhiệm vụ này.");
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Không duyệt được sao — thử lại.", ex);
        }

        var reloaded = await _repo.GetCommitmentForFamilyAsync(familyId, commitmentId, cancellationToken)
            ?? existing;

        var localNow = FamilyTimeZones.NowIn(family.Timezone);
        var flowDate = reloaded.FlowDate
            ?? DateOnly.FromDateTime(localNow.DateTime);
        var tierSettings = await ResolveTierSettingsAsync(familyId, cancellationToken);
        var awardForMap = new StarAwardResult(
            starAward.Delta,
            starAward.Tier,
            starAward.LateMinutes,
            starAward.LabelVi);

        return MapCommitment(
            reloaded,
            TimeOnly.FromTimeSpan(localNow.TimeOfDay),
            flowDate,
            family.Timezone,
            awardForMap,
            tierSettings,
            localNow,
            starAward.Balance);
    }

    public async Task<CommitmentDto> AddAdHocCommitmentAsync(
        Guid familyId,
        AddAdHocCommitmentRequest request,
        CancellationToken cancellationToken = default)
    {
        await _commercial.EnsureEntitledAsync(familyId, cancellationToken);

        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var title = (request.Title ?? "").Trim();
        if (title.Length is < 2 or > 200)
            throw new InvalidOperationException("Tên việc phải từ 2–200 ký tự.");

        if (request.MemberId is Guid mid)
        {
            var members = await _families.ListMembersAsync(familyId, cancellationToken);
            if (members.All(m => m.Id != mid))
                throw new InvalidOperationException("Thành viên không thuộc gia đình.");
        }

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var flowDate = request.FlowDate ?? today;

        var flow = await EnsureDayFlowAsync(
            familyId,
            new EnsureDayFlowRequest(flowDate, null),
            cancellationToken);

        var sort = await _repo.MaxSortOrderAsync(flow.Id, cancellationToken) + 1;
        var priority = string.IsNullOrWhiteSpace(request.Priority)
            ? "normal"
            : request.Priority.Trim().ToLowerInvariant();
        var duration = request.ExpectedDurationMinutes is > 0 and <= 240
            ? request.ExpectedDurationMinutes
            : null;

        var id = await _repo.InsertAdHocCommitmentAsync(
            flow.Id,
            request.MemberId,
            title,
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            request.WindowStart,
            request.WindowEnd,
            sort,
            priority,
            duration,
            starReward: 1,
            cancellationToken);

        var row = await _repo.GetCommitmentForFamilyAsync(familyId, id, cancellationToken)
            ?? throw new InvalidOperationException("Không tạo được việc hôm nay.");

        var localNow = FamilyTimeZones.NowIn(family.Timezone);
        var tierSettings = await ResolveTierSettingsAsync(familyId, cancellationToken);
        return MapCommitment(
            row,
            TimeOnly.FromTimeSpan(localNow.TimeOfDay),
            flowDate,
            family.Timezone,
            null,
            tierSettings,
            localNow,
            null);
    }

    private async Task<DayFlowDto> MapDayFlowAsync(
        FamilyDayFlowRepository.DayFlowRow flow,
        string timezone,
        CancellationToken cancellationToken)
    {
        var localNow = FamilyTimeZones.NowIn(timezone);
        var localTime = TimeOnly.FromTimeSpan(localNow.TimeOfDay);
        var tierSettings = await ResolveTierSettingsAsync(flow.FamilyId, cancellationToken);
        var commitments = await _repo.ListCommitmentsAsync(flow.Id, cancellationToken);
        foreach (var c in commitments)
        {
            if (c.Status != FamilyCommitmentStatuses.Done || c.StarPostedAt is not null)
                continue;
            if (c.PendingStarDelta is not null
                && FamilyCommitmentReview.NeedsParentApproval(c.Title, c.EvidenceUrl))
            {
                continue;
            }

            try
            {
                await _stars.RepairMissingPendingStarsAsync(flow.FamilyId, c.Id, cancellationToken);
            }
            catch
            {
                // Star repair is best-effort — never block day-flow reads
            }
        }

        commitments = await _repo.ListCommitmentsAsync(flow.Id, cancellationToken);
        var ledgerAwards = await _stars.GetCommitmentAwardsAsync(
            commitments.Select(c => c.Id), cancellationToken);
        var nudgesUsed = 0;
        var observeOnly = false;
        var nudgeBudget = FamilyMotivationIntervention.DefaultParentNudgeBudgetPerDay;
        try
        {
            nudgesUsed = await _value.GetNudgeCountAsync(
                flow.FamilyId, flow.FlowDate, cancellationToken);
            var policy = await _behaviorRepo.GetRetirementPolicyAsync(
                flow.FamilyId, cancellationToken);
            observeOnly = policy?.ObserveOnly ?? false;
            if (policy?.ParentNudgeBudget is int b)
                nudgeBudget = b;
        }
        catch
        {
            // budget lookup is best-effort
        }

        var mapped = commitments
            .Select(c =>
            {
                ledgerAwards.TryGetValue(c.Id, out var award);
                return WithEveningPrediction(
                    MapCommitment(
                        c, localTime, flow.FlowDate, timezone, award, tierSettings, localNow,
                        memberStarBalance: null,
                        parentNudgesUsedToday: nudgesUsed,
                        familyObserveOnly: observeOnly,
                        parentNudgeBudget: nudgeBudget),
                    localTime,
                    memberSignals: null);
            })
            .OrderBy(c => FamilyCommitmentReminder.SortRank(c.ReminderState, c.Status))
            .ThenBy(c => c.SortOrder)
            .ToList();

        return new DayFlowDto(
            flow.Id,
            flow.FamilyId,
            flow.RoutineId,
            flow.RoutineName,
            flow.FlowDate,
            flow.Status,
            mapped.Count,
            mapped.Count(c => c.Status == FamilyCommitmentStatuses.Done),
            mapped.Count(c => c.Status is FamilyCommitmentStatuses.Pending
                or FamilyCommitmentStatuses.InProgress),
            mapped.Count(c => c.ReminderState == FamilyReminderStates.DueNow),
            mapped.Count(c => c.ReminderState == FamilyReminderStates.Overdue),
            mapped.Count(c => c.ReminderState == FamilyReminderStates.Upcoming),
            localTime,
            mapped);
    }

    private async Task<FamilyStarTierSettings> ResolveTierSettingsAsync(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        var row = await _starSettings.GetAsync(familyId, cancellationToken);
        return FamilyStarSettingsService.ResolveTierSettings(row);
    }

    private static CommitmentDto MapCommitment(
        FamilyDayFlowRepository.CommitmentRow c,
        TimeOnly localTime,
        DateOnly flowDate,
        string? timezone,
        StarAwardResult? ledgerAward = null,
        FamilyStarTierSettings? tierSettings = null,
        DateTimeOffset? localNow = null,
        int? memberStarBalance = null,
        int parentNudgesUsedToday = 0,
        bool familyObserveOnly = false,
        int parentNudgeBudget = FamilyMotivationIntervention.DefaultParentNudgeBudgetPerDay)
    {
        var (state, label) = FamilyCommitmentReminder.Evaluate(
            c.Status,
            c.WindowStart,
            c.WindowEnd,
            localTime,
            c.HabitStage,
            c.ReminderSuppressed);
        var late = FamilyCommitmentReminder.IsLateDone(
            c.Status, c.CompletedAt, c.WindowEnd, flowDate, timezone, c.OnTimeGraceMinutes);

        var starReward = c.StarReward > 0
            ? c.StarReward
            : FamilyStarCalculator.InferStarReward(c.Title);

        int? projectedDelta = null;
        string? projectedLabel = null;
        if (c.Status is FamilyCommitmentStatuses.Pending or FamilyCommitmentStatuses.InProgress
            && localNow is not null)
        {
            var projected = FamilyStarCalculator.Calculate(
                starReward,
                localNow,
                c.WindowEnd,
                flowDate,
                timezone,
                tierSettings,
                c.OnTimeGraceMinutes);
            projectedDelta = projected.Delta;
            projectedLabel = projected.LabelVi;
        }

        int? starDelta = null;
        string? starTier = null;
        string? starLabel = null;
        var starPosted = c.StarPostedAt is not null;

        if (c.Status == FamilyCommitmentStatuses.Done)
        {
            if (starPosted && ledgerAward is not null)
            {
                starDelta = ledgerAward.Delta;
                starTier = ledgerAward.Tier;
                starLabel = ledgerAward.LabelVi;
            }
            else if (c.PendingStarDelta is int pending)
            {
                starDelta = pending;
                starTier = c.PendingStarTier;
                starLabel = FamilyStarCalculator.FormatLabelVi(
                    pending,
                    c.PendingStarLateMinutes,
                    c.PendingStarTier ?? FamilyStarTiers.OnTime);
            }
            else if (c.CompletedAt is not null)
            {
                var computed = FamilyStarCalculator.Calculate(
                    starReward,
                    c.CompletedAt,
                    c.WindowEnd,
                    flowDate,
                    timezone,
                    tierSettings,
                    c.OnTimeGraceMinutes);
                starDelta = computed.Delta;
                starTier = computed.Tier;
                starLabel = computed.LabelVi;
            }
        }

        var isLearning = FamilyLearningMission.IsLearningTitle(c.Title);
        var intervention = FamilyMotivationIntervention.Decide(
            new FamilyMotivationIntervention.Input(
                c.Status,
                state,
                string.IsNullOrWhiteSpace(c.HabitStage) ? FamilyHabitStages.New : c.HabitStage,
                FamilyHabitStages.IsReminderSuppressed(c.HabitStage, c.ReminderSuppressed),
                c.HabitStreakDays,
                isLearning,
                c.SkipReason,
                parentNudgesUsedToday,
                parentNudgeBudget,
                FamilyObserveOnly: familyObserveOnly));

        return new CommitmentDto(
            c.Id,
            c.DayFlowId,
            c.TemplateId,
            c.MemberId,
            c.MemberName,
            c.Title,
            c.Description,
            c.WindowStart,
            c.WindowEnd,
            c.SortOrder,
            c.Status,
            c.SkipReason,
            c.CompletedAt,
            late,
            state,
            label,
            string.IsNullOrWhiteSpace(c.Priority) ? FamilyCommitmentPriorities.Normal : c.Priority,
            c.ExpectedDurationMinutes,
            c.ContextAnchor,
            (c.DependsOnTemplateIds ?? []).ToList(),
            c.EvidenceUrl,
            c.EvidenceUploadedAt,
            c.AllowEarlyComplete,
            c.EarlyLeadMinutes,
            c.OnTimeGraceMinutes,
            starReward,
            starDelta,
            starTier,
            starLabel,
            memberStarBalance,
            projectedDelta,
            projectedLabel,
            starPosted,
            c.StarComputedAt,
            string.IsNullOrWhiteSpace(c.HabitStage) ? FamilyHabitStages.New : c.HabitStage,
            FamilyHabitStages.LabelVi(
                string.IsNullOrWhiteSpace(c.HabitStage) ? FamilyHabitStages.New : c.HabitStage),
            c.HabitStreakDays,
            FamilyHabitStages.IsReminderSuppressed(c.HabitStage, c.ReminderSuppressed),
            c.Status == FamilyCommitmentStatuses.Done && !c.HasReflection,
            c.Status == FamilyCommitmentStatuses.Done && !c.HasReflection
                ? FamilyReflectionPrompts.SuggestFor(c.Id)
                : null,
            c.EvidenceLevel,
            FamilyEvidenceLevels.LabelVi(c.EvidenceLevel),
            c.Status == FamilyCommitmentStatuses.Done ? c.ConfidenceScore : null,
            c.Status == FamilyCommitmentStatuses.Done && c.ConfidenceScore is int conf
                ? FamilyEvidenceConfidence.LabelVi(conf)
                : null,
            c.Status == FamilyCommitmentStatuses.Done
                && isLearning
                && !c.HasRetrievalCheck,
            isLearning,
            string.IsNullOrWhiteSpace(intervention.MotivationCueVi)
                ? null
                : intervention.MotivationDriver,
            string.IsNullOrWhiteSpace(intervention.MotivationCueVi)
                ? null
                : intervention.MotivationCueVi,
            intervention.InterventionLevel == FamilyInterventionLevels.None
                ? null
                : intervention.InterventionLevel,
            intervention.InterventionLevel == FamilyInterventionLevels.None
                ? null
                : intervention.InterventionLabelVi,
            intervention.AllowParentPush,
            intervention.AllowChildChime,
            string.IsNullOrWhiteSpace(intervention.ParentAdviceVi)
                ? null
                : intervention.ParentAdviceVi,
            null,
            null,
            null);
    }

    /// <summary>Attach evening prediction bands after mapping (Wave 4).</summary>
    private static CommitmentDto WithEveningPrediction(
        CommitmentDto c,
        TimeOnly localTime,
        FamilyBehaviorTwin.WindowSignals? memberSignals)
    {
        if (c.Status is FamilyCommitmentStatuses.Done or FamilyCommitmentStatuses.Skipped)
            return c;

        var end = c.WindowEnd ?? c.WindowStart;
        if (end is null || end.Value.Hour < 17)
            return c;

        var pred = FamilyBehaviorTwin.PredictEveningQuit(
            c.WindowStart,
            c.WindowEnd,
            c.HabitStage,
            c.IsLearningMission,
            memberSignals,
            localTime);

        return c with
        {
            EveningRiskBand = pred.RiskBand,
            EveningRiskLabelVi = pred.RiskLabelVi,
            EveningRiskActionVi = pred.RiskBand is FamilyPredictionBands.Medium
                or FamilyPredictionBands.High
                ? pred.SuggestedActionVi
                : null,
        };
    }

    /// <summary>Screen Wallet earn rules — reading / movement titles.</summary>
    private static int EarnMinutesForTitle(string title)
    {
        var t = (title ?? "").Trim().ToLowerInvariant();
        if (t.Length == 0) return 0;
        if (t.Contains("đọc", StringComparison.Ordinal) || t.Contains("sach", StringComparison.Ordinal)
            || t.Contains("sách", StringComparison.Ordinal) || t.Contains("read", StringComparison.Ordinal))
            return 15;
        if (t.Contains("xe đạp", StringComparison.Ordinal) || t.Contains("xe dap", StringComparison.Ordinal)
            || t.Contains("chạy", StringComparison.Ordinal) || t.Contains("thể dục", StringComparison.Ordinal)
            || t.Contains("the duc", StringComparison.Ordinal) || t.Contains("đá bóng", StringComparison.Ordinal)
            || t.Contains("vận động", StringComparison.Ordinal) || t.Contains("van dong", StringComparison.Ordinal))
            return 20;
        return 0;
    }

    /// <summary>Only accept same-tenant family-os upload paths.</summary>
    internal static string? NormalizeEvidenceUrl(string? raw, Guid tenantId)
    {
        var url = (raw ?? "").Trim();
        if (url.Length == 0) return null;
        if (url.Length > 500)
            throw new InvalidOperationException("evidenceUrl quá dài.");

        var prefix = $"/uploads/family-os/{tenantId:N}/";
        if (!url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "evidenceUrl phải là đường dẫn /uploads/family-os/... từ API upload FamilyOS.");
        if (url.Contains("..", StringComparison.Ordinal) || url.Contains('\\', StringComparison.Ordinal))
            throw new InvalidOperationException("evidenceUrl không hợp lệ.");
        return url;
    }
}
