using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// One-shot Gemini still. The key argument is the only credential input; no global or environment key.
/// </summary>
public interface IGeminiTenantImageTransport
{
    int GenerationCount { get; }
    Task<GeminiProductionStillResult> GenerateOnceAsync(
        string apiKey, string modelId, string prompt, string? aspectRatio, CancellationToken ct = default);

    Task<GeminiProductionStillResult> GenerateWithReferencesAsync(
        string apiKey, string modelId, string prompt, string? aspectRatio,
        IReadOnlyList<GeminiStillReference> references, CancellationToken ct = default)
        => references is { Count: > 0 }
            ? throw new InvalidOperationException(ProductionLockedStillReference.NotSent)
            : GenerateOnceAsync(apiKey, modelId, prompt, aspectRatio, ct);
}

/// <summary>
/// Tenant-owned CREATE_IMAGE. Provider, model and credential come only from the frozen snapshot.
/// </summary>
public sealed class TenantGeminiImageExecutionAdapter : IImageExecutionAdapter
{
    readonly IGeminiTenantImageTransport _transport;
    readonly IProductionCredentialResolver _credentials;
    readonly IProductionLockedMasterSource? _masters;

    public TenantGeminiImageExecutionAdapter(
        IGeminiTenantImageTransport transport,
        IProductionCredentialResolver credentials,
        IProductionLockedMasterSource? masters = null)
    {
        _transport = transport;
        _credentials = credentials;
        _masters = masters;
    }

    public string ProviderId => ProductionImageCatalog.GeminiProviderId;
    public string ModelId => ProductionImageCatalog.GeminiFlashImage;
    public bool IsPaid => true;
    public bool RequiresNetwork => true;

    public async Task<ProductionMediaResult> ExecuteFrozenAsync(
        ProductionTaskInputSnapshot snapshot, byte[]? importBytes, string? importMime, CancellationToken ct = default)
    {
        _ = importMime;
        if (importBytes is { Length: > 0 })
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        if (!string.Equals(snapshot.ProviderId, ProviderId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(snapshot.ModelId, ModelId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);
        var prompt = (snapshot.Prompt ?? "").Trim();
        if (prompt.Length < 8)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        var material = await ProductionCredentialResolver.RequireAsync(
            _credentials, ProductionFrozenCredential.FromSnapshot(snapshot), snapshot.ProviderId, snapshot.ModelId, ct);
        if (string.IsNullOrWhiteSpace(material.Secret))
            throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
        var aspect = string.IsNullOrWhiteSpace(snapshot.AspectRatio) ? null : snapshot.AspectRatio.Trim();
        var references = await LockedReferencesAsync(snapshot, ct);
        var generated = references.Count > 0
            ? await _transport.GenerateWithReferencesAsync(material.Secret, snapshot.ModelId, prompt, aspect, references, ct)
            : await _transport.GenerateOnceAsync(material.Secret, snapshot.ModelId, prompt, aspect, ct);
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

    async Task<IReadOnlyList<GeminiStillReference>> LockedReferencesAsync(
        ProductionTaskInputSnapshot snapshot, CancellationToken ct)
    {
        if (_masters is null) return [];
        var frozen = ProductionLockedStillReference.Frozen(snapshot.Prompt);
        if (frozen.Count == 0)
        {
            if (ProductionLockedStillReference.Characters(snapshot.VisualContract).Count > 0)
                throw new InvalidOperationException(ProductionLockedStillReference.NotSent);
            return [];
        }
        var loaded = new List<GeminiStillReference>(frozen.Count);
        foreach (var (id, sha) in frozen)
        {
            var row = await _masters.ReadOfficialAsync(id, id, ct);
            if (row is null
                || row.Bytes is not { Length: > 32 }
                || !string.Equals(row.Sha256, sha, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(ProductionLockedStillReference.NotSent);
            loaded.Add(row with { Sha256 = sha.ToLowerInvariant() });
        }
        return loaded;
    }
}
