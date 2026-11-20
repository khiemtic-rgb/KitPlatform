namespace KitPlatform.Packs.Content;

/// <summary>
/// One approved SH02 still may attach the existing Linh file.
/// The file is not a locked character master and this rule does not write one.
/// </summary>
public static class Sh02ApprovedLinhStillReferenceV1
{
    public const string SuiteId = "FAMIXA_SH02_APPROVED_LINH_STILL_REFERENCE_V1";
    public static readonly Guid BuildId = Guid.Parse("30ba4061-a443-4c0d-85aa-1d13ff4fdedb");
    public const string ShotCode = "EP01-SC01-SH02";
    public const string LinhCharacterId = "CHAR-003";
    public const string MinhCharacterId = "CHAR-001";
    public const string LinhSha256 = "f33efc50d226916947e966e94b86bbb95f23fb0ecc2e4a433fa30cb9e50fcc73";
    public const string MinhMasterSha256 = "be439c39e067aa6c7727255e9643ac78cb7c6285917af60dda38bf14a32518f1";
    public const string Authority = "APPROVED_ONE_STILL";
    public const string Source = "APPROVED_ONE_STILL";
    public const string ModelId = "gemini-3.1-flash-lite-image";

    public static bool IsApprovedScope(Guid? buildId, string? shotCode) =>
        buildId == BuildId
        && string.Equals((shotCode ?? "").Trim(), ShotCode, StringComparison.Ordinal);

    public static VisualReferenceClaim Classify(
        ProjectVisualModeContract mode,
        ContentSeriesStillRefDto row,
        string byteSha256,
        bool approvedScope)
    {
        var character = (row.CharacterId ?? "").Trim();
        var sha = (byteSha256 ?? "").Trim().ToLowerInvariant();
        var linhBytes = string.Equals(sha, LinhSha256, StringComparison.Ordinal);
        if (linhBytes && !approvedScope)
            throw new InvalidOperationException("SH02_LINH_REFERENCE_OUT_OF_SCOPE");
        if (linhBytes && !string.Equals(character, LinhCharacterId, StringComparison.Ordinal))
            throw new InvalidOperationException("SH02_LINH_REFERENCE_CHARACTER");
        if (approvedScope && string.Equals(character, LinhCharacterId, StringComparison.Ordinal))
        {
            if (!linhBytes)
                throw new InvalidOperationException("SH02_LINH_REFERENCE_HASH_MISMATCH");
            return new VisualReferenceClaim(
                mode.ProjectId,
                LinhCharacterId,
                string.IsNullOrWhiteSpace(row.Name) ? "Linh" : row.Name,
                mode.VisualMode,
                mode.VisualUniverse,
                mode.VisualStyle,
                ProjectVisualModeAuthorityV1Rules.RoleScene,
                Authority,
                ProjectVisualModeAuthorityV1Rules.StatusActive,
                Source,
                LinhSha256);
        }

        if (approvedScope && string.Equals(character, MinhCharacterId, StringComparison.Ordinal))
        {
            if (!string.Equals(sha, MinhMasterSha256, StringComparison.Ordinal))
                throw new InvalidOperationException("SH02_MINH_MASTER_HASH_MISMATCH");
            return new VisualReferenceClaim(
                mode.ProjectId,
                MinhCharacterId,
                string.IsNullOrWhiteSpace(row.Name) ? "Minh" : row.Name,
                mode.VisualMode,
                mode.VisualUniverse,
                mode.VisualStyle,
                ProjectVisualModeAuthorityV1Rules.RoleIdentityAnchor,
                "LOCKED",
                ProjectVisualModeAuthorityV1Rules.StatusActive,
                "VIDEO_MASTER_REFERENCE",
                MinhMasterSha256);
        }

        return ClassifyUnscoped(mode, row);
    }

    public static bool HasRequiredPair(IReadOnlyList<VisualReferenceClaim> claims) =>
        claims.Count(c =>
            c.CharacterId == LinhCharacterId
            && c.AuthorityStatus == Authority
            && c.ReferenceRole == ProjectVisualModeAuthorityV1Rules.RoleScene
            && c.ArtifactSha256 == LinhSha256) == 1
        && claims.Count(c =>
            c.CharacterId == MinhCharacterId
            && c.AuthorityStatus == "LOCKED"
            && c.ReferenceRole == ProjectVisualModeAuthorityV1Rules.RoleIdentityAnchor
            && c.ArtifactSha256 == MinhMasterSha256) == 1;

    private static VisualReferenceClaim ClassifyUnscoped(
        ProjectVisualModeContract mode,
        ContentSeriesStillRefDto row)
    {
        var role = (row.Role ?? "").Trim();
        var identity = role.Equals("identity", StringComparison.OrdinalIgnoreCase)
            || role.Equals("identity-secondary", StringComparison.OrdinalIgnoreCase)
            || string.Equals(row.ReferenceRole, ProjectVisualModeAuthorityV1Rules.RoleIdentityAnchor, StringComparison.OrdinalIgnoreCase);
        var missingMode = string.IsNullOrWhiteSpace(row.VisualMode);
        var legacyIdentity = identity && missingMode;
        return new VisualReferenceClaim(
            mode.ProjectId,
            row.CharacterId ?? "",
            row.Name,
            legacyIdentity
                ? ProjectVisualModeAuthorityV1Rules.ModePhotoreal
                : (row.VisualMode ?? mode.VisualMode),
            mode.VisualUniverse,
            mode.VisualStyle,
            row.ReferenceRole ?? (identity
                ? ProjectVisualModeAuthorityV1Rules.RoleIdentityAnchor
                : role.Equals("scene", StringComparison.OrdinalIgnoreCase)
                    ? ProjectVisualModeAuthorityV1Rules.RoleScene
                    : role),
            row.AuthorityStatus ?? (identity ? "" : "LOCKED"),
            row.ReferenceStatus ?? (legacyIdentity
                ? ProjectVisualModeAuthorityV1Rules.StatusLegacy
                : ProjectVisualModeAuthorityV1Rules.StatusActive),
            legacyIdentity ? "CANON_SEED" : "CHARACTER_STUDIO",
            null);
    }
}
