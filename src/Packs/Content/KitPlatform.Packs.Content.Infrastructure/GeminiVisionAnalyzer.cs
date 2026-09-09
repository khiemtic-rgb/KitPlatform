using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class GeminiVisionAnalyzer : IImageVisionAnalyzer
{
    private readonly ContentGeminiClient _gemini;
    public GeminiVisionAnalyzer(ContentGeminiClient gemini) => _gemini = gemini;

    public string ProviderId => "GEMINI_VISION";

    public async Task<KitVideoVisionAnalysisResult> AnalyzeAsync(
        KitVideoVisionAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        var images = new List<(string Mime, string Base64, string Label)>
        {
            (request.ImageMime, Convert.ToBase64String(request.Image), "CANDIDATE KEYFRAME — judge only this image against the contract."),
        };
        var i = 1;
        foreach (var r in request.References)
        {
            images.Add((r.Mime, Convert.ToBase64String(r.Bytes), $"REFERENCE {i} {r.Role} {r.Label}. Identity / location only. Do not copy pose."));
            i += 1;
        }
        if (request.PreviousKeyframe is { Length: > 32 })
        {
            var mime = KitVideoArtifactRules.DetectMime(request.PreviousKeyframe) ?? "image/jpeg";
            images.Add((mime, Convert.ToBase64String(request.PreviousKeyframe), "PREVIOUS APPROVED KEYFRAME — continuity reference. Not a pose to copy."));
        }

        var system = """
            You are a pixel Vision QA judge for film keyframes.
            Return ONLY JSON. Do not invent characters that are not visible.
            Compare the candidate image to attached character/location references when present.
            "uncertain" on a P0 requirement must not be treated as pass — use status UNCERTAIN.
            Prop state must be one of: held_by_minh, held_by_mother, on_table, not_visible, not_present.
            Classify imageType FIRST. A character sheet, collage, multi-panel, reference board, storyboard, or logo-heavy layout is NEVER PRODUCTION_STILL.
            """;
        var user = $$"""
            VisualContract JSON:
            {{request.Contract.GetRawText()}}

            Return this exact shape:
            {
              "overall": "PASS|FAIL|UNCERTAIN",
              "characters": { "expected": 2, "detected": 0, "ids": ["CHAR-001"] },
              "requirements": [
                { "id": "CHAR-001", "status": "PASS|FAIL|UNCERTAIN", "reason": "", "confidence": 0.0 }
              ],
              "propState": "held_by_minh|on_table|held_by_mother|not_visible|not_present",
              "location": "LOC-001 or Bedroom or unknown",
              "wardrobe": "WARDROBE-001 or WARDROBE-002 or unknown",
              "actionVisible": false,
              "uncertain": false,
              "imageType": "PRODUCTION_STILL|CHARACTER_SHEET|COLLAGE|MULTI_PANEL|REFERENCE_BOARD|TEXT_HEAVY_IMAGE|INVALID_COMPOSITION"
            }
            Judge pixels. Identity = match reference face, not "a boy exists".
            If the image has multiple portraits, wardrobe rows, labeled panels, a logo, or LOCKED badge: imageType=CHARACTER_SHEET and overall=FAIL.
            """;

        var raw = await _gemini.GenerateJsonWithImagesAsync(system, user, images, cancellationToken);
        try
        {
            return Parse(raw, request.Contract);
        }
        catch (Exception)
        {
            return new KitVideoVisionAnalysisResult(
                "FAIL", 0, 0, [], [], null, null, null, false, true, raw);
        }
    }

    public static KitVideoVisionAnalysisResult Parse(string raw, JsonElement contract)
    {
        using var doc = JsonDocument.Parse(ExtractJson(raw));
        var root = doc.RootElement;
        var expected = 0;
        var detected = 0;
        var ids = new List<string>();
        if (root.TryGetProperty("characters", out var ch) && ch.ValueKind == JsonValueKind.Object)
        {
            expected = ch.TryGetProperty("expected", out var e) && e.TryGetInt32(out var ev) ? ev : 0;
            detected = ch.TryGetProperty("detected", out var d) && d.TryGetInt32(out var dv) ? dv : 0;
            if (ch.TryGetProperty("ids", out var arr) && arr.ValueKind == JsonValueKind.Array)
                ids.AddRange(arr.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s.Length > 0));
        }
        if (expected == 0 && contract.TryGetProperty("characters", out var cc) && cc.ValueKind == JsonValueKind.Array)
            expected = cc.GetArrayLength();

        var reqs = new List<KitVideoVisionRequirementDto>();
        if (root.TryGetProperty("requirements", out var rq) && rq.ValueKind == JsonValueKind.Array)
        {
            foreach (var r in rq.EnumerateArray())
            {
                reqs.Add(new KitVideoVisionRequirementDto(
                    r.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                    r.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "",
                    r.TryGetProperty("reason", out var rs) ? rs.GetString() : null,
                    r.TryGetProperty("confidence", out var cf) && cf.TryGetDouble(out var c) ? c : 0));
            }
        }

        var overall = root.TryGetProperty("overall", out var ov) ? ov.GetString() ?? "FAIL" : "FAIL";
        var uncertain = (root.TryGetProperty("uncertain", out var un) && un.ValueKind == JsonValueKind.True)
            || reqs.Any(r => r.Status.Equals("UNCERTAIN", StringComparison.OrdinalIgnoreCase))
            || overall.Equals("UNCERTAIN", StringComparison.OrdinalIgnoreCase);
        var action = root.TryGetProperty("actionVisible", out var av) && av.ValueKind == JsonValueKind.True;
        return new KitVideoVisionAnalysisResult(
            overall.ToUpperInvariant(),
            expected,
            detected,
            ids,
            reqs,
            root.TryGetProperty("propState", out var ps) ? ps.GetString() : null,
            root.TryGetProperty("location", out var loc) ? loc.GetString() : null,
            root.TryGetProperty("wardrobe", out var wd) ? wd.GetString() : null,
            action,
            uncertain,
            raw,
            KitVideoIntegrityRules.InferImageType(
                root.TryGetProperty("imageType", out var it) ? it.GetString() : null,
                raw));
    }

    private static string ExtractJson(string raw)
    {
        var t = (raw ?? "").Trim();
        var fence = t.IndexOf("```", StringComparison.Ordinal);
        if (fence >= 0)
        {
            var after = t[(fence + 3)..];
            if (after.StartsWith("json", StringComparison.OrdinalIgnoreCase))
                after = after[4..];
            var close = after.LastIndexOf("```", StringComparison.Ordinal);
            if (close > 0) t = after[..close].Trim();
        }
        var start = t.IndexOf('{');
        var end = t.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            var slice = t[start..(end + 1)];
            try
            {
                using var _ = JsonDocument.Parse(slice);
                return slice;
            }
            catch (JsonException)
            {
                for (var i = start; i <= end; i++)
                {
                    if (t[i] != '{') continue;
                    for (var j = end; j > i; j--)
                    {
                        if (t[j] != '}') continue;
                        var cand = t[i..(j + 1)];
                        try
                        {
                            using var doc = JsonDocument.Parse(cand);
                            if (doc.RootElement.ValueKind == JsonValueKind.Object)
                                return cand;
                        }
                        catch (JsonException)
                        {
                            // keep searching
                        }
                    }
                }
            }
        }
        throw new InvalidOperationException("VISION_FAILED: response is not JSON.");
    }
}
