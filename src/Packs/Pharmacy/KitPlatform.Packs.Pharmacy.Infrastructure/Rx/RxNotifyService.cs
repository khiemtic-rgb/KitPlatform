using KitPlatform.Application.Configuration;
using KitPlatform.Application.CustomerApp;
using KitPlatform.Packs.Pharmacy.Rx;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KitPlatform.Packs.Pharmacy.Infrastructure;

internal sealed class RxNotifyService : IRxNotifyService
{
    private readonly ISmsTextSender _sms;
    private readonly PlatformSettings _platform;
    private readonly ILogger<RxNotifyService> _logger;

    public RxNotifyService(
        ISmsTextSender sms,
        IOptions<PlatformSettings> platform,
        ILogger<RxNotifyService> logger)
    {
        _sms = sms;
        _platform = platform.Value;
        _logger = logger;
    }

    public async Task NotifyPrescriberInviteAsync(
        string phone,
        string tenantCode,
        string tenantName,
        CancellationToken cancellationToken = default)
    {
        var portal = (_platform.PrescriberPortalUrl ?? "https://prescriber.novixa.vn").TrimEnd('/');
        var message =
            $"Novixa: {tenantName} moi ban lien ket ke don. Mo {portal} de chap nhan.";
        await TrySendAsync(phone, tenantCode, message, "invite", cancellationToken);
    }

    public async Task NotifyPrescriberLinkApprovedAsync(
        string phone,
        string tenantCode,
        string tenantName,
        CancellationToken cancellationToken = default)
    {
        var portal = (_platform.PrescriberPortalUrl ?? "https://prescriber.novixa.vn").TrimEnd('/');
        var message =
            $"Novixa: {tenantName} da duyet lien ket. Ban co the ke don tai {portal}";
        await TrySendAsync(phone, tenantCode, message, "link_approved", cancellationToken);
    }

    public Task NotifyPharmacyLinkRequestAsync(
        string tenantCode,
        string tenantName,
        string prescriberName,
        string? prescriberPhone,
        CancellationToken cancellationToken = default)
    {
        // In-app surface = B5 dashboard pendingLinkApprovals. Log for ops / future staff SMS.
        _logger.LogInformation(
            "Rx notify (pharmacy in-app): BS {Prescriber} ({Phone}) xin lien ket NT {Tenant} ({Code}) — mo {Admin}/rx/prescriber-links",
            prescriberName,
            prescriberPhone ?? "—",
            tenantName,
            tenantCode,
            (_platform.AdminUrl ?? "https://admin.novixa.vn").TrimEnd('/'));
        return Task.CompletedTask;
    }

    public Task NotifyPharmacyNewPrescriptionAsync(
        string tenantCode,
        string tenantName,
        string prescriptionCode,
        Guid prescriptionId,
        string? patientName,
        string? prescriberName,
        CancellationToken cancellationToken = default)
    {
        var admin = (_platform.AdminUrl ?? "https://admin.novixa.vn").TrimEnd('/');
        var deepLink = $"{admin}/rx/prescriptions?rx={prescriptionId}";
        _logger.LogInformation(
            "Rx notify (pharmacy in-app): Don moi {Code} NT {Tenant} BS {Prescriber} BN {Patient} — {Link}",
            prescriptionCode,
            tenantName,
            prescriberName ?? "—",
            patientName ?? "—",
            deepLink);
        return Task.CompletedTask;
    }

    private async Task TrySendAsync(
        string phone,
        string tenantCode,
        string message,
        string kind,
        CancellationToken cancellationToken)
    {
        var normalized = new string(phone.Where(char.IsDigit).ToArray());
        if (normalized.Length < 9)
        {
            _logger.LogWarning("Rx SMS {Kind} skipped — invalid phone", kind);
            return;
        }

        try
        {
            await _sms.SendTextAsync(normalized, tenantCode, message, cancellationToken);
        }
        catch (Exception ex)
        {
            // Never fail business flow on notify.
            _logger.LogWarning(ex, "Rx SMS {Kind} failed for {Phone}", kind, normalized);
        }
    }
}
