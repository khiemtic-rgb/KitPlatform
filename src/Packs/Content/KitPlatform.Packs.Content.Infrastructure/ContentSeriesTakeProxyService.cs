using System.Net;
using System.Net.Sockets;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>Fetch Runway/Fal take bytes for operator download. Blocks private hosts.</summary>
internal sealed class ContentSeriesTakeProxyService : IContentSeriesTakeProxyService
{
    private const int MaxBytes = 80_000_000;
    private readonly IHttpClientFactory _http;

    public ContentSeriesTakeProxyService(IHttpClientFactory http)
    {
        _http = http;
    }

    public async Task<(byte[] Bytes, string ContentType, string FileName)> FetchAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate((url ?? "").Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Chỉ tải take HTTPS (Runway / Fal).");
        }

        if (IsBlockedHost(uri.Host))
            throw new InvalidOperationException("Không tải take từ máy nội bộ.");

        var client = _http.CreateClient("content-take-proxy");
        using var res = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Không tải được take ({(int)res.StatusCode}). Link Runway có thể hết hạn — gửi lại clip.");
        }

        var type = res.Content.Headers.ContentType?.MediaType ?? "video/mp4";
        if (type.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            || type.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("URL không trả file video.");
        }

        var len = res.Content.Headers.ContentLength;
        if (len is > MaxBytes)
            throw new InvalidOperationException("Take lớn hơn 80MB — tải trực tiếp từ Runway.");

        await using var stream = await res.Content.ReadAsStreamAsync(cancellationToken);
        using var buf = new MemoryStream();
        var copy = new byte[64 * 1024];
        var total = 0;
        int n;
        while ((n = await stream.ReadAsync(copy, cancellationToken)) > 0)
        {
            total += n;
            if (total > MaxBytes)
                throw new InvalidOperationException("Take lớn hơn 80MB — tải trực tiếp từ Runway.");
            await buf.WriteAsync(copy.AsMemory(0, n), cancellationToken);
        }

        var name = Path.GetFileName(uri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80 || !name.Contains('.'))
            name = "take.mp4";
        return (buf.ToArray(), type.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ? type : "video/mp4", name);
    }

    public async Task<ContentSeriesTakeProbeDto> ProbeAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate((url ?? "").Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            return new ContentSeriesTakeProbeDto(false, null, null, "Chỉ đọc take HTTPS.");
        }

        if (IsBlockedHost(uri.Host))
            return new ContentSeriesTakeProbeDto(false, null, null, "Không đọc take từ máy nội bộ.");

        var client = _http.CreateClient("content-take-proxy");
        try
        {
            using var head = new HttpRequestMessage(HttpMethod.Head, uri);
            using var headRes = await client.SendAsync(head, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (headRes.IsSuccessStatusCode)
            {
                var mime = headRes.Content.Headers.ContentType?.MediaType;
                var bytes = headRes.Content.Headers.ContentLength;
                if (LooksLikeHtmlOrJson(mime))
                    return new ContentSeriesTakeProbeDto(false, mime, bytes, "URL không trả file video.");
                if (bytes is 0)
                    return new ContentSeriesTakeProbeDto(false, mime, bytes, "File 0 byte.");
                if (bytes is > MaxBytes)
                    return new ContentSeriesTakeProbeDto(false, mime, bytes, "Take lớn hơn 80MB.");
                if ((mime ?? "").StartsWith("video/", StringComparison.OrdinalIgnoreCase) || bytes is > 800)
                    return new ContentSeriesTakeProbeDto(true, mime ?? "video/mp4", bytes, null);
            }
        }
        catch
        {
            /* HEAD often blocked — fall through to ranged GET */
        }

        try
        {
            using var get = new HttpRequestMessage(HttpMethod.Get, uri);
            get.Headers.TryAddWithoutValidation("Range", "bytes=0-63");
            using var getRes = await client.SendAsync(get, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!getRes.IsSuccessStatusCode && (int)getRes.StatusCode != 206)
                return new ContentSeriesTakeProbeDto(false, null, null, $"Không đọc được take ({(int)getRes.StatusCode}).");

            var mime = getRes.Content.Headers.ContentType?.MediaType;
            var bytes = getRes.Content.Headers.ContentLength
                        ?? getRes.Content.Headers.ContentRange?.Length;
            if (LooksLikeHtmlOrJson(mime))
                return new ContentSeriesTakeProbeDto(false, mime, bytes, "URL không trả file video.");

            await using var stream = await getRes.Content.ReadAsStreamAsync(cancellationToken);
            var prefix = new byte[64];
            var n = await stream.ReadAsync(prefix.AsMemory(0, prefix.Length), cancellationToken);
            if (n < 12 || !LooksLikeVideoMagic(prefix.AsSpan(0, n)))
                return new ContentSeriesTakeProbeDto(false, mime, bytes, "File không phải video.");
            if (bytes is 0)
                return new ContentSeriesTakeProbeDto(false, mime, bytes, "File 0 byte.");
            return new ContentSeriesTakeProbeDto(true, mime ?? "video/mp4", bytes, null);
        }
        catch (Exception ex)
        {
            return new ContentSeriesTakeProbeDto(false, null, null, ex.Message);
        }
    }

    private static bool LooksLikeHtmlOrJson(string? mime)
    {
        var t = (mime ?? "").ToLowerInvariant();
        return t.StartsWith("text/") || t.Contains("json") || t.Contains("html");
    }

    private static bool LooksLikeVideoMagic(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 12
            && b[4] == (byte)'f' && b[5] == (byte)'t' && b[6] == (byte)'y' && b[7] == (byte)'p')
            return true;
        if (b.Length >= 4 && b[0] == 0x1A && b[1] == 0x45 && b[2] == 0xDF && b[3] == 0xA3)
            return true;
        return false;
    }

    private static bool IsBlockedHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(host, out var ip))
            return false;
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            if (b[0] == 10) return true;
            if (b[0] == 127) return true;
            if (b[0] == 192 && b[1] == 168) return true;
            if (b[0] == 169 && b[1] == 254) return true;
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
        }

        return false;
    }
}
