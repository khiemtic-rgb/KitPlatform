using System.Net;
using System.Text.Json;
using KitPlatform.Application.CustomerApp;
using KitPlatform.Packs.FamilyOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebPush;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyOsParentPushService : IFamilyOsParentPushService
{
    private static readonly int[] StreakMilestones = [3, 7, 14, 21, 27];

    private readonly FamilyOsParentPushRepository _repo;
    private readonly FamilyGraphRepository _families;
    private readonly FamilyValueRepository _value;
    private readonly FamilyBehaviorRepository _behaviorRepo;
    private readonly IFamilyBehaviorService _behavior;
    private readonly IFamilyMemoryService _memories;
    private readonly CustomerAppPushOptions _pushOptions;
    private readonly FamilyOsReminderOptions _reminderOptions;
    private readonly ILogger<FamilyOsParentPushService> _logger;

    public FamilyOsParentPushService(
        FamilyOsParentPushRepository repo,
        FamilyGraphRepository families,
        FamilyValueRepository value,
        FamilyBehaviorRepository behaviorRepo,
        IFamilyBehaviorService behavior,
        IFamilyMemoryService memories,
        IOptions<CustomerAppPushOptions> pushOptions,
        IOptions<FamilyOsReminderOptions> reminderOptions,
        ILogger<FamilyOsParentPushService> logger)
    {
        _repo = repo;
        _families = families;
        _value = value;
        _behaviorRepo = behaviorRepo;
        _behavior = behavior;
        _memories = memories;
        _pushOptions = pushOptions.Value;
        _reminderOptions = reminderOptions.Value;
        _logger = logger;
    }

    public async Task<FamilyParentPushStatusDto> GetStatusAsync(
        Guid familyId,
        Guid? membershipId,
        CancellationToken cancellationToken = default)
    {
        var supported = !string.IsNullOrWhiteSpace(_pushOptions.PublicKey)
            && !string.IsNullOrWhiteSpace(_pushOptions.PrivateKey)
            && _pushOptions.Enabled;

        var subscribed = false;
        if (supported && membershipId is Guid mid)
            subscribed = await _repo.HasSubscriptionAsync(familyId, mid, cancellationToken);

        return new FamilyParentPushStatusDto(
            Supported: supported,
            Subscribed: subscribed,
            PublicKey: supported ? _pushOptions.PublicKey : null);
    }

    public async Task SubscribeAsync(
        Guid familyId,
        FamilyParentPushSubscribeRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsurePushConfigured();

        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var member = members.FirstOrDefault(m => m.Id == request.MembershipId)
            ?? throw new InvalidOperationException("membershipId không thuộc gia đình này.");

        if (member.RoleCode is FamilyMembershipRoles.Child or FamilyMembershipRoles.Viewer)
            throw new InvalidOperationException("Chỉ phụ huynh / caregiver mới đăng ký nhắc push.");

        if (string.IsNullOrWhiteSpace(request.Endpoint)
            || string.IsNullOrWhiteSpace(request.P256dh)
            || string.IsNullOrWhiteSpace(request.Auth))
            throw new InvalidOperationException("Thiếu endpoint / p256dh / auth.");

        _ = family;
        await _repo.UpsertSubscriptionAsync(
            familyId,
            request.MembershipId,
            request.Endpoint.Trim(),
            request.P256dh.Trim(),
            request.Auth.Trim(),
            string.IsNullOrWhiteSpace(request.UserAgent) ? null : request.UserAgent.Trim(),
            cancellationToken);
    }

    public async Task UnsubscribeAsync(
        Guid familyId,
        Guid membershipId,
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new InvalidOperationException("endpoint bắt buộc.");

        await _repo.SoftDeleteSubscriptionAsync(
            familyId, membershipId, endpoint.Trim(), cancellationToken);
    }

    public async Task<int> DispatchDueParentRemindersAsync(CancellationToken cancellationToken = default)
    {
        if (!_reminderOptions.Enabled || !_pushOptions.Enabled)
            return 0;
        if (string.IsNullOrWhiteSpace(_pushOptions.PublicKey)
            || string.IsNullOrWhiteSpace(_pushOptions.PrivateKey))
            return 0;

        var subscriptions = await _repo.ListAllActiveSubscriptionsAsync(cancellationToken);
        var byFamily = subscriptions.GroupBy(s => s.FamilyId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var sent = 0;
        // Surprises also write Family Memories, so they run even without push subscriptions.
        sent += await DispatchPositiveSurprisesAsync(byFamily, cancellationToken);

        if (byFamily.Count == 0)
            return sent;

        // P1.14 lite: human signal first; then ≤1 alert push/family/day (voice | due | digest).
        sent += await DispatchRelationshipVoiceAsync(byFamily, cancellationToken);
        if (_reminderOptions.HotCommitmentPushEnabled)
            sent += await DispatchHotCommitmentsAsync(byFamily, cancellationToken);
        sent += await DispatchApprovalDigestsAsync(byFamily, cancellationToken);
        sent += await DispatchEveningDigestsAsync(byFamily, cancellationToken);
        return sent;
    }

    private async Task<int> DispatchRelationshipVoiceAsync(
        IReadOnlyDictionary<Guid, List<FamilyOsParentPushRepository.SubscriptionRow>> byFamily,
        CancellationToken cancellationToken)
    {
        var unread = await _repo.ListUnreadParentVoicesAsync(cancellationToken);
        var sent = 0;
        var seenFamily = new HashSet<Guid>();

        foreach (var row in unread)
        {
            if (!seenFamily.Add(row.FamilyId))
                continue;
            if (!byFamily.TryGetValue(row.FamilyId, out var subs) || subs.Count == 0)
                continue;

            var localNow = FamilyTimeZones.NowIn(row.Timezone);
            var today = DateOnly.FromDateTime(localNow.DateTime);
            if (row.FlowDate != today)
                continue;

            if (await _repo.HasAlertDispatchTodayAsync(
                    row.TenantId, row.FamilyId, today, cancellationToken))
                continue;

            var who = string.IsNullOrWhiteSpace(row.FromMemberName)
                ? "Người cùng chăm"
                : row.FromMemberName.Trim();
            var preview = string.IsNullOrWhiteSpace(row.BodyPreview)
                ? "Có một lời ấm chưa đọc."
                : row.BodyPreview.Trim();
            var summary = $"relationship_voice:{row.MessageId:N}";

            var inserted = await _repo.TryInsertDispatchAsync(
                row.TenantId,
                row.FamilyId,
                today,
                "relationship_voice",
                null,
                summary,
                cancellationToken);
            if (!inserted)
                continue;

            var preferred = subs.Where(s => s.MembershipId == row.ToMemberId).ToList();
            var targets = preferred.Count > 0 ? preferred : subs;

            if (await SendToSubscriptionsAsync(
                    targets,
                    $"{who} gửi lời cho bạn",
                    preview,
                    "/today",
                    "familyos_relationship_voice",
                    cancellationToken))
                sent++;
        }

        return sent;
    }

    public async Task<bool> TryNotifyFamilyAsync(
        Guid tenantId,
        Guid familyId,
        DateOnly flowDate,
        string kind,
        string title,
        string body,
        string url,
        string dataType,
        string? payloadSummary = null,
        Guid? preferMembershipId = null,
        CancellationToken cancellationToken = default)
    {
        if (!_reminderOptions.Enabled || !_pushOptions.Enabled)
            return false;
        if (string.IsNullOrWhiteSpace(_pushOptions.PublicKey)
            || string.IsNullOrWhiteSpace(_pushOptions.PrivateKey))
            return false;

        var subs = await _repo.ListSubscriptionsForFamilyAsync(familyId, cancellationToken);
        if (subs.Count == 0)
            return false;

        var summary = string.IsNullOrWhiteSpace(payloadSummary)
            ? $"{kind}:{title}"
            : payloadSummary.Trim();
        if (summary.Length > 390)
            summary = summary[..390];

        var inserted = await _repo.TryInsertDispatchAsync(
            tenantId, familyId, flowDate, kind, null, summary, cancellationToken);
        if (!inserted)
            return false;

        if (preferMembershipId is Guid mid)
        {
            var preferred = subs.Where(s => s.MembershipId == mid).ToList();
            if (preferred.Count > 0)
                subs = preferred;
        }

        return await SendToSubscriptionsAsync(subs, title, body, url, dataType, cancellationToken);
    }

    private async Task<int> DispatchHotCommitmentsAsync(
        IReadOnlyDictionary<Guid, List<FamilyOsParentPushRepository.SubscriptionRow>> byFamily,
        CancellationToken cancellationToken)
    {
        var opens = await _repo.ListOpenCommitmentsAsync(cancellationToken);
        var sent = 0;

        var alertedFamilies = new HashSet<Guid>();

        foreach (var row in opens)
        {
            if (!byFamily.TryGetValue(row.FamilyId, out var subs) || subs.Count == 0)
                continue;
            if (alertedFamilies.Contains(row.FamilyId))
                continue;

            var localNow = FamilyTimeZones.NowIn(row.Timezone);
            var today = DateOnly.FromDateTime(localNow.DateTime);
            if (row.FlowDate != today)
                continue;

            if (await _repo.HasAlertDispatchTodayAsync(
                    row.TenantId, row.FamilyId, today, cancellationToken))
            {
                alertedFamilies.Add(row.FamilyId);
                continue;
            }

            var localTime = TimeOnly.FromTimeSpan(localNow.TimeOfDay);
            var (state, label) = FamilyCommitmentReminder.Evaluate(
                row.Status,
                row.WindowStart,
                row.WindowEnd,
                localTime,
                row.HabitStage,
                row.ReminderSuppressed);

            // Alert push: due_now / overdue only (upcoming stays in-app; not early-task spam).
            if (state is not (FamilyReminderStates.DueNow or FamilyReminderStates.Overdue))
                continue;

            var nudgesUsed = 0;
            var observeOnly = false;
            var nudgeBudget = FamilyMotivationIntervention.DefaultParentNudgeBudgetPerDay;
            try
            {
                nudgesUsed = await _value.GetNudgeCountAsync(
                    row.FamilyId, row.FlowDate, cancellationToken);
                var policy = await _behaviorRepo.GetRetirementPolicyAsync(
                    row.FamilyId, cancellationToken);
                observeOnly = policy?.ObserveOnly ?? false;
                if (policy?.ParentNudgeBudget is int b)
                    nudgeBudget = b;
            }
            catch
            {
                // ignore
            }

            var decision = FamilyMotivationIntervention.Decide(
                new FamilyMotivationIntervention.Input(
                    row.Status,
                    state,
                    row.HabitStage,
                    row.ReminderSuppressed,
                    row.HabitStreakDays,
                    FamilyLearningMission.IsLearningTitle(row.Title),
                    SkipReason: null,
                    nudgesUsed,
                    nudgeBudget,
                    FamilyObserveOnly: observeOnly,
                    WindowEnd: row.WindowEnd,
                    Title: row.Title));

            if (!decision.AllowParentPush)
            {
                try
                {
                    await _behavior.RecordParentNudgeAsync(
                        row.FamilyId,
                        row.CommitmentId,
                        memberId: null,
                        allowed: false,
                        reason: decision.InterventionLevel,
                        cancellationToken);
                }
                catch
                {
                    // best-effort
                }

                continue;
            }

            var kind = state == FamilyReminderStates.Overdue ? "overdue" : "due_now";
            var who = string.IsNullOrWhiteSpace(row.MemberName) ? "" : $"{row.MemberName} · ";
            var title = state == FamilyReminderStates.Overdue ? "Quá giờ rồi" : "Đến giờ rồi";
            var body = $"{who}{row.Title}" + (label is null ? "" : $" ({label})");
            var summary = $"{kind}:{row.Title}";

            var inserted = await _repo.TryInsertDispatchAsync(
                row.TenantId, row.FamilyId, row.FlowDate, kind, row.CommitmentId, summary,
                cancellationToken);
            if (!inserted)
                continue;

            if (await SendToSubscriptionsAsync(
                    subs, title, body, "/today", "familyos_parent_reminder", cancellationToken))
            {
                sent++;
                alertedFamilies.Add(row.FamilyId);
                try
                {
                    await _value.IncrementNudgeAsync(row.FamilyId, row.FlowDate, 1, cancellationToken);
                    await _behavior.RecordParentNudgeAsync(
                        row.FamilyId,
                        row.CommitmentId,
                        memberId: null,
                        allowed: true,
                        reason: "push_dispatch",
                        cancellationToken);
                }
                catch
                {
                    // best-effort
                }
            }
        }

        return sent;
    }

    private async Task<int> DispatchApprovalDigestsAsync(
        IReadOnlyDictionary<Guid, List<FamilyOsParentPushRepository.SubscriptionRow>> byFamily,
        CancellationToken cancellationToken)
    {
        var hour = Math.Clamp(_reminderOptions.ApprovalDigestHour, 0, 23);
        var families = await _repo.ListActiveFamiliesAsync(cancellationToken);
        var sent = 0;

        foreach (var family in families)
        {
            if (!byFamily.TryGetValue(family.FamilyId, out var subs) || subs.Count == 0)
                continue;

            var localNow = FamilyTimeZones.NowIn(family.Timezone);
            if (localNow.Hour < hour)
                continue;

            var today = DateOnly.FromDateTime(localNow.DateTime);
            if (await _repo.HasAlertDispatchTodayAsync(
                    family.TenantId, family.FamilyId, today, cancellationToken))
                continue;

            var pending = await _repo.ListPendingApprovalsAsync(
                family.TenantId, family.FamilyId, today, cancellationToken);

            var needReview = pending
                .Where(p => FamilyCommitmentReview.NeedsParentApproval(p.Title, p.EvidenceUrl))
                .Take(5)
                .ToList();

            if (needReview.Count == 0)
                continue;

            var n = needReview.Count;
            var samples = needReview
                .Select(p =>
                {
                    var who = string.IsNullOrWhiteSpace(p.MemberName) ? "" : $"{p.MemberName}: ";
                    return $"{who}{p.Title}";
                })
                .ToList();

            var summary = $"approval:{n}|" + string.Join("|", samples);
            if (summary.Length > 390)
                summary = summary[..390];

            var inserted = await _repo.TryInsertDispatchAsync(
                family.TenantId,
                family.FamilyId,
                today,
                "approval_digest",
                null,
                summary,
                cancellationToken);
            if (!inserted)
                continue;

            var title = n == 1
                ? "Chỉ 1 việc cần xác nhận (~15 giây)"
                : $"Chỉ {n} việc cần xác nhận (~15 giây)";
            var body = string.Join(" · ", samples.Take(2))
                       + (n > 2 ? $" · +{n - 2} việc nữa" : "")
                       + " — đổi lấy việc phải nhắc con ít hơn.";

            if (await SendToSubscriptionsAsync(
                    subs, title, body, "/today", "familyos_approval_digest", cancellationToken))
                sent++;
        }

        return sent;
    }

    private async Task<int> DispatchEveningDigestsAsync(
        IReadOnlyDictionary<Guid, List<FamilyOsParentPushRepository.SubscriptionRow>> byFamily,
        CancellationToken cancellationToken)
    {
        var hour = Math.Clamp(_reminderOptions.EveningDigestHour, 0, 23);
        var families = await _repo.ListActiveFamiliesAsync(cancellationToken);
        var sent = 0;

        foreach (var family in families)
        {
            if (!byFamily.TryGetValue(family.FamilyId, out var subs) || subs.Count == 0)
                continue;

            var localNow = FamilyTimeZones.NowIn(family.Timezone);
            if (localNow.Hour < hour)
                continue;

            var today = DateOnly.FromDateTime(localNow.DateTime);
            if (await _repo.HasAlertDispatchTodayAsync(
                    family.TenantId, family.FamilyId, today, cancellationToken))
                continue;

            var candidates = await _repo.ListDigestCandidatesAsync(
                family.TenantId, family.FamilyId, today, cancellationToken);

            var lateItems = new List<string>();
            var localTime = TimeOnly.FromTimeSpan(localNow.TimeOfDay);

            foreach (var c in candidates)
            {
                if (c.Status is FamilyCommitmentStatuses.Pending or FamilyCommitmentStatuses.InProgress)
                {
                    var (state, _) = FamilyCommitmentReminder.Evaluate(
                        c.Status, c.WindowStart, c.WindowEnd, localTime);
                    if (state is FamilyReminderStates.Overdue or FamilyReminderStates.DueNow)
                    {
                        var who = string.IsNullOrWhiteSpace(c.MemberName) ? "" : $"{c.MemberName}: ";
                        lateItems.Add($"{who}{c.Title} (chưa xong)");
                    }
                }
                else if (c.Status == FamilyCommitmentStatuses.Done
                         && FamilyCommitmentReminder.IsLateDone(
                             c.Status, c.CompletedAt, c.WindowEnd, today, family.Timezone))
                {
                    var who = string.IsNullOrWhiteSpace(c.MemberName) ? "" : $"{c.MemberName}: ";
                    lateItems.Add($"{who}{c.Title} (xong muộn)");
                }

                if (lateItems.Count >= 3)
                    break;
            }

            if (lateItems.Count == 0)
                continue;

            var summary = string.Join(" | ", lateItems);
            var inserted = await _repo.TryInsertDispatchAsync(
                family.TenantId,
                family.FamilyId,
                today,
                "evening_digest",
                null,
                summary.Length > 390 ? summary[..390] : summary,
                cancellationToken);
            if (!inserted)
                continue;

            var title = "Tóm tắt tối — việc muộn";
            var body = string.Join(" · ", lateItems);
            if (await SendToSubscriptionsAsync(
                    subs, title, body, "/today", "familyos_parent_reminder", cancellationToken))
                sent++;
        }

        return sent;
    }

    private async Task<int> DispatchPositiveSurprisesAsync(
        IReadOnlyDictionary<Guid, List<FamilyOsParentPushRepository.SubscriptionRow>> byFamily,
        CancellationToken cancellationToken)
    {
        var earliest = Math.Clamp(_reminderOptions.SurpriseEarliestHour, 0, 23);
        var families = await _repo.ListActiveFamiliesAsync(cancellationToken);
        var sent = 0;

        foreach (var family in families)
        {
            byFamily.TryGetValue(family.FamilyId, out var subs);
            subs ??= [];

            var localNow = FamilyTimeZones.NowIn(family.Timezone);
            if (localNow.Hour < earliest)
                continue;

            var today = DateOnly.FromDateTime(localNow.DateTime);
            var agg = await _repo.GetDayAggregateAsync(
                family.TenantId, family.FamilyId, today, cancellationToken);
            if (agg is null || agg.ChildTotal <= 0)
                continue;

            // All child tasks finished — soft win (may include late).
            if (agg.ChildOpen == 0 && agg.ChildDone > 0)
            {
                var allDoneBody = agg.ChildLateDone == 0 && agg.AppliedConsequences == 0
                    ? "Con hoàn thành toàn bộ. Không cần mở App — chúc cả nhà buổi tối vui vẻ."
                    : "Con đã xong hết việc hôm nay. Bố mẹ có thể nghỉ một nhịp.";
                if (await TryFamilyKindPushAsync(
                        family, today, subs,
                        kind: "all_done",
                        title: "Cả nhà xong việc rồi",
                        body: allDoneBody,
                        dataType: "familyos_surprise",
                        cancellationToken))
                    sent++;
            }

            var beautiful = agg.ChildOpen == 0
                && agg.ChildTotal > 0
                && agg.AppliedConsequences == 0
                && agg.ChildLateDone == 0;

            if (beautiful)
            {
                await TryCaptureMemoryAsync(
                    family,
                    today,
                    FamilyMemoryKinds.BeautifulDay,
                    "Ngày đẹp — không phải nhắc con",
                    "Con xong hết việc đúng giờ, cả nhà không phải nhắc.",
                    "🌤️",
                    cancellationToken);

                if (await TryFamilyKindPushAsync(
                        family, today, subs,
                        kind: "beautiful_day",
                        title: "Ngày đẹp của cả nhà",
                        body: "Hôm nay không phải nhắc con — một khoảnh khắc đáng giữ lại.",
                        dataType: "familyos_surprise",
                        cancellationToken))
                    sent++;

                var streak = await _repo.CountBeautifulDayStreakAsync(
                    family.TenantId, family.FamilyId, today, family.Timezone, cancellationToken);
                if (StreakMilestones.Contains(streak))
                {
                    await TryCaptureMemoryAsync(
                        family,
                        today,
                        FamilyMemoryKinds.StreakMilestone,
                        $"Chuỗi {streak} ngày đẹp liên tiếp",
                        "Nhịp sống của nhà mình đang dần thành thói quen.",
                        "🔥",
                        cancellationToken,
                        sourceRef: $"streak:{today:yyyy-MM-dd}:{streak}");

                    if (await TryFamilyKindPushAsync(
                            family,
                            today,
                            subs,
                            kind: "streak_milestone",
                            title: $"Ngày thứ {streak}",
                            body: streak >= 27
                                ? $"Hôm nay là ngày thứ {streak} cả nhà cùng giữ nhịp. Đây là kỷ lục đáng tự hào."
                                : $"Chuỗi {streak} ngày đẹp liên tiếp — gia đình đang lớn lên từng ngày.",
                            dataType: "familyos_surprise",
                            cancellationToken: cancellationToken,
                            summary: $"streak:{streak}"))
                        sent++;
                }
            }
        }

        return sent;
    }

    private async Task TryCaptureMemoryAsync(
        FamilyOsParentPushRepository.FamilyClockRow family,
        DateOnly today,
        string kind,
        string titleVi,
        string noteVi,
        string icon,
        CancellationToken cancellationToken,
        string? sourceRef = null)
    {
        try
        {
            await _memories.TryCaptureAsync(
                family.TenantId,
                family.FamilyId,
                today,
                kind,
                titleVi,
                noteVi: noteVi,
                icon: icon,
                sourceRef: sourceRef ?? $"{kind}:{today:yyyy-MM-dd}",
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FamilyOS memory capture failed for family {FamilyId}.", family.FamilyId);
        }
    }

    private async Task<bool> TryFamilyKindPushAsync(
        FamilyOsParentPushRepository.FamilyClockRow family,
        DateOnly today,
        IReadOnlyList<FamilyOsParentPushRepository.SubscriptionRow> subs,
        string kind,
        string title,
        string body,
        string dataType,
        CancellationToken cancellationToken,
        string? summary = null)
    {
        // No subscriber yet — don't burn the dedupe row, parent may subscribe later today.
        if (subs.Count == 0)
            return false;

        var payload = summary ?? $"{kind}:{today:yyyy-MM-dd}";
        if (payload.Length > 390)
            payload = payload[..390];

        var inserted = await _repo.TryInsertDispatchAsync(
            family.TenantId, family.FamilyId, today, kind, null, payload, cancellationToken);
        if (!inserted)
            return false;

        return await SendToSubscriptionsAsync(subs, title, body, "/today", dataType, cancellationToken);
    }

    private async Task<bool> SendToSubscriptionsAsync(
        IReadOnlyList<FamilyOsParentPushRepository.SubscriptionRow> subscriptions,
        string title,
        string body,
        string url,
        string dataType,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            title,
            body,
            silent = false,
            data = new { type = dataType, url },
        });

        var vapid = new VapidDetails(_pushOptions.Subject, _pushOptions.PublicKey, _pushOptions.PrivateKey);
        var client = new WebPushClient();
        var any = false;

        foreach (var sub in subscriptions)
        {
            try
            {
                var pushSub = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                await client.SendNotificationAsync(pushSub, payload, vapid, cancellationToken);
                any = true;
            }
            catch (WebPushException ex) when (
                ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Pruning stale FamilyOS push endpoint.");
                await _repo.SoftDeleteByEndpointAsync(sub.Endpoint, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FamilyOS parent push failed for family {FamilyId}.", sub.FamilyId);
            }
        }

        return any;
    }

    private void EnsurePushConfigured()
    {
        if (!_pushOptions.Enabled
            || string.IsNullOrWhiteSpace(_pushOptions.PublicKey)
            || string.IsNullOrWhiteSpace(_pushOptions.PrivateKey))
        {
            throw new InvalidOperationException(
                "Web Push chưa cấu hình (CustomerAppPush PublicKey/PrivateKey).");
        }
    }
}
