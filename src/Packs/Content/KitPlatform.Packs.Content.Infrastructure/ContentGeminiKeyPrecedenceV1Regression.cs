using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using KitPlatform.Infrastructure.Configuration;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

public static class ContentGeminiKeyPrecedenceV1Regression
{
    public const string SuiteId = "FAMIXA_VIDEO_RECOVERY_002_GEMINI_KEY";
    const string OldKey = "old-stored-gemini-key-value";
    const string NewKey = "new-secret-ref-gemini-key";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        try
        {
            var stored = Resolve("""{"geminiApiKey":"old-stored-gemini-key-value"}""", new Dictionary<string, string?>(), new ContentOptions());
            Ok(stored.KeySource == ContentGeminiKeyPrecedence.SourceStoredKey
               && stored.ApiKey == OldKey
               && !stored.ToString().Contains(OldKey, StringComparison.Ordinal),
                "stored key is used only when no secret reference was saved");

            var secret = Resolve(
                """{"geminiApiKey":"old-stored-gemini-key-value","geminiApiKeySecretRef":"FAMIXA_GEMINI_KEY"}""",
                new Dictionary<string, string?> { ["FAMIXA_GEMINI_KEY"] = NewKey },
                new ContentOptions { GeminiApiKey = "options-key-should-not-win" });
            Ok(secret.KeySource == ContentGeminiKeyPrecedence.SourceSecretRef
               && secret.ApiKey == NewKey
               && secret.ApiKey != OldKey
               && secret.KeyFingerprint is { Length: 8 }
               && !secret.ToString().Contains(NewKey, StringComparison.Ordinal)
               && !secret.ToString().Contains(OldKey, StringComparison.Ordinal),
                "an explicit secret reference wins over the stored key and options");

            var unresolved = Resolve(
                """{"geminiApiKey":"old-stored-gemini-key-value","geminiApiKeySecretRef":"FAMIXA_GEMINI_KEY"}""",
                new Dictionary<string, string?>(),
                new ContentOptions { GeminiApiKey = "options-key-should-not-win" });
            Ok(unresolved.KeySource == ContentGeminiKeyPrecedence.SourceMissing
               && unresolved.ApiKey is null
               && !unresolved.ApiKeyConfigured,
                "an unresolved custom secret reference does not fall back to the old stored key");

            var invalid = Resolve(
                """{"geminiApiKey":"old-stored-gemini-key-value","geminiApiKeySecretRef":"bad ref"}""",
                new Dictionary<string, string?> { ["bad ref"] = NewKey },
                new ContentOptions());
            Ok(invalid.KeySource == ContentGeminiKeyPrecedence.SourceInvalid && invalid.ApiKey is null,
                "an invalid secret reference is a configuration error");

            var blank = Resolve(
                """{"geminiApiKey":"   ","geminiApiKeySecretRef":""}""",
                new Dictionary<string, string?>(),
                new ContentOptions());
            Ok(blank.KeySource is ContentGeminiKeyPrecedence.SourceMissing or ContentGeminiKeyPrecedence.SourceEnvironment
               && blank.ApiKey != "   ",
                "a blank stored key is not a key");

            var options = Resolve("{}", new Dictionary<string, string?>(), new ContentOptions { GeminiApiKey = "options-gemini-key" });
            Ok(options.KeySource == ContentGeminiKeyPrecedence.SourceOptions && options.ApiKey == "options-gemini-key",
                "options are the fallback when no stored key and no secret reference are configured");

            var env = ContentGeminiKeyPrecedence.Resolve(
                null, null, false, null, null, "env-gemini-key", null, null);
            Ok(env.Source == ContentGeminiKeyPrecedence.SourceEnvironment
               && env.ApiKey == "env-gemini-key"
               && !env.ToString().Contains("env-gemini-key", StringComparison.Ordinal),
                "environment GEMINI_API_KEY is the fallback after stored key and options");

            var canonical = ContentGeminiKeyPrecedence.Resolve(
                OldKey,
                ContentGeminiKeyPrecedence.CanonicalSecretRef,
                true,
                name => name == ContentGeminiKeyPrecedence.CanonicalSecretRef ? null : null,
                "options-key",
                null,
                "google-env-key",
                null);
            Ok(canonical.Source == ContentGeminiKeyPrecedence.SourceEnvironment
               && canonical.ApiKey == "google-env-key"
               && canonical.ApiKey != OldKey,
                "a configured GEMINI_API_KEY reference does not revive the old stored key");

            var broken = ContentAiConfigParser.Parse("{");
            Ok(!broken.SecretRefSpecified && broken.GeminiApiKey is null, "malformed settings do not invent a secret reference");

            var shared = ContentParkGeminiKeyResolver.ResolveSettingsFromOrgJson(
                """{"textModel":"gemini-3.6-flash","geminiApiKey":"old-stored-gemini-key-value","geminiApiKeySecretRef":"FAMIXA_GEMINI_KEY"}""",
                new MapConfiguration(new Dictionary<string, string?> { ["FAMIXA_GEMINI_KEY"] = NewKey }));
            Ok(shared.ApiKey == NewKey && shared.TextModel == "gemini-3.6-flash",
                "the shared Content Park resolver uses the same secret-reference order");

            var client = File.ReadAllText(Path.Combine(RepoRoot(),
                "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure", "ContentGeminiClient.cs"));
            var logAt = client.IndexOf("Content Park Gemini key source", StringComparison.Ordinal);
            Ok(logAt > 0, "resolver logs the configuration source");
            if (logAt > 0)
            {
                var window = client.Substring(logAt, Math.Min(280, client.Length - logAt));
                Ok(window.Contains("resolved.KeySource", StringComparison.Ordinal)
                   && window.Contains("resolved.ApiKeyConfigured", StringComparison.Ordinal)
                   && window.Contains("resolved.KeyFingerprint", StringComparison.Ordinal)
                   && !window.Contains("resolved.ApiKey,", StringComparison.Ordinal)
                   && !window.Contains("resolved.ApiKey)", StringComparison.Ordinal),
                    "the resolver log does not include the API key");
            }
        }
        catch (Exception ex)
        {
            fail.Add("UNCAUGHT:" + ex.GetType().Name + ":" + ex.Message);
        }

        return fail;
    }

    static ContentAiResolved Resolve(string json, Dictionary<string, string?> values, ContentOptions options)
    {
        var state = ContentAiConfigParser.Parse(json);
        return ContentAiConfigParser.Resolve(state, options, new MapConfiguration(values));
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src", "KitPlatform.Api", "KitPlatform.Api.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return @"E:\KitPlatform";
    }

    sealed class MapConfiguration(Dictionary<string, string?> values) : IConfiguration
    {
        public string? this[string key]
        {
            get => values.TryGetValue(key, out var value) ? value : null;
            set => values[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => Array.Empty<IConfigurationSection>();
        public IChangeToken GetReloadToken() => NoChange.Instance;
        public IConfigurationSection GetSection(string key) => new MapSection(key);
    }

    sealed class MapSection(string key) : IConfigurationSection
    {
        public string? this[string child]
        {
            get => null;
            set { }
        }

        public string Key { get; } = key;
        public string Path { get; } = key;
        public string? Value { get; set; }
        public IEnumerable<IConfigurationSection> GetChildren() => Array.Empty<IConfigurationSection>();
        public IChangeToken GetReloadToken() => NoChange.Instance;
        public IConfigurationSection GetSection(string child) => new MapSection(child);
    }

    sealed class NoChange : IChangeToken
    {
        public static readonly NoChange Instance = new();
        public bool HasChanged => false;
        public bool ActiveChangeCallbacks => false;
        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => new Noop();
        sealed class Noop : IDisposable { public void Dispose() { } }
    }
}
