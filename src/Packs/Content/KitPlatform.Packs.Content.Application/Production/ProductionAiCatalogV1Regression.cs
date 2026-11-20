using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// AI-CATALOG-02: Production AI catalog normalization V1.
/// Verifies Motion, Voice, and LipSync catalogs expose consistent structured metadata.
/// </summary>
public static class ProductionAiCatalogV1Regression
{
    public const string SuiteId = "AI_CATALOG_02";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        // TEST 1-4: Models exist in catalogs
        Ok(ProductionMotionCatalog.TryGet("runway", "gen4_turbo", out var runway), "T1_runway_exists");
        Ok(ProductionMotionCatalog.TryGet("google", "veo-3.1", out var veo), "T2_google_veo_exists");
        Ok(ProductionVoiceCatalog.TryGet("elevenlabs", "eleven_v3", out var eleven), "T3_elevenlabs_exists");
        Ok(ProductionLipsyncCatalog.TryGet("fal", "sync-lipsync-v3", out var fal), "T4_fal_exists");

        // TEST 5: Runway metadata
        Ok(runway.ProviderId == "runway", "T5_runway_provider");
        Ok(runway.ModelId == "gen4_turbo", "T5_runway_model");
        Ok(runway.Capability == FamixaProviderCapability.Motion, "T5_runway_capability");
        Ok(runway.DisplayName == "Runway Gen-4 Turbo", "T5_runway_display_name");
        Ok(runway.Enabled, "T5_runway_enabled");
        Ok(runway.IsPaid, "T5_runway_is_paid");

        // TEST 6: Google Veo metadata
        Ok(veo.ProviderId == "google", "T6_veo_provider");
        Ok(veo.ModelId == "veo-3.1", "T6_veo_model");
        Ok(veo.Capability == FamixaProviderCapability.Motion, "T6_veo_capability");
        Ok(veo.DisplayName == "Google Veo 3.1", "T6_veo_display_name");
        Ok(veo.Enabled, "T6_veo_enabled");
        Ok(veo.IsPaid, "T6_veo_is_paid");

        // TEST 7: ElevenLabs metadata
        Ok(eleven.ProviderId == "elevenlabs", "T7_eleven_provider");
        Ok(eleven.ModelId == "eleven_v3", "T7_eleven_model");
        Ok(eleven.Capability == FamixaProviderCapability.Voice, "T7_eleven_capability");
        Ok(eleven.DisplayName == "ElevenLabs V3", "T7_eleven_display_name");
        Ok(eleven.Enabled, "T7_eleven_enabled");
        Ok(eleven.IsPaid, "T7_eleven_is_paid");

        // TEST 8: Fal Lipsync metadata
        Ok(fal.ProviderId == "fal", "T8_fal_provider");
        Ok(fal.ModelId == "sync-lipsync-v3", "T8_fal_model");
        Ok(fal.Capability == FamixaProviderCapability.LipSync, "T8_fal_capability");
        Ok(fal.DisplayName == "Fal Sync Lipsync V3", "T8_fal_display_name");
        Ok(fal.Enabled, "T8_fal_enabled");
        Ok(fal.IsPaid, "T8_fal_is_paid");

        // TEST 9: Unknown provider/model returns false
        Ok(!ProductionMotionCatalog.TryGet("unknown", "model", out _), "T9_unknown_motion");
        Ok(!ProductionVoiceCatalog.TryGet("unknown", "model", out _), "T9_unknown_voice");
        Ok(!ProductionLipsyncCatalog.TryGet("unknown", "model", out _), "T9_unknown_lipsync");
        Ok(!ProductionMotionCatalog.TryGet("runway", "unknown", out _), "T9_runway_unknown_model");
        Ok(!ProductionVoiceCatalog.TryGet("elevenlabs", "unknown", out _), "T9_eleven_unknown_model");
        Ok(!ProductionLipsyncCatalog.TryGet("fal", "unknown", out _), "T9_fal_unknown_model");

        // TEST 10: All collections contain expected real models
        Ok(ProductionMotionCatalog.All.Count >= 2, "T10_motion_has_real_models");
        Ok(ProductionMotionCatalog.All.Any(m => 
            m.ProviderId == "runway" && m.ModelId == "gen4_turbo"), "T10_motion_has_runway");
        Ok(ProductionMotionCatalog.All.Any(m => 
            m.ProviderId == "google" && m.ModelId == "veo-3.1"), "T10_motion_has_veo");

        Ok(ProductionVoiceCatalog.All.Count >= 1, "T10_voice_has_real_models");
        Ok(ProductionVoiceCatalog.All.Any(m => 
            m.ProviderId == "elevenlabs" && m.ModelId == "eleven_v3"), "T10_voice_has_eleven");

        Ok(ProductionLipsyncCatalog.All.Count >= 1, "T10_lipsync_has_real_models");
        Ok(ProductionLipsyncCatalog.All.Any(m => 
            m.ProviderId == "fal" && m.ModelId == "sync-lipsync-v3"), "T10_lipsync_has_fal");

        // TEST 11: Existing fixture identifiers remain present
        Ok(ProductionMotionCatalog.FixtureProviderId == "fixture", "T11_motion_fixture_provider");
        Ok(ProductionMotionCatalog.FixtureModelId == "fixture-motion-v1", "T11_motion_fixture_model");
        Ok(ProductionVoiceCatalog.FixtureProviderId == "fixture", "T11_voice_fixture_provider");
        Ok(ProductionVoiceCatalog.FixtureModelId == "fixture-voice-v1", "T11_voice_fixture_model");
        Ok(ProductionLipsyncCatalog.FixtureProviderId == "fixture", "T11_lipsync_fixture_provider");
        Ok(ProductionLipsyncCatalog.FixtureModelId == "fixture-lipsync-v1", "T11_lipsync_fixture_model");

        Ok(ProductionMotionCatalog.TryGet("fixture", "fixture-motion-v1", out _), "T11_motion_fixture_in_catalog");
        Ok(ProductionVoiceCatalog.TryGet("fixture", "fixture-voice-v1", out _), "T11_voice_fixture_in_catalog");
        Ok(ProductionLipsyncCatalog.TryGet("fixture", "fixture-lipsync-v1", out _), "T11_lipsync_fixture_in_catalog");

        // TEST 12: Existing ProductionMotionCatalog behavior remains intact
        Ok(ProductionMotionCatalog.RunwayProviderId == "runway", "T12_runway_provider_const");
        Ok(ProductionMotionCatalog.RunwayGen4Turbo == "gen4_turbo", "T12_runway_model_const");
        Ok(ProductionMotionCatalog.GoogleProviderId == "google", "T12_google_provider_const");
        Ok(ProductionMotionCatalog.Veo31ModelId == "veo-3.1", "T12_veo_model_const");
        Ok(ProductionMotionCatalog.RunwayGen4.ProviderId == "runway", "T12_runway_record_provider");
        Ok(ProductionMotionCatalog.GoogleVeo31.ProviderId == "google", "T12_veo_record_provider");
        Ok(ProductionMotionCatalog.ProviderExists("runway"), "T12_provider_exists_runway");
        Ok(ProductionMotionCatalog.ProviderExists("google"), "T12_provider_exists_google");
        Ok(!ProductionMotionCatalog.ProviderExists("unknown"), "T12_provider_exists_unknown");

        // TEST 13: No duplicate real provider/model pairs within each catalog
        var motionPairs = ProductionMotionCatalog.All
            .Where(m => m.ProviderId != "fixture")
            .Select(m => (m.ProviderId, m.ModelId))
            .ToList();
        Ok(motionPairs.Count == motionPairs.Distinct().Count(), "T13_motion_no_duplicates");

        var voicePairs = ProductionVoiceCatalog.All
            .Where(m => m.ProviderId != "fixture")
            .Select(m => (m.ProviderId, m.ModelId))
            .ToList();
        Ok(voicePairs.Count == voicePairs.Distinct().Count(), "T13_voice_no_duplicates");

        var lipsyncPairs = ProductionLipsyncCatalog.All
            .Where(m => m.ProviderId != "fixture")
            .Select(m => (m.ProviderId, m.ModelId))
            .ToList();
        Ok(lipsyncPairs.Count == lipsyncPairs.Distinct().Count(), "T13_lipsync_no_duplicates");

        // TEST 14: Case-insensitive lookup
        Ok(ProductionMotionCatalog.TryGet("RUNWAY", "GEN4_TURBO", out var runwayUpper), "T14_runway_case_insensitive");
        Ok(runwayUpper.ProviderId == "runway", "T14_runway_case_provider");
        Ok(ProductionVoiceCatalog.TryGet("ElevenLabs", "ELEVEN_V3", out var elevenUpper), "T14_eleven_case_insensitive");
        Ok(elevenUpper.ProviderId == "elevenlabs", "T14_eleven_case_provider");
        Ok(ProductionLipsyncCatalog.TryGet("FAL", "SYNC-LIPSYNC-V3", out var falUpper), "T14_fal_case_insensitive");
        Ok(falUpper.ProviderId == "fal", "T14_fal_case_provider");

        // TEST 15: Existing Voice and Lipsync constants remain unchanged
        Ok(ProductionVoiceCatalog.ElevenLabsProviderId == "elevenlabs", "T15_voice_eleven_provider_const");
        Ok(ProductionVoiceCatalog.ElevenV3 == "eleven_v3", "T15_voice_eleven_model_const");
        Ok(ProductionLipsyncCatalog.FalProviderId == "fal", "T15_lipsync_fal_provider_const");
        Ok(ProductionLipsyncCatalog.SyncLipsyncV3 == "sync-lipsync-v3", "T15_lipsync_fal_model_const");

        // TEST 16: Verify Action field is preserved for Motion
        Ok(runway.Action == ProductionActions.CreateVideo, "T16_runway_action");
        Ok(veo.Action == ProductionActions.CreateVideo, "T16_veo_action");
        Ok(eleven.Action == ProductionActions.CreateVoice, "T16_eleven_action");
        Ok(fal.Action == ProductionActions.CreateLipsync, "T16_fal_action");

        return fail;
    }
}
