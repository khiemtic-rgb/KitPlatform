using System.Linq;

namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_CHARACTER_MASTER_REVISION_DIRECTOR_REVIEW_V1 — Director review
/// for Master Revision candidates. Does not call Gemini, auto-approve, auto-lock,
/// or branch on character name.
/// </summary>
public static class CharacterMasterRevisionDirectorReviewV1Rules
{
    public const string DocumentId = "FAMIXA_CHARACTER_MASTER_REVISION_DIRECTOR_REVIEW_V1";
    public const string SuiteId = "FAMIXA_CHARACTER_MASTER_REVISION_DIRECTOR_REVIEW_V1_REGRESSION";

    public const string ActionApprove = "APPROVE";
    public const string ActionReject = "REJECT";
    public const string ActionLock = "LOCK";

    public const string DecisionApprove = "APPROVED";
    public const string DecisionReject = "REJECTED";
    public const string DecisionLock = "LOCKED";

    public const string GateInvalidState = "BLOCK_INVALID_STATE";
    public const string GateNotApproved = "MASTER_REVISION_NOT_APPROVED";
    public const string GateCandidateNotFound = "CANDIDATE_NOT_FOUND";
    public const string GateCharacterMismatch = "CANDIDATE_CHARACTER_MISMATCH";
    public const string GateAuthorityChanged = "MASTER_AUTHORITY_CHANGED";
    public const string GateUnauthorized = "UNAUTHORIZED_REVIEW_ACTOR";
    public const string GateRejectReason = "REJECTION_REASON_REQUIRED";

    public static readonly string[] ForbiddenActors = ["system", "auto", "background", "kit"];

    public sealed record ReviewCommand(
        string? CharacterId,
        string? CandidateCharacterId,
        string? CandidateMasterSha,
        string? RevisionCurrentMasterSha,
        string? LiveAuthorityMasterSha,
        string? Status,
        string Action,
        string? Actor,
        string? Notes = null,
        string? RejectionReason = null);

    public sealed record ReviewAudit(
        string CharacterId,
        string? CandidateMasterId,
        string PreviousStatus,
        string NewStatus,
        string Actor,
        string Timestamp,
        string? CurrentAuthoritySha,
        string? CandidateSha,
        string Decision,
        string? Notes);

    public static bool MayReview(string? status, bool hasCandidate) =>
        hasCandidate && status == CharacterMasterRevisionV1Rules.StatusPendingReview;

    public static bool MayApprove(string? status) =>
        status == CharacterMasterRevisionV1Rules.StatusPendingReview;

    public static bool MayReject(string? status) =>
        status == CharacterMasterRevisionV1Rules.StatusPendingReview;

    public static bool MayLock(string? status) =>
        status == CharacterMasterRevisionV1Rules.StatusApproved;

    public static bool IsCurrentAuthority(string? revisionStatus, bool candidate) =>
        candidate
            ? revisionStatus == CharacterMasterRevisionV1Rules.StatusLocked
            : revisionStatus != CharacterMasterRevisionV1Rules.StatusLocked;

    public static bool ActorAllowed(string? actor)
    {
        var t = (actor ?? "").Trim();
        if (t.Length == 0) return false;
        return !ForbiddenActors.Contains(t, StringComparer.OrdinalIgnoreCase);
    }

    public static string? Evaluate(ReviewCommand command)
    {
        if (!ActorAllowed(command.Actor))
            return GateUnauthorized;
        if (string.IsNullOrWhiteSpace(command.CharacterId))
            return CharacterMasterRevisionV1Rules.GateCharacterNotFound;
        if (!string.Equals(
                CharacterStudioV1Rules.NormalizeCharacterId(command.CharacterId),
                CharacterStudioV1Rules.NormalizeCharacterId(command.CandidateCharacterId ?? ""),
                StringComparison.OrdinalIgnoreCase))
            return GateCharacterMismatch;
        if (!CharacterReferencePackRules.ShaExists(command.CandidateMasterSha))
            return GateCandidateNotFound;
        var action = (command.Action ?? "").Trim().ToUpperInvariant();
        var status = command.Status ?? "";
        if (action == ActionLock)
        {
            if (!CharacterReferencePackRules.ShaExists(command.LiveAuthorityMasterSha)
                || !CharacterReferencePackRules.ShaExists(command.RevisionCurrentMasterSha)
                || !string.Equals(
                    command.LiveAuthorityMasterSha, command.RevisionCurrentMasterSha,
                    StringComparison.OrdinalIgnoreCase))
                return GateAuthorityChanged;
        }
        if (action == ActionLock && status != CharacterMasterRevisionV1Rules.StatusApproved)
            return status == CharacterMasterRevisionV1Rules.StatusPendingReview
                || status == CharacterMasterRevisionV1Rules.StatusRejected
                ? GateNotApproved
                : GateInvalidState;
        if (action == ActionApprove && !MayApprove(status))
            return GateInvalidState;
        if (action == ActionReject && !MayReject(status))
            return GateInvalidState;
        if (action == ActionReject && string.IsNullOrWhiteSpace(command.RejectionReason))
            return GateRejectReason;
        if (action is not ActionApprove and not ActionReject and not ActionLock)
            return GateInvalidState;
        return null;
    }

    public static CharacterMasterRevisionV1Rules.AuthorityState Apply(
        CharacterMasterRevisionV1Rules.AuthorityState state, string action)
    {
        var code = Evaluate(new ReviewCommand(
            "CHAR-099", "CHAR-099",
            state.CandidateMasterSha ?? new string('c', 64),
            state.CurrentMasterSha,
            state.CurrentMasterSha,
            state.Status, action, "director", null,
            action == ActionReject ? "Vẫn quá già" : null));
        if (code is not null)
            return state;
        return action switch
        {
            ActionApprove => CharacterMasterRevisionV1Rules.ApplyApprove(state),
            ActionReject => CharacterMasterRevisionV1Rules.ApplyReject(state),
            ActionLock => CharacterMasterRevisionV1Rules.ApplyLock(state),
            _ => state,
        };
    }

    public static ReviewAudit Audit(
        string characterId,
        string? candidateId,
        string previous,
        string next,
        string actor,
        string? authoritySha,
        string? candidateSha,
        string decision,
        string? notes) =>
        new(characterId, candidateId, previous, next, actor,
            DateTimeOffset.UtcNow.ToString("O"), authoritySha, candidateSha, decision, notes);

    public static string ConcurrentLoser(string firstAction, string secondAction, string status)
    {
        var first = Evaluate(new ReviewCommand(
            "CHAR-099", "CHAR-099", new string('c', 64), new string('a', 64), new string('a', 64),
            status, firstAction, "director", null, firstAction == ActionReject ? "Vẫn quá già" : null));
        if (first is not null)
            return first;
        var after = Apply(
            new CharacterMasterRevisionV1Rules.AuthorityState(
                new string('a', 64), new string('c', 64), status, "LOCKED", "LOCKED", "PENDING"),
            firstAction);
        return Evaluate(new ReviewCommand(
            "CHAR-099", "CHAR-099", new string('c', 64), new string('a', 64), new string('a', 64),
            after.Status, secondAction, "director-b", null,
            secondAction == ActionReject ? "Vẫn quá già" : null)) ?? "";
    }

    public static bool UsesCharacterName() => false;
    public static bool UsesCharacterSpecificBranch() => false;
    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool AutoGenerateDownstream() => false;
    public static bool CallsGemini() => false;
    public static bool CallsGeminiOnApprove() => false;
    public static bool CallsGeminiOnReject() => false;
    public static bool CallsGeminiOnLock() => false;
}

public sealed record CharacterMasterRevisionReviewRequestDto(
    string? EraId = null,
    string? Notes = null,
    string? RejectionReason = null);
