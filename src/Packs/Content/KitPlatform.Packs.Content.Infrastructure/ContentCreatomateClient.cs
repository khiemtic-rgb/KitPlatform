using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentCreatomateClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly ContentRepository _repo;
    private readonly IOptions<ContentOptions> _options;
    private readonly IConfiguration _configuration;

    public ContentCreatomateClient(
        HttpClient http,
        ContentRepository repo,
        IOptions<ContentOptions> options,
        IConfiguration configuration)
    {
        _http = http;
        _repo = repo;
        _options = options;
        _configuration = configuration;
        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri("https://api.creatomate.com/");
    }

    public async Task<ContentVideoResolved> ResolveAsync(CancellationToken cancellationToken)
    {
        var row = await _repo.GetOrgSettingsAsync(cancellationToken);
        return ContentVideoConfigParser.Resolve(
            ContentVideoConfigParser.Parse(row.VideoConfigJson),
            _options.Value,
            _configuration);
    }

    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken) =>
        (await ResolveAsync(cancellationToken)).CreatomateConfigured;

    public async Task<(bool Ok, string Message)> TestConnectionAsync(CancellationToken cancellationToken)
    {
        var resolved = await ResolveAsync(cancellationToken);
        if (!resolved.CreatomateConfigured)
            return (false, "Chưa có Creatomate API key — đặt Secret ref hoặc dán key (chỉ ghi).");

        using var req = new HttpRequestMessage(HttpMethod.Get, "v1/templates");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", resolved.CreatomateApiKey);
        using var res = await _http.SendAsync(req, cancellationToken);
        var body = await res.Content.ReadAsStringAsync(cancellationToken);
        if (!res.IsSuccessStatusCode)
            return (false, $"Creatomate {(int)res.StatusCode}: {Truncate(body)}");
        return (true, "Creatomate kết nối OK");
    }

    public async Task<CreatomateRenderResult> CreateRenderAsync(
        string templateId,
        IReadOnlyDictionary<string, string> modifications,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolveAsync(cancellationToken);
        var key = resolved.CreatomateApiKey
                  ?? throw new InvalidOperationException("Chưa cấu hình CreatomateApiKey.");

        using var req = new HttpRequestMessage(HttpMethod.Post, "v1/renders");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        req.Content = JsonContent.Create(new
        {
            template_id = templateId,
            modifications,
        }, options: JsonOpts);

        using var res = await _http.SendAsync(req, cancellationToken);
        var body = await res.Content.ReadAsStringAsync(cancellationToken);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Creatomate render failed ({(int)res.StatusCode}): {Truncate(body)}");

        var parsed = JsonSerializer.Deserialize<CreatomateRenderDto[]>(body, JsonOpts);
        CreatomateRenderDto? row = parsed?.FirstOrDefault();
        if (row is null)
            row = JsonSerializer.Deserialize<CreatomateRenderDto>(body, JsonOpts);
        if (row is null || string.IsNullOrWhiteSpace(row.Id))
            throw new InvalidOperationException("Creatomate không trả render id.");
        return new CreatomateRenderResult(row.Id, row.Status ?? "planned", row.Url, row.SnapshotUrl);
    }

    public async Task<CreatomateRenderResult> GetRenderAsync(string renderId, CancellationToken cancellationToken)
    {
        var resolved = await ResolveAsync(cancellationToken);
        var key = resolved.CreatomateApiKey
                  ?? throw new InvalidOperationException("Chưa cấu hình CreatomateApiKey.");

        using var req = new HttpRequestMessage(HttpMethod.Get, $"v1/renders/{Uri.EscapeDataString(renderId)}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var res = await _http.SendAsync(req, cancellationToken);
        var body = await res.Content.ReadAsStringAsync(cancellationToken);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Creatomate get failed ({(int)res.StatusCode}): {Truncate(body)}");

        var row = JsonSerializer.Deserialize<CreatomateRenderDto>(body, JsonOpts)
                  ?? throw new InvalidOperationException("Creatomate response invalid.");
        return new CreatomateRenderResult(row.Id ?? renderId, row.Status ?? "unknown", row.Url, row.SnapshotUrl);
    }

    private static string Truncate(string s) =>
        s.Length <= 400 ? s : s[..400] + "…";

    private sealed class CreatomateRenderDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("snapshot_url")]
        public string? SnapshotUrl { get; set; }
    }
}

internal sealed record CreatomateRenderResult(
    string Id,
    string Status,
    string? Url,
    string? SnapshotUrl);
