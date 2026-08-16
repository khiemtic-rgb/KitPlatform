using System.Net.Http.Headers;
using System.Text.Json;
using KitPlatform.Packs.LocalOs;
using Microsoft.Extensions.Options;

namespace KitPlatform.Api.LocalOs;

/// <summary>Đọc báo cáo độc giả từ Worker KV (thainguyenlife.vn). Không ghi feed.</summary>
public sealed class LocalOsReaderReportInbox
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly LocalOsHomepageFeedOptions _options;
    private readonly ILogger<LocalOsReaderReportInbox> _log;

    public LocalOsReaderReportInbox(
        HttpClient http,
        IOptions<LocalOsHomepageFeedOptions> options,
        ILogger<LocalOsReaderReportInbox> log)
    {
        _http = http;
        _options = options.Value;
        _log = log;
        _http.Timeout = TimeSpan.FromSeconds(20);
    }

    public async Task<IReadOnlyList<LocalListingReportDto>> PullAsync(
        CancellationToken cancellationToken = default)
    {
        var url = ReportsUrl(_options.PublicFeedUrl);
        var secret = (_options.PublicFeedSecret ?? "").Trim();
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(secret))
            return [];

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        try
        {
            using var res = await _http.SendAsync(req, cancellationToken);
            if (!res.IsSuccessStatusCode) return [];
            var body = await res.Content.ReadAsStringAsync(cancellationToken);
            var parsed = JsonSerializer.Deserialize<InboxPayload>(body, Json);
            return (parsed?.Reports ?? [])
                .Where(r => r.ListingId != Guid.Empty && !string.IsNullOrWhiteSpace(r.Reason))
                .Select(r => new LocalListingReportDto(
                    r.Id == Guid.Empty ? Guid.CreateVersion7() : r.Id,
                    r.ListingId,
                    r.Reason.Trim().ToLowerInvariant(),
                    string.IsNullOrWhiteSpace(r.Note) ? null : r.Note.Trim(),
                    r.CreatedAt == default ? DateTimeOffset.UtcNow : r.CreatedAt,
                    null, null, null))
                .ToList();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Không đọc được báo cáo từ trang chủ mạng.");
            return [];
        }
    }

    internal static string? ReportsUrl(string? feedUrl)
    {
        var u = (feedUrl ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(u)) return null;
        if (u.EndsWith("/api/feed", StringComparison.OrdinalIgnoreCase))
            return string.Concat(u.AsSpan(0, u.Length - "/api/feed".Length), "/api/reports");
        if (u.EndsWith("/feed", StringComparison.OrdinalIgnoreCase))
            return string.Concat(u.AsSpan(0, u.Length - "/feed".Length), "/reports");
        return u + "/reports";
    }

    private sealed class InboxPayload
    {
        public List<RemoteReport> Reports { get; set; } = [];
    }

    private sealed class RemoteReport
    {
        public Guid Id { get; set; }
        public Guid ListingId { get; set; }
        public string Reason { get; set; } = "";
        public string? Note { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
