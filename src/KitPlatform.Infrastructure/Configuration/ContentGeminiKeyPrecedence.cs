using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KitPlatform.Infrastructure.Configuration;

/// <summary>
/// Gemini key selection for Content Park. A configured secret reference wins.
/// A stale stored key is not used once the owner has set that reference.
/// The decision text never includes the key.
/// </summary>
public static class ContentGeminiKeyPrecedence
{
    public const string CanonicalSecretRef = "GEMINI_API_KEY";
    public const string SourceSecretRef = "secret-ref";
    public const string SourceStoredKey = "stored-key";
    public const string SourceOptions = "options";
    public const string SourceEnvironment = "environment";
    public const string SourceMissing = "missing";
    public const string SourceInvalid = "invalid-secret-ref";

    public sealed record Decision(
        string? ApiKey,
        string Source,
        string? SecretRef,
        bool SecretRefConfigured,
        string? Fingerprint)
    {
        public bool Configured => !string.IsNullOrWhiteSpace(ApiKey);

        public override string ToString() =>
            FormattableString.Invariant(
                $"GeminiKey source={Source} configured={Configured} fingerprint={Fingerprint ?? "-"} ref={SecretRef ?? "-"}");
    }

    public static bool JsonSpecifiesSecretRef(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("geminiApiKeySecretRef", out _);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static Decision Resolve(
        string? storedKey,
        string? secretRef,
        bool secretRefSpecified,
        Func<string, string?>? resolveNamedSecret,
        string? optionsKey,
        string? geminiEnvironment,
        string? googleEnvironment,
        string? googleGenerativeEnvironment)
    {
        var stored = Blank(storedKey);
        var refName = Blank(secretRef);
        if (secretRefSpecified && refName is not null)
        {
            if (!IsSecretRefName(refName))
                return new Decision(null, SourceInvalid, refName, true, null);

            var fromRef = resolveNamedSecret is null ? null : Blank(resolveNamedSecret(refName));
            if (fromRef is not null)
                return Done(fromRef, SourceSecretRef, refName, true);

            if (string.Equals(refName, CanonicalSecretRef, StringComparison.Ordinal))
            {
                var sibling = Blank(googleEnvironment) ?? Blank(googleGenerativeEnvironment);
                if (sibling is not null)
                    return Done(sibling, SourceEnvironment, refName, true);
            }

            return new Decision(null, SourceMissing, refName, true, null);
        }

        if (stored is not null)
            return Done(stored, SourceStoredKey, null, false);
        var options = Blank(optionsKey);
        if (options is not null)
            return Done(options, SourceOptions, null, false);
        var env = Blank(geminiEnvironment) ?? Blank(googleEnvironment) ?? Blank(googleGenerativeEnvironment);
        if (env is not null)
            return Done(env, SourceEnvironment, null, false);
        return new Decision(null, SourceMissing, null, false, null);
    }

    public static string? FingerprintOf(string? apiKey)
    {
        var key = Blank(apiKey);
        if (key is null)
            return null;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hash)[..8].ToLowerInvariant();
    }

    static Decision Done(string key, string source, string? secretRef, bool secretRefConfigured) =>
        new(key, source, secretRef, secretRefConfigured, FingerprintOf(key));

    static bool IsSecretRefName(string name)
    {
        if (name.Length is < 1 or > 128)
            return false;
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c is '_' or ':' or '-' or '.')
                continue;
            return false;
        }
        return true;
    }

    static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
