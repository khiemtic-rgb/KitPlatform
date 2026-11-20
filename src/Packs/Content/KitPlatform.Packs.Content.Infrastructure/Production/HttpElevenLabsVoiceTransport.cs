using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>Real ElevenLabs HTTP. Start is one-shot and requires a consumed official CREATE_VOICE permit.</summary>
internal sealed class HttpElevenLabsVoiceTransport : IElevenLabsVoiceTransport
{
    readonly ContentElevenLabsClient _client;
    readonly IProductionPaidNetworkPermit _permit;
    readonly IProductionExecutionPermitStore _officialPermits;
    readonly ProductionElevenLabsHttpAudit _audit;
    readonly IProductionCredentialResolver _credentials;

    public HttpElevenLabsVoiceTransport(
        ContentElevenLabsClient client,
        IProductionPaidNetworkPermit permit,
        IProductionExecutionPermitStore officialPermits,
        ProductionElevenLabsHttpAudit audit,
        IProductionCredentialResolver credentials)
    {
        _client = client;
        _permit = permit;
        _officialPermits = officialPermits;
        _audit = audit;
        _credentials = credentials;
    }

    public bool RequiresNetwork => true;
    public int SynthesizeCount { get; private set; }
    public string? LastProviderJobId { get; private set; }

    public async Task<ElevenLabsVoiceTransportResult> SynthesizeOnceAsync(
        ElevenLabsVoiceTransportRequest request, CancellationToken ct = default)
    {
        var scope = request.Scope ?? throw new InvalidOperationException(ProductionFailureReasons.UnknownScope);
        var shot = request.ShotCode ?? scope.ShotCode;
        if (ProductionPaidNetworkGate.TransportUsesExecutionPermit(scope.BuildId, scope.EpisodeCode))
        {
            var latest = await _officialPermits.GetLatestAsync(
                scope.BuildId, shot, ProductionActions.CreateVoice,
                ProductionVoiceCatalog.ElevenLabsProviderId, request.ModelId, ct);
            if (latest is null
                || !ProductionExecutionPermitCanon.MatchesConsumed(
                    latest, scope.BuildId, shot, ProductionActions.CreateVoice,
                    ProductionVoiceCatalog.ElevenLabsProviderId, request.ModelId))
                throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
        }
        else if (!_permit.TryConsumeStart(scope, shot, ProductionVoiceCatalog.ElevenLabsProviderId, request.ModelId))
            throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);

        if (string.IsNullOrWhiteSpace(request.VoiceId)
            || ProductionVoiceFreeze.IsTestIdentity(request.VoiceId)
            || string.IsNullOrWhiteSpace(request.Text))
            throw new InvalidOperationException(ProductionFailureReasons.CanonicalVoiceInputNotBound);
        if (!string.Equals(request.ModelId, ProductionVoiceCatalog.ElevenV3, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);

        var material = await ProductionCredentialResolver.RequireAsync(
            _credentials, request.Credential, ProductionVoiceCatalog.ElevenLabsProviderId, request.ModelId, ct);
        SynthesizeCount++;
        _audit.StartCount++;
        var synthesized = await _client.SynthesizeProductionOnceAsync(
            request.Text, request.VoiceId, request.ModelId, ct, material.Secret);
        if (synthesized.Bytes is null || synthesized.Bytes.Length < 32)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        if (string.IsNullOrWhiteSpace(synthesized.ProviderJobId)
            || synthesized.ProviderJobId.StartsWith(FakeElevenLabsVoiceTransport.JobPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.ProviderUnavailable);
        LastProviderJobId = synthesized.ProviderJobId;
        _audit.LastProviderJobId = synthesized.ProviderJobId;
        return new ElevenLabsVoiceTransportResult(synthesized.ProviderJobId, synthesized.Bytes, synthesized.MimeType);
    }
}
