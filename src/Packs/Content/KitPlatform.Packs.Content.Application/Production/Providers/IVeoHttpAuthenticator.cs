namespace KitPlatform.Packs.Content.Production;

public interface IVeoHttpAuthenticator
{
    Task<string> GetAccessTokenAsync(CancellationToken ct = default);

    Task<string> AuthorizeFrozenAsync(ProductionCredentialMaterial material, CancellationToken ct = default)
        => GetAccessTokenAsync(ct);
}

/// <summary>Deterministic bearer. Never reads Join/env/secrets.</summary>
public sealed class FakeVeoHttpAuthenticator : IVeoHttpAuthenticator
{
    public const string Token = "test-veo-bearer-token";
    public int Calls { get; private set; }

    public Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        Calls++;
        return Task.FromResult(Token);
    }
}

public sealed record VeoHttpSettings(
    string ProjectId,
    string Location = "us-central1",
    string VertexModel = VeoVertexEndpoints.VertexModel);

public static class VeoVertexEndpoints
{
    public const string VertexModel = "veo-3.1-generate-001";
    public const string Publisher = "google";
    public const string ApiVersion = "v1";

    public static string PredictLongRunning(VeoHttpSettings settings) =>
        Host(settings) + ":predictLongRunning";

    public static string FetchPredictOperation(VeoHttpSettings settings) =>
        Host(settings) + ":fetchPredictOperation";

    static string Host(VeoHttpSettings settings)
    {
        var location = string.IsNullOrWhiteSpace(settings.Location) ? "us-central1" : settings.Location.Trim();
        var project = (settings.ProjectId ?? "").Trim();
        var model = string.IsNullOrWhiteSpace(settings.VertexModel) ? VertexModel : settings.VertexModel.Trim();
        return $"https://{location}-aiplatform.googleapis.com/{ApiVersion}/projects/{project}/locations/{location}/publishers/{Publisher}/models/{model}";
    }
}

public static class VeoVertexOperationId
{
    public const string Prefix = "veo_";

    public static string Encode(string operationName)
    {
        var b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(operationName))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return Prefix + b64;
    }

    public static string Decode(string providerJobId)
    {
        var raw = (providerJobId ?? "").Trim();
        if (raw.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            raw = raw[Prefix.Length..];
        raw = raw.Replace('-', '+').Replace('_', '/');
        while (raw.Length % 4 != 0) raw += "=";
        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(raw));
    }
}
