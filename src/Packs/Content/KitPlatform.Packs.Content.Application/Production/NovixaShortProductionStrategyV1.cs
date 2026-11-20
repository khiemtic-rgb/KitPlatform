namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// NOVIXA short plan. Picture and speech are separate so a voice-over is not a lip-sync
/// and a moving shot can also carry speech. An empty plan leaves the Famixa contract unchanged.
/// Motion graphics stay unavailable: this pipeline has no graphics renderer or QA for that label.
/// A flat JPEG is not a rig, so this type never starts local mouth animation.
/// </summary>
public static class NovixaShortProductionStrategyV1
{
    public const string PictureStatic = "STATIC";
    public const string PictureImageToVideo = "IMAGE_TO_VIDEO";
    public const string PictureMotionGraphics = "MOTION_GRAPHICS";
    public const string PictureUndeclared = "UNDECLARED";

    public const string SpeechNone = "NONE";
    public const string SpeechVoiceOver = "VOICE_OVER";
    public const string SpeechMouthUnsynced = "MOUTH_UNSYNCED";
    public const string SpeechLipSync = "LIP_SYNC";

    public const string ConfirmationRequired = "STRATEGY_CONFIRMATION_REQUIRED";
    public const string Undeclared = "STRATEGY_UNDECLARED";
    public const string StaticForbidsPaidVideo = "STATIC_FORBIDS_PAID_VIDEO";
    public const string MotionGraphicsUnsupported = "MOTION_GRAPHICS_UNSUPPORTED";
    public const string VoiceOverForbidsLipsync = "VOICE_OVER_FORBIDS_LIPSYNC";
    public const string MouthUnsyncedForbidsLipsync = "MOUTH_UNSYNCED_FORBIDS_LIPSYNC";
    public const string SpeechDoesNotRequireLipsync = "SPEECH_DOES_NOT_REQUIRE_LIPSYNC";
    public const string LipsyncRequiresMovingPicture = "LIPSYNC_REQUIRES_IMAGE_TO_VIDEO";

    public const bool MotionGraphicsRendererReady = false;
    public const bool FlatImageHasRig = false;

    public sealed record Facts(
        bool HasDialogue,
        bool SpeaksOnCamera,
        bool NeedsPictureMotion,
        bool WordLevelMouth);

    public sealed record Plan(
        string Picture,
        string Speech,
        bool Confirmed,
        string Reason);

    public static bool Engaged(ShotExecutionInput input) =>
        !string.IsNullOrWhiteSpace(input.PictureStrategy) || !string.IsNullOrWhiteSpace(input.SpeechDelivery);

    public static bool Engaged(CreateVideoReadinessClaim? claim) =>
        claim is not null && (
            !string.IsNullOrWhiteSpace(claim.PictureStrategy)
            || !string.IsNullOrWhiteSpace(claim.SpeechDelivery));

    public static Plan Suggest(Facts facts)
    {
        var wordLevel = facts.HasDialogue && facts.SpeaksOnCamera && facts.WordLevelMouth;
        var picture = facts.NeedsPictureMotion || wordLevel ? PictureImageToVideo : PictureStatic;
        var speech = !facts.HasDialogue
            ? SpeechNone
            : !facts.SpeaksOnCamera
                ? SpeechVoiceOver
                : wordLevel
                    ? SpeechLipSync
                    : SpeechMouthUnsynced;
        var reason = wordLevel
            ? "On-camera line needs a moving picture and a mouth target. Confirm before any paid step."
            : speech == SpeechVoiceOver
                ? "Dialogue is voice-over. Do not call lip-sync."
                : speech == SpeechMouthUnsynced
                    ? "Mouth motion is not word-level. Do not call lip-sync."
                    : picture == PictureStatic
                        ? "Approved still. Do not call image-to-video."
                        : "Picture motion only. Confirm before any paid step.";
        return new Plan(picture, speech, false, reason);
    }

    public static string? PaidMotionBlock(ShotExecutionInput input)
    {
        if (!Engaged(input)) return null;
        if (!input.StrategyConfirmed) return ConfirmationRequired;
        var picture = Norm(input.PictureStrategy);
        if (picture is "" or PictureUndeclared) return Undeclared;
        if (picture == PictureStatic) return StaticForbidsPaidVideo;
        if (picture == PictureMotionGraphics)
            return MotionGraphicsUnsupported;
        if (picture == PictureImageToVideo) return null;
        return Undeclared;
    }

    public static string? ImageToVideoBlock(ShotExecutionInput input, ProductionCostDecision? cost)
    {
        var picture = PaidMotionBlock(input);
        if (picture is not null) return picture;
        if (!Engaged(input)) return null;
        if (cost is null || !cost.Allowed)
            return cost?.BlockReason ?? ProductionFailureReasons.UnknownCost;
        return null;
    }

    public static string? LipsyncRefusal(ShotExecutionInput input)
    {
        if (!Engaged(input)) return null;
        if (!input.StrategyConfirmed) return ConfirmationRequired;
        if (input.DirectSpeakerCount > 1)
            return VideoAudioLipsyncPipelineV1Rules.GateMultiSpeaker;
        var speech = Norm(input.SpeechDelivery);
        if (speech is "" or SpeechNone) return SpeechDoesNotRequireLipsync;
        if (speech == SpeechVoiceOver) return VoiceOverForbidsLipsync;
        if (speech == SpeechMouthUnsynced) return MouthUnsyncedForbidsLipsync;
        if (speech != SpeechLipSync) return SpeechDoesNotRequireLipsync;
        if (Norm(input.PictureStrategy) != PictureImageToVideo) return LipsyncRequiresMovingPicture;
        return null;
    }

    public static string? LipsyncBlock(
        ShotExecutionInput input,
        bool hasTake = false,
        bool hasVoice = false,
        bool hasMouthTarget = false)
    {
        var refusal = LipsyncRefusal(input);
        if (refusal is not null) return refusal;
        if (!Engaged(input) || Norm(input.SpeechDelivery) != SpeechLipSync) return null;
        if (!hasTake) return ProductionFailureReasons.MissingVideoArtifact;
        if (!hasVoice) return ProductionFailureReasons.MissingVoiceArtifact;
        if (!hasMouthTarget) return ProductionFailureReasons.LipsyncTargetUnresolved;
        return null;
    }

    public static IReadOnlyList<ProductionActionAvailability> Apply(
        IReadOnlyList<ProductionActionAvailability> actions,
        CreateVideoReadinessClaim? claim)
    {
        if (!Engaged(claim)) return actions;
        var input = new ShotExecutionInput(
            claim?.Story ?? "",
            claim?.ActingBeat ?? "",
            claim?.VisualContract ?? "",
            "",
            "",
            "",
            "",
            "",
            PictureStrategy: claim?.PictureStrategy ?? "",
            SpeechDelivery: claim?.SpeechDelivery ?? "",
            StrategyConfirmed: claim?.StrategyConfirmed ?? false,
            DirectSpeakerCount: claim?.DirectSpeakerCount ?? 0);
        var paid = PaidMotionBlock(input);
        var lip = LipsyncRefusal(input);
        return actions.Select(action =>
        {
            if (action.Action == ProductionActions.CreateVideo && paid is not null)
                return action with { Available = false, BlockReason = paid };
            if (action.Action == ProductionActions.CreateLipsync && lip is not null)
                return action with { Available = false, BlockReason = lip };
            if (action.Action == ProductionActions.MixVideo && IsStaticVoiceOver(input) && !input.StrategyConfirmed)
                return action with { Available = false, BlockReason = ConfirmationRequired };
            return action;
        }).ToArray();
    }

    public static string SnapshotMaterial(ShotExecutionInput input) =>
        string.Join('|', input.Story, input.ActingBeat, input.VisualContract, input.KeyframeByteSha256, input.Timing);

    public static bool IsStaticVoiceOver(ShotExecutionInput input) =>
        Engaged(input)
        && Norm(input.PictureStrategy) == PictureStatic
        && Norm(input.SpeechDelivery) == SpeechVoiceOver;

    public static bool IsConfirmedStaticVoiceOver(ShotExecutionInput input) =>
        input.StrategyConfirmed && IsStaticVoiceOver(input);

    public static bool IsConfirmedStaticVoiceOver(CreateVideoReadinessClaim? claim) =>
        claim is not null && IsConfirmedStaticVoiceOver(new ShotExecutionInput(
            claim.Story ?? "",
            claim.ActingBeat ?? "",
            claim.VisualContract ?? "",
            "",
            "",
            "",
            "",
            "",
            PictureStrategy: claim.PictureStrategy ?? "",
            SpeechDelivery: claim.SpeechDelivery ?? "",
            StrategyConfirmed: claim.StrategyConfirmed,
            DirectSpeakerCount: claim.DirectSpeakerCount));

    static string Norm(string? value) => (value ?? "").Trim().ToUpperInvariant();
}
