using KitPlatform.Packs.Care;

namespace KitPlatform.Packs.Care.Infrastructure;

internal sealed class CareOsOverviewService : ICareOsOverviewService
{
    public Task<CareOsOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var dto = new CareOsOverviewDto(
            CarePackDefinition.PackCode,
            CarePackDefinition.DisplayName,
            Phase: CarePackDefinition.SolutionPhase,
            Tagline: "Instrument first — productize Community Health when data exists.",
            Purpose:
            "Định hình data plane Smart Care → Community Health: care_event, cohort, KPI hooks. Không phải sản phẩm outcome cộng đồng đang live.",
            LegalBoundary:
            "Không thay bác sĩ · không khám · không điều trị · không chẩn đoán AI. Care OS hỗ trợ vận hành theo dõi/nhắc/chuyển tuyến/đồng hành.",
            EnabledCapabilities:
            [
                "care_event_append",
                "care_event_list",
                "cohort_catalog",
                "cohort_membership_manual",
                "kpi_catalog",
                "overview_api",
                "module_gate_care_os",
            ],
            ExplicitNonGoals:
            [
                "live_community_outcome_dashboard",
                "auto_cohort_rules_engine",
                "kpi_compute_worker",
                "replace_customer_app_care_tables",
                "clinical_diagnosis",
                "prescribe",
                "public_community_health_claims",
            ],
            Readiness:
            [
                new(
                    "care_event_store",
                    "Care event append/list",
                    "instrumented",
                    "Có thể ghi manual ngay. Dual-write từ CustomerApp/Connect/Success/Learning: CHƯA.",
                    "Chạy ngay (shaping/manual). Production adapters: khi wire từng nguồn.",
                    null),
                new(
                    "cohort_manual",
                    "Gán cohort thủ công",
                    "instrumented",
                    "Cần customer_id hợp lệ trong tenant.",
                    "Chạy ngay sau khi enable module care_os + có khách.",
                    null),
                new(
                    "cohort_auto_rules",
                    "Gán cohort tự động (HTA/ĐTĐ…)",
                    "not_started",
                    "Cần rule engine + tín hiệu Rx/counseling/adherence.",
                    "Sau khi có rules + đủ tín hiệu (ước lượng ≥1 quý dữ liệu bán/care).",
                    "Chưa triển khai rule engine / job gán cohort."),
                new(
                    "kpi_catalog",
                    "Danh mục 7 KPI (NVX-CMP-05)",
                    "design",
                    "Chỉ metadata; chưa có số liệu aggregate.",
                    "Catalog chạy ngay. Giá trị KPI: khi snapshot worker + đủ event.",
                    null),
                new(
                    "kpi_snapshots",
                    "care_metric_snapshot compute",
                    "not_started",
                    "Cần volume care_event + membership theo compute_hints.",
                    "Khi worker aggregate được viết và KPI status → live.",
                    "Bảng đã reserve; không có worker/job."),
                new(
                    "connect_dual_write",
                    "Referral/booking → care_event",
                    "not_started",
                    "Cần pack_connect.referrals/bookings active trên tenant.",
                    "Sau khi wire handler IPlatformEvent / service hook.",
                    "Chưa dual-write từ Connect."),
                new(
                    "customer_app_dual_write",
                    "Reminders/adherence/repurchase → care_event",
                    "not_started",
                    "Cần traffic CustomerApp care features.",
                    "Sau khi adapter ghi event khi tạo/cập nhật bản ghi public.*",
                    "Channel tables vẫn là source; Care OS chưa subscribe."),
                new(
                    "community_health_ui",
                    "Admin UI Community Health",
                    "not_started",
                    "Cần KPI live + narrative GTM.",
                    "Sau khi ≥1–2 KPI instrumented có snapshot thật.",
                    "Cố ý chưa làm UI — giai đoạn định hình lõi API/schema."),
            ]);

        return Task.FromResult(dto);
    }
}
