using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>Optional TTS for Video MVP V1. No-op when API key missing.</summary>
internal sealed class ContentElevenLabsClient
{
    private readonly HttpClient _http;
    private readonly ContentRepository _repo;
    private readonly IOptions<ContentOptions> _options;
    private readonly IConfiguration _configuration;

    public ContentElevenLabsClient(
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
            _http.BaseAddress = new Uri("https://api.elevenlabs.io/");
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
        (await ResolveAsync(cancellationToken)).ElevenLabsConfigured;

    public async Task<(bool Ok, string Message)> TestConnectionAsync(CancellationToken cancellationToken)
    {
        var resolved = await ResolveAsync(cancellationToken);
        if (!resolved.ElevenLabsConfigured)
            return (false, "Chưa có ElevenLabs API key — đặt Secret ref hoặc dán key (chỉ ghi).");

        using var req = new HttpRequestMessage(HttpMethod.Get, "v1/user");
        req.Headers.Add("xi-api-key", resolved.ElevenLabsApiKey);
        using var res = await _http.SendAsync(req, cancellationToken);
        var body = await res.Content.ReadAsStringAsync(cancellationToken);
        if (!res.IsSuccessStatusCode)
        {
            var snippet = body.Length > 300 ? body[..300] + "…" : body;
            return (false, $"ElevenLabs {(int)res.StatusCode}: {snippet}");
        }
        return (true, $"ElevenLabs kết nối OK · voice {resolved.VoiceId}");
    }

    public async Task<byte[]> SynthesizeMp3Async(string text, CancellationToken cancellationToken)
    {
        var resolved = await ResolveAsync(cancellationToken);
        var key = resolved.ElevenLabsApiKey
                  ?? throw new InvalidOperationException("Chưa cấu hình ElevenLabsApiKey.");
        var voice = resolved.VoiceId;
        var body = new
        {
            text,
            model_id = "eleven_multilingual_v2",
            voice_settings = new { stability = 0.4, similarity_boost = 0.7 },
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, $"v1/text-to-speech/{Uri.EscapeDataString(voice)}");
        req.Headers.Add("xi-api-key", key);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/mpeg"));
        req.Content = JsonContent.Create(body);

        using var res = await _http.SendAsync(req, cancellationToken);
        var bytes = await res.Content.ReadAsByteArrayAsync(cancellationToken);
        if (!res.IsSuccessStatusCode)
        {
            var snippet = System.Text.Encoding.UTF8.GetString(bytes);
            if (snippet.Length > 300) snippet = snippet[..300] + "…";
            throw new InvalidOperationException($"ElevenLabs TTS failed ({(int)res.StatusCode}): {snippet}");
        }

        if (bytes.Length < 100)
            throw new InvalidOperationException("ElevenLabs trả audio rỗng.");
        return bytes;
    }
}
