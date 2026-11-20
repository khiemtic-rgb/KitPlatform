namespace KitPlatform.Packs.Content.Production;

public enum ProductionScopeKind
{
    Film,
    Test,
    Unknown,
}

public sealed record ProductionScopeAnchor(
    Guid ScopeId,
    string Kind,
    Guid? SeriesBuildId,
    Guid? TestRunId,
    DateTimeOffset CreatedAt);

public sealed record ProductionTestRun(
    Guid TestRunId,
    string Name,
    string Status,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    string MetadataJson);

public sealed record ProductionTestScope(
    Guid ScopeId,
    Guid TestRunId,
    string EpisodeCode,
    string SceneCode,
    DateTimeOffset CreatedAt,
    string MetadataJson);

public sealed record ProductionTestShot(
    Guid TestShotId,
    Guid ScopeId,
    string ShotId,
    string EpisodeCode,
    string SceneCode,
    string ShotCode,
    DateTimeOffset CreatedAt,
    string MetadataJson);

public sealed record ProductionTestFoundation(
    ProductionTestRun Run,
    ProductionScopeAnchor Anchor,
    ProductionTestScope Scope,
    ProductionTestShot Shot);

public static class ProductionScopeGuard
{
    public static ProductionScopeKind ParseKind(string? kind)
    {
        var k = (kind ?? "").Trim();
        if (string.Equals(k, ProductionScopeAnchorRules.Film, StringComparison.Ordinal))
            return ProductionScopeKind.Film;
        if (string.Equals(k, ProductionScopeAnchorRules.Test, StringComparison.Ordinal))
            return ProductionScopeKind.Test;
        return ProductionScopeKind.Unknown;
    }

    public static ProductionScopeKind Resolve(ProductionScopeAnchor? anchor, Guid scopeId)
    {
        if (anchor is not null)
            return ParseKind(anchor.Kind);
        if (ProductionScopeAnchorRules.IsOfficialFilm(scopeId))
            return ProductionScopeKind.Film;
        return ProductionScopeKind.Unknown;
    }

    public static void RequireKnown(ProductionScopeKind kind)
    {
        if (kind == ProductionScopeKind.Unknown)
            throw new InvalidOperationException(ProductionFailureReasons.UnknownScope);
    }

    public static void RequireCompatible(ProductionScopeKind request, ProductionScopeKind owner)
    {
        if (request == ProductionScopeKind.Test || owner == ProductionScopeKind.Test)
        {
            if (request != owner)
                throw new InvalidOperationException(ProductionFailureReasons.CrossScopeForbidden);
            return;
        }

        if (request == ProductionScopeKind.Unknown || owner == ProductionScopeKind.Unknown)
            return;

        if (request != owner)
            throw new InvalidOperationException(ProductionFailureReasons.CrossScopeForbidden);
    }

    public static void RequireSameScope(Guid requestScopeId, ProductionScopeKind requestKind, Guid ownerScopeId, ProductionScopeKind ownerKind)
    {
        RequireCompatible(requestKind, ownerKind);
        if (requestKind == ProductionScopeKind.Test || ownerKind == ProductionScopeKind.Test)
        {
            if (requestScopeId != ownerScopeId)
                throw new InvalidOperationException(ProductionFailureReasons.CrossScopeForbidden);
        }
        else if (requestScopeId != ownerScopeId
            && requestKind != ProductionScopeKind.Unknown
            && ownerKind != ProductionScopeKind.Unknown)
        {
            throw new InvalidOperationException(ProductionFailureReasons.CrossScopeForbidden);
        }
    }
}

public static class ProductionStoragePolicy
{
    public const string FilmKeyframeFolder = "os-kf";
    public const string FilmVideoFolder = "os";

    public static string KeyframeFolder(ProductionScopeKind kind, Guid? testRunId)
    {
        if (kind == ProductionScopeKind.Test)
        {
            if (testRunId is null || testRunId == Guid.Empty)
                throw new InvalidOperationException(ProductionFailureReasons.UnknownScope);
            return $"os-test/{testRunId.Value:D}/kf";
        }

        return FilmKeyframeFolder;
    }

    public static string VideoFolder(ProductionScopeKind kind, Guid? testRunId)
    {
        if (kind == ProductionScopeKind.Test)
        {
            if (testRunId is null || testRunId == Guid.Empty)
                throw new InvalidOperationException(ProductionFailureReasons.UnknownScope);
            return $"os-test/{testRunId.Value:D}/video";
        }

        return FilmVideoFolder;
    }

    public static string VoiceFolder(ProductionScopeKind kind, Guid? testRunId)
    {
        if (kind == ProductionScopeKind.Test)
        {
            if (testRunId is null || testRunId == Guid.Empty)
                throw new InvalidOperationException(ProductionFailureReasons.UnknownScope);
            return $"os-test/{testRunId.Value:D}/voice";
        }

        return "os-voice";
    }
}
