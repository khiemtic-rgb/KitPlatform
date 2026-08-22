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
        var kept = new List<ContentSeriesStillRefDto>();
        foreach (var row in refs)
        {
            var url = (row.ImageDataUrl ?? "").Trim();
            var scene = string.Equals(row.Role, "scene", StringComparison.OrdinalIgnoreCase);
            if (url.Length is < 32 or > MaxDataUrlChars)
            {
                if (scene) continue;
                throw new InvalidOperationException($"Canon {(row.Name ?? "CHAR")} quá lớn hoặc trống.");
            }
            if (!TryParseDataUrl(url, out var mime, out var b64))
            {
                if (scene) continue;
                throw new InvalidOperationException($"Canon {(row.Name ?? "CHAR")} không phải data URL ảnh.");
            }
            parsed.Add((mime, b64));
            kept.Add(row);
        }
        refs = kept;

        var hasScene = refs.Any(r =>
            string.Equals(r.Role, "scene", StringComparison.OrdinalIgnoreCase));
        var guarded = hasScene
            ? "Photorealistic Vietnamese live-action SCENE still. Full environment + bodies in frame. "
              + "The image marked role=scene is the PREVIOUS SHOT END FRAME. Keep the same place, clothes, lighting, bodies and camera. "
              + "Only apply the new action. Do not reset the scene or invent a new location. "
              + "Other attached images are FACE/WARDROBE identity only — never copy a character bible, master reference, turnaround, or expression grid. "
              + "No extra people, no text, no title card, no watermark.\n"
              + prompt
            : "Photorealistic Vietnamese live-action SCENE still. Full environment + bodies in frame. "
              + "Attached images are FACE/WARDROBE REFERENCES only. Do NOT output a character design sheet, master reference, turnaround, expression grid, contact sheet, passport crop, or title card. "
              + "Output one film frame of the locked Action. "
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

    public async Task<ContentSeriesKfNoteDto> RewriteNoteAsync(
        ContentSeriesKfNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var note = (request.Note ?? "").Trim();
        if (note.Length is < 4 or > 400)
            throw new InvalidOperationException("Mô tả 4–400 ký tự — lời thường, không viết prompt.");

        var raw = await _gemini.GenerateJsonAsync(
            "You rewrite a Vietnamese operator note into a SHORT continuity lock for a still. "
            + "JSON only: {\"instruction\":\"...\",\"place\":bool,\"lighting\":bool,\"wardrobe\":bool,\"camera\":bool,\"inherit\":bool}. "
            + "instruction <= 220 chars, Vietnamese. Keep the locked Action. "
            + "Do not invent location changes, new characters, dialogue, hugs, apologies, or plot. "
            + "inherit=true unless the note says start a new scene.",
            $"Action: {(request.Action ?? "").Trim()}\nLocation: {(request.Location ?? "").Trim()}\nNote: {note}",
            cancellationToken,
            512,
            true);

        var t = (raw ?? "").Trim();
        if (t.StartsWith("```", StringComparison.Ordinal))
        {
            var nl = t.IndexOf('\n');
            t = nl > 0 ? t[(nl + 1)..] : t;
            var end = t.LastIndexOf("```", StringComparison.Ordinal);
            if (end > 0) t = t[..end];
        }
        using var doc = System.Text.Json.JsonDocument.Parse(t);
        var root = doc.RootElement;
        var instruction = root.TryGetProperty("instruction", out var i) ? (i.GetString() ?? "").Trim() : "";
        if (instruction.Length > 280) instruction = instruction[..280];
        if (instruction.Length < 8)
            throw new InvalidOperationException("Gemini không trả lệnh continuity đọc được.");
        return new ContentSeriesKfNoteDto(
            instruction,
            ReadBool(root, "place"),
            ReadBool(root, "lighting"),
            ReadBool(root, "wardrobe"),
            ReadBool(root, "camera"),
            root.TryGetProperty("inherit", out var h) && h.ValueKind == System.Text.Json.JsonValueKind.False ? false : true);
    }

    private static bool ReadBool(System.Text.Json.JsonElement root, string name) =>
        root.TryGetProperty(name, out var p) && p.ValueKind == System.Text.Json.JsonValueKind.True;

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
