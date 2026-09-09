using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_SERIES_STILL_VISUAL_INGRESS_V1 — Series Still / IGE compile through
/// VisualUniverseSnapshot → IUnifiedVisualCompiler. No second style compiler.
/// Does not call Gemini, create pixels, or mutate characters.
/// Video may later reuse SceneVisualContract + CompileStill.
/// </summary>
public static class SeriesStillVisualIngressV1Rules
{
    public const string DocumentId = "FAMIXA_SERIES_STILL_VISUAL_INGRESS_V1";
    public const string SuiteId = "FAMIXA_SERIES_STILL_VISUAL_INGRESS_V1_REGRESSION";
    public const string VisualStyleSource = "VisualUniverseSnapshot";
    public const string StyleCompiler = "IUnifiedVisualCompiler";
    public const string SnapshotResolver = "IVisualUniverseSnapshotResolver";
    public const string StillView = "SERIES_STILL";

    public const string GateSnapshot = CharacterFirstMasterVisualIngressV1Rules.GateSnapshot;
    public const string GateVuaSha = CharacterFirstMasterVisualIngressV1Rules.GateVuaSha;
    public const string GatePvsSha = CharacterFirstMasterVisualIngressV1Rules.GatePvsSha;
    public const string GateCdlSha = CharacterFirstMasterVisualIngressV1Rules.GateCdlSha;
    public const string GateCompile = CharacterFirstMasterVisualIngressV1Rules.GateCompile;
    public const string GateCompiledSha = "COMPILED_PROMPT_SHA_MISSING";

    public static bool CallsGemini() => false;
    public static bool CreatesPixels() => false;
    public static bool ProviderCannotDefineStyle() => true;
    public static bool RawStylePromptForbidden() => true;
    public static bool LegacyStyleFallback() => false;
    public static bool SeriesStillOwnsVisualStyle() => false;
    public static bool IdentityBriefBypassesCompiler() => false;
    public static bool PvsBuildPromptBypassesCompiler() => false;
    public static bool InventsSecondCompiler() => false;

    public static bool IngressInvariant() =>
        VisualStyleSource == "VisualUniverseSnapshot"
        && StyleCompiler == "IUnifiedVisualCompiler"
        && SnapshotResolver == "IVisualUniverseSnapshotResolver"
        && ProviderCannotDefineStyle()
        && RawStylePromptForbidden()
        && !LegacyStyleFallback()
        && !SeriesStillOwnsVisualStyle()
        && !InventsSecondCompiler();

    public sealed record SceneVisualContract(
        string? ProjectId = null,
        string? SeriesId = null,
        string? EpisodeId = null,
        string? ShotId = null,
        string? CharacterId = null,
        string? Story = null,
        string? Action = null,
        string? Location = null,
        string? Environment = null,
        string? Props = null,
        string? Wardrobe = null,
        string? Camera = null,
        string? Framing = null,
        string? Composition = null,
        string? Lighting = null,
        string? Continuity = null,
        string? Emotion = null,
        string? RawPrompt = null,
        string? IdentityBrief = null,
        string? PvsBuildPrompt = null,
        AgeExpressionTarget? Age = null,
        CharacterAppearanceProfile? Appearance = null,
        string? Gender = null,
        string? RevisionIntent = null,
        string? Subject = null,
        string? Motion = null,
        string? Blocking = null,
        string? CameraMovement = null,
        string? Duration = null,
        string? Timing = null);

    public static string? ValidateSnapshot(VisualUniverseSnapshot? snapshot) =>
        CharacterFirstMasterVisualIngressV1Rules.ValidateSnapshot(snapshot);

    public static VisualUniverseSnapshot DefaultSnapshot() =>
        CharacterFirstMasterVisualIngressV1Rules.DefaultSnapshot();

    public static SceneVisualContract FromSeriesRequest(ContentSeriesStillRequest request) =>
        new(
            ProjectId: FamixaVisualUniverseAuthorityV1Rules.ProjectId,
            CharacterId: request.CharacterId,
            Story: request.Story,
            Action: request.Action,
            Location: request.Location,
            Environment: request.Environment,
            Props: request.Props,
            Wardrobe: request.Wardrobe,
            Camera: request.Camera,
            Framing: request.Framing,
            Composition: request.Composition,
            Lighting: request.Lighting,
            Continuity: request.Continuity,
            Emotion: request.Emotion,
            RawPrompt: request.Prompt,
            IdentityBrief: request.IdentityBrief,
            PvsBuildPrompt: request.PvsBuildPrompt,
            RevisionIntent: request.RevisionIntent);

    public static SceneVisualContract FromIge(
        string? characterId,
        string? productionPrompt,
        string? story = null,
        string? action = null,
        string? location = null,
        string? revisionIntent = null) =>
        new(
            ProjectId: FamixaVisualUniverseAuthorityV1Rules.ProjectId,
            CharacterId: characterId,
            Story: story,
            Action: action,
            Location: location,
            RawPrompt: productionPrompt,
            RevisionIntent: revisionIntent);

    public static string StripCallerStyleAuthority(string? text)
    {
        var t = UnifiedVisualCompilerV1Rules.SanitizeCallerLayer(text);
        var pvs = ProjectVisualStyleV1Rules.PresetOf(FamixaVisualUniverseAuthorityV1Rules.StyleId);
        if (pvs is not null)
        {
            var build = ProjectVisualStyleV1Rules.BuildPrompt(pvs);
            if (build.Length > 0)
                t = t.Replace(build, " ", StringComparison.Ordinal);
        }
        t = Regex.Replace(t, @"(?i)single canonical visual identity of one character\.?", " ");
        t = Regex.Replace(t, @"(?i)use the project visual style authority[^.]*\.?", " ");
        t = Regex.Replace(t, @"(?i)\[FAMIXA VISUAL UNIVERSE AUTHORITY V1\]", " ");
        t = Regex.Replace(t, @"(?i)\b(stylized cinematic|cinematic 3d|cinematic film still|3d stylized realism|not photoreal(?:istic)?)\b", " ");
        return Regex.Replace(t, @"\s+", " ").Trim();
    }

    public static UnifiedVisualSceneLayer ToSceneLayer(SceneVisualContract scene)
    {
        var action = StripCallerStyleAuthority(string.Join(" ", new[]
        {
            scene.Action,
            scene.RawPrompt,
            scene.IdentityBrief,
            scene.PvsBuildPrompt,
            scene.RevisionIntent,
            scene.Wardrobe is null ? null : $"Wardrobe: {scene.Wardrobe}",
            scene.Lighting is null ? null : $"Lighting: {scene.Lighting}",
            scene.Continuity is null ? null : $"Continuity: {scene.Continuity}",
            scene.Environment is null ? null : $"Environment: {scene.Environment}",
        }.Where(x => !string.IsNullOrWhiteSpace(x))));
        return new UnifiedVisualSceneLayer(
            Story: StripCallerStyleAuthority(scene.Story),
            Action: action,
            Emotion: StripCallerStyleAuthority(scene.Emotion),
            Location: StripCallerStyleAuthority(scene.Location),
            SceneObjects: StripCallerStyleAuthority(scene.Props),
            CompositionIntent: StripCallerStyleAuthority(string.Join(" ", new[]
            {
                scene.Composition,
                scene.Camera,
                scene.Framing,
            }.Where(x => !string.IsNullOrWhiteSpace(x)))));
    }

    public static (UnifiedVisualContract? Contract, string? Gate) CompileStill(
        VisualUniverseSnapshot? snapshot,
        SceneVisualContract scene,
        IUnifiedVisualCompiler? compiler = null)
    {
        var gate = ValidateSnapshot(snapshot);
        if (gate is not null || snapshot is null) return (null, gate ?? GateSnapshot);
        try
        {
            var identity = CharacterFirstMasterVisualIngressV1Rules.IdentityLayer(
                scene.CharacterId ?? "",
                scene.Age,
                scene.Appearance,
                scene.Gender ?? scene.Appearance?.Gender,
                null);
            var view = string.IsNullOrWhiteSpace(scene.Framing) ? StillView : scene.Framing;
            var contract = compiler is null
                ? UnifiedVisualCompilerV1Rules.Compile(snapshot, identity, ToSceneLayer(scene), view)
                : compiler.Compile(snapshot, identity, ToSceneLayer(scene), view);
            if (string.IsNullOrWhiteSpace(contract.CompiledPrompt)
                || !contract.CompiledPrompt.StartsWith(contract.StyleLayer, StringComparison.Ordinal)
                || !FamixaVisualUniverseAuthorityV1Rules.PromptHasAuthorityOrder(contract.CompiledPrompt))
                return (null, GateCompile);
            if (!FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(contract.CompiledPromptSha))
                return (null, GateCompiledSha);
            return (contract, null);
        }
        catch
        {
            return (null, GateCompile);
        }
    }

    public static (UnifiedVisualContract? Contract, string? Gate) CompileRevision(
        VisualUniverseSnapshot? snapshot,
        SceneVisualContract scene,
        IUnifiedVisualCompiler? compiler = null) =>
        CompileStill(snapshot, scene, compiler);

    public static bool ProviderMayCall(UnifiedVisualContract? contract) =>
        contract is not null
        && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(contract.VisualUniverseSha)
        && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(contract.PvsSha)
        && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(contract.CdlSha)
        && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(contract.CompiledPromptSha)
        && !string.IsNullOrWhiteSpace(contract.CompiledPrompt)
        && contract.CompiledPrompt.Contains("[FAMIXA VISUAL UNIVERSE AUTHORITY V1]", StringComparison.Ordinal);

    public static bool ProviderMayCall(ImageGenerationExecutionRequest request) =>
        FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(request.VisualUniverseSha)
        && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(request.PvsSha)
        && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(request.CdlSha)
        && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(request.CompiledPromptSha)
        && !string.IsNullOrWhiteSpace(request.Prompt)
        && request.Prompt.Contains("[FAMIXA VISUAL UNIVERSE AUTHORITY V1]", StringComparison.Ordinal);

    public static ImageGenerationExecutionRequest ToProviderRequest(
        UnifiedVisualContract contract,
        IReadOnlyList<ImageGenerationReferenceBytes> references,
        string aspectRatio,
        string? resolution = null) =>
        new(
            contract.CompiledPrompt,
            references,
            aspectRatio,
            resolution,
            contract.VisualUniverseSha,
            contract.PvsSha,
            contract.CdlSha,
            contract.CompiledPromptSha);

    public static async Task<(VisualUniverseSnapshot? Snapshot, string? Gate)> ResolveSnapshotAsync(
        IVisualUniverseSnapshotResolver resolver,
        string? projectId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var snapshot = await resolver.GetCurrentAsync(
                VisualUniverseSnapshotV1Rules.NormalizeProject(projectId), cancellationToken);
            return (snapshot, ValidateSnapshot(snapshot));
        }
        catch
        {
            return (null, GateSnapshot);
        }
    }
}
