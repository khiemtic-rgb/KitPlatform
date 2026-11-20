using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

/// <summary>Production OS server MIX adapter. FFmpeg lives behind IFfmpegMixTransport. Not an AI provider.</summary>
public sealed class FfmpegMixExecutionAdapter : IMixExecutionAdapter
{
    readonly IFfmpegMixTransport _transport;

    public FfmpegMixExecutionAdapter(IFfmpegMixTransport transport) => _transport = transport;

    public string ProviderId => ProductionMixCatalog.ServerProviderId;
    public string ModelId => ProductionMixCatalog.FfmpegModelId;
    public bool IsPaid => false;
    public bool RequiresNetwork => _transport.RequiresNetwork;

    public Task<ProductionMediaResult> ExecuteFrozenAsync(
        ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
        => ExecuteFrozenAsync(snapshot, null, ct);

    public async Task<ProductionMediaResult> ExecuteFrozenAsync(
        ProductionTaskInputSnapshot snapshot, MixStartContext? context, CancellationToken ct)
    {
        if (!string.Equals(snapshot.ProviderId, ProviderId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(snapshot.ModelId, ModelId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);
        if (snapshot.ParentArtifactIds is null || snapshot.ParentArtifactIds.Count < 1)
            throw new InvalidOperationException(ProductionFailureReasons.MissingLipsyncArtifact);

        if (ProductionMixFreeze.TryParseV1(snapshot.ShotContract, out var contract))
            return await ExecuteComposeAsync(snapshot, contract, context, ct);

        if (context?.LipsyncBytes is not { Length: >= 32 })
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        if (!ProductionMixFreeze.TryParse(snapshot.ShotContract, out _, out _))
            throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);

        var remuxed = await _transport.RemuxOnceAsync(new FfmpegMixTransportRequest(
            snapshot.TaskId,
            context.AttemptId,
            context.LipsyncBytes,
            snapshot.DurationSec,
            snapshot.SnapshotHash), ct);
        return RequireDecodable(remuxed);
    }

    async Task<ProductionMediaResult> ExecuteComposeAsync(
        ProductionTaskInputSnapshot snapshot,
        ProductionMixContractV1 contract,
        MixStartContext? context,
        CancellationToken ct)
    {
        if (string.Equals(contract.CompositionMode, ProductionMixContract.VoiceOverStill, StringComparison.Ordinal))
            return await ExecuteVoiceOverStillAsync(snapshot, contract, context, ct);
        var err = ProductionMixContract.Validate(contract);
        if (err is not null)
            throw new InvalidOperationException(err);
        if (snapshot.DurationSec != contract.OutputDurationSec)
            throw new InvalidOperationException(ProductionFailureReasons.DurationMismatch);
        var needsLipsync = contract.Parents.Any(p => p.Role == ProductionArtifactRoles.Lipsync);
        if (context?.VideoTakeBytes is not { Length: >= 32 }
            || (needsLipsync && context.LipsyncBytes is not { Length: >= 32 }))
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);

        var parentBytes = new List<byte[]>();
        foreach (var parent in contract.Parents)
        {
            var bytes = parent.Role == ProductionArtifactRoles.VideoTake
                ? context.VideoTakeBytes
                : parent.Role == ProductionArtifactRoles.Lipsync
                    ? context.LipsyncBytes
                    : null;
            if (bytes is null)
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
            if (ProductionMixContract.NormSha(KitVideoIntegrityRules.Sha256Hex(bytes))
                != ProductionMixContract.NormSha(parent.Sha256))
                throw new InvalidOperationException(ProductionFailureReasons.HashChanged);
            if (!snapshot.ParentArtifactIds!.Contains(parent.ArtifactId))
                throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);
            parentBytes.Add(bytes);
        }

        var executed = await _transport.ComposeSegmentsOnceAsync(new FfmpegMixComposeRequest(
            snapshot.TaskId,
            context.AttemptId,
            contract,
            parentBytes,
            snapshot.SnapshotHash), ct);
        var result = RequireDecodable(executed);
        if (ProductionMixContract.Dec(result.DurationSec) != ProductionMixContract.Dec(contract.OutputDurationSec))
            throw new InvalidOperationException(ProductionFailureReasons.DurationMismatch);
        return result;
    }

    async Task<ProductionMediaResult> ExecuteVoiceOverStillAsync(
        ProductionTaskInputSnapshot snapshot,
        ProductionMixContractV1 contract,
        MixStartContext? context,
        CancellationToken ct)
    {
        var err = ProductionMixContract.Validate(contract);
        if (err is not null)
            throw new InvalidOperationException(err);
        if (snapshot.DurationSec != contract.OutputDurationSec)
            throw new InvalidOperationException(ProductionFailureReasons.DurationMismatch);
        if (context?.KeyframeBytes is not { Length: >= 32 } || context.VoiceBytes is not { Length: >= 32 })
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        var parentBytes = new List<byte[]>();
        foreach (var parent in contract.Parents)
        {
            var bytes = parent.Role == ProductionArtifactRoles.Keyframe
                ? context.KeyframeBytes
                : parent.Role == ProductionArtifactRoles.Voice
                    ? context.VoiceBytes
                    : null;
            if (bytes is null)
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
            if (ProductionMixContract.NormSha(KitVideoIntegrityRules.Sha256Hex(bytes))
                != ProductionMixContract.NormSha(parent.Sha256))
                throw new InvalidOperationException(ProductionFailureReasons.HashChanged);
            if (!snapshot.ParentArtifactIds!.Contains(parent.ArtifactId))
                throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);
            parentBytes.Add(bytes);
        }
        var executed = await _transport.ComposeSegmentsOnceAsync(new FfmpegMixComposeRequest(
            snapshot.TaskId,
            context.AttemptId,
            contract,
            parentBytes,
            snapshot.SnapshotHash), ct);
        var result = RequireDecodable(executed);
        if (ProductionMixContract.Dec(result.DurationSec) != ProductionMixContract.Dec(contract.OutputDurationSec))
            throw new InvalidOperationException(ProductionFailureReasons.DurationMismatch);
        return result;
    }

    static ProductionMediaResult RequireDecodable(FfmpegMixTransportResult executed)
    {
        if (string.IsNullOrWhiteSpace(executed.ProviderJobId) || executed.Bytes is not { Length: >= 32 })
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        if (!ProductionTestMp4.IsDecodable(executed.Bytes, out _)
            || !ProductionTestMp4.TryReadDuration(executed.Bytes, out var duration)
            || duration <= 0)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        return new ProductionMediaResult(
            executed.ProviderJobId,
            executed.Bytes,
            string.IsNullOrWhiteSpace(executed.MimeType) ? ProductionTestMp4.Mime : executed.MimeType,
            duration);
    }
}
