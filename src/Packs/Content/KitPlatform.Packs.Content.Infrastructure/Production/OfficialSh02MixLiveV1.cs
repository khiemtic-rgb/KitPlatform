using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

public sealed record OfficialSh02MixLiveResult(
    bool Executed,
    bool Replayed,
    string? BlockReason,
    Guid? RequestId,
    Guid? TaskId,
    Guid? SnapshotId,
    string? SnapshotHash,
    Guid? AttemptId,
    Guid? ArtifactId,
    string? Sha256,
    long SizeBytes,
    decimal DurationSec,
    string? StorageKey,
    string? MimeType,
    string? ContractHash,
    string? FreezeToken,
    int ComposeCount,
    int RemuxCount,
    Guid? CurrentMixId,
    Guid? AcceptedMixId);

/// <summary>
/// CK24D live official SH02 MIX. Not registered in the regression catalog.
/// </summary>
public static class OfficialSh02MixLiveV1
{
    public static async Task<OfficialSh02MixLiveResult> ExecuteOnceAsync(CancellationToken ct = default)
    {
        var db = new ContentLocalConnectionFactory();
        var store = new ProductionWorkRepository(db);
        var scopes = new ProductionScopeFoundationRepository(db);
        var permits = new ProductionExecutionPermitRepository(db);
        var options = Options.Create(new ContentOptions
        {
            KitVideoVideoRoot = Path.GetFullPath(
                Path.Combine("E:", "KitPlatform", "src", "KitPlatform.Api", "App_Data", "kit-video-i2v")),
        });
        var files = new ProductionArtifactFileStore(new KitVideoVideoStore(options, new LiveHostEnv()));
        var audit = new ProductionFfmpegMixAudit();
        var transport = new ProcessFfmpegMixTransport(audit);
        var officialMix = new FfmpegMixExecutionAdapter(transport);
        var fixture = new FixtureMotionAdapter();
        var os = new ProductionExecutionService(store, fixture, scopes);
        var svc = new ProductionCreateVideoService(
            os, store, files, fixture, scopes,
            officialPermits: permits, officialMix: officialMix);

        var shot = ProductionEp01Sh02MixCanon.Scope();
        var pre = await PreflightAsync(store, files, shot, ct);
        if (pre is not null)
            return Fail(pre, audit);

        var existing = await store.GetActiveBindingAsync(
            shot.BuildId, shot.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix), ct);
        if (existing is not null)
        {
            var art = await store.GetArtifactAsync(existing.ArtifactId, ct);
            var accepted = await store.GetActiveBindingAsync(
                shot.BuildId, shot.ShotCode, ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.Mix), ct);
            return new OfficialSh02MixLiveResult(
                false, true, null, null, art?.TaskId, null, null, art?.AttemptId,
                art?.ArtifactId, art?.ByteSha256, art?.SizeBytes ?? 0, art?.DurationSec ?? 0,
                art?.StorageKey, art?.MimeType, null, null,
                audit.ComposeExecutionCount, audit.ExecutionCount,
                existing.ArtifactId, accepted?.ArtifactId);
        }

        var issued = await permits.GetIssuedAsync(
            shot.BuildId, shot.ShotCode, ProductionActions.MixVideo,
            ProductionMixCatalog.ServerProviderId, ProductionMixCatalog.FfmpegModelId, ct);
        if (issued is null)
        {
            await permits.IssueAsync(new IssueProductionExecutionPermitCommand(
                shot.BuildId, shot.ShotCode, shot.EpisodeCode, shot.SceneCode,
                ProductionActions.MixVideo,
                ProductionMixCatalog.ServerProviderId,
                ProductionMixCatalog.FfmpegModelId,
                "ck24d",
                ProductionEp01Sh02MixCanon.PermitReason("pending")), ct);
        }

        var slice = await svc.MixVideoAsync(new ProductionActionApiCommand(
            shot,
            new ShotExecutionInput(
                ProductionEp01Sh02IngressCanon.Story,
                ProductionEp01Sh02IngressCanon.ActingBeat,
                ProductionEp01Sh02IngressCanon.VisualContract,
                "", "", "", "", "",
                DurationSec: ProductionEp01Sh02MixCanon.VideoDurationSec,
                LipsyncArtifactId: ProductionEp01Sh02MixCanon.LipsyncArtifactId),
            "ck24d",
            null), ct);

        var snap = await store.GetSnapshotByTaskAsync(slice.Task.TaskId, ct);
        ProductionMixFreeze.TryParseV1(snap?.ShotContract, out var contract);

        var acceptedSlice = await svc.AcceptOsAsync(new ProductionActionApiCommand(
            shot,
            new ShotExecutionInput(
                ProductionEp01Sh02IngressCanon.Story,
                ProductionEp01Sh02IngressCanon.ActingBeat,
                ProductionEp01Sh02IngressCanon.VisualContract,
                "", "", "", "", "",
                AcceptArtifactId: slice.Artifact.ArtifactId),
            "ck24d",
            "ck24d-accept-mix-" + slice.Artifact.ArtifactId.ToString("N")), ct);

        var current = await store.GetActiveBindingAsync(
            shot.BuildId, shot.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix), ct);
        var acceptedBind = await store.GetActiveBindingAsync(
            shot.BuildId, shot.ShotCode, ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.Mix), ct);

        _ = acceptedSlice;
        return new OfficialSh02MixLiveResult(
            true, false, null,
            slice.Request.RequestId, slice.Task.TaskId, snap?.SnapshotId, snap?.SnapshotHash,
            slice.Attempt.AttemptId, slice.Artifact.ArtifactId, slice.Artifact.ByteSha256,
            slice.Artifact.SizeBytes, slice.Artifact.DurationSec, slice.Artifact.StorageKey,
            slice.Artifact.MimeType, contract.ContractHash, snap?.ShotContract,
            audit.ComposeExecutionCount, audit.ExecutionCount,
            current?.ArtifactId, acceptedBind?.ArtifactId);
    }

    /// <summary>
    /// Replay the same CREATE_MIX request through MixVideo idempotency. Must not start FFmpeg.
    /// </summary>
    public static async Task<OfficialSh02MixLiveResult> ReplayIdempotencyAsync(CancellationToken ct = default)
    {
        var db = new ContentLocalConnectionFactory();
        var store = new ProductionWorkRepository(db);
        var scopes = new ProductionScopeFoundationRepository(db);
        var permits = new ProductionExecutionPermitRepository(db);
        var options = Options.Create(new ContentOptions
        {
            KitVideoVideoRoot = Path.GetFullPath(
                Path.Combine("E:", "KitPlatform", "src", "KitPlatform.Api", "App_Data", "kit-video-i2v")),
        });
        var files = new ProductionArtifactFileStore(new KitVideoVideoStore(options, new LiveHostEnv()));
        var audit = new ProductionFfmpegMixAudit();
        var transport = new ProcessFfmpegMixTransport(audit);
        var officialMix = new FfmpegMixExecutionAdapter(transport);
        var fixture = new FixtureMotionAdapter();
        var os = new ProductionExecutionService(store, fixture, scopes);
        var svc = new ProductionCreateVideoService(
            os, store, files, fixture, scopes,
            officialPermits: permits, officialMix: officialMix);

        var shot = ProductionEp01Sh02MixCanon.Scope();
        var pre = await PreflightAsync(store, files, shot, ct);
        if (pre is not null)
            return Fail(pre, audit);

        var slice = await svc.MixVideoAsync(new ProductionActionApiCommand(
            shot,
            new ShotExecutionInput(
                ProductionEp01Sh02IngressCanon.Story,
                ProductionEp01Sh02IngressCanon.ActingBeat,
                ProductionEp01Sh02IngressCanon.VisualContract,
                "", "", "", "", "",
                DurationSec: ProductionEp01Sh02MixCanon.VideoDurationSec,
                LipsyncArtifactId: ProductionEp01Sh02MixCanon.LipsyncArtifactId),
            "ck24d",
            null), ct);

        var current = await store.GetActiveBindingAsync(
            shot.BuildId, shot.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix), ct);
        var acceptedBind = await store.GetActiveBindingAsync(
            shot.BuildId, shot.ShotCode, ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.Mix), ct);
        return new OfficialSh02MixLiveResult(
            false, true, null,
            slice.Request.RequestId, slice.Task.TaskId, null, null,
            slice.Attempt.AttemptId, slice.Artifact.ArtifactId, slice.Artifact.ByteSha256,
            slice.Artifact.SizeBytes, slice.Artifact.DurationSec, slice.Artifact.StorageKey,
            slice.Artifact.MimeType, null, null,
            audit.ComposeExecutionCount, audit.ExecutionCount,
            current?.ArtifactId, acceptedBind?.ArtifactId);
    }

    static async Task<string?> PreflightAsync(
        IProductionWorkStore store,
        IProductionArtifactFileStore files,
        ProductionScope shot,
        CancellationToken ct)
    {
        var video = await store.GetArtifactAsync(ProductionEp01Sh02MixCanon.VideoArtifactId, ct);
        var lipsync = await store.GetArtifactAsync(ProductionEp01Sh02MixCanon.LipsyncArtifactId, ct);
        if (video is null) return ProductionFailureReasons.MissingVideoArtifact;
        if (lipsync is null) return ProductionFailureReasons.MissingLipsyncArtifact;
        var lockErr = ProductionEp01Sh02MixCanon.ParentLockError(video, lipsync);
        if (lockErr is not null) return lockErr;
        if (video.VerificationStatus != ProductionArtifactStatuses.Verified
            || lipsync.VerificationStatus != ProductionArtifactStatuses.Verified)
            return ProductionFailureReasons.FailedVerification;
        var acceptedVideo = await store.GetActiveBindingAsync(
            shot.BuildId, shot.ShotCode, OfficialVeoVideoTakeAcceptanceV1.AcceptanceRole, ct);
        if (acceptedVideo?.ArtifactId != video.ArtifactId)
            return ProductionFailureReasons.BindingChanged;
        var currentLip = await store.GetActiveBindingAsync(
            shot.BuildId, shot.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync), ct);
        if (currentLip?.ArtifactId != lipsync.ArtifactId
            || currentLip.ArtifactId == ProductionEp01Sh02MixCanon.HistoricalLipsyncArtifactId)
            return ProductionFailureReasons.BindingChanged;
        var videoBytes = files.Read(video.StorageKey);
        var lipBytes = files.Read(lipsync.StorageKey);
        if (videoBytes is null || KitVideoIntegrityRules.Sha256Hex(videoBytes) != video.ByteSha256)
            return ProductionFailureReasons.FailedIdentity;
        if (lipBytes is null || KitVideoIntegrityRules.Sha256Hex(lipBytes) != lipsync.ByteSha256)
            return ProductionFailureReasons.FailedIdentity;
        return null;
    }

    static OfficialSh02MixLiveResult Fail(string reason, ProductionFfmpegMixAudit audit) =>
        new(false, false, reason, null, null, null, null, null, null, null, 0, 0, null, null, null, null,
            audit.ComposeExecutionCount, audit.ExecutionCount, null, null);

    sealed class LiveHostEnv : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "ck24d";
        public string ContentRootPath { get; set; } =
            Path.GetFullPath(Path.Combine("E:", "KitPlatform", "src", "KitPlatform.Api"));
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
