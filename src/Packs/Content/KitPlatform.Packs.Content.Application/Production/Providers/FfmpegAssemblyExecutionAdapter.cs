namespace KitPlatform.Packs.Content.Production;

/// <summary>Official ASSEMBLE adapter. Reuses IFfmpegMixTransport remux. Not an AI provider.</summary>
public sealed class FfmpegAssemblyExecutionAdapter
{
    readonly IFfmpegMixTransport _transport;

    public FfmpegAssemblyExecutionAdapter(IFfmpegMixTransport transport) => _transport = transport;

    public string ProviderId => ProductionAssemblyCatalog.ServerProviderId;
    public string ModelId => ProductionAssemblyCatalog.FfmpegModelId;
    public bool IsPaid => false;
    public bool RequiresNetwork => _transport.RequiresNetwork;

    public async Task<ProductionMediaResult> ExecuteFrozenAsync(
        ProductionTaskInputSnapshot snapshot,
        AssemblyStartContext context,
        CancellationToken ct = default)
    {
        if (!string.Equals(snapshot.ProviderId, ProviderId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(snapshot.ModelId, ModelId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);
        if (!ProductionAssemblyFreeze.TryParse(snapshot.ShotContract, out _, out _, out var shots)
            || shots.Count != 1)
            throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);
        if (snapshot.ParentArtifactIds is null
            || snapshot.ParentArtifactIds.Count != 1
            || snapshot.ParentArtifactIds[0] != shots[0].ArtifactId)
            throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);
        if (context.InputBytes is not { Length: >= 32 })
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);

        var executed = await _transport.RemuxOnceAsync(new FfmpegMixTransportRequest(
            snapshot.TaskId,
            context.AttemptId,
            context.InputBytes,
            snapshot.DurationSec,
            snapshot.SnapshotHash), ct);
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

public sealed record AssemblyStartContext(Guid AttemptId, byte[] InputBytes);
