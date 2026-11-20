using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class NovixaShortEndToEndV1Regression
{
    public const string SuiteId = "NOVIXA_SHORT_END_TO_END_V1";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var blank = new ShotExecutionInput("story", "acting", "visual", "char", "kf", "5s", "voice", "motion");
        Ok(NovixaShortProductionStrategyV1.PaidMotionBlock(blank) is null, "blank plan does not engage the gate");
        Ok(!NovixaShortProductionStrategyV1.IsConfirmedStaticVoiceOver(blank), "blank plan is not a confirmed still");

        var unconfirmed = blank with
        {
            PictureStrategy = NovixaShortProductionStrategyV1.PictureStatic,
            SpeechDelivery = NovixaShortProductionStrategyV1.SpeechVoiceOver,
        };
        Ok(NovixaShortProductionStrategyV1.PaidMotionBlock(unconfirmed) == NovixaShortProductionStrategyV1.ConfirmationRequired, "unconfirmed static blocks paid video");
        var shown = NovixaShortProductionStrategyV1.Apply(
            [
                new ProductionActionAvailability(ProductionActions.CreateVideo, "Video", true, null),
                new ProductionActionAvailability(ProductionActions.CreateLipsync, "Môi", true, null),
                new ProductionActionAvailability(ProductionActions.MixVideo, "Mix", true, null),
            ],
            new CreateVideoReadinessClaim(
                PictureStrategy: unconfirmed.PictureStrategy,
                SpeechDelivery: unconfirmed.SpeechDelivery));
        Ok(shown.Single(a => a.Action == ProductionActions.CreateVideo).BlockReason == NovixaShortProductionStrategyV1.ConfirmationRequired, "catalog keeps paid video closed");
        Ok(shown.Single(a => a.Action == ProductionActions.MixVideo).BlockReason == NovixaShortProductionStrategyV1.ConfirmationRequired, "catalog keeps mix closed until confirm");

        var confirmed = unconfirmed with { StrategyConfirmed = true };
        Ok(NovixaShortProductionStrategyV1.PaidMotionBlock(confirmed) == NovixaShortProductionStrategyV1.StaticForbidsPaidVideo, "confirmed static still forbids paid video");
        Ok(NovixaShortProductionStrategyV1.LipsyncRefusal(confirmed) == NovixaShortProductionStrategyV1.VoiceOverForbidsLipsync, "confirmed voice-over forbids lip-sync");
        Ok(NovixaShortProductionStrategyV1.IsConfirmedStaticVoiceOver(confirmed), "confirmed static voice-over is the still path");

        var moving = confirmed with
        {
            PictureStrategy = NovixaShortProductionStrategyV1.PictureImageToVideo,
            SpeechDelivery = NovixaShortProductionStrategyV1.SpeechLipSync,
        };
        Ok(NovixaShortProductionStrategyV1.PaidMotionBlock(moving) is null, "image-to-video stays on the existing paid path");
        Ok(NovixaShortProductionStrategyV1.LipsyncBlock(moving, hasTake: false) == ProductionFailureReasons.MissingVideoArtifact, "lip-sync still requires a take");
        Ok(NovixaShortProductionStrategyV1.LipsyncBlock(moving, hasTake: true, hasVoice: false) == ProductionFailureReasons.MissingVoiceArtifact, "lip-sync still requires voice");
        Ok(NovixaShortProductionStrategyV1.LipsyncBlock(moving, hasTake: true, hasVoice: true) == ProductionFailureReasons.LipsyncTargetUnresolved, "lip-sync still requires a mouth target");
        var unknown = ProductionMotionCostGuard.EvaluateQuote(FamixaProviderCostCatalog.Quote(FamixaProviderIds.Wan, "wan-2.1", 5));
        Ok(!unknown.Allowed && unknown.BlockReason == ProductionFailureReasons.UnknownCost, "a missing quote stays unknown cost");
        var over = ProductionMotionCostGuard.Evaluate("runway", "gen4_turbo", 5, isPaid: true, serverMaxCredits: 1);
        Ok(!over.Allowed && over.BlockReason == ProductionFailureReasons.OverBudget, "an over-budget quote stays blocked");

        var cue = new VideoAudioLipsyncPipelineV1Rules.DialogueCue(
            "d", "NV-01", "CHAR-001", "Đóng cửa rồi.", "voice", 0, 2, "audio", 2, true);
        var stillPf = VideoAudioLipsyncPipelineV1Rules.Preflight(new(
            "NV-01", true, [cue], false, false, MixReady: true, DirectorApproved: true, VoiceOverStill: true));
        Ok(stillPf.FinalReady
            && stillPf.FinalSource == VideoAudioLipsyncPipelineV1Rules.FinalSourceVoiceOverStill
            && stillPf.FinalSource != "I2V_ALLOWED"
            && stillPf.FinalSource != "TTS_OVERLAY"
            && stillPf.VideoDurationSec == stillPf.AudioDurationSec
            && stillPf.AudioDurationSec == 2
            && !stillPf.Blockers.Contains(VideoAudioLipsyncPipelineV1Rules.GateLipsyncRequired)
            && !stillPf.LipSyncReady,
            "static voice-over final source is the still hold");
        var legacySpoken = VideoAudioLipsyncPipelineV1Rules.Preflight(new("EP01", true, [cue], true, false, MixReady: true));
        Ok(legacySpoken.FinalSource == "RAW_I2V" && !legacySpoken.FinalReady, "undeclared spoken shot still requires lip-sync");
        var confirmedPf = VideoAudioLipsyncPipelineV1Rules.Preflight(new(
            "NV-01", true, [cue], false, false, MixReady: true,
            PictureStrategy: "STATIC", SpeechDelivery: "VOICE_OVER", StrategyConfirmed: true));
        Ok(confirmedPf.FinalSource == VideoAudioLipsyncPipelineV1Rules.FinalSourceVoiceOverStill && confirmedPf.FinalReady,
            "confirmed static voice-over maps to the still final");
        var unconfirmedPf = VideoAudioLipsyncPipelineV1Rules.Preflight(new(
            "NV-01", true, [cue], true, false, MixReady: true,
            PictureStrategy: "STATIC", SpeechDelivery: "VOICE_OVER", StrategyConfirmed: false));
        Ok(unconfirmedPf.FinalSource == "RAW_I2V" && !unconfirmedPf.FinalReady,
            "unconfirmed static voice-over stays on the Famixa contract");
        var movingPf = VideoAudioLipsyncPipelineV1Rules.Preflight(new(
            "NV-01", true, [cue], true, false, MixReady: true,
            PictureStrategy: "IMAGE_TO_VIDEO", SpeechDelivery: "LIP_SYNC", StrategyConfirmed: true));
        Ok(movingPf.FinalSource == "RAW_I2V" && !movingPf.FinalReady,
            "confirmed image-to-video does not become a still");
        var overlay = VideoAudioLipsyncPipelineV1Rules.Preflight(new(
            "NV-01", true, [cue], false, false, MixReady: true, OverlayPreview: true, DirectorApproved: true, VoiceOverStill: true));
        Ok(!overlay.FinalReady
            && overlay.FinalSource == "NONE"
            && overlay.FinalSource != "TTS_OVERLAY"
            && overlay.Blockers.Contains(VideoAudioLipsyncPipelineV1Rules.GatePreviewOnly)
            && overlay.Blockers.Contains(VideoAudioLipsyncPipelineV1Rules.GateNotEligible),
            "overlay cannot become the still final");

        var keyframeBytes = Encoding.ASCII.GetBytes("NOVIXA-STILL-KEYFRAME-" + new string('K', 32));
        var voiceBytes = Encoding.ASCII.GetBytes("NOVIXA-STILL-VOICE-" + new string('V', 32));
        var keyframeId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        var voiceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2");
        var keyframe = Art(keyframeId, ProductionArtifactRoles.Keyframe, 0, KitVideoIntegrityRules.Sha256Hex(keyframeBytes));
        var voice = Art(voiceId, ProductionArtifactRoles.Voice, 8, KitVideoIntegrityRules.Sha256Hex(voiceBytes));
        var contract = ProductionMixContract.BuildVoiceOverStill(
            keyframe, voice,
            ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.Keyframe),
            ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.Voice));
        Ok(contract.CompositionMode == ProductionMixContract.VoiceOverStill
            && contract.OutputClock == ProductionMixContract.OutputClockVoice
            && contract.OutputDurationSec == 8
            && contract.Parents.All(p => p.Role is ProductionArtifactRoles.Keyframe or ProductionArtifactRoles.Voice)
            && ProductionMixContract.Validate(contract, keyframe, voice) is null,
            "still contract holds the keyframe for the voice duration");
        Ok(ProductionMixFreeze.TryParseV1(ProductionMixFreeze.TokenV1(contract), out var roundTrip)
            && roundTrip.ContractHash == contract.ContractHash,
            "still contract freezes without changing the token parser");
        var relabeled = contract with
        {
            CompositionMode = ProductionMixContract.ComposeSegments,
            OutputClock = ProductionMixContract.OutputClockVideoTake,
        };
        Ok(ProductionMixContract.Validate(relabeled) == ProductionFailureReasons.FailedIdentity, "compose mode still rejects a voice parent");
        Ok(ProductionMixContract.Validate(contract with { CompositionMode = "REMUX_LIPSYNC" })
            == ProductionFailureReasons.UnsupportedCompositionMode, "an unknown mode stays unsupported");

        var transport = new FakeFfmpegMixTransport();
        var adapter = new FfmpegMixExecutionAdapter(transport);
        var snap = new ProductionTaskInputSnapshot(
            Guid.NewGuid(), Guid.NewGuid(), "s", "a", "v", ProductionMixFreeze.TokenV1(contract),
            "", "", "s", "a", "v", "c", "", "", "", "", "",
            ProductionMixCatalog.ServerProviderId, ProductionMixCatalog.FfmpegModelId, "snap",
            DateTimeOffset.UtcNow, DurationSec: contract.OutputDurationSec, ParentArtifactIds: [keyframeId, voiceId]);
        var mixed = adapter.ExecuteFrozenAsync(
            snap, new MixStartContext(Guid.NewGuid(), [], null, keyframeBytes, voiceBytes), default)
            .GetAwaiter().GetResult();
        var command = transport.LastComposeCommand ?? "";
        Ok(mixed.DurationSec == contract.OutputDurationSec
            && transport.ComposeExecutionCount == 1
            && transport.ExecutionCount == 0
            && command.Contains("-loop 1", StringComparison.Ordinal)
            && command.Contains("-t 8", StringComparison.Ordinal)
            && !command.Contains("runway", StringComparison.OrdinalIgnoreCase)
            && !command.Contains("fal", StringComparison.OrdinalIgnoreCase)
            && !command.Contains("veo", StringComparison.OrdinalIgnoreCase),
            "still mix is a local ffmpeg hold and does not call a paid provider");

        return fail;
    }

    static ProductionArtifact Art(Guid id, string role, decimal duration, string sha) =>
        new(id, Guid.NewGuid(), Guid.NewGuid(), role, "application/octet-stream",
            48, duration, sha, "still/" + id.ToString("N")[..8], null, DateTimeOffset.UtcNow,
            ProductionArtifactStatuses.Verified, "still-hash");
}
