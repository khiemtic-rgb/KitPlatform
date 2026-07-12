using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Application.CustomerApp;
using KitPlatform.Application.Customers;
using WebPush;

namespace KitPlatform.Infrastructure.CustomerApp;

internal sealed class CustomerPushService : ICustomerPushService
{
    private readonly CustomerPushRepository _repo;
    private readonly CustomerEngagementRepository _engagement;
    private readonly CustomerReminderRepository _reminders;
    private readonly CustomerAppConsentRepository _consents;
    private readonly ICustomerEngagementEventService _engagementEvents;
    private readonly ICustomerNotificationTextService _notifyText;
    private readonly CustomerAppPushOptions _options;
    private readonly ILogger<CustomerPushService> _logger;

    public CustomerPushService(
        CustomerPushRepository repo,
        CustomerEngagementRepository engagement,
        CustomerReminderRepository reminders,
        CustomerAppConsentRepository consents,
        ICustomerEngagementEventService engagementEvents,
        ICustomerNotificationTextService notifyText,
        IOptions<CustomerAppPushOptions> options,
        ILogger<CustomerPushService> logger)
    {
        _repo = repo;
        _engagement = engagement;
        _reminders = reminders;
        _consents = consents;
        _engagementEvents = engagementEvents;
        _notifyText = notifyText;
        _options = options.Value;
        _logger = logger;
    }

    public string? GetPublicKey() =>
        string.IsNullOrWhiteSpace(_options.PublicKey) ? null : _options.PublicKey.Trim();

    public async Task<PushSubscriptionStatusDto> GetStatusAsync(
        Guid tenantId,
        Guid customerAccountId,
        CancellationToken cancellationToken = default)
    {
        var json = await _repo.GetDeviceTokensJsonAsync(tenantId, customerAccountId, cancellationToken);
        var subscriptions = ParseSubscriptions(json);
        return new PushSubscriptionStatusDto(
            Supported: !string.IsNullOrWhiteSpace(GetPublicKey()),
            Subscribed: subscriptions.Count > 0,
            SubscriptionCount: subscriptions.Count,
            PublicKey: GetPublicKey());
    }

    public async Task RegisterSubscriptionAsync(
        Guid tenantId,
        Guid customerAccountId,
        RegisterPushSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Endpoint)
            || string.IsNullOrWhiteSpace(request.P256dh)
            || string.IsNullOrWhiteSpace(request.Auth))
        {
            throw new InvalidOperationException("Thiếu thông tin đăng ký push.");
        }

        var json = await _repo.GetDeviceTokensJsonAsync(tenantId, customerAccountId, cancellationToken);
        var subscriptions = ParseSubscriptions(json);
        var endpoint = request.Endpoint.Trim();
        var next = subscriptions
            .Where(s => !string.Equals(s.Endpoint, endpoint, StringComparison.Ordinal))
            .Append(new StoredPushSubscription(endpoint, request.P256dh.Trim(), request.Auth.Trim(), DateTimeOffset.UtcNow))
            .ToList();

        await _repo.SaveDeviceTokensJsonAsync(
            tenantId,
            customerAccountId,
            SerializeSubscriptions(next),
            cancellationToken);

        var customerId = await _repo.GetCustomerIdForAccountAsync(tenantId, customerAccountId, cancellationToken);
        if (customerId.HasValue)
        {
            await _engagementEvents.RecordEventAsync(
                tenantId,
                customerAccountId,
                customerId.Value,
                CustomerEngagementEventTypes.PushEnable,
                cancellationToken: cancellationToken);
        }
    }

    public async Task UnregisterSubscriptionAsync(
        Guid tenantId,
        Guid customerAccountId,
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new InvalidOperationException("Thiếu endpoint push.");

        var json = await _repo.GetDeviceTokensJsonAsync(tenantId, customerAccountId, cancellationToken);
        var subscriptions = ParseSubscriptions(json)
            .Where(s => !string.Equals(s.Endpoint, endpoint.Trim(), StringComparison.Ordinal))
            .ToList();

        await _repo.SaveDeviceTokensJsonAsync(
            tenantId,
            customerAccountId,
            SerializeSubscriptions(subscriptions),
            cancellationToken);
    }

    public async Task<int> DispatchDueRemindersAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.PublicKey) || string.IsNullOrWhiteSpace(_options.PrivateKey))
            return 0;

        var dueRows = await _repo.ListDueRemindersAsync(30, cancellationToken);
        if (dueRows.Count == 0)
            return 0;

        var sentCount = 0;
        foreach (var row in dueRows)
        {
            try
            {
                if (!await _consents.HasGrantedConsentAsync(
                        row.TenantId,
                        row.CustomerId,
                        CustomerConsentChannels.AppPush,
                        CustomerConsentPurposes.CareReminder,
                        cancellationToken))
                {
                    await AdvanceReminderScheduleAsync(row, cancellationToken);
                    continue;
                }

                var subscriptions = ParseSubscriptions(row.DeviceTokensJson);
                if (subscriptions.Count == 0)
                {
                    await AdvanceReminderScheduleAsync(row, cancellationToken);
                    continue;
                }

                var locale = await _notifyText.ResolveLocaleAsync(row.TenantId, row.CustomerId, cancellationToken);
                var title = row.FamilyMemberId.HasValue && row.NotifyCaregiver && !string.IsNullOrWhiteSpace(row.FamilyMemberName)
                    ? await _notifyText.FormatAsync(
                        row.TenantId,
                        locale,
                        CustomerNotificationTextKeys.MedicationReminderFamilyTitle,
                        new Dictionary<string, string> { ["name"] = row.FamilyMemberName! },
                        cancellationToken)
                    : await _notifyText.FormatAsync(
                        row.TenantId,
                        locale,
                        CustomerNotificationTextKeys.MedicationReminderTitle,
                        cancellationToken: cancellationToken);
                var bodyArgs = new Dictionary<string, string> { ["productName"] = row.ProductName };
                if (!string.IsNullOrWhiteSpace(row.DosageNote))
                    bodyArgs["dosageNote"] = row.DosageNote;
                var bodyKey = string.IsNullOrWhiteSpace(row.DosageNote)
                    ? CustomerNotificationTextKeys.MedicationReminderBody
                    : CustomerNotificationTextKeys.MedicationReminderBodyWithNote;
                var body = await _notifyText.FormatAsync(row.TenantId, locale, bodyKey, bodyArgs, cancellationToken);
                var notification = JsonSerializer.Serialize(new
                {
                    title,
                    body,
                    data = new
                    {
                        type = "medication_reminder",
                        reminderId = row.ReminderId,
                        productId = row.ProductId,
                        productName = row.ProductName,
                    },
                });

                var staleEndpoints = new List<string>();
                var delivered = false;
                foreach (var subscription in subscriptions)
                {
                    try
                    {
                        await SendPushAsync(subscription, notification, cancellationToken);
                        delivered = true;
                    }
                    catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone
                                                      || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        staleEndpoints.Add(subscription.Endpoint);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Push failed for reminder {ReminderId}", row.ReminderId);
                    }
                }

                if (staleEndpoints.Count > 0)
                    await RemoveStaleSubscriptionsAsync(row, staleEndpoints, cancellationToken);

                if (delivered)
                {
                    await _repo.InsertNotificationAsync(
                        row.TenantId,
                        row.CustomerId,
                        title,
                        body,
                        new { reminderId = row.ReminderId, channel = "app_push", familyMemberId = row.FamilyMemberId },
                        "medication",
                        "/reminders",
                        cancellationToken);
                    sentCount++;
                }

                await AdvanceReminderScheduleAsync(row, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed processing reminder push {ReminderId}", row.ReminderId);
            }
        }

        return sentCount;
    }

    public async Task<int> DispatchEngagementNotificationsAsync(CancellationToken cancellationToken = default)
    {
        var sent = 0;
        sent += await DispatchCareReminderNotificationsAsync(cancellationToken);
        sent += await DispatchRepurchaseNotificationsAsync(cancellationToken);
        sent += await DispatchMissedAdherenceNotificationsAsync(cancellationToken);
        return sent;
    }

    private async Task<int> DispatchCareReminderNotificationsAsync(CancellationToken cancellationToken)
    {
        var sent = 0;
        var advanceRows = await _engagement.ListCareRemindersForAdvanceNoticeAsync(30, cancellationToken);
        foreach (var row in advanceRows)
        {
            if (await TryDispatchCareReminderAsync(row, advance: true, cancellationToken))
                sent++;
            await _engagement.MarkCareReminderAdvanceNotifiedAsync(row.TenantId, row.CareReminderId, cancellationToken);
        }

        var dueRows = await _engagement.ListCareRemindersDueAsync(30, cancellationToken);
        foreach (var row in dueRows)
        {
            if (await TryDispatchCareReminderAsync(row, advance: false, cancellationToken))
                sent++;
            await _engagement.MarkCareReminderDueNotifiedAsync(row.TenantId, row.CareReminderId, cancellationToken);
        }

        return sent;
    }

    private async Task<bool> TryDispatchCareReminderAsync(
        CareReminderDispatchRow row,
        bool advance,
        CancellationToken cancellationToken)
    {
        var locale = await _notifyText.ResolveLocaleAsync(row.TenantId, row.CustomerId, cancellationToken);
        var when = new DateTimeOffset(DateTime.SpecifyKind(row.RemindAt, DateTimeKind.Utc))
            .ToOffset(TimeSpan.FromHours(7))
            .ToString("dd/MM/yyyy HH:mm");
        var titleKey = advance
            ? (string.IsNullOrWhiteSpace(row.FamilyMemberName)
                ? CustomerNotificationTextKeys.CareAdvanceTitle
                : CustomerNotificationTextKeys.CareAdvanceFamilyTitle)
            : (string.IsNullOrWhiteSpace(row.FamilyMemberName)
                ? CustomerNotificationTextKeys.CareDueTitle
                : CustomerNotificationTextKeys.CareDueFamilyTitle);
        var titleArgs = new Dictionary<string, string> { ["title"] = row.Title };
        if (!string.IsNullOrWhiteSpace(row.FamilyMemberName))
            titleArgs["name"] = row.FamilyMemberName;
        var title = await _notifyText.FormatAsync(row.TenantId, locale, titleKey, titleArgs, cancellationToken);
        var bodyKey = advance
            ? CustomerNotificationTextKeys.CareAdvanceBody
            : CustomerNotificationTextKeys.CareDueBody;
        var body = await _notifyText.FormatAsync(
            row.TenantId,
            locale,
            bodyKey,
            new Dictionary<string, string>
            {
                ["when"] = when,
                ["note"] = FormatCareNote(row.Note),
            },
            cancellationToken);

        return await NotifyCustomerAsync(
            row.TenantId,
            row.CustomerId,
            row.AccountId,
            row.DeviceTokensJson,
            title,
            body,
            "care",
            "/health",
            new { type = advance ? "care_reminder_advance" : "care_reminder_due", careReminderId = row.CareReminderId },
            cancellationToken);
    }

    private async Task<int> DispatchRepurchaseNotificationsAsync(CancellationToken cancellationToken)
    {
        var sent = 0;
        var rows = await _engagement.ListRepurchaseDueAsync(30, cancellationToken);
        foreach (var row in rows)
        {
            var locale = await _notifyText.ResolveLocaleAsync(row.TenantId, row.CustomerId, cancellationToken);
            var dateLabel = row.SuggestedForDate?.ToString("dd/MM/yyyy")
                ?? await _notifyText.FormatAsync(
                    row.TenantId,
                    locale,
                    CustomerNotificationTextKeys.DateToday,
                    cancellationToken: cancellationToken);
            var title = await _notifyText.FormatAsync(
                row.TenantId,
                locale,
                CustomerNotificationTextKeys.RepurchaseTitle,
                cancellationToken: cancellationToken);
            var body = await _notifyText.FormatAsync(
                row.TenantId,
                locale,
                CustomerNotificationTextKeys.RepurchaseBody,
                new Dictionary<string, string>
                {
                    ["orderLabel"] = row.OrderLabel,
                    ["dateLabel"] = dateLabel,
                },
                cancellationToken);
            if (await NotifyCustomerAsync(
                    row.TenantId,
                    row.CustomerId,
                    row.AccountId,
                    row.DeviceTokensJson,
                    title,
                    body,
                    "medication",
                    "/medications",
                    new { type = "repurchase_due", repurchaseId = row.RepurchaseId },
                    cancellationToken))
            {
                sent++;
            }

            await _engagement.MarkRepurchaseNotifiedAsync(row.TenantId, row.RepurchaseId, cancellationToken);
        }

        return sent;
    }

    private async Task<int> DispatchMissedAdherenceNotificationsAsync(CancellationToken cancellationToken)
    {
        var sent = 0;
        var utcNow = DateTimeOffset.UtcNow;
        var candidates = await _engagement.ListMissedAdherenceCandidatesAsync(50, cancellationToken);
        foreach (var row in candidates)
        {
            var summary = await _reminders.GetAdherenceSummaryAsync(row.TenantId, row.CustomerId, utcNow, cancellationToken);
            if (summary.MissedStreakDays < 3)
                continue;

            var locale = await _notifyText.ResolveLocaleAsync(row.TenantId, row.CustomerId, cancellationToken);
            var title = await _notifyText.FormatAsync(
                row.TenantId,
                locale,
                CustomerNotificationTextKeys.AdherenceMissedTitle,
                cancellationToken: cancellationToken);
            var body = await _notifyText.FormatAsync(
                row.TenantId,
                locale,
                CustomerNotificationTextKeys.AdherenceMissedBody,
                new Dictionary<string, string> { ["days"] = summary.MissedStreakDays.ToString() },
                cancellationToken);
            if (await NotifyCustomerAsync(
                    row.TenantId,
                    row.CustomerId,
                    row.AccountId,
                    row.DeviceTokensJson,
                    title,
                    body,
                    "medication",
                    "/reminders",
                    new { type = "adherence_missed", streakDays = summary.MissedStreakDays },
                    cancellationToken))
            {
                sent++;
            }

            await _engagement.MarkAdherenceAlertDispatchedAsync(row.TenantId, row.CustomerId, cancellationToken);
        }

        return sent;
    }

    private static string FormatCareNote(string? note) =>
        string.IsNullOrWhiteSpace(note) ? "" : $" {note.Trim()}";

    private async Task<bool> NotifyCustomerAsync(
        Guid tenantId,
        Guid customerId,
        Guid accountId,
        string deviceTokensJson,
        string title,
        string body,
        string category,
        string href,
        object payload,
        CancellationToken cancellationToken)
    {
        await _repo.InsertNotificationAsync(
            tenantId,
            customerId,
            title,
            body,
            payload,
            category,
            href,
            cancellationToken);

        if (!_options.Enabled
            || string.IsNullOrWhiteSpace(_options.PublicKey)
            || string.IsNullOrWhiteSpace(_options.PrivateKey))
        {
            return true;
        }

        if (!await _consents.HasGrantedConsentAsync(
                tenantId,
                customerId,
                CustomerConsentChannels.AppPush,
                CustomerConsentPurposes.CareReminder,
                cancellationToken))
            return true;

        var subscriptions = ParseSubscriptions(deviceTokensJson);
        if (subscriptions.Count == 0)
            return true;

        var notification = JsonSerializer.Serialize(new
        {
            title,
            body,
            data = new { url = href, category },
        });

        var staleEndpoints = new List<string>();
        var delivered = false;
        foreach (var subscription in subscriptions)
        {
            try
            {
                await SendPushAsync(subscription, notification, cancellationToken);
                delivered = true;
            }
            catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone
                                              || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                staleEndpoints.Add(subscription.Endpoint);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Engagement push failed for customer {CustomerId}", customerId);
            }
        }

        if (staleEndpoints.Count > 0)
        {
            var remaining = subscriptions
                .Where(s => !staleEndpoints.Contains(s.Endpoint, StringComparer.Ordinal))
                .ToList();
            await _repo.SaveDeviceTokensJsonAsync(
                tenantId,
                accountId,
                SerializeSubscriptions(remaining),
                cancellationToken);
        }

        return delivered;
    }

    public async Task SendStaffChatReplyPushAsync(
        Guid tenantId,
        Guid customerId,
        string? staffName,
        string messageBody,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled
            || string.IsNullOrWhiteSpace(_options.PublicKey)
            || string.IsNullOrWhiteSpace(_options.PrivateKey))
        {
            return;
        }

        if (!await _consents.HasGrantedConsentAsync(
                tenantId,
                customerId,
                CustomerConsentChannels.InApp,
                CustomerConsentPurposes.AiAssist,
                cancellationToken))
        {
            return;
        }

        var target = await _repo.GetPushTargetByCustomerAsync(tenantId, customerId, cancellationToken);
        if (target is null)
            return;

        var subscriptions = ParseSubscriptions(target.DeviceTokensJson);
        if (subscriptions.Count == 0)
            return;

        var locale = await _notifyText.ResolveLocaleAsync(tenantId, customerId, cancellationToken);
        var title = string.IsNullOrWhiteSpace(staffName)
            ? await _notifyText.FormatAsync(
                tenantId,
                locale,
                CustomerNotificationTextKeys.ChatStaffReplyTitle,
                cancellationToken: cancellationToken)
            : await _notifyText.FormatAsync(
                tenantId,
                locale,
                CustomerNotificationTextKeys.ChatStaffReplyNamedTitle,
                new Dictionary<string, string> { ["name"] = staffName },
                cancellationToken);
        var preview = messageBody.Length > 180 ? messageBody[..177] + "..." : messageBody;
        var payload = JsonSerializer.Serialize(new
        {
            title,
            body = preview,
            data = new
            {
                type = "staff_chat_reply",
                url = "/chat",
            },
        });

        var staleEndpoints = new List<string>();
        var delivered = false;
        foreach (var subscription in subscriptions)
        {
            try
            {
                await SendPushAsync(subscription, payload, cancellationToken);
                delivered = true;
            }
            catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone
                                              || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                staleEndpoints.Add(subscription.Endpoint);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Chat reply push failed for customer {CustomerId}", customerId);
            }
        }

        if (staleEndpoints.Count > 0)
        {
            var remaining = subscriptions
                .Where(s => !staleEndpoints.Contains(s.Endpoint, StringComparer.Ordinal))
                .ToList();
            await _repo.SaveDeviceTokensJsonAsync(
                tenantId,
                target.AccountId,
                SerializeSubscriptions(remaining),
                cancellationToken);
        }

        if (delivered)
        {
            await _repo.InsertNotificationAsync(
                tenantId,
                customerId,
                title,
                preview,
                new { channel = "app_push", type = "staff_chat_reply" },
                "chat",
                "/chat",
                cancellationToken);
        }
    }

    public Task SendDraftOrderPushAsync(
        Guid tenantId,
        Guid customerId,
        string draftNumber,
        decimal totalAmount,
        CancellationToken cancellationToken = default) =>
        SendDraftOrderPushInternalAsync(tenantId, customerId, draftNumber, totalAmount, cancellationToken);

    public Task<bool> TryDispatchPushToCustomerAsync(
        Guid tenantId,
        Guid customerId,
        string title,
        string body,
        string category,
        string? href,
        CancellationToken cancellationToken = default) =>
        TryDispatchPushOnlyAsync(tenantId, customerId, title, body, category, href, cancellationToken);

    private async Task<bool> TryDispatchPushOnlyAsync(
        Guid tenantId,
        Guid customerId,
        string title,
        string body,
        string category,
        string? href,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled
            || string.IsNullOrWhiteSpace(_options.PublicKey)
            || string.IsNullOrWhiteSpace(_options.PrivateKey))
        {
            return false;
        }

        var target = await _repo.GetPushTargetByCustomerAsync(tenantId, customerId, cancellationToken);
        if (target is null)
            return false;

        var subscriptions = ParseSubscriptions(target.DeviceTokensJson);
        if (subscriptions.Count == 0)
            return false;

        var payload = JsonSerializer.Serialize(new
        {
            title,
            body,
            data = new { url = href ?? "/", category },
        });

        var staleEndpoints = new List<string>();
        var delivered = false;
        foreach (var subscription in subscriptions)
        {
            try
            {
                await SendPushAsync(subscription, payload, cancellationToken);
                delivered = true;
            }
            catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone
                                              || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                staleEndpoints.Add(subscription.Endpoint);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Queued push failed for customer {CustomerId}", customerId);
            }
        }

        if (staleEndpoints.Count > 0)
        {
            var remaining = subscriptions
                .Where(s => !staleEndpoints.Contains(s.Endpoint, StringComparer.Ordinal))
                .ToList();
            await _repo.SaveDeviceTokensJsonAsync(
                tenantId,
                target.AccountId,
                SerializeSubscriptions(remaining),
                cancellationToken);
        }

        return delivered;
    }

    private async Task SendDraftOrderPushInternalAsync(
        Guid tenantId,
        Guid customerId,
        string draftNumber,
        decimal totalAmount,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled
            || string.IsNullOrWhiteSpace(_options.PublicKey)
            || string.IsNullOrWhiteSpace(_options.PrivateKey))
        {
            return;
        }

        if (!await _consents.HasGrantedConsentAsync(
                tenantId,
                customerId,
                CustomerConsentChannels.AppPush,
                CustomerConsentPurposes.CareReminder,
                cancellationToken)
            && !await _consents.HasGrantedConsentAsync(
                tenantId,
                customerId,
                CustomerConsentChannels.InApp,
                CustomerConsentPurposes.AiAssist,
                cancellationToken))
        {
            return;
        }

        var target = await _repo.GetPushTargetByCustomerAsync(tenantId, customerId, cancellationToken);
        if (target is null)
            return;

        var subscriptions = ParseSubscriptions(target.DeviceTokensJson);
        if (subscriptions.Count == 0)
            return;

        var locale = await _notifyText.ResolveLocaleAsync(tenantId, customerId, cancellationToken);
        var totalLabel = locale.Equals("en-US", StringComparison.OrdinalIgnoreCase)
            ? totalAmount.ToString("N0")
            : $"{totalAmount:N0}đ";
        var title = await _notifyText.FormatAsync(
            tenantId,
            locale,
            CustomerNotificationTextKeys.DraftOrderTitle,
            cancellationToken: cancellationToken);
        var body = await _notifyText.FormatAsync(
            tenantId,
            locale,
            CustomerNotificationTextKeys.DraftOrderBody,
            new Dictionary<string, string>
            {
                ["draftNumber"] = draftNumber,
                ["total"] = totalLabel,
            },
            cancellationToken);
        var payload = JsonSerializer.Serialize(new
        {
            title,
            body,
            data = new
            {
                type = "customer_draft_order",
                url = "/orders",
            },
        });

        var staleEndpoints = new List<string>();
        var delivered = false;
        foreach (var subscription in subscriptions)
        {
            try
            {
                await SendPushAsync(subscription, payload, cancellationToken);
                delivered = true;
            }
            catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone
                                              || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                staleEndpoints.Add(subscription.Endpoint);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Draft order push failed for customer {CustomerId}", customerId);
            }
        }

        if (staleEndpoints.Count > 0)
        {
            var remaining = subscriptions
                .Where(s => !staleEndpoints.Contains(s.Endpoint, StringComparer.Ordinal))
                .ToList();
            await _repo.SaveDeviceTokensJsonAsync(
                tenantId,
                target.AccountId,
                SerializeSubscriptions(remaining),
                cancellationToken);
        }

        if (delivered)
        {
            await _repo.InsertNotificationAsync(
                tenantId,
                customerId,
                title,
                body,
                new { channel = "app_push", type = "customer_draft_order", draftNumber },
                "order",
                "/orders",
                cancellationToken);
        }
    }

    public async Task NotifyConnectRxReadyAsync(
        Guid pharmacyTenantId,
        Guid customerId,
        string pharmacyDisplayName,
        string? summary,
        CancellationToken cancellationToken = default)
    {
        var locale = await _notifyText.ResolveLocaleAsync(pharmacyTenantId, customerId, cancellationToken);
        var pharmacy = string.IsNullOrWhiteSpace(pharmacyDisplayName) ? "nhà thuốc" : pharmacyDisplayName.Trim();
        var title = await _notifyText.FormatAsync(
            pharmacyTenantId,
            locale,
            CustomerNotificationTextKeys.ConnectRxReadyTitle,
            cancellationToken: cancellationToken);
        var body = await _notifyText.FormatAsync(
            pharmacyTenantId,
            locale,
            CustomerNotificationTextKeys.ConnectRxReadyBody,
            new Dictionary<string, string>
            {
                ["pharmacy"] = pharmacy,
                ["detail"] = string.IsNullOrWhiteSpace(summary) ? "" : $" {summary.Trim()}",
            },
            cancellationToken);

        await _repo.InsertNotificationAsync(
            pharmacyTenantId,
            customerId,
            title,
            body,
            new { channel = "connect", type = "rx_ready", pharmacy },
            "connect",
            "/",
            cancellationToken);

        if (!_options.Enabled
            || string.IsNullOrWhiteSpace(_options.PublicKey)
            || string.IsNullOrWhiteSpace(_options.PrivateKey))
        {
            return;
        }

        try
        {
            await TryDispatchPushOnlyAsync(
                pharmacyTenantId,
                customerId,
                title,
                body,
                "connect",
                "/",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Connect rx_ready push failed for customer {CustomerId}", customerId);
        }
    }

    private async Task AdvanceReminderScheduleAsync(DueReminderPushRow row, CancellationToken cancellationToken)
    {
        var next = ReminderScheduleHelper.ComputeNextRemindAt(
            row.RemindTime.ToTimeSpan(),
            row.DaysOfWeek.Select(d => (int)d).ToArray(),
            DateTimeOffset.UtcNow);
        await _repo.UpdateReminderNextAtAsync(row.TenantId, row.CustomerId, row.ReminderId, next, cancellationToken);
    }

    private async Task RemoveStaleSubscriptionsAsync(
        DueReminderPushRow row,
        IReadOnlyList<string> staleEndpoints,
        CancellationToken cancellationToken)
    {
        var subscriptions = ParseSubscriptions(row.DeviceTokensJson)
            .Where(s => !staleEndpoints.Contains(s.Endpoint, StringComparer.Ordinal))
            .ToList();
        await _repo.SaveDeviceTokensJsonAsync(
            row.TenantId,
            row.AccountId,
            SerializeSubscriptions(subscriptions),
            cancellationToken);
    }

    private Task SendPushAsync(
        StoredPushSubscription subscription,
        string payload,
        CancellationToken cancellationToken)
    {
        var pushSubscription = new PushSubscription(subscription.Endpoint, subscription.P256dh, subscription.Auth);
        var vapid = new VapidDetails(_options.Subject, _options.PublicKey, _options.PrivateKey);
        var client = new WebPushClient();
        return client.SendNotificationAsync(pushSubscription, payload, vapid, cancellationToken);
    }

    private static List<StoredPushSubscription> ParseSubscriptions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return [];

            var result = new List<StoredPushSubscription>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var endpoint = item.TryGetProperty("endpoint", out var endpointEl)
                    ? endpointEl.GetString()
                    : item.TryGetProperty("Endpoint", out var endpointAlt)
                        ? endpointAlt.GetString()
                        : null;
                var p256dh = item.TryGetProperty("p256dh", out var p256dhEl)
                    ? p256dhEl.GetString()
                    : item.TryGetProperty("P256dh", out var p256dhAlt)
                        ? p256dhAlt.GetString()
                        : null;
                var auth = item.TryGetProperty("auth", out var authEl)
                    ? authEl.GetString()
                    : item.TryGetProperty("Auth", out var authAlt)
                        ? authAlt.GetString()
                        : null;

                if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(p256dh) || string.IsNullOrWhiteSpace(auth))
                    continue;

                result.Add(new StoredPushSubscription(endpoint, p256dh, auth, null));
            }

            return result;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string SerializeSubscriptions(IReadOnlyList<StoredPushSubscription> subscriptions) =>
        JsonSerializer.Serialize(subscriptions.Select(s => new
        {
            endpoint = s.Endpoint,
            p256dh = s.P256dh,
            auth = s.Auth,
            registeredAt = (s.RegisteredAt ?? DateTimeOffset.UtcNow).UtcDateTime,
        }));
}
