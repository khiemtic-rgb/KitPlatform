using Dapper;
using KitPlatform.Application.Configuration;
using KitPlatform.Application.Core;
using KitPlatform.Application.CustomerApp;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Infrastructure.CustomerApp;

internal sealed class CustomerAppConnectService : ICustomerAppConnectService
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantPlatformSettings _platform;

    public CustomerAppConnectService(IDbConnectionFactory db, ITenantPlatformSettings platform)
    {
        _db = db;
        _platform = platform;
    }

    public async Task<CustomerConnectInboxDto> GetInboxAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        if (!await _platform.IsModuleEnabledAsync(PlatformModuleCodes.NovixaConnect, cancellationToken))
            return new CustomerConnectInboxDto(false, Array.Empty<CustomerConnectInboxItemDto>());

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        const string phoneSql = """
            SELECT phone
            FROM public.customers
            WHERE id = @CustomerId
              AND tenant_id = @TenantId
              AND deleted_at IS NULL
            LIMIT 1
            """;
        var phone = await conn.QuerySingleOrDefaultAsync<string?>(
            new CommandDefinition(
                phoneSql,
                new { TenantId = tenantId, CustomerId = customerId },
                cancellationToken: cancellationToken));

        var phoneDigits = DigitsOnly(phone);

        var items = new List<CustomerConnectInboxItemDto>();

        const string referralSql = """
            SELECT
                r.id AS Id,
                r.referral_status AS ReferralStatus,
                r.reason AS Reason,
                r.created_at AS CreatedAt,
                r.responded_at AS RespondedAt,
                ct.tenant_name AS ClinicName
            FROM pack_connect.referrals r
            INNER JOIN public.tenants ct ON ct.id = r.clinic_tenant_id
            WHERE r.pharmacy_tenant_id = @TenantId
              AND r.pharmacy_customer_id = @CustomerId
              AND r.referral_status IN ('pending_clinic_accept', 'accepted')
            ORDER BY r.created_at DESC
            LIMIT 20
            """;
        var referrals = await conn.QueryAsync<ReferralRow>(
            new CommandDefinition(
                referralSql,
                new { TenantId = tenantId, CustomerId = customerId },
                cancellationToken: cancellationToken));

        foreach (var row in referrals)
        {
            var pending = string.Equals(
                row.ReferralStatus,
                "pending_clinic_accept",
                StringComparison.OrdinalIgnoreCase);
            items.Add(
                new CustomerConnectInboxItemDto(
                    pending
                        ? CustomerConnectItemKinds.ReferralPending
                        : CustomerConnectItemKinds.ReferralAccepted,
                    row.Id,
                    string.IsNullOrWhiteSpace(row.Reason) ? null : row.Reason.Trim(),
                    row.ClinicName,
                    pending ? row.CreatedAt : row.RespondedAt ?? row.CreatedAt,
                    ScheduledAt: null,
                    PrescriptionCode: null,
                    pending ? "wait" : "info"));
        }

        const string bookingSql = """
            SELECT
                b.id AS Id,
                b.booking_status AS BookingStatus,
                b.scheduled_at AS ScheduledAt,
                b.created_at AS CreatedAt,
                b.notes AS Notes,
                ct.tenant_name AS ClinicName
            FROM pack_connect.bookings b
            INNER JOIN public.tenants ct ON ct.id = b.clinic_tenant_id
            WHERE b.pharmacy_tenant_id = @TenantId
              AND b.pharmacy_customer_id = @CustomerId
              AND b.booking_status IN ('proposed', 'confirmed')
              AND b.scheduled_at >= NOW() - INTERVAL '1 day'
            ORDER BY b.scheduled_at ASC
            LIMIT 20
            """;
        var bookings = await conn.QueryAsync<BookingRow>(
            new CommandDefinition(
                bookingSql,
                new { TenantId = tenantId, CustomerId = customerId },
                cancellationToken: cancellationToken));

        foreach (var row in bookings)
        {
            var proposed = string.Equals(row.BookingStatus, "proposed", StringComparison.OrdinalIgnoreCase);
            items.Add(
                new CustomerConnectInboxItemDto(
                    proposed
                        ? CustomerConnectItemKinds.BookingProposed
                        : CustomerConnectItemKinds.BookingConfirmed,
                    row.Id,
                    string.IsNullOrWhiteSpace(row.Notes) ? null : row.Notes.Trim(),
                    row.ClinicName,
                    row.CreatedAt,
                    row.ScheduledAt,
                    PrescriptionCode: null,
                    "info"));
        }

        if (!string.IsNullOrEmpty(phoneDigits) && phoneDigits.Length >= 8)
        {
            const string handoffSql = """
                SELECT
                    h.id AS Id,
                    h.prescription_code AS PrescriptionCode,
                    h.created_at AS CreatedAt,
                    h.provider_display_name AS ProviderDisplayName,
                    ct.tenant_name AS ClinicName
                FROM pack_connect.rx_handoffs h
                INNER JOIN public.tenants ct ON ct.id = h.clinic_tenant_id
                INNER JOIN public.customers c
                    ON c.id = @CustomerId
                   AND c.tenant_id = @TenantId
                   AND c.deleted_at IS NULL
                WHERE h.pharmacy_tenant_id = @TenantId
                  AND h.handoff_status = 'pending_pharmacy'
                  AND regexp_replace(COALESCE(h.patient_phone, ''), '\D', '', 'g')
                      = regexp_replace(COALESCE(c.phone, ''), '\D', '', 'g')
                ORDER BY h.created_at DESC
                LIMIT 20
                """;
            var handoffs = await conn.QueryAsync<HandoffRow>(
                new CommandDefinition(
                    handoffSql,
                    new { TenantId = tenantId, CustomerId = customerId },
                    cancellationToken: cancellationToken));

            foreach (var row in handoffs)
            {
                var detailBits = new List<string>();
                if (!string.IsNullOrWhiteSpace(row.PrescriptionCode))
                    detailBits.Add(row.PrescriptionCode.Trim());
                if (!string.IsNullOrWhiteSpace(row.ProviderDisplayName))
                    detailBits.Add(row.ProviderDisplayName.Trim());

                items.Add(
                    new CustomerConnectInboxItemDto(
                        CustomerConnectItemKinds.RxReady,
                        row.Id,
                        detailBits.Count == 0 ? null : string.Join(" · ", detailBits),
                        row.ClinicName,
                        row.CreatedAt,
                        ScheduledAt: null,
                        row.PrescriptionCode,
                        "pickup"));
            }
        }

        var ordered = items
            .OrderByDescending(i => i.Kind == CustomerConnectItemKinds.RxReady)
            .ThenBy(i => i.ScheduledAt ?? DateTime.MaxValue)
            .ThenByDescending(i => i.OccurredAt ?? DateTime.MinValue)
            .Take(30)
            .ToList();

        return new CustomerConnectInboxDto(true, ordered);
    }

    private static string DigitsOnly(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return string.Empty;
        return new string(phone.Where(char.IsDigit).ToArray());
    }

    private sealed class ReferralRow
    {
        public Guid Id { get; init; }
        public string ReferralStatus { get; init; } = "";
        public string? Reason { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? RespondedAt { get; init; }
        public string? ClinicName { get; init; }
    }

    private sealed class BookingRow
    {
        public Guid Id { get; init; }
        public string BookingStatus { get; init; } = "";
        public DateTime ScheduledAt { get; init; }
        public DateTime CreatedAt { get; init; }
        public string? Notes { get; init; }
        public string? ClinicName { get; init; }
    }

    private sealed class HandoffRow
    {
        public Guid Id { get; init; }
        public string? PrescriptionCode { get; init; }
        public DateTime CreatedAt { get; init; }
        public string? ProviderDisplayName { get; init; }
        public string? ClinicName { get; init; }
    }
}
