using System.Text.RegularExpressions;
using KitPlatform.Packs.Content.Production.Legacy;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Read-only short acceptance ledger. Maps existing task, verification, and binding
/// evidence onto one stage. Does not persist a second status machine.
/// Identity role ACCEPTED is not a quality review. Series graph shotQa is not an input.
/// </summary>
public static class ShortProductionAcceptanceV1
{
    public const string SuiteId = "FAMIXA_SHORT_PRODUCTION_ACCEPTANCE_V1";
    public const string Generated = "GENERATED";
    public const string TechnicallyValid = "TECHNICALLY_VALID";
    public const string QualityReviewed = "QUALITY_REVIEWED";
    public const string Accepted = "ACCEPTED";
    public const string Rejected = "REJECTED";
    public const string DecisionAccept = "ACCEPT";
    public const string DecisionReject = "REJECT";
    public const string CrossShotArtifact = "CROSS_SHOT_ARTIFACT";
    public const string ActingSubjectContradictsStory = "ACTING_SUBJECT_CONTRADICTS_STORY";
    public const string SpeechDoesNotCoverPlayback = "SPEECH_DOES_NOT_COVER_PLAYBACK";
    public const string ReviewArtifactMismatch = "REVIEW_ARTIFACT_MISMATCH";
    public const string IdentityBindingIsNotQualityReview = "IDENTITY_BINDING_IS_NOT_QUALITY_REVIEW";

    public static bool IdentityBindingIsQualityReview() => false;

    public static bool ProviderSuccessIsQualityReview() =>
        ProductionStateMachines.ProviderSuccessIsAccepted();

    public static bool GraphChecklistIsQualityReview() =>
        LegacySeriesGraphAdapter.IsAuthoritative();

    public static ShortAcceptanceVerdict Classify(ShortAcceptanceEvidence evidence)
    {
        var blockers = new List<string>();
        var notes = new List<string>();
        var sameShot = SameShot(evidence.ArtifactShotCode, evidence.BindingShotCode);
        if (!string.IsNullOrWhiteSpace(evidence.ArtifactShotCode)
            && !string.IsNullOrWhiteSpace(evidence.BindingShotCode)
            && !sameShot)
            blockers.Add(CrossShotArtifact);
        if (ActingEntrantContradictsStory(evidence.Story, evidence.ActingBeat))
            blockers.Add(ActingSubjectContradictsStory);
        if (evidence.DirectDialogue
            && SpeechWindowDoesNotCoverPlayback(evidence.SpeechDurationSec, evidence.PlaybackDurationSec))
            blockers.Add(SpeechDoesNotCoverPlayback);

        var generated = string.Equals(evidence.TaskStatus, ProductionTaskStatuses.Succeeded, StringComparison.Ordinal);
        var technical = generated
            && string.Equals(evidence.VerificationStatus, ProductionArtifactStatuses.Verified, StringComparison.Ordinal)
            && evidence.DurationSec > 0
            && PlayableMime(evidence.MimeType)
            && sameShot
            && !string.IsNullOrWhiteSpace(evidence.ArtifactShotCode);
        var reviewTargetsThisPlayback = !string.IsNullOrWhiteSpace(evidence.QualityDecision)
            && !string.IsNullOrWhiteSpace(evidence.ReviewedArtifactId)
            && string.Equals(evidence.ReviewedArtifactId, evidence.PlaybackArtifactId, StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(evidence.QualityDecision)
            && !string.IsNullOrWhiteSpace(evidence.ReviewedArtifactId)
            && !reviewTargetsThisPlayback)
            blockers.Add(ReviewArtifactMismatch);
        if (evidence.IdentityBindingActive && !reviewTargetsThisPlayback)
            notes.Add(IdentityBindingIsNotQualityReview);

        if (!generated)
            return new ShortAcceptanceVerdict(ShotNodeStatuses.Missing, false, blockers, notes);
        if (!technical)
            return new ShortAcceptanceVerdict(Generated, false, blockers, notes);

        var decision = (evidence.QualityDecision ?? "").Trim();
        if (!reviewTargetsThisPlayback)
            return new ShortAcceptanceVerdict(TechnicallyValid, false, blockers, notes);
        if (string.Equals(decision, DecisionReject, StringComparison.OrdinalIgnoreCase))
            return new ShortAcceptanceVerdict(Rejected, false, blockers, notes);
        if (string.Equals(decision, DecisionAccept, StringComparison.OrdinalIgnoreCase) && blockers.Count == 0)
            return new ShortAcceptanceVerdict(Accepted, true, blockers, notes);
        return new ShortAcceptanceVerdict(QualityReviewed, false, blockers, notes);
    }

    public static bool ActingEntrantContradictsStory(string? story, string? acting)
    {
        var storyEntrant = Entrant(story, vietnamese: true);
        var actingEntrant = Entrant(acting, vietnamese: false) ?? Entrant(acting, vietnamese: true);
        if (storyEntrant is null || actingEntrant is null)
            return false;
        return !SamePerson(storyEntrant, actingEntrant);
    }

    public static bool SpeechWindowDoesNotCoverPlayback(decimal speechDurationSec, decimal playbackDurationSec) =>
        speechDurationSec > 0 && playbackDurationSec > 0 && playbackDurationSec - speechDurationSec > 0.75m;

    public static bool SameShot(string? artifactShotCode, string? bindingShotCode) =>
        !string.IsNullOrWhiteSpace(artifactShotCode)
        && string.Equals(artifactShotCode.Trim(), (bindingShotCode ?? "").Trim(), StringComparison.Ordinal);

    static bool PlayableMime(string? mime)
    {
        var value = (mime ?? "").Trim();
        return value.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("audio/", StringComparison.OrdinalIgnoreCase);
    }

    static string? Entrant(string? text, bool vietnamese)
    {
        var raw = text ?? "";
        var match = vietnamese
            ? Regex.Match(raw, @"(?i)((?:người\s+)?(?:mẹ|minh|linh|nam|an))\s+bước\s+vào")
            : Regex.Match(raw, @"(?i)\b(minh|linh|nam|an|mother)\s+entering\b");
        if (!match.Success) return null;
        return match.Groups[1].Value.Trim();
    }

    static bool SamePerson(string left, string right) =>
        CanonPerson(left) == CanonPerson(right);

    static string CanonPerson(string name)
    {
        var value = Regex.Replace(name.Trim().ToLowerInvariant(), @"^người\s+", "");
        return value is "mẹ" or "mother" or "linh" ? "linh" : value;
    }
}

public sealed record ShortAcceptanceEvidence(
    string? TaskStatus,
    string? VerificationStatus,
    string? MimeType,
    decimal DurationSec,
    string? ArtifactShotCode,
    string? BindingShotCode,
    bool IdentityBindingActive,
    string? QualityDecision,
    string? ReviewedArtifactId,
    string? PlaybackArtifactId,
    string? Story,
    string? ActingBeat,
    bool DirectDialogue,
    decimal SpeechDurationSec,
    decimal PlaybackDurationSec);

public sealed record ShortAcceptanceVerdict(
    string Stage,
    bool ProductionReady,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Notes);
