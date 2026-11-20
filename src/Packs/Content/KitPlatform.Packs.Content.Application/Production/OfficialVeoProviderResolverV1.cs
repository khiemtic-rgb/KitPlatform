using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public sealed record OfficialVeoProviderResolverRequest(
    ProductionScope Scope,
    ProductionAiSelection? Selection,
    OfficialVeoHttpCompositionResult? HttpComposition = null);

public sealed record OfficialVeoProviderResolverResult(
    bool Ready,
    string State,
    string? BlockReason,
    IProductionMotionAdapterResolver? Resolver,
    VeoMotionExecutionAdapter? Adapter,
    HttpVeoMotionTransport? Transport,
    FakeVeoHttpMessageHandler? Handler,
    FakeVeoHttpAuthenticator? Authenticator,
    string ProviderId,
    string ModelId);

/// <summary>
/// Official google/veo-3.1 resolver only. Binds
/// <see cref="VeoMotionExecutionAdapter"/> + <see cref="HttpVeoMotionTransport"/>.
/// Not registered on production DI. Does not Start.
/// </summary>
public static class OfficialVeoProviderResolverV1
{
    public const bool ProductionDiRegistered = false;

    public static OfficialVeoProviderResolverResult Compose(OfficialVeoProviderResolverRequest request)
    {
        try
        {
            OfficialVeoProductionAuthenticatorV1.RequireOfficialTarget(
                new OfficialVeoAuthenticatorCompositionRequest(request.Scope, request.Selection));

            var settings = request.HttpComposition is { Ready: true, Settings: { } composed }
                ? composed
                : new VeoHttpSettings(
                    OfficialVeoHttpCompositionV1.DefaultComposeProject,
                    VeoHttpConfigPrecedence.DefaultLocation,
                    VeoVertexEndpoints.VertexModel);
            var handler = new FakeVeoHttpMessageHandler();
            var authenticator = new FakeVeoHttpAuthenticator();
            var transport = new HttpVeoMotionTransport(new HttpClient(handler), authenticator, settings);
            var adapter = new VeoMotionExecutionAdapter(transport);
            if (!string.Equals(adapter.ProviderId, RealVertexExecutionV1Lock.ProviderId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(adapter.ModelId, RealVertexExecutionV1Lock.ModelId, StringComparison.OrdinalIgnoreCase)
                || adapter is not VeoMotionExecutionAdapter
                || transport is not HttpVeoMotionTransport)
                throw new InvalidOperationException(ProductionFailureReasons.UnknownProvider);

            var resolver = new OfficialVeoScopedResolver(adapter);
            var resolved = resolver.Resolve(RealVertexExecutionV1Lock.ProviderId, RealVertexExecutionV1Lock.ModelId);
            if (resolved is null || !ReferenceEquals(resolved, adapter))
                throw new InvalidOperationException(ProductionFailureReasons.UnknownProvider);
            if (transport.HttpCallCount != 0 || handler.Requests.Count != 0 || authenticator.Calls != 0)
                throw new InvalidOperationException(ProductionFailureReasons.VertexExecutionClosed);

            return new OfficialVeoProviderResolverResult(
                true,
                ProductionFailureReasons.ProviderResolverReady,
                null,
                resolver,
                adapter,
                transport,
                handler,
                authenticator,
                RealVertexExecutionV1Lock.ProviderId,
                RealVertexExecutionV1Lock.ModelId);
        }
        catch (InvalidOperationException ex)
        {
            return new OfficialVeoProviderResolverResult(
                false,
                ProductionFailureReasons.UnknownProvider,
                CodeOf(ex.Message),
                null, null, null, null, null,
                RealVertexExecutionV1Lock.ProviderId,
                RealVertexExecutionV1Lock.ModelId);
        }
    }

    public static bool IsBoundTo(RealVertexExecutionPreflightRequest request) =>
        OfficialVeoProductionAuthenticatorV1.IsBoundTo(request);

    static string CodeOf(string message)
    {
        var raw = (message ?? "").Trim();
        var sep = raw.IndexOf(" · ", StringComparison.Ordinal);
        return sep < 0 ? raw : raw[..sep];
    }
}

/// <summary>Resolves google/veo-3.1 only. Fixture and Runway cannot satisfy this gate.</summary>
public sealed class OfficialVeoScopedResolver : IProductionMotionAdapterResolver
{
    readonly VeoMotionExecutionAdapter _adapter;

    public OfficialVeoScopedResolver(VeoMotionExecutionAdapter adapter) => _adapter = adapter;

    public IMotionExecutionAdapter Resolve(
        string? providerId,
        string? modelId,
        FamixaProviderCapability requiredCapability = FamixaProviderCapability.Motion)
    {
        var provider = (providerId ?? "").Trim();
        var model = (modelId ?? "").Trim();
        if (provider.Length == 0 || model.Length == 0)
            throw new InvalidOperationException(ProductionFailureReasons.MissingProviderModel);
        if (string.Equals(provider, ProductionMotionCatalog.FixtureProviderId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, ProductionMotionCatalog.RunwayProviderId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);
        if (!string.Equals(provider, RealVertexExecutionV1Lock.ProviderId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(model, RealVertexExecutionV1Lock.ModelId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionFailureReasons.UnknownProvider);
        return _adapter;
    }
}
