namespace KitPlatform.Packs.Content;

/// <summary>
/// Locked KIT_MKT rules from docs/novixa/03-solution/kit-mkt-replan. No I/O.
/// </summary>
public static class KitMktWorkspaceRules
{
    public static readonly string[] WorkflowKeys = ["famixa_series", "factory_short", "guided_production"];
    public static readonly string[] ContentFormats = ["article", "video", "infographic", "other"];

    public static bool KnownWorkflow(string? key) =>
        WorkflowKeys.Contains(key ?? "", StringComparer.Ordinal);

    public static bool KnownFormat(string? format) =>
        ContentFormats.Contains(format ?? "", StringComparer.Ordinal);

    public static bool IdeaShape(Guid? brandId, Guid? topicId, Guid? sourcePackageId) =>
        brandId is null && topicId is null && sourcePackageId is null;

    public static bool PackageScopeAllowed(Guid? brandId, Guid? topicId, Guid? sourcePackageId) =>
        IdeaShape(brandId, topicId, sourcePackageId)
        || (brandId is not null && topicId is not null);

    public static bool ProjectFilmBrandMatch(Guid? projectBrandId, Guid filmTypeBrandId) =>
        projectBrandId is Guid brand && brand == filmTypeBrandId;

    /// <summary>
    /// Production opens only when the project already has a Film Type of its own brand.
    /// Content is not required. A missing brand is reported before a missing Film Type.
    /// </summary>
    public static string? OpenProductionRefusal(Guid? projectBrandId, Guid? filmTypeId, Guid? filmTypeBrandId)
    {
        if (projectBrandId is null)
            return "Project chưa có brand.";
        if (filmTypeId is null)
            return "Project chưa có Film Type.";
        if (filmTypeBrandId != projectBrandId)
            return "Film Type không thuộc brand của project.";
        return null;
    }

    /// <summary>Missing brand is reported before narrative or off-brand refusal.</summary>
    public static string? SeriesCreateRefusal(Guid? brandId, string? narrativeBlock)
    {
        if (brandId is null)
            return "Idea chưa gắn thương hiệu.";
        if (narrativeBlock == BrandNarrativeFit.NarrativeFitRequired)
            return "NarrativeFitRequired — package chưa có Territory. Không suy ra CORE từ score.";
        if (narrativeBlock is not null)
            return "OFF-BRAND — không đưa package này vào Series.";
        return null;
    }

    /// <summary>Null when the Film Type may be bound. The two refusals stay distinct.</summary>
    public static string? FilmTypeBindRefusal(Guid? projectBrandId, Guid filmTypeBrandId)
    {
        if (projectBrandId is null)
            return "Project chưa có brand. Không gán brand hộ.";
        if (projectBrandId != filmTypeBrandId)
            return "Film Type không thuộc brand của project.";
        return null;
    }

    /// <summary>
    /// A series_build hook is the Famixa workflow. An unbound project keeps the existing hook
    /// until a film type is attached. A bound film type of another workflow cannot take that hook.
    /// </summary>
    public static bool MayAttachSeriesBuild(string? workflowKey, Guid? seriesBuildId)
    {
        if (seriesBuildId is null) return true;
        return workflowKey is null || workflowKey == "famixa_series";
    }

    public static bool MayChangeWorkflow(string currentKey, string nextKey, bool hasActiveProject) =>
        string.Equals(currentKey, nextKey, StringComparison.Ordinal) || !hasActiveProject;

    public static bool MaySelectProductionForPublish(Guid? contentTopicId) =>
        contentTopicId is not null;

    /// <summary>Calendar placement is the publish instant only. Missing publish_at is not a date.</summary>
    public static DateTimeOffset? CalendarInstant(DateTimeOffset? publishAt) => publishAt;
}
