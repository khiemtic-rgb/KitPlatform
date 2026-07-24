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
    private readonly FamilyOsParentPushRepository _repo;
    private readonly FamilyGraphRepository _families;
    private readonly CustomerAppPushOptions _pushOptions;
    private readonly FamilyOsReminderOptions _reminderOptions;
    private readonly ILogger<FamilyOsParentPushService> _logger;

    public FamilyOsParentPushService(
        FamilyOsParentPushRepository repo,
        FamilyGraphRepository families,
        IOptions<CustomerAppPushOptions> pushOptions,
        IOptions<FamilyOsReminderOptions> reminderOptions,
        ILogger<FamilyOsParentPushService> logger)
    {
        _repo = repo;
        _families = families;
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
        if (subscriptions.Count == 0)
            return 0;

        var byFamily = subscriptions.GroupBy(s => s.FamilyId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var sent = 0;
        sent += await DispatchHotCommitmentsAsync(byFamily, cancellationToken);
        sent += await DispatchEveningDigestsAsync(byFamily, cancellationToken);
        return sent;
    }

    private async Task<int> DispatchHotCommitmentsAsync(
        IReadOnlyDictionary<Guid, List<FamilyOsParentPushRepository.SubscriptionRow>> byFamily,
        CancellationToken cancellationToken)
    {
        var opens = await _repo.ListOpenCommitmentsAsync(cancellationToken);
        var sent = 0;

        foreach (var row in opens)
        {
            if (!byFamily.TryGetValue(row.FamilyId, out var subs) || subs.Count == 0)
                continue;

            var localNow = FamilyTimeZones.NowIn(row.Timezone);
            var today = DateOnly.FromDateTime(localNow.DateTime);
            if (row.FlowDate != today)
                continue;

            var localTime = TimeOnly.FromTimeSpan(localNow.TimeOfDay);
            var (state, label) = FamilyCommitmentReminder.Evaluate(
                row.Status, row.WindowStart, row.WindowEnd, localTime);

            if (state is not (FamilyReminderStates.DueNow or FamilyReminderStates.Overdue))
                continue;

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

            if (await SendToSubscriptionsAsync(subs, title, body, "/today", cancellationToken))
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
            if (await SendToSubscriptionsAsync(subs, title, body, "/today", cancellationToken))
                sent++;
        }

        return sent;
    }

    private async Task<bool> SendToSubscriptionsAsync(
        IReadOnlyList<FamilyOsParentPushRepository.SubscriptionRow> subscriptions,
        string title,
        string body,
        string url,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            title,
            body,
            data = new { type = "familyos_parent_reminder", url },
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
