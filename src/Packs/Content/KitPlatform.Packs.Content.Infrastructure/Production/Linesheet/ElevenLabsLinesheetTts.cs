using System.Net.Http.Headers;
using System.Net.Http.Json;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Gọi ElevenLabs Eleven v3 cho M1. Khóa API lấy đúng chỗ mà Content đang dùng (cài đặt Video của tổ chức, qua
/// <see cref="ContentElevenLabsClient.ResolveAsync"/>), không lưu khóa mới.
/// Khác <c>ContentElevenLabsClient.SynthesizeMp3Async</c> ở chỗ GIỮ thẻ cảm xúc [..] (client cũ bỏ thẻ), vì M1 cần thẻ
/// để ra cảm xúc. Theo kinh nghiệm của client cũ với giọng Bắc: không gửi language_code, tắt chuẩn hóa chữ, cố định seed theo giọng.
/// </summary>
internal sealed class ElevenLabsLinesheetTts : ILinesheetTts
{
    public const string HttpClientName = "famixa-linesheet-tts";

    readonly IHttpClientFactory _httpFactory;
    readonly ContentElevenLabsClient _eleven;

    public ElevenLabsLinesheetTts(IHttpClientFactory httpFactory, ContentElevenLabsClient eleven)
    {
        _httpFactory = httpFactory;
        _eleven = eleven;
    }

    public async Task<byte[]> SynthesizeAsync(string voiceId, string text, LinesheetVoiceDef voice, int take, CancellationToken ct)
    {
        NovixaE2eIsolation.ThrowIfExternalCall("ElevenLabs");
        var resolved = await _eleven.ResolveAsync(ct);
        var key = resolved.ElevenLabsApiKey;
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("LINESHEET_TTS_NO_KEY: chưa cấu hình khóa ElevenLabs trong cài đặt AI của Content.");
        if (string.IsNullOrWhiteSpace(voiceId)) throw new InvalidOperationException("LINESHEET_TTS_NO_VOICE");
        if (text.Length > 4900) text = text[..4900];

        var body = new
        {
            text,
            model_id = LinesheetTtsPlan.Model,
            apply_text_normalization = "off",
            seed = Seed(voiceId, take),
            voice_settings = new
            {
                stability = Snap(voice.Stability),
                similarity_boost = Math.Clamp(voice.SimilarityBoost, 0, 1),
                style = Math.Clamp(voice.Style, 0, 1),
                speed = Math.Clamp(voice.Speed, 0.7, 1.2),
                use_speaker_boost = true,
            },
        };

        var http = _httpFactory.CreateClient(HttpClientName);
        for (var attempt = 1; ; attempt++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post,
                $"https://api.elevenlabs.io/v1/text-to-speech/{Uri.EscapeDataString(voiceId.Trim())}?output_format=mp3_44100_128");
            req.Headers.Add("xi-api-key", key);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/mpeg"));
            req.Content = JsonContent.Create(body);
            using var res = await http.SendAsync(req, ct);
            var bytes = await res.Content.ReadAsByteArrayAsync(ct);
            if (res.IsSuccessStatusCode)
            {
                if (bytes.Length < 200) throw new InvalidOperationException("LINESHEET_TTS_EMPTY");
                return bytes;
            }

            // 409 already_running: lần đầu dùng giọng thư viện, ElevenLabs đang thêm giọng vào tài khoản và từ chối
            // các câu gọi cùng lúc (EP01 gặp 2 lần). 429: quá nhiều yêu cầu. Cả hai chưa tính ký tự nên chờ rồi gửi lại.
            if (IsRetryable((int)res.StatusCode) && attempt < MaxAttempts)
            {
                await Task.Delay(RetryDelay(attempt), ct);
                continue;
            }

            var snippet = System.Text.Encoding.UTF8.GetString(bytes);
            if (snippet.Length > 240) snippet = snippet[..240] + "…";
            throw new InvalidOperationException($"LINESHEET_TTS_FAILED ({(int)res.StatusCode}): {snippet}");
        }
    }

    internal const int MaxAttempts = 4;

    internal static bool IsRetryable(int status) => status is 409 or 429;

    /// <summary>Chờ 3, 6, 12 giây.</summary>
    internal static TimeSpan RetryDelay(int attempt) => TimeSpan.FromSeconds(3 * Math.Pow(2, attempt - 1));

    /// <summary>Eleven v3 chỉ nhận stability 0 / 0.5 / 1.</summary>
    internal static double Snap(double s) => s < 0.25 ? 0.0 : s < 0.75 ? 0.5 : 1.0;

    /// <summary>
    /// Seed cố định theo giọng để tạo lại một câu vẫn ra cùng chất giọng. Lần tạo 2, 3… lệch seed đi để ra bản đọc khác
    /// (dùng khi một câu bị lơ lớ vài chữ). Lần 1 giữ đúng seed cũ.
    /// </summary>
    internal static int Seed(string voiceId, int take = 1)
    {
        unchecked
        {
            var h = 17;
            foreach (var c in voiceId) h = h * 31 + c;
            var baseSeed = Math.Abs(h % 4_000_000);
            return take <= 1 ? baseSeed : (baseSeed + (take - 1) * 104_729) % 4_000_000;
        }
    }
}
