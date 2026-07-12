using Dapper;
using KitPlatform.Application.Configuration;
using KitPlatform.Application.CustomerApp;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Connect;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KitPlatform.Packs.Connect.Infrastructure;

internal sealed class ConnectNotifyService : IConnectNotifyService
{
    private readonly ISmsTextSender _sms;
    private readonly ICustomerPushService _push;
    private readonly IDbConnectionFactory _db;
    private readonly PlatformSettings _platform;
    private readonly ILogger<ConnectNotifyService> _logger;

    public ConnectNotifyService(
        ISmsTextSender sms,
        ICustomerPushService push,
        IDbConnectionFactory db,
        IOptions<PlatformSettings> platform,
        ILogger<ConnectNotifyService> logger)
    {
        _sms = sms;
        _push = push;
        _db = db;
        _platform = platform.Value;
        _logger = logger;
    }

    public async Task NotifyBookingProposedAsync(
        ConnectBookingDto booking,
        CancellationToken cancellationToken = default)
    {
        var admin = (_platform.AdminUrl ?? "https://admin.novixa.vn").TrimEnd('/');
        var when = booking.ScheduledAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        _logger.LogInformation(
            "Connect notify (booking proposed): {Patient} @ {When} Clinic {Clinic} — {Link}/connect/bookings",
            booking.PatientDisplayName,
            when,
            booking.ClinicTenantCode,
            admin);

        var message =
            $"Novixa Connect: lich hen de xuat {when} tai {booking.ClinicTenantName}. Lien he phong kham de xac nhan.";
        await TrySmsAsync(booking.PatientPhone, booking.ClinicTenantCode, message, "booking_proposed", cancellationToken);
    }

    public async Task NotifyBookingConfirmedAsync(
        ConnectBookingDto booking,
        CancellationToken cancellationToken = default)
    {
        var admin = (_platform.AdminUrl ?? "https://admin.novixa.vn").TrimEnd('/');
        var when = booking.ScheduledAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        _logger.LogInformation(
            "Connect notify (booking confirmed): {Patient} @ {When} Clinic {Clinic} — {Link}/connect/bookings",
            booking.PatientDisplayName,
            when,
            booking.ClinicTenantCode,
            admin);

        var message =
            $"Novixa Connect: lich hen {when} tai {booking.ClinicTenantName} da duoc xac nhan.";
        await TrySmsAsync(booking.PatientPhone, booking.ClinicTenantCode, message, "booking_confirmed", cancellationToken);
    }

    public async Task NotifyReadyToDispenseAsync(
        ConnectStatusEventDto statusEvent,
        CancellationToken cancellationToken = default)
    {
        var admin = (_platform.AdminUrl ?? "https://admin.novixa.vn").TrimEnd('/');
        _logger.LogInformation(
            "Connect notify (ready_to_dispense): {Patient} Clinic {Clinic} → Pharmacy {Pharmacy} — {Link}/connect/status",
            statusEvent.PatientDisplayName ?? "(no name)",
            statusEvent.ClinicTenantCode,
            statusEvent.PharmacyTenantCode,
            admin);

        var pharmacy = string.IsNullOrWhiteSpace(statusEvent.PharmacyTenantName)
            ? statusEvent.PharmacyTenantCode
            : statusEvent.PharmacyTenantName;
        var message =
            $"Novixa Connect: san sang lay thuoc tai {pharmacy}. Vui long den nha thuoc (hoac lien he NT).";
        await TrySmsAsync(
            statusEvent.PatientPhone,
            statusEvent.PharmacyTenantCode,
            message,
            "ready_to_dispense",
            cancellationToken);

        await TryCustomerAppNotifyAsync(statusEvent, pharmacy, cancellationToken);
    }

    private async Task TryCustomerAppNotifyAsync(
        ConnectStatusEventDto statusEvent,
        string pharmacyDisplayName,
        CancellationToken cancellationToken)
    {
        try
        {
            var customerId = await ResolvePharmacyCustomerIdAsync(
                statusEvent.PharmacyTenantId,
                statusEvent.PatientPhone,
                cancellationToken);
            if (customerId is null)
            {
                _logger.LogInformation(
                    "Connect rx_ready app notify skipped — no pharmacy CRM match for phone");
                return;
            }

            var detail = string.IsNullOrWhiteSpace(statusEvent.Summary)
                ? null
                : statusEvent.Summary.Trim();

            await _push.NotifyConnectRxReadyAsync(
                statusEvent.PharmacyTenantId,
                customerId.Value,
                pharmacyDisplayName,
                detail,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Connect rx_ready app notify failed");
        }
    }

    private async Task<Guid?> ResolvePharmacyCustomerIdAsync(
        Guid pharmacyTenantId,
        string? patientPhone,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(patientPhone))
            return null;

        var digits = new string(patientPhone.Where(char.IsDigit).ToArray());
        if (digits.Length < 8)
            return null;

        const string sql = """
            SELECT id
            FROM public.customers
            WHERE tenant_id = @TenantId
              AND deleted_at IS NULL
              AND regexp_replace(COALESCE(phone, ''), '\D', '', 'g') = @Digits
            ORDER BY updated_at DESC NULLS LAST, created_at DESC
            LIMIT 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(
                sql,
                new { TenantId = pharmacyTenantId, Digits = digits },
                cancellationToken: cancellationToken));
    }

    private async Task TrySmsAsync(
        string? phone,
        string tenantCode,
        string message,
        string kind,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return;

        var normalized = new string(phone.Where(char.IsDigit).ToArray());
        if (normalized.Length < 9)
        {
            _logger.LogWarning("Connect SMS {Kind} skipped — invalid phone", kind);
            return;
        }

        try
        {
            await _sms.SendTextAsync(normalized, tenantCode, message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Connect SMS {Kind} failed for {Phone}", kind, normalized);
        }
    }
}
