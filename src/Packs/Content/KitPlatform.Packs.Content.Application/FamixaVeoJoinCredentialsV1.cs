using System.Text;

namespace KitPlatform.Packs.Content;

/// <summary>
/// Veo Join file is a separate credential from Gemini API keys.
/// Presence/shape only — does not call Google.
/// </summary>
public static class FamixaVeoJoinCredentialsV1
{
    public const int MaxUtf8Bytes = 64 * 1024;

    public sealed record Gate(bool Ok, string? Code, string? Message, string? NormalizedText, string? FileName);

    public static Gate Accept(string? raw, string? fileName)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new Gate(false, "VEO_JOIN_FILE_MISSING", "Chưa chọn file Join Veo.", null, null);

        var text = raw.Trim();
        if (Encoding.UTF8.GetByteCount(text) > MaxUtf8Bytes)
            return new Gate(false, "VEO_JOIN_FILE_TOO_LARGE", "File Join vượt 64KB.", null, null);

        if (IsGeminiApiKeyLookalike(text))
            return new Gate(false, "GEMINI_API_KEY_REJECTED", "File Join Veo không phải Gemini API key.", null, null);

        var name = SanitizeFileName(fileName);
        return new Gate(true, null, null, text, name);
    }

    public static bool IsGeminiApiKeyLookalike(string? raw)
    {
        var t = raw?.Trim() ?? "";
        if (t.Length < 20) return false;
        if (t.StartsWith("AIza", StringComparison.Ordinal) || t.StartsWith("AQ.", StringComparison.Ordinal))
            return true;
        if (t.Contains('{', StringComparison.Ordinal) || t.Contains('\n'))
            return false;
        if (t.Length is < 30 or > 90) return false;
        foreach (var c in t)
        {
            if (char.IsLetterOrDigit(c) || c is '.' or '_' or '-') continue;
            return false;
        }
        return true;
    }

    public static string? SanitizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        var name = fileName.Replace('\\', '/').Split('/').Last().Trim();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    /// <summary>Vertex service account fields only. Never log <see cref="Account.PrivateKey"/>.</summary>
    public sealed record Account(string ProjectId, string ClientEmail, string PrivateKey, string TokenUri)
    {
        public override string ToString() => $"{ClientEmail} / {ProjectId}";
    }

    public static Account? TryParseAccount(string? raw)
    {
        var gate = Accept(raw, "join.json");
        if (!gate.Ok || string.IsNullOrWhiteSpace(gate.NormalizedText)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(gate.NormalizedText);
            var root = doc.RootElement;
            var type = root.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
            if (!string.Equals(type, "service_account", StringComparison.OrdinalIgnoreCase))
                return null;
            var project = root.TryGetProperty("project_id", out var p) ? p.GetString() : null;
            var email = root.TryGetProperty("client_email", out var e) ? e.GetString() : null;
            var key = root.TryGetProperty("private_key", out var k) ? k.GetString() : null;
            var tokenUri = root.TryGetProperty("token_uri", out var t) ? t.GetString() : null;
            if (string.IsNullOrWhiteSpace(project) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(key))
                return null;
            if (!key.Contains("BEGIN", StringComparison.Ordinal))
                return null;
            return new Account(
                project.Trim(),
                email.Trim(),
                key.Replace("\\n", "\n", StringComparison.Ordinal),
                string.IsNullOrWhiteSpace(tokenUri) ? "https://oauth2.googleapis.com/token" : tokenUri.Trim());
        }
        catch
        {
            return null;
        }
    }
}
