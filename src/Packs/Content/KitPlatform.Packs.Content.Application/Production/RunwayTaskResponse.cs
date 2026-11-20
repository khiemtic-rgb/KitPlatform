using System.Text.Json;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Reads a Runway task body. The internal task label stays PROVIDER_UNAVAILABLE.
/// The provider failureCode stays a separate field. Output URLs and secrets are not kept.
/// </summary>
public static class RunwayTaskResponse
{
    public const string InternalFailureLabel = ProductionFailureReasons.ProviderUnavailable;

    static readonly string[] SecretNames =
    [
        "authorization", "token", "secret", "password", "credential", "apikey", "api_key", "bearer", "signature",
    ];

    public static RunwayTaskRead Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new RunwayTaskRead("UNKNOWN", null, null, null, "{}");
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return new RunwayTaskRead("UNKNOWN", null, null, null, "{}");

            var status = MapStatus(StringOf(root, "status"));
            var code = StringOf(root, "failureCode");
            var text = Redact(First(FailureText(root), StringOf(root, "error")));
            var diagnostic = Diagnostic(root, status, code, text);
            return new RunwayTaskRead(status, code, text, Format(code, text), diagnostic);
        }
        catch (JsonException)
        {
            return new RunwayTaskRead("UNKNOWN", null, null, null, "{}");
        }
    }

    public static string? Format(string? providerCode, string? failureText)
    {
        var code = (providerCode ?? "").Trim();
        var text = (failureText ?? "").Trim();
        if (code.Length == 0) return text.Length == 0 ? null : text;
        if (text.Length == 0) return code;
        return code + ": " + text;
    }

    static string MapStatus(string? raw)
    {
        var status = (raw ?? "").Trim();
        if (status.Length == 0) return "UNKNOWN";
        return status.ToUpperInvariant() switch
        {
            "SUCCESS" => "SUCCEEDED",
            "COMPLETE" or "COMPLETED" => "SUCCEEDED",
            var other => other,
        };
    }

    static string? FailureText(JsonElement root)
    {
        if (!root.TryGetProperty("failure", out var fail))
            return null;
        if (fail.ValueKind == JsonValueKind.String)
            return fail.GetString();
        if (fail.ValueKind != JsonValueKind.Object)
            return null;
        return First(StringOf(fail, "message"), StringOf(fail, "detail"), StringOf(fail, "reason"));
    }

    static string Diagnostic(JsonElement root, string status, string? code, string? text)
    {
        var failure = root.TryGetProperty("failure", out var fail) && fail.ValueKind == JsonValueKind.Object
            ? (object)ObjectWithoutSecrets(fail)
            : text;
        var body = new Dictionary<string, object?>
        {
            ["id"] = StringOf(root, "id"),
            ["status"] = status,
            ["failureCode"] = code,
            ["failure"] = failure,
        };
        return JsonSerializer.Serialize(body);
    }

    static Dictionary<string, object?> ObjectWithoutSecrets(JsonElement obj)
    {
        var copy = new Dictionary<string, object?>();
        foreach (var prop in obj.EnumerateObject())
        {
            if (IsDropped(prop.Name))
                continue;
            var kept = KeptValue(prop.Value);
            if (kept is null)
                continue;
            copy[prop.Name] = kept;
        }
        return copy;
    }

    static object? KeptValue(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
            return Redact(value.GetString());
        if (value.ValueKind == JsonValueKind.Object)
        {
            var nested = ObjectWithoutSecrets(value);
            return nested.Count == 0 ? null : nested;
        }
        var raw = value.ToString();
        return ContainsSensitive(raw) ? null : raw;
    }

    static bool IsDropped(string name)
    {
        var key = name.Replace("_", "", StringComparison.Ordinal).ToLowerInvariant();
        if (key is "output" or "outputs" or "artifacts" or "url" or "uri" or "promptimage" or "image")
            return true;
        foreach (var secret in SecretNames)
        {
            if (key.Contains(secret, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    static string? Redact(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || ContainsSensitive(raw) && IsOnlySensitive(raw))
            return ContainsSensitive(raw) ? null : raw;
        if (!ContainsSensitive(raw))
            return raw.Trim();
        var s = Strip(Strip(Strip(raw, "https://"), "http://"), "data:");
        s = StripBearer(s).Trim();
        return s.Length == 0 ? null : s;
    }

    static bool IsOnlySensitive(string raw) => StripBearer(Strip(Strip(Strip(raw, "https://"), "http://"), "data:")).Trim().Length == 0;

    static bool ContainsSensitive(string? raw) =>
        !string.IsNullOrEmpty(raw)
        && (raw.Contains("https://", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("http://", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("data:", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("Bearer ", StringComparison.OrdinalIgnoreCase));

    static string Strip(string raw, string marker)
    {
        var s = raw;
        while (true)
        {
            var i = s.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return s;
            var end = i + marker.Length;
            while (end < s.Length && !char.IsWhiteSpace(s[end]) && s[end] is not '"' and not ',')
                end++;
            s = s.Remove(i, end - i);
        }
    }

    static string StripBearer(string raw)
    {
        var s = raw;
        while (true)
        {
            var i = s.IndexOf("Bearer ", StringComparison.OrdinalIgnoreCase);
            if (i < 0) return s;
            var end = i + "Bearer ".Length;
            while (end < s.Length && !char.IsWhiteSpace(s[end]))
                end++;
            s = s.Remove(i, end - i);
        }
    }

    static string? StringOf(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    static string? First(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }
        return null;
    }
}

public sealed record RunwayTaskRead(
    string Status,
    string? ProviderFailureCode,
    string? FailureText,
    string? Reason,
    string DiagnosticJson);
