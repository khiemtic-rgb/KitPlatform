namespace KitPlatform.Packs.Content.Production;

public sealed record ProductionMediaResult(
    string ProviderJobId,
    byte[] Bytes,
    string MimeType,
    decimal DurationSec);

public interface IImageExecutionAdapter
{
    string ProviderId { get; }
    string ModelId { get; }
    bool IsPaid { get; }
    bool RequiresNetwork { get; }
    Task<ProductionMediaResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, byte[]? importBytes, string? importMime, CancellationToken ct = default);
}

public interface IVoiceExecutionAdapter
{
    string ProviderId { get; }
    string ModelId { get; }
    bool IsPaid { get; }
    bool RequiresNetwork { get; }
    Task<ProductionMediaResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default);
}

public interface ILipSyncExecutionAdapter
{
    string ProviderId { get; }
    string ModelId { get; }
    bool IsPaid { get; }
    bool RequiresNetwork { get; }
    Task<ProductionMediaResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default);
}

public interface IMixExecutionAdapter
{
    string ProviderId { get; }
    string ModelId { get; }
    bool IsPaid { get; }
    bool RequiresNetwork { get; }
    Task<ProductionMediaResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default);
}

public interface IAssemblyExecutionAdapter
{
    string ProviderId { get; }
    string ModelId { get; }
    bool IsPaid { get; }
    bool RequiresNetwork { get; }
    Task<string> PlanFrozenAsync(ProductionTaskInputSnapshot snapshot, IReadOnlyList<Guid> acceptedArtifactIds, CancellationToken ct = default);
}

public sealed class ImportImageAdapter : IImageExecutionAdapter
{
    public string ProviderId => "import";
    public string ModelId => "existing-pixels";
    public bool IsPaid => false;
    public bool RequiresNetwork => false;

    public Task<ProductionMediaResult> ExecuteFrozenAsync(
        ProductionTaskInputSnapshot snapshot, byte[]? importBytes, string? importMime, CancellationToken ct = default)
    {
        _ = snapshot;
        _ = ct;
        if (importBytes is null || importBytes.Length < 32)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        return Task.FromResult(new ProductionMediaResult(
            "import-" + snapshot.SnapshotHash[..12],
            importBytes,
            string.IsNullOrWhiteSpace(importMime) ? "image/png" : importMime,
            0));
    }
}

public sealed class FixtureImageAdapter : IImageExecutionAdapter
{
    public string ProviderId => "fixture";
    public string ModelId => "fixture-image-v1";
    public bool IsPaid => false;
    public bool RequiresNetwork => false;

    public Task<ProductionMediaResult> ExecuteFrozenAsync(
        ProductionTaskInputSnapshot snapshot, byte[]? importBytes, string? importMime, CancellationToken ct = default)
    {
        _ = ct;
        if (importBytes is { Length: >= 32 })
            return new ImportImageAdapter().ExecuteFrozenAsync(snapshot, importBytes, importMime, ct);
        var bytes = ProductionTestKeyframePng.EncodeSolidRgb(160, 90, 40, 80, 120);
        return Task.FromResult(new ProductionMediaResult(
            "fixture-image-" + snapshot.TaskId.ToString("N")[..12],
            bytes,
            "image/png",
            0));
    }
}

public sealed class FixtureVoiceAdapter : IVoiceExecutionAdapter
{
    public string ProviderId => "fixture";
    public string ModelId => "fixture-voice-v1";
    public bool IsPaid => false;
    public bool RequiresNetwork => false;

    public Task<ProductionMediaResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
    {
        _ = ct;
        var duration = snapshot.DurationSec > 0 ? snapshot.DurationSec : 1;
        var bytes = ProductionTestWav.Encode(snapshot.SnapshotHash + snapshot.VoiceHash, duration);
        return Task.FromResult(new ProductionMediaResult(
            "fixture-voice-" + snapshot.TaskId.ToString("N")[..12],
            bytes,
            ProductionTestWav.Mime,
            duration));
    }
}

public sealed class FixtureLipSyncAdapter : ILipSyncExecutionAdapter
{
    public string ProviderId => "fixture";
    public string ModelId => "fixture-lipsync-v1";
    public bool IsPaid => false;
    public bool RequiresNetwork => false;

    public Task<ProductionMediaResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
    {
        _ = ct;
        if (snapshot.ParentArtifactIds is null || snapshot.ParentArtifactIds.Count < 2)
            throw new InvalidOperationException(ProductionFailureReasons.MissingDependency);
        if (string.IsNullOrWhiteSpace(snapshot.MotionHash) || string.IsNullOrWhiteSpace(snapshot.VoiceHash))
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        var duration = snapshot.DurationSec > 0 ? snapshot.DurationSec : 5;
        var seed = snapshot.SnapshotHash + snapshot.MotionHash + snapshot.VoiceHash
            + string.Join("", snapshot.ParentArtifactIds.Select(id => id.ToString("N")));
        var bytes = ProductionTestMp4.Encode(seed, duration);
        return Task.FromResult(new ProductionMediaResult(
            "fixture-lipsync-" + snapshot.TaskId.ToString("N")[..12],
            bytes,
            ProductionTestMp4.Mime,
            duration));
    }
}

public sealed class FixtureMixAdapter : IMixExecutionAdapter
{
    public string ProviderId => "fixture";
    public string ModelId => "fixture-mix-v1";
    public bool IsPaid => false;
    public bool RequiresNetwork => false;

    public Task<ProductionMediaResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
    {
        _ = ct;
        if (snapshot.ParentArtifactIds is null || snapshot.ParentArtifactIds.Count < 1)
            throw new InvalidOperationException(ProductionFailureReasons.MissingDependency);
        if (string.IsNullOrWhiteSpace(snapshot.MotionHash))
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        var duration = snapshot.DurationSec > 0 ? snapshot.DurationSec : 5;
        var seed = "mix|" + snapshot.SnapshotHash + snapshot.MotionHash + snapshot.ParentArtifactIds[0].ToString("N");
        var bytes = ProductionTestMp4.Encode(seed, duration);
        return Task.FromResult(new ProductionMediaResult(
            "fixture-mix-" + snapshot.TaskId.ToString("N")[..12],
            bytes,
            ProductionTestMp4.Mime,
            duration));
    }
}

public sealed class FixtureAssemblyAdapter : IAssemblyExecutionAdapter
{
    public string ProviderId => "fixture";
    public string ModelId => "fixture-assembly-v1";
    public bool IsPaid => false;
    public bool RequiresNetwork => false;

    public Task<string> PlanFrozenAsync(
        ProductionTaskInputSnapshot snapshot, IReadOnlyList<Guid> acceptedArtifactIds, CancellationToken ct = default)
    {
        _ = snapshot;
        _ = ct;
        if (acceptedArtifactIds is null || acceptedArtifactIds.Count == 0)
            throw new InvalidOperationException(ProductionFailureReasons.MissingDependency);
        foreach (var id in acceptedArtifactIds)
            ProductionIdentityRules.ForbidUrlIdentity(id.ToString());
        return Task.FromResult("fixture-assembly-" + snapshot.TaskId.ToString("N")[..12]);
    }
}
