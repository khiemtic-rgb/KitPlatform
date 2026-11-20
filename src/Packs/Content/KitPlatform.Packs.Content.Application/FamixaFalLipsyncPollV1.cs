namespace KitPlatform.Packs.Content;

/// <summary>
/// Official Fal JS: GET owner/alias (drop /v3). Payload is /response after COMPLETED.
/// GET .../v3/requests/... is 405. Status needs ?logs=1. 0 provider calls.
/// </summary>
public static class FamixaFalLipsyncPollV1
{
    public const string SuiteId = "FAMIXA_FAL_LIPSYNC_POLL_V1";
    public const string QueueOrigin = "https://queue.fal.run/";

    public static string NormalizeEndpoint(string? endpoint)
    {
        var ep = Uri.UnescapeDataString((endpoint ?? "").Trim().Trim('/'));
        return ep.Replace("%2F", "/", StringComparison.OrdinalIgnoreCase).Trim('/');
    }

    /// <summary>queue.status/result use owner/alias. Submit keeps /v3.</summary>
    public static string QueueApp(string? endpoint)
    {
        var ep = NormalizeEndpoint(endpoint);
        if (ep.EndsWith("/v3", StringComparison.OrdinalIgnoreCase))
            return ep[..^3].TrimEnd('/');
        return ep;
    }

    public static string ResultPath(string endpoint, string requestId)
    {
        var id = Uri.EscapeDataString((requestId ?? "").Trim());
        return $"{QueueApp(endpoint)}/requests/{id}";
    }

    public static string ResponsePath(string endpoint, string requestId) =>
        $"{ResultPath(endpoint, requestId)}/response";

    public static string StatusPath(string endpoint, string requestId) =>
        $"{ResultPath(endpoint, requestId)}/status?logs=1";

    public static string? ToQueueRelative(string? url, bool keepResponse = false)
    {
        var s = (url ?? "").Trim();
        if (s.Length == 0) return null;
        if (s.StartsWith(QueueOrigin, StringComparison.OrdinalIgnoreCase))
            s = s[QueueOrigin.Length..].TrimStart('/');
        else if (s.StartsWith("https://queue.fal.run/", StringComparison.OrdinalIgnoreCase))
            s = s["https://queue.fal.run/".Length..].TrimStart('/');
        else if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                 || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return s;
        else
            s = s.TrimStart('/');

        var q = s.IndexOf('?', StringComparison.Ordinal);
        var path = q >= 0 ? s[..q] : s;
        var query = q >= 0 ? s[q..] : "";
        if (!keepResponse && path.EndsWith("/response", StringComparison.OrdinalIgnoreCase))
            path = path[..^"/response".Length];

        const string v3req = "fal-ai/sync-lipsync/v3/requests/";
        if (path.StartsWith(v3req, StringComparison.OrdinalIgnoreCase))
            path = "fal-ai/sync-lipsync/requests/" + path[v3req.Length..];

        return path + query;
    }

    public static IReadOnlyList<string> FollowPaths(
        string endpoint,
        string requestId,
        string? responseUrl = null)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();
        void Add(string? raw)
        {
            var path = ToQueueRelative(raw, keepResponse: true);
            if (string.IsNullOrWhiteSpace(path)) return;
            if (path.Contains("%2F", StringComparison.OrdinalIgnoreCase)) return;
            if (path.Contains("/v3/requests/", StringComparison.OrdinalIgnoreCase)) return;
            if (seen.Add(path)) list.Add(path);
        }

        Add(responseUrl);
        Add(ResponsePath(endpoint, requestId));
        Add(ResultPath(endpoint, requestId));
        return list;
    }

    public static IReadOnlyList<string> PeekPaths(
        string endpoint,
        string requestId,
        string? statusUrl = null,
        string? resultUrl = null)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();
        void Add(string? raw, bool keepResponse = false)
        {
            var path = ToQueueRelative(raw, keepResponse);
            if (string.IsNullOrWhiteSpace(path)) return;
            if (path.Contains("%2F", StringComparison.OrdinalIgnoreCase)) return;
            if (path.Contains("/v3/requests/", StringComparison.OrdinalIgnoreCase)) return;
            if (seen.Add(path)) list.Add(path);
        }

        Add(ResponsePath(endpoint, requestId), keepResponse: true);
        Add(ResultPath(endpoint, requestId));
        Add(StatusPath(endpoint, requestId));
        Add(resultUrl, keepResponse: true);
        Add(statusUrl);
        return list;
    }
}
