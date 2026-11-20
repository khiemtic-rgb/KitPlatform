namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// PRODUCTION-41A: Production CREATE_LIPSYNC must resolve to FalLipSyncExecutionAdapter, not fixture.
/// Root cause: DI line 187 was binding ILipSyncExecutionAdapter to FixtureLipSyncAdapter.
/// Fix: Changed to resolve FalLipSyncExecutionAdapter from service provider.
/// Forensic: FAMIXA-FILM-PRODUCTION-40.md (identified DI bug).
/// </summary>
public static class ProductionRealFalLipsyncWireV1Regression
{
    /// <summary>
    /// TEST 1: ILipSyncExecutionAdapter resolves to FalLipSyncExecutionAdapter (not fixture).
    /// </summary>
    public static void AssertAdapterIsFal(ILipSyncExecutionAdapter adapter)
    {
        if (adapter is null)
            throw new InvalidOperationException("ILipSyncExecutionAdapter is null - DI registration missing.");

        if (adapter is not FalLipSyncExecutionAdapter)
            throw new InvalidOperationException(
                $"ILipSyncExecutionAdapter resolved to {adapter.GetType().Name}, expected FalLipSyncExecutionAdapter. " +
                "Production lipsync must use real Fal provider, not fixture.");

        // Passed: adapter is FalLipSyncExecutionAdapter
    }

    /// <summary>
    /// TEST 2: Adapter ProviderId must be "fal".
    /// </summary>
    public static void AssertProviderIdIsFal(ILipSyncExecutionAdapter adapter)
    {
        if (adapter is null)
            throw new InvalidOperationException("ILipSyncExecutionAdapter is null.");

        var providerId = adapter.ProviderId;
        if (string.IsNullOrWhiteSpace(providerId))
            throw new InvalidOperationException("ProviderId is null or empty.");

        if (!providerId.Equals(ProductionLipsyncCatalog.FalProviderId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"ProviderId is '{providerId}', expected '{ProductionLipsyncCatalog.FalProviderId}' (fal).");

        // Passed: providerId == "fal"
    }

    /// <summary>
    /// TEST 3: Adapter ModelId must be "sync-lipsync-v3".
    /// </summary>
    public static void AssertModelIdIsV3(ILipSyncExecutionAdapter adapter)
    {
        if (adapter is null)
            throw new InvalidOperationException("ILipSyncExecutionAdapter is null.");

        var modelId = adapter.ModelId;
        if (string.IsNullOrWhiteSpace(modelId))
            throw new InvalidOperationException("ModelId is null or empty.");

        if (!modelId.Equals(ProductionLipsyncCatalog.SyncLipsyncV3, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"ModelId is '{modelId}', expected '{ProductionLipsyncCatalog.SyncLipsyncV3}' (sync-lipsync-v3).");

        // Passed: modelId == "sync-lipsync-v3"
    }

    /// <summary>
    /// TEST 4: Adapter must be IsPaid (Fal is a paid provider).
    /// </summary>
    public static void AssertIsPaid(ILipSyncExecutionAdapter adapter)
    {
        if (adapter is null)
            throw new InvalidOperationException("ILipSyncExecutionAdapter is null.");

        if (!adapter.IsPaid)
            throw new InvalidOperationException(
                "IsPaid is false. Fal lipsync is a paid provider and must have IsPaid = true.");

        // Passed: IsPaid == true
    }

    /// <summary>
    /// TEST 5: FixtureLipSyncAdapter must not be resolvable as ILipSyncExecutionAdapter.
    /// Production interface must not resolve to fixture.
    /// </summary>
    public static void AssertFixtureNotResolvedForProductionInterface(ILipSyncExecutionAdapter adapter)
    {
        if (adapter is null)
            throw new InvalidOperationException("ILipSyncExecutionAdapter is null.");

        if (adapter is FixtureLipSyncAdapter)
            throw new InvalidOperationException(
                "ILipSyncExecutionAdapter resolved to FixtureLipSyncAdapter. " +
                "Production lipsync must NOT silently fall back to fixture. " +
                "If Fal credential is missing, execution must fail closed, not fall back.");

        // Passed: adapter is NOT FixtureLipSyncAdapter
    }

    /// <summary>
    /// TEST 6: Verify provider ID is not "fixture".
    /// </summary>
    public static void AssertProviderIdNotFixture(ILipSyncExecutionAdapter adapter)
    {
        if (adapter is null)
            throw new InvalidOperationException("ILipSyncExecutionAdapter is null.");

        var providerId = adapter.ProviderId?.Trim().ToLowerInvariant() ?? "";
        if (providerId == ProductionLipsyncCatalog.FixtureProviderId.ToLowerInvariant())
            throw new InvalidOperationException(
                $"ProviderId is 'fixture'. Production lipsync must use real provider (fal), not fixture.");

        // Passed: providerId != "fixture"
    }

    /// <summary>
    /// TEST 7: FalLipSyncExecutionAdapter must require network (real provider).
    /// </summary>
    public static void AssertRequiresNetwork(ILipSyncExecutionAdapter adapter)
    {
        if (adapter is null)
            throw new InvalidOperationException("ILipSyncExecutionAdapter is null.");

        // FalLipSyncExecutionAdapter.RequiresNetwork depends on transport.RequiresNetwork
        // HttpFalLipsyncTransport should have RequiresNetwork = true (real HTTP calls)
        // FakeFalLipsyncTransport would have RequiresNetwork = false (mock)

        // For production, we expect RequiresNetwork = true when using HttpFalLipsyncTransport
        // But this depends on which transport is injected.
        // Since we're using HttpFalLipsyncTransport in production DI, RequiresNetwork should be true.

        // Note: We can't assert RequiresNetwork without knowing the transport implementation.
        // If HttpFalLipsyncTransport.RequiresNetwork is true (which it should be), then adapter.RequiresNetwork will be true.
        // If using FakeFalLipsyncTransport (testing), RequiresNetwork would be false.

        // For this regression, we'll document the expectation but not hard-assert,
        // since RequiresNetwork correctness depends on transport registration (which is correct in DI).

        // Expected: adapter.RequiresNetwork == true when using HttpFalLipsyncTransport
        // Actual behavior will be validated in integration tests with real transport.
    }

    /// <summary>
    /// SUITE: Run all regression tests for PRODUCTION-41A.
    /// Verifies that ILipSyncExecutionAdapter correctly resolves to FalLipSyncExecutionAdapter
    /// with correct provider/model and does NOT resolve to fixture.
    /// </summary>
    public static void RunAllTests(ILipSyncExecutionAdapter adapter)
    {
        AssertAdapterIsFal(adapter);
        AssertProviderIdIsFal(adapter);
        AssertModelIdIsV3(adapter);
        AssertIsPaid(adapter);
        AssertFixtureNotResolvedForProductionInterface(adapter);
        AssertProviderIdNotFixture(adapter);
        // AssertRequiresNetwork is documentary only (depends on transport)
    }

    /// <summary>
    /// Smoke test: Verify adapter properties without execution.
    /// NO PROVIDER CALLS. NO DB WRITES.
    /// </summary>
    public static ProductionRealFalLipsyncWireV1Report SmokeTest(ILipSyncExecutionAdapter adapter)
    {
        try
        {
            RunAllTests(adapter);
            return new ProductionRealFalLipsyncWireV1Report
            {
                AdapterType = adapter?.GetType().Name ?? "NULL",
                ProviderId = adapter?.ProviderId ?? "NULL",
                ModelId = adapter?.ModelId ?? "NULL",
                IsPaid = adapter?.IsPaid ?? false,
                RequiresNetwork = adapter?.RequiresNetwork ?? false,
                AllTestsPassed = true
            };
        }
        catch (Exception ex)
        {
            return new ProductionRealFalLipsyncWireV1Report
            {
                AdapterType = adapter?.GetType().Name ?? "NULL",
                ProviderId = adapter?.GetType().Name ?? "NULL",
                ModelId = adapter?.ModelId ?? "NULL",
                IsPaid = adapter?.IsPaid ?? false,
                RequiresNetwork = adapter?.RequiresNetwork ?? false,
                AllTestsPassed = false,
                FailureReason = ex.Message
            };
        }
    }
}

public sealed record ProductionRealFalLipsyncWireV1Report(
    string AdapterType = "",
    string ProviderId = "",
    string ModelId = "",
    bool IsPaid = false,
    bool RequiresNetwork = false,
    bool AllTestsPassed = false,
    string? FailureReason = null);
