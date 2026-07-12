using Dapper;
using KitPlatform.Application.CustomerApp;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Infrastructure.CustomerApp;

internal sealed class CustomerNotificationTextService : ICustomerNotificationTextService
{
    private const string DefaultLocale = "vi-VN";
    private const string EnglishLocale = "en-US";

    private static readonly IReadOnlyDictionary<string, string> ViDefaults =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [CustomerNotificationTextKeys.MedicationReminderTitle] = "Nhắc uống thuốc",
            [CustomerNotificationTextKeys.MedicationReminderFamilyTitle] = "{name} đến giờ uống thuốc",
            [CustomerNotificationTextKeys.MedicationReminderBody] = "{productName}",
            [CustomerNotificationTextKeys.MedicationReminderBodyWithNote] = "{productName} — {dosageNote}",
            [CustomerNotificationTextKeys.CareAdvanceTitle] = "Nhắc trước: {title}",
            [CustomerNotificationTextKeys.CareAdvanceFamilyTitle] = "Nhắc trước cho {name}: {title}",
            [CustomerNotificationTextKeys.CareDueTitle] = "{title}",
            [CustomerNotificationTextKeys.CareDueFamilyTitle] = "{name} — {title}",
            [CustomerNotificationTextKeys.CareAdvanceBody] = "Lịch vào {when}.{note}",
            [CustomerNotificationTextKeys.CareDueBody] = "Đến giờ {when}.{note}",
            [CustomerNotificationTextKeys.CareScheduledTitle] = "Đã lên lịch nhắc",
            [CustomerNotificationTextKeys.CareScheduledBody] = "{title} — {when}",
            [CustomerNotificationTextKeys.RepurchaseTitle] = "Đơn thuốc sắp hết",
            [CustomerNotificationTextKeys.RepurchaseBody] = "{orderLabel} — dự kiến cần mua lại khoảng {dateLabel}.",
            [CustomerNotificationTextKeys.DateToday] = "hôm nay",
            [CustomerNotificationTextKeys.AdherenceMissedTitle] = "Bạn bỏ liều vài ngày gần đây",
            [CustomerNotificationTextKeys.AdherenceMissedBody] =
                "Đã {days} ngày không ghi nhận uống thuốc. Mở Nhắc thuốc để cập nhật.",
            [CustomerNotificationTextKeys.ChatStaffReplyTitle] = "Dược sĩ trả lời",
            [CustomerNotificationTextKeys.ChatStaffReplyNamedTitle] = "{name} trả lời",
            [CustomerNotificationTextKeys.DraftOrderTitle] = "Đơn thuốc tạm mới",
            [CustomerNotificationTextKeys.DraftOrderBody] =
                "{draftNumber} — tổng tạm tính {total}đ. Xem và xác nhận (tuỳ chọn) trên app.",
            [CustomerNotificationTextKeys.ConnectRxReadyTitle] = "Đơn sẵn sàng lấy thuốc",
            [CustomerNotificationTextKeys.ConnectRxReadyBody] =
                "Đơn phòng khám sẵn sàng tại {pharmacy}. Mở trang chủ app để xem chi tiết.{detail}",
        };

    private static readonly IReadOnlyDictionary<string, string> EnDefaults =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [CustomerNotificationTextKeys.MedicationReminderTitle] = "Medication reminder",
            [CustomerNotificationTextKeys.MedicationReminderFamilyTitle] = "{name} — time for medication",
            [CustomerNotificationTextKeys.MedicationReminderBody] = "{productName}",
            [CustomerNotificationTextKeys.MedicationReminderBodyWithNote] = "{productName} — {dosageNote}",
            [CustomerNotificationTextKeys.CareAdvanceTitle] = "Upcoming: {title}",
            [CustomerNotificationTextKeys.CareAdvanceFamilyTitle] = "Upcoming for {name}: {title}",
            [CustomerNotificationTextKeys.CareDueTitle] = "{title}",
            [CustomerNotificationTextKeys.CareDueFamilyTitle] = "{name} — {title}",
            [CustomerNotificationTextKeys.CareAdvanceBody] = "Scheduled for {when}.{note}",
            [CustomerNotificationTextKeys.CareDueBody] = "Due at {when}.{note}",
            [CustomerNotificationTextKeys.CareScheduledTitle] = "Reminder scheduled",
            [CustomerNotificationTextKeys.CareScheduledBody] = "{title} — {when}",
            [CustomerNotificationTextKeys.RepurchaseTitle] = "Running low on medication",
            [CustomerNotificationTextKeys.RepurchaseBody] = "{orderLabel} — refill expected around {dateLabel}.",
            [CustomerNotificationTextKeys.DateToday] = "today",
            [CustomerNotificationTextKeys.AdherenceMissedTitle] = "Missed doses recently",
            [CustomerNotificationTextKeys.AdherenceMissedBody] =
                "No doses logged for {days} days. Open Reminders to update.",
            [CustomerNotificationTextKeys.ChatStaffReplyTitle] = "Pharmacist replied",
            [CustomerNotificationTextKeys.ChatStaffReplyNamedTitle] = "{name} replied",
            [CustomerNotificationTextKeys.DraftOrderTitle] = "New draft order",
            [CustomerNotificationTextKeys.DraftOrderBody] =
                "{draftNumber} — subtotal {total}. Review and confirm in the app.",
            [CustomerNotificationTextKeys.ConnectRxReadyTitle] = "Prescription ready for pickup",
            [CustomerNotificationTextKeys.ConnectRxReadyBody] =
                "A clinic prescription is ready at {pharmacy}. Open Home in the app for details.{detail}",
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Defaults =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            [DefaultLocale] = ViDefaults,
            [EnglishLocale] = EnDefaults,
        };

    private readonly IDbConnectionFactory _db;

    public CustomerNotificationTextService(IDbConnectionFactory db) => _db = db;

    public async Task<string> ResolveLocaleAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                COALESCE(
                    NULLIF(TRIM(ca.preferred_locale), ''),
                    NULLIF(TRIM(t.settings #>> '{platform,i18n,customer_app_default_locale}'), ''),
                    @DefaultLocale
                ) AS Locale
            FROM customer_accounts ca
            INNER JOIN tenants t ON t.id = ca.tenant_id AND t.deleted_at IS NULL
            WHERE ca.tenant_id = @TenantId
              AND ca.customer_id = @CustomerId
              AND ca.status = 1
            ORDER BY ca.updated_at DESC
            LIMIT 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var locale = await conn.QuerySingleOrDefaultAsync<string?>(
            sql,
            new { TenantId = tenantId, CustomerId = customerId, DefaultLocale });

        return NormalizeLocale(locale);
    }

    public async Task<string> FormatAsync(
        Guid tenantId,
        string locale,
        string translationKey,
        IReadOnlyDictionary<string, string>? args = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(translationKey))
            return string.Empty;

        var effectiveLocale = NormalizeLocale(locale);
        var template = await LookupTemplateAsync(tenantId, effectiveLocale, translationKey.Trim(), cancellationToken)
            ?? LookupDefault(effectiveLocale, translationKey.Trim())
            ?? LookupDefault(DefaultLocale, translationKey.Trim())
            ?? translationKey;

        return ApplyArgs(template, args);
    }

    private async Task<string?> LookupTemplateAsync(
        Guid tenantId,
        string locale,
        string key,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT translated_value
            FROM tenant_string_translations
            WHERE tenant_id = @TenantId
              AND translation_key = @Key
              AND locale_code = @Locale
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<string?>(sql, new
        {
            TenantId = tenantId,
            Key = key,
            Locale = locale,
        });
    }

    private static string? LookupDefault(string locale, string key)
    {
        if (!Defaults.TryGetValue(locale, out var map))
            return null;
        return map.TryGetValue(key, out var value) ? value : null;
    }

    private static string NormalizeLocale(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
            return DefaultLocale;
        return locale.Trim().Equals(EnglishLocale, StringComparison.OrdinalIgnoreCase)
            ? EnglishLocale
            : DefaultLocale;
    }

    private static string ApplyArgs(string template, IReadOnlyDictionary<string, string>? args)
    {
        if (args is null || args.Count == 0)
            return template;

        var result = template;
        foreach (var (key, value) in args)
        {
            result = result.Replace($"{{{key}}}", value ?? string.Empty, StringComparison.Ordinal);
        }

        return result;
    }
}
