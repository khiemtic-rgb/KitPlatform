using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KitPlatform.Packs.LocalOs;
using Microsoft.Extensions.Options;

namespace KitPlatform.Api.LocalOs;

public sealed class LocalOsHomepageFeedOptions
{
    public const string Section = "LocalOs";
    public string PublicFeedUrl { get; set; } = "https://thainguyenlife.vn/api/feed";
    public string PublicFeedSecret { get; set; } = "";
}

public sealed record LocalOsFeedPublishResult(bool Ok, string Message, int ListingCount, bool Skipped = false);

public sealed class LocalOsHomepageFeedPublisher
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ILocalOsListingService _listings;
    private readonly ILocalOsPublisherService _publishers;
    private readonly HttpClient _http;
    private readonly LocalOsHomepageFeedOptions _options;
    private readonly IHostEnvironment _env;
    private readonly ILogger<LocalOsHomepageFeedPublisher> _log;

    public LocalOsHomepageFeedPublisher(
        ILocalOsListingService listings,
        ILocalOsPublisherService publishers,
        HttpClient http,
        IOptions<LocalOsHomepageFeedOptions> options,
        IHostEnvironment env,
        ILogger<LocalOsHomepageFeedPublisher> log)
    {
        _listings = listings;
        _publishers = publishers;
        _http = http;
        _options = options.Value;
        _env = env;
        _log = log;
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<LocalOsFeedPublishResult> PublishAsync(CancellationToken cancellationToken = default)
    {
        var url = (_options.PublicFeedUrl ?? "").Trim();
        var secret = (_options.PublicFeedSecret ?? "").Trim();
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(secret))
            return new LocalOsFeedPublishResult(true, "Chưa cấu hình đẩy trang chủ mạng.", 0, Skipped: true);
        if (_env.IsDevelopment() && url.Contains("thainguyenlife.vn", StringComparison.OrdinalIgnoreCase))
            return new LocalOsFeedPublishResult(
                true,
                "Máy local không đẩy thainguyenlife.vn — tin Đang đăng ở đây chưa lên trang chủ mạng.",
                0,
                Skipped: true);

        var listings = await _listings.ListAsync(
            new LocalListingQuery(null, null, null, null, PublicOnly: true),
            cancellationToken);
        var jobCount = listings.Count(x => string.Equals(x.Kind, "job", StringComparison.OrdinalIgnoreCase));
        if (listings.Count == 0 || jobCount < 8)
            return new LocalOsFeedPublishResult(
                false,
                $"Không đẩy trang chủ khi việc công khai còn {jobCount} — giữ bản đang chạy.",
                listings.Count);

        var groups = await _publishers.RecommendGroupsAsync(null, "student", cancellationToken);
        var payload = JsonSerializer.Serialize(
            new { listings, groups, exportedAt = DateTimeOffset.UtcNow },
            Json);

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        req.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        try
        {
            using var res = await _http.SendAsync(req, cancellationToken);
            var body = await res.Content.ReadAsStringAsync(cancellationToken);
            if (!res.IsSuccessStatusCode)
            {
                _log.LogWarning("Homepage feed push failed {Status}: {Body}", (int)res.StatusCode, body);
                return new LocalOsFeedPublishResult(
                    false,
                    $"Trang chủ mạng trả {(int)res.StatusCode}. Thử lại nút Cập nhật trang chủ.",
                    listings.Count);
            }

            return new LocalOsFeedPublishResult(true, "Đã lên trang chủ Thái Nguyên Life.", listings.Count);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Homepage feed push failed");
            return new LocalOsFeedPublishResult(
                false,
                "Không gọi được trang chủ mạng. Kiểm tra internet rồi bấm Cập nhật trang chủ.",
                listings.Count);
        }
    }
}
