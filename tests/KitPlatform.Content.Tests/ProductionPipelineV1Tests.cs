using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionPipelineV1Tests
{
    [Fact]
    public void Visual_continuity_keeps_character_count_inside_runway_prompt()
    {
        var fail = ProductionVisualContinuityV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Runway_task_response_keeps_provider_code_apart_from_internal_label()
    {
        var fail = ProductionRunwayTaskResponseV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Runway_prompt_cap_applies_after_continuity_and_keeps_character_json()
    {
        var fail = ProductionRunwayPromptCapV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Work_execution_T01_through_T24_still_pass()
    {
        var fail = ProductionWorkExecutionV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Pipeline_graph_and_create_video_fixture_slice()
    {
        var fail = ProductionPipelineV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Create_video_cutover_fixture_service()
    {
        var fail = ProductionCreateVideoV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Create_video_action_contract_enforcement_T01_through_T11()
    {
        var fail = ProductionCreateVideoActionContractV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Scope_anchor_checkpoint_1_film_identity_unchanged()
    {
        var fail = ProductionScopeAnchorV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Test_scope_foundation_checkpoint_2_T01_through_T16()
    {
        var fail = ProductionTestScopeFoundationV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Test_fixture_keyframe_checkpoint_3a_T01_through_T24()
    {
        var fail = ProductionTestKeyframeV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Test_create_video_e2e_checkpoint_3b_T01_through_T29()
    {
        var fail = ProductionTestCreateVideoV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Playable_fixture_mp4_is_deterministic_and_coherent()
    {
        var fail = ProductionTestMp4PlayableV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Real_provider_bridge_v1_T01_through_T56()
    {
        var fail = ProductionRunwayBridgeV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Paid_network_permit_is_one_shot_and_test_scoped()
    {
        var fail = ProductionPaidNetworkPermitV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Complete_action_authority_v1_fixture_chain()
    {
        var fail = ProductionCompleteActionAuthorityV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Shared_performance_contract_v1_phase_a_T01_through_T25()
    {
        var fail = PerformanceContractV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Performance_snapshot_binding_v1_T01_through_T22()
    {
        var fail = PerformanceSnapshotBindingV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Performance_consumption_cutover_v1_T01_through_T32()
    {
        var fail = PerformanceConsumptionCutoverV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Existing_video_take_ingest_v1()
    {
        var fail = ProductionExistingVideoTakeIngestV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Existing_voice_ingest_v1()
    {
        var fail = ProductionExistingVoiceIngestV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Tenant_ep01_lipsync_v1()
    {
        var fail = ProductionTenantEp01LipsyncV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Tenant_ep01_mix_v1()
    {
        var fail = ProductionTenantEp01MixV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Tenant_ep01_production_path_v1()
    {
        var fail = ProductionTenantEp01ProductionPathV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Ai_selection_supersede_v1()
    {
        var fail = ProductionAiSelectionSupersedeV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Pilot_sh02_local_readiness_v1()
    {
        var fail = ProductionPilotSh02ReadinessV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Gemini_31_image_readiness_v1()
    {
        var fail = ProductionGemini31ImageReadinessV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Tenant_ep01_assemble_v1()
    {
        var fail = ProductionTenantEp01AssembleV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Tenant_workspace_video_take_authority_v1()
    {
        var fail = ProductionTenantWorkspaceAuthorityV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Performance_snapshot_live_cutover_v1_T01_through_T30()
    {
        var fail = PerformanceSnapshotLiveCutoverV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Series_build_catalog_marks_null_tenant_read_only()
    {
        Assert.Equal(SeriesBuildCatalogOwnership.Unattributed, SeriesBuildCatalogOwnership.FromTenant(null));
        Assert.Equal(SeriesBuildCatalogOwnership.Unattributed, SeriesBuildCatalogOwnership.FromTenant(Guid.Empty));
        Assert.True(SeriesBuildCatalogOwnership.IsUnattributed(null));
        Assert.Equal(
            SeriesBuildCatalogOwnership.Tenant,
            SeriesBuildCatalogOwnership.FromTenant(Guid.Parse("11111111-1111-1111-1111-111111111105")));
    }

    [Fact]
    public void Tenant_ep01_video_gate_is_not_a_missing_snapshot()
    {
        var build = Guid.Parse("30ba4061-a443-4c0d-85aa-1d13ff4fdedb");
        var episode = new ProductionScope(build, "EP01", "SC01", "EP01-SC01-SH01");
        var omitted = new ProductionScope(build, "", "SC01", "EP01-SC01-SH01");
        Assert.False(ProductionPerformanceConsumption.ActionRequiresSnapshot(ProductionActions.CreateVideo, episode));
        Assert.True(ProductionPerformanceConsumption.ActionRequiresSnapshot(ProductionActions.CreateVideo, omitted));
    }

    [Fact]
    public void Novixa_short_strategy_gates_paid_motion_without_calling_providers()
    {
        var fail = NovixaShortProductionStrategyV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Novixa_short_static_voice_over_reaches_final_without_paid_motion()
    {
        var fail = NovixaShortEndToEndV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Novixa_voice_bytes_import_binds_current_voice_without_accepting()
    {
        var fail = NovixaVoiceBytesImportV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Novixa_series_does_not_inherit_famixa_shot_character_canon()
    {
        Assert.False(ProductionLockedStillReference.InheritFamixaShotCanon("NOVIXA_S001"));
        Assert.True(ProductionLockedStillReference.InheritFamixaShotCanon("FAMIXA"));
        Assert.True(ProductionLockedStillReference.InheritFamixaShotCanon(null));
        var novixa = ProductionLockedStillReference.Characters("novixa dashboard", shotCode: null);
        var famixa = ProductionLockedStillReference.Characters("", "EP01-SC01-SH01");
        Assert.Empty(novixa);
        Assert.Contains(famixa, row => row.Id == "CHAR-003");
    }

    [Fact]
    public void Voice_over_still_clock_lands_on_the_voice_duration()
    {
        var clock = FfmpegMixComposeGraph.StillClock(4.152m);
        Assert.Equal(125, clock.Fps);
        Assert.Equal(519, clock.Frames);
        Assert.Equal("4.152", (clock.Frames / (decimal)clock.Fps).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
    }
}
