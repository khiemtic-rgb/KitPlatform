using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Vertex auth field contract. Never stores PEM / access token.
/// Parse only from an explicit caller-supplied string — never env / Join file / IConfiguration.
/// </summary>
public sealed record VeoHttpCredentialContract(
    string ProjectId,
    string ClientEmail,
    string TokenUri,
    bool HasPrivateKeyPem,
    string Scope = VeoHttpAuthContract.Scope,
    int JwtLifetimeSeconds = VeoHttpAuthContract.JwtLifetimeSeconds);

public static class VeoHttpAuthContract
{
    public const string Algorithm = "RS256";
    public const string TokenType = "JWT";
    public const string Scope = "https://www.googleapis.com/auth/cloud-platform";
    public const string GrantType = "urn:ietf:params:oauth:grant-type:jwt-bearer";
    public const int JwtLifetimeSeconds = 3600;
    public const string DefaultTokenUri = "https://oauth2.googleapis.com/token";
}

/// <summary>
/// Documented precedence from ContentVideoConfig.Resolve. Does not read secrets or Environment.
/// First non-empty wins. Callers pass already-resolved slot values.
/// </summary>
public static class VeoHttpConfigPrecedence
{
    public static readonly IReadOnlyList<string> JoinFileSourceOrder =
    [
        "videoConfigJson.veoJoinFile",
        "veoJoinSecretRef",
        "ContentOptions.VeoJoinFile",
        "Content:VeoJoinFile",
        "VEO_JOIN_FILE",
    ];

    public static readonly IReadOnlyList<string> LocationSourceOrder =
    [
        "ContentOptions.VeoLocation",
        "Content:VeoLocation",
        "us-central1",
    ];

    public const string DefaultLocation = "us-central1";

    public static string? ResolveJoinFile(
        string? videoStateFile,
        string? secretRefValue,
        string? optionsFile,
        string? configurationKey,
        string? environmentVariable) =>
        FirstNonEmpty(videoStateFile, secretRefValue, optionsFile, configurationKey, environmentVariable);

    public static string ResolveLocation(string? optionsLocation, string? configurationLocation) =>
        FirstNonEmpty(optionsLocation, configurationLocation, DefaultLocation)!;

    public static VeoHttpCredentialParse ParseExplicit(string? rawJson)
        => VeoHttpServiceAccountValidator.ValidateExplicit(rawJson);

    public static VeoHttpSettings ToSettings(this VeoHttpCredentialContract contract, string? location = null) =>
        new(contract.ProjectId, ResolveLocation(location, null));

    public static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
        }
        return null;
    }
}

public sealed record VeoHttpCredentialParse(bool Ok, string? Code, VeoHttpCredentialContract? Contract);

public static class VeoHttpServiceAccountCodes
{
    public const string TypeMissing = "VEO_SA_TYPE_MISSING";
    public const string TypeInvalid = "VEO_SA_TYPE_INVALID";
    public const string ProjectIdMissing = "VEO_SA_PROJECT_ID_MISSING";
    public const string ClientEmailMissing = "VEO_SA_CLIENT_EMAIL_MISSING";
    public const string PrivateKeyMissing = "VEO_SA_PRIVATE_KEY_MISSING";
    public const string PrivateKeyInvalid = "VEO_SA_PRIVATE_KEY_INVALID";
    public const string TokenUriInvalid = "VEO_SA_TOKEN_URI_INVALID";
}

/// <summary>
/// Offline service-account shape check. Explicit JSON only — never env / Join file / IConfiguration.
/// Does not ImportFromPem, sign, or POST token_uri.
/// </summary>
public static class VeoHttpServiceAccountValidator
{
    public static VeoHttpCredentialParse ValidateExplicit(string? rawJson)
    {
        var gate = FamixaVeoJoinCredentialsV1.Accept(rawJson, "explicit-sa.json");
        if (!gate.Ok)
            return new VeoHttpCredentialParse(false, gate.Code, null);
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(gate.NormalizedText!);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl) || string.IsNullOrWhiteSpace(typeEl.GetString()))
                return Fail(VeoHttpServiceAccountCodes.TypeMissing);
            if (!string.Equals(typeEl.GetString()!.Trim(), "service_account", StringComparison.OrdinalIgnoreCase))
                return Fail(VeoHttpServiceAccountCodes.TypeInvalid);
            if (!TryText(root, "project_id", out var project))
                return Fail(VeoHttpServiceAccountCodes.ProjectIdMissing);
            if (!TryText(root, "client_email", out var email))
                return Fail(VeoHttpServiceAccountCodes.ClientEmailMissing);
            if (!root.TryGetProperty("private_key", out var keyEl) || string.IsNullOrWhiteSpace(keyEl.GetString()))
                return Fail(VeoHttpServiceAccountCodes.PrivateKeyMissing);
            var pem = keyEl.GetString()!.Replace("\\n", "\n", StringComparison.Ordinal);
            if (!IsPemShape(pem))
                return Fail(VeoHttpServiceAccountCodes.PrivateKeyInvalid);

            string tokenUri;
            if (!root.TryGetProperty("token_uri", out var uriEl) || string.IsNullOrWhiteSpace(uriEl.GetString()))
                tokenUri = VeoHttpAuthContract.DefaultTokenUri;
            else if (!IsHttpsAbsolute(uriEl.GetString()!))
                return Fail(VeoHttpServiceAccountCodes.TokenUriInvalid);
            else
                tokenUri = uriEl.GetString()!.Trim();

            return new VeoHttpCredentialParse(
                true,
                null,
                new VeoHttpCredentialContract(
                    project,
                    email,
                    tokenUri,
                    HasPrivateKeyPem: true));
        }
        catch (System.Text.Json.JsonException)
        {
            return Fail(ProductionFailureReasons.InvalidProviderResponse);
        }
    }

    static bool TryText(System.Text.Json.JsonElement root, string name, out string value)
    {
        value = "";
        if (!root.TryGetProperty(name, out var el) || string.IsNullOrWhiteSpace(el.GetString()))
            return false;
        value = el.GetString()!.Trim();
        return true;
    }

    static bool IsPemShape(string pem)
    {
        var raw = (pem ?? "").Trim();
        return (raw.Contains("BEGIN PRIVATE KEY", StringComparison.Ordinal)
                || raw.Contains("BEGIN RSA PRIVATE KEY", StringComparison.Ordinal))
            && (raw.Contains("END PRIVATE KEY", StringComparison.Ordinal)
                || raw.Contains("END RSA PRIVATE KEY", StringComparison.Ordinal));
    }

    static bool IsHttpsAbsolute(string raw) =>
        Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri)
        && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    static VeoHttpCredentialParse Fail(string code) => new(false, code, null);
}
