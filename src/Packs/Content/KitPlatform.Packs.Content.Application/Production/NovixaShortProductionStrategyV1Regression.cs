using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Production;

public static class NovixaShortProductionStrategyV1Regression
{
    public const string SuiteId = "NOVIXA_SHORT_PRODUCTION_STRATEGY_V1";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        Ok(!NovixaShortProductionStrategyV1.MotionGraphicsRendererReady, "motion graphics renderer is not ready");
        Ok(!NovixaShortProductionStrategyV1.FlatImageHasRig, "flat image is not a rig");

        var still = NovixaShortProductionStrategyV1.Suggest(new(false, false, false, false));
        Ok(still.Picture == NovixaShortProductionStrategyV1.PictureStatic && still.Speech == NovixaShortProductionStrategyV1.SpeechNone && !still.Confirmed, "suggest static");
        Ok(NovixaShortProductionStrategyV1.PaidMotionBlock(Plan(still)) == NovixaShortProductionStrategyV1.ConfirmationRequired, "unconfirmed static does not open paid video");

        var confirmedStill = Plan(still, confirmed: true);
        Ok(NovixaShortProductionStrategyV1.PaidMotionBlock(confirmedStill) == NovixaShortProductionStrategyV1.StaticForbidsPaidVideo, "confirmed static forbids paid video");
        Ok(NovixaShortProductionStrategyV1.LipsyncRefusal(confirmedStill) == NovixaShortProductionStrategyV1.SpeechDoesNotRequireLipsync, "static without speech does not call lip-sync");

        var voiceOver = NovixaShortProductionStrategyV1.Suggest(new(true, false, false, true));
        Ok(voiceOver.Picture == NovixaShortProductionStrategyV1.PictureStatic && voiceOver.Speech == NovixaShortProductionStrategyV1.SpeechVoiceOver, "voice-over is not lip-sync");
        var voiceOverPlan = Plan(voiceOver, confirmed: true);
        Ok(NovixaShortProductionStrategyV1.LipsyncRefusal(voiceOverPlan) == NovixaShortProductionStrategyV1.VoiceOverForbidsLipsync, "voice-over forbids Fal");
        Ok(NovixaShortProductionStrategyV1.PaidMotionBlock(voiceOverPlan) == NovixaShortProductionStrategyV1.StaticForbidsPaidVideo, "voice-over still does not buy video");

        var movingVoice = Plan(NovixaShortProductionStrategyV1.Suggest(new(true, false, true, false)), confirmed: true);
        Ok(NovixaShortProductionStrategyV1.PaidMotionBlock(movingVoice) is null, "moving voice-over may reach the existing video gate");
        Ok(NovixaShortProductionStrategyV1.LipsyncRefusal(movingVoice) == NovixaShortProductionStrategyV1.VoiceOverForbidsLipsync, "moving voice-over still forbids Fal");

        var idleMouth = Plan(NovixaShortProductionStrategyV1.Suggest(new(true, true, true, false)), confirmed: true);
        Ok(idleMouth.SpeechDelivery == NovixaShortProductionStrategyV1.SpeechMouthUnsynced, "mouth movement is not word-level sync");
        Ok(NovixaShortProductionStrategyV1.LipsyncRefusal(idleMouth) == NovixaShortProductionStrategyV1.MouthUnsyncedForbidsLipsync, "unsynced mouth does not call Fal");

        var spoken = NovixaShortProductionStrategyV1.Suggest(new(true, true, true, true));
        Ok(spoken.Picture == NovixaShortProductionStrategyV1.PictureImageToVideo && spoken.Speech == NovixaShortProductionStrategyV1.SpeechLipSync, "motion and speech stay together");
        var spokenPlan = Plan(spoken, confirmed: true);
        Ok(NovixaShortProductionStrategyV1.LipsyncBlock(spokenPlan, false, true, true) == ProductionFailureReasons.MissingVideoArtifact, "lip-sync needs a take");
        Ok(NovixaShortProductionStrategyV1.LipsyncBlock(spokenPlan, true, false, true) == ProductionFailureReasons.MissingVoiceArtifact, "lip-sync needs a voice");
        Ok(NovixaShortProductionStrategyV1.LipsyncBlock(spokenPlan, true, true, false) == ProductionFailureReasons.LipsyncTargetUnresolved, "lip-sync needs a mouth target");
        Ok(NovixaShortProductionStrategyV1.LipsyncBlock(spokenPlan, true, true, true) is null, "lip-sync with take, voice, and mouth passes the strategy gate");

        var two = spokenPlan with { DirectSpeakerCount = 2 };
        Ok(NovixaShortProductionStrategyV1.LipsyncRefusal(two) == VideoAudioLipsyncPipelineV1Rules.GateMultiSpeaker, "two speakers stay unsupported");

        var graphics = confirmedStill with { PictureStrategy = NovixaShortProductionStrategyV1.PictureMotionGraphics };
        Ok(NovixaShortProductionStrategyV1.PaidMotionBlock(graphics) == NovixaShortProductionStrategyV1.MotionGraphicsUnsupported, "motion graphics has no renderer");

        var undeclared = new ShotExecutionInput("s", "a", "v", "c", "k", "t", "vr", "mr", PictureStrategy: NovixaShortProductionStrategyV1.PictureUndeclared, StrategyConfirmed: true);
        Ok(NovixaShortProductionStrategyV1.PaidMotionBlock(undeclared) == NovixaShortProductionStrategyV1.Undeclared, "undeclared plan does not start paid video");
        var legacy = new ShotExecutionInput("s", "a", "v", "c", "k", "t", "vr", "mr");
        Ok(NovixaShortProductionStrategyV1.PaidMotionBlock(legacy) is null, "empty plan keeps the existing contract");
        Ok(NovixaShortProductionStrategyV1.LipsyncRefusal(legacy) is null, "empty plan keeps the existing lip-sync contract");

        var allowed = ProductionMotionCostGuard.Evaluate("runway", "gen4_turbo", 5, true);
        Ok(allowed.Allowed, "known runway quote is allowed under the default ceiling");
        Ok(NovixaShortProductionStrategyV1.ImageToVideoBlock(spokenPlan, allowed) is null, "confirmed image-to-video continues when the cost guard allows");
        var unknown = ProductionMotionCostGuard.Evaluate("missing-provider", "none", 5, true);
        Ok(!unknown.Allowed && unknown.BlockReason == ProductionFailureReasons.ProviderDisabled, "missing provider is blocked before HTTP");
        Ok(NovixaShortProductionStrategyV1.ImageToVideoBlock(spokenPlan, unknown) == ProductionFailureReasons.ProviderDisabled, "strategy does not skip a missing quote");
        var over = ProductionMotionCostGuard.Evaluate("runway", "gen4_turbo", 5, true, serverMaxCredits: 1);
        Ok(!over.Allowed && over.BlockReason == ProductionFailureReasons.OverBudget, "over budget is blocked before HTTP");
        var wanQuote = FamixaProviderCostCatalog.Quote(FamixaProviderIds.Wan, "wan-2.1", 5);
        var wan = ProductionMotionCostGuard.EvaluateQuote(wanQuote);
        Ok(!wan.Allowed && wan.BlockReason == ProductionFailureReasons.UnknownCost, "null quote is unknown cost");

        var changedSpeech = spokenPlan with { SpeechDelivery = NovixaShortProductionStrategyV1.SpeechVoiceOver };
        Ok(NovixaShortProductionStrategyV1.SnapshotMaterial(spokenPlan) == NovixaShortProductionStrategyV1.SnapshotMaterial(changedSpeech), "strategy is outside the snapshot material");
        Ok(!NovixaShortProductionStrategyV1.SnapshotMaterial(spokenPlan).Contains(NovixaShortProductionStrategyV1.PictureImageToVideo, StringComparison.Ordinal), "snapshot material has no strategy label");

        var silent = VideoAudioLipsyncPipelineV1Rules.Preflight(new(
            "sh", true, [], true, false, MixReady: true, TakeDurationSec: 5, DirectorApproved: true));
        Ok(silent.FinalSource == "I2V_ALLOWED" && silent.FinalReady, "silent final source unchanged");
        var cue = new VideoAudioLipsyncPipelineV1Rules.DialogueCue("d", "sh", "CHAR-001", "Xin chào", "voice", 0.2, 2, "audio", 1.8, true);
        var one = VideoAudioLipsyncPipelineV1Rules.Preflight(new("sh", true, [cue], true, false, TakeDurationSec: 5));
        Ok(one.Blockers.Contains(VideoAudioLipsyncPipelineV1Rules.GateLipsyncRequired) && one.FinalSource == "RAW_I2V" && !one.FinalReady, "single speaker still requires lip-sync until a strategy says otherwise");
        var other = cue with { SpeakerCharacterId = "CHAR-002", DialogueId = "d2" };
        var many = VideoAudioLipsyncPipelineV1Rules.Preflight(new("sh", true, [cue, other], true, true, TakeDurationSec: 5));
        Ok(many.Blockers.Contains(VideoAudioLipsyncPipelineV1Rules.GateMultiSpeaker) && many.FinalSource == "NONE", "multi speaker final source unchanged");

        var shown = NovixaShortProductionStrategyV1.Apply(
            [new ProductionActionAvailability(ProductionActions.CreateVideo, "Video", true, null),
             new ProductionActionAvailability(ProductionActions.CreateLipsync, "Môi", true, null)],
            new CreateVideoReadinessClaim(PictureStrategy: NovixaShortProductionStrategyV1.PictureStatic, SpeechDelivery: NovixaShortProductionStrategyV1.SpeechVoiceOver, StrategyConfirmed: true));
        Ok(shown.Single(a => a.Action == ProductionActions.CreateVideo).Available is false, "action list hides paid video for static");
        Ok(shown.Single(a => a.Action == ProductionActions.CreateLipsync).BlockReason == NovixaShortProductionStrategyV1.VoiceOverForbidsLipsync, "action list hides lip-sync for voice-over");

        return fail;
    }

    static ShotExecutionInput Plan(NovixaShortProductionStrategyV1.Plan plan, bool confirmed = false) =>
        new("story", "acting", "visual", "char", "kf", "5s", "voice", "motion",
            PictureStrategy: plan.Picture,
            SpeechDelivery: plan.Speech,
            StrategyConfirmed: confirmed);
}
