using System.Text.Json;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyBehaviorService : IFamilyBehaviorService
{
    private readonly FamilyBehaviorRepository _repo;
    private readonly FamilyGraphRepository _families;
    private readonly FamilyDayFlowRepository _dayFlows;
    private readonly FamilyValueRepository _value;
    private readonly IFamilyCommercialService _commercial;
    private readonly IFamilyBlueprintService _blueprint;

    public FamilyBehaviorService(
        FamilyBehaviorRepository repo,
        FamilyGraphRepository families,
        FamilyDayFlowRepository dayFlows,
        FamilyValueRepository value,
        IFamilyCommercialService commercial,
        IFamilyBlueprintService blueprint)
    {
        _repo = repo;
        _families = families;
        _dayFlows = dayFlows;
        _value = value;
        _commercial = commercial;
        _blueprint = blueprint;
    }

    public async Task<HabitProgressDto?> SyncHabitAfterProgressAsync(
        Guid familyId,
        Guid commitmentId,
        string status,
        DateOnly flowDate,
        CancellationToken cancellationToken = default)
    {
        var ctx = await _repo.GetCommitmentContextAsync(familyId, commitmentId, cancellationToken);
        if (ctx is null) return null;

        var eventType = status == FamilyCommitmentStatuses.Done
            ? FamilyBehaviorEventTypes.CommitmentDone
            : status == FamilyCommitmentStatuses.Skipped
                ? FamilyBehaviorEventTypes.CommitmentSkipped
                : null;

        if (eventType is not null)
        {
            try
            {
                await _repo.InsertBehaviorEventAsync(
                    familyId,
                    ctx.MemberId,
                    eventType,
                    commitmentId,
                    ctx.TemplateId,
                    new { status, flowDate = flowDate.ToString("yyyy-MM-dd") },
                    cancellationToken);
            }
            catch
            {
                // Event bus is best-effort
            }
        }

        if (!string.IsNullOrWhiteSpace(ctx.EvidenceUrl)
            && status == FamilyCommitmentStatuses.Done)
        {
            try
            {
                await _repo.InsertBehaviorEventAsync(
                    familyId,
                    ctx.MemberId,
                    FamilyBehaviorEventTypes.EvidenceUploaded,
                    commitmentId,
                    ctx.TemplateId,
                    new { hasUrl = true },
                    cancellationToken);
            }
            catch
            {
                // best-effort
            }
        }

        FamilyEvidenceConfidence.ScoreResult? confidence = null;
        if (status == FamilyCommitmentStatuses.Done)
        {
            confidence = await RecalculateAndPersistConfidenceAsync(
                familyId, ctx, cancellationToken);
            ctx = await _repo.GetCommitmentContextAsync(familyId, commitmentId, cancellationToken)
                ?? ctx;
        }

        if (ctx.TemplateId is not Guid templateId)
        {
            return ToProgress(
                new FamilyHabitLifecycle.Snapshot(
                    FamilyHabitStages.New, 0, null, false, null),
                commitmentId,
                needsReflection: status == FamilyCommitmentStatuses.Done && !ctx.HasReflection,
                ctx,
                confidence);
        }

        var template = await _repo.GetTemplateHabitAsync(templateId, cancellationToken);
        if (template is null) return null;

        var current = new FamilyHabitLifecycle.Snapshot(
            string.IsNullOrWhiteSpace(template.HabitStage)
                ? FamilyHabitStages.New
                : template.HabitStage,
            template.HabitStreakDays,
            template.HabitLastDoneDate,
            template.ReminderSuppressed,
            template.HabitStageChangedAt);

        FamilyHabitLifecycle.TransitionResult? transition = status switch
        {
            FamilyCommitmentStatuses.Done => FamilyHabitLifecycle.ApplyDone(current, flowDate),
            FamilyCommitmentStatuses.Skipped => FamilyHabitLifecycle.ApplySkip(current, flowDate),
            _ => null,
        };

        if (transition is null)
        {
            return ToProgress(current, commitmentId, needsReflection: false, ctx, confidence);
        }

        var next = transition.Next;
        await _repo.UpdateTemplateHabitAsync(
            templateId,
            next.Stage,
            next.StreakDays,
            next.LastDoneDate,
            next.ReminderSuppressed,
            transition.StageChanged ? next.StageChangedAt : null,
            cancellationToken);

        await _repo.SyncCommitmentHabitSnapshotAsync(
            commitmentId, next.Stage, next.ReminderSuppressed, cancellationToken);

        if (transition.StageChanged)
        {
            try
            {
                await _repo.InsertBehaviorEventAsync(
                    familyId,
                    ctx.MemberId,
                    FamilyBehaviorEventTypes.HabitStageChanged,
                    commitmentId,
                    templateId,
                    new
                    {
                        from = transition.PreviousStage,
                        to = next.Stage,
                        streak = next.StreakDays,
                    },
                    cancellationToken);
            }
            catch
            {
                // best-effort
            }
        }

        if (transition.BecameSuppressed)
        {
            try
            {
                await _repo.InsertBehaviorEventAsync(
                    familyId,
                    ctx.MemberId,
                    FamilyBehaviorEventTypes.ReminderSuppressed,
                    commitmentId,
                    templateId,
                    new { stage = next.Stage, streak = next.StreakDays },
                    cancellationToken);
            }
            catch
            {
                // best-effort
            }
        }

        return ToProgress(
            next,
            commitmentId,
            needsReflection: status == FamilyCommitmentStatuses.Done && !ctx.HasReflection,
            ctx,
            confidence);
    }

    public async Task RecordSelfStartAsync(
        Guid familyId,
        Guid commitmentId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await _repo.GetCommitmentContextAsync(familyId, commitmentId, cancellationToken);
        if (ctx is null) return;

        try
        {
            await _repo.InsertBehaviorEventAsync(
                familyId,
                ctx.MemberId,
                FamilyBehaviorEventTypes.SelfStart,
                commitmentId,
                ctx.TemplateId,
                new { status = ctx.Status },
                cancellationToken);
        }
        catch
        {
            // best-effort
        }
    }

    public async Task RecordParentNudgeAsync(
        Guid familyId,
        Guid? commitmentId,
        Guid? memberId,
        bool allowed,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        Guid? templateId = null;
        Guid? mid = memberId;
        if (commitmentId is Guid cid)
        {
            var ctx = await _repo.GetCommitmentContextAsync(familyId, cid, cancellationToken);
            if (ctx is not null)
            {
                templateId = ctx.TemplateId;
                mid ??= ctx.MemberId;
            }
        }

        try
        {
            await _repo.InsertBehaviorEventAsync(
                familyId,
                mid,
                allowed
                    ? FamilyBehaviorEventTypes.ParentNudge
                    : FamilyBehaviorEventTypes.ParentNudgeBlocked,
                commitmentId,
                templateId,
                new { allowed, reason },
                cancellationToken);
        }
        catch
        {
            // best-effort
        }
    }

    public async Task<BehaviorCoachDto> GetTodayCoachAsync(
        Guid familyId,
        DateOnly? flowDate = null,
        CancellationToken cancellationToken = default)
    {
        await _commercial.EnsureCapabilityAsync(familyId, FamilyCapabilityCodes.BehaviorCoach, cancellationToken);

        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var localNow = FamilyTimeZones.NowIn(family.Timezone);
        var date = flowDate ?? DateOnly.FromDateTime(localNow.DateTime);
        var localTime = TimeOnly.FromTimeSpan(localNow.TimeOfDay);

        var flow = await _dayFlows.GetByDateAsync(familyId, date, cancellationToken);
        var nudgesUsed = await _value.GetNudgeCountAsync(familyId, date, cancellationToken);
        var policy = await _repo.GetRetirementPolicyAsync(familyId, cancellationToken);
        var budget = policy?.ParentNudgeBudget
            ?? FamilyMotivationIntervention.DefaultParentNudgeBudgetPerDay;
        var observeOnly = policy?.ObserveOnly ?? false;

        if (flow is null)
        {
            return new BehaviorCoachDto(
                date, nudgesUsed, budget, 0, 0, Array.Empty<BehaviorCoachMemberHintDto>());
        }

        var rows = await _dayFlows.ListCommitmentsAsync(flow.Id, cancellationToken);
        var weekStart = FamilyBehaviorPatterns.WeekStart(date);
        var playbookByMember = new Dictionary<Guid, FamilyBehaviorRepository.WeekPlaybookRow>();
        FamilyBehaviorRepository.WeekPlaybookRow? familyPlaybook = null;
        try
        {
            familyPlaybook = await _repo.GetWeekPlaybookAsync(
                familyId, memberId: null, weekStart, cancellationToken);
        }
        catch
        {
            // table may not exist yet before mig 267
        }

        var hints = new List<BehaviorCoachMemberHintDto>();
        foreach (var c in rows)
        {
            if (c.Status is FamilyCommitmentStatuses.Done or FamilyCommitmentStatuses.Skipped)
                continue;

            var (state, _) = FamilyCommitmentReminder.Evaluate(
                c.Status, c.WindowStart, c.WindowEnd, localTime, c.HabitStage, c.ReminderSuppressed);
            if (state is FamilyReminderStates.None)
                continue;

            string? weekPattern = familyPlaybook?.PatternCode;
            string? weekTactic = familyPlaybook?.TacticCode;
            if (c.MemberId is Guid mid)
            {
                if (!playbookByMember.TryGetValue(mid, out var memberBook))
                {
                    try
                    {
                        memberBook = await _repo.GetWeekPlaybookAsync(
                            familyId, mid, weekStart, cancellationToken);
                    }
                    catch
                    {
                        memberBook = null;
                    }

                    if (memberBook is not null)
                        playbookByMember[mid] = memberBook;
                }

                weekPattern = memberBook?.PatternCode ?? weekPattern;
                weekTactic = memberBook?.TacticCode ?? weekTactic;
            }

            var inferred = FamilyBehaviorPatterns.InferCode(
                new FamilyBehaviorPatterns.InferSignals(
                    c.WindowEnd,
                    state,
                    FamilyLearningMission.IsLearningTitle(c.Title),
                    c.Title,
                    c.HabitStage,
                    c.HabitStreakDays,
                    nudgesUsed,
                    c.SkipReason));
            var useWeekTactic =
                inferred is not null
                && weekPattern is not null
                && string.Equals(inferred, weekPattern, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(weekTactic);

            var decision = FamilyMotivationIntervention.Decide(
                new FamilyMotivationIntervention.Input(
                    c.Status,
                    state,
                    c.HabitStage,
                    FamilyHabitStages.IsReminderSuppressed(c.HabitStage, c.ReminderSuppressed),
                    c.HabitStreakDays,
                    FamilyLearningMission.IsLearningTitle(c.Title),
                    c.SkipReason,
                    nudgesUsed,
                    budget,
                    FamilyObserveOnly: observeOnly,
                    WindowEnd: c.WindowEnd,
                    Title: c.Title,
                    ForcedPatternCode: useWeekTactic ? weekPattern : null,
                    ForcedTacticCode: useWeekTactic ? weekTactic : null));

            if (decision.InterventionLevel is FamilyInterventionLevels.None)
                continue;

            hints.Add(new BehaviorCoachMemberHintDto(
                c.MemberId,
                c.MemberName,
                c.Id,
                c.Title,
                decision.InterventionLevel,
                decision.InterventionLabelVi,
                decision.ParentAdviceVi,
                decision.AllowParentPush,
                string.IsNullOrWhiteSpace(decision.MotivationCueVi)
                    ? null
                    : decision.MotivationCueVi,
                decision.BehaviorPatternCode,
                decision.BehaviorTacticCode));
        }

        return new BehaviorCoachDto(
            date,
            nudgesUsed,
            budget,
            hints.Count(h => h.InterventionLevel == FamilyInterventionLevels.ObserveOnly),
            hints.Count(h => h.AllowParentPush),
            hints
                .OrderByDescending(h => h.AllowParentPush)
                .ThenBy(h => h.Title)
                .ToList());
    }

    public async Task<BehaviorTwinDto> GetTwinAsync(
        Guid familyId,
        Guid? memberId = null,
        CancellationToken cancellationToken = default)
    {
        await _commercial.EnsureCapabilityAsync(familyId, FamilyCapabilityCodes.BehaviorTwin, cancellationToken);

        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var localNow = FamilyTimeZones.NowIn(family.Timezone);
        var today = DateOnly.FromDateTime(localNow.DateTime);
        var from = today.AddDays(-6);
        var fromUtc = localNow.AddDays(-7);

        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var targets = members
            .Where(m => memberId is null
                ? IsChildRole(m.RoleCode)
                : m.Id == memberId.Value)
            .ToList();

        if (memberId is Guid mid && targets.Count == 0)
        {
            var one = members.FirstOrDefault(m => m.Id == mid);
            if (one is not null) targets.Add(one);
        }

        var results = new List<BehaviorTwinMemberDto>();
        foreach (var m in targets)
        {
            var stats = await _repo.GetMemberWindowStatsAsync(
                familyId, m.Id, from, today, cancellationToken);
            var events = await _repo.CountMemberEventsAsync(
                familyId, m.Id, fromUtc, cancellationToken);

            var signals = new FamilyBehaviorTwin.WindowSignals(
                OpenCommitments: stats.OpenCount,
                DoneCount: stats.DoneCount,
                SkippedCount: stats.SkippedCount,
                SelfStartCount: events.SelfStartCount,
                ReflectionCount: events.ReflectionCount,
                RetrievalCheckCount: events.RetrievalCount,
                ParentNudgeCount: events.ParentNudgeCount,
                OverdueDoneCount: 0,
                EveningOpenCount: stats.EveningOpenCount,
                EveningSkipCount: stats.EveningSkipCount,
                MaxHabitStreak: stats.MaxHabitStreak,
                AnyAutonomousHabit: stats.AnyAutonomous,
                AvgConfidenceWhenDone: stats.ConfidenceN > 0
                    ? stats.ConfidenceSum / stats.ConfidenceN
                    : 0);

            var twin = FamilyBehaviorTwin.Score(signals);
            var evening = FamilyBehaviorTwin.PredictEveningQuit(
                windowStart: new TimeOnly(19, 0),
                windowEnd: new TimeOnly(20, 0),
                habitStage: stats.AnyAutonomous
                    ? FamilyHabitStages.Autonomous
                    : FamilyHabitStages.Guided,
                isLearningMission: true,
                signals,
                TimeOnly.FromTimeSpan(localNow.TimeOfDay));

            // Member-level evening risk uses a representative evening learning window.
            // Per-commitment risk is attached on day-flow DTO.
            var dimsJson = System.Text.Json.JsonSerializer.Serialize(
                twin.Dimensions.Select(d => new
                {
                    d.Code,
                    d.LabelVi,
                    d.Score,
                    d.WhyVi,
                }));
            var reasonsJson = System.Text.Json.JsonSerializer.Serialize(evening.ReasonsVi);

            try
            {
                await _repo.UpsertTwinSnapshotAsync(
                    familyId,
                    m.Id,
                    today,
                    twin.OverallScore,
                    twin.OverallLabelVi,
                    dimsJson,
                    evening.RiskBand,
                    reasonsJson,
                    twin.DisclaimerVi,
                    cancellationToken);

                await _repo.InsertBehaviorEventAsync(
                    familyId,
                    m.Id,
                    FamilyBehaviorEventTypes.TwinScored,
                    commitmentId: null,
                    templateId: null,
                    new { overall = twin.OverallScore, evening = evening.RiskBand },
                    cancellationToken);

                if (evening.RiskBand is FamilyPredictionBands.Medium or FamilyPredictionBands.High)
                {
                    await _repo.InsertBehaviorEventAsync(
                        familyId,
                        m.Id,
                        FamilyBehaviorEventTypes.PredictionFlagged,
                        commitmentId: null,
                        templateId: null,
                        new { band = evening.RiskBand, scope = "evening_quit_lite" },
                        cancellationToken);
                }
            }
            catch
            {
                // snapshot / events best-effort
            }

            results.Add(new BehaviorTwinMemberDto(
                m.Id,
                m.DisplayName,
                twin.OverallScore,
                twin.OverallLabelVi,
                twin.DisclaimerVi,
                evening.RiskBand,
                evening.RiskLabelVi,
                evening.ReasonsVi,
                evening.SuggestedActionVi,
                twin.Dimensions
                    .Select(d => new BehaviorTwinDimensionDto(d.Code, d.LabelVi, d.Score, d.WhyVi))
                    .ToList(),
                today));
        }

        return new BehaviorTwinDto(today, FamilyBehaviorTwin.DisclaimerVi, results);
    }

    public async Task<FamilyBehaviorTwinDto> GetFamilyTwinAsync(
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        await _commercial.EnsureCapabilityAsync(familyId, FamilyCapabilityCodes.BehaviorTwin, cancellationToken);

        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var policyRow = await _repo.GetRetirementPolicyAsync(familyId, cancellationToken);
        var policy = MapPolicy(policyRow);

        var childTwin = await GetTwinAsync(familyId, memberId: null, cancellationToken);
        var localNow = FamilyTimeZones.NowIn(family.Timezone);
        var weekStart = localNow.AddDays(-7);
        var prevWeekStart = localNow.AddDays(-14);
        var nudgesThis = await _repo.CountFamilyParentNudgesAsync(
            familyId, weekStart, localNow, cancellationToken);
        var nudgesPrev = await _repo.CountFamilyParentNudgesAsync(
            familyId, prevWeekStart, weekStart, cancellationToken);

        var childSignals = childTwin.Members.Select(m =>
        {
            int Dim(string code) =>
                m.Dimensions.FirstOrDefault(d =>
                    string.Equals(d.Code, code, StringComparison.OrdinalIgnoreCase))?.Score ?? 40;

            return new FamilyTwinRetirement.ChildSignal(
                m.MemberId,
                m.MemberName,
                m.OverallScore,
                Dim(FamilyTwinDimensions.Autonomy),
                Dim(FamilyTwinDimensions.Peace),
                Dim(FamilyTwinDimensions.SelfStart),
                DoneCount: 0,
                SkippedCount: 0,
                ParentNudgeCount: 0);
        }).ToList();

        // Enrich done/skip from window stats
        var today = DateOnly.FromDateTime(localNow.DateTime);
        var from = today.AddDays(-6);
        for (var i = 0; i < childSignals.Count; i++)
        {
            var s = childSignals[i];
            var stats = await _repo.GetMemberWindowStatsAsync(
                familyId, s.MemberId, from, today, cancellationToken);
            var ev = await _repo.CountMemberEventsAsync(
                familyId, s.MemberId, weekStart, cancellationToken);
            childSignals[i] = s with
            {
                DoneCount = stats.DoneCount,
                SkippedCount = stats.SkippedCount,
                ParentNudgeCount = ev.ParentNudgeCount,
            };
        }

        var familyTwin = FamilyTwinRetirement.Score(
            childSignals,
            nudgesThis,
            nudgesPrev,
            policy.ObserveOnly);

        // Persist recommended stage onto policy row (non-destructive for observe flag)
        try
        {
            await _repo.UpsertRetirementPolicyAsync(
                familyId,
                policy.ObserveOnly,
                familyTwin.RetirementStage,
                policy.ParentNudgeBudget,
                policy.NotesVi,
                cancellationToken);

            await _repo.InsertBehaviorEventAsync(
                familyId,
                memberId: null,
                FamilyBehaviorEventTypes.RetirementAdvanced,
                commitmentId: null,
                templateId: null,
                new
                {
                    stage = familyTwin.RetirementStage,
                    autonomy = familyTwin.FamilyAutonomyIndex,
                    intervention = familyTwin.ParentalInterventionIndex,
                },
                cancellationToken);

            if (familyTwin.DependenceWarning)
            {
                await _repo.InsertBehaviorEventAsync(
                    familyId,
                    memberId: null,
                    FamilyBehaviorEventTypes.DependenceWarned,
                    commitmentId: null,
                    templateId: null,
                    new { message = familyTwin.DependenceWarningVi },
                    cancellationToken);
            }
        }
        catch
        {
            // best-effort
        }

        policy = MapPolicy(await _repo.GetRetirementPolicyAsync(familyId, cancellationToken));

        return new FamilyBehaviorTwinDto(
            childTwin.AsOfDate,
            familyTwin.DisclaimerVi,
            familyTwin.FamilyPeaceIndex,
            familyTwin.FamilyAutonomyIndex,
            familyTwin.ParentalInterventionIndex,
            familyTwin.RetirementStage,
            familyTwin.RetirementLabelVi,
            familyTwin.RetirementAdviceVi,
            familyTwin.SiblingBalance,
            familyTwin.SiblingBalanceLabelVi,
            familyTwin.SiblingAdviceVi,
            familyTwin.DependenceWarning,
            familyTwin.DependenceWarningVi,
            familyTwin.RecommendObserveOnly,
            policy.ObserveOnly,
            policy,
            childTwin.Members);
    }

    public async Task<BehaviorRetirementPolicyDto> GetRetirementPolicyAsync(
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        await _commercial.EnsureCapabilityAsync(familyId, FamilyCapabilityCodes.BehaviorCoach, cancellationToken);
        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        return MapPolicy(await _repo.GetRetirementPolicyAsync(familyId, cancellationToken));
    }

    public async Task<BehaviorRetirementPolicyDto> UpdateRetirementPolicyAsync(
        Guid familyId,
        UpdateBehaviorRetirementPolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        await _commercial.EnsureCapabilityAsync(familyId, FamilyCapabilityCodes.BehaviorCoach, cancellationToken);
        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        var current = await _repo.GetRetirementPolicyAsync(familyId, cancellationToken);
        var observe = request.ObserveOnly ?? current?.ObserveOnly ?? false;
        var budget = request.ParentNudgeBudget ?? current?.ParentNudgeBudget;
        if (budget is < 0 or > 20)
            throw new InvalidOperationException("parentNudgeBudget phải từ 0–20.");

        var notes = request.NotesVi ?? current?.NotesVi;
        if (notes is { Length: > 500 })
            throw new InvalidOperationException("notesVi tối đa 500 ký tự.");

        var wasObserve = current?.ObserveOnly ?? false;
        var row = await _repo.UpsertRetirementPolicyAsync(
            familyId,
            observe,
            current?.RetirementStage,
            budget,
            notes,
            cancellationToken);

        try
        {
            if (!wasObserve && observe)
            {
                await _repo.InsertBehaviorEventAsync(
                    familyId, null, FamilyBehaviorEventTypes.ObserveModeEntered,
                    null, null, new { source = "parent_toggle" }, cancellationToken);
            }
            else if (wasObserve && !observe)
            {
                await _repo.InsertBehaviorEventAsync(
                    familyId, null, FamilyBehaviorEventTypes.ObserveModeExited,
                    null, null, new { source = "parent_toggle" }, cancellationToken);
            }
        }
        catch
        {
            // best-effort
        }

        return MapPolicy(row);
    }

    private static BehaviorRetirementPolicyDto MapPolicy(
        FamilyBehaviorRepository.RetirementPolicyRow? row) =>
        row is null
            ? new BehaviorRetirementPolicyDto(
                ObserveOnly: false,
                RetirementStage: null,
                RetirementLabelVi: null,
                ParentNudgeBudget: null,
                NotesVi: null,
                UpdatedAt: DateTimeOffset.UtcNow)
            : new BehaviorRetirementPolicyDto(
                row.ObserveOnly,
                row.RetirementStage,
                row.RetirementStage is null
                    ? null
                    : FamilyRetirementStages.LabelVi(row.RetirementStage),
                row.ParentNudgeBudget,
                row.NotesVi,
                row.UpdatedAt);

    private static bool IsChildRole(string? roleCode) =>
        string.Equals(
            roleCode?.Trim(),
            FamilyMembershipRoles.Child,
            StringComparison.OrdinalIgnoreCase);

    public async Task<CommitmentReflectionDto> SubmitReflectionAsync(
        Guid familyId,
        Guid commitmentId,
        SubmitCommitmentReflectionRequest request,
        CancellationToken cancellationToken = default)
    {
        await _commercial.EnsureCapabilityAsync(familyId, FamilyCapabilityCodes.BehaviorCoach, cancellationToken);

        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        var ctx = await _repo.GetCommitmentContextAsync(familyId, commitmentId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy commitment.");

        if (ctx.Status != FamilyCommitmentStatuses.Done)
            throw new InvalidOperationException("Chỉ ghi reflection sau khi đã hoàn thành.");

        var prompt = (request.PromptCode ?? "").Trim().ToLowerInvariant();
        if (!FamilyReflectionPrompts.All.Contains(prompt))
            throw new InvalidOperationException(
                "promptCode phải là hardest | learned | improve_tomorrow.");

        var answer = (request.AnswerText ?? "").Trim();
        if (answer.Length is < 1 or > 500)
            throw new InvalidOperationException("Câu trả lời cần 1–500 ký tự.");

        var row = await _repo.InsertReflectionAsync(
            familyId, commitmentId, ctx.MemberId, prompt, answer, cancellationToken);

        try
        {
            await _repo.InsertBehaviorEventAsync(
                familyId,
                ctx.MemberId,
                FamilyBehaviorEventTypes.ReflectionSubmitted,
                commitmentId,
                ctx.TemplateId,
                new { promptCode = prompt, answerLength = answer.Length },
                cancellationToken);
        }
        catch
        {
            // best-effort
        }

        ctx = await _repo.GetCommitmentContextAsync(familyId, commitmentId, cancellationToken)
            ?? ctx;
        var confidence = await RecalculateAndPersistConfidenceAsync(
            familyId, ctx, cancellationToken);

        return MapReflection(row, confidence, ctx);
    }

    public async Task<CommitmentReflectionDto?> GetReflectionAsync(
        Guid familyId,
        Guid commitmentId,
        CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetReflectionAsync(familyId, commitmentId, cancellationToken);
        if (row is null) return null;
        var ctx = await _repo.GetCommitmentContextAsync(familyId, commitmentId, cancellationToken);
        return MapReflection(row, confidence: null, ctx);
    }

    public async Task<RetrievalCheckChallengeDto?> GetRetrievalCheckAsync(
        Guid familyId,
        Guid commitmentId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await _repo.GetCommitmentContextAsync(familyId, commitmentId, cancellationToken);
        if (ctx is null) return null;
        if (ctx.Status != FamilyCommitmentStatuses.Done) return null;
        if (!FamilyLearningMission.IsLearningTitle(ctx.Title)) return null;

        return new RetrievalCheckChallengeDto(
            commitmentId,
            ctx.Title,
            AlreadySubmitted: ctx.HasRetrievalCheck,
            BuildRetrievalQuestions());
    }

    public async Task<RetrievalCheckResultDto> SubmitRetrievalCheckAsync(
        Guid familyId,
        Guid commitmentId,
        SubmitRetrievalCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        await _commercial.EnsureCapabilityAsync(familyId, FamilyCapabilityCodes.BehaviorCoach, cancellationToken);

        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        var ctx = await _repo.GetCommitmentContextAsync(familyId, commitmentId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy commitment.");

        if (ctx.Status != FamilyCommitmentStatuses.Done)
            throw new InvalidOperationException("Chỉ kiểm tra nhớ sau khi đã hoàn thành.");

        if (!FamilyLearningMission.IsLearningTitle(ctx.Title))
            throw new InvalidOperationException("Nhiệm vụ này không cần kiểm tra nhớ.");

        var method = (request.MethodAnswer ?? "").Trim().ToLowerInvariant();
        var recall = (request.RecallAnswer ?? "").Trim().ToLowerInvariant();
        if (!FamilyRetrievalAnswers.Methods.Contains(method))
            throw new InvalidOperationException("methodAnswer phải là skim | practice | retrieve.");
        if (!FamilyRetrievalAnswers.Recalls.Contains(recall))
            throw new InvalidOperationException(
                "recallAnswer phải là can_explain | vaguely | need_review.");

        var preview = FamilyEvidenceConfidence.Score(new FamilyEvidenceConfidence.Signals(
            IsDone: true,
            HasReflection: ctx.HasReflection,
            HasRetrievalCheck: true,
            MethodAnswer: method,
            RecallAnswer: recall,
            HasPhotoEvidence: !string.IsNullOrWhiteSpace(ctx.EvidenceUrl),
            IsLearningMission: FamilyLearningMission.IsLearningTitle(ctx.Title)));

        var row = await _repo.InsertRetrievalCheckAsync(
            familyId,
            commitmentId,
            ctx.MemberId,
            method,
            recall,
            preview.IllusionRisk,
            cancellationToken);

        try
        {
            await _repo.InsertBehaviorEventAsync(
                familyId,
                ctx.MemberId,
                FamilyBehaviorEventTypes.RetrievalSubmitted,
                commitmentId,
                ctx.TemplateId,
                new
                {
                    method,
                    recall,
                    illusionRisk = preview.IllusionRisk,
                },
                cancellationToken);
        }
        catch
        {
            // best-effort
        }

        ctx = await _repo.GetCommitmentContextAsync(familyId, commitmentId, cancellationToken)
            ?? ctx;
        var confidence = await RecalculateAndPersistConfidenceAsync(
            familyId, ctx, cancellationToken);

        if (confidence.IllusionRisk)
        {
            try
            {
                await _blueprint.NoteIllusionRiskAsync(familyId, cancellationToken);
            }
            catch
            {
                // Blueprint may be missing pre-mig 249 — retrieval still succeeds.
            }
        }

        return new RetrievalCheckResultDto(
            commitmentId,
            row.MethodAnswer,
            row.RecallAnswer,
            confidence.IllusionRisk,
            confidence.ConfidenceScore,
            confidence.ConfidenceLabelVi,
            confidence.EvidenceLevel,
            FamilyEvidenceLevels.LabelVi(confidence.EvidenceLevel));
    }

    private async Task<FamilyEvidenceConfidence.ScoreResult> RecalculateAndPersistConfidenceAsync(
        Guid familyId,
        FamilyBehaviorRepository.CommitmentContextRow ctx,
        CancellationToken cancellationToken)
    {
        var result = FamilyEvidenceConfidence.Score(BuildSignals(ctx));
        await _repo.UpdateCommitmentConfidenceAsync(
            ctx.CommitmentId,
            result.EvidenceLevel,
            result.ConfidenceScore,
            cancellationToken);

        try
        {
            await _repo.InsertBehaviorEventAsync(
                familyId,
                ctx.MemberId,
                FamilyBehaviorEventTypes.ConfidenceScored,
                ctx.CommitmentId,
                ctx.TemplateId,
                new
                {
                    evidenceLevel = result.EvidenceLevel,
                    confidence = result.ConfidenceScore,
                    illusionRisk = result.IllusionRisk,
                },
                cancellationToken);
        }
        catch
        {
            // best-effort
        }

        return result;
    }

    private static FamilyEvidenceConfidence.Signals BuildSignals(
        FamilyBehaviorRepository.CommitmentContextRow ctx) =>
        new(
            IsDone: ctx.Status == FamilyCommitmentStatuses.Done,
            HasReflection: ctx.HasReflection,
            HasRetrievalCheck: ctx.HasRetrievalCheck,
            MethodAnswer: ctx.MethodAnswer,
            RecallAnswer: ctx.RecallAnswer,
            HasPhotoEvidence: !string.IsNullOrWhiteSpace(ctx.EvidenceUrl),
            IsLearningMission: FamilyLearningMission.IsLearningTitle(ctx.Title));

    private static HabitProgressDto ToProgress(
        FamilyHabitLifecycle.Snapshot snap,
        Guid commitmentId,
        bool needsReflection,
        FamilyBehaviorRepository.CommitmentContextRow ctx,
        FamilyEvidenceConfidence.ScoreResult? confidence)
    {
        var isLearning = FamilyLearningMission.IsLearningTitle(ctx.Title);
        var needsQuiz = ctx.Status == FamilyCommitmentStatuses.Done
            && isLearning
            && !ctx.HasRetrievalCheck;
        var score = confidence?.ConfidenceScore ?? ctx.ConfidenceScore ?? 0;
        var label = confidence?.ConfidenceLabelVi
            ?? (score > 0 ? FamilyEvidenceConfidence.LabelVi(score) : null);
        var level = confidence?.EvidenceLevel ?? ctx.EvidenceLevel;

        return new HabitProgressDto(
            snap.Stage,
            FamilyHabitStages.LabelVi(snap.Stage),
            snap.StreakDays,
            snap.ReminderSuppressed,
            needsReflection,
            needsReflection ? FamilyReflectionPrompts.SuggestFor(commitmentId) : null,
            score,
            label,
            level,
            needsQuiz);
    }

    private static CommitmentReflectionDto MapReflection(
        FamilyBehaviorRepository.ReflectionRow row,
        FamilyEvidenceConfidence.ScoreResult? confidence,
        FamilyBehaviorRepository.CommitmentContextRow? ctx)
    {
        var isLearning = FamilyLearningMission.IsLearningTitle(ctx?.Title);
        var needsQuiz = ctx is not null
            && ctx.Status == FamilyCommitmentStatuses.Done
            && isLearning
            && !ctx.HasRetrievalCheck;
        // After insert reflection, HasReflection may still be stale if we didn't reload —
        // submit path always reloads. For get path, quiz flag uses DB HasRetrievalCheck.
        if (confidence is not null)
        {
            // Reflection just saved — treat as has reflection for quiz gating
            needsQuiz = ctx is not null
                && ctx.Status == FamilyCommitmentStatuses.Done
                && isLearning
                && !ctx.HasRetrievalCheck;
        }

        var score = confidence?.ConfidenceScore ?? ctx?.ConfidenceScore ?? 0;
        var label = confidence?.ConfidenceLabelVi
            ?? (score > 0 ? FamilyEvidenceConfidence.LabelVi(score) : null);
        var level = confidence?.EvidenceLevel ?? ctx?.EvidenceLevel ?? 0;

        return new CommitmentReflectionDto(
            row.Id,
            row.FamilyId,
            row.CommitmentId,
            row.MemberId,
            row.PromptCode,
            FamilyReflectionPrompts.LabelVi(row.PromptCode),
            row.AnswerText,
            row.CreatedAt,
            score,
            label,
            level,
            needsQuiz);
    }

    public async Task<WeekPlaybookDto> GetWeekPlaybookAsync(
        Guid familyId,
        Guid? memberId = null,
        DateOnly? asOf = null,
        CancellationToken cancellationToken = default)
    {
        // Free+ weekly insight surface — no Twin cap required for pattern cards.
        await _commercial.EnsureCapabilityAsync(
            familyId, FamilyCapabilityCodes.WeeklyInsight, cancellationToken);

        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var localNow = FamilyTimeZones.NowIn(family.Timezone);
        var today = asOf ?? DateOnly.FromDateTime(localNow.DateTime);
        var weekStart = FamilyBehaviorPatterns.WeekStart(today);
        var weekLocal = localNow.AddDays(-(today.DayNumber - weekStart.DayNumber));
        var fromUtc = new DateTimeOffset(
            weekLocal.Year, weekLocal.Month, weekLocal.Day, 0, 0, 0, weekLocal.Offset)
            .ToUniversalTime();
        var toUtc = fromUtc.AddDays(7);

        var nudges = await _repo.CountFamilyEventsAsync(
            familyId, FamilyBehaviorEventTypes.ParentNudge, fromUtc, toUtc, cancellationToken);
        var selfStarts = await _repo.CountFamilyEventsAsync(
            familyId, FamilyBehaviorEventTypes.SelfStart, fromUtc, toUtc, cancellationToken);

        FamilyBehaviorRepository.WeekPlaybookRow? row = null;
        try
        {
            row = await _repo.GetWeekPlaybookAsync(familyId, memberId, weekStart, cancellationToken);
        }
        catch
        {
            // mig 267 may not be applied yet
        }

        // Infer primary pattern from today's open commitments if none stored.
        string? patternCode = row?.PatternCode;
        string? tacticCode = row?.TacticCode;
        if (string.IsNullOrWhiteSpace(patternCode))
        {
            var flow = await _dayFlows.GetByDateAsync(familyId, today, cancellationToken);
            if (flow is not null)
            {
                var localTime = TimeOnly.FromTimeSpan(localNow.TimeOfDay);
                var commitments = await _dayFlows.ListCommitmentsAsync(flow.Id, cancellationToken);
                foreach (var c in commitments.Where(x =>
                             memberId is null || x.MemberId == memberId))
                {
                    if (c.Status is FamilyCommitmentStatuses.Done or FamilyCommitmentStatuses.Skipped)
                        continue;
                    var (state, _) = FamilyCommitmentReminder.Evaluate(
                        c.Status, c.WindowStart, c.WindowEnd, localTime, c.HabitStage, c.ReminderSuppressed);
                    patternCode = FamilyBehaviorPatterns.InferCode(
                        new FamilyBehaviorPatterns.InferSignals(
                            c.WindowEnd,
                            state,
                            FamilyLearningMission.IsLearningTitle(c.Title),
                            c.Title,
                            c.HabitStage,
                            c.HabitStreakDays,
                            nudges > 14 ? 3 : nudges / 5,
                            c.SkipReason));
                    if (patternCode is not null) break;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(patternCode) && string.IsNullOrWhiteSpace(tacticCode))
        {
            tacticCode = FamilyBehaviorPatterns.PickTacticCode(
                patternCode!, today, memberId, row?.LastFailedTactic);
        }

        var strategyTip = row?.ParentStrategyTipVi
            ?? FamilyBehaviorPatterns.ParentStrategyTipVi(
                patternCode, tacticCode, nudges, selfStarts);

        if (row is null || string.IsNullOrWhiteSpace(row.ParentStrategyTipVi)
            || string.IsNullOrWhiteSpace(row.PatternCode))
        {
            try
            {
                row = await _repo.UpsertWeekPlaybookAsync(
                    familyId,
                    memberId,
                    weekStart,
                    patternCode,
                    tacticCode,
                    lastFailedTactic: null,
                    strategyTip,
                    childVoiceJson: null,
                    childVoiceAt: null,
                    cancellationToken);

                await _repo.InsertBehaviorEventAsync(
                    familyId,
                    memberId,
                    FamilyBehaviorEventTypes.ParentStrategyTip,
                    commitmentId: null,
                    templateId: null,
                    new { patternCode, tacticCode, tip = strategyTip, weekStart },
                    cancellationToken);
                if (patternCode is not null)
                {
                    await _repo.InsertBehaviorEventAsync(
                        familyId,
                        memberId,
                        FamilyBehaviorEventTypes.PatternDetected,
                        commitmentId: null,
                        templateId: null,
                        new { patternCode, tacticCode, weekStart },
                        cancellationToken);
                }
            }
            catch
            {
                // best-effort until mig 267
            }
        }

        var pattern = FamilyBehaviorPatterns.Get(patternCode);
        var tactic = FamilyBehaviorPatterns.GetTactic(patternCode, tacticCode);
        var childVoice = ParseChildVoice(row?.ChildVoiceJson, memberId ?? row?.MemberId, weekStart, row?.ChildVoiceAt);

        var catalog = FamilyBehaviorPatterns.Catalog
            .Select(p => ToPatternCard(p))
            .ToList();
        var active = new List<BehaviorPatternCardDto>();
        if (pattern is not null)
        {
            active.Add(ToPatternCard(pattern, tacticCode, tactic));
        }

        return new WeekPlaybookDto(
            weekStart,
            today,
            pattern?.Code,
            pattern?.TitleVi,
            pattern?.WhyVi,
            tactic?.Code,
            tactic?.LabelVi,
            tactic?.ChildCueVi,
            tactic?.ParentAdviceVi,
            strategyTip,
            childVoice,
            catalog,
            active,
            nudges,
            selfStarts);
    }

    public async Task<ChildVoiceWeekDto> SubmitChildVoiceWeekAsync(
        Guid familyId,
        SubmitChildVoiceWeekRequest request,
        CancellationToken cancellationToken = default)
    {
        await _commercial.EnsureCapabilityAsync(
            familyId, FamilyCapabilityCodes.WeeklyInsight, cancellationToken);

        if (request.MemberId == Guid.Empty)
            throw new InvalidOperationException("Thiếu thành viên con.");

        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var localNow = FamilyTimeZones.NowIn(family.Timezone);
        var today = DateOnly.FromDateTime(localNow.DateTime);
        var weekStart = request.WeekStart ?? FamilyBehaviorPatterns.WeekStart(today);

        var hardest = NormalizeChoice(request.HardestCode, ["evening", "subject", "alone", "long", "other"]);
        var want = NormalizeChoice(
            request.WantParentCode,
            ["less_remind", "praise", "together", "choose_time", "friends", "other"]);
        var wish = Truncate(request.WishVi, 280);
        var tips = FamilyBehaviorPatterns.TipsFromChildVoice(hardest, want, wish);
        var submittedAt = DateTimeOffset.UtcNow;

        var payload = JsonSerializer.Serialize(new
        {
            hardestCode = hardest,
            wantParentCode = want,
            wishVi = wish,
            parentTipsVi = tips,
            submittedAt,
        });

        var weekLocal = localNow.AddDays(-(today.DayNumber - weekStart.DayNumber));
        var fromUtc = new DateTimeOffset(
            weekLocal.Year, weekLocal.Month, weekLocal.Day, 0, 0, 0, weekLocal.Offset)
            .ToUniversalTime();
        var toUtc = fromUtc.AddDays(7);
        var nudges = await _repo.CountFamilyEventsAsync(
            familyId, FamilyBehaviorEventTypes.ParentNudge, fromUtc, toUtc, cancellationToken);
        var selfStarts = await _repo.CountFamilyEventsAsync(
            familyId, FamilyBehaviorEventTypes.SelfStart, fromUtc, toUtc, cancellationToken);

        var existing = await _repo.GetWeekPlaybookAsync(
            familyId, request.MemberId, weekStart, cancellationToken);
        var patternCode = existing?.PatternCode;
        var tacticCode = existing?.TacticCode;
        if (want is "less_remind" or "choose_time")
            patternCode ??= FamilyBehaviorPatternCodes.NudgeDependent;
        else if (want is "together" or "friends")
            patternCode ??= FamilyBehaviorPatternCodes.SocialBoost;
        else if (hardest == "evening")
            patternCode ??= FamilyBehaviorPatternCodes.EveningFatigue;
        else if (hardest == "subject")
            patternCode ??= FamilyBehaviorPatternCodes.SubjectAvoidance;

        if (!string.IsNullOrWhiteSpace(patternCode) && string.IsNullOrWhiteSpace(tacticCode))
        {
            tacticCode = FamilyBehaviorPatterns.PickTacticCode(
                patternCode!, today, request.MemberId, existing?.LastFailedTactic);
        }

        var strategyTip = tips.FirstOrDefault()
            ?? FamilyBehaviorPatterns.ParentStrategyTipVi(patternCode, tacticCode, nudges, selfStarts);

        await _repo.UpsertWeekPlaybookAsync(
            familyId,
            request.MemberId,
            weekStart,
            patternCode,
            tacticCode,
            lastFailedTactic: null,
            strategyTip,
            payload,
            submittedAt,
            cancellationToken);

        try
        {
            await _repo.InsertBehaviorEventAsync(
                familyId,
                request.MemberId,
                FamilyBehaviorEventTypes.ChildVoiceSubmitted,
                commitmentId: null,
                templateId: null,
                new { weekStart, hardest, want, wish, tips },
                cancellationToken);
        }
        catch
        {
            // best-effort
        }

        return new ChildVoiceWeekDto(
            request.MemberId,
            weekStart,
            hardest,
            want,
            wish,
            tips,
            submittedAt);
    }

    private static BehaviorPatternCardDto ToPatternCard(
        BehaviorPatternDef pattern,
        string? activeTacticCode = null,
        BehaviorTacticDef? activeTactic = null) =>
        new(
            pattern.Code,
            pattern.TitleVi,
            pattern.WhyVi,
            activeTactic?.Code ?? activeTacticCode,
            activeTactic?.LabelVi,
            activeTactic?.ChildCueVi,
            activeTactic?.ParentAdviceVi,
            pattern.Tactics.Select(t => t.LabelVi).ToList());

    private static ChildVoiceWeekDto? ParseChildVoice(
        string? json,
        Guid? memberId,
        DateOnly weekStart,
        DateTimeOffset? submittedAt)
    {
        if (string.IsNullOrWhiteSpace(json) || json is "{}")
            return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var tips = new List<string>();
            if (root.TryGetProperty("parentTipsVi", out var tipsEl) && tipsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in tipsEl.EnumerateArray())
                {
                    if (t.ValueKind == JsonValueKind.String)
                        tips.Add(t.GetString() ?? "");
                }
            }

            return new ChildVoiceWeekDto(
                memberId,
                weekStart,
                root.TryGetProperty("hardestCode", out var h) ? h.GetString() : null,
                root.TryGetProperty("wantParentCode", out var w) ? w.GetString() : null,
                root.TryGetProperty("wishVi", out var wish) ? wish.GetString() : null,
                tips,
                submittedAt);
        }
        catch
        {
            return null;
        }
    }

    private static string? NormalizeChoice(string? raw, string[] allowed)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var v = raw.Trim().ToLowerInvariant();
        return allowed.Contains(v) ? v : "other";
    }

    private static string? Truncate(string? raw, int max)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var t = raw.Trim();
        return t.Length <= max ? t : t[..max];
    }

    private static IReadOnlyList<RetrievalQuestionDto> BuildRetrievalQuestions() =>
    [
        new(
            "method",
            "Thời gian vừa rồi con chủ yếu làm gì?",
            [
                new RetrievalOptionDto(FamilyRetrievalAnswers.Skim, FamilyRetrievalAnswers.MethodLabelVi(FamilyRetrievalAnswers.Skim)),
                new RetrievalOptionDto(FamilyRetrievalAnswers.Practice, FamilyRetrievalAnswers.MethodLabelVi(FamilyRetrievalAnswers.Practice)),
                new RetrievalOptionDto(FamilyRetrievalAnswers.Retrieve, FamilyRetrievalAnswers.MethodLabelVi(FamilyRetrievalAnswers.Retrieve)),
            ]),
        new(
            "recall",
            "Nếu hỏi lại ngay, con thế nào?",
            [
                new RetrievalOptionDto(FamilyRetrievalAnswers.CanExplain, FamilyRetrievalAnswers.RecallLabelVi(FamilyRetrievalAnswers.CanExplain)),
                new RetrievalOptionDto(FamilyRetrievalAnswers.Vaguely, FamilyRetrievalAnswers.RecallLabelVi(FamilyRetrievalAnswers.Vaguely)),
                new RetrievalOptionDto(FamilyRetrievalAnswers.NeedReview, FamilyRetrievalAnswers.RecallLabelVi(FamilyRetrievalAnswers.NeedReview)),
            ]),
    ];
}
