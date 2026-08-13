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
    private readonly IOptions<ContentOptions> _options;
    private readonly IConfiguration _configuration;

    public ContentElevenLabsClient(
        HttpClient http,
        IOptions<ContentOptions> options,
        IConfiguration configuration)
    {
        _http = http;
        _options = options;
        _configuration = configuration;
        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri("https://api.elevenlabs.io/");
    }

    public string? ResolveApiKey() =>
        FirstNonEmpty(
            _options.Value.ElevenLabsApiKey,
            _configuration["Content:ElevenLabsApiKey"],
            Environment.GetEnvironmentVariable("ELEVENLABS_API_KEY"));

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ResolveApiKey());

    public string ResolveVoiceId() =>
        FirstNonEmpty(
            _options.Value.ElevenLabsVoiceId,
            _configuration["Content:ElevenLabsVoiceId"],
            Environment.GetEnvironmentVariable("ELEVENLABS_VOICE_ID"))
        ?? "21m00Tcm4TlvDq8ikWAM"; // Rachel

    public async Task<byte[]> SynthesizeMp3Async(string text, CancellationToken cancellationToken)
    {
        var key = ResolveApiKey()
                  ?? throw new InvalidOperationException("Chưa cấu hình ElevenLabsApiKey.");
        var voice = ResolveVoiceId();
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

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}
