using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentSeriesStillService : IContentSeriesStillService
{
    private const int MaxRefs = 4;
    private const int MaxDataUrlChars = 2_400_000;

    private readonly ContentGeminiClient _gemini;

    public ContentSeriesStillService(ContentGeminiClient gemini)
    {
        _gemini = gemini;
    }

    public async Task<ContentSeriesStillDto> GenerateAsync(
        ContentSeriesStillRequest request,
        CancellationToken cancellationToken = default)
    {
        var prompt = (request.Prompt ?? "").Trim();
        if (prompt.Length is < 12 or > 4000)
            throw new InvalidOperationException("Prompt KF cảnh 12–4000 ký tự.");

        var aspect = NormalizeAspect(request.Aspect);
        var refs = request.References ?? Array.Empty<ContentSeriesStillRefDto>();
        if (refs.Count is < 1 or > MaxRefs)
            throw new InvalidOperationException("Cần 1–4 ảnh Canon mặt (không gửi crop mặt làm KF I2V).");

        var parsed = new List<(string Mime, string Base64)>(refs.Count);
        foreach (var row in refs)
        {
            var url = (row.ImageDataUrl ?? "").Trim();
            if (url.Length is < 32 or > MaxDataUrlChars)
                throw new InvalidOperationException($"Canon {(row.Name ?? "CHAR")} quá lớn hoặc trống.");
            if (!TryParseDataUrl(url, out var mime, out var b64))
                throw new InvalidOperationException($"Canon {(row.Name ?? "CHAR")} không phải data URL ảnh.");
            parsed.Add((mime, b64));
        }

        var guarded =
            "Photorealistic Vietnamese live-action SCENE still. Full environment + bodies in frame. "
            + "Attached images are FACE/WARDROBE REFERENCES only — do not output a passport crop, contact sheet, or headshot grid. "
            + "Match each named person's face, hair, age and clothes from the matching reference. "
            + "No extra people, no text, no subtitles, no watermark, no logo, no illustration.\n"
            + prompt;

        var (bytes, model) = await _gemini.GenerateImageWithRefsAsync(
            guarded,
            parsed,
            aspect,
            cancellationToken);
        var mimeOut = bytes.Length >= 8 && bytes[0] == 0x89 ? "image/png" : "image/jpeg";
        return new ContentSeriesStillDto(
            $"data:{mimeOut};base64,{Convert.ToBase64String(bytes)}",
            model,
            aspect);
    }

    private static string NormalizeAspect(string? raw)
    {
        var s = (raw ?? "").Trim();
        if (s is "9:16" or "16:9" or "1:1" or "4:3" or "3:4") return s;
        return "9:16";
    }

    private static bool TryParseDataUrl(string raw, out string mime, out string b64)
    {
        mime = "image/jpeg";
        b64 = "";
        var s = raw.Trim();
        if (!s.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return false;
        var comma = s.IndexOf(',');
        if (comma < 6 || comma >= s.Length - 8) return false;
        var meta = s[..comma];
        b64 = s[(comma + 1)..];
        if (b64.Length < 24) return false;
        var mimePart = meta[5..].Split(';')[0].Trim().ToLowerInvariant();
        if (mimePart is "image/png" or "image/jpeg" or "image/jpg" or "image/webp")
            mime = mimePart == "image/jpg" ? "image/jpeg" : mimePart;
        else if (mimePart.StartsWith("image/", StringComparison.Ordinal))
            mime = mimePart;
        else
            return false;
        return true;
    }
}
