namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_VISUAL_UNIVERSE_SNAPSHOT_V1 — read-only mapping onto current VUA + PVS + CDL.
/// Does not persist, promote, lock, generate, or mutate authorities.
/// </summary>
public static class VisualUniverseSnapshotV1Rules
{
    public const string DocumentId = "FAMIXA_VISUAL_UNIVERSE_SNAPSHOT_V1";
    public const string SuiteId = "FAMIXA_VISUAL_UNIVERSE_SNAPSHOT_RESOLVER_V1_REGRESSION";
    public const string SnapshotVersion = "VISUAL_UNIVERSE_SNAPSHOT_V1";

    public static bool CallsGemini() => false;
    public static bool CreatesPixels() => false;
    public static bool PromotesVua() => false;
    public static bool LocksVua() => false;
    public static bool MutatesRulesJson() => false;
    public static bool MutatesCharacters() => false;
    public static bool UsesPvsV2AsLiveAuthority() => false;

    public static string NormalizeProject(string? projectId)
    {
        var t = (projectId ?? "").Trim().ToUpperInvariant();
        return string.IsNullOrWhiteSpace(t) ? FamixaVisualUniverseAuthorityV1Rules.ProjectId : t;
    }

    public static string CurrentPvsSha(string? livePvsSha, string? vuaStatus)
    {
        _ = vuaStatus;
        var live = (livePvsSha ?? "").Trim().ToLowerInvariant();
        if (FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(live)
            && FamixaVisualUniverseAuthorityV1Rules.SameSha(live, ProjectVisualStyleV2Rules.ProtectedV1Sha))
            return ProjectVisualStyleV2Rules.ProtectedV1Sha;
        return ProjectVisualStyleV2Rules.ProtectedV1Sha;
    }

    public static VisualUniverseSnapshot FromAuthorities(
        string? projectId,
        VisualUniverseAuthorityDto universe,
        string? livePvsSha,
        string? calibrationPackId = null,
        string? calibrationPackSha = null)
    {
        var project = NormalizeProject(string.IsNullOrWhiteSpace(projectId) ? universe.ProjectId : projectId);
        var status = string.IsNullOrWhiteSpace(universe.Status)
            ? FamixaVisualUniverseAuthorityV1Rules.StatusDraft
            : universe.Status.Trim().ToUpperInvariant();
        var vuaSha = FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(universe.Sha256)
            ? universe.Sha256.Trim().ToLowerInvariant()
            : FamixaVisualUniverseAuthorityV1Rules.Sha();
        var cdlSha = CharacterDesignLanguageV2Rules.Sha();
        var calId = string.IsNullOrWhiteSpace(calibrationPackId) ? null : calibrationPackId.Trim();
        var calSha = FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(calibrationPackSha)
            ? calibrationPackSha!.Trim().ToLowerInvariant()
            : null;
        return new VisualUniverseSnapshot(
            project,
            string.IsNullOrWhiteSpace(universe.AuthorityId)
                ? FamixaVisualUniverseAuthorityV1Rules.AuthorityId
                : universe.AuthorityId.Trim(),
            vuaSha,
            status,
            CurrentPvsSha(livePvsSha, status),
            cdlSha,
            calSha,
            calId,
            FamixaVisualUniverseAuthorityV1Rules.StylizationLevel,
            FamixaVisualUniverseAuthorityV1Rules.PhotorealismCeiling,
            FamixaVisualUniverseAuthorityV1Rules.CameraLanguage,
            FamixaVisualUniverseAuthorityV1Rules.MaterialLanguage,
            SnapshotVersion,
            FamixaVisualUniverseAuthorityV1Rules.CurrentPvsRemainsAuthority(status),
            FamixaVisualUniverseAuthorityV1Rules.IsAuthority(status));
    }
}
