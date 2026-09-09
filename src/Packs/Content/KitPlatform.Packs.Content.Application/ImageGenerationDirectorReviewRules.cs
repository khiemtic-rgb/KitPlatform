using System.Linq;

namespace KitPlatform.Packs.Content;

/// <summary>PRODUCTION_IMAGE_DIRECTOR_REVIEW_V1 — Director views the real still. No provider. No auto-approve.</summary>
public static class ImageGenerationDirectorReviewRules
{
    public const string DocumentId = "PRODUCTION_IMAGE_DIRECTOR_REVIEW_V1";

    public static readonly string[] Statuses = ["PENDING", "APPROVED", "REJECTED", "SUPERSEDED"];
    public static readonly string[] ExecutionReady = ["READY_FOR_DIRECTOR"];

    public static bool AutoApprove() => false;
    public static bool AutoFix() => false;
    public static bool CallsProvider() => false;
    public static bool AllowsGemini() => false;
    public static bool AllowsRunway() => false;
    public static bool TouchesGolden(string? path) => ImageGenerationExecutionRules.TouchesGolden(path);
    public static bool AllowsArtifactMutation(string? reviewStatus) =>
        reviewStatus is null or "" or "PENDING";

    public sealed record Block(
        string Status,
        string Code,
        string Source,
        string Attribute,
        string? Requested,
        string? Authoritative,
        string Message);

    public sealed record ReviewInput(
        string ExecutionStatus,
        string DirectorApproval,
        bool MasterLocked,
        bool DnaLocked,
        bool PrpLocked,
        string ShotContractStatus,
        string PromptStatus,
        string IgcStatus,
        string TechnicalQa,
        string CharacterQa,
        string IdentityQa,
        string ContinuityQa,
        string CompositionQa,
        int P0,
        bool ArtifactExists,
        bool ArtifactReadable,
        string ExecutionMasterSha,
        string LiveMasterSha,
        string ExecutionDnaSha,
        string LiveDnaSha,
        string ExecutionPrpSha,
        string LivePrpSha,
        string ExecutionShotContractSha,
        string LiveShotContractSha,
        string ExecutionPromptSha,
        string LivePromptSha,
        string ExecutionIgcSha,
        string LiveIgcSha,
        string StoredArtifactSha,
        string LiveArtifactSha,
        string? RejectionReason = null);

    public sealed record ReviewOutput(
        string Status,
        IReadOnlyList<Block> Blocks,
        bool CanApprove,
        bool CanReject,
        bool Generation = false);

    public static bool IsImageApproved(string? status) =>
        status is "IMAGE_APPROVED" or "APPROVED";
    public static bool IsImageRejected(string? status) =>
        status is "IMAGE_REJECTED" or "REJECTED";
    public static bool IsPending(string? approval) =>
        string.IsNullOrWhiteSpace(approval) || approval.Equals("PENDING", StringComparison.OrdinalIgnoreCase);

    public static ReviewOutput Evaluate(ReviewInput input)
    {
        var blocks = new List<Block>();
        void Need(bool ok, string code, string source, string attribute, string? requested, string? authoritative, string message)
        {
            if (!ok)
                blocks.Add(new Block("BLOCKED", code, source, attribute, requested, authoritative, message));
        }

        var readyCode = input.ExecutionStatus is "PROCESSING" or "SUCCEEDED" or "DRAFT" or "REQUESTED" or "ACCEPTED"
            ? "IMAGE_DIRECTOR_REVIEW_NOT_READY"
            : "IMAGE_DIRECTOR_REVIEW_NOT_READY";
        Need(input.ExecutionStatus == "READY_FOR_DIRECTOR", readyCode, "EXECUTION", "status", input.ExecutionStatus, "READY_FOR_DIRECTOR", "Execution must be READY_FOR_DIRECTOR.");
        Need(IsPending(input.DirectorApproval), "IMAGE_DIRECTOR_REVIEW_NOT_READY", "DIRECTOR", "directorApproval", input.DirectorApproval, "PENDING", "Director approval is not PENDING.");
        Need(input.MasterLocked, "IMAGE_DIRECTOR_REVIEW_NOT_READY", "MASTER", "master", "MISSING", "LOCKED", "Master must be LOCKED.");
        Need(input.DnaLocked, "IMAGE_DIRECTOR_REVIEW_NOT_READY", "CHARACTER_DNA", "dna", "MISSING", "LOCKED", "DNA must be LOCKED.");
        Need(input.PrpLocked, "IMAGE_DIRECTOR_REVIEW_NOT_READY", "PRODUCTION_REFERENCE_PACK", "prp", "MISSING", "LOCKED", "PRP must be LOCKED.");
        Need(ProductionShotContractRules.IsApproved(input.ShotContractStatus), "IMAGE_DIRECTOR_REVIEW_NOT_READY", "SHOT_CONTRACT", "status", input.ShotContractStatus, "DIRECTOR_APPROVED", "Shot Contract must stay DIRECTOR_APPROVED.");
        Need(string.Equals(input.PromptStatus, "COMPILED", StringComparison.OrdinalIgnoreCase), "IMAGE_DIRECTOR_REVIEW_NOT_READY", "PROMPT", "status", input.PromptStatus, "COMPILED", "Prompt must stay COMPILED.");
        Need(ImageGenerationContractRules.IsApproved(input.IgcStatus), "IMAGE_DIRECTOR_REVIEW_NOT_READY", "IMAGE_GENERATION_CONTRACT", "status", input.IgcStatus, "DIRECTOR_APPROVED", "IGC must stay DIRECTOR_APPROVED.");
        Need(input.TechnicalQa == "PASS", "IMAGE_DIRECTOR_REVIEW_NOT_READY", "QA", "technical", input.TechnicalQa, "PASS", "Technical QA must PASS.");
        Need(input.CharacterQa == "PASS", "IMAGE_DIRECTOR_REVIEW_NOT_READY", "QA", "character", input.CharacterQa, "PASS", "Character QA must PASS.");
        Need(input.IdentityQa == "PASS", "IMAGE_DIRECTOR_REVIEW_NOT_READY", "QA", "identity", input.IdentityQa, "PASS", "Identity QA must PASS.");
        Need(input.ContinuityQa == "PASS", "IMAGE_DIRECTOR_REVIEW_NOT_READY", "QA", "continuity", input.ContinuityQa, "PASS", "Continuity QA must PASS.");
        Need(input.CompositionQa == "PASS", "IMAGE_DIRECTOR_REVIEW_NOT_READY", "QA", "composition", input.CompositionQa, "PASS", "Composition QA must PASS.");
        Need(input.P0 == 0, "IMAGE_DIRECTOR_REVIEW_NOT_READY", "QA", "p0", input.P0.ToString(), "0", "P0 must be 0.");
        Need(input.ArtifactExists && input.ArtifactReadable, "IMAGE_DIRECTOR_REVIEW_NOT_READY", "ARTIFACT", "artifact", "missing", "readable", "Artifact missing or unreadable.");

        void Sha(string source, string stored, string live)
        {
            if (!CharacterIdentityGovernanceRules.ShaExists(stored) || !CharacterIdentityGovernanceRules.ShaExists(live)
                || !CharacterIdentityGovernanceRules.SameSha(stored, live))
                blocks.Add(new Block("BLOCKED", "IMAGE_DIRECTOR_REVIEW_BLOCKED", source, "sha256", stored, live,
                    "Production artifact provenance is no longer valid. Director approval is blocked."));
        }
        Sha("MASTER", input.ExecutionMasterSha, input.LiveMasterSha);
        Sha("CHARACTER_DNA", input.ExecutionDnaSha, input.LiveDnaSha);
        Sha("PRODUCTION_REFERENCE_PACK", input.ExecutionPrpSha, input.LivePrpSha);
        Sha("SHOT_CONTRACT", input.ExecutionShotContractSha, input.LiveShotContractSha);
        Sha("PROMPT", input.ExecutionPromptSha, input.LivePromptSha);
        Sha("IMAGE_GENERATION_CONTRACT", input.ExecutionIgcSha, input.LiveIgcSha);
        Sha("ARTIFACT", input.StoredArtifactSha, input.LiveArtifactSha);

        var canApprove = blocks.Count == 0;
        var canReject = input.ExecutionStatus == "READY_FOR_DIRECTOR" && IsPending(input.DirectorApproval);
        return new ReviewOutput(canApprove ? "READY" : "BLOCKED", blocks, canApprove, canReject, false);
    }

    public static bool RejectReasonRequired(string? reason) =>
        !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length >= 3;
}
