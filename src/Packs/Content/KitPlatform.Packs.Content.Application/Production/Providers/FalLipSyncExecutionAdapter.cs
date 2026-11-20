namespace KitPlatform.Packs.Content.Production;

/// <summary>Production OS Fal lipsync adapter. Vendor HTTP lives behind IFalLipsyncTransport.</summary>
public sealed class FalLipSyncExecutionAdapter : ILipSyncExecutionAdapter
{
    readonly IFalLipsyncTransport _transport;

    public FalLipSyncExecutionAdapter(IFalLipsyncTransport transport) => _transport = transport;

    public string ProviderId => ProductionLipsyncCatalog.FalProviderId;
    public string ModelId => ProductionLipsyncCatalog.SyncLipsyncV3;
    public bool IsPaid => true;
    public bool RequiresNetwork => _transport.RequiresNetwork;

    public Task<ProductionMediaResult> ExecuteFrozenAsync(
        ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
        => ExecuteFrozenAsync(snapshot, null, ct);

    public async Task<ProductionMediaResult> ExecuteFrozenAsync(
        ProductionTaskInputSnapshot snapshot, LipsyncStartContext? context, CancellationToken ct)
    {
        if (!string.Equals(snapshot.ProviderId, ProviderId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(snapshot.ModelId, ModelId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);
        if (!ProductionLipsyncFreeze.TryParsePin(snapshot.ShotContract, out var pin)
            && !(ProductionLipsyncFreeze.TryParse(snapshot.ShotContract, out var lineId, out var speaker, out _, out _)
                && ProductionLipsyncSpeakerTarget.TryResolveFrozen(lineId, speaker, out pin)))
            throw new InvalidOperationException(ProductionFailureReasons.LipsyncTargetUnresolved);
        if (snapshot.ParentArtifactIds is null || snapshot.ParentArtifactIds.Count < 2)
            throw new InvalidOperationException(ProductionFailureReasons.MissingDependency);
        if (context?.VideoBytes is not { Length: >= 32 } || context.VoiceBytes is not { Length: >= 32 })
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);

        int[]? box = null;
        string? syncMode = null;
        if (context.Scope is not null && ProductionEp01Sh02LipsyncCanon.MatchesScope(context.Scope))
        {
            if (!ProductionLipsyncFreeze.TryParseBox(snapshot.ShotContract, out var parsed)
                || !ProductionEp01Sh02LipsyncCanon.MatchesFaceBox(parsed))
                throw new InvalidOperationException(ProductionFailureReasons.LipsyncTargetUnresolved);
            box = parsed;
            syncMode = ProductionEp01Sh02LipsyncCanon.SyncMode;
        }
        else if (context.Scope is not null
            && ProductionScopeAnchorRules.IsTenantOwnedEp01Build(context.Scope.BuildId, context.Scope.EpisodeCode)
            && ProductionLipsyncFreeze.TryParseBox(snapshot.ShotContract, out var tenantBox))
        {
            box = tenantBox;
            syncMode = ProductionEp01Sh02LipsyncCanon.SyncMode;
        }

        var executed = await _transport.ExecuteOnceAsync(new FalLipsyncTransportRequest(
            snapshot.TaskId,
            context.AttemptId,
            context.VideoBytes,
            context.VoiceBytes,
            pin,
            snapshot.DurationSec,
            snapshot.SnapshotHash,
            context.Scope,
            context.ShotCode ?? snapshot.TaskId.ToString("N"),
            box,
            syncMode,
            ProductionFrozenCredential.FromSnapshot(snapshot)), ct);
        if (string.IsNullOrWhiteSpace(executed.ProviderJobId)
            || executed.ProviderJobId.StartsWith(FakeFalLipsyncTransport.JobPrefix, StringComparison.Ordinal)
                && _transport.RequiresNetwork)
            throw new InvalidOperationException(ProductionFailureReasons.ProviderUnavailable);
        if (executed.Bytes is null || executed.Bytes.Length < 32)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);

        var duration = snapshot.DurationSec > 0 ? snapshot.DurationSec : 5;
        if (ProductionTestMp4.TryReadDuration(executed.Bytes, out var measured) && measured > 0)
            duration = measured;

        return new ProductionMediaResult(
            executed.ProviderJobId,
            executed.Bytes,
            string.IsNullOrWhiteSpace(executed.MimeType) ? ProductionTestMp4.Mime : executed.MimeType,
            duration);
    }

    public async Task<ProductionMediaResult> RecoverFrozenAsync(
        ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
    {
        if (!string.Equals(snapshot.ProviderId, ProviderId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(snapshot.ModelId, ModelId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);
        if (!ProductionLipsyncFreeze.TryParsePin(snapshot.ShotContract, out _)
            && !(ProductionLipsyncFreeze.TryParse(snapshot.ShotContract, out var lineId, out var speaker, out _, out _)
                && ProductionLipsyncSpeakerTarget.TryResolveFrozen(lineId, speaker, out _)))
            throw new InvalidOperationException(ProductionFailureReasons.LipsyncTargetUnresolved);
        if (snapshot.ParentArtifactIds is null || snapshot.ParentArtifactIds.Count < 2)
            throw new InvalidOperationException(ProductionFailureReasons.MissingDependency);
        if (string.IsNullOrWhiteSpace(providerJobId))
            throw new InvalidOperationException(ProductionFailureReasons.RecoveryFailed);

        var reference = ProductionFrozenCredential.FromSnapshot(snapshot);
        FalLipsyncTransportResult executed;
        if (_transport.RequiresNetwork)
        {
            if (reference is null)
                throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
            executed = await _transport.RecoverOnceAsync(providerJobId.Trim(), reference, ct);
        }
        else
            executed = await _transport.RecoverOnceAsync(providerJobId.Trim(), ct);
        if (string.IsNullOrWhiteSpace(executed.ProviderJobId)
            || executed.ProviderJobId.StartsWith(FakeFalLipsyncTransport.JobPrefix, StringComparison.Ordinal)
                && _transport.RequiresNetwork)
            throw new InvalidOperationException(ProductionFailureReasons.RecoveryFailed);
        if (executed.Bytes is null || executed.Bytes.Length < 32)
            throw new InvalidOperationException(ProductionFailureReasons.RecoveryFailed);

        var duration = snapshot.DurationSec > 0 ? snapshot.DurationSec : 5;
        if (ProductionTestMp4.TryReadDuration(executed.Bytes, out var measured) && measured > 0)
            duration = measured;

        return new ProductionMediaResult(
            executed.ProviderJobId,
            executed.Bytes,
            string.IsNullOrWhiteSpace(executed.MimeType) ? ProductionTestMp4.Mime : executed.MimeType,
            duration);
    }
}
