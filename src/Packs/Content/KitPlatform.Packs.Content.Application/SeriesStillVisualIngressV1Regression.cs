namespace KitPlatform.Packs.Content;

public static class SeriesStillVisualIngressV1Regression
{
    public const string SuiteId = SeriesStillVisualIngressV1Rules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var snap = SeriesStillVisualIngressV1Rules.DefaultSnapshot();
        var resolver = new CountingSnapshotResolver(snap);
        var compiler = new CountingUnifiedVisualCompiler();
        var pvs = ProjectVisualStyleV1Rules.BuildPrompt(
            ProjectVisualStyleV1Rules.PresetOf(FamixaVisualUniverseAuthorityV1Rules.StyleId)!);
        var prefix = FamixaVisualUniverseAuthorityV1Rules.CompileStylePrefix(pvs);
        var age38 = CharacterAgeConsistencyV1Rules.FromCanonicalAge(38);
        var age11 = CharacterAgeConsistencyV1Rules.FromCanonicalAge(11);
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
            Action: "father turns toward the child",
            Location: "living room",
            Age: age38,
            Appearance: male,
            Gender: "male");

        var resolved = SeriesStillVisualIngressV1Rules.ResolveSnapshotAsync(resolver, "FAMIXA")
            .GetAwaiter().GetResult();
        Ok(resolved.Gate is null && resolved.Snapshot is not null
            && resolved.Snapshot.VisualUniverseSha == snap.VisualUniverseSha,
            "F-01 Series Still resolves VisualUniverseSnapshot");
        Ok(resolver.Calls >= 1
            && SeriesStillVisualIngressV1Rules.SnapshotResolver == "IVisualUniverseSnapshotResolver",
            "F-02 Series Still uses IVisualUniverseSnapshotResolver");

        var (still, stillGate) = SeriesStillVisualIngressV1Rules.CompileStill(snap, scene, compiler);
        Ok(stillGate is null && still is not null && compiler.Calls >= 1
            && SeriesStillVisualIngressV1Rules.StyleCompiler == "IUnifiedVisualCompiler",
            "F-03 Series Still calls IUnifiedVisualCompiler");
        Ok(still is not null && SeriesStillVisualIngressV1Rules.ProviderMayCall(still)
            && still.CompiledPrompt.StartsWith(still.StyleLayer, StringComparison.Ordinal),
            "F-04 UnifiedVisualContract is created before provider call");
        Ok(still is not null && FamixaVisualUniverseAuthorityV1Rules.SameSha(
            still.VisualUniverseSha, FamixaVisualUniverseAuthorityV1Rules.Sha()),
            "F-05 visualUniverseSha is present");
        Ok(still is not null && FamixaVisualUniverseAuthorityV1Rules.SameSha(
            still.PvsSha, ProjectVisualStyleV2Rules.ProtectedV1Sha),
            "F-06 pvsSha is present");
        Ok(still is not null && FamixaVisualUniverseAuthorityV1Rules.SameSha(
            still.CdlSha, CharacterDesignLanguageV2Rules.Sha()),
            "F-07 cdlSha is present");
        Ok(still is not null && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(still.CompiledPromptSha),
            "F-08 compiledPromptSha is present");
        Ok(!SeriesStillVisualIngressV1Rules.SeriesStillOwnsVisualStyle()
            && !SeriesStillVisualIngressV1Rules.InventsSecondCompiler()
            && still!.StyleLayer == prefix,
            "F-09 Series Still does not own independent visual style grammar");
        Ok(!string.IsNullOrWhiteSpace(still!.SceneLanguage)
            && still.SceneLanguage.Contains("father turns", StringComparison.OrdinalIgnoreCase)
            && !still.SceneLanguage.Contains("[FAMIXA VISUAL UNIVERSE AUTHORITY V1]", StringComparison.Ordinal),
            "F-10 Scene Visual Contract contains scene intent, not visual universe authority");

        var raw = SeriesStillVisualIngressV1Rules.CompileStill(
            snap,
            scene with { RawPrompt = "photorealistic Vietnamese family" },
            compiler).Contract!;
        Ok(raw.StyleLayer == prefix
            && !raw.SceneLanguage.Contains("photorealistic", StringComparison.OrdinalIgnoreCase)
            && FamixaVisualUniverseAuthorityV1Rules.PromptBlocksAdultPhotorealEscalation(raw.CompiledPrompt),
            "F-11 Raw visual-style prompt cannot bypass compiler");

        var brief = CharacterStudioV1Rules.IdentityBrief(
            "CHAR-099", "Lan", 38, "male", "STYLE_3D_STYLIZED_REALISM", "adult", "Bố");
        var fromBrief = SeriesStillVisualIngressV1Rules.CompileStill(
            snap, scene with { IdentityBrief = brief }, compiler).Contract!;
        Ok(!SeriesStillVisualIngressV1Rules.IdentityBriefBypassesCompiler()
            && fromBrief.StyleLayer == prefix
            && FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(fromBrief.CompiledPrompt, still.CompiledPrompt),
            "F-12 IdentityBrief cannot bypass compiler");

        var fromPvs = SeriesStillVisualIngressV1Rules.CompileStill(
            snap, scene with { RawPrompt = pvs, PvsBuildPrompt = pvs }, compiler).Contract!;
        Ok(!SeriesStillVisualIngressV1Rules.PvsBuildPromptBypassesCompiler()
            && fromPvs.StyleLayer == prefix
            && !fromPvs.SceneLanguage.Contains(pvs, StringComparison.Ordinal),
            "F-13 PVS BuildPrompt cannot bypass compiler");
        Ok(!SeriesStillVisualIngressV1Rules.LegacyStyleFallback(),
            "F-14 Legacy prompt fallback is forbidden");

        Ok(SeriesStillVisualIngressV1Rules.CompileStill(null, scene).Gate
            == SeriesStillVisualIngressV1Rules.GateSnapshot,
            "F-15 Missing VisualUniverseSnapshot blocks");
        Ok(SeriesStillVisualIngressV1Rules.ValidateSnapshot(snap with { VisualUniverseSha = "" })
            == SeriesStillVisualIngressV1Rules.GateVuaSha,
            "F-16 Missing VUA SHA blocks");
        Ok(SeriesStillVisualIngressV1Rules.ValidateSnapshot(snap with { PvsSha = "" })
            == SeriesStillVisualIngressV1Rules.GatePvsSha,
            "F-17 Missing PVS SHA blocks");
        Ok(SeriesStillVisualIngressV1Rules.ValidateSnapshot(snap with { CdlSha = "" })
            == SeriesStillVisualIngressV1Rules.GateCdlSha,
            "F-18 Missing CDL SHA blocks");
        Ok(SeriesStillVisualIngressV1Rules.CompileStill(snap, scene, new FailingUnifiedVisualCompiler()).Gate
            == SeriesStillVisualIngressV1Rules.GateCompile,
            "F-19 Compiler failure blocks");

        var unbound = new ImageGenerationExecutionRequest(
            "photorealistic Vietnamese family", [], "16:9", null);
        Ok(!SeriesStillVisualIngressV1Rules.ProviderMayCall(unbound)
            && SeriesStillVisualIngressV1Rules.ProviderMayCall(still)
            && SeriesStillVisualIngressV1Rules.ProviderMayCall(
                SeriesStillVisualIngressV1Rules.ToProviderRequest(still, [], "16:9")),
            "F-20 Provider is not called on blocked request");
        Ok(raw.PhotorealismCeiling == snap.PhotorealismCeiling
            && raw.StyleLayer == still.StyleLayer,
            "F-21 Scene request photorealistic cannot override Visual Universe");
        Ok(raw.PhotorealismCeiling == "LOW"
            && raw.StylizationLevel == snap.StylizationLevel,
            "F-22 Scene request cannot increase photorealism above universe ceiling");

        var otherId = SeriesStillVisualIngressV1Rules.CompileStill(
            snap, scene with { CharacterId = "CHAR-098", Action = "child waves" }, compiler).Contract!;
        Ok(otherId.StyleLayer == still.StyleLayer
            && otherId.RenderingLanguage == still.RenderingLanguage
            && otherId.CameraLanguage == still.CameraLanguage,
            "F-23 Character identity cannot change rendering grammar");

        var child = SeriesStillVisualIngressV1Rules.CompileStill(
            snap, scene with { Age = age11, Gender = "female", Appearance = null }, compiler).Contract!;
        Ok(child.StyleLayer == still.StyleLayer
            && child.PhotorealismCeiling == still.PhotorealismCeiling
            && still.CompiledPrompt.Contains("ChronologicalAge: 38.", StringComparison.Ordinal)
            && child.CompiledPrompt.Contains("ChronologicalAge: 11.", StringComparison.Ordinal),
            "F-24 Age cannot change rendering grammar");
        Ok(FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(still.CompiledPrompt, otherId.CompiledPrompt)
            && still.StyleLayer == otherId.StyleLayer,
            "F-25 Same snapshot → same canonical style prefix");

        var master = CharacterFirstMasterVisualIngressV1Rules.CompileMaster(
            snap, "CHAR-099", age38, male, "male", null).Contract!;
        Ok(FamixaVisualUniverseAuthorityV1Rules.SameSha(master.VisualUniverseSha, still.VisualUniverseSha),
            "F-26 Master and Series Still share same Visual Universe SHA");
        Ok(FamixaVisualUniverseAuthorityV1Rules.SameSha(master.PvsSha, still.PvsSha),
            "F-27 Master and Series Still share same PVS SHA");
        Ok(FamixaVisualUniverseAuthorityV1Rules.SameSha(master.CdlSha, still.CdlSha)
            && FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(master.CompiledPrompt, still.CompiledPrompt),
            "F-28 Master and Series Still share same CDL SHA");

        var revision = SeriesStillVisualIngressV1Rules.CompileRevision(
            snap, scene with { RevisionIntent = "tighter two-shot, same room" }, compiler).Contract!;
        Ok(revision.StyleLayer == still.StyleLayer
            && FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(revision.CompiledPrompt, still.CompiledPrompt),
            "F-29 Series Still revision uses same compiler");
        Ok(!SeriesStillVisualIngressV1Rules.CallsGemini()
            && !SeriesStillVisualIngressV1Rules.CreatesPixels()
            && CharacterStudioV1Rules.ProtectedMinhUnchanged(
                CharacterAuthorityInitializationV1Rules.ProtectedMasterSha,
                CharacterAuthorityInitializationV1Rules.ProtectedDnaSha,
                CharacterAuthorityInitializationV1Rules.ProtectedPrpSha,
                CharacterAuthorityInitializationV1Rules.ProtectedCrpSha),
            "F-30 No existing character mutation");
        Ok(CharacterAuthorityInitializationV1Rules.ProtectedMasterSha
                == "be439c39e067aa6c7727255e9643ac78cb7c6285917af60dda38bf14a32518f1"
            && CharacterAuthorityInitializationV1Rules.ProtectedDnaSha
                == "75ececad8899211ce31107232fe0288c11a9e113c5bc0e7c0a6c9f749d72f4dc"
            && CharacterAuthorityInitializationV1Rules.ProtectedPrpSha
                == "5e61ad240aaebaa13dcd91463a41ef9f9c0498fabefe86b7e8b1a1ad973a9444"
            && CharacterAuthorityInitializationV1Rules.ProtectedCrpSha
                == "82543a4a4331e32a79a865fc3881c17e8bc3dc74c5dec51c52c2deab1a70c2b7",
            "F-31 Minh SHA unchanged");
        Ok(ProjectVisualStyleV1Rules.Sha(ProjectVisualStyleV1Rules.PresetOf("3D_STYLIZED_REALISM")!)
                == "d48e4884f6ac3315c887dfd139aae86510822d15ec8cc8705e8980629547de58"
            && ProjectVisualStyleV2Rules.Sha()
                == "56b57eee8388538fe7277d49bf6e53e013c495338329c4d5cd4549688fe1530d",
            "F-32 PVS authority unchanged");
        Ok(CharacterDesignLanguageV2Rules.Sha()
                == "683ce6bd64b1588c38db325cf4ce724d4f66be48b581b7f044ab25a065c88153",
            "F-33 CDL authority unchanged");
        Ok(FamixaVisualUniverseAuthorityV1Rules.Sha()
                == "4e9c4bad9ee0d241d1896846a574828865dcdd9e96e18751c251b3c77ea76726"
            && !FamixaVisualUniverseAuthorityV1Rules.DraftIsProductionAuthority(),
            "F-34 VUA authority unchanged");
        Ok(!SeriesStillVisualIngressV1Rules.CallsGemini()
            && SeriesStillVisualIngressV1Rules.IngressInvariant(),
            "F-35 No Gemini call");

        return fail;
    }

    private sealed class CountingSnapshotResolver(VisualUniverseSnapshot snapshot) : IVisualUniverseSnapshotResolver
    {
        public int Calls { get; private set; }

        public Task<VisualUniverseSnapshot> GetCurrentAsync(
            string projectId, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(snapshot);
        }

        public IReadOnlyList<string> RunRegression() => [];
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
