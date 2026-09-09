using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_VISUAL_MODE_AUTHORITY_V1 — Visual Mode is a Project authority.
/// Character / Calibration / Scene / Video inherit. No silent override.
/// Does not call Gemini, generate pixels, approve, lock, or mutate PVS/VUA/CDL/Master.
/// </summary>
public static class ProjectVisualModeAuthorityV1Rules
{
    public const string DocumentId = "FAMIXA_VISUAL_MODE_AUTHORITY_V1";
    public const string SuiteId = "FAMIXA_VISUAL_MODE_AUTHORITY_V1_REGRESSION";
    public const string Version = "V1";
    public const string ProjectFamixa = "FAMIXA";
    public const string VisualUniverseFamixa = "FAMIXA";

    public const string Mode3dStylized = "3D_STYLIZED_REALISM";
    public const string ModePhotoreal = "PHOTOREALISTIC";
    public const string ModeLiveAction = "LIVE_ACTION";
    public const string ModeAnime = "ANIME";
    public const string ModeIllustration = "ILLUSTRATION";
    public const string ModeCustom = "CUSTOM";

    public const string StatusActive = "ACTIVE";
    public const string StatusLegacy = "LEGACY";
    public const string StatusHistorical = "HISTORICAL";
    public const string StatusIneligible = "INELIGIBLE";
    public const string StatusIneligibleForProject = "INELIGIBLE_FOR_PROJECT";
    public const string StatusInvalidPipeline = "INVALID_REFERENCE_PIPELINE";

    public const string RoleIdentityAnchor = "IDENTITY_ANCHOR";
    public const string RoleCalibration = "CALIBRATION";
    public const string RoleScene = "SCENE";
    public const string RoleLegacyCanon = "LEGACY_CANON";

    public const string GateMissing = "PROJECT_VISUAL_MODE_MISSING";
    public const string GateConflict = "VISUAL_MODE_CONFLICT";
    public const string GateLegacy = "LEGACY_VISUAL_MODE_CONFLICT";
    public const string GateRefIneligible = "REFERENCE_VISUAL_MODE_INELIGIBLE";
    public const string GateIdentity = "CHARACTER_IDENTITY_NOT_AVAILABLE";
    public const string GateCompiler = "COMPILER_VISUAL_MODE_MISMATCH";
    public const string GateProvider = "PROVIDER_PAYLOAD_VISUAL_MODE_MISMATCH";
    public const string GateRevisionRequired = "VISUAL_MODE_REVISION_REQUIRED";

    public const string PhotorealismCeilingLow = "LOW";
    public const string CharacterRender3d = "3D_CHARACTER";
    public const string SceneRender3d = "3D_STYLIZED_SCENE";
    public const string VideoRender3d = "3D_STYLIZED_VIDEO";
    public const string ReferencePolicyStudioOnly = "LOCKED_STUDIO_ONLY";

    public const string StaffNeedMode = "Chọn Visual Mode trước khi tạo Project.";
    public const string StaffConflict = "Reference không cùng Visual Mode với Project.";
    public const string StaffLegacy = "Canon photoreal cũ không được dùng cho project 3D Stylized.";
    public const string StaffStudio = "Dùng Character Studio LOCKED làm identity anchor.";

    public static readonly string[] Modes =
    [
        Mode3dStylized, ModePhotoreal, ModeLiveAction, ModeAnime, ModeIllustration, ModeCustom,
    ];

    public static bool RequiresVisualMode() => true;
    public static bool SceneMayChooseMode() => false;
    public static bool CharacterMayChooseMode() => false;
    public static bool CalibrationMayChooseMode() => false;
    public static bool VideoMayChooseMode() => false;
    public static bool SilentFallback() => false;
    public static bool AutoSelectLegacyCanon() => false;
    public static bool ProviderDecidesMode() => false;
    public static bool CallsGemini() => false;
    public static bool CreatesPixels() => false;
    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool MutatesAuthorities() => false;
    public static bool RequiresMigration() => false;
    public static bool MayChangeModeDirectly() => false;

    public static string? ValidateMode(string? mode) =>
        Modes.Contains((mode ?? "").Trim().ToUpperInvariant())
            ? null
            : GateMissing;

    public static string NormalizeMode(string? mode)
    {
        var t = (mode ?? "").Trim().ToUpperInvariant();
        return t switch
        {
            "3D" or "STYLIZED" or "3D_STYLIZED" => Mode3dStylized,
            "PHOTO" or "REAL" or "PHOTOREAL" => ModePhotoreal,
            "LIVE" or "LIVEACTION" => ModeLiveAction,
            "ANIMATION" => ModeAnime,
            _ => t,
        };
    }

    public static string PvsKeyOf(string visualMode) => NormalizeMode(visualMode) switch
    {
        ModePhotoreal => "PHOTOREALISTIC",
        ModeLiveAction => "CINEMATIC_REALISM",
        ModeAnime => "ANIMATION",
        ModeIllustration => "3D_CARTOON",
        ModeCustom => "3D_STYLIZED_REALISM",
        _ => Mode3dStylized,
    };

    public static string ModeOfPvsKey(string? styleKey)
    {
        var key = (styleKey ?? "").Trim().ToUpperInvariant();
        return key switch
        {
            "PHOTOREALISTIC" => ModePhotoreal,
            "CINEMATIC_REALISM" => ModeLiveAction,
            "ANIMATION" => ModeAnime,
            "3D_CARTOON" => ModeIllustration,
            _ => Mode3dStylized,
        };
    }

    public static bool Compatible(string? projectMode, string? referenceMode)
    {
        var project = NormalizeMode(projectMode);
        var reference = NormalizeMode(referenceMode);
        if (ValidateMode(project) is not null || ValidateMode(reference) is not null) return false;
        return project == reference;
    }

    public static ProjectVisualModeContract FamixaCurrent() =>
        Compile(new ProjectVisualModeSource(
            ProjectFamixa,
            Mode3dStylized,
            VisualUniverseFamixa,
            "3D Stylized Realism",
            PhotorealismCeilingLow,
            CharacterRender3d,
            SceneRender3d,
            VideoRender3d,
            ReferencePolicyStudioOnly,
            Version,
            DateTimeOffset.Parse("2026-09-04T00:00:00Z")));

    public static ProjectVisualModeContract Compile(ProjectVisualModeSource source)
    {
        var mode = NormalizeMode(source.VisualMode);
        var missing = ValidateMode(mode);
        if (missing is not null)
            throw new InvalidOperationException(missing + ": " + StaffNeedMode);
        var payload = Canonical(source with { VisualMode = mode });
        return new ProjectVisualModeContract(
            source.ProjectId,
            mode,
            source.VisualUniverse,
            source.VisualStyle,
            source.PhotorealismCeiling,
            source.CharacterRenderingMode,
            source.SceneRenderingMode,
            source.VideoRenderingMode,
            source.ReferencePolicy,
            source.CreatedAt,
            source.Version,
            Sha256Hex(payload),
            StatusActive);
    }

    public static string? Inherit(string? projectMode, string? childMode)
    {
        if (ValidateMode(NormalizeMode(projectMode)) is not null) return GateMissing;
        if (string.IsNullOrWhiteSpace(childMode)) return null;
        return Compatible(projectMode, childMode) ? null : GateConflict;
    }

    public static VisualReferenceEligibility ClassifyReference(VisualReferenceClaim claim, string projectMode)
    {
        var mode = NormalizeMode(claim.VisualMode);
        var project = NormalizeMode(projectMode);
        var legacy = claim.ReferenceStatus is StatusLegacy or StatusHistorical
            || claim.ReferenceRole == RoleLegacyCanon
            || claim.Source == "CANON_SEED";
        if (legacy && !Compatible(project, mode))
        {
            return new VisualReferenceEligibility(
                false, StatusIneligibleForProject, GateLegacy, StaffLegacy, claim);
        }
        if (!Compatible(project, mode))
        {
            return new VisualReferenceEligibility(
                false, StatusIneligible, GateRefIneligible, StaffConflict, claim);
        }
        if (claim.ReferenceRole == RoleIdentityAnchor
            && !string.Equals(claim.AuthorityStatus, "LOCKED", StringComparison.OrdinalIgnoreCase))
        {
            return new VisualReferenceEligibility(
                false, StatusIneligible, GateIdentity, StaffStudio, claim);
        }
        return new VisualReferenceEligibility(true, StatusActive, null, null, claim);
    }

    public static VisualReferenceClaim LegacyPhotorealCanon(string characterId, string name) =>
        new(
            ProjectFamixa,
            characterId,
            name,
            ModePhotoreal,
            VisualUniverseFamixa,
            "Photoreal Master Reference v1.0",
            RoleLegacyCanon,
            StatusLegacy,
            "HISTORICAL",
            "CANON_SEED",
            null);

    public static VisualReferenceClaim LockedStudioAnchor(
        string characterId, string name, string? sha = null) =>
        new(
            ProjectFamixa,
            characterId,
            name,
            Mode3dStylized,
            VisualUniverseFamixa,
            "3D Stylized Realism",
            RoleIdentityAnchor,
            "LOCKED",
            "LOCKED",
            "CHARACTER_STUDIO",
            sha);

    public static SceneReferenceResolution ResolveSceneReferences(
        string projectMode,
        IReadOnlyList<VisualReferenceClaim> candidates)
    {
        var judged = candidates.Select(c => ClassifyReference(c, projectMode)).ToList();
        var eligible = judged.Where(j => j.Eligible).ToList();
        var blocked = judged.Where(j => !j.Eligible).ToList();
        var allowed = blocked.Count == 0 && eligible.Count > 0;
        var code = !allowed
            ? (blocked.FirstOrDefault()?.Code ?? GateConflict)
            : null;
        return new SceneReferenceResolution(
            allowed,
            code,
            eligible,
            blocked,
            allowed ? eligible.Select(e => e.Claim).ToList() : [],
            false,
            false);
    }

    public static VisualGenerationPreflight Preflight(VisualGenerationPreflightInput input)
    {
        var checks = new List<VisualGenerationCheck>();
        string? Fail(string id, string? code, string message)
        {
            checks.Add(new VisualGenerationCheck(id, false, code, message));
            return code;
        }
        void Pass(string id) => checks.Add(new VisualGenerationCheck(id, true, null, null));

        var project = input.Contract;
        if (project is null || ValidateMode(project.VisualMode) is not null)
            Fail("ProjectVisualModeExists", GateMissing, StaffNeedMode);
        else Pass("ProjectVisualModeExists");

        if (string.IsNullOrWhiteSpace(input.VisualUniverse))
            Fail("VisualUniverseExists", FamixaVisualUniverseAuthorityV1Rules.GateNotReady, "Visual Universe missing.");
        else Pass("VisualUniverseExists");

        if (Inherit(project?.VisualMode, input.CharacterVisualMode) is { } charGate)
            Fail("CharacterVisualModeCompatible", charGate, StaffConflict);
        else Pass("CharacterVisualModeCompatible");

        if (!input.CharacterIdentityAvailable)
            Fail("CharacterIdentityAvailable", GateIdentity, StaffStudio);
        else Pass("CharacterIdentityAvailable");

        var resolution = ResolveSceneReferences(project?.VisualMode ?? "", input.References);
        if (resolution.Blocked.Count > 0)
            Fail("ReferenceVisualModeCompatible", resolution.Code ?? GateRefIneligible, StaffConflict);
        else Pass("ReferenceVisualModeCompatible");

        if (input.References.Any(r =>
                r.ReferenceRole == RoleIdentityAnchor
                && !string.Equals(r.AuthorityStatus, "LOCKED", StringComparison.OrdinalIgnoreCase)))
            Fail("ReferenceAuthorityValid", GateIdentity, StaffStudio);
        else Pass("ReferenceAuthorityValid");

        if (resolution.Blocked.Any(b => b.Code == GateLegacy) || input.HasLegacyConflict)
            Fail("NoLegacyConflict", GateLegacy, StaffLegacy);
        else Pass("NoLegacyConflict");

        if (!string.IsNullOrWhiteSpace(input.CompilerVisualMode)
            && !Compatible(project?.VisualMode, input.CompilerVisualMode))
            Fail("CompilerVisualModeMatchesProject", GateCompiler, StaffConflict);
        else Pass("CompilerVisualModeMatchesProject");

        if (!string.IsNullOrWhiteSpace(input.ProviderPayloadVisualMode)
            && !Compatible(project?.VisualMode, input.ProviderPayloadVisualMode))
            Fail("ProviderPayloadMatchesProject", GateProvider, StaffConflict);
        else Pass("ProviderPayloadMatchesProject");

        var allowed = checks.All(c => c.Pass);
        return new VisualGenerationPreflight(
            allowed,
            allowed ? null : checks.First(c => !c.Pass).Code,
            checks,
            false,
            false,
            false);
    }

    public static string ClassifyExistingScene(
        string? sceneId,
        IReadOnlyList<VisualReferenceClaim> usedReferences,
        string projectMode)
    {
        var resolution = ResolveSceneReferences(projectMode, usedReferences);
        if (!resolution.Allowed)
            return StatusInvalidPipeline;
        return StatusActive;
    }

    public static VisualModeRevision DraftRevision(
        string projectId, string previous, string next, string reason, string actor) =>
        new(
            projectId,
            "VMR-" + Sha256Hex(projectId + previous + next + reason).Substring(0, 12),
            NormalizeMode(previous),
            NormalizeMode(next),
            reason,
            actor,
            DateTimeOffset.UtcNow,
            "DRAFT");

    public static string Canonical(ProjectVisualModeSource source) =>
        string.Join('\n', new[]
        {
            DocumentId,
            "ProjectId=" + source.ProjectId,
            "VisualMode=" + source.VisualMode,
            "VisualUniverse=" + source.VisualUniverse,
            "VisualStyle=" + source.VisualStyle,
            "PhotorealismCeiling=" + source.PhotorealismCeiling,
            "CharacterRenderingMode=" + source.CharacterRenderingMode,
            "SceneRenderingMode=" + source.SceneRenderingMode,
            "VideoRenderingMode=" + source.VideoRenderingMode,
            "ReferencePolicy=" + source.ReferencePolicy,
            "Version=" + source.Version,
        });

    public static string Sha256Hex(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool AuthoritiesUnchanged(string? pvsSha, string? vuaSha, string? cdlSha) =>
        (string.IsNullOrWhiteSpace(pvsSha)
            || ProjectVisualStyleV1Rules.SameSha(pvsSha, ProjectVisualStyleV2Rules.ProtectedV1Sha))
        && (string.IsNullOrWhiteSpace(vuaSha) || FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(vuaSha))
        && (string.IsNullOrWhiteSpace(cdlSha) || FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(cdlSha));
}

public sealed record ProjectVisualModeSource(
    string ProjectId,
    string VisualMode,
    string VisualUniverse,
    string VisualStyle,
    string PhotorealismCeiling,
    string CharacterRenderingMode,
    string SceneRenderingMode,
    string VideoRenderingMode,
    string ReferencePolicy,
    string Version,
    DateTimeOffset CreatedAt);

public sealed record ProjectVisualModeContract(
    string ProjectId,
    string VisualMode,
    string VisualUniverse,
    string VisualStyle,
    string PhotorealismCeiling,
    string CharacterRenderingMode,
    string SceneRenderingMode,
    string VideoRenderingMode,
    string ReferencePolicy,
    DateTimeOffset CreatedAt,
    string Version,
    string Sha256,
    string Status);

public sealed record VisualReferenceClaim(
    string ProjectId,
    string CharacterId,
    string Name,
    string VisualMode,
    string VisualUniverse,
    string VisualStyle,
    string ReferenceRole,
    string AuthorityStatus,
    string ReferenceStatus,
    string Source,
    string? ArtifactSha256);

public sealed record VisualReferenceEligibility(
    bool Eligible,
    string ReferenceStatus,
    string? Code,
    string? Reason,
    VisualReferenceClaim Claim);

public sealed record SceneReferenceResolution(
    bool Allowed,
    string? Code,
    IReadOnlyList<VisualReferenceEligibility> Eligible,
    IReadOnlyList<VisualReferenceEligibility> Blocked,
    IReadOnlyList<VisualReferenceClaim> Attachable,
    bool GeminiCalled,
    bool Generation);

public sealed record VisualGenerationCheck(
    string Id,
    bool Pass,
    string? Code,
    string? Message);

public sealed record VisualGenerationPreflightInput(
    ProjectVisualModeContract? Contract,
    string? VisualUniverse,
    string? CharacterVisualMode,
    bool CharacterIdentityAvailable,
    IReadOnlyList<VisualReferenceClaim> References,
    bool HasLegacyConflict,
    string? CompilerVisualMode,
    string? ProviderPayloadVisualMode);

public sealed record VisualGenerationPreflight(
    bool Allowed,
    string? Code,
    IReadOnlyList<VisualGenerationCheck> Checks,
    bool ProviderCalled,
    bool GeminiCalled,
    bool Generation);

public sealed record VisualModeRevision(
    string ProjectId,
    string RevisionId,
    string PreviousVisualMode,
    string NewVisualMode,
    string Reason,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    string Status);
