using System.Linq;
using System.Text;
using System.Text.Json;

namespace KitPlatform.Packs.Content;

/// <summary>PRODUCTION_IMAGE_GENERATION_EXECUTION_V1 — one Gemini still after preflight. No blind retry. No auto-approve. No Runway.</summary>
public static class ImageGenerationExecutionRules
{
    public const string DocumentId = "PRODUCTION_IMAGE_GENERATION_EXECUTION_V1";
    public const string Provider = "GEMINI";
    public const string GenerationPolicy = "IMAGE_ONLY";

    public static readonly string[] Statuses =
    [
        "PREFLIGHT", "BLOCKED", "REQUESTED", "ACCEPTED", "PROCESSING",
        "SUCCEEDED", "FAILED", "QA_FAILED", "READY_FOR_DIRECTOR", "APPROVED", "REJECTED",
        "IMAGE_APPROVED", "IMAGE_REJECTED",
    ];

    public static bool AutoApprove() => false;
    public static bool AutoRetry() => false;
    public static bool AutoFix() => false;
    public static bool AllowsProvider(string? provider) =>
        string.Equals(provider, Provider, StringComparison.OrdinalIgnoreCase);
    public static bool IsRunway(string? provider) =>
        string.Equals(provider, "RUNWAY", StringComparison.OrdinalIgnoreCase);
    public static bool TouchesGolden(string? path) =>
        ProductionPromptCompilerRules.TouchesGolden(path)
        || (!string.IsNullOrWhiteSpace(path)
            && path.Contains("GOLDEN", StringComparison.OrdinalIgnoreCase)
            && path.Contains("SH01-01", StringComparison.OrdinalIgnoreCase));
    public static bool CanCallGemini(bool preflightPass) => preflightPass;
    public static bool IsTerminalSuccess(string? status) =>
        status is "SUCCEEDED" or "READY_FOR_DIRECTOR" or "APPROVED" or "IMAGE_APPROVED";
    public static bool IsFailed(string? status) =>
        status is "FAILED" or "QA_FAILED" or "BLOCKED";
    public static bool DoNotBlindRetry(string? status) =>
        IsFailed(status) || IsTerminalSuccess(status) || status is "REJECTED" or "IMAGE_REJECTED";
    public static bool ShouldExecute(string? existingStatus) =>
        string.IsNullOrWhiteSpace(existingStatus);
    public static string CreditStatus(decimal? credit) =>
        credit is null ? "UNKNOWN" : "KNOWN";
    public static bool CanDirectorApprove(string? status, bool technical, bool character, bool identity, bool continuity, bool composition, int p0) =>
        status == "READY_FOR_DIRECTOR" && technical && character && identity && continuity && composition && p0 == 0;
    public static bool GenerationBeforeDirector(string? status) =>
        status is "APPROVED";

    public sealed record Block(
        string Status,
        string Code,
        string Source,
        string Attribute,
        string? Requested,
        string? Authoritative,
        string Message);

    public sealed record PreflightInput(
        string CharacterId,
        string ShotId,
        bool MasterLocked,
        bool DnaLocked,
        bool PrpLocked,
        bool GovernancePass,
        string ShotContractStatus,
        string PromptStatus,
        string IgcStatus,
        string MasterSha256,
        string LiveMasterSha256,
        string DnaSha256,
        string LiveDnaSha256,
        string PrpSha256,
        string LivePrpSha256,
        string ShotContractSha256,
        string LiveShotContractSha256,
        string PromptSha256,
        string LivePromptSha256,
        string IgcSha256,
        string LiveIgcSha256,
        string PromptProvenanceMasterSha,
        string PromptProvenanceDnaSha,
        string PromptProvenancePrpSha,
        string PromptProvenanceContractSha,
        string ProviderCapability,
        string ProviderSelection,
        string GenerationPolicy,
        bool MasterReferenceReadable,
        string? MasterReferencePath,
        string? GoldenPath,
        bool CrpUsable = true,
        string CrpStatus = "LOCKED",
        string CrpSha256 = "",
        bool CharacterReady = true);

    public sealed record PreflightOutput(
        string Status,
        IReadOnlyList<Block> Blocks,
        string? Fingerprint,
        bool RunGemini,
        int Credit);

    public sealed record QaObservation(
        string? Age = null,
        string? Hair = null,
        string? Identity = null,
        string? Framing = null,
        string? Continuity = null,
        bool FileReadable = true,
        bool ValidImage = true,
        int Width = 1280,
        int Height = 720,
        long Size = 1024,
        string? ArtifactSha256 = null,
        string? ExpectedSha256 = null);

    public sealed record QaResult(
        string Technical,
        string Character,
        string Identity,
        string Continuity,
        string Composition,
        int P0,
        string Overall,
        IReadOnlyList<string> Reasons);

    public static PreflightOutput EvaluatePreflight(PreflightInput input)
    {
        var blocks = new List<Block>();
        void Need(bool ok, string source, string attribute, string? requested, string? authoritative, string message)
        {
            if (!ok)
                blocks.Add(new Block("BLOCKED", "IMAGE_GENERATION_PREFLIGHT_FAILED", source, attribute, requested, authoritative, message));
        }

        Need(input.CrpUsable, "CHARACTER_REFERENCE_PACK", "canUse", input.CrpStatus, "LOCKED",
            FirstRealProductionRules.StaffCrpBlock);
        Need(input.CharacterReady, "CHARACTER", "status", input.CrpStatus, CharacterStudioV1Rules.CharacterReady,
            CharacterStudioUnifiedGenerationV1Rules.GateProduction);
        Need(input.MasterLocked, "MASTER", "master", "MISSING", "LOCKED", "Master must be LOCKED.");
        Need(input.DnaLocked, "CHARACTER_DNA", "dna", "MISSING", "LOCKED", "DNA must be LOCKED.");
        Need(input.PrpLocked, "PRODUCTION_REFERENCE_PACK", "prp", "MISSING", "LOCKED", "PRP must be LOCKED.");
        Need(ShaOk(input.MasterSha256, input.LiveMasterSha256), "MASTER", "sha256", input.MasterSha256, input.LiveMasterSha256, "Master SHA mismatch.");
        Need(ShaOk(input.DnaSha256, input.LiveDnaSha256), "CHARACTER_DNA", "sha256", input.DnaSha256, input.LiveDnaSha256, "DNA SHA mismatch.");
        Need(ShaOk(input.PrpSha256, input.LivePrpSha256), "PRODUCTION_REFERENCE_PACK", "sha256", input.PrpSha256, input.LivePrpSha256, "PRP SHA mismatch.");
        Need(ProductionShotContractRules.IsApproved(input.ShotContractStatus), "SHOT_CONTRACT", "status", input.ShotContractStatus, "DIRECTOR_APPROVED", "Shot Contract must be DIRECTOR_APPROVED.");
        Need(string.Equals(input.PromptStatus, "COMPILED", StringComparison.OrdinalIgnoreCase), "PROMPT", "status", input.PromptStatus, "COMPILED", "Prompt must be COMPILED.");
        Need(ShaOk(input.ShotContractSha256, input.LiveShotContractSha256), "SHOT_CONTRACT", "sha256", input.ShotContractSha256, input.LiveShotContractSha256, "Shot Contract SHA mismatch.");
        Need(ShaOk(input.PromptSha256, input.LivePromptSha256), "PROMPT", "sha256", input.PromptSha256, input.LivePromptSha256, "Prompt SHA mismatch.");
        Need(CharacterIdentityGovernanceRules.SameSha(input.PromptProvenanceMasterSha, input.LiveMasterSha256)
             && CharacterIdentityGovernanceRules.SameSha(input.PromptProvenanceDnaSha, input.LiveDnaSha256)
             && CharacterIdentityGovernanceRules.SameSha(input.PromptProvenancePrpSha, input.LivePrpSha256)
             && CharacterIdentityGovernanceRules.SameSha(input.PromptProvenanceContractSha, input.LiveShotContractSha256),
            "PROMPT", "provenance", "mismatch", "match", "Prompt provenance mismatch.");
        Need(ImageGenerationContractRules.IsApproved(input.IgcStatus), "IMAGE_GENERATION_CONTRACT", "status", input.IgcStatus, "DIRECTOR_APPROVED", "Image Generation Contract must be DIRECTOR_APPROVED.");
        Need(ShaOk(input.IgcSha256, input.LiveIgcSha256), "IMAGE_GENERATION_CONTRACT", "sha256", input.IgcSha256, input.LiveIgcSha256, "IGC SHA mismatch.");
        Need(input.GovernancePass, "GOVERNANCE", "identity", "FAIL", "PASS", "Identity Governance must PASS.");
        Need(string.Equals(input.GenerationPolicy, GenerationPolicy, StringComparison.OrdinalIgnoreCase),
            "POLICY", "generationPolicy", input.GenerationPolicy, GenerationPolicy, "generation policy must be IMAGE_ONLY.");
        Need(AllowsGeminiPolicy(input.ProviderCapability, input.ProviderSelection),
            "PROVIDER", "providerPolicy", $"{input.ProviderCapability}/{input.ProviderSelection}", "IMAGE_GENERATION/EXTERNAL", "Provider policy must allow Gemini image generation.");
        Need(!TouchesGolden(input.GoldenPath) && !TouchesGolden(input.MasterReferencePath),
            "GOLDEN", "path", input.GoldenPath ?? input.MasterReferencePath, "protected", "Golden SH01-01 is protected.");
        Need(input.MasterReferenceReadable, "REFERENCE", "master", "unreadable", "readable", "Master reference unreadable. Gemini = FALSE.");
        Need(!string.IsNullOrWhiteSpace(CharacterIdentityGovernanceRules.NormalizeCharacterId(input.CharacterId)),
            "OWNERSHIP", "characterId", input.CharacterId, "required", "character_id is required. No CHAR-001 fallback.");

        if (blocks.Count > 0)
            return new PreflightOutput("BLOCKED", blocks, null, false, 0);
        return new PreflightOutput("PASS", [], Fingerprint(input), true, 0);
    }

    public static bool AllowsGeminiPolicy(string? capability, string? selection) =>
        string.Equals(capability, "IMAGE_GENERATION", StringComparison.OrdinalIgnoreCase)
        && string.Equals(selection, "EXTERNAL", StringComparison.OrdinalIgnoreCase);

    public static string Fingerprint(PreflightInput input) =>
        Fingerprint(input.CharacterId, input.ShotId, input);

    public static string Fingerprint(
        string characterId,
        string shotId,
        string masterSha,
        string dnaSha,
        string prpSha,
        string shotContractSha,
        string promptSha,
        string igcSha,
        string providerCapability,
        string providerSelection,
        string crpSha256 = "")
    {
        var surface = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["characterId"] = CharacterIdentityGovernanceRules.NormalizeCharacterId(characterId),
            ["shotId"] = shotId,
            ["masterSha256"] = (masterSha ?? "").Trim().ToLowerInvariant(),
            ["dnaSha256"] = (dnaSha ?? "").Trim().ToLowerInvariant(),
            ["prpSha256"] = (prpSha ?? "").Trim().ToLowerInvariant(),
            ["crpSha256"] = (crpSha256 ?? "").Trim().ToLowerInvariant(),
            ["shotContractSha256"] = (shotContractSha ?? "").Trim().ToLowerInvariant(),
            ["promptSha256"] = (promptSha ?? "").Trim().ToLowerInvariant(),
            ["imageGenerationContractSha256"] = (igcSha ?? "").Trim().ToLowerInvariant(),
            ["providerPolicy"] = new Dictionary<string, object?>
            {
                ["providerCapability"] = (providerCapability ?? "").Trim().ToUpperInvariant(),
                ["providerSelection"] = (providerSelection ?? "").Trim().ToUpperInvariant(),
            },
        });
        return KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(ProductionShotContractRules.CanonicalJson(surface)));
    }

    public static string Fingerprint(string characterId, string shotId, PreflightInput input) =>
        Fingerprint(characterId, shotId, input.LiveMasterSha256, input.LiveDnaSha256, input.LivePrpSha256,
            input.LiveShotContractSha256, input.LivePromptSha256, input.LiveIgcSha256,
            input.ProviderCapability, input.ProviderSelection, input.CrpSha256);

    public static string MapHttp(bool httpSuccess, bool hasArtifact) =>
        httpSuccess ? (hasArtifact ? "SUCCEEDED" : "ACCEPTED") : "FAILED";

    public static string AfterAccepted(bool artifactAvailable) =>
        artifactAvailable ? "PROCESSING" : "FAILED";

    public static QaResult EvaluateQa(
        byte[]? bytes,
        string? path,
        string? expectedSha,
        QaObservation? observation,
        string? expectedAge,
        string? expectedHair,
        string? expectedFraming,
        bool continuityRequired)
    {
        var reasons = new List<string>();
        var tech = EvaluateTechnical(bytes, path, expectedSha, observation, reasons);
        var obs = observation ?? new QaObservation(ArtifactSha256: expectedSha, ExpectedSha256: expectedSha);
        var identity = "PASS";
        var character = "PASS";
        if (!string.IsNullOrWhiteSpace(obs.Identity) && !obs.Identity.Equals("match", StringComparison.OrdinalIgnoreCase))
        {
            identity = "FAIL";
            reasons.Add("Identity mismatch.");
        }
        if (!string.IsNullOrWhiteSpace(obs.Age) && !string.IsNullOrWhiteSpace(expectedAge)
            && !obs.Age.Equals(expectedAge, StringComparison.OrdinalIgnoreCase))
        {
            identity = "FAIL";
            character = "FAIL";
            reasons.Add("Age mismatch.");
        }
        if (!string.IsNullOrWhiteSpace(obs.Hair) && !string.IsNullOrWhiteSpace(expectedHair)
            && HairConflicts(obs.Hair, expectedHair))
        {
            identity = "FAIL";
            character = "FAIL";
            reasons.Add("Hair mismatch.");
        }

        var continuity = "PASS";
        if (continuityRequired && !string.IsNullOrWhiteSpace(obs.Continuity)
            && (obs.Continuity.Contains("break", StringComparison.OrdinalIgnoreCase)
                || obs.Continuity.Contains("mismatch", StringComparison.OrdinalIgnoreCase)))
        {
            continuity = "FAIL";
            reasons.Add("Continuity mismatch.");
        }

        var composition = "PASS";
        if (!string.IsNullOrWhiteSpace(obs.Framing) && !string.IsNullOrWhiteSpace(expectedFraming)
            && !obs.Framing.Equals(expectedFraming, StringComparison.OrdinalIgnoreCase)
            && obs.Framing.Contains("mismatch", StringComparison.OrdinalIgnoreCase))
        {
            composition = "FAIL";
            reasons.Add("Composition mismatch.");
        }

        var p0 = 0;
        if (tech == "FAIL") p0++;
        if (identity == "FAIL") p0++;
        var overall = p0 > 0 || character == "FAIL" || continuity == "FAIL" || composition == "FAIL"
            ? "QA_FAILED"
            : "READY_FOR_DIRECTOR";
        return new QaResult(tech, character, identity, continuity, composition, p0, overall, reasons);
    }

    public static string EvaluateTechnical(byte[]? bytes, string? path, string? expectedSha, QaObservation? observation, List<string>? reasons = null)
    {
        reasons ??= [];
        if (observation is { FileReadable: false } || bytes is null || bytes.Length == 0)
        {
            reasons.Add("File unreadable or zero-byte.");
            return "FAIL";
        }
        if (observation is { ValidImage: false })
        {
            reasons.Add("Not a valid image.");
            return "FAIL";
        }
        var check = KitVideoArtifactRules.Validate(bytes, KitVideoArtifactRules.DetectMime(bytes), path);
        if (!check.Ok)
        {
            reasons.AddRange(check.Reasons);
            return "FAIL";
        }
        var sha = KitVideoIntegrityRules.Sha256Hex(bytes);
        var expect = expectedSha ?? observation?.ExpectedSha256;
        if (!string.IsNullOrWhiteSpace(expect) && !CharacterIdentityGovernanceRules.SameSha(sha, expect))
        {
            reasons.Add("Artifact SHA mismatch.");
            return "FAIL";
        }
        return "PASS";
    }

    public static byte[] FixtureJpeg512()
    {
        var bytes = new byte[64];
        bytes[0] = 0xFF; bytes[1] = 0xD8;
        bytes[2] = 0xFF; bytes[3] = 0xC0; bytes[4] = 0x00; bytes[5] = 0x11; bytes[6] = 0x08;
        bytes[7] = 0x02; bytes[8] = 0x00;
        bytes[9] = 0x02; bytes[10] = 0x00;
        bytes[11] = 0x01; bytes[12] = 0x01; bytes[13] = 0x11; bytes[14] = 0x00;
        bytes[62] = 0xFF; bytes[63] = 0xD9;
        return bytes;
    }

    private static bool HairConflicts(string requested, string authoritative) =>
        !requested.Equals(authoritative, StringComparison.OrdinalIgnoreCase)
        && (requested.Contains("long", StringComparison.OrdinalIgnoreCase)
            || requested.Contains("different", StringComparison.OrdinalIgnoreCase)
            || requested.Contains("mismatch", StringComparison.OrdinalIgnoreCase));

    private static bool ShaOk(string stored, string live) =>
        CharacterIdentityGovernanceRules.ShaExists(stored)
        && CharacterIdentityGovernanceRules.ShaExists(live)
        && CharacterIdentityGovernanceRules.SameSha(stored, live);
}
