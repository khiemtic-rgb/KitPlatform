using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public sealed record OfficialCreateVideoFreezeSources(
    string Story,
    string ActingBeat,
    string VisualContract,
    decimal DurationSec,
    string Timing,
    Guid PerformanceSnapshotId,
    string PerformanceHash,
    Guid KeyframeArtifactId,
    string KeyframeHash,
    byte[] KeyframeBytes);

public sealed record OfficialCreateVideoFreezeResult(
    ProductionTaskInputSnapshot Snapshot,
    bool Created,
    bool Reused);

/// <summary>
/// Official CREATE_VIDEO freeze only. Does not Start, consume permit, or invent cost.
/// </summary>
public static class OfficialCreateVideoFreezeV1
{
    public const string IdempotencyPrefix = "official-create-video-freeze-v1";
    public static readonly Guid OfficialKeyframeId = Guid.Parse("91daaf4b-5550-4cbc-b9f9-0bd0be556cef");
    public static readonly Guid OfficialPerformanceId = Guid.Parse("6505b8a2-8a71-4d74-8d93-f1fae8319b6b");
    public const string OfficialPerformanceHash =
        "PERFORMANCE_HASH_V1:8fa1d71b14e46e64bf1cf4dc7b107ae7f39be69f3f79da1d55d24f33a116914a";
    public const decimal OfficialDurationSec = 10;
    public const string OfficialTiming = "10.0s";

    public static string IdempotencyKey() =>
        $"{IdempotencyPrefix}:{RealVertexExecutionV1Lock.BuildId:N}:{RealVertexExecutionV1Lock.ShotCode}:{RealVertexExecutionV1Lock.ProviderId}:{RealVertexExecutionV1Lock.ModelId}";

    public static OfficialCreateVideoFreezeSources FromEp01Sh02Canon(byte[] keyframeBytes, string keyframeHash) =>
        new(
            ProductionEp01Sh02IngressCanon.Story,
            ProductionEp01Sh02IngressCanon.ActingBeat,
            ProductionEp01Sh02IngressCanon.VisualContract,
            OfficialDurationSec,
            OfficialTiming,
            OfficialPerformanceId,
            OfficialPerformanceHash,
            OfficialKeyframeId,
            keyframeHash,
            keyframeBytes);

    public static Task<OfficialCreateVideoFreezeResult> FreezeAsync(
        IProductionWorkStore store,
        OfficialCreateVideoFreezeSources sources,
        CancellationToken ct = default)
        => FreezeAsync(store, sources, null, null, ct);

    public static Task<OfficialCreateVideoFreezeResult> FreezeAsync(
        IProductionWorkStore store,
        OfficialCreateVideoFreezeSources sources,
        OfficialCreateVideoFreezeCredentialPin pin,
        CancellationToken ct = default)
        => FreezeAsync(store, sources, pin.Resolver, pin.Reference, ct);

    public static async Task<OfficialCreateVideoFreezeResult> FreezeAsync(
        IProductionWorkStore store,
        OfficialCreateVideoFreezeSources sources,
        IProductionCredentialResolver? credentials,
        ProductionCredentialReference? credential,
        CancellationToken ct = default)
    {
        RealVertexExecutionV1Lock.RequireAuthorizedTarget(
            RealVertexExecutionV1Lock.OfficialScope,
            RealVertexExecutionV1Lock.ProviderId,
            RealVertexExecutionV1Lock.ModelId);
        var pin = await RequirePinAsync(credentials, credential, ct);
        await ValidateSourcesAsync(sources, store, ct);

        var existing = await FindOfficialCreateVideoSnapshotAsync(store, ct);
        var live = LiveOf(sources);
        var adapter = new VeoMotionExecutionAdapter(new FakeVeoMotionTransport());
        if (existing is not null)
        {
            if (!SamePin(existing, pin))
                throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);
            ProductionFrozenCreateVideoMatch.Assert(existing, live, adapter);
            return new OfficialCreateVideoFreezeResult(existing, Created: false, Reused: true);
        }

        return await store.InTransactionAsync(async inner =>
        {
            var raced = await FindOfficialCreateVideoSnapshotAsync(store, inner);
            if (raced is not null)
            {
                if (!SamePin(raced, pin))
                    throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);
                ProductionFrozenCreateVideoMatch.Assert(raced, live, adapter);
                return new OfficialCreateVideoFreezeResult(raced, Created: false, Reused: true);
            }

            var engine = new ProductionWorkExecutionEngine(store);
            var request = await engine.CreateRequestAsync(
                RealVertexExecutionV1Lock.OfficialScope,
                ProductionActions.CreateVideo,
                "checkpoint-12a",
                IdempotencyKey(),
                ct: inner);
            var task = (await store.ListTasksByRequestAsync(request.RequestId, inner)).LastOrDefault()
                ?? await engine.CreateTaskAsync(request.RequestId, inner);
            var already = await store.GetSnapshotByTaskAsync(task.TaskId, inner);
            if (already is not null)
            {
                if (!SamePin(already, pin))
                    throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);
                ProductionFrozenCreateVideoMatch.Assert(already, live, adapter);
                return new OfficialCreateVideoFreezeResult(already, Created: false, Reused: true);
            }

            var compiled = ShotExecutionCompiler.Compile(live);
            var snap = new ProductionTaskInputSnapshot(
                Guid.NewGuid(),
                task.TaskId,
                compiled.Story,
                compiled.ActingBeat,
                compiled.VisualContract,
                live.MotionRequirements,
                compiled.CompilerOutput,
                compiled.CompilerOutput,
                ShotExecutionCompiler.Hash(compiled.Story),
                ShotExecutionCompiler.Hash(compiled.ActingBeat),
                ShotExecutionCompiler.Hash(compiled.VisualContract),
                ShotExecutionCompiler.Hash(compiled.CompilerOutput),
                sources.KeyframeHash,
                "",
                "",
                "",
                ShotExecutionCompiler.Hash(live.Timing),
                RealVertexExecutionV1Lock.ProviderId,
                RealVertexExecutionV1Lock.ModelId,
                "",
                DateTimeOffset.UtcNow,
                sources.KeyframeArtifactId,
                live.AspectRatio,
                OfficialDurationSec,
                live.CompilerVersion,
                live.KeyframeArtifactId is { } kf ? [kf] : null,
                sources.PerformanceSnapshotId,
                sources.PerformanceHash,
                null,
                pin.CredentialId,
                pin.CredentialVersionId);
            snap = snap with { SnapshotHash = ShotExecutionCompiler.SnapshotHash(snap) };
            await store.InsertSnapshotAsync(snap, inner);
            await store.UpdateTaskAsync(task with
            {
                InputSnapshotId = snap.SnapshotId,
                ProviderId = snap.ProviderId,
                ModelId = snap.ModelId,
                Status = ProductionTaskStatuses.ProviderFrozen,
            }, inner);
            ProductionFrozenCreateVideoMatch.Assert(snap, live, adapter);
            return new OfficialCreateVideoFreezeResult(snap, Created: true, Reused: false);
        }, ct);
    }

    static async Task<ProductionCredentialReference> RequirePinAsync(
        IProductionCredentialResolver? credentials,
        ProductionCredentialReference? credential,
        CancellationToken ct)
    {
        if (credentials is null || credential is null
            || credential.CredentialId == Guid.Empty
            || credential.CredentialVersionId == Guid.Empty)
            throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
        var material = await credentials.ResolveAsync(
            credential.CredentialId,
            credential.CredentialVersionId,
            RealVertexExecutionV1Lock.ProviderId,
            RealVertexExecutionV1Lock.ModelId,
            ct);
        if (material.CredentialId != credential.CredentialId
            || material.CredentialVersionId != credential.CredentialVersionId)
            throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
        return new ProductionCredentialReference(material.CredentialId, material.CredentialVersionId);
    }

    static bool SamePin(ProductionTaskInputSnapshot snap, ProductionCredentialReference pin) =>
        IsComplete(snap)
        && snap.CredentialId == pin.CredentialId
        && snap.CredentialVersionId == pin.CredentialVersionId;

    public static async Task<ProductionTaskInputSnapshot?> FindOfficialCreateVideoSnapshotAsync(
        IProductionWorkStore store, CancellationToken ct = default)
    {
        var tasks = await store.ListTasksByScopeAsync(
            RealVertexExecutionV1Lock.BuildId, RealVertexExecutionV1Lock.ShotCode, ct);
        foreach (var task in tasks.Where(t => t.Action == ProductionActions.CreateVideo))
        {
            var snap = await store.GetSnapshotByTaskAsync(task.TaskId, ct);
            if (snap is not null) return snap;
        }
        return null;
    }

    public static async Task<int> CountOfficialCreateVideoSnapshotsAsync(
        IProductionWorkStore store, CancellationToken ct = default)
    {
        var tasks = await store.ListTasksByScopeAsync(
            RealVertexExecutionV1Lock.BuildId, RealVertexExecutionV1Lock.ShotCode, ct);
        var n = 0;
        foreach (var task in tasks.Where(t => t.Action == ProductionActions.CreateVideo))
        {
            if (await store.GetSnapshotByTaskAsync(task.TaskId, ct) is not null)
                n++;
        }
        return n;
    }

    static async Task ValidateSourcesAsync(
        OfficialCreateVideoFreezeSources sources, IProductionWorkStore store, CancellationToken ct)
    {
        if (sources.DurationSec != OfficialDurationSec)
            throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);
        if (sources.PerformanceSnapshotId != OfficialPerformanceId
            || !string.Equals(sources.PerformanceHash, OfficialPerformanceHash, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);
        if (sources.KeyframeArtifactId != OfficialKeyframeId)
            throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);
        if (sources.KeyframeBytes is null || sources.KeyframeBytes.Length < 32)
            throw new InvalidOperationException(ProductionFailureReasons.KeyframeBytesMissing);
        var hash = KitVideoIntegrityRules.Sha256Hex(sources.KeyframeBytes);
        if (!string.Equals(hash, sources.KeyframeHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);
        var art = await store.GetArtifactAsync(OfficialKeyframeId, ct);
        if (art is not null
            && !string.Equals(art.ByteSha256, hash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);
    }

    static bool IsComplete(ProductionTaskInputSnapshot snap) =>
        snap.DurationSec == OfficialDurationSec
        && string.Equals(snap.ProviderId, RealVertexExecutionV1Lock.ProviderId, StringComparison.Ordinal)
        && string.Equals(snap.ModelId, RealVertexExecutionV1Lock.ModelId, StringComparison.Ordinal)
        && CreateVideoActionContractGate.IsPresent(snap.Story)
        && CreateVideoActionContractGate.IsPresent(snap.ActingBeat)
        && CreateVideoActionContractGate.IsPresent(snap.VisualContract)
        && snap.PerformanceSnapshotId == OfficialPerformanceId
        && string.Equals(snap.PerformanceHash, OfficialPerformanceHash, StringComparison.Ordinal)
        && snap.KeyframeArtifactId == OfficialKeyframeId
        && CreateVideoActionContractGate.IsPresent(snap.KeyframeHash)
        && CreateVideoActionContractGate.IsPresent(snap.SnapshotHash)
        && CreateVideoActionContractGate.IsPresent(snap.TimingHash)
        && snap.CredentialId is Guid credentialId && credentialId != Guid.Empty
        && snap.CredentialVersionId is Guid credentialVersionId && credentialVersionId != Guid.Empty;

    static ShotExecutionInput LiveOf(OfficialCreateVideoFreezeSources sources) =>
        new(
            sources.Story,
            sources.ActingBeat,
            sources.VisualContract,
            "CHAR-001",
            sources.KeyframeHash,
            sources.Timing,
            "",
            "",
            sources.KeyframeArtifactId,
            sources.KeyframeHash,
            "16:9",
            sources.DurationSec,
            ShotExecutionCompiler.Version,
            [sources.KeyframeArtifactId],
            PerformanceSnapshotId: sources.PerformanceSnapshotId,
            PerformanceHash: sources.PerformanceHash);
}
