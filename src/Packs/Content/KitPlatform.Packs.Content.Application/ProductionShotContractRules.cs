using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

/// <summary>PRODUCTION_SHOT_CONTRACT_V1 — facts only. No model prompt. No generation.</summary>
public static class ProductionShotContractRules
{
    public const string DocumentId = "PRODUCTION_SHOT_CONTRACT_V1";
    public const string Project = "FAMIXA";
    public const string Version = "V1";

    public static readonly string[] Statuses =
        ["DRAFT", "VALIDATED", "DIRECTOR_APPROVED", "REJECTED", "SUPERSEDED"];

    public static readonly string[] ForbiddenModelKeys =
    [
        "gemini_prompt", "runway_prompt", "model_prompt", "final_prompt",
        "geminiPrompt", "runwayPrompt", "modelPrompt", "finalPrompt",
        "GEMINI_PROMPT", "RUNWAY_PROMPT", "MODEL_PROMPT", "FINAL_PROMPT",
        "gen4_turbo", "gemini_model", "runway_model", "geminiModel", "runwayModel",
    ];

    public static readonly string[] ShotCharacterFields = ["expression", "pose", "gaze", "movement"];

    public static bool AutoFix() => false;
    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool CreatesPixels(string? action) =>
        action is "GENERATE" or "REGENERATE" or "GEMINI" or "RUNWAY";
    public static bool TouchesGolden(string? path) =>
        !string.IsNullOrWhiteSpace(path) && path.Contains("SH01-01", StringComparison.OrdinalIgnoreCase)
        && path.Contains("GOLDEN", StringComparison.OrdinalIgnoreCase);

    public sealed record Issue(string Code, string Attribute, string Message);

    public static bool IsApproved(string? status) =>
        string.Equals(status, "DIRECTOR_APPROVED", StringComparison.OrdinalIgnoreCase);

    public static bool CanEdit(string? status) =>
        status is "DRAFT" or "VALIDATED" or "REJECTED" || string.IsNullOrWhiteSpace(status);

    public static string NextVersion(string? current)
    {
        var v = (current ?? "V1").Trim().ToUpperInvariant();
        var n = Regex.Match(v, @"^V(\d+)$");
        return n.Success && int.TryParse(n.Groups[1].Value, out var i) ? $"V{i + 1}" : "V2";
    }

    public static bool SameTenant(string? shotCharacterId, string? contractCharacterId, string? seriesId, string? projectCode)
    {
        if (string.IsNullOrWhiteSpace(contractCharacterId) || string.IsNullOrWhiteSpace(shotCharacterId))
            return false;
        if (!string.Equals(CharacterIdentityGovernanceRules.NormalizeCharacterId(shotCharacterId),
                CharacterIdentityGovernanceRules.NormalizeCharacterId(contractCharacterId), StringComparison.Ordinal))
            return false;
        var series = (seriesId ?? "").Trim().ToUpperInvariant();
        var project = (projectCode ?? Project).Trim().ToUpperInvariant();
        return series.Length > 0 && series == project;
    }

    public static string CanonicalJson(JsonElement el)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            WriteCanonical(writer, el);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string HashCanonical(JsonElement payload) =>
        KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(CanonicalJson(HashSurface(payload))));

    public static JsonElement HashSurface(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object)
            return JsonSerializer.SerializeToElement(new { });
        var keep = new Dictionary<string, JsonElement>();
        foreach (var name in new[]
                 {
                     "shotId", "seriesId", "characterId", "eraId", "identity", "scene", "story", "character",
                     "wardrobe", "props", "composition", "lighting", "motion", "timing", "continuity",
                     "constraints", "dialogue", "production",
                 })
        {
            if (payload.TryGetProperty(name, out var n))
                keep[name] = n;
        }
        return JsonSerializer.SerializeToElement(keep.ToDictionary(p => p.Key, p => (object)p.Value));
    }

    public static IReadOnlyList<Issue> Validate(JsonElement payload)
    {
        var issues = new List<Issue>();
        if (payload.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new Issue("SHOT_CONTRACT_INVALID", "payload", "Contract payload must be an object."));
            return issues;
        }
        Require(payload, "shotId", issues);
        Require(payload, "characterId", issues);
        Require(payload, "seriesId", issues);
        Require(payload, "eraId", issues);
        if (ReadString(payload, "characterId").Length == 0)
            issues.Add(new Issue("SHOT_CONTRACT_INVALID", "characterId", "missing character"));
        if (!payload.TryGetProperty("identity", out var ident) || ident.ValueKind != JsonValueKind.Object)
            issues.Add(new Issue("SHOT_CONTRACT_INVALID", "identity", "missing identity authority"));
        else
        {
            foreach (var key in new[] { "masterId", "masterSha256", "dnaId", "dnaSha256", "prpId", "prpSha256" })
            {
                if (ReadString(ident, key).Length == 0)
                    issues.Add(new Issue("SHOT_CONTRACT_INVALID", key, "missing identity authority"));
            }
        }
        if (!payload.TryGetProperty("scene", out var scene) || scene.ValueKind != JsonValueKind.Object
            || ReadString(scene, "location").Length == 0)
            issues.Add(new Issue("SHOT_CONTRACT_INVALID", "scene.location", "missing scene"));
        if (!payload.TryGetProperty("story", out var story) || story.ValueKind != JsonValueKind.Object)
            issues.Add(new Issue("SHOT_CONTRACT_INVALID", "story", "missing story beat"));
        else
        {
            if (ReadString(story, "beat").Length == 0)
                issues.Add(new Issue("SHOT_CONTRACT_INVALID", "story.beat", "missing story beat"));
            if (ReadString(story, "action").Length == 0)
                issues.Add(new Issue("SHOT_CONTRACT_INVALID", "story.action", "missing action"));
            if (ContainsCameraInstruction(ReadString(story, "action")))
                issues.Add(new Issue("SHOT_CONTRACT_INVALID", "story.action", "camera fields separate from action"));
        }
        if (!payload.TryGetProperty("composition", out var comp) || comp.ValueKind != JsonValueKind.Object
            || ReadString(comp, "framing").Length == 0
            || ReadString(comp, "cameraAngle").Length == 0
            || ReadString(comp, "cameraDistance").Length == 0)
            issues.Add(new Issue("SHOT_CONTRACT_INVALID", "composition", "missing composition"));
        if (!payload.TryGetProperty("timing", out var timing) || timing.ValueKind != JsonValueKind.Object
            || !TryDuration(timing, out var seconds) || seconds <= 0)
            issues.Add(new Issue("SHOT_CONTRACT_INVALID", "timing.durationSeconds", "invalid duration"));
        if (payload.TryGetProperty("motion", out var motion) && motion.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty("story", out var st) && ContainsCameraInstruction(ReadString(st, "action")))
            issues.Add(new Issue("SHOT_CONTRACT_INVALID", "motion", "motion fields separate from action"));
        if (ContainsForbiddenModelFields(payload))
            issues.Add(new Issue("SHOT_CONTRACT_INVALID", "production", "no provider-specific model fields"));
        if (ContainsPromptCompilerFields(payload))
            issues.Add(new Issue("SHOT_CONTRACT_INVALID", "prompt", "no FINAL_PROMPT"));
        if (payload.TryGetProperty("props", out var props) && props.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in props.EnumerateArray())
            {
                if (p.ValueKind == JsonValueKind.Object && ReadString(p, "state").Length == 0 && ReadString(p, "id").Length > 0)
                    issues.Add(new Issue("SHOT_CONTRACT_INVALID", "props.state", "props have state"));
            }
        }
        return issues;
    }

    public static bool HasSeparatedCamera(JsonElement payload) =>
        payload.TryGetProperty("composition", out var c) && c.ValueKind == JsonValueKind.Object
        && ReadString(c, "framing").Length > 0
        && ReadString(c, "cameraAngle").Length > 0
        && !ContainsCameraInstruction(payload.TryGetProperty("story", out var s) ? ReadString(s, "action") : "");

    public static bool HasSeparatedMotion(JsonElement payload) =>
        payload.TryGetProperty("motion", out var m) && m.ValueKind == JsonValueKind.Object
        && (ReadString(m, "subjectMotion").Length > 0 || ReadString(m, "cameraMotion").Length > 0)
        && !ContainsCameraInstruction(payload.TryGetProperty("story", out var s) ? ReadString(s, "action") : "");

    public static bool PropsHaveState(JsonElement payload)
    {
        if (!payload.TryGetProperty("props", out var props) || props.ValueKind != JsonValueKind.Array)
            return false;
        return props.EnumerateArray().Any(p => p.ValueKind == JsonValueKind.Object && ReadString(p, "state").Length > 0);
    }

    public static bool ContinuityHasPrevious(JsonElement payload) =>
        payload.TryGetProperty("continuity", out var c) && c.ValueKind == JsonValueKind.Object
        && (c.TryGetProperty("previousShotId", out _) || c.TryGetProperty("rules", out _));

    public static bool ConstraintsSeparated(JsonElement payload) =>
        payload.TryGetProperty("constraints", out var c) && c.ValueKind == JsonValueKind.Object
        && c.TryGetProperty("mandatory", out _) && c.TryGetProperty("allowed", out _) && c.TryGetProperty("forbidden", out _);

    public static bool ContainsForbiddenModelFields(JsonElement el) => WalkNames(el).Any(n =>
        ForbiddenModelKeys.Contains(n, StringComparer.OrdinalIgnoreCase));

    public static bool ContainsPromptCompilerFields(JsonElement el)
    {
        var blob = el.GetRawText();
        return blob.Contains("FINAL_PROMPT", StringComparison.OrdinalIgnoreCase)
            || blob.Contains("MODEL_PROMPT", StringComparison.OrdinalIgnoreCase)
            || blob.Contains("GEMINI_PROMPT", StringComparison.OrdinalIgnoreCase)
            || blob.Contains("RUNWAY_PROMPT", StringComparison.OrdinalIgnoreCase);
    }

    public static bool ContainsCameraInstruction(string action)
    {
        if (string.IsNullOrWhiteSpace(action)) return false;
        return Regex.IsMatch(action, @"\b(camera|zoom|dolly|tilt|pan|push-in|push in|rack focus)\b", RegexOptions.IgnoreCase)
            && Regex.IsMatch(action, @"\b(slowly|zooms|dolly|tilt|pan|push)\b", RegexOptions.IgnoreCase);
    }

    public static JsonElement ToGovernanceShotSpec(JsonElement payload)
    {
        var identity = new Dictionary<string, object?>();
        foreach (var (attr, value) in CollectRequestedIdentity(payload))
            identity[attr] = value;
        return JsonSerializer.SerializeToElement(new { identity, requestedState = CollectRequestedIdentity(payload).Select(x => new { attribute = x.Attr, requested = x.Value }) });
    }

    public static string ToGovernancePrompt(JsonElement payload)
    {
        var story = payload.TryGetProperty("story", out var s) ? ReadString(s, "action") : "";
        var wardrobe = payload.TryGetProperty("wardrobe", out var w) ? ReadString(w, "description") : "";
        var character = payload.TryGetProperty("character", out var c) ? c.GetRawText() : "";
        return $"{story} {wardrobe} {character}";
    }

    public static IReadOnlyList<(string Attr, string Value)> CollectRequestedIdentity(JsonElement payload)
    {
        var list = new List<(string, string)>();
        if (payload.TryGetProperty("character", out var ch) && ch.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in ch.EnumerateObject())
            {
                if (ShotCharacterFields.Contains(p.Name, StringComparer.OrdinalIgnoreCase)) continue;
                var v = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() ?? "" : p.Value.GetRawText();
                if (!string.IsNullOrWhiteSpace(v))
                    list.Add((p.Name, v));
            }
        }
        if (payload.TryGetProperty("requestedState", out var rs) && rs.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in rs.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var attr = ReadString(item, "attribute");
                var val = ReadString(item, "requested");
                if (attr.Length > 0 && val.Length > 0)
                    list.Add((attr, val));
            }
        }
        return list;
    }

    public static JsonElement BuildValidFixture(
        string characterId, string seriesId, string eraId,
        Guid masterId, string masterSha, Guid dnaId, string dnaSha, Guid prpId, string prpSha,
        string shotId = "SHOT-099")
    {
        return JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["shotId"] = shotId,
            ["seriesId"] = seriesId,
            ["characterId"] = characterId,
            ["eraId"] = eraId,
            ["identity"] = new Dictionary<string, object?>
            {
                ["masterId"] = masterId.ToString(),
                ["masterSha256"] = masterSha,
                ["dnaId"] = dnaId.ToString(),
                ["dnaSha256"] = dnaSha,
                ["prpId"] = prpId.ToString(),
                ["prpSha256"] = prpSha,
            },
            ["scene"] = new { location = "living_room", time = "afternoon", environment = "family living room, study context" },
            ["story"] = new { beat = "read_result", action = "Looks at the test paper in hand.", objective = "read the score" },
            ["character"] = new { expression = "focused", pose = "sitting", gaze = "toward test paper", movement = "lifting paper" },
            ["wardrobe"] = new { description = "school clothes within PRP", continuity = "same as previous" },
            ["props"] = new object[] { new { id = "test-paper", name = "test paper", state = "held_by_character", continuityRequired = true } },
            ["composition"] = new
            {
                framing = "medium shot",
                cameraAngle = "eye level",
                cameraDistance = "medium",
                cameraPosition = "front-side",
                subjectPosition = "center-left",
                spatialRelationship = "seated at table",
            },
            ["lighting"] = new { description = "soft afternoon interior" },
            ["motion"] = new { intent = "calm natural movement", cameraMotion = "subtle push-in", subjectMotion = "raises the test paper slightly" },
            ["timing"] = new { durationSeconds = 5 },
            ["continuity"] = new { previousShotId = (string?)null, rules = new[] { "same wardrobe", "same location", "test paper remains in character's hand" } },
            ["constraints"] = new
            {
                mandatory = new[] { "character visible", "test paper visible" },
                allowed = new[] { "subtle hand movement", "natural eye movement" },
                forbidden = new[] { "additional person", "wardrobe change", "identity change" },
            },
            ["dialogue"] = new { enabled = false, text = (string?)null },
            ["production"] = new { aspectRatio = "16:9", resolution = "1280x720", frameRate = "24" },
        });
    }

    public static JsonElement With(JsonElement payload, string path, object? value)
    {
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payload.GetRawText())
            ?? new Dictionary<string, JsonElement>();
        var parts = path.Split('.');
        if (parts.Length == 1)
        {
            dict[parts[0]] = JsonSerializer.SerializeToElement(value);
            return JsonSerializer.SerializeToElement(dict);
        }
        var root = parts[0];
        var inner = dict.TryGetValue(root, out var existing) && existing.ValueKind == JsonValueKind.Object
            ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(existing.GetRawText()) ?? new()
            : new Dictionary<string, JsonElement>();
        inner[parts[1]] = JsonSerializer.SerializeToElement(value);
        dict[root] = JsonSerializer.SerializeToElement(inner);
        return JsonSerializer.SerializeToElement(dict);
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var p in el.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(p.Name);
                    WriteCanonical(writer, p.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in el.EnumerateArray())
                    WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            default:
                el.WriteTo(writer);
                break;
        }
    }

    private static IEnumerable<string> WalkNames(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in el.EnumerateObject())
            {
                yield return p.Name;
                foreach (var n in WalkNames(p.Value))
                    yield return n;
            }
        }
        else if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var i in el.EnumerateArray())
            {
                foreach (var n in WalkNames(i))
                    yield return n;
            }
        }
    }

    private static void Require(JsonElement obj, string name, List<Issue> issues)
    {
        if (ReadString(obj, name).Length == 0)
            issues.Add(new Issue("SHOT_CONTRACT_INVALID", name, $"missing {name}"));
    }

    private static string ReadString(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var n))
            return "";
        return n.ValueKind == JsonValueKind.String ? (n.GetString() ?? "").Trim() : n.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? "" : n.GetRawText().Trim('"');
    }

    private static bool TryDuration(JsonElement timing, out double seconds)
    {
        seconds = 0;
        if (!timing.TryGetProperty("durationSeconds", out var n)) return false;
        if (n.ValueKind == JsonValueKind.Number && n.TryGetDouble(out seconds)) return true;
        return n.ValueKind == JsonValueKind.String && double.TryParse(n.GetString(), out seconds);
    }
}
