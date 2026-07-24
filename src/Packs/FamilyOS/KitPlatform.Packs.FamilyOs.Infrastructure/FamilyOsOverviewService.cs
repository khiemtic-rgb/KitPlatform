using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyOsOverviewService : IFamilyOsOverviewService
{
    public Task<FamilyOsOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var dto = new FamilyOsOverviewDto(
            FamilyOsPackDefinition.PackCode,
            FamilyOsPackDefinition.DisplayName,
            Phase: "F2.5_accountability_lite",
            Tagline: "One Family. One Plan. One Daily Flow.",
            LegalBoundary:
            "FamilyOS giúp gia đình cùng thực hiện những cam kết đã thống nhất. AI hỗ trợ, không thay thế cha mẹ. Không giám sát trẻ, không hình phạt gây hại.",
            EnabledCapabilities:
            [
                "family_graph",
                "membership_without_account",
                "membership_admin",
                "routine",
                "commitment_template",
                "day_flow",
                "commitment_progress",
                "context_reminder",
                "family_agreement",
                "consequence_library",
                "reward_library",
                "accountability_options_config",
                "reflection_skip_reason",
                "consequence_pending_confirm",
                "streak_reward_lite",
                "overview_api",
                "family_coach_insight",
            ],
            ExplicitNonGoals:
            [
                "finance",
                "health_records",
                "medication",
                "continuous_gps",
                "school_lms",
                "smart_home",
                "child_surveillance",
                "replace_parenting",
                "harmful_punishment",
                "ai_environment_sensing",
                "accountability_engine_full",
                "freeform_llm_chat",
            ]);

        return Task.FromResult(dto);
    }
}
