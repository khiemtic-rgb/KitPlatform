using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public sealed record GeminiProductionStillResult(
    bool Accepted,
    byte[]? Bytes,
    string? Mime,
    string? Model,
    string? Error);

/// <summary>
/// Existing one-shot Gemini still. No model fallback and no Pollinations.
/// </summary>
public interface IGeminiProductionStillClient
{
    int GenerationCount { get; }
    Task<string> RequireModelAsync(CancellationToken ct = default);
    Task<GeminiProductionStillResult> GenerateOnceAsync(string prompt, string? aspectRatio, CancellationToken ct = default);
}

/// <summary>
/// Production OS image adapter. Prompt text is the frozen snapshot. Artifact registration stays in Production OS.
/// </summary>
public sealed class GeminiImageExecutionAdapter : IImageExecutionAdapter
{
    readonly IGeminiProductionStillClient _client;

    public GeminiImageExecutionAdapter(IGeminiProductionStillClient client) => _client = client;

    public string ProviderId => ImageGenerationExecutionRules.Provider;
    public string ModelId => ProductionImageCatalog.GeminiFlashImage;
    public bool IsPaid => true;
    public bool RequiresNetwork => true;
    public Task<string> RequireModelAsync(CancellationToken ct = default) => _client.RequireModelAsync(ct);

    public async Task<ProductionMediaResult> ExecuteFrozenAsync(
        ProductionTaskInputSnapshot snapshot, byte[]? importBytes, string? importMime, CancellationToken ct = default)
    {
        _ = importMime;
        if (importBytes is { Length: > 0 })
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        if (!string.Equals(snapshot.ProviderId, ProviderId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);
        var prompt = (snapshot.Prompt ?? "").Trim();
        if (prompt.Length < 8)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        var aspect = string.IsNullOrWhiteSpace(snapshot.AspectRatio) ? null : snapshot.AspectRatio.Trim();
        var generated = await _client.GenerateOnceAsync(prompt, aspect, ct);
        if (!generated.Accepted || generated.Bytes is not { Length: >= 32 })
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(generated.Error)
                ? ProductionFailureReasons.ProviderOutputMissing
                : generated.Error);
        if (!string.IsNullOrWhiteSpace(generated.Model)
            && !string.Equals(generated.Model, snapshot.ModelId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);
        var mime = string.IsNullOrWhiteSpace(generated.Mime) ? "image/png" : generated.Mime.Trim();
        var sha = KitVideoIntegrityRules.Sha256Hex(generated.Bytes);
        return new ProductionMediaResult("gemini-" + sha[..12], generated.Bytes, mime, 0);
    }
}
