using System.Security.Cryptography;
using System.Text;

namespace KitPlatform.Packs.Content;

/// <summary>Fal lipsync take is a local mp4, not a queue URL. 0 provider calls.</summary>
public static class FamixaFalLipsyncPlaybackV1
{
    public const string Route = "/api/content/series/fal-video";

    public static string FileId(string taskId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(taskId ?? ""));
        return Convert.ToHexString(hash).ToLowerInvariant()[..32];
    }

    public static string PublicPath(string taskId)
    {
        var id = (taskId ?? "").Trim();
        if (id.Length < 8) throw new InvalidOperationException("FAL_LIPSYNC: missing task id.");
        return $"{Route}?taskId={Uri.EscapeDataString(id)}";
    }

    public static bool IsLocalPointer(string? url)
    {
        var raw = (url ?? "").Trim();
        return raw.Contains("/content/series/fal-video", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Playable LipSync source. Local /fal-video only after this API can read the mp4.
    /// Persist failure must keep the provider CDN — never mint a broken pointer.
    /// </summary>
    public static string CanonicalPlayableUrl(string? providerOutputUrl, bool localReadable, string? taskId)
    {
        var id = (taskId ?? "").Trim();
        if (localReadable && IsLipsyncTask(id))
            return PublicPath(id);
        var provider = (providerOutputUrl ?? "").Trim();
        if (IsLocalPointer(provider) && !localReadable)
            return "";
        return provider;
    }

    public static bool IsStoredTake(string? urlOrTaskId)
        => FileIdFromUrl(urlOrTaskId) is { Length: >= 8 };

    public static bool IsLipsyncTask(string? taskId)
    {
        var t = (taskId ?? "").Trim();
        return t.StartsWith("lipsync_", StringComparison.OrdinalIgnoreCase);
    }

    public static string? FileIdFromUrl(string? url)
    {
        var raw = (url ?? "").Trim();
        if (raw.Length == 0) return null;
        if (IsLipsyncTask(raw))
            return FileId(raw);
        if (Uri.TryCreate(raw, UriKind.Absolute, out var abs))
            raw = abs.PathAndQuery;
        var q = raw.IndexOf("taskId=", StringComparison.OrdinalIgnoreCase);
        if (q < 0) return null;
        if (!raw.Contains("/content/series/fal-video", StringComparison.OrdinalIgnoreCase))
            return null;
        var id = raw[(q + 7)..];
        var amp = id.IndexOf('&');
        if (amp >= 0) id = id[..amp];
        id = Uri.UnescapeDataString(id);
        return IsLipsyncTask(id) ? FileId(id) : null;
    }
}
