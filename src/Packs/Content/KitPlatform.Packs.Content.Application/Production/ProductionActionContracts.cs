namespace KitPlatform.Packs.Content.Production;

public sealed record ProductionActionContract(
    string Action,
    string Target,
    IReadOnlyList<string> RequiredInputs,
    string AllowedProviderCapability,
    string ExpectedOutputType,
    string OutputArtifactRole,
    IReadOnlyList<string> AllowedTransitions,
    IReadOnlyList<string> ForbiddenFallbacks,
    string UiCta,
    string SuccessCondition);

public static class ProductionActionContractRegistry
{
    public static readonly ProductionActionContract CreateImage = new(
        ProductionActions.CreateImage,
        "Shot",
        ["VisualContract", "CharacterIdentity"],
        "Picture",
        ProductionOutputTypes.Image,
        ProductionArtifactRoles.Keyframe,
        [ProductionTaskStatuses.Created, ProductionTaskStatuses.Succeeded, ProductionTaskStatuses.FailedIdentity],
        [ProductionActions.CreateVideo, ProductionActions.CreateVoice, ProductionActions.CreateLipsync],
        "Tạo hình",
        "Verified IMAGE artifact belongs to Attempt and snapshot.");

    public static readonly ProductionActionContract CreateVideo = new(
        ProductionActions.CreateVideo,
        "Shot",
        ["ApprovedKeyframe", "Story", "ActingBeat", "VisualContract", "Timing", "FrozenProviderModel", "PerformanceSnapshot"],
        "Motion",
        ProductionOutputTypes.VideoTake,
        ProductionArtifactRoles.VideoTake,
        [ProductionTaskStatuses.Created, ProductionTaskStatuses.Succeeded, ProductionTaskStatuses.FailedIdentity],
        [ProductionActions.CreateImage, ProductionActions.CreateVoice, ProductionActions.CreateLipsync],
        "Tạo video",
        "Verified VIDEO_TAKE artifact belongs to Attempt and snapshot.");

    public static readonly ProductionActionContract CreateVoice = new(
        ProductionActions.CreateVoice,
        "Shot",
        ["LineText", "VoiceId", "LineId", "CharacterId", "PerformanceSnapshot"],
        "Voice",
        ProductionOutputTypes.Voice,
        ProductionArtifactRoles.Voice,
        [ProductionTaskStatuses.Created, ProductionTaskStatuses.Succeeded, ProductionTaskStatuses.FailedIdentity],
        [
            ProductionActions.CreateImage, ProductionActions.CreateVideo, ProductionActions.CreateLipsync,
            "indexedDB.kit-famixa-tts", "latest voice", "voiceId alone", "provider URL", "audioUrl", "voiceUrl",
        ],
        "Tạo giọng nói",
        "Verified VOICE artifact belongs to Attempt and snapshot. CURRENT:VOICE names exact artifactId.");

    public static readonly ProductionActionContract CreateLipsync = new(
        ProductionActions.CreateLipsync,
        "Shot",
        ["MotionArtifactId", "VoiceArtifactId", "FrozenProviderModel", "PerformanceSnapshot"],
        "LipSync",
        ProductionOutputTypes.Lipsync,
        ProductionArtifactRoles.Lipsync,
        [ProductionTaskStatuses.Created, ProductionTaskStatuses.Succeeded, ProductionTaskStatuses.FailedIdentity],
        [
            ProductionActions.CreateImage, ProductionActions.CreateVideo, ProductionActions.CreateVoice,
            "indexedDB.kit-famixa-tts", "latest video", "latest voice", "voiceId alone",
            "provider URL", "acceptedTake.url", "takeUrl", "previewUrl", "audioUrl", "voiceUrl", "lipsyncUrl",
        ],
        "Khớp môi",
        "Verified LIPSYNC artifact belongs to Attempt and snapshot. Parents are exact VIDEO_TAKE+VOICE artifactIds.");

    public static readonly ProductionActionContract MixVideo = new(
        ProductionActions.MixVideo,
        "Shot",
        ["LipSyncArtifactId", "Timing"],
        "Assemble",
        ProductionOutputTypes.Mix,
        ProductionArtifactRoles.Mix,
        [ProductionTaskStatuses.Created, ProductionTaskStatuses.Succeeded, ProductionTaskStatuses.FailedIdentity],
        [
            ProductionActions.CreateImage, ProductionActions.CreateVideo, ProductionActions.CreateVoice,
            "latest lipsync", "latest take", "lipsyncUrl", "previewUrl", "finalUrl", "acceptedTake.url",
            "takeUrl", "videoUrl", "audioUrl", "voiceUrl", "provider URL", "browser blob",
            "indexedDB.kit-famixa-tts", "ShotProductionFinalStore",
        ],
        "Hoàn thiện Shot",
        "Verified MIX artifact belongs to Attempt and snapshot. Parent is exact CURRENT:LIPSYNC artifactId.");

    public static readonly ProductionActionContract Accept = new(
        ProductionActions.Accept,
        "Artifact",
        ["ArtifactId"],
        "None",
        ProductionOutputTypes.Binding,
        ProductionBindingRoles.Accepted,
        [ProductionBindingRoles.Accepted],
        ["visible URL", "latest playable"],
        "Dùng video này",
        "ACCEPTED binding names exact artifactId.");

    public static readonly ProductionActionContract Bind = new(
        ProductionActions.Bind,
        "Artifact",
        ["ArtifactId", "Role"],
        "None",
        ProductionOutputTypes.Binding,
        ProductionBindingRoles.Current,
        [ProductionBindingRoles.Current, ProductionBindingRoles.Visible],
        ["latest playable URL"],
        "Gắn artifact",
        "Binding names exact artifactId and role.");

    public static readonly ProductionActionContract Regenerate = new(
        ProductionActions.Regenerate,
        "Shot",
        ["PriorTaskId"],
        "SameAsPrior",
        ProductionOutputTypes.VideoTake,
        ProductionArtifactRoles.VideoTake,
        [ProductionTaskStatuses.Created],
        ["mutate historical Attempt"],
        "Tạo lại video",
        "New Task or Attempt; prior artifact stays immutable.");

    public static readonly ProductionActionContract Assemble = new(
        ProductionActions.Assemble,
        "Film",
        ["AcceptedArtifactIds"],
        "Assemble",
        "ASSEMBLY",
        ProductionArtifactRoles.Assembly,
        [ProductionTaskStatuses.Created, ProductionTaskStatuses.Succeeded],
        ["latest take", "acceptedTake.url", "previewUrl", "takeUrl", "lipsyncUrl"],
        "Lắp tập",
        "Assembly lists ordered ACCEPTED artifactIds. No FINAL claim.");

    public static readonly ProductionActionContract AssembleEpisode = new(
        ProductionActions.AssembleEpisode,
        "Episode",
        ["OrderedAssemblyInputs"],
        "Assemble",
        "ASSEMBLY",
        ProductionArtifactRoles.Assembly,
        [ProductionTaskStatuses.Created, ProductionTaskStatuses.Succeeded],
        [
            "latest take", "acceptedTake.url", "previewUrl", "takeUrl", "lipsyncUrl",
            "acceptedArtifactId", "graph_json", "series_build", "/content/series/assemble",
            "RemuxOnceAsync", "FINAL", "FilmAssemblyShot MIX",
        ],
        "Lắp tập phim",
        "Verified ASSEMBLY artifact; parents are exact CURRENT:ASSEMBLY ids. No FINAL claim.");

    public static IReadOnlyList<ProductionActionContract> All =>
    [
        CreateImage, CreateVideo, CreateVoice, CreateLipsync, MixVideo, Accept, Bind, Regenerate, Assemble,
        AssembleEpisode,
    ];

    public static ProductionActionContract Require(string action)
    {
        var hit = All.FirstOrDefault(c => c.Action == action);
        if (hit is null)
            throw new InvalidOperationException($"Unknown production action: {action}");
        return hit;
    }

    public static string CtaFor(string action) => Require(action).UiCta;

    public static string ActionForCta(string label)
    {
        var cta = (label ?? "").Trim();
        var hit = All.FirstOrDefault(c => string.Equals(c.UiCta, cta, StringComparison.Ordinal));
        if (hit is null)
            throw new InvalidOperationException($"CTA is not a registered action: {cta}");
        return hit.Action;
    }
}

public static class ProductionUiActionMap
{
    public static readonly IReadOnlyDictionary<string, string> CtaToAction =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Tạo hình"] = ProductionActions.CreateImage,
            ["Tạo hình mới"] = ProductionActions.CreateImage,
            ["Tạo video"] = ProductionActions.CreateVideo,
            ["Tạo lại video"] = ProductionActions.Regenerate,
            ["Tạo giọng nói"] = ProductionActions.CreateVoice,
            ["Nạp thoại"] = ProductionActions.CreateVoice,
            ["Khớp môi"] = ProductionActions.CreateLipsync,
            ["Khớp môi mới"] = ProductionActions.CreateLipsync,
            ["Dùng video này"] = ProductionActions.Accept,
            ["Hoàn thiện Shot"] = ProductionActions.MixVideo,
            ["Hoàn thiện"] = ProductionActions.MixVideo,
            ["Dùng kết quả này"] = ProductionActions.Accept,
            ["Lắp tập"] = ProductionActions.Assemble,
            ["Lắp tập phim"] = ProductionActions.AssembleEpisode,
        };

    public static string RequireAction(string cta)
    {
        if (!CtaToAction.TryGetValue((cta ?? "").Trim(), out var action))
            throw new InvalidOperationException($"CTA has no 1:1 action: {cta}");
        return action;
    }
}
