using System.Security.Cryptography;
using System.Text;

namespace KitPlatform.Packs.Content;

/// <summary>
/// Maps a Veo task id or local playback URL to a file-name token.
/// Does not read disk, credentials, or the network.
/// </summary>
public static class FamixaVeoTakePlaybackV1
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
        if (raw.StartsWith("veo_", StringComparison.OrdinalIgnoreCase))
            return FileId(raw);
        if (Uri.TryCreate(raw, UriKind.Absolute, out var abs) && abs.IsAbsoluteUri)
            raw = abs.PathAndQuery;
        var q = raw.IndexOf("taskId=", StringComparison.OrdinalIgnoreCase);
        if (q >= 0)
        {
            var id = raw[(q + 7)..];
            var amp = id.IndexOf('&');
            if (amp >= 0) id = id[..amp];
            id = UnescapeOrNull(id);
            return id is not null && id.StartsWith("veo_", StringComparison.OrdinalIgnoreCase)
                ? FileId(id)
                : null;
        }

        const string takes = "/api/content/series/veo-takes/";
        var i = raw.IndexOf(takes, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return null;
        var fileId = raw[(i + takes.Length)..].Trim('/');
        var cut = fileId.IndexOfAny(['?', '#']);
        if (cut >= 0) fileId = fileId[..cut];
        return SafeStoredFileId(UnescapeOrNull(fileId));
    }

    /// <summary>File-name token only. A raw URL path is not a filesystem path.</summary>
    public static string? SafeStoredFileId(string? fileId)
    {
        var id = (fileId ?? "").Trim();
        if (id.Length is < 8 or > 64) return null;
        foreach (var ch in id)
        {
            if (!(char.IsAsciiLetterOrDigit(ch) || ch == '-')) return null;
        }

        return id;
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
