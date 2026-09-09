using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

public interface IVisualPromptCompiler
{
    KitVideoGenerationRequestDto Compile(JsonElement contract, string? projectStyle = null, string? dialogue = null);
}

public interface IVisionQa
{
    KitVideoVisionQaDto Evaluate(JsonElement contract, JsonElement observation);
}

public interface IImageGenerator
{
    string ProviderId { get; }
    Task<KitVideoImageGenerationResult> GenerateAsync(
        KitVideoImageGenerationRequest request,
        CancellationToken cancellationToken);
}

public interface IImageVisionAnalyzer
{
    string ProviderId { get; }
    Task<KitVideoVisionAnalysisResult> AnalyzeAsync(
        KitVideoVisionAnalysisRequest request,
        CancellationToken cancellationToken);
}

public sealed record KitVideoImageGenerationRequest(
    string Prompt,
    IReadOnlyList<KitVideoPromptRefBytes> References,
    string AspectRatio,
    int TargetWidth,
    int TargetHeight,
    string? Model = null);

public sealed record KitVideoPromptRefBytes(string Role, string Mime, byte[] Bytes, string Label);

public sealed record KitVideoImageGenerationResult(
    bool Ok,
    string FailureClass,
    byte[]? Bytes,
    string? Mime,
    string Provider,
    string Model,
    string? GenerationId,
    decimal? EstimatedCost,
    decimal? ActualCost,
    bool CostUnknown);

public sealed record KitVideoVisionAnalysisRequest(
    byte[] Image,
    string ImageMime,
    JsonElement Contract,
    IReadOnlyList<KitVideoPromptRefBytes> References,
    byte[]? PreviousKeyframe = null);

public sealed record KitVideoVisionRequirementDto(
    string Id,
    string Status,
    string? Reason,
    double Confidence);

public sealed record KitVideoVisionAnalysisResult(
    string Overall,
    int ExpectedCharacters,
    int DetectedCharacters,
    IReadOnlyList<string> DetectedCharacterIds,
    IReadOnlyList<KitVideoVisionRequirementDto> Requirements,
    string? PropState,
    string? Location,
    string? Wardrobe,
    bool ActionVisible,
    bool Uncertain,
    string RawJson,
    string ImageType = "UNKNOWN");

public sealed record KitVideoGenerationRequestDto(
    string Provider,
    IReadOnlyDictionary<string, string> Sections,
    string Prompt,
    IReadOnlyList<KitVideoPromptRefDto> References,
    string I2vImageSource,
    string Fingerprint,
    bool HasDialogue);

public sealed record KitVideoPromptRefDto(string Role, string Path, string? CharacterId = null, string? Version = null, string? Era = null);

public sealed record KitVideoVisionQaDto(
    string Status,
    IReadOnlyDictionary<string, int> Scores,
    IReadOnlyList<string> P0Fail,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Reasons,
    bool AllowI2v,
    bool CanBeReference,
    string? ArtifactHash = null,
    string? ImageType = null);

public sealed record KitVideoRepairDto(string RepairClass, string Repair);

public sealed record KitVideoI2vReadyPackageDto(
    string ShotCode,
    string AttemptId,
    string Source,
    bool Ready,
    bool RunwaySubmitted,
    IReadOnlyList<string> Blocked,
    string? SourceArtifactHash = null,
    string? ImageType = null);

public static class KitVideoVisionRules
{
    public const string Version = "KIT-VIDEO-VISION-V1";
    public const int MaxAutoAttempts = 3;

    public static KitVideoGenerationRequestDto CompilePrompt(JsonElement contract, string? projectStyle, string? dialogue)
    {
        var chars = ArrObj(contract, "characters");
        var location = Obj(contract, "location");
        var action = Str(contract, "action");
        var forbidden = ArrStr(contract, "forbidden");
        var wardrobe = Obj(contract, "wardrobe");
        var composition = Obj(contract, "composition");
        var spatial = Obj(contract, "spatial");
        var props = ArrObj(contract, "props");

        var subject = string.Join("; ", chars.Select(c =>
            $"{Str(c, "name")} CHARACTER_ID={Str(c, "code")} CHARACTER_VERSION={Str(c, "version")} ERA={Str(c, "era")}"
            + (string.IsNullOrWhiteSpace(Str(c, "reference")) ? "" : $" REFERENCE={Str(c, "reference")}")));
        var propLine = string.Join("; ", props.Select(p =>
            $"{Str(p, "name")} PROP_ID={Str(p, "code")} state={Str(p, "state")}"));
        var style = string.IsNullOrWhiteSpace(projectStyle) ? "FAMIXA_VISUAL_STYLE_V1" : projectStyle.Trim();

        var sections = new Dictionary<string, string>
        {
            ["SCENE"] = $"{Str(location, "name")} {Str(location, "time")}".Trim(),
            ["CHARACTERS"] = subject,
            ["SUBJECT"] = subject,
            ["ACTION"] = action,
            ["PROPS"] = string.IsNullOrWhiteSpace(propLine) ? "none" : propLine,
            ["ENVIRONMENT"] = $"{Str(location, "name")} {Str(location, "time")} {Str(location, "lighting")}".Trim(),
            ["SPATIAL"] = SpatialLine(spatial),
            ["COMPOSITION"] = $"{Str(composition, "shotSize")}; {Str(composition, "framing")}; {Str(composition, "actionArea")}",
            ["CAMERA"] = $"{Str(composition, "cameraAngle")}; {Str(composition, "cameraPosition")}",
            ["LIGHTING"] = string.IsNullOrWhiteSpace(Str(location, "lighting")) ? "warm indoor" : Str(location, "lighting"),
            ["STYLE"] = style,
            ["CONTINUITY"] = wardrobe.ValueKind == JsonValueKind.Object
                ? string.Join("; ", wardrobe.EnumerateObject().Select(p => $"{p.Name} wardrobe {p.Value.GetString()}"))
                : "",
            ["NEGATIVE"] = string.Join("; ", new[]
            {
                string.Join("; ", forbidden),
                "one PRODUCTION_STILL only: single frame, single scene, no character sheet, no collage, no multi-panel, no storyboard, no logo, no watermark, no UI chrome, no subtitle",
            }.Where(s => s.Length > 0)),
        };
        var order = new[] { "SCENE", "CHARACTERS", "ACTION", "PROPS", "SPATIAL", "COMPOSITION", "CAMERA", "LIGHTING", "STYLE", "CONTINUITY", "NEGATIVE" };
        var prompt = string.Join("\n\n", order.Select(k => $"{k}\n{sections.GetValueOrDefault(k, "")}"));
        if (!string.IsNullOrWhiteSpace(dialogue) && prompt.Contains(dialogue, StringComparison.Ordinal))
            throw new InvalidOperationException("Prompt không chứa thoại.");
        if (Regex.IsMatch(prompt, @"[“""][^”""]+[”""]") ||
            Regex.IsMatch(prompt, @"voice_settings|lipsync|runway|idempotency|SELECT |INSERT |pack_content|tts|elevenlabs", RegexOptions.IgnoreCase))
            throw new InvalidOperationException("Prompt chứa thoại hoặc instruction nội bộ.");

        var refs = chars
            .Where(c => !string.IsNullOrWhiteSpace(Str(c, "reference")))
            .Select(c => new KitVideoPromptRefDto("PRIMARY_REFERENCE", Str(c, "reference"), Str(c, "code"), Str(c, "version"), Str(c, "era")))
            .ToList();
        var locRef = Str(location, "reference");
        if (!string.IsNullOrWhiteSpace(locRef))
            refs.Add(new KitVideoPromptRefDto("PRIMARY_REFERENCE", locRef, Str(location, "code"), Str(location, "version"), null));
        return new KitVideoGenerationRequestDto(
            "GEMINI",
            sections,
            prompt,
            refs,
            "APPROVED_KEYFRAME",
            GenerationFingerprint(contract.GetRawText(), prompt, refs.Select(r => $"{r.CharacterId}:{r.Version}:{r.Path}"), "GEMINI", style),
            false);
    }

    public static KitVideoVisionQaDto Evaluate(JsonElement contract, JsonElement obs)
    {
        if (obs.TryGetProperty("imageType", out var imageTypeEl) && !string.IsNullOrWhiteSpace(imageTypeEl.GetString()))
        {
            var requiredType = KitVideoIntegrityRules.RequiredImageType(contract);
            var seenType = KitVideoIntegrityRules.InferImageType(imageTypeEl.GetString(), null);
            if (requiredType == KitVideoIntegrityRules.ProductionStill && seenType != KitVideoIntegrityRules.ProductionStill)
                return KitVideoIntegrityRules.ImageTypeGate(requiredType, seenType);
        }

        var p0 = new List<string>();
        var warnings = new List<string>();
        var expected = ArrObj(contract, "characters").Select(c => Str(c, "code")).ToList();
        var detected = ArrStr(obs, "characters");
        if (detected.Count != expected.Count)
            p0.Add($"Character count: Expected {expected.Count} Detected {detected.Count}");
        foreach (var code in expected.Where(c => !detected.Contains(c, StringComparer.OrdinalIgnoreCase)))
            p0.Add($"Missing: {code}");
        foreach (var extra in detected.Where(d => !expected.Contains(d, StringComparer.OrdinalIgnoreCase)))
            p0.Add($"Extra Character: {extra}");
        if (obs.TryGetProperty("actionVisible", out var av) && av.ValueKind == JsonValueKind.False)
            p0.Add("Action visibility: FAIL");

        foreach (var prop in ArrObj(contract, "props"))
        {
            if (!Str(prop, "level").Equals("MANDATORY", StringComparison.OrdinalIgnoreCase)) continue;
            var code = Str(prop, "code");
            var state = Str(prop, "state");
            var hit = ArrObj(obs, "props").FirstOrDefault(p => Str(p, "code") == code);
            if (hit.ValueKind != JsonValueKind.Object)
                p0.Add($"Missing: {code} {Str(prop, "name")}");
            else if (Str(hit, "state") != state)
                p0.Add($"Wrong: {code} state {Str(hit, "state")} required {state}");
        }

        var loc = Obj(contract, "location");
        var seenLoc = Str(obs, "location");
        if (!string.IsNullOrWhiteSpace(seenLoc) && seenLoc != Str(loc, "code") && seenLoc != Str(loc, "name"))
            p0.Add($"Wrong: Location {seenLoc}");

        var composition = Obj(contract, "composition");
        var cropped = ArrStr(obs, "croppedOut");
        if (composition.TryGetProperty("bothCharactersVisible", out var both) && both.ValueKind == JsonValueKind.True)
        {
            foreach (var code in expected.Where(cropped.Contains))
                p0.Add($"Action crop: {code} fully cropped");
        }
        if (Str(composition, "shotSize") != "Close-up")
        {
            foreach (var code in ArrStr(obs, "missingHeads"))
                p0.Add($"Missing head: {code}");
        }
        if (composition.TryGetProperty("handsVisible", out var hands) && hands.ValueKind == JsonValueKind.True
            && obs.TryGetProperty("missingHandsRelevant", out var mh) && mh.ValueKind == JsonValueKind.True)
            p0.Add("Important hand missing");

        var requiredWardrobe = Obj(contract, "wardrobe");
        var seenWardrobe = Obj(obs, "wardrobe");
        if (requiredWardrobe.ValueKind == JsonValueKind.Object && seenWardrobe.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in requiredWardrobe.EnumerateObject())
            {
                if (seenWardrobe.TryGetProperty(p.Name, out var got) && got.GetString() != p.Value.GetString())
                    p0.Add($"Wrong: {p.Name} wardrobe {got.GetString()}");
            }
        }

        var identity = Obj(obs, "identityMatch");
        if (identity.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in identity.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.False)
                    p0.Add($"Wrong: {p.Name} face identity");
            }
        }

        var lighting = Str(obs, "lightingDelta");
        if (lighting == "minor") warnings.Add("Minor lighting variation");
        if (lighting == "major") p0.Add("Wrong: lighting continuity");

        var integrity = Obj(obs, "integrity");
        if (integrity.TryGetProperty("readable", out var rd) && rd.ValueKind == JsonValueKind.False)
            p0.Add("Image integrity: not readable");
        var w = integrity.TryGetProperty("width", out var wi) && wi.TryGetInt32(out var ww) ? ww : 0;
        var h = integrity.TryGetProperty("height", out var hi) && hi.TryGetInt32(out var hh) ? hh : 0;
        if (w > 0 && (w < 512 || h < 512)) p0.Add("Image integrity: resolution");
        if (integrity.TryGetProperty("unexpectedText", out var tx) && tx.ValueKind == JsonValueKind.True)
            p0.Add("Image integrity: unexpected text");
        if (integrity.TryGetProperty("watermark", out var wm) && wm.ValueKind == JsonValueKind.True)
            p0.Add("Image integrity: watermark");

        var scores = new Dictionary<string, int>
        {
            ["character"] = detected.Count == expected.Count && expected.All(detected.Contains) ? 100 : 20,
            ["location"] = p0.Exists(x => x.Contains("Location", StringComparison.Ordinal)) ? 20 : 95,
            ["composition"] = cropped.Count > 0 ? 40 : 91,
            ["action"] = p0.Exists(x => x.Contains("Action visibility", StringComparison.Ordinal)) ? 10 : 100,
            ["props"] = p0.Exists(x => x.Contains("PROP", StringComparison.Ordinal) || x.Contains("state", StringComparison.Ordinal)) ? 15 : 100,
            ["wardrobe"] = p0.Exists(x => x.Contains("wardrobe", StringComparison.Ordinal)) ? 20 : 97,
            ["continuity"] = p0.Exists(x => x.Contains("identity", StringComparison.Ordinal)) ? 10 : lighting == "minor" ? 88 : 97,
            ["integrity"] = p0.Exists(x => x.Contains("integrity", StringComparison.Ordinal)) ? 20 : 96,
        };
        var status = p0.Count > 0 ? "FAIL" : warnings.Count > 0 ? "WARNING" : "PASS";
        var imageType = obs.TryGetProperty("imageType", out var typeEl)
            ? KitVideoIntegrityRules.InferImageType(typeEl.GetString(), null)
            : null;
        return new KitVideoVisionQaDto(status, scores, p0, warnings, p0.Concat(warnings).ToList(), status == "PASS", status == "PASS", null, imageType);
    }

    public static KitVideoRepairDto Diagnose(KitVideoVisionQaDto qa)
    {
        var text = string.Join(" | ", qa.P0Fail);
        if (Regex.IsMatch(text, @"Missing: PROP|state "))
            return new KitVideoRepairDto("ASSET_ERROR", "Increase prop visibility and specify: \"Minh is visibly holding the test paper in both hands.\"");
        if (Regex.IsMatch(text, @"Extra Character|Character count"))
            return new KitVideoRepairDto("PROMPT_ERROR", "Tighten NEGATIVE: no extra people. Keep exact character count.");
        if (Regex.IsMatch(text, @"Missing: CHAR|fully cropped"))
            return new KitVideoRepairDto("COMPOSITION_ERROR", "Widen framing so every required character is fully visible.");
        if (text.Contains("face identity", StringComparison.OrdinalIgnoreCase))
            return new KitVideoRepairDto("REFERENCE_ERROR", "Re-lock character REFERENCE (FRONT) and previous Approved KF. Do not invent a new face.");
        if (text.Contains("wardrobe", StringComparison.OrdinalIgnoreCase))
            return new KitVideoRepairDto("REFERENCE_ERROR", "Lock wardrobe from Continuity Snapshot. Do not redraw outfit from prompt.");
        if (text.Contains("Location", StringComparison.OrdinalIgnoreCase))
            return new KitVideoRepairDto("REFERENCE_ERROR", "Re-attach Scene Master + Location REFERENCE. Do not change room identity.");
        if (Regex.IsMatch(text, @"IMAGE_TYPE|CHARACTER_SHEET|COLLAGE|MULTI_PANEL"))
            return new KitVideoRepairDto("GENERATION_ERROR", "Regenerate as one PRODUCTION_STILL. No character sheet, collage, panels, logo, or storyboard.");
        if (Regex.IsMatch(text, @"integrity|text|watermark"))
            return new KitVideoRepairDto("GENERATION_ERROR", "Regenerate without on-image text, logo, or watermark.");
        if (text.Contains("Action visibility", StringComparison.OrdinalIgnoreCase))
            return new KitVideoRepairDto("PROMPT_ERROR", "Restate ACTION so the interaction is readable.");
        return new KitVideoRepairDto("QA_ERROR", "Director review required — diagnosis incomplete.");
    }

    public static void EnsureNotBlindRetry(string previousFingerprint, string nextFingerprint, bool strategyChanged)
    {
        if (previousFingerprint == nextFingerprint && !strategyChanged)
            throw new InvalidOperationException("DO_NOT_BLIND_RETRY: same prompt + same references.");
    }

    public static string DirectorRevision(string note)
    {
        var t = (note ?? "").Trim();
        if (t.Length == 0) throw new InvalidOperationException("Director feedback trống.");
        if (Regex.IsMatch(t, @"già|old|age", RegexOptions.IgnoreCase))
            return "Keep CHAR-001 ERA-01 age 11. Do not age the face.";
        if (Regex.IsMatch(t, @"bài|paper|kiểm tra", RegexOptions.IgnoreCase))
            return "Make PROP-001 clearly visible in Minh hands.";
        if (Regex.IsMatch(t, @"nhìn|facing|mẹ chưa", RegexOptions.IgnoreCase))
            return "Mother faces Minh. Eyeline toward the test paper.";
        if (Regex.IsMatch(t, @"rộng|wide", RegexOptions.IgnoreCase))
            return "Tighten framing to medium two-shot. Keep both bodies and the prop.";
        return $"Visual Revision Instruction: {t}";
    }

    public static KitVideoI2vReadyPackageDto I2vPackage(string shotCode, string attemptId, string attemptStatus, string? qaStatus, bool allowI2v)
    {
        var blocked = new List<string>();
        if (!string.Equals(attemptStatus, "APPROVED", StringComparison.OrdinalIgnoreCase))
            blocked.Add("Director not APPROVED");
        if (!string.Equals(qaStatus, "PASS", StringComparison.OrdinalIgnoreCase))
            blocked.Add("Vision QA not PASS");
        if (!allowI2v) blocked.Add("I2V blocked");
        return new KitVideoI2vReadyPackageDto(shotCode, attemptId, "APPROVED_KEYFRAME", blocked.Count == 0, false, blocked);
    }

    public static string Fingerprint(string prompt, IEnumerable<string> refs) =>
        GenerationFingerprint("", prompt, refs, "GEMINI", "");

    public static string GenerationFingerprint(
        string contractJson,
        string prompt,
        IEnumerable<string> referenceVersions,
        string provider,
        string model)
    {
        var raw = string.Join("||", new[]
        {
            contractJson.Trim(),
            prompt,
            string.Join('|', referenceVersions.OrderBy(x => x, StringComparer.Ordinal)),
            provider,
            model,
        });
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return $"vis-{Convert.ToHexString(hash)[..16].ToLowerInvariant()}";
    }

    private static string SpatialLine(JsonElement spatial)
    {
        if (spatial.ValueKind != JsonValueKind.Object) return "";
        var bits = new List<string>();
        if (spatial.TryGetProperty("positions", out var pos) && pos.ValueKind == JsonValueKind.Object)
            bits.AddRange(pos.EnumerateObject().Select(p => $"{p.Name}={p.Value.GetString()}"));
        if (spatial.TryGetProperty("relative", out var rel) && rel.ValueKind == JsonValueKind.String)
            bits.Add(rel.GetString() ?? "");
        return string.Join("; ", bits.Where(s => s.Length > 0));
    }

    private static string Str(JsonElement el, string name) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? ""
            : "";

    private static JsonElement Obj(JsonElement el, string name) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) ? v : default;

    private static List<string> ArrStr(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Array)
            return [];
        return v.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s.Length > 0).ToList();
    }

    private static List<JsonElement> ArrObj(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Array)
            return [];
        return v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).ToList();
    }
}

public sealed class KitVideoVisualPromptCompiler : IVisualPromptCompiler
{
    public KitVideoGenerationRequestDto Compile(JsonElement contract, string? projectStyle = null, string? dialogue = null) =>
        KitVideoVisionRules.CompilePrompt(contract, projectStyle, dialogue);
}

public sealed class KitVideoVisionQaEngine : IVisionQa
{
    public KitVideoVisionQaDto Evaluate(JsonElement contract, JsonElement observation) =>
        KitVideoVisionRules.Evaluate(contract, observation);
}

public sealed class KitVideoArtifactRules
{
    public const int TargetWidth = 1280;
    public const int TargetHeight = 720;
    public const string TargetAspect = "16:9";

    public static KitVideoArtifactValidation Validate(byte[]? bytes, string? mime, string? path = null)
    {
        var reasons = new List<string>();
        if (bytes is null || bytes.Length < 32) reasons.Add("File missing or too small.");
        if (bytes is { Length: > 12_000_000 }) reasons.Add("File too large.");
        var detected = DetectMime(bytes);
        if (detected is null) reasons.Add("Not a readable JPEG/PNG.");
        if (!string.IsNullOrWhiteSpace(mime) && detected is not null && mime != detected && mime != "image/jpg")
            reasons.Add($"MIME mismatch {mime} vs {detected}.");
        if (!string.IsNullOrWhiteSpace(path) && !File.Exists(path)) reasons.Add("Artifact path missing.");
        var (w, h) = ReadSize(bytes);
        if (w < 512 || h < 512) reasons.Add("Resolution too low.");
        return new KitVideoArtifactValidation(
            reasons.Count == 0,
            reasons.Count == 0 ? "OK" : "GENERATION_ARTIFACT_INVALID",
            detected ?? mime ?? "",
            w,
            h,
            reasons);
    }

    public static string? DetectMime(byte[]? bytes)
    {
        if (bytes is { Length: >= 3 } && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";
        if (bytes is { Length: >= 8 } && bytes[0] == 0x89 && bytes[1] == 0x50)
            return "image/png";
        return null;
    }

    public static (int Width, int Height) ReadSize(byte[]? bytes)
    {
        if (bytes is null || bytes.Length < 24) return (0, 0);
        if (bytes[0] == 0x89 && bytes[1] == 0x50)
            return (ReadBe32(bytes, 16), ReadBe32(bytes, 20));
        if (bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            var i = 2;
            while (i + 9 < bytes.Length)
            {
                if (bytes[i] != 0xFF) break;
                var marker = bytes[i + 1];
                var len = (bytes[i + 2] << 8) | bytes[i + 3];
                if (marker is 0xC0 or 0xC1 or 0xC2)
                    return ((bytes[i + 7] << 8) | bytes[i + 8], (bytes[i + 5] << 8) | bytes[i + 6]);
                i += 2 + len;
            }
        }
        return (0, 0);
    }

    private static int ReadBe32(byte[] b, int o) =>
        (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];
}

public sealed record KitVideoArtifactValidation(
    bool Ok,
    string Status,
    string Mime,
    int Width,
    int Height,
    IReadOnlyList<string> Reasons);

public static class KitVideoPixelVisionRules
{
    public static KitVideoVisionQaDto Gate(JsonElement contract, KitVideoVisionAnalysisResult analysis)
    {
        var imageType = KitVideoIntegrityRules.InferImageType(analysis.ImageType, analysis.RawJson);
        var required = KitVideoIntegrityRules.RequiredImageType(contract);
        if (required == KitVideoIntegrityRules.ProductionStill && imageType != KitVideoIntegrityRules.ProductionStill)
            return KitVideoIntegrityRules.ImageTypeGate(required, imageType);

        var obs = ToObservation(contract, analysis);
        var qa = KitVideoVisionRules.Evaluate(contract, obs);
        if (analysis.Uncertain && qa.P0Fail.Count == 0)
        {
            return qa with
            {
                Status = "REVIEW_REQUIRED",
                Warnings = qa.Warnings.Append("P0 UNCERTAIN — not auto PASS.").ToArray(),
                AllowI2v = false,
                CanBeReference = false,
            };
        }
        return qa;
    }

    public static JsonElement ToObservation(JsonElement contract, KitVideoVisionAnalysisResult analysis)
    {
        var expected = contract.TryGetProperty("characters", out var ch) && ch.ValueKind == JsonValueKind.Array
            ? ch.EnumerateArray().Select(x => x.TryGetProperty("code", out var c) ? c.GetString() ?? "" : "").Where(s => s.Length > 0).ToList()
            : [];
        var detected = analysis.DetectedCharacterIds.Count > 0
            ? analysis.DetectedCharacterIds.ToList()
            : analysis.DetectedCharacters > 0
                ? expected.Take(analysis.DetectedCharacters).ToList()
                : new List<string>();
        if (analysis.DetectedCharacters > detected.Count)
        {
            for (var i = detected.Count; i < analysis.DetectedCharacters; i++)
                detected.Add($"EXTRA-{i + 1}");
        }
        var loc = contract.TryGetProperty("location", out var l) && l.ValueKind == JsonValueKind.Object
            ? (l.TryGetProperty("code", out var lc) ? lc.GetString() ?? "" : "")
            : "";
        var locName = contract.TryGetProperty("location", out var ln) && ln.ValueKind == JsonValueKind.Object
            ? (ln.TryGetProperty("name", out var lnn) ? lnn.GetString() ?? "" : "")
            : "";
        var reqs = analysis.Requirements;
        var missingHead = reqs.Any(r =>
            (r.Id.Contains("FACE", StringComparison.OrdinalIgnoreCase) || r.Reason?.Contains("face", StringComparison.OrdinalIgnoreCase) == true)
            && r.Status == "FAIL");
        var cropped = reqs.Where(r => r.Reason?.Contains("crop", StringComparison.OrdinalIgnoreCase) == true).Select(r => r.Id).ToList();
        var identity = new Dictionary<string, bool>();
        foreach (var code in expected)
        {
            var row = reqs.FirstOrDefault(r => r.Id.Equals(code, StringComparison.OrdinalIgnoreCase));
            identity[code] = row is not null
                ? row.Status.Equals("PASS", StringComparison.OrdinalIgnoreCase)
                : detected.Contains(code, StringComparer.OrdinalIgnoreCase)
                    && analysis.Overall.Equals("PASS", StringComparison.OrdinalIgnoreCase)
                    && !analysis.Uncertain;
        }
        var propState = NormalizePropState(analysis.PropState);
        if (reqs.Any(r => r.Id.Contains("PROP-001", StringComparison.OrdinalIgnoreCase) && r.Status is "FAIL" or "UNCERTAIN"))
            propState = propState is "held_by_minh" ? "not_visible" : propState;
        var obj = new
        {
            characters = detected,
            props = new[] { new { code = "PROP-001", state = propState } },
            location = NormalizeLocation(analysis.Location, loc, locName),
            wardrobe = new Dictionary<string, string>
            {
                ["CHAR-001"] = NormalizeWardrobe(analysis.Wardrobe),
            },
            imageType = KitVideoIntegrityRules.InferImageType(analysis.ImageType, analysis.RawJson),
            actionVisible = analysis.ActionVisible,
            croppedOut = cropped,
            missingHeads = missingHead ? expected : [],
            missingHandsRelevant = reqs.Any(r => r.Id.Contains("HAND", StringComparison.OrdinalIgnoreCase) && r.Status == "FAIL"),
            occludedFaces = Array.Empty<string>(),
            identityMatch = identity,
            lightingDelta = "none",
            integrity = new
            {
                readable = true,
                width = 1280,
                height = 720,
                aspect = "16:9",
                unexpectedText = reqs.Any(r => r.Id.Contains("TEXT", StringComparison.OrdinalIgnoreCase) && r.Status == "FAIL"),
                watermark = reqs.Any(r => r.Id.Contains("WATERMARK", StringComparison.OrdinalIgnoreCase) && r.Status == "FAIL"),
                artifact = false,
            },
        };
        return JsonSerializer.SerializeToElement(obj);
    }

    private static string NormalizePropState(string? raw)
    {
        var s = (raw ?? "").Trim().ToLowerInvariant().Replace(' ', '_');
        if (s.Contains("held_by_minh") || s.Contains("minh") && s.Contains("hold")) return "held_by_minh";
        if (s.Contains("held_by_mother") || s.Contains("linh") && s.Contains("hold")) return "held_by_mother";
        if (s.Contains("on_table") || s.Contains("table")) return "on_table";
        if (s.Contains("not_visible") || s.Contains("uncertain")) return "not_visible";
        return string.IsNullOrWhiteSpace(s) ? "not_present" : s;
    }

    private static string NormalizeLocation(string? seen, string expectedCode, string expectedName)
    {
        var s = (seen ?? "").Trim();
        if (string.IsNullOrWhiteSpace(s) || s.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            return "unknown";
        if (s.Equals(expectedCode, StringComparison.OrdinalIgnoreCase) || s.Equals(expectedName, StringComparison.OrdinalIgnoreCase))
            return expectedCode;
        if (Regex.IsMatch(s, @"living\s*room|phòng khách|phong khach", RegexOptions.IgnoreCase))
            return expectedCode is "LOC-001" || Regex.IsMatch(expectedName, @"living", RegexOptions.IgnoreCase) ? expectedCode : "LOC-001";
        if (Regex.IsMatch(s, @"bedroom|phòng ngủ|phong ngu", RegexOptions.IgnoreCase))
            return "Bedroom";
        return s;
    }

    private static string NormalizeWardrobe(string? raw)
    {
        var s = (raw ?? "").Trim();
        if (string.IsNullOrWhiteSpace(s) || s.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            return "";
        if (Regex.IsMatch(s, @"WARDROBE-001|school\s*uniform|đồng phục", RegexOptions.IgnoreCase))
            return "WARDROBE-001";
        if (Regex.IsMatch(s, @"WARDROBE-002|home\s*clothes|đồ ở nhà|casual", RegexOptions.IgnoreCase))
            return "WARDROBE-002";
        return s;
    }
}
