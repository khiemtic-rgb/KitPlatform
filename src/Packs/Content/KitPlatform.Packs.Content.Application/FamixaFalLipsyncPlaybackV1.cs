using System.Security.Cryptography;
using System.Text;

namespace KitPlatform.Packs.Content;

/// <summary>
/// Maps a Fal lipsync task id or local playback URL to a file-name token.
/// Does not read disk, CDN, credentials, or the network.
/// </summary>
public static class FamixaFalLipsyncPlaybackV1
{
    public static string FileId(string taskId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(taskId ?? ""));
        return Convert.ToHexString(hash).ToLowerInvariant()[..32];
    }

    public static string? FileIdFromUrl(string? url)
    {
        var raw = (url ?? "").Trim();
        if (raw.Length == 0) return null;
        if (IsLipsyncTask(raw))
            return FileId(raw);
        if (Uri.TryCreate(raw, UriKind.Absolute, out var abs) && abs.IsAbsoluteUri)
            raw = abs.PathAndQuery;
        var q = raw.IndexOf("taskId=", StringComparison.OrdinalIgnoreCase);
        if (q < 0) return null;
        if (!raw.Contains("/content/series/fal-video", StringComparison.OrdinalIgnoreCase))
            return null;
        var id = raw[(q + 7)..];
        var amp = id.IndexOf('&');
        if (amp >= 0) id = id[..amp];
        id = UnescapeOrNull(id);
        return id is not null && IsLipsyncTask(id) ? FileId(id) : null;
    }

    private static bool IsLipsyncTask(string? taskId)
    {
        var t = (taskId ?? "").Trim();
        return t.StartsWith("lipsync_", StringComparison.OrdinalIgnoreCase);
    }

    private static string? UnescapeOrNull(string value)
    {
        try
        {
            return Uri.UnescapeDataString(value);
        }
        catch (UriFormatException)
        {
            return null;
        }
    }
}
