namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Explicit, process-local, one-shot paid-network authorization.
/// Not a global config change. Consumed only at real Start.
/// </summary>
public interface IProductionPaidNetworkPermit
{
    int StartCount { get; }
    bool IsEligible(ProductionScope scope, string shotCode, string providerId, string modelId);
    bool TryConsumeStart(ProductionScope scope, string shotCode, string providerId, string modelId);
}

public sealed class ProductionPaidNetworkOneShotPermit : IProductionPaidNetworkPermit
{
    int _starts;

    public int StartCount => Volatile.Read(ref _starts);

    public bool IsEligible(ProductionScope scope, string shotCode, string providerId, string modelId)
        => Matches(scope, shotCode, providerId, modelId) && Volatile.Read(ref _starts) == 0;

    public bool TryConsumeStart(ProductionScope scope, string shotCode, string providerId, string modelId)
    {
        if (!Matches(scope, shotCode, providerId, modelId))
            return false;
        return Interlocked.CompareExchange(ref _starts, 1, 0) == 0;
    }

    public static bool Matches(ProductionScope scope, string shotCode, string providerId, string modelId)
    {
        if (scope.BuildId != ProductionTestSandboxIds.ScopeId)
            return false;
        if (!string.Equals((shotCode ?? "").Trim(), ProductionTestSandboxIds.ShotCode, StringComparison.Ordinal))
            return false;
        return string.Equals((providerId ?? "").Trim(), ProductionMotionCatalog.RunwayProviderId, StringComparison.OrdinalIgnoreCase)
            && string.Equals((modelId ?? "").Trim(), ProductionMotionCatalog.RunwayGen4Turbo, StringComparison.OrdinalIgnoreCase);
    }
}
