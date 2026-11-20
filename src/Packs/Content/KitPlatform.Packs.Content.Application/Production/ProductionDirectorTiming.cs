using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Explicit DIRECTOR production duration. Uses PerformanceSnapshot + Timing V3.
/// Not provider / preview / TTS duration.
/// </summary>
public static class ProductionDirectorTiming
{
    public const string Source = "DIRECTOR";
    public const string DecisionType = "EXPLICIT_PRODUCTION_DECISION";
    public const string Stage = "VIDEO";
    public const decimal Ep01Sh01DurationSec = 5.0m;

    public static bool IsExplicitDecision(string? extraJson)
    {
        var parsed = TryParse(extraJson);
        return parsed is not null && parsed.DurationSec > 0
            && string.Equals(parsed.Source, Source, StringComparison.Ordinal)
            && string.Equals(parsed.DecisionType, DecisionType, StringComparison.Ordinal);
    }

    public static bool HasDirectorTiming(PerformanceSourceRef? source) =>
        source is { ProductionDurationSec: > 0 }
        && string.Equals(source.TimingSource, Source, StringComparison.Ordinal)
        && string.Equals(source.DecisionType, DecisionType, StringComparison.Ordinal);

    public static DirectorTimingDecision? TryParse(string? extraJson)
    {
        if (string.IsNullOrWhiteSpace(extraJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(extraJson);
            var root = doc.RootElement;
            if (!TryDec(root, "durationSec", out var duration) || duration <= 0) return null;
            var source = Str(root, "source");
            var decision = Str(root, "decisionType");
            var stage = Str(root, "stage");
            if (!string.Equals(source, Source, StringComparison.Ordinal)) return null;
            if (!string.Equals(decision, DecisionType, StringComparison.Ordinal)) return null;
            if (stage.Length > 0 && !string.Equals(stage, Stage, StringComparison.Ordinal)) return null;
            decimal? speechStart = null;
            decimal? speechEnd = null;
            if (TrySpeech(root, "speechStartSec", out var start) && TrySpeech(root, "speechEndSec", out var end) && end > start)
            {
                speechStart = start;
                speechEnd = end;
            }
            return new DirectorTimingDecision(
                duration, source, decision, stage.Length == 0 ? Stage : stage, speechStart, speechEnd);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string ExtraJson(
        decimal durationSec,
        string selectedBy,
        decimal? speechStartSec = null,
        decimal? speechEndSec = null)
    {
        var payload = new Dictionary<string, object>
        {
            ["durationSec"] = durationSec,
            ["source"] = Source,
            ["decisionType"] = DecisionType,
            ["stage"] = Stage,
            ["selectedBy"] = (selectedBy ?? "").Trim(),
        };
        if (TryApprovedSpeech(speechStartSec, speechEndSec, out var start, out var end))
        {
            payload["speechStartSec"] = start;
            payload["speechEndSec"] = end;
        }
        return JsonSerializer.Serialize(payload);
    }

    /// <summary>
    /// Both ends must be present and ordered. A picture duration or a single end is not an interval.
    /// </summary>
    public static bool TryApprovedSpeech(decimal? start, decimal? end, out decimal speechStart, out decimal speechEnd)
    {
        speechStart = 0;
        speechEnd = 0;
        if (start is null || end is null) return false;
        if (start < 0 || end <= start) return false;
        speechStart = start.Value;
        speechEnd = end.Value;
        return true;
    }

    public static DirectorSpeechInterval? ApprovedSpeech(DirectorTimingDecision? decision)
    {
        if (decision is null || !string.Equals(decision.Source, Source, StringComparison.Ordinal)) return null;
        if (!TryApprovedSpeech(decision.SpeechStartSec, decision.SpeechEndSec, out var start, out var end)) return null;
        return new DirectorSpeechInterval(start, end);
    }

    public static DirectorSpeechInterval? ApprovedSpeech(PerformanceSourceRef? source)
    {
        if (!HasDirectorTiming(source)) return null;
        if (!TryApprovedSpeech(source!.SpeechStartSec, source.SpeechEndSec, out var start, out var end)) return null;
        return new DirectorSpeechInterval(start, end);
    }

    public static string TimingHash(Guid buildId, string shotCode, decimal durationSec) =>
        "TIMING_HASH_V1:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|',
            buildId.ToString("D"),
            (shotCode ?? "").Trim(),
            Stage,
            durationSec.ToString("0.0", CultureInfo.InvariantCulture),
            Source,
            DecisionType)))).ToLowerInvariant();

    public static string TimingHash(
        Guid buildId, string shotCode, decimal durationSec, decimal speechStartSec, decimal speechEndSec)
    {
        if (!TryApprovedSpeech(speechStartSec, speechEndSec, out var start, out var end))
            return TimingHash(buildId, shotCode, durationSec);
        return "TIMING_HASH_V1:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|',
            buildId.ToString("D"),
            (shotCode ?? "").Trim(),
            Stage,
            durationSec.ToString("0.0", CultureInfo.InvariantCulture),
            Source,
            DecisionType,
            start.ToString("0.###", CultureInfo.InvariantCulture),
            end.ToString("0.###", CultureInfo.InvariantCulture))))).ToLowerInvariant();
    }

    static string Str(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? (v.GetString() ?? "").Trim()
            : "";

    static bool TrySpeech(JsonElement obj, string name, out decimal value)
    {
        value = 0;
        if (!obj.TryGetProperty(name, out var v)) return false;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out value)) return value >= 0;
        return v.ValueKind == JsonValueKind.String
            && decimal.TryParse(v.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out value)
            && value >= 0;
    }

    static bool TryDec(JsonElement obj, string name, out decimal value)
    {
        value = 0;
        if (!obj.TryGetProperty(name, out var v)) return false;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out value)) return value > 0;
        return v.ValueKind == JsonValueKind.String
            && decimal.TryParse(v.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out value)
            && value > 0;
    }
}

public readonly record struct DirectorSpeechInterval(decimal StartSec, decimal EndSec);

public sealed record DirectorTimingDecision(
    decimal DurationSec,
    string Source,
    string DecisionType,
    string Stage,
    decimal? SpeechStartSec = null,
    decimal? SpeechEndSec = null);
