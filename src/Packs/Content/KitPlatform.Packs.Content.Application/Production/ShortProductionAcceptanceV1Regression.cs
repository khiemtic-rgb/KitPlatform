using KitPlatform.Packs.Content.Production.Legacy;

namespace KitPlatform.Packs.Content.Production;

public static class ShortProductionAcceptanceV1Regression
{
    public const string SuiteId = ShortProductionAcceptanceV1.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        Ok(!ShortProductionAcceptanceV1.IdentityBindingIsQualityReview(), "T01 identity binding is not quality");
        Ok(!ShortProductionAcceptanceV1.ProviderSuccessIsQualityReview(), "T02 provider success is not quality");
        Ok(!ShortProductionAcceptanceV1.GraphChecklistIsQualityReview(), "T03 graph checklist is not quality");
        Ok(LegacySeriesGraphAdapter.NonAuthoritativeFields.Contains("SeriesShotRun.shotQa"), "T03 shotQa listed");
        Ok(LegacySeriesGraphAdapter.NonAuthoritativeFields.Contains("SeriesShotRun.visualQa"), "T03 visualQa listed");
        Ok(!LegacySeriesGraphAdapter.MayDriveNewBinding("SeriesShotRun.shotQa"), "T03 shotQa cannot bind");

        var sh01Story = ProductionEp01Sh01IngressCanon.Story;
        var sh01Acting = ProductionEp01Sh01IngressCanon.ActingBeat;
        Ok(ShortProductionAcceptanceV1.ActingEntrantContradictsStory(sh01Story, sh01Acting), "T04 SH01 acting entrant contradicts story");
        Ok(!ShortProductionAcceptanceV1.ActingEntrantContradictsStory(
            "Người mẹ bước vào.",
            "Audience must see Linh entering the room."), "T05 mother and Linh are the same entrant");
        Ok(ShortProductionAcceptanceV1.SpeechWindowDoesNotCoverPlayback(2.5m, 8m), "T06 SH02 speech window");
        Ok(!ShortProductionAcceptanceV1.SpeechWindowDoesNotCoverPlayback(5.042m, 5.042m), "T07 equal window is covered");

        var generated = ShortProductionAcceptanceV1.Classify(Base() with
        {
            VerificationStatus = ProductionArtifactStatuses.Unverified,
        });
        Ok(generated.Stage == ShortProductionAcceptanceV1.Generated && !generated.ProductionReady, "T08 succeeded unverified is GENERATED");

        var technical = ShortProductionAcceptanceV1.Classify(Base());
        Ok(technical.Stage == ShortProductionAcceptanceV1.TechnicallyValid
            && !technical.ProductionReady
            && technical.Notes.Contains(ShortProductionAcceptanceV1.IdentityBindingIsNotQualityReview),
            "T09 verified identity binding stays TECHNICALLY_VALID");

        var cross = ShortProductionAcceptanceV1.Classify(Base() with { BindingShotCode = "EP01-SC01-SH02" });
        Ok(cross.Stage == ShortProductionAcceptanceV1.Generated
            && cross.Blockers.Contains(ShortProductionAcceptanceV1.CrossShotArtifact),
            "T10 cross-shot artifact is not technically valid");

        var reviewed = ShortProductionAcceptanceV1.Classify(Clean() with
        {
            QualityDecision = ShortProductionAcceptanceV1.DecisionAccept,
            ReviewedArtifactId = "art-1",
            PlaybackArtifactId = "art-1",
            Story = sh01Story,
            ActingBeat = sh01Acting,
        });
        Ok(reviewed.Stage == ShortProductionAcceptanceV1.QualityReviewed && !reviewed.ProductionReady, "T11 contradiction blocks ACCEPTED");

        var rejected = ShortProductionAcceptanceV1.Classify(Clean() with
        {
            QualityDecision = ShortProductionAcceptanceV1.DecisionReject,
            ReviewedArtifactId = "art-1",
            PlaybackArtifactId = "art-1",
        });
        Ok(rejected.Stage == ShortProductionAcceptanceV1.Rejected && !rejected.ProductionReady, "T12 REJECT");

        var mismatch = ShortProductionAcceptanceV1.Classify(Clean() with
        {
            QualityDecision = ShortProductionAcceptanceV1.DecisionAccept,
            ReviewedArtifactId = "art-old",
            PlaybackArtifactId = "art-1",
        });
        Ok(mismatch.Stage == ShortProductionAcceptanceV1.TechnicallyValid
            && mismatch.Blockers.Contains(ShortProductionAcceptanceV1.ReviewArtifactMismatch),
            "T13 review of another artifact does not accept this playback");

        var ready = ShortProductionAcceptanceV1.Classify(Clean() with
        {
            QualityDecision = ShortProductionAcceptanceV1.DecisionAccept,
            ReviewedArtifactId = "art-1",
            PlaybackArtifactId = "art-1",
        });
        Ok(ready.Stage == ShortProductionAcceptanceV1.Accepted && ready.ProductionReady, "T14 matching review can accept");

        var speech = ShortProductionAcceptanceV1.Classify(Clean() with
        {
            QualityDecision = ShortProductionAcceptanceV1.DecisionAccept,
            ReviewedArtifactId = "art-1",
            PlaybackArtifactId = "art-1",
            DirectDialogue = true,
            SpeechDurationSec = 2.5m,
            PlaybackDurationSec = 8m,
        });
        Ok(speech.Stage == ShortProductionAcceptanceV1.QualityReviewed
            && speech.Blockers.Contains(ShortProductionAcceptanceV1.SpeechDoesNotCoverPlayback),
            "T15 speech window blocks production accept");

        return fail;
    }

    static ShortAcceptanceEvidence Base() => new(
        ProductionTaskStatuses.Succeeded,
        ProductionArtifactStatuses.Verified,
        "video/mp4",
        5.042m,
        "EP01-SC01-SH01",
        "EP01-SC01-SH01",
        true,
        null,
        null,
        "art-1",
        "Minh đứng yên.",
        "Minh looks toward Linh.",
        false,
        0,
        5.042m);

    static ShortAcceptanceEvidence Clean() => Base() with { IdentityBindingActive = false };
}
