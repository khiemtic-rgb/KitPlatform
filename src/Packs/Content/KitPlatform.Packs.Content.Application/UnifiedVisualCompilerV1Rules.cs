using System.Text;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

/// <summary>
/// UNIFIED_VISUAL_COMPILER_V1 — wrapper around CompileStylePrefix.
/// Does not invent a second grammar. Does not use PVS V2. Does not call Gemini.
/// </summary>
public static class UnifiedVisualCompilerV1Rules
{
    public const string DocumentId = "UNIFIED_VISUAL_COMPILER_V1";
    public const string SuiteId = "UNIFIED_VISUAL_COMPILER_V1_REGRESSION";
    public const string PromptVersion = "UNIFIED_VISUAL_COMPILER_V1";
    public const string ContractSuiteId = "UNIFIED_VISUAL_CONTRACT_V1_REGRESSION";

    public static bool CallsGemini() => false;
    public static bool CreatesPixels() => false;
    public static bool InventsSecondGrammar() => false;
    public static bool UsesPvsV2AsLiveAuthority() => false;
    public static bool RawPromptIsStyleAuthority() => false;

    public static UnifiedVisualContract Compile(
        VisualUniverseSnapshot snapshot,
        UnifiedVisualIdentityLayer? identity = null,
        UnifiedVisualSceneLayer? scene = null,
        string? view = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var pvs = ProjectVisualStyleV1Rules.PresetOf(FamixaVisualUniverseAuthorityV1Rules.StyleId)
            ?? throw new InvalidOperationException("PROJECT_VISUAL_STYLE_NOT_READY");
        var styleLayer = FamixaVisualUniverseAuthorityV1Rules.CompileStylePrefix(
            ProjectVisualStyleV1Rules.BuildPrompt(pvs));
        var identityLayer = ComposeIdentity(identity);
        var viewLayer = ComposeView(view);
        var sceneLayer = ComposeScene(scene);
        var compiledPrompt = string.Join(" ", new[]
        {
            styleLayer,
            identityLayer,
            viewLayer,
            sceneLayer,
        }.Where(x => x.Length > 0));
        var compiledPromptSha = HashCompiled(compiledPrompt, snapshot);
        return new UnifiedVisualContract(
            snapshot.ProjectId,
            snapshot.VisualUniverseId,
            snapshot.VisualUniverseSha,
            snapshot.PvsSha,
            snapshot.CdlSha,
            snapshot.CalibrationPackSha,
            string.IsNullOrWhiteSpace(identity?.CharacterId) ? null : identity.CharacterId.Trim(),
            string.IsNullOrWhiteSpace(identity?.CharacterMasterSha) ? null : identity.CharacterMasterSha.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(identity?.CharacterCanonSha) ? null : identity.CharacterCanonSha.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(view) ? null : view.Trim().ToUpperInvariant(),
            snapshot.CameraLanguage,
            snapshot.RenderingLanguage,
            sceneLayer,
            snapshot.StylizationLevel,
            snapshot.PhotorealismCeiling,
            PromptVersion,
            compiledPromptSha,
            compiledPrompt,
            styleLayer,
            identityLayer);
    }

    public static string HashCompiled(string compiledPrompt, VisualUniverseSnapshot snapshot) =>
        KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(string.Join('\n',
            compiledPrompt,
            snapshot.VisualUniverseSha,
            snapshot.PvsSha,
            snapshot.CdlSha,
            snapshot.CalibrationPackSha ?? "")));

    public static string ComposeIdentity(UnifiedVisualIdentityLayer? identity)
    {
        if (identity is null) return "";
        return SanitizeCallerLayer(string.Join(" ", new[]
        {
            string.IsNullOrWhiteSpace(identity.IdentityText) ? "" : identity.IdentityText.Trim(),
            string.IsNullOrWhiteSpace(identity.CharacterId) ? "" : $"CharacterId: {identity.CharacterId.Trim()}.",
        }.Where(x => x.Length > 0)));
    }

    public static string ComposeView(string? view)
    {
        var type = (view ?? "").Trim().ToUpperInvariant();
        return type.Length == 0 ? "" : $"View: {type}.";
    }

    public static string ComposeScene(UnifiedVisualSceneLayer? scene)
    {
        if (scene is null) return "";
        return SanitizeCallerLayer(string.Join(" ", new[]
        {
            Fact("Story", scene.Story),
            Fact("Action", scene.Action),
            Fact("Emotion", scene.Emotion),
            Fact("Location", scene.Location),
            Fact("Props", scene.SceneObjects),
            Fact("Composition", scene.CompositionIntent),
        }.Where(x => x.Length > 0)));
    }

    public static string SanitizeCallerLayer(string? text)
    {
        var raw = (text ?? "").Trim();
        if (raw.Length == 0) return "";
        var cleaned = Regex.Replace(
            raw,
            @"(?i)\b(photorealistic|photographic portrait|digital[- ]human|3d stylized|visual universe|stylization\s*=|realism ceiling|cinematic family drama|live[- ]action actor)\b",
            " ");
        return Regex.Replace(cleaned, @"\s+", " ").Trim();
    }

    private static string Fact(string label, string? value)
    {
        var t = (value ?? "").Trim();
        return t.Length == 0 ? "" : $"{label}: {t}.";
    }
}
