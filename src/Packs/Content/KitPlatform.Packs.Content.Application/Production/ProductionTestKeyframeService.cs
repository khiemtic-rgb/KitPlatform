using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public sealed record RegisterTestKeyframeCommand(
    Guid TestRunId,
    Guid ScopeId,
    string ShotCode,
    string FixtureId,
    string CreatedBy);

public sealed record ProductionTestKeyframeRegistration(
    string FixtureId,
    Guid ArtifactId,
    Guid ScopeId,
    string ShotCode,
    string Role,
    string Mime,
    string ByteSha256,
    long ByteLength,
    int Width,
    int Height,
    string StorageKey,
    string VerificationStatus,
    DateTimeOffset CreatedAt,
    string Source,
    Guid BindingId,
    bool Reused);

public interface IProductionTestKeyframeService
{
    Task<ProductionTestKeyframeRegistration> RegisterAsync(RegisterTestKeyframeCommand command, CancellationToken ct = default);
}

public sealed class ProductionTestKeyframeService : IProductionTestKeyframeService
{
    public const string Source = "TEST_FIXTURE";

    readonly IProductionExecutionService _os;
    readonly IProductionWorkStore _store;
    readonly IProductionArtifactFileStore _files;
    readonly IProductionScopeFoundationStore _scopes;

    public ProductionTestKeyframeService(
        IProductionExecutionService os,
        IProductionWorkStore store,
        IProductionArtifactFileStore files,
        IProductionScopeFoundationStore scopes)
    {
        _os = os;
        _store = store;
        _files = files;
        _scopes = scopes;
    }

    public async Task<ProductionTestKeyframeRegistration> RegisterAsync(RegisterTestKeyframeCommand command, CancellationToken ct = default)
    {
        var shotCode = (command.ShotCode ?? "").Trim();
        var fixtureId = (command.FixtureId ?? "").Trim();
        if (shotCode.Length == 0)
            throw new InvalidOperationException(ProductionFailureReasons.TestShotMissing);
        if (fixtureId.Length == 0)
            throw new InvalidOperationException(ProductionFailureReasons.FixtureNotFound);

        var anchor = await _scopes.GetAnchorAsync(command.ScopeId, ct);
        var kind = ProductionScopeGuard.Resolve(anchor, command.ScopeId);
        if (kind == ProductionScopeKind.Unknown)
            throw new InvalidOperationException(ProductionFailureReasons.UnknownScope);
        if (kind != ProductionScopeKind.Test)
            throw new InvalidOperationException(ProductionFailureReasons.TestScopeRequired);
        if (anchor is null || anchor.TestRunId is null || anchor.TestRunId == Guid.Empty)
            throw new InvalidOperationException(ProductionFailureReasons.UnknownScope);
        if (anchor.TestRunId.Value != command.TestRunId)
            throw new InvalidOperationException(ProductionFailureReasons.TestRunMismatch);
        if (anchor.SeriesBuildId is not null)
            throw new InvalidOperationException(ProductionFailureReasons.TestScopeRequired);

        var shot = await _scopes.GetTestShotAsync(command.ScopeId, shotCode, ct)
            ?? throw new InvalidOperationException(ProductionFailureReasons.TestShotMissing);

        var fixture = ProductionTestKeyframeCatalog.Load(fixtureId);
        var bytes = fixture.Bytes;
        var sha = KitVideoIntegrityRules.Sha256Hex(bytes);
        if (!string.Equals(sha, fixture.Manifest.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionFailureReasons.FixtureHashMismatch);

        var scope = new ProductionScope(command.ScopeId, shot.EpisodeCode, shot.SceneCode, shot.ShotCode);
        var folder = ProductionStoragePolicy.KeyframeFolder(ProductionScopeKind.Test, command.TestRunId);
        var idem = IdempotencyKey(command.ScopeId, shot.ShotCode, fixtureId, sha);

        var existing = await TryReuseAsync(scope, idem, fixture, sha, ct);
        if (existing is not null) return existing;

        var current = await _store.GetActiveBindingAsync(scope.BuildId, scope.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Keyframe), ct);
        if (current is not null)
        {
            var bound = await _store.GetArtifactAsync(current.ArtifactId, ct);
            if (bound is { VerificationStatus: ProductionArtifactStatuses.Verified }
                && string.Equals(bound.ByteSha256, sha, StringComparison.OrdinalIgnoreCase))
                return ToRegistration(bound, current, fixture, reused: true);
            if (bound is { VerificationStatus: ProductionArtifactStatuses.Verified }
                && string.Equals(bound.ByteSha256, sha, StringComparison.OrdinalIgnoreCase) == false
                && string.Equals(fixtureId, FixtureIdOf(bound), StringComparison.Ordinal))
                throw new InvalidOperationException(ProductionFailureReasons.FixtureBytesChanged);
        }

        var request = await _os.CreateRequestAsync(scope, ProductionActions.CreateImage, command.CreatedBy, idem, ct: ct);
        var reusedAfterCreate = await TryReuseAsync(scope, idem, fixture, sha, ct);
        if (reusedAfterCreate is not null) return reusedAfterCreate;

        var task = await _os.CreateTaskAsync(request.RequestId, ct);
        await FreezeFixtureSnapshotAsync(task, sha, ct);
        var attempt = await _os.StartAttemptAsync(task.TaskId, sha, ct);
        var storage = _files.Write(folder, attempt.AttemptId.ToString("N"), bytes);
        if (!storage.StartsWith(folder + "/", StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.UnknownScope);
        var readback = _files.Read(storage);
        if (readback is null
            || readback.Length != bytes.Length
            || !string.Equals(KitVideoIntegrityRules.Sha256Hex(readback), sha, StringComparison.OrdinalIgnoreCase)
            || !ProductionTestKeyframePng.HasPngSignature(readback))
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);

        await _os.SubmitProviderJobAsync(attempt.AttemptId, "import-" + sha[..12], ct);
        await _os.CompleteAttemptAsync(attempt.AttemptId, sha, ct);
        var artifact = await _os.VerifyArtifactAsync(attempt.AttemptId, readback, fixture.Manifest.Mime, 0, storage, ct);
        if (artifact.Role != ProductionArtifactRoles.Keyframe
            || artifact.VerificationStatus != ProductionArtifactStatuses.Verified
            || !string.Equals(artifact.ByteSha256, sha, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);

        var binding = await _os.BindCurrentAsync(artifact.ArtifactId, ct);
        await _store.InsertEventAsync(new ProductionTaskEvent(
            Guid.NewGuid(), ProductionEventTypes.TestFixtureRegistered, DateTimeOffset.UtcNow,
            request.RequestId, task.TaskId, attempt.AttemptId, artifact.ArtifactId, scope.ShotCode,
            ProductionActions.CreateImage, Payload(fixture, artifact, storage)), ct);
        var typed = await _store.GetActiveBindingAsync(
            scope.BuildId, scope.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Keyframe), ct)
            ?? binding;
        return ToRegistration(artifact, typed, fixture, reused: false);
    }

    async Task<ProductionTestKeyframeRegistration?> TryReuseAsync(
        ProductionScope scope, string idem, ProductionTestKeyframeFixture fixture, string sha, CancellationToken ct)
    {
        var request = await _store.GetRequestByIdempotencyAsync(idem, ct);
        if (request is null) return null;
        var tasks = await _store.ListTasksByRequestAsync(request.RequestId, ct);
        foreach (var task in tasks)
        {
            var arts = await _store.ListArtifactsByTaskAsync(task.TaskId, ct);
            var hit = arts.FirstOrDefault(a =>
                a.Role == ProductionArtifactRoles.Keyframe
                && a.VerificationStatus == ProductionArtifactStatuses.Verified
                && string.Equals(a.ByteSha256, sha, StringComparison.OrdinalIgnoreCase));
            if (hit is null) continue;
            var typed = await _store.GetActiveBindingAsync(
                scope.BuildId, scope.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Keyframe), ct);
            if (typed is null || typed.ArtifactId != hit.ArtifactId)
                typed = await _os.BindCurrentAsync(hit.ArtifactId, ct);
            return ToRegistration(hit, typed, fixture, reused: true);
        }
        return null;
    }

    async Task FreezeFixtureSnapshotAsync(ProductionTask task, string sha, CancellationToken ct)
    {
        var snap = new ProductionTaskInputSnapshot(
            Guid.NewGuid(), task.TaskId,
            "", "", "", "",
            "", "",
            ShotExecutionCompiler.Hash(""),
            ShotExecutionCompiler.Hash(""),
            ShotExecutionCompiler.Hash(""),
            ShotExecutionCompiler.Hash(""),
            sha, "", "", "",
            ShotExecutionCompiler.Hash(""),
            "import", "existing-pixels", "", DateTimeOffset.UtcNow,
            null, "16:9", 0, ShotExecutionCompiler.Version, null);
        snap = snap with { SnapshotHash = ShotExecutionCompiler.SnapshotHash(snap) };
        await _store.InsertSnapshotAsync(snap, ct);
        await _store.UpdateTaskAsync(task with
        {
            InputSnapshotId = snap.SnapshotId,
            ProviderId = "import",
            ModelId = "existing-pixels",
            Status = ProductionTaskStatuses.ProviderFrozen,
        }, ct);
        await _store.InsertEventAsync(new ProductionTaskEvent(
            Guid.NewGuid(), ProductionEventTypes.InputFrozen, DateTimeOffset.UtcNow,
            task.RequestId, task.TaskId, null, null, task.Scope.ShotCode, task.Action, null), ct);
    }

    static string IdempotencyKey(Guid scopeId, string shotCode, string fixtureId, string sha) =>
        $"TEST_FIXTURE:{scopeId:N}:{shotCode}:{fixtureId}:{sha}";

    static string FixtureIdOf(ProductionArtifact _) => "";

    static string Payload(ProductionTestKeyframeFixture fixture, ProductionArtifact artifact, string storage) =>
        JsonSerializer.Serialize(new
        {
            source = Source,
            fixtureId = fixture.Manifest.FixtureId,
            width = fixture.Manifest.Width,
            height = fixture.Manifest.Height,
            mime = fixture.Manifest.Mime,
            byteSha256 = artifact.ByteSha256,
            byteLength = artifact.SizeBytes,
            storageKey = storage,
        });

    static ProductionTestKeyframeRegistration ToRegistration(
        ProductionArtifact artifact,
        ProductionBinding binding,
        ProductionTestKeyframeFixture fixture,
        bool reused) =>
        new(
            fixture.Manifest.FixtureId,
            artifact.ArtifactId,
            binding.BuildId,
            binding.ShotCode,
            artifact.Role,
            artifact.MimeType,
            artifact.ByteSha256,
            artifact.SizeBytes,
            fixture.Manifest.Width,
            fixture.Manifest.Height,
            artifact.StorageKey,
            artifact.VerificationStatus,
            artifact.CreatedAt,
            Source,
            binding.BindingId,
            reused);
}
