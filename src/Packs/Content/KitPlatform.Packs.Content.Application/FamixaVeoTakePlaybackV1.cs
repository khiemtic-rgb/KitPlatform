using System.Security.Cryptography;
using System.Text;

namespace KitPlatform.Packs.Content;

/// <summary>Veo take is a local mp4, not a data-URI in poll JSON. 0 provider calls.</summary>
public static class FamixaVeoTakePlaybackV1
{
    public const string Route = "/api/content/series/veo-video";

    public static string FileId(string taskId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(taskId ?? ""));
        return Convert.ToHexString(hash).ToLowerInvariant()[..32];
    }

    public static string PublicPath(string taskId)
    {
        var id = (taskId ?? "").Trim();
        if (id.Length < 8) throw new InvalidOperationException("VEO_TAKE: missing task id.");
        return $"{Route}?taskId={Uri.EscapeDataString(id)}";
    }

    public static bool IsStoredTake(string? urlOrTaskId)
        => FileIdFromUrl(urlOrTaskId) is { Length: >= 8 };

    public static string? FileIdFromUrl(string? url)
    {
        var raw = (url ?? "").Trim();
        if (raw.Length == 0) return null;
        if (raw.StartsWith("veo_", StringComparison.OrdinalIgnoreCase))
            return FileId(raw);
        if (Uri.TryCreate(raw, UriKind.Absolute, out var abs))
            raw = abs.PathAndQuery;
        var q = raw.IndexOf("taskId=", StringComparison.OrdinalIgnoreCase);
        if (q >= 0)
        {
            var id = raw[(q + 7)..];
            var amp = id.IndexOf('&');
            if (amp >= 0) id = id[..amp];
            id = Uri.UnescapeDataString(id);
            return id.StartsWith("veo_", StringComparison.OrdinalIgnoreCase) ? FileId(id) : null;
        }
        const string takes = "/api/content/series/veo-takes/";
        var i = raw.IndexOf(takes, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return null;
        var fileId = raw[(i + takes.Length)..].Trim('/');
        var cut = fileId.IndexOfAny(['?', '#']);
        if (cut >= 0) fileId = fileId[..cut];
        return fileId.Length >= 8 ? fileId : null;
    }
}
