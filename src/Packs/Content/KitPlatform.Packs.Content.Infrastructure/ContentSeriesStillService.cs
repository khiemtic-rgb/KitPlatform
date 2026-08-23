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
        if (prompt.Length is < 12 or > 8000)
            throw new InvalidOperationException("Prompt KF cảnh 12–8000 ký tự.");

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
            ? "HARD BAN: no readable letters, numbers, captions, subtitles, logos, watermarks, UI, or typography. "
              + "If a reference has text, ignore the letters — do not copy them. KIT may overlay narrative marks later.\n"
              + "Photorealistic Vietnamese live-action SCENE still. Frame ONLY what FRAMING LOCK / SHOT INTENT require — "
              + "do not force a full-body wide of every named person. Secondary may be partial.\n"
              + "The image marked role=scene is the PREVIOUS SHOT END FRAME. Keep the same place, clothes, lighting and key props. "
              + "Only apply the new action. Soft continuity (pose, gaze, camera distance) may change. "
              + "Other attached images are FACE/WARDROBE identity only — never copy a character bible, master reference, turnaround, or expression grid. "
              + "No extra people, no title card.\n"
              + prompt
            : "HARD BAN: no readable letters, numbers, captions, subtitles, logos, watermarks, UI, or typography. "
              + "If a reference has text, ignore the letters — do not copy them. KIT may overlay narrative marks later.\n"
              + "Photorealistic Vietnamese live-action SCENE still. Frame ONLY what FRAMING LOCK / SHOT INTENT require — "
              + "do not force a full-body wide of every named person.\n"
              + "Attached images are FACE/WARDROBE REFERENCES only. Do NOT output a character design sheet, master reference, turnaround, expression grid, contact sheet, passport crop, or title card. "
              + "Output one film frame of the locked Action and composition. "
              + "Match each named person's face, hair, age and clothes from the matching reference. "
              + "No extra people, no illustration.\n"
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

    public async Task<ContentSeriesStillQaDto> QaAsync(
        ContentSeriesStillQaRequest request,
        CancellationToken cancellationToken = default)
    {
        var url = (request.ImageDataUrl ?? "").Trim();
        var spec = (request.SpecJson ?? "").Trim();
        if (url.Length < 32 || spec.Length < 8)
            throw new InvalidOperationException("Cần ảnh KF + Visual Spec để QA.");
        if (!TryParseDataUrl(url, out var mime, out var b64))
            throw new InvalidOperationException("Ảnh QA không phải data URL.");

        var raw = await _gemini.GenerateJsonWithImageAsync(
            "You are a film still QA judge. Script/spec is source of truth. Do not invent plot, hugs, apologies, or extra people. "
            + "Score the attached still against the Visual Spec. JSON only: "
            + "{\"total\":0-100,\"axes\":{\"character\":0-100,\"face\":0-100,\"action\":0-100,\"prop\":0-100,\"composition\":0-100,\"continuity\":0-100,\"emotion\":0-100},"
            + "\"hardFails\":[\"MISSING_FACE\"|\"MISSING_PROP\"|\"WRONG_COUNT\"|\"WRONG_LOCATION\"|\"WRONG_WARDROBE\"|\"WRONG_ACTION\"],"
            + "\"notes\":\"short Vietnamese\"}. "
            + "HARD FAIL (never average away): missing required primary face, missing required prop, wrong people count, wrong place, wrong wardrobe, wrong action. "
            + "FACE_REQUIRED: reject cropped/back-turned/unreadable primary face. Secondary may be partial if spec says so. "
            + "Do not fail because a KIT overlay number looks graphic.",
            $"VISUAL SPEC:\n{spec}",
            mime,
            b64,
            cancellationToken,
            768);

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
        var fails = new List<string>();
        if (root.TryGetProperty("hardFails", out var hf) && hf.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var x in hf.EnumerateArray())
            {
                var s = (x.GetString() ?? "").Trim();
                if (s.Length > 0) fails.Add(s);
            }
        }
        var axes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("axes", out var ax) && ax.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var p in ax.EnumerateObject())
            {
                if (p.Value.ValueKind == System.Text.Json.JsonValueKind.Number && p.Value.TryGetInt32(out var n))
                    axes[p.Name] = n;
            }
        }
        int? total = root.TryGetProperty("total", out var tot) && tot.TryGetInt32(out var tv) ? tv : null;
        var notes = root.TryGetProperty("notes", out var nt) ? (nt.GetString() ?? "").Trim() : "";
        var status = fails.Count > 0 || (total is < 80) ? "REJECT" : "PASS";
        return new ContentSeriesStillQaDto(status, total, axes.Count == 0 ? null : axes, fails, notes.Length == 0 ? null : notes);
    }

    private static bool ReadBool(System.Text.Json.JsonElement root, string name) =>
        root.TryGetProperty(name, out var p) && p.ValueKind == System.Text.Json.JsonValueKind.True;

    private static string NormalizeAspect(string? raw)
    {
        var s = (raw ?? "").Trim();
        if (s is "9:16") return "9:16";
        return "16:9";
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
