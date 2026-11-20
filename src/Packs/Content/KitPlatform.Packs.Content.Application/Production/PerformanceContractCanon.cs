using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content.Production;

public static class PerformanceContractCanon
{
    static readonly Regex UrlLike = new(@"https?://|blob:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    static readonly HashSet<string> ForbiddenKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "voiceId", "voice_id", "provider", "providerId", "provider_id", "model", "modelId", "model_id",
        "runway", "veo", "gemini", "elevenlabs", "elevenLabs", "fal", "wan",
        "providerDurationSec", "provider_duration_sec", "providerDuration",
        "previewUrl", "acceptedTake", "motionUrl",
    };

    static readonly HashSet<string> DurationKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "providerDurationSec", "provider_duration_sec", "providerDuration",
    };

    static readonly HashSet<string> VoiceKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "voiceId", "voice_id",
    };

    public static string CanonicalJson(PerformanceDraft draft)
    {
        var sb = new StringBuilder();
        WriteObject(sb, [
            ("documentId", draft.DocumentId),
            ("schemaVersion", draft.SchemaVersion),
            ("buildId", NormalizeGuid(draft.BuildId)),
            ("shotCode", draft.ShotCode),
            ("episodeCode", draft.EpisodeCode),
            ("sceneCode", draft.SceneCode),
            ("shotPerformance", draft.ShotPerformance),
            ("characters", draft.Characters),
            ("lines", draft.Lines),
            ("timingRef", draft.TimingRef),
            ("visualRef", draft.VisualRef),
            ("storyRef", draft.StoryRef),
        ]);
        return sb.ToString();
    }

    public static string PerformanceHashOf(PerformanceDraft draft) =>
        PerformanceContractIds.HashPrefix + Sha256Hex(Encoding.UTF8.GetBytes(CanonicalJson(draft)));

    public static string LineTextHashOf(string text) =>
        PerformanceContractIds.LineTextPrefix + Sha256Hex(Encoding.UTF8.GetBytes(text ?? ""));

    public static string DraftFingerprintOf(PerformanceDraft draft) =>
        PerformanceContractIds.DraftPrefix + Sha256Hex(Encoding.UTF8.GetBytes(CanonicalJson(draft)));

    public static string Sha256Hex(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public static string NormalizeGuid(Guid id) => id.ToString("D").ToLowerInvariant();

    public static bool IsValidPerformanceHash(string? hash)
    {
        var h = (hash ?? "").Trim();
        if (!h.StartsWith(PerformanceContractIds.HashPrefix, StringComparison.Ordinal)) return false;
        var hex = h[PerformanceContractIds.HashPrefix.Length..];
        return hex.Length == 64 && hex.All(Uri.IsHexDigit);
    }

    public static IReadOnlyList<PerformanceIssue> RejectLegacyHash(string? hash)
    {
        if (IsValidPerformanceHash(hash)) return [];
        return [new PerformanceIssue(PerformanceContractIds.InvalidPerformanceHash, "performanceHash",
            "Legacy ActingHash / fingerprint cannot be PerformanceHash.")];
    }

    public static IReadOnlyList<PerformanceIssue> ScanForbidden(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            var issues = new List<PerformanceIssue>();
            ScanElement(doc.RootElement, "$", issues);
            return issues;
        }
        catch (JsonException)
        {
            return [new PerformanceIssue(PerformanceContractIds.UnresolvedProjection, "$", "Payload is not JSON.")];
        }
    }

    static void ScanElement(JsonElement el, string path, List<PerformanceIssue> issues)
    {
        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in el.EnumerateObject())
            {
                var child = path + "." + p.Name;
                if (VoiceKeys.Contains(p.Name))
                    issues.Add(new PerformanceIssue(PerformanceContractIds.VoiceIdForbidden, child, "VoiceId is HOW, not Performance WHAT."));
                else if (DurationKeys.Contains(p.Name))
                    issues.Add(new PerformanceIssue(PerformanceContractIds.ProviderDurationForbidden, child, "Provider duration is HOW / Timing V3."));
                else if (ForbiddenKeys.Contains(p.Name))
                    issues.Add(new PerformanceIssue(PerformanceContractIds.ProviderFieldForbidden, child, "Provider / model field is not allowed."));
                ScanElement(p.Value, child, issues);
            }
        }
        else if (el.ValueKind == JsonValueKind.Array)
        {
            var i = 0;
            foreach (var item in el.EnumerateArray())
                ScanElement(item, path + "[" + i++ + "]", issues);
        }
        else if (el.ValueKind == JsonValueKind.String)
        {
            var s = el.GetString() ?? "";
            if (UrlLike.IsMatch(s))
                issues.Add(new PerformanceIssue(PerformanceContractIds.ProviderFieldForbidden, path, "URL is not Performance WHAT."));
        }
    }

    static void WriteObject(StringBuilder sb, IReadOnlyList<(string Key, object? Value)> fields)
    {
        sb.Append('{');
        var first = true;
        foreach (var (key, value) in fields)
        {
            if (!TryWriteValue(sb, key, value, first, out first)) continue;
        }
        sb.Append('}');
    }

    static bool TryWriteValue(StringBuilder sb, string key, object? value, bool first, out bool nextFirst)
    {
        nextFirst = first;
        if (value is null) return false;
        if (value is string s && string.IsNullOrWhiteSpace(s)) return false;
        if (value is ShotPerformanceSlice slice)
        {
            var inner = new StringBuilder();
            WriteObject(inner, [
                ("before", slice.Before),
                ("during", slice.During),
                ("after", slice.After),
                ("gazeTarget", slice.GazeTarget),
            ]);
            if (inner.ToString() == "{}") return false;
            Comma(sb, ref nextFirst);
            WriteKey(sb, key);
            sb.Append(inner);
            return true;
        }
        if (value is ShotPerformanceBefore before)
        {
            var inner = new StringBuilder();
            WriteObject(inner, [
                ("action", before.Action),
                ("body", before.Body),
                ("prop", before.Prop),
                ("gaze", before.Gaze),
                ("room", before.Room),
            ]);
            if (inner.ToString() == "{}") return false;
            Comma(sb, ref nextFirst);
            WriteKey(sb, key);
            sb.Append(inner);
            return true;
        }
        if (value is ShotPerformanceDuring during)
        {
            var inner = new StringBuilder();
            WriteObject(inner, [
                ("emotion", during.Emotion),
                ("intensity", during.Intensity),
            ]);
            if (inner.ToString() == "{}") return false;
            Comma(sb, ref nextFirst);
            WriteKey(sb, key);
            sb.Append(inner);
            return true;
        }
        if (value is ShotPerformanceAfter after)
        {
            var inner = new StringBuilder();
            WriteObject(inner, [
                ("action", after.Action),
                ("gaze", after.Gaze),
            ]);
            if (inner.ToString() == "{}") return false;
            Comma(sb, ref nextFirst);
            WriteKey(sb, key);
            sb.Append(inner);
            return true;
        }
        if (value is PerformanceTimingRef timing)
        {
            Comma(sb, ref nextFirst);
            WriteKey(sb, key);
            WriteObject(sb, [
                ("timingDocumentId", timing.TimingDocumentId),
                ("timingHash", timing.TimingHash),
            ]);
            return true;
        }
        if (value is PerformanceVisualRef visual)
        {
            Comma(sb, ref nextFirst);
            WriteKey(sb, key);
            WriteObject(sb, [
                ("visualAuthorityId", visual.VisualAuthorityId),
                ("visualContractHash", visual.VisualContractHash),
                ("characterIdentityIds", visual.CharacterIdentityIds),
            ]);
            return true;
        }
        if (value is PerformanceStoryRef story)
        {
            Comma(sb, ref nextFirst);
            WriteKey(sb, key);
            WriteObject(sb, [
                ("storyTextHash", story.StoryTextHash),
                ("beatId", story.BeatId),
                ("dialogueSegmentIds", story.DialogueSegmentIds),
            ]);
            return true;
        }
        if (value is IReadOnlyList<CharacterInShot> chars)
        {
            Comma(sb, ref nextFirst);
            WriteKey(sb, key);
            sb.Append('[');
            for (var i = 0; i < chars.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var c = chars[i];
                WriteObject(sb, [
                    ("characterId", c.CharacterId),
                    ("participation", c.Participation),
                    ("visualRole", c.VisualRole),
                    ("speechRole", c.SpeechRole),
                    ("emotionalState", c.EmotionalState),
                    ("intensity", c.Intensity),
                    ("lineIds", c.LineIds ?? []),
                ]);
            }
            sb.Append(']');
            return true;
        }
        if (value is IReadOnlyList<PerformanceLine> lines)
        {
            Comma(sb, ref nextFirst);
            WriteKey(sb, key);
            sb.Append('[');
            for (var i = 0; i < lines.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var line = lines[i];
                WriteObject(sb, [
                    ("lineId", line.LineId),
                    ("characterId", line.CharacterId),
                    ("textIdentity", line.TextIdentity),
                    ("delivery", line.Delivery),
                ]);
            }
            sb.Append(']');
            return true;
        }
        if (value is PerformanceTextIdentity text)
        {
            Comma(sb, ref nextFirst);
            WriteKey(sb, key);
            WriteObject(sb, [
                ("text", text.Text),
                ("textHash", text.TextHash),
            ]);
            return true;
        }
        if (value is PerformanceDelivery delivery)
        {
            Comma(sb, ref nextFirst);
            WriteKey(sb, key);
            WriteObject(sb, [
                ("emotion", delivery.Emotion),
                ("intensity", delivery.Intensity),
                ("pace", delivery.Pace),
                ("volume", delivery.Volume),
                ("pauseSec", delivery.PauseSec),
                ("label", delivery.Label),
                ("emphasis", delivery.Emphasis),
                ("locked", delivery.Locked),
            ]);
            return true;
        }
        if (value is IReadOnlyList<string> strings)
        {
            Comma(sb, ref nextFirst);
            WriteKey(sb, key);
            sb.Append('[');
            for (var i = 0; i < strings.Count; i++)
            {
                if (i > 0) sb.Append(',');
                WriteString(sb, strings[i] ?? "");
            }
            sb.Append(']');
            return true;
        }
        if (value is int or long or byte or short)
        {
            Comma(sb, ref nextFirst);
            WriteKey(sb, key);
            sb.Append(Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
            return true;
        }
        if (value is int?)
        {
            var n = (int?)value;
            if (n is null) return false;
            Comma(sb, ref nextFirst);
            WriteKey(sb, key);
            sb.Append(n.Value.ToString(CultureInfo.InvariantCulture));
            return true;
        }
        if (value is double or float or decimal)
        {
            Comma(sb, ref nextFirst);
            WriteKey(sb, key);
            WriteNumber(sb, Convert.ToDouble(value, CultureInfo.InvariantCulture));
            return true;
        }
        if (value is bool b)
        {
            Comma(sb, ref nextFirst);
            WriteKey(sb, key);
            sb.Append(b ? "true" : "false");
            return true;
        }
        if (value is string textValue)
        {
            Comma(sb, ref nextFirst);
            WriteKey(sb, key);
            WriteString(sb, textValue);
            return true;
        }
        return false;
    }

    static void Comma(StringBuilder sb, ref bool first)
    {
        if (!first) sb.Append(',');
        first = false;
    }

    static void WriteKey(StringBuilder sb, string key)
    {
        WriteString(sb, key);
        sb.Append(':');
    }

    static void WriteNumber(StringBuilder sb, double n)
    {
        if (Math.Abs(n - Math.Round(n)) < 1e-12)
        {
            sb.Append(((long)Math.Round(n)).ToString(CultureInfo.InvariantCulture));
            return;
        }
        sb.Append(n.ToString("0.##########", CultureInfo.InvariantCulture));
    }

    static void WriteString(StringBuilder sb, string value)
    {
        sb.Append('"');
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (ch < ' ')
                        sb.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        sb.Append(ch);
                    break;
            }
        }
        sb.Append('"');
    }
}
