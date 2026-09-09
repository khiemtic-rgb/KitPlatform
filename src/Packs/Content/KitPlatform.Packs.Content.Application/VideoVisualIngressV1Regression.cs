namespace KitPlatform.Packs.Content;

public static class VideoVisualIngressV1Regression
{
    public const string SuiteId = VideoVisualIngressV1Rules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var snap = VideoVisualIngressV1Rules.DefaultSnapshot();
        var compiler = new CountingUnifiedVisualCompiler();
        var pvs = ProjectVisualStyleV1Rules.BuildPrompt(
            ProjectVisualStyleV1Rules.PresetOf(FamixaVisualUniverseAuthorityV1Rules.StyleId)!);
        var prefix = FamixaVisualUniverseAuthorityV1Rules.CompileStylePrefix(pvs);
        var age38 = CharacterAgeConsistencyV1Rules.FromCanonicalAge(38);
        var male = CharacterAppearanceProfileV1Rules.Compile(
            new CharacterAppearanceProfileV1Rules.AppearanceSource(
                38, "male", "Bố", "warm", null, null, snap.PvsSha));
        var scene = new SeriesStillVisualIngressV1Rules.SceneVisualContract(
            ProjectId: "FAMIXA",
            SeriesId: "SERIES-01",
            EpisodeId: "EP-01",
            ShotId: "SH-01",
            CharacterId: "CHAR-099",
            Story: "after dinner",
            Action: "father turns",
            Location: "living room",
            Age: age38,
            Appearance: male,
            Gender: "male",
            Motion: "subtle turn and blink",
            CameraMovement: "steady hold",
            Duration: "5",
            Timing: "hold 1s then move");

        Ok(VideoVisualIngressV1Rules.DocumentId == "FAMIXA_VIDEO_VISUAL_INGRESS_V1"
            && VideoVisualIngressV1Rules.IngressInvariant(),
            "G-01 Video contract exists");
        Ok(VideoVisualIngressV1Rules.CompileVideo(null, scene).Gate
            == VideoVisualIngressV1Rules.GateSnapshot,
            "G-02 Snapshot required");
        Ok(VideoVisualIngressV1Rules.ValidateSnapshot(snap with { VisualUniverseSha = "" })
            == VideoVisualIngressV1Rules.GateVuaSha,
            "G-03 VUA SHA required");
        Ok(VideoVisualIngressV1Rules.ValidateSnapshot(snap with { PvsSha = "" })
            == VideoVisualIngressV1Rules.GatePvsSha,
            "G-04 PVS SHA required");
        Ok(VideoVisualIngressV1Rules.ValidateSnapshot(snap with { CdlSha = "" })
            == VideoVisualIngressV1Rules.GateCdlSha,
            "G-05 CDL SHA required");
        Ok(VideoVisualIngressV1Rules.StyleCompiler == "IUnifiedVisualCompiler"
            && !VideoVisualIngressV1Rules.InventsSecondCompiler(),
            "G-06 Unified compiler required");
        Ok(VideoVisualIngressV1Rules.CompileVideo(snap, scene, new FailingUnifiedVisualCompiler()).Gate
            == VideoVisualIngressV1Rules.GateCompile,
            "G-07 compiler failure blocks");

        var video = VideoVisualIngressV1Rules.CompileVideo(snap, scene, compiler).Contract!;
        Ok(!VideoVisualIngressV1Rules.LegacyStyleFallback()
            && !VideoVisualIngressV1Rules.ProviderMayCall(
                new VideoGenerationExecutionRequest([], "image/jpeg", "", 5, "1280x720", "24", "16:9",
                    "photorealistic Vietnamese family", "V1")),
            "G-08 no raw prompt fallback");

        var brief = CharacterStudioV1Rules.IdentityBrief(
            "CHAR-099", "Lan", 38, "male", "STYLE_3D_STYLIZED_REALISM", "adult", "Bố");
        var fromBrief = VideoVisualIngressV1Rules.CompileVideo(
            snap, scene with { IdentityBrief = brief }, compiler).Contract!;
        Ok(fromBrief.StyleLayer == prefix
            && FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(fromBrief.CompiledPrompt, video.CompiledPrompt),
            "G-09 no IdentityBrief style fallback");
        var fromPvs = VideoVisualIngressV1Rules.CompileVideo(
            snap, scene with { RawPrompt = pvs, PvsBuildPrompt = pvs }, compiler).Contract!;
        Ok(fromPvs.StyleLayer == prefix
            && !fromPvs.SceneLanguage.Contains(pvs, StringComparison.Ordinal),
            "G-10 no PVS BuildPrompt fallback");

        var bound = VideoVisualIngressV1Rules.ToProviderRequest(
            video, [1], "image/jpeg", new string('a', 64), 5, "1280x720", "24", "16:9", "V1");
        Ok(VideoVisualIngressV1Rules.ProviderMayCall(video)
            && VideoVisualIngressV1Rules.ProviderMayCall(bound)
            && bound.CompiledPrompt == video.CompiledPrompt
            && bound.MotionIntent == video.CompiledPrompt,
            "G-11 provider receives compiled contract only");
        Ok(VideoVisualIngressV1Rules.ProviderCannotDefineStyle()
            && compiler.Calls >= 1,
            "G-12 provider cannot resolve style");
        Ok(video.StyleLayer == prefix
            && video.CompiledPrompt.StartsWith(prefix, StringComparison.Ordinal)
            && FamixaVisualUniverseAuthorityV1Rules.PromptHasAuthorityOrder(video.CompiledPrompt),
            "G-13 canonical style prefix exists");

        var videoAgain = VideoVisualIngressV1Rules.CompileVideo(snap, scene, compiler).Contract!;
        Ok(video.StyleLayer == videoAgain.StyleLayer
            && FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(video.CompiledPrompt, videoAgain.CompiledPrompt),
            "G-14 same snapshot → same style prefix");

        var master = CharacterFirstMasterVisualIngressV1Rules.CompileMaster(
            snap, "CHAR-099", age38, male, "male", null).Contract!;
        var still = SeriesStillVisualIngressV1Rules.CompileStill(snap, scene, compiler).Contract!;
        Ok(FamixaVisualUniverseAuthorityV1Rules.SameSha(master.VisualUniverseSha, video.VisualUniverseSha)
            && FamixaVisualUniverseAuthorityV1Rules.SameSha(master.PvsSha, still.PvsSha)
            && FamixaVisualUniverseAuthorityV1Rules.SameSha(still.CdlSha, video.CdlSha)
            && FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(master.CompiledPrompt, still.CompiledPrompt)
            && FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(still.CompiledPrompt, video.CompiledPrompt),
            "G-15 Master / Still / Video style consistency");
        Ok(video.IdentityLayer.Contains("AgeAppearanceProfile:", StringComparison.Ordinal)
            && video.StyleLayer == prefix,
            "G-16 character appearance remains identity-layer");
        Ok(video.CompiledPrompt.Contains("ChronologicalAge: 38.", StringComparison.Ordinal)
            && video.PhotorealismCeiling == snap.PhotorealismCeiling,
            "G-17 age remains identity-layer");

        var otherMotion = VideoVisualIngressV1Rules.CompileVideo(
            snap, scene with { Motion = "walk across the room" }, compiler).Contract!;
        Ok(otherMotion.StyleLayer == video.StyleLayer
            && otherMotion.CompiledPrompt.Contains("walk across the room", StringComparison.Ordinal)
            && FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(otherMotion.CompiledPrompt, video.CompiledPrompt),
            "G-18 motion does not change style");
        var otherCam = VideoVisualIngressV1Rules.CompileVideo(
            snap, scene with { CameraMovement = "slow push in" }, compiler).Contract!;
        Ok(otherCam.StyleLayer == video.StyleLayer
            && FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(otherCam.CompiledPrompt, video.CompiledPrompt),
            "G-19 camera movement does not change style");

        var dryMissing = VideoVisualIngressV1Rules.DryRun(true, null, scene, compiler);
        Ok(dryMissing.GateCode == VideoVisualIngressV1Rules.GateSnapshot
            && !dryMissing.ProviderCalled && !dryMissing.GenerationExecuted,
            "G-20 missing snapshot blocks provider");
        var drySha = VideoVisualIngressV1Rules.DryRun(true, snap with { CdlSha = "" }, scene, compiler);
        Ok(drySha.GateCode == VideoVisualIngressV1Rules.GateCdlSha
            && !drySha.ProviderCalled,
            "G-21 missing SHA blocks provider");

        var dry = VideoVisualIngressV1Rules.DryRun(false, snap, scene, compiler);
        Ok(dry.GateCode == VideoVisualIngressV1Rules.GateConfirm
            && !dry.Confirm && !dry.ProviderCalled && !dry.GenerationExecuted
            && !dry.ArtifactCreated && !dry.DatabaseMutated && dry.Contract is null,
            "G-22 confirm=false blocks generation");
        Ok(!VideoVisualIngressV1Rules.MutatesDatabase() && !dry.DatabaseMutated,
            "G-23 no database mutation");
        Ok(!VideoVisualIngressV1Rules.MutatesCharacters()
            && CharacterStudioV1Rules.ProtectedMinhUnchanged(
                CharacterAuthorityInitializationV1Rules.ProtectedMasterSha,
                CharacterAuthorityInitializationV1Rules.ProtectedDnaSha,
                CharacterAuthorityInitializationV1Rules.ProtectedPrpSha,
                CharacterAuthorityInitializationV1Rules.ProtectedCrpSha),
            "G-24 no character mutation");
        Ok(!VideoVisualIngressV1Rules.MutatesAuthority()
            && ProjectVisualStyleV1Rules.Sha(ProjectVisualStyleV1Rules.PresetOf("3D_STYLIZED_REALISM")!)
                == "d48e4884f6ac3315c887dfd139aae86510822d15ec8cc8705e8980629547de58"
            && CharacterDesignLanguageV2Rules.Sha()
                == "683ce6bd64b1588c38db325cf4ce724d4f66be48b581b7f044ab25a065c88153"
            && FamixaVisualUniverseAuthorityV1Rules.Sha()
                == "4e9c4bad9ee0d241d1896846a574828865dcdd9e96e18751c251b3c77ea76726",
            "G-25 no authority mutation");
        Ok(CharacterAuthorityInitializationV1Rules.ProtectedMasterSha
                == "be439c39e067aa6c7727255e9643ac78cb7c6285917af60dda38bf14a32518f1"
            && CharacterAuthorityInitializationV1Rules.ProtectedDnaSha
                == "75ececad8899211ce31107232fe0288c11a9e113c5bc0e7c0a6c9f749d72f4dc"
            && CharacterAuthorityInitializationV1Rules.ProtectedPrpSha
                == "5e61ad240aaebaa13dcd91463a41ef9f9c0498fabefe86b7e8b1a1ad973a9444"
            && CharacterAuthorityInitializationV1Rules.ProtectedCrpSha
                == "82543a4a4331e32a79a865fc3881c17e8bc3dc74c5dec51c52c2deab1a70c2b7",
            "G-26 Minh SHA unchanged");
        Ok(!VideoVisualIngressV1Rules.CallsGemini()
            && !VideoVisualIngressV1Rules.CallsRunway()
            && SeriesStillVisualIngressV1Regression.Run().Count == 0
            && CharacterFirstMasterVisualIngressV1Regression.Run().Count == 0
            && UnifiedVisualCompilerV1Regression.Run().Count == 0
            && VideoGenerationExecutionV1Regression.Run().Count == 0,
            "G-27 existing regression suites remain PASS");
        var dryOk = VideoVisualIngressV1Rules.DryRun(true, snap, scene, compiler);
        Ok(dryOk.Confirm && dryOk.GateCode is null && dryOk.Contract is not null
            && !dryOk.ProviderCalled && !dryOk.GenerationExecuted
            && !dryOk.ArtifactCreated && !dryOk.DatabaseMutated,
            "G-28 confirm=true dry-run still does not call provider");
        Ok(!VideoVisualIngressV1Rules.ProviderMayCall(
                new VideoGenerationExecutionRequest([], "image/jpeg", "", 5, "1280x720", "24", "16:9",
                    video.CompiledPrompt, "V1"))
            && VideoVisualIngressV1Rules.ProviderMayCall(bound),
            "G-29 unbound compiled text without SHAs cannot reach provider");
        var runwayText = VideoVisualIngressV1Rules.RunwayI2vPrompt(video.CompiledPrompt);
        Ok(runwayText.Length <= VideoVisualIngressV1Rules.RunwayPromptMax
            && !runwayText.Contains("[FAMIXA VISUAL UNIVERSE AUTHORITY V1]", StringComparison.Ordinal)
            && runwayText.Contains("Motion:", StringComparison.Ordinal),
            "G-30 Runway I2V text is motion-only ≤1000");

        return fail;
    }

    private sealed class CountingUnifiedVisualCompiler : IUnifiedVisualCompiler
    {
        public int Calls { get; private set; }

        public UnifiedVisualContract Compile(
            VisualUniverseSnapshot snapshot,
            UnifiedVisualIdentityLayer? identity = null,
            UnifiedVisualSceneLayer? scene = null,
            string? view = null)
        {
            Calls++;
            return UnifiedVisualCompilerV1Rules.Compile(snapshot, identity, scene, view);
        }

        public IReadOnlyList<string> RunRegression() => [];
    }

    private sealed class FailingUnifiedVisualCompiler : IUnifiedVisualCompiler
    {
        public UnifiedVisualContract Compile(
            VisualUniverseSnapshot snapshot,
            UnifiedVisualIdentityLayer? identity = null,
            UnifiedVisualSceneLayer? scene = null,
            string? view = null) =>
            throw new InvalidOperationException("UNIFIED_VISUAL_COMPILER_FAILED");

        public IReadOnlyList<string> RunRegression() => [];
    }
}
