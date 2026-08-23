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

        var guarded = prompt;

        var labeled = new List<(string Mime, string Base64, string Label)>(parsed.Count);
        for (var i = 0; i < parsed.Count; i++)
        {
            var row = refs[i];
            var n = i + 1;
            var role = (row.Role ?? "").Trim();
            var label = role.Equals("scene", StringComparison.OrdinalIgnoreCase)
                ? $"REFERENCE {n} — Scene Master ({row.Name}). Exact environment. Do not redesign the room."
                : role.Equals("continuity", StringComparison.OrdinalIgnoreCase)
                    ? $"REFERENCE {n} — Previous KF ({row.Name}). Keep faces, wardrobe, room, lighting, props. Do not copy crop, zoom, or pose."
                    : role.Equals("identity-secondary", StringComparison.OrdinalIgnoreCase)
                        ? $"REFERENCE {n} — {row.Name} Canon. Identity only. May be a shoulder / partial face."
                        : $"REFERENCE {n} — {row.Name} Canon. Face, hair, age, wardrobe identity only. Not a pose to copy.";
            labeled.Add((parsed[i].Mime, parsed[i].Base64, label));
        }

        var (bytes, model) = await _gemini.GenerateImageWithRefsAsync(
            guarded,
            labeled,
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
            + "JSON only: {\"hardChecks\":{\"character\":\"PASS|FAIL\",\"face\":\"PASS|FAIL\",\"people\":\"PASS|FAIL\","
            + "\"location\":\"PASS|FAIL\",\"prop\":\"PASS|FAIL|PARTIAL\",\"action\":\"PASS|FAIL\",\"gaze\":\"PASS|FAIL\",\"continuity\":\"PASS|FAIL\"},"
            + "\"hardFails\":[\"MISSING_FACE\"|\"WRONG_CHARACTER\"|\"WRONG_COUNT\"|\"WRONG_LOCATION\"|\"MISSING_PROP\"|\"WRONG_ACTION\"|\"WRONG_WARDROBE\"|\"WRONG_GAZE\"],"
            + "\"total\":0-100,\"axes\":{\"character\":0-100,\"face\":0-100,\"action\":0-100,\"prop\":0-100,\"composition\":0-100,\"continuity\":0-100,\"emotion\":0-100},"
            + "\"evidence\":\"one Vietnamese sentence naming the miss\",\"confidence\":0-100,"
            + "\"notes\":\"short Vietnamese\"}. "
            + "HARD CHECKS decide FAIL. Quality total is SOFT polish only — never write a hardFail because composition is not 70%, lens is not 50mm, emotion is mild, or lighting is a bit warm. "
            + "A still that performs the Visual Contract (right primary, readable primary face, gaze toward the other person not the lens, right place, visible required prop) "
            + "MUST have empty hardFails and hardChecks PASS or PARTIAL. That image PASSES even if total is 70-80. "
            + "Do not score 45-70 and invent hardFails for an ordinary photoreal family still. "
            + "Do not FAIL without a specific hardFail code AND evidence. No evidence = empty hardFails. "
            + "PARTIAL prop (paper present but not fully readable) is PASS, not MISSING_PROP. "
            + "FACE: only PRIMARY face. Secondary shoulder/partial is PASS, not MISSING_FACE. "
            + "WRONG_COUNT: only an EXTRA person. One full face + a mother shoulder is PASS. "
            + "GAZE: looking at the other person or a prop is PASS. Looking into the lens is WRONG_GAZE. "
            + "Never fail because the subject is not looking at camera. "
            + "Do not fail because a KIT overlay number looks graphic.",
            $"VISUAL SPEC:\n{spec}",
            mime,
            b64,
            cancellationToken,
            1024);

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
        var evidence = root.TryGetProperty("evidence", out var ev) ? (ev.GetString() ?? "").Trim() : "";
        int? confidence = root.TryGetProperty("confidence", out var cf) && cf.TryGetInt32(out var cv) ? cv : null;
        var hardChecks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("hardChecks", out var hc) && hc.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var p in hc.EnumerateObject())
            {
                var v = p.Value.ValueKind == System.Text.Json.JsonValueKind.String ? (p.Value.GetString() ?? "").Trim() : "";
                if (v.Length > 0) hardChecks[p.Name] = v;
            }
        }
        var status = fails.Count > 0 ? "REJECT" : "PASS";
        return new ContentSeriesStillQaDto(
            status,
            total,
            axes.Count == 0 ? null : axes,
            fails,
            notes.Length == 0 ? null : notes,
            hardChecks.Count == 0 ? null : hardChecks,
            evidence.Length == 0 ? null : evidence,
            confidence);
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
