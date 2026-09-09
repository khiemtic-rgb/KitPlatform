namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_VIDEO_VISUAL_INGRESS_V1 — Video / I2V compile through
/// VisualUniverseSnapshot → IUnifiedVisualCompiler. No second style compiler.
/// Motion / camera / timing append after the canonical style body.
/// Does not call Gemini, Runway, or create artifacts.
/// </summary>
public static class VideoVisualIngressV1Rules
{
    public const string DocumentId = "FAMIXA_VIDEO_VISUAL_INGRESS_V1";
    public const string SuiteId = "FAMIXA_VIDEO_VISUAL_INGRESS_V1_REGRESSION";
    public const string VisualStyleSource = "VisualUniverseSnapshot";
    public const string StyleCompiler = "IUnifiedVisualCompiler";
    public const string SnapshotResolver = "IVisualUniverseSnapshotResolver";
    public const string VideoView = "VIDEO";
    public const string GateConfirm = "CONFIRMATION_REQUIRED";
    public const int RunwayPromptMax = 1000;

    public const string GateSnapshot = SeriesStillVisualIngressV1Rules.GateSnapshot;
    public const string GateVuaSha = SeriesStillVisualIngressV1Rules.GateVuaSha;
    public const string GatePvsSha = SeriesStillVisualIngressV1Rules.GatePvsSha;
    public const string GateCdlSha = SeriesStillVisualIngressV1Rules.GateCdlSha;
    public const string GateCompile = SeriesStillVisualIngressV1Rules.GateCompile;
    public const string GateCompiledSha = SeriesStillVisualIngressV1Rules.GateCompiledSha;

    public static bool CallsGemini() => false;
    public static bool CallsRunway() => false;
    public static bool CreatesPixels() => false;
    public static bool CreatesVideo() => false;
    public static bool ProviderCannotDefineStyle() => true;
    public static bool RawStylePromptForbidden() => true;
    public static bool LegacyStyleFallback() => false;
    public static bool VideoOwnsVisualStyle() => false;
    public static bool InventsSecondCompiler() => false;
    public static bool MutatesDatabase() => false;
    public static bool MutatesCharacters() => false;
    public static bool MutatesAuthority() => false;

    public static bool IngressInvariant() =>
        VisualStyleSource == "VisualUniverseSnapshot"
        && StyleCompiler == "IUnifiedVisualCompiler"
        && SnapshotResolver == "IVisualUniverseSnapshotResolver"
        && ProviderCannotDefineStyle()
        && RawStylePromptForbidden()
        && !LegacyStyleFallback()
        && !VideoOwnsVisualStyle()
        && !InventsSecondCompiler();

    public static string? ValidateSnapshot(VisualUniverseSnapshot? snapshot) =>
        SeriesStillVisualIngressV1Rules.ValidateSnapshot(snapshot);

    public static VisualUniverseSnapshot DefaultSnapshot() =>
        SeriesStillVisualIngressV1Rules.DefaultSnapshot();

    public static SeriesStillVisualIngressV1Rules.SceneVisualContract FromTurbo(
        ContentSeriesTurboStartRequest request) =>
        new(
            ProjectId: FamixaVisualUniverseAuthorityV1Rules.ProjectId,
            ShotId: request.ClipId,
            RawPrompt: request.Prompt,
            Motion: request.Prompt,
            Duration: request.Seconds > 0 ? request.Seconds.ToString() : null,
            Framing: VideoView);

    public static SeriesStillVisualIngressV1Rules.SceneVisualContract FromExecution(
        string? characterId,
        string? motionIntent,
        string? productionPrompt,
        double duration,
        string? aspect) =>
        new(
            ProjectId: FamixaVisualUniverseAuthorityV1Rules.ProjectId,
            CharacterId: characterId,
            RawPrompt: productionPrompt,
            Motion: motionIntent,
            Duration: duration > 0 ? duration.ToString("0.##") : null,
            Framing: string.IsNullOrWhiteSpace(aspect) ? VideoView : aspect);

    public static string ComposeMotionLayer(SeriesStillVisualIngressV1Rules.SceneVisualContract scene)
    {
        var parts = new[]
        {
            Fact("Motion", scene.Motion),
            Fact("Blocking", scene.Blocking),
            Fact("CameraMovement", scene.CameraMovement),
            Fact("Duration", scene.Duration),
            Fact("Timing", scene.Timing),
            Fact("Subject", scene.Subject),
        }.Where(x => x.Length > 0);
        return SeriesStillVisualIngressV1Rules.StripCallerStyleAuthority(string.Join(" ", parts));
    }

    public static (UnifiedVisualContract? Contract, string? Gate) CompileVideo(
        VisualUniverseSnapshot? snapshot,
        SeriesStillVisualIngressV1Rules.SceneVisualContract scene,
        IUnifiedVisualCompiler? compiler = null)
    {
        var framed = scene with
        {
            Framing = string.IsNullOrWhiteSpace(scene.Framing) ? VideoView : scene.Framing,
        };
        var compiled = SeriesStillVisualIngressV1Rules.CompileStill(snapshot, framed, compiler);
        if (compiled.Gate is not null || compiled.Contract is null || snapshot is null)
            return compiled;
        var motion = ComposeMotionLayer(framed);
        if (motion.Length == 0)
            return compiled;
        var prompt = compiled.Contract.CompiledPrompt + " " + motion;
        if (!prompt.StartsWith(compiled.Contract.StyleLayer, StringComparison.Ordinal)
            || !FamixaVisualUniverseAuthorityV1Rules.PromptHasAuthorityOrder(prompt))
            return (null, GateCompile);
        var sha = UnifiedVisualCompilerV1Rules.HashCompiled(prompt, snapshot);
        if (!FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(sha))
            return (null, GateCompiledSha);
        return (compiled.Contract with { CompiledPrompt = prompt, CompiledPromptSha = sha }, null);
    }

    public static VideoVisualIngressDryRun DryRun(
        bool confirm,
        VisualUniverseSnapshot? snapshot,
        SeriesStillVisualIngressV1Rules.SceneVisualContract scene,
        IUnifiedVisualCompiler? compiler = null)
    {
        if (!confirm)
            return new VideoVisualIngressDryRun(
                GateConfirm, false, false, false, false, false, null);
        var compiled = CompileVideo(snapshot, scene, compiler);
        if (compiled.Gate is not null || compiled.Contract is null
            || !ProviderMayCall(compiled.Contract))
            return new VideoVisualIngressDryRun(
                compiled.Gate ?? GateCompile, true, false, false, false, false, null);
        return new VideoVisualIngressDryRun(
            null, true, false, false, false, false, compiled.Contract);
    }

    /// <summary>
    /// Runway I2V promptText max 1000. Style lives in the keyframe + CompiledPrompt SHA,
    /// not in promptText. Does not invent a second style compiler.
    /// </summary>
    public static string RunwayI2vPrompt(string? compiledOrMotion, int max = RunwayPromptMax)
    {
        var cap = max < 1 ? RunwayPromptMax : max;
        var source = compiledOrMotion ?? "";
        var hasAuthority = source.Contains("[FAMIXA VISUAL UNIVERSE AUTHORITY V1]", StringComparison.Ordinal);
        var motionAt = IndexOfMotionLayer(source);
        var slice = motionAt >= 0 ? source[motionAt..] : hasAuthority ? "" : source;
        var raw = SeriesStillVisualIngressV1Rules.StripCallerStyleAuthority(slice);
        raw = System.Text.RegularExpressions.Regex.Replace(raw ?? "", @"\s+", " ").Trim();
        if (raw.Length == 0)
            raw = "Subtle body movement, blink and breathe. Camera remains steady.";
        return raw.Length <= cap ? raw : raw[..cap];
    }

    public static bool ProviderMayCall(UnifiedVisualContract? contract) =>
        SeriesStillVisualIngressV1Rules.ProviderMayCall(contract);

    public static bool ProviderMayCall(VideoGenerationExecutionRequest request) =>
        FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(request.VisualUniverseSha)
        && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(request.PvsSha)
        && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(request.CdlSha)
        && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(request.CompiledPromptSha)
        && !string.IsNullOrWhiteSpace(request.CompiledPrompt)
        && request.CompiledPrompt.Contains("[FAMIXA VISUAL UNIVERSE AUTHORITY V1]", StringComparison.Ordinal);

    public static VideoGenerationExecutionRequest ToProviderRequest(
        UnifiedVisualContract contract,
        byte[] sourceImage,
        string sourceMime,
        string sourceSha,
        double duration,
        string resolution,
        string fps,
        string aspect,
        string providerConfigVersion) =>
        new(
            sourceImage,
            sourceMime,
            sourceSha,
            duration,
            resolution,
            fps,
            aspect,
            contract.CompiledPrompt,
            providerConfigVersion,
            contract.CompiledPrompt,
            contract.VisualUniverseSha,
            contract.PvsSha,
            contract.CdlSha,
            contract.CompiledPromptSha);

    public static Task<(VisualUniverseSnapshot? Snapshot, string? Gate)> ResolveSnapshotAsync(
        IVisualUniverseSnapshotResolver resolver,
        string? projectId,
        CancellationToken cancellationToken = default) =>
        SeriesStillVisualIngressV1Rules.ResolveSnapshotAsync(resolver, projectId, cancellationToken);

    private static string Fact(string label, string? value)
    {
        var t = SeriesStillVisualIngressV1Rules.StripCallerStyleAuthority(value);
        return t.Length == 0 ? "" : $"{label}: {t}.";
    }

    private static int IndexOfMotionLayer(string raw)
    {
        foreach (var mark in new[] { "Motion:", "CameraMovement:", "Blocking:", "Duration:", "Timing:" })
        {
            var i = raw.IndexOf(mark, StringComparison.Ordinal);
            if (i >= 0) return i;
        }
        return -1;
    }
}

public sealed record VideoVisualIngressDryRun(
    string? GateCode,
    bool Confirm,
    bool ProviderCalled,
    bool GenerationExecuted,
    bool ArtifactCreated,
    bool DatabaseMutated,
    UnifiedVisualContract? Contract);
