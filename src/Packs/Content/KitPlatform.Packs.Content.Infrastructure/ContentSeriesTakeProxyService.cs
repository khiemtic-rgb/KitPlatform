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
