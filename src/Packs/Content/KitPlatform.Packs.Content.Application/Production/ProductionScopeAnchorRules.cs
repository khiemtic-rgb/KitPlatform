namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// FILM identity: scope_id = series_build.id.
/// TEST identity: scope_id is a new UUID on production_scope_anchor (kind=TEST, series_build_id NULL).
/// </summary>
public static class ProductionScopeAnchorRules
{
    public const string Film = "FILM";
    public const string Test = "TEST";

    public static readonly Guid OfficialEp01 = Guid.Parse("4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f");
    public static readonly Guid OfficialEp99 = Guid.Parse("77262ec0-74ad-459d-9128-f09bb553a5dd");

    public static readonly IReadOnlyList<Guid> OfficialFilmScopeIds =
    [
        OfficialEp01,
        OfficialEp99,
        Guid.Parse("fd760913-0eb8-46a5-b6eb-ec2990a4b0d0"),
        Guid.Parse("fca0c122-797e-4356-9bd1-2a70daac1ed4"),
    ];

    public static Guid FilmScopeId(Guid seriesBuildId) => seriesBuildId;

    public static bool IsOfficialFilm(Guid scopeId) =>
        OfficialFilmScopeIds.Contains(scopeId);

    /// <summary>
    /// Episode EP01 on a build that is not one of the historical official-film ids.
    /// Caller ownership is ProductionBuildTenantGate, not this predicate.
    /// </summary>
    public static bool IsTenantOwnedEp01Build(Guid buildId, string? episodeCode) =>
        buildId != Guid.Empty
        && !IsOfficialFilm(buildId)
        && string.Equals((episodeCode ?? "").Trim(), "EP01", StringComparison.Ordinal);

    public static bool IsEp01EpisodeBuild(Guid buildId, string? episodeCode) =>
        string.Equals((episodeCode ?? "").Trim(), "EP01", StringComparison.Ordinal)
        && (buildId == OfficialEp01 || IsTenantOwnedEp01Build(buildId, episodeCode));
}
