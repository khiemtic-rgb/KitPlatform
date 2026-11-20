using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

/// <summary>Official EPISODE ASSEMBLE adapter. CONCAT_COPY only. Never RemuxOnceAsync.</summary>
public sealed class FfmpegEpisodeAssemblyExecutionAdapter
{
    readonly IFfmpegMixTransport _transport;

    public FfmpegEpisodeAssemblyExecutionAdapter(IFfmpegMixTransport transport) => _transport = transport;

    public string ProviderId => ProductionAssemblyCatalog.ServerProviderId;
    public string ModelId => ProductionAssemblyCatalog.FfmpegModelId;
    public bool IsPaid => false;
    public bool RequiresNetwork => _transport.RequiresNetwork;

    public async Task<ProductionMediaResult> ExecuteFrozenAsync(
        ProductionTaskInputSnapshot snapshot,
        EpisodeAssemblyStartContext context,
        CancellationToken ct = default)
    {
        if (!string.Equals(snapshot.ProviderId, ProviderId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(snapshot.ModelId, ModelId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);
        if (!ProductionEpisodeAssemblyContract.TryParse(snapshot.ShotContract, out var contract))
            throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);
        if (snapshot.ParentArtifactIds is null
            || snapshot.ParentArtifactIds.Count != contract.OrderedInputs.Count
            || !snapshot.ParentArtifactIds.SequenceEqual(contract.OrderedInputs.Select(r => r.ArtifactId)))
            throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);
        if (context.ParentBytes.Count != contract.OrderedInputs.Count)
            throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);
        if (ProductionMixContract.Dec(snapshot.DurationSec)
            != ProductionMixContract.Dec(contract.OutputDurationSec))
            throw new InvalidOperationException(ProductionFailureReasons.DurationMismatch);

        var executed = await _transport.ConcatCopyOnceAsync(new FfmpegEpisodeConcatRequest(
            snapshot.TaskId,
            context.AttemptId,
            contract,
            context.ParentBytes,
            snapshot.SnapshotHash), ct);
        if (string.IsNullOrWhiteSpace(executed.ProviderJobId) || executed.Bytes is not { Length: >= 32 })
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        if (!ProductionTestMp4.IsDecodable(executed.Bytes, out _)
            || !ProductionTestMp4.TryReadDuration(executed.Bytes, out var duration)
            || duration <= 0)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        if (ProductionMixContract.Dec(duration) != ProductionMixContract.Dec(contract.OutputDurationSec))
            throw new InvalidOperationException(ProductionFailureReasons.DurationMismatch);

        return new ProductionMediaResult(
            executed.ProviderJobId,
            executed.Bytes,
            string.IsNullOrWhiteSpace(executed.MimeType) ? ProductionTestMp4.Mime : executed.MimeType,
            duration);
    }
}

public sealed record EpisodeAssemblyStartContext(Guid AttemptId, IReadOnlyList<byte[]> ParentBytes);
