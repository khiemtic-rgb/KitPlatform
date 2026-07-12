using KitPlatform.Packs.Connect;

namespace KitPlatform.Packs.Connect.Infrastructure;

internal sealed class ConnectOverviewService : IConnectOverviewService
{
    public Task<ConnectOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var dto = new ConnectOverviewDto(
            ConnectPackDefinition.PackCode,
            ConnectPackDefinition.DisplayName,
            Phase: "C5_status_sync",
            LegalBoundary:
            "Novixa Connect điều phối liên kết tổ chức; không khám chữa bệnh, không phát hành/ký số đơn thuốc.",
            EnabledCapabilities:
            [
                "workspace_shell",
                "overview_api",
                "admin_nav",
                "org_profile",
                "org_links",
                "partner_directory",
                "doctor_identity",
                "clinic_membership",
                "referral",
                "booking_stub",
                "booking_notify",
                "status_sync",
                "pharmacy_ready_inbox",
            ],
            ExplicitNonGoals:
            [
                "prescription_issuance",
                "digital_signature",
                "national_e_rx_sync",
                "telemedicine_provider",
                "his_emr_full",
            ]);

        return Task.FromResult(dto);
    }
}
