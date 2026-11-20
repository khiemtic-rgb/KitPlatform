namespace KitPlatform.Packs.Content;

/// <summary>
/// Queue GET for v3 often returns only the envelope. Usage Platform API
/// keeps json_output (video.url) for ~90 days. 0 provider calls.
/// </summary>
public static class FamixaFalLipsyncUsageV1
{
    public const string SuiteId = "FAMIXA_FAL_LIPSYNC_USAGE_V1";
    public const string PlatformOrigin = "https://api.fal.ai/v1";

    public static IReadOnlyList<string> EndpointIds(string? endpoint)
    {
        var submit = FamixaFalLipsyncPollV1.NormalizeEndpoint(endpoint);
        var app = FamixaFalLipsyncPollV1.QueueApp(endpoint);
        var list = new List<string>();
        if (submit.Length > 0) list.Add(submit);
        if (app.Length > 0 && !app.Equals(submit, StringComparison.OrdinalIgnoreCase))
            list.Add(app);
        return list;
    }

    public static string ByRequestUrl(string endpointId, string requestId)
    {
        var ep = Uri.EscapeDataString((endpointId ?? "").Trim());
        var id = Uri.EscapeDataString((requestId ?? "").Trim());
        return $"{PlatformOrigin}/models/requests/by-endpoint?endpoint_id={ep}&request_id={id}&expand=payloads";
    }

    public static string RecentUrl(string endpointId) =>
        $"{PlatformOrigin}/models/requests/by-endpoint?endpoint_id={Uri.EscapeDataString((endpointId ?? "").Trim())}&expand=payloads&limit=100";

    public static string AssetsByQueryUrl(string requestId) =>
        $"{PlatformOrigin}/assets?q={Uri.EscapeDataString((requestId ?? "").Trim())}&media_type=video&limit=20";

    public static string AssetsGeneratedUrl() =>
        $"{PlatformOrigin}/assets?media_type=video&source=response&section=generated&limit=50";

    public static IReadOnlyList<string> PeekUrls(string requestId, string? endpoint)
    {
        var list = new List<string>();
        foreach (var ep in EndpointIds(endpoint))
        {
            list.Add(ByRequestUrl(ep, requestId));
            list.Add(RecentUrl(ep));
        }

        list.Add(AssetsByQueryUrl(requestId));
        list.Add(AssetsGeneratedUrl());
        return list;
    }
}
