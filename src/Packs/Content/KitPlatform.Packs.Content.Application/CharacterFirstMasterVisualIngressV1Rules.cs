namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_CHARACTER_FIRST_MASTER_VISUAL_INGRESS_V1 — Master + Master Revision compile
/// through VisualUniverseSnapshot → IUnifiedVisualCompiler. No legacy style fallback.
/// Does not call Gemini, mutate characters, or promote VUA.
/// </summary>
public static class CharacterFirstMasterVisualIngressV1Rules
{
    public const string DocumentId = "FAMIXA_CHARACTER_FIRST_MASTER_VISUAL_INGRESS_V1";
    public const string SuiteId = "FAMIXA_CHARACTER_FIRST_MASTER_VISUAL_INGRESS_V1_REGRESSION";
    public const string VisualStyleSource = "VisualUniverseSnapshot";
    public const string StyleCompiler = "IUnifiedVisualCompiler";
    public const string GenerationTypeMaster = "STUDIO_MASTER";
    public const string GenerationTypeRevision = "MASTER_REVISION";
    public const string MasterView = "MASTER";

    public const string GateSnapshot = "VISUAL_UNIVERSE_NOT_READY";
    public const string GateVuaSha = "VISUAL_UNIVERSE_SHA_MISSING";
    public const string GatePvsSha = "PROJECT_VISUAL_STYLE_NOT_READY";
    public const string GateCdlSha = "CHARACTER_DESIGN_LANGUAGE_NOT_READY";
    public const string GateCompile = "UNIFIED_VISUAL_COMPILER_FAILED";

    public static bool CallsGemini() => false;
    public static bool CreatesPixels() => false;
    public static bool ProviderCannotDefineStyle() => true;
    public static bool RawStylePromptForbidden() => true;
    public static bool LegacyStyleFallback() => false;
    public static bool CharacterStudioOwnsVisualStyle() => false;
    public static bool RoleChangesVisualLanguage() => false;
    public static bool NameChangesVisualLanguage() => false;
    public static bool AgeChangesVisualLanguage() => false;

    public static string? ValidateSnapshot(VisualUniverseSnapshot? snapshot)
    {
        if (snapshot is null) return GateSnapshot;
        if (!FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(snapshot.VisualUniverseSha))
            return GateVuaSha;
        if (!FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(snapshot.PvsSha))
            return GatePvsSha;
        if (!FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(snapshot.CdlSha))
            return GateCdlSha;
        return null;
    }

    public static bool RequiresBoundContract(string? generationType) =>
        string.Equals(generationType, GenerationTypeMaster, StringComparison.OrdinalIgnoreCase)
        || string.Equals(generationType, GenerationTypeRevision, StringComparison.OrdinalIgnoreCase);

    public static bool ProviderMayCall(CharacterAuthorityGenerationRequest request)
    {
        if (!RequiresBoundContract(request.GenerationType)) return true;
        return FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(request.VisualUniverseSha)
            && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(request.PvsSha)
            && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(request.CdlSha)
            && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(request.CompiledPromptSha)
            && !string.IsNullOrWhiteSpace(request.CanonicalText)
            && request.CanonicalText.Contains("[FAMIXA VISUAL UNIVERSE AUTHORITY V1]", StringComparison.Ordinal);
    }

    public static VisualUniverseSnapshot DefaultSnapshot() =>
        VisualUniverseSnapshotV1Rules.FromAuthorities(
            FamixaVisualUniverseAuthorityV1Rules.ProjectId,
            FamixaVisualUniverseAuthorityV1Rules.ToDto(FamixaVisualUniverseAuthorityV1Rules.StatusDraft, false),
            ProjectVisualStyleV2Rules.ProtectedV1Sha);

    public static bool IngressInvariant() =>
        VisualStyleSource == "VisualUniverseSnapshot"
        && StyleCompiler == "IUnifiedVisualCompiler"
        && ProviderCannotDefineStyle()
        && RawStylePromptForbidden()
        && !LegacyStyleFallback()
        && !CharacterStudioOwnsVisualStyle()
        && !NameChangesVisualLanguage()
        && !RoleChangesVisualLanguage()
        && !AgeChangesVisualLanguage();

    public static (UnifiedVisualContract? Contract, string? Gate) CompileMaster(
        VisualUniverseSnapshot? snapshot,
        string characterId,
        AgeExpressionTarget? age,
        CharacterAppearanceProfile? appearance,
        string? gender,
        string? description,
        string? view = MasterView,
        IUnifiedVisualCompiler? compiler = null)
    {
        var gate = ValidateSnapshot(snapshot);
        if (gate is not null || snapshot is null) return (null, gate ?? GateSnapshot);
        try
        {
            var identity = IdentityLayer(characterId, age, appearance, gender, description);
            var contract = compiler is null
                ? UnifiedVisualCompilerV1Rules.Compile(
                    snapshot, identity, null, string.IsNullOrWhiteSpace(view) ? MasterView : view)
                : compiler.Compile(
                    snapshot, identity, null, string.IsNullOrWhiteSpace(view) ? MasterView : view);
            if (string.IsNullOrWhiteSpace(contract.CompiledPrompt)
                || !contract.CompiledPrompt.StartsWith(contract.StyleLayer, StringComparison.Ordinal)
                || !FamixaVisualUniverseAuthorityV1Rules.PromptHasAuthorityOrder(contract.CompiledPrompt))
                return (null, GateCompile);
            return (contract, null);
        }
        catch
        {
            return (null, GateCompile);
        }
    }

    public static (UnifiedVisualContract? Contract, string? Gate) CompileRevision(
        VisualUniverseSnapshot? snapshot,
        CharacterMasterRevisionV1Rules.RevisionSource source,
        IUnifiedVisualCompiler? compiler = null)
    {
        var gate = ValidateSnapshot(snapshot);
        if (gate is not null || snapshot is null) return (null, gate ?? GateSnapshot);
        var reason = CharacterMasterRevisionV1Rules.NormalizeReason(source.RevisionReason);
        var scene = new UnifiedVisualSceneLayer(
            Story: null,
            Action: $"RevisionReason: {reason}. "
                + (string.IsNullOrWhiteSpace(source.RevisionNotes) ? "" : $"RevisionNotes: {source.RevisionNotes.Trim()}. ")
                + "Identity continuity: keep the same character concept, CharacterId, gender, chronological age, and narrative role. "
                + "Do not treat Role as age or lifestyle. Role is a narrative function only. "
                + (CharacterMasterRevisionV1Rules.MayReviseAppearance(reason)
                    ? CharacterMasterRevisionV1Rules.ContinuityInstruction + " " + CharacterMasterRevisionV1Rules.AppearanceLockForbidden
                    : "Preserve identity. Apply only the requested revision. ")
                + (CharacterMasterRevisionV1Rules.CurrentMasterIsAppearanceLock(reason)
                    ? "Current Master may guide appearance."
                    : "Do not keep exact face, hairstyle, or age from the current Master."));
        try
        {
            var identity = IdentityLayer(
                source.CharacterId ?? "",
                source.Age,
                source.Appearance,
                source.Appearance?.Gender,
                null);
            var contract = compiler is null
                ? UnifiedVisualCompilerV1Rules.Compile(snapshot, identity, scene, MasterView)
                : compiler.Compile(snapshot, identity, scene, MasterView);
            if (string.IsNullOrWhiteSpace(contract.CompiledPrompt)
                || !FamixaVisualUniverseAuthorityV1Rules.PromptHasAuthorityOrder(contract.CompiledPrompt))
                return (null, GateCompile);
            return (contract, null);
        }
        catch
        {
            return (null, GateCompile);
        }
    }

    public static UnifiedVisualIdentityLayer IdentityLayer(
        string characterId,
        AgeExpressionTarget? age,
        CharacterAppearanceProfile? appearance,
        string? gender,
        string? description)
    {
        var ageBlock = age is null ? "" : CharacterAgeConsistencyV1Rules.AgePromptBlock(age, gender ?? appearance?.Gender);
        var appearanceBlock = appearance is null ? "" : CharacterAppearanceProfileV1Rules.PromptBlock(appearance);
        var desc = UnifiedVisualCompilerV1Rules.SanitizeCallerLayer(description);
        return new UnifiedVisualIdentityLayer(
            CharacterStudioV1Rules.NormalizeCharacterId(characterId),
            null,
            null,
            string.Join(" ", new[] { ageBlock, appearanceBlock, desc }.Where(x => x.Length > 0)));
    }

    public static CharacterAuthorityGenerationRequest ToProviderRequest(
        UnifiedVisualContract contract,
        string characterId,
        string eraId,
        string generationType,
        string identityVersion) =>
        new(
            CharacterStudioV1Rules.NormalizeCharacterId(characterId),
            eraId,
            generationType,
            contract.CompiledPrompt,
            "3:4",
            identityVersion,
            contract.VisualUniverseSha,
            contract.PvsSha,
            contract.CdlSha,
            contract.CompiledPromptSha);
}
