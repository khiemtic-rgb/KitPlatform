using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>Real Fal HTTP. Start is one-shot and requires a consumed official CREATE_LIPSYNC permit.</summary>
internal sealed class HttpFalLipsyncTransport : IFalLipsyncTransport
{
    readonly ContentFalClient _client;
    readonly IProductionPaidNetworkPermit _permit;
    readonly IProductionExecutionPermitStore _officialPermits;
    readonly ProductionFalHttpAudit _audit;
    readonly IProductionCredentialResolver _credentials;

    public HttpFalLipsyncTransport(
        ContentFalClient client,
        IProductionPaidNetworkPermit permit,
        IProductionExecutionPermitStore officialPermits,
        ProductionFalHttpAudit audit,
        IProductionCredentialResolver credentials)
    {
        _client = client;
        _permit = permit;
        _officialPermits = officialPermits;
        _audit = audit;
        _credentials = credentials;
    }

    public bool RequiresNetwork => true;
    public int StartCount { get; private set; }
    public string? LastProviderJobId { get; set; }

    public async Task<FalLipsyncTransportResult> ExecuteOnceAsync(
        FalLipsyncTransportRequest request, CancellationToken ct = default)
    {
        var scope = request.Scope ?? throw new InvalidOperationException(ProductionFailureReasons.UnknownScope);
        var shot = request.ShotCode ?? scope.ShotCode;
        if (ProductionPaidNetworkGate.TransportUsesExecutionPermit(scope.BuildId, scope.EpisodeCode))
        {
            var latest = await _officialPermits.GetLatestAsync(
                scope.BuildId, shot, ProductionActions.CreateLipsync,
                ProductionLipsyncCatalog.FalProviderId, ProductionLipsyncCatalog.SyncLipsyncV3, ct);
            if (latest is null
                || !ProductionExecutionPermitCanon.MatchesConsumed(
                    latest, scope.BuildId, shot, ProductionActions.CreateLipsync,
                    ProductionLipsyncCatalog.FalProviderId, ProductionLipsyncCatalog.SyncLipsyncV3))
                throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
        }
        else if (!_permit.TryConsumeStart(scope, shot, ProductionLipsyncCatalog.FalProviderId, ProductionLipsyncCatalog.SyncLipsyncV3))
            throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);

        if (request.Credential is null)
            throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
        var material = await ProductionCredentialResolver.RequireAsync(
            _credentials, request.Credential, ProductionLipsyncCatalog.FalProviderId, ProductionLipsyncCatalog.SyncLipsyncV3, ct);
        var authorizedKey = material.Secret;
        if (string.IsNullOrWhiteSpace(authorizedKey))
            throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
        if (request.VideoBytes is not { Length: >= 32 } || request.VoiceBytes is not { Length: >= 32 })
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        if (!ProductionLipsyncSpeakerTarget.TryAcceptTransportPin(scope, request.Pin, out var pin))
            throw new InvalidOperationException(ProductionFailureReasons.LipsyncTargetUnresolved);
        int[]? box = request.FaceBoxXyxy;
        var syncMode = request.SyncMode;
        if (ProductionEp01Sh02LipsyncCanon.MatchesScope(scope))
        {
            if (!ProductionEp01Sh02LipsyncCanon.MatchesFaceBox(box))
                throw new InvalidOperationException(ProductionFailureReasons.LipsyncTargetUnresolved);
            syncMode = ProductionEp01Sh02LipsyncCanon.SyncMode;
        }

        StartCount++;
        _audit.StartCount++;
        var executed = await _client.ExecuteProductionLipsyncOnceAsync(
            request.VideoBytes,
            request.VoiceBytes,
            pin.X01,
            pin.Y01,
            pin.Width,
            pin.Height,
            pin.FrameNumber,
            (double)(request.VideoDurationSec > 0 ? request.VideoDurationSec : 5),
            ct,
            syncMode,
            box,
            authorizedKey);
        if (executed.Bytes is null || executed.Bytes.Length < 32)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        if (string.IsNullOrWhiteSpace(executed.ProviderJobId)
            || executed.ProviderJobId.StartsWith(FakeFalLipsyncTransport.JobPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.ProviderUnavailable);
        LastProviderJobId = executed.ProviderJobId;
        _audit.LastProviderJobId = executed.ProviderJobId;
        return new FalLipsyncTransportResult(executed.ProviderJobId, executed.Bytes, executed.MimeType);
    }

    public Task<FalLipsyncTransportResult> RecoverOnceAsync(
        string providerJobId, CancellationToken ct = default)
    {
        _ = providerJobId;
        _ = ct;
        throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
    }

    public async Task<FalLipsyncTransportResult> RecoverOnceAsync(
        string providerJobId, ProductionCredentialReference credential, CancellationToken ct = default)
    {
        var material = await ProductionCredentialResolver.RequireAsync(
            _credentials, credential, FamixaProviderIds.Fal, ProductionLipsyncCatalog.SyncLipsyncV3, ct);
        return await DownloadExistingAsync(providerJobId, material.Secret, ct);
    }

    async Task<FalLipsyncTransportResult> DownloadExistingAsync(
        string providerJobId, string? authorizedKey, CancellationToken ct)
    {
        var recovered = await _client.RecoverProductionLipsyncOutputAsync(providerJobId, ct, authorizedKey);
        if (recovered.Bytes is not { Length: >= 32 })
            throw new InvalidOperationException(recovered.Error ?? ProductionFailureReasons.RecoveryFailed);
        if (string.IsNullOrWhiteSpace(recovered.ProviderJobId)
            || recovered.ProviderJobId.StartsWith(FakeFalLipsyncTransport.JobPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.RecoveryFailed);
        LastProviderJobId = recovered.ProviderJobId;
        _audit.LastProviderJobId = recovered.ProviderJobId;
        return new FalLipsyncTransportResult(recovered.ProviderJobId, recovered.Bytes, recovered.MimeType);
    }
}
