using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public interface IProductionMotionAdapterResolver
{
    IMotionExecutionAdapter Resolve(
        string? providerId,
        string? modelId,
        FamixaProviderCapability requiredCapability = FamixaProviderCapability.Motion);
}

/// <summary>Fail closed. Never silently falls back to fixture or another provider.</summary>
public sealed class ProductionMotionAdapterResolver : IProductionMotionAdapterResolver
{
    readonly IReadOnlyList<IMotionExecutionAdapter> _adapters;

    public ProductionMotionAdapterResolver(IEnumerable<IMotionExecutionAdapter> adapters)
        => _adapters = adapters.ToArray();

    public IMotionExecutionAdapter Resolve(
        string? providerId,
        string? modelId,
        FamixaProviderCapability requiredCapability = FamixaProviderCapability.Motion)
    {
        var provider = (providerId ?? "").Trim();
        var model = (modelId ?? "").Trim();
        if (provider.Length == 0 || model.Length == 0)
            throw new InvalidOperationException(ProductionFailureReasons.MissingProviderModel);

        if (!ProductionMotionCatalog.ProviderExists(provider))
            throw new InvalidOperationException(ProductionFailureReasons.UnknownProvider);

        if (!ProductionMotionCatalog.TryGet(provider, model, out var catalog))
        {
            if (ProductionMotionCatalog.All.Any(m =>
                    string.Equals(m.ProviderId, provider, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);
            throw new InvalidOperationException(ProductionFailureReasons.UnknownProvider);
        }

        if (!string.Equals(catalog.ProviderId, provider, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(catalog.ModelId, model, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);

        if (!catalog.Enabled)
            throw new InvalidOperationException(ProductionFailureReasons.ProviderDisabled);

        if (catalog.Capability != requiredCapability || catalog.Action != ProductionActions.CreateVideo)
            throw new InvalidOperationException(ProductionFailureReasons.CapabilityUnsupported);

        var adapter = _adapters.FirstOrDefault(a =>
            string.Equals(a.ProviderId, catalog.ProviderId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.ModelId, catalog.ModelId, StringComparison.OrdinalIgnoreCase));
        if (adapter is null)
            throw new InvalidOperationException(ProductionFailureReasons.UnknownProvider);

        return adapter;
    }
}
