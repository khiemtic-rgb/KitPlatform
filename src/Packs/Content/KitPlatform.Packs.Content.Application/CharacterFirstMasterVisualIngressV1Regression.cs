namespace KitPlatform.Packs.Content;

public static class CharacterFirstMasterVisualIngressV1Regression
{
    public const string SuiteId = CharacterFirstMasterVisualIngressV1Rules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var snap = CharacterFirstMasterVisualIngressV1Rules.DefaultSnapshot();
        var compiler = new CountingUnifiedVisualCompiler();
        var pvs = ProjectVisualStyleV1Rules.BuildPrompt(
            ProjectVisualStyleV1Rules.PresetOf(FamixaVisualUniverseAuthorityV1Rules.StyleId)!);
        var prefix = FamixaVisualUniverseAuthorityV1Rules.CompileStylePrefix(pvs);
        var age38 = CharacterAgeConsistencyV1Rules.FromCanonicalAge(38);
        var age11 = CharacterAgeConsistencyV1Rules.FromCanonicalAge(11);
        var male = CharacterAppearanceProfileV1Rules.Compile(
            new CharacterAppearanceProfileV1Rules.AppearanceSource(
                38, "male", "Bố", "warm", null, null, snap.PvsSha));
        var female = CharacterAppearanceProfileV1Rules.Compile(
            new CharacterAppearanceProfileV1Rules.AppearanceSource(
                36, "female", "mẹ", "calm", null, null, snap.PvsSha));
        var child = CharacterAppearanceProfileV1Rules.Compile(
            new CharacterAppearanceProfileV1Rules.AppearanceSource(
                11, "female", "bạn học", "curious", null, null, snap.PvsSha));
        var elderRole = CharacterAppearanceProfileV1Rules.Compile(
            new CharacterAppearanceProfileV1Rules.AppearanceSource(
                38, "male", "ông", "warm", null, null, snap.PvsSha));

        var (master, masterGate) = CharacterFirstMasterVisualIngressV1Rules.CompileMaster(
            snap, "CHAR-099", age38, male, "male", "adult father", CharacterFirstMasterVisualIngressV1Rules.MasterView, compiler);
        Ok(masterGate is null && master is not null && master.VisualUniverseSha == snap.VisualUniverseSha,
            "D-01 Master generation resolves VisualUniverseSnapshot");
        Ok(compiler.Calls >= 1 && CharacterFirstMasterVisualIngressV1Rules.StyleCompiler == "IUnifiedVisualCompiler",
            "D-02 Master generation calls IUnifiedVisualCompiler");
        Ok(master is not null
            && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(master.VisualUniverseSha)
            && FamixaVisualUniverseAuthorityV1Rules.SameSha(master.VisualUniverseSha, FamixaVisualUniverseAuthorityV1Rules.Sha())
            && FamixaVisualUniverseAuthorityV1Rules.SameSha(master.PvsSha, ProjectVisualStyleV2Rules.ProtectedV1Sha)
            && FamixaVisualUniverseAuthorityV1Rules.SameSha(master.CdlSha, CharacterDesignLanguageV2Rules.Sha()),
            "D-03 Compiled Master contract contains visualUniverseSha pvsSha cdlSha");
        Ok(master is not null
            && master.StyleLayer == prefix
            && master.CompiledPrompt.StartsWith(prefix, StringComparison.Ordinal)
            && FamixaVisualUniverseAuthorityV1Rules.PromptHasAuthorityOrder(master.CompiledPrompt),
            "D-04 Compiled Master prompt contains canonical style prefix");
        Ok(!CharacterFirstMasterVisualIngressV1Rules.CharacterStudioOwnsVisualStyle()
            && CharacterFirstMasterVisualIngressV1Rules.VisualStyleSource == "VisualUniverseSnapshot"
            && !master!.IdentityLayer.Contains("3D stylized", StringComparison.OrdinalIgnoreCase)
            && !master.IdentityLayer.Contains("cinematic family drama", StringComparison.OrdinalIgnoreCase),
            "D-05 Character Studio does not own visual-style authority");

        var namedA = CharacterFirstMasterVisualIngressV1Rules.CompileMaster(
            snap, "CHAR-099", age38, male, "male", "Minh").Contract!;
        var namedB = CharacterFirstMasterVisualIngressV1Rules.CompileMaster(
            snap, "CHAR-098", age38, male, "male", "Nam").Contract!;
        Ok(!CharacterFirstMasterVisualIngressV1Rules.NameChangesVisualLanguage()
            && namedA.StyleLayer == namedB.StyleLayer
            && FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(namedA.CompiledPrompt, namedB.CompiledPrompt),
            "D-06 Character name does not change style");

        var roleBo = CharacterFirstMasterVisualIngressV1Rules.CompileMaster(
            snap, "CHAR-099", age38, male, "male", null).Contract!;
        var roleMe = CharacterFirstMasterVisualIngressV1Rules.CompileMaster(
            snap, "CHAR-098", CharacterAgeConsistencyV1Rules.FromCanonicalAge(36), female, "female", null).Contract!;
        var roleOng = CharacterFirstMasterVisualIngressV1Rules.CompileMaster(
            snap, "CHAR-097", age38, elderRole, "male", null).Contract!;
        Ok(!CharacterFirstMasterVisualIngressV1Rules.RoleChangesVisualLanguage()
            && roleBo.StyleLayer == roleMe.StyleLayer
            && roleBo.StyleLayer == roleOng.StyleLayer
            && FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(roleBo.CompiledPrompt, roleMe.CompiledPrompt),
            "D-07 Role does not change style");

        var childMaster = CharacterFirstMasterVisualIngressV1Rules.CompileMaster(
            snap, "CHAR-096", age11, child, "female", null).Contract!;
        Ok(!CharacterFirstMasterVisualIngressV1Rules.AgeChangesVisualLanguage()
            && roleBo.StyleLayer == childMaster.StyleLayer
            && roleBo.CompiledPrompt.Contains("ChronologicalAge: 38.", StringComparison.Ordinal)
            && childMaster.CompiledPrompt.Contains("ChronologicalAge: 11.", StringComparison.Ordinal)
            && roleBo.PhotorealismCeiling == childMaster.PhotorealismCeiling,
            "D-08 Age changes age appearance only");
        Ok(namedA.StyleLayer == namedB.StyleLayer && namedA.StyleLayer == prefix,
            "D-09 Different characters with same Visual Universe receive same style prefix");
        Ok(FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(roleBo.CompiledPrompt, childMaster.CompiledPrompt),
            "D-10 Child and adult characters use same Visual Universe style language");
        Ok(FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(roleBo.CompiledPrompt, roleMe.CompiledPrompt),
            "D-11 Male and female characters use same Visual Universe style language");
        Ok(roleBo.StyleLayer == roleMe.StyleLayer
            && roleBo.StylizationLevel == roleMe.StylizationLevel
            && roleBo.PhotorealismCeiling == roleMe.PhotorealismCeiling
            && roleBo.RenderingLanguage == roleMe.RenderingLanguage
            && roleBo.CameraLanguage == roleMe.CameraLanguage,
            "D-12 Different identity data does not create different rendering grammar");

        var hijack = CharacterFirstMasterVisualIngressV1Rules.CompileMaster(
            snap, "CHAR-099", age38, male, "male", "photorealistic Vietnamese man").Contract!;
        Ok(hijack.StyleLayer == prefix
            && !hijack.IdentityLayer.Contains("photorealistic", StringComparison.OrdinalIgnoreCase)
            && FamixaVisualUniverseAuthorityV1Rules.PromptBlocksAdultPhotorealEscalation(hijack.CompiledPrompt),
            "D-13 Raw visual-style override is rejected");
        Ok(hijack.PhotorealismCeiling == snap.PhotorealismCeiling
            && hijack.PhotorealismCeiling == "LOW"
            && hijack.StylizationLevel == snap.StylizationLevel,
            "D-14 Caller cannot raise photorealism above snapshot ceiling");

        Ok(CharacterFirstMasterVisualIngressV1Rules.CompileMaster(
                null, "CHAR-099", age38, male, "male", null).Gate
            == CharacterFirstMasterVisualIngressV1Rules.GateSnapshot
            && CharacterFirstMasterVisualIngressV1Rules.ValidateSnapshot(null)
                == CharacterFirstMasterVisualIngressV1Rules.GateSnapshot,
            "D-15 Missing snapshot blocks generation");
        Ok(CharacterFirstMasterVisualIngressV1Rules.ValidateSnapshot(snap with { VisualUniverseSha = "" })
                == CharacterFirstMasterVisualIngressV1Rules.GateVuaSha,
            "D-16 Missing VUA SHA blocks generation");
        Ok(CharacterFirstMasterVisualIngressV1Rules.ValidateSnapshot(snap with { PvsSha = "" })
                == CharacterFirstMasterVisualIngressV1Rules.GatePvsSha,
            "D-17 Missing PVS SHA blocks generation");
        Ok(CharacterFirstMasterVisualIngressV1Rules.ValidateSnapshot(snap with { CdlSha = "" })
                == CharacterFirstMasterVisualIngressV1Rules.GateCdlSha,
            "D-18 Missing CDL SHA blocks generation");
        Ok(CharacterFirstMasterVisualIngressV1Rules.CompileMaster(
                snap, "CHAR-099", age38, male, "male", null, null, new FailingUnifiedVisualCompiler()).Gate
            == CharacterFirstMasterVisualIngressV1Rules.GateCompile,
            "D-19 Compiler failure blocks generation");

        var blockedReq = new CharacterAuthorityGenerationRequest(
            "CHAR-099", "ERA-01", CharacterFirstMasterVisualIngressV1Rules.GenerationTypeMaster,
            "raw style fallback", "3:4", "V1");
        var boundReq = CharacterFirstMasterVisualIngressV1Rules.ToProviderRequest(
            master!, "CHAR-099", "ERA-01", CharacterFirstMasterVisualIngressV1Rules.GenerationTypeMaster, "V1");
        Ok(!CharacterFirstMasterVisualIngressV1Rules.ProviderMayCall(blockedReq)
            && CharacterFirstMasterVisualIngressV1Rules.ProviderMayCall(boundReq)
            && !CharacterFirstMasterVisualIngressV1Rules.LegacyStyleFallback()
            && CharacterFirstMasterVisualIngressV1Rules.RawStylePromptForbidden(),
            "D-20 Provider is not called on blocked requests");

        var identity = CharacterStudioV1Rules.IdentityBrief(
            "CHAR-099", "Lan", 38, "male", "STYLE_3D_STYLIZED_REALISM", "adult", "Bố");
        var revisionSource = new CharacterMasterRevisionV1Rules.RevisionSource(
            true, "CHAR-099", true, "MASTER-V1", new string('a', 64),
            male, age38, new string('b', 64), identity, snap.PvsSha, true,
            CharacterMasterRevisionV1Rules.ReasonAge,
            "Character is 38 but current Master appears approximately 55–65 years old.",
            true, "GEMINI", false, false, null);
        var revisionCompiler = new CountingUnifiedVisualCompiler();
        var (revision, revisionGate) = CharacterFirstMasterVisualIngressV1Rules.CompileRevision(
            snap, revisionSource, revisionCompiler);
        var built = CharacterMasterRevisionV1Rules.BuildGenerationRequest(revisionSource);
        Ok(revisionGate is null && revision is not null && revisionCompiler.Calls >= 1
            && built.CanonicalText.StartsWith(prefix, StringComparison.Ordinal)
            && CharacterMasterRevisionV1Rules.PromptForbidsExactMasterFace(built.CanonicalText, revisionSource.RevisionReason),
            "D-21 Master Revision uses same compiler");
        Ok(revision is not null
            && revision.StyleLayer == master!.StyleLayer
            && FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(revision.CompiledPrompt, master.CompiledPrompt),
            "D-22 First Master and Master Revision with same snapshot produce the same style prefix");

        Ok(!CharacterFirstMasterVisualIngressV1Rules.CallsGemini()
            && !CharacterFirstMasterVisualIngressV1Rules.CreatesPixels()
            && CharacterStudioV1Rules.ProtectedMinhUnchanged(
                CharacterAuthorityInitializationV1Rules.ProtectedMasterSha,
                CharacterAuthorityInitializationV1Rules.ProtectedDnaSha,
                CharacterAuthorityInitializationV1Rules.ProtectedPrpSha,
                CharacterAuthorityInitializationV1Rules.ProtectedCrpSha)
            && !FamixaVisualUniverseAuthorityV1Rules.MutatesLockedCharacters(),
            "D-23 No existing character is mutated");
        Ok(CharacterAuthorityInitializationV1Rules.ProtectedMasterSha
                == "be439c39e067aa6c7727255e9643ac78cb7c6285917af60dda38bf14a32518f1"
            && CharacterAuthorityInitializationV1Rules.ProtectedDnaSha
                == "75ececad8899211ce31107232fe0288c11a9e113c5bc0e7c0a6c9f749d72f4dc"
            && CharacterAuthorityInitializationV1Rules.ProtectedPrpSha
                == "5e61ad240aaebaa13dcd91463a41ef9f9c0498fabefe86b7e8b1a1ad973a9444"
            && CharacterAuthorityInitializationV1Rules.ProtectedCrpSha
                == "82543a4a4331e32a79a865fc3881c17e8bc3dc74c5dec51c52c2deab1a70c2b7",
            "D-24 Minh SHA protection passes");

        Ok(CharacterFirstMasterVisualIngressV1Rules.IngressInvariant()
            && ProjectVisualStyleV1Regression.Run().Count == 0
            && ProjectVisualStyleV2Regression.Run().Count == 0
            && CharacterDesignLanguageV1Regression.Run().Count == 0
            && CharacterDesignLanguageV2Regression.Run().Count == 0
            && FamixaVisualUniverseAuthorityV1Regression.Run().Count == 0
            && VisualUniverseSnapshotResolverV1Regression.Run().Count == 0
            && UnifiedVisualContractV1Regression.Run().Count == 0
            && UnifiedVisualCompilerV1Regression.Run().Count == 0
            && VisualCalibrationPackV1Regression.Run().Count == 0
            && CharacterAppearanceProfileV1Regression.Run().Count == 0
            && CharacterAgeConsistencyV1Regression.Run().Count == 0
            && CharacterAgeGateV1Regression.Run().Count == 0
            && CharacterAgeGenerationIntegrationV1Regression.Run().Count == 0
            && CharacterStudioV1Regression.Run().Count == 0
            && CharacterStudioUnifiedGenerationV1Regression.Run().Count == 0
            && CharacterMasterRevisionV1Regression.Run().Count == 0,
            "D-25 Existing regressions remain PASS");

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
