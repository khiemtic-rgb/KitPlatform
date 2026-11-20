using System.Text.Json;

namespace KitPlatform.Packs.Content;

/// <summary>
/// Brand-owned video pipeline rules. A pipeline is a film type.
/// Workspace follows the pipeline stored on the film, never the nav brand.
/// </summary>
public static class VideoPipelineRules
{
    public const string LegacySeriesCatalog = "FAMIXA";
    public const string SeriesWorkspace = "series_continuity";
    public const string ShortWorkspace = "short_factory";
    public const string GuidedWorkspace = "guided";
    public const string UnconfiguredWorkspace = "unconfigured";

    public static readonly string[] Steps = ["script", "scene", "shot", "voice", "image", "video", "review", "export"];
    public static readonly string[] Aspects = ["", "16:9", "9:16", "1:1"];

    public static string WorkspaceForWorkflow(string? workflowKey) => workflowKey switch
    {
        "famixa_series" => SeriesWorkspace,
        "factory_short" => ShortWorkspace,
        "guided_production" => GuidedWorkspace,
        _ => UnconfiguredWorkspace,
    };

    public static string ResolveWorkspace(Guid? filmTypeId, string? workflowKey, string? seriesCode)
    {
        if (filmTypeId is Guid id && id != Guid.Empty)
            return WorkspaceForWorkflow(workflowKey);
        if (!string.IsNullOrWhiteSpace(seriesCode))
            return SeriesWorkspace;
        return UnconfiguredWorkspace;
    }

    /// <summary>Null keeps the existing series chrome. Any other film uses only its stored label.</summary>
    public static string? SeriesLine(Guid? filmTypeId, string? seriesCode, string? configuredLabel, string? pipelineName)
    {
        if (!string.IsNullOrWhiteSpace(configuredLabel))
            return configuredLabel.Trim();
        if ((filmTypeId is null || filmTypeId == Guid.Empty)
            && string.Equals(seriesCode?.Trim(), LegacySeriesCatalog, StringComparison.Ordinal))
            return null;
        if (!string.IsNullOrWhiteSpace(pipelineName))
            return pipelineName.Trim();
        var code = seriesCode?.Trim();
        if (string.IsNullOrWhiteSpace(code)
            || string.Equals(code, LegacySeriesCatalog, StringComparison.Ordinal))
            return null;
        return code;
    }

    public static string? BindRefusal(Guid filmBrandId, Guid pipelineBrandId, string? status, bool creating)
    {
        if (filmBrandId == Guid.Empty || pipelineBrandId == Guid.Empty || filmBrandId != pipelineBrandId)
            return "Pipeline không thuộc brand của phim.";
        if (creating && !string.Equals(status, "active", StringComparison.Ordinal))
            return "Pipeline chưa bật. Không tạo phim mới.";
        return null;
    }

    public static string? RetargetRefusal(Guid? storedPipelineId, Guid? requestedPipelineId)
    {
        if (storedPipelineId is null || storedPipelineId == Guid.Empty) return null;
        if (requestedPipelineId is null || requestedPipelineId == Guid.Empty) return null;
        if (requestedPipelineId == storedPipelineId) return null;
        return "Đổi pipeline của phim cần thao tác chuyển riêng.";
    }

    public static bool OfferedForNewFilm(string? status) =>
        string.Equals(status, "active", StringComparison.Ordinal);

    /// <summary>
    /// famixa: stored famixa_series, or an unbound film whose series catalog is FAMIXA.
    /// unavailable: factory_short or guided_production.
    /// unconfigured: any other film, including a series workspace that is not the Famixa catalog.
    /// </summary>
    public static string CharacterDesk(string? seriesCode, string? workflowKey, string? filmTypeId)
    {
        var workflow = (workflowKey ?? "").Trim();
        if (workflow == "famixa_series") return "famixa";
        if (workflow is "factory_short" or "guided_production") return "unavailable";
        if (workflow.Length > 0) return "unconfigured";
        var typed = !string.IsNullOrWhiteSpace(filmTypeId);
        var series = (seriesCode ?? "").Trim();
        if (!typed && string.Equals(series, LegacySeriesCatalog, StringComparison.OrdinalIgnoreCase))
            return "famixa";
        return "unconfigured";
    }

    public static bool CharacterRequestScoped(string? seriesCode, string? workflowKey, string? filmTypeId) =>
        !string.IsNullOrWhiteSpace(seriesCode)
        || !string.IsNullOrWhiteSpace(workflowKey)
        || !string.IsNullOrWhiteSpace(filmTypeId);

    /// <summary>
    /// A stored film keeps the workflow it was created with.
    /// Changing that workflow is a separate action, not an edit of the pipeline.
    /// </summary>
    public static string? WorkflowChangeRefusal(string? currentKey, string? nextKey, int filmCount, bool hasActiveProject)
    {
        if (string.Equals(currentKey, nextKey, StringComparison.Ordinal)) return null;
        if (filmCount > 0) return "Pipeline đang có phim. Không đổi workflow.";
        if (hasActiveProject) return "Pipeline đang có project active. Không đổi workflow.";
        return null;
    }

    /// <summary>Never substitutes the legacy catalog code.</summary>
    public static string? SeriesCodeForPipeline(string? pipelineCode)
    {
        var code = (pipelineCode ?? "").Trim().ToUpperInvariant().Replace('-', '_');
        var clean = new string(code.Where(ch => ch is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_').ToArray());
        if (clean.Length is < 2 or > 64) return null;
        if (clean[0] is < 'A' or > 'Z') return null;
        return clean;
    }

    public static string? ConfigError(string? workflowKey, string? status, IReadOnlyList<string>? steps, string? aspect)
    {
        if (!KitMktWorkspaceRules.KnownWorkflow(workflowKey))
            return "workflow_key không hợp lệ.";
        if (status is not ("draft" or "active" or "archived"))
            return "Trạng thái pipeline không hợp lệ.";
        if (!Aspects.Contains(aspect ?? ""))
            return "Tỷ lệ khung không hợp lệ.";
        if (steps is null) return null;
        foreach (var step in steps)
        {
            if (!Steps.Contains(step))
                return "Bước sản xuất không hợp lệ.";
        }
        return null;
    }

    public readonly record struct PipelineConfig(
        string Purpose,
        string SeriesLabel,
        string Aspect,
        bool IsDefault,
        string[] Steps);

    public static PipelineConfig ReadConfig(string? json)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            var root = doc.RootElement;
            var steps = new List<string>();
            if (root.TryGetProperty("steps", out var stepNode) && stepNode.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in stepNode.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String && item.GetString() is string step && step.Length > 0)
                        steps.Add(step);
                }
            }
            return new PipelineConfig(
                Text(root, "purpose"),
                Text(root, "seriesLabel"),
                Text(root, "aspect"),
                root.TryGetProperty("isDefault", out var flag) && flag.ValueKind == JsonValueKind.True,
                steps.ToArray());
        }
        catch (JsonException)
        {
            return new PipelineConfig("", "", "", false, []);
        }
    }

    public static string WriteConfig(string? purpose, string? seriesLabel, string? aspect, bool isDefault, IReadOnlyList<string>? steps)
    {
        var payload = new Dictionary<string, object?>
        {
            ["purpose"] = (purpose ?? "").Trim(),
            ["seriesLabel"] = (seriesLabel ?? "").Trim(),
            ["aspect"] = aspect ?? "",
            ["isDefault"] = isDefault,
            ["steps"] = steps ?? Array.Empty<string>(),
        };
        return JsonSerializer.Serialize(payload);
    }

    private static string Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var node) && node.ValueKind == JsonValueKind.String
            ? node.GetString() ?? ""
            : "";
}
