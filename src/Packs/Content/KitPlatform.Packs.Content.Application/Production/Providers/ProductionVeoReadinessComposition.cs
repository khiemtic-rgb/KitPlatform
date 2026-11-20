namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Test-only / readiness composition. Production resolver stays Fixture + Runway.
/// Do not register VeoMotionExecutionAdapter on IProductionMotionAdapterResolver.
/// </summary>
public static class ProductionVeoReadinessComposition
{
    public const bool ProductionResolverIncludesVeo = false;
    public const bool RealVertexExecutionOpen = false;

    public static IProductionMotionAdapterResolver CreateProductionResolver() =>
        new ProductionMotionAdapterResolver(
        [
            new FixtureMotionAdapter(),
            new RunwayMotionExecutionAdapter(new FakeRunwayTransport()),
        ]);

    public static ProductionVeoTestGraph CreateClosedTestGraph(
        FakeVeoHttpMessageHandler? handler = null,
        VeoHttpSettings? settings = null)
    {
        handler ??= new FakeVeoHttpMessageHandler();
        var auth = new FakeVeoHttpAuthenticator();
        var http = new HttpClient(handler)
        {
            Timeout = ProductionVeoHttpClientPolicy.RequestTimeout,
        };
        var transport = new CountingVeoMotionTransport(
            new HttpVeoMotionTransport(http, auth, settings ?? new VeoHttpSettings("test-project")));
        var adapter = new VeoMotionExecutionAdapter(transport);
        return new ProductionVeoTestGraph(adapter, transport, auth, handler, http);
    }
}

public static class ProductionVeoHttpClientPolicy
{
    public const string Name = "content-veo";
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(4);
    public const int StartAutomaticRetryCount = 0;
    public const int OsPollAttempts = 90;
    public static readonly TimeSpan OsPollInterval = TimeSpan.FromSeconds(2);
}

public sealed record ProductionVeoTestGraph(
    VeoMotionExecutionAdapter Adapter,
    CountingVeoMotionTransport Transport,
    FakeVeoHttpAuthenticator Authenticator,
    FakeVeoHttpMessageHandler Handler,
    HttpClient Http);

/// <summary>Counts Start/Poll. Does not invent jobs. Forwards to an inner transport.</summary>
public sealed class CountingVeoMotionTransport : IVeoMotionTransport
{
    readonly IVeoMotionTransport _inner;

    public CountingVeoMotionTransport(IVeoMotionTransport inner) => _inner = inner;

    public int StartCount { get; private set; }
    public int PollCount { get; private set; }
    public bool RequiresNetwork => _inner.RequiresNetwork;
    public int HttpCallCount => _inner.HttpCallCount;

    public Task<VeoMotionTransportCreateResult> CreateImageToVideoAsync(
        VeoMotionTransportCreateRequest request, CancellationToken ct = default)
    {
        StartCount++;
        return _inner.CreateImageToVideoAsync(request, ct);
    }

    public Task<VeoMotionTransportPollResult> GetOperationAsync(
        string providerJobId, CancellationToken ct = default)
    {
        PollCount++;
        return _inner.GetOperationAsync(providerJobId, ct);
    }

    public Task<byte[]> DownloadOutputAsync(string providerJobId, CancellationToken ct = default) =>
        _inner.DownloadOutputAsync(providerJobId, ct);
}

public sealed class CountingProductionExecutionPermitStore : IProductionExecutionPermitStore
{
    readonly InMemoryProductionExecutionPermitStore _inner = new();
    public int ConsumeCalls { get; private set; }
    public int IssueCalls { get; private set; }

    public Task<ProductionExecutionPermit> IssueAsync(
        IssueProductionExecutionPermitCommand command, CancellationToken ct = default)
    {
        IssueCalls++;
        return _inner.IssueAsync(command, ct);
    }

    public Task<ProductionExecutionPermit?> GetIssuedAsync(
        Guid buildId, string shotCode, string action, string providerId, string modelId, CancellationToken ct = default) =>
        _inner.GetIssuedAsync(buildId, shotCode, action, providerId, modelId, ct);

    public Task<ProductionExecutionPermit?> GetLatestAsync(
        Guid buildId, string shotCode, string action, string providerId, string modelId, CancellationToken ct = default) =>
        _inner.GetLatestAsync(buildId, shotCode, action, providerId, modelId, ct);

    public Task<bool> TryConsumeAsync(
        Guid buildId, string shotCode, string action, string providerId, string modelId, CancellationToken ct = default)
    {
        ConsumeCalls++;
        return _inner.TryConsumeAsync(buildId, shotCode, action, providerId, modelId, ct);
    }

    public Task<IReadOnlyList<ProductionExecutionPermit>> ListByTargetAsync(
        Guid buildId, string shotCode, string action, string providerId, string modelId, CancellationToken ct = default) =>
        _inner.ListByTargetAsync(buildId, shotCode, action, providerId, modelId, ct);

    public void AddExisting(ProductionExecutionPermit permit) => _inner.AddExisting(permit);
}
