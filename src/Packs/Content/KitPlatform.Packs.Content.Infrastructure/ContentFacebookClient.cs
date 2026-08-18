using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentFacebookClient
{
    private const string Graph = "https://graph.facebook.com/" + ContentFacebookConfigParser.GraphVersion;
    private readonly IHttpClientFactory _http;
    private readonly ContentRepository _repo;
    private readonly ContentOptions _options;
    private readonly IConfiguration _configuration;

    public ContentFacebookClient(
        IHttpClientFactory http,
        ContentRepository repo,
        IOptions<ContentOptions> options,
        IConfiguration configuration)
    {
        _http = http;
        _repo = repo;
        _options = options.Value;
        _configuration = configuration;
    }

    public async Task<ContentFacebookResolved> ResolveAsync(CancellationToken ct)
    {
        var row = await _repo.GetOrgSettingsAsync(ct);
        var state = ContentFacebookConfigParser.Parse(row.FacebookConfigJson);
        return ContentFacebookConfigParser.Resolve(state, _options, _configuration);
    }

    public async Task<(bool Ok, string Message)> TestAppAsync(CancellationToken ct)
    {
        var cfg = await ResolveAsync(ct);
        if (string.IsNullOrWhiteSpace(cfg.AppId) || string.IsNullOrWhiteSpace(cfg.AppSecret))
            return (false, "Chưa có App ID / App Secret — vào Model AI → Facebook.");
        try
        {
            var url = $"{Graph}/{Uri.EscapeDataString(cfg.AppId)}?access_token={Uri.EscapeDataString(cfg.AppId + "|" + cfg.AppSecret)}&fields=id,name";
            using var res = await Client().GetAsync(url, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                return (false, GraphMessage(body) ?? $"Meta trả {(int)res.StatusCode}.");
            return (true, "App Meta OK — nhớ khai Redirect URI trùng với ô bên dưới.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message.Length > 240 ? ex.Message[..240] : ex.Message);
        }
    }

    public async Task<string> ExchangeCodeAsync(string code, string redirectUri, CancellationToken ct)
    {
        var cfg = await RequireAppAsync(ct);
        var url =
            $"{Graph}/oauth/access_token?client_id={Uri.EscapeDataString(cfg.AppId!)}" +
            $"&client_secret={Uri.EscapeDataString(cfg.AppSecret!)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&code={Uri.EscapeDataString(code)}";
        var token = await ReadTokenAsync(url, ct);
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Facebook không trả User token từ mã OAuth.");
        return token;
    }

    public async Task<string> ExchangeLongLivedAsync(string shortLivedUserToken, CancellationToken ct)
    {
        var cfg = await RequireAppAsync(ct);
        var url =
            $"{Graph}/oauth/access_token?grant_type=fb_exchange_token" +
            $"&client_id={Uri.EscapeDataString(cfg.AppId!)}" +
            $"&client_secret={Uri.EscapeDataString(cfg.AppSecret!)}" +
            $"&fb_exchange_token={Uri.EscapeDataString(shortLivedUserToken)}";
        var token = await ReadTokenAsync(url, ct);
        return string.IsNullOrWhiteSpace(token) ? shortLivedUserToken : token;
    }

    public async Task<IReadOnlyList<FacebookPageToken>> ListPagesAsync(string userToken, CancellationToken ct)
    {
        var url = $"{Graph}/me/accounts?fields=id,name,access_token,tasks&limit=100&access_token={Uri.EscapeDataString(userToken)}";
        using var res = await Client().GetAsync(url, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(GraphMessage(body) ?? "Không lấy được danh sách Page.");

        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        var list = new List<FacebookPageToken>();
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return list;
        foreach (var item in data.EnumerateArray())
        {
            var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            var name = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            var token = item.TryGetProperty("access_token", out var tokEl) ? tokEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(token)) continue;
            list.Add(new FacebookPageToken(id.Trim(), (name ?? id).Trim(), token.Trim()));
        }
        return list;
    }

    public async Task<(bool Ok, string? PageName, string? Error)> InspectPageAsync(string pageId, string pageToken, CancellationToken ct)
    {
        var url = $"{Graph}/{Uri.EscapeDataString(pageId)}?fields=id,name&access_token={Uri.EscapeDataString(pageToken)}";
        using var res = await Client().GetAsync(url, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            return (false, null, GraphMessage(body) ?? $"Meta trả {(int)res.StatusCode}.");
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        var name = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() : null;
        return (true, name, null);
    }

    private async Task<ContentFacebookResolved> RequireAppAsync(CancellationToken ct)
    {
        var cfg = await ResolveAsync(ct);
        if (string.IsNullOrWhiteSpace(cfg.AppId) || string.IsNullOrWhiteSpace(cfg.AppSecret))
            throw new InvalidOperationException("Chưa cấu hình Facebook App — Model AI → Facebook.");
        return cfg;
    }

    private async Task<string?> ReadTokenAsync(string url, CancellationToken ct)
    {
        using var res = await Client().GetAsync(url, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(GraphMessage(body) ?? $"OAuth Meta {(int)res.StatusCode}.");
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            return doc.RootElement.TryGetProperty("access_token", out var t) ? t.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    private HttpClient Client() => _http.CreateClient("content-facebook");

    private static string? GraphMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            if (doc.RootElement.TryGetProperty("error", out var err))
            {
                var msg = err.TryGetProperty("message", out var m) ? m.GetString() : null;
                return string.IsNullOrWhiteSpace(msg) ? null : msg.Trim();
            }
        }
        catch
        {
            /* raw */
        }
        return null;
    }
}

internal sealed record FacebookPageToken(string Id, string Name, string AccessToken);
