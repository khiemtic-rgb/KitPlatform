using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

public sealed record OfficialSh02AssembleLiveResult(
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
    string? FreezeToken,
    int ComposeCount,
    int RemuxCount,
    Guid? CurrentAssemblyId,
    Guid? AcceptedAssemblyId,
    Guid? FilmAssemblyId,
    Guid? FilmAssemblyShotId,
    Guid? Sh01AssemblyId,
    Guid? Sh01MixId);

/// <summary>
/// CK26 live official SH02 ASSEMBLE. Not registered in the regression catalog.
/// </summary>
public static class OfficialSh02AssembleLiveV1
{
    public static Task<OfficialSh02AssembleLiveResult> ExecuteOnceAsync(CancellationToken ct = default)
        => RunAsync(replayOnly: false, ct);

    public static Task<OfficialSh02AssembleLiveResult> ReplayIdempotencyAsync(CancellationToken ct = default)
        => RunAsync(replayOnly: true, ct);

    static async Task<OfficialSh02AssembleLiveResult> RunAsync(bool replayOnly, CancellationToken ct)
    {
        var db = new ContentLocalConnectionFactory();
        var store = new ProductionWorkRepository(db);
        var scopes = new ProductionScopeFoundationRepository(db);
        var options = Options.Create(new ContentOptions
        {
            KitVideoVideoRoot = Path.GetFullPath(
                Path.Combine("E:", "KitPlatform", "src", "KitPlatform.Api", "App_Data", "kit-video-i2v")),
        });
        var files = new ProductionArtifactFileStore(new KitVideoVideoStore(options, new LiveHostEnv()));
        var audit = new ProductionFfmpegMixAudit();
        var transport = new ProcessFfmpegMixTransport(audit);
        var officialAssembly = new FfmpegAssemblyExecutionAdapter(transport);
        var fixture = new FixtureMotionAdapter();
        var os = new ProductionExecutionService(store, fixture, scopes);
        var svc = new ProductionCreateVideoService(
            os, store, files, fixture, scopes, officialAssembly: officialAssembly);

        var shot = ProductionEp01Sh02AssembleCanon.Scope();
        var pre = await PreflightAsync(store, files, shot, ct);
        if (pre is not null)
            return Fail(pre, audit);

        var sh01 = await Sh01MembershipAsync(store, ct);
        var slice = await svc.AssembleOsAsync(new ProductionActionApiCommand(
            shot,
            new ShotExecutionInput(
                ProductionEp01Sh02IngressCanon.Story,
                ProductionEp01Sh02IngressCanon.ActingBeat,
                ProductionEp01Sh02IngressCanon.VisualContract,
                "", "", "8.0s", "", "",
                DurationSec: ProductionEp01Sh02AssembleCanon.MixDurationSec,
                ParentArtifactIds: [ProductionEp01Sh02AssembleCanon.MixArtifactId]),
            "ck26",
            null,
            AssemblyShots:
            [
                (shot.ShotCode, ProductionEp01Sh02AssembleCanon.MixArtifactId,
                    ProductionEp01Sh02AssembleCanon.MixDurationSec)
            ]), ct);

        if (ProductionMixContract.Dec(slice.Output?.DurationSec ?? 0)
            != ProductionMixContract.Dec(ProductionEp01Sh02AssembleCanon.MixDurationSec))
            return Fail(ProductionFailureReasons.DurationMismatch, audit);

        var snap = await store.GetSnapshotByTaskAsync(slice.Task.TaskId, ct);
        var current = await store.GetActiveBindingAsync(
            shot.BuildId, shot.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly), ct);
        var accepted = await store.GetActiveBindingAsync(
            shot.BuildId, shot.ShotCode, ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.Assembly), ct);
        var rows = slice.Assembly.AssemblyId == Guid.Empty
            ? []
            : await store.ListAssemblyShotsAsync(slice.Assembly.AssemblyId, ct);
        var sh02Row = rows.FirstOrDefault(r => r.ShotCode == shot.ShotCode);
        var sh01After = await Sh01MembershipAsync(store, ct);
        if (sh01After.AssemblyId != sh01.AssemblyId || sh01After.MixId != sh01.MixId)
            return Fail(ProductionFailureReasons.BindingChanged, audit);

        var remux = audit.ExecutionCount;
        var executed = !replayOnly && remux == 1;
        return new OfficialSh02AssembleLiveResult(
            executed, remux == 0, null,
            slice.Request.RequestId, slice.Task.TaskId, snap?.SnapshotId, snap?.SnapshotHash,
            slice.Attempt.AttemptId, slice.Output?.ArtifactId, slice.Output?.ByteSha256,
            slice.Output?.SizeBytes ?? 0, slice.Output?.DurationSec ?? 0,
            slice.Output?.StorageKey, slice.Output?.MimeType, snap?.ShotContract,
            audit.ComposeExecutionCount, remux,
            current?.ArtifactId, accepted?.ArtifactId,
            slice.Assembly.AssemblyId, sh02Row?.AssemblyShotId,
            sh01After.AssemblyId, sh01After.MixId);
    }

    static async Task<string?> PreflightAsync(
        IProductionWorkStore store,
        IProductionArtifactFileStore files,
        ProductionScope shot,
        CancellationToken ct)
    {
        var mix = await store.GetArtifactAsync(ProductionEp01Sh02AssembleCanon.MixArtifactId, ct);
        if (mix is null) return ProductionFailureReasons.MissingDependency;
        var locked = ProductionEp01Sh02AssembleCanon.ParentLockError(mix);
        if (locked is not null) return locked;
        var acceptedMix = await store.GetActiveBindingAsync(
            shot.BuildId, shot.ShotCode, ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.Mix), ct);
        var generic = await store.GetActiveBindingAsync(
            shot.BuildId, shot.ShotCode, ProductionBindingRoles.Accepted, ct);
        var currentMix = await store.GetActiveBindingAsync(
            shot.BuildId, shot.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix), ct);
        var visibleMix = await store.GetActiveBindingAsync(
            shot.BuildId, shot.ShotCode, ProductionBindingRoles.VisibleFor(ProductionArtifactRoles.Mix), ct);
        if (acceptedMix?.ArtifactId != mix.ArtifactId
            || generic?.ArtifactId != mix.ArtifactId
            || currentMix?.ArtifactId != mix.ArtifactId
            || visibleMix?.ArtifactId != mix.ArtifactId)
            return ProductionFailureReasons.BindingChanged;
        var video = await store.GetActiveBindingAsync(
            shot.BuildId, shot.ShotCode, OfficialVeoVideoTakeAcceptanceV1.AcceptanceRole, ct);
        var voice = await store.GetActiveBindingAsync(
            shot.BuildId, shot.ShotCode, ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.Voice), ct);
        var lipsync = await store.GetActiveBindingAsync(
            shot.BuildId, shot.ShotCode, ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.Lipsync), ct);
        if (video?.ArtifactId != ProductionEp01Sh02AssembleCanon.VideoArtifactId
            || voice?.ArtifactId != ProductionEp01Sh02AssembleCanon.VoiceArtifactId
            || lipsync?.ArtifactId != ProductionEp01Sh02AssembleCanon.LipsyncArtifactId)
            return ProductionFailureReasons.BindingChanged;
        var bytes = files.Read(mix.StorageKey);
        if (bytes is null || KitVideoIntegrityRules.Sha256Hex(bytes) != ProductionEp01Sh02AssembleCanon.MixSha256)
            return ProductionFailureReasons.HashChanged;
        return null;
    }

    static async Task<(Guid? AssemblyId, Guid? MixId)> Sh01MembershipAsync(
        IProductionWorkStore store, CancellationToken ct)
    {
        var assemblies = await store.ListAssembliesByBuildAsync(ProductionEp01Sh02IngressCanon.BuildId, ct);
        foreach (var assembly in assemblies)
        {
            if (assembly.AssemblyId != ProductionEp01Sh02AssembleCanon.HistoricalSh01AssemblyId)
                continue;
            var rows = await store.ListAssemblyShotsAsync(assembly.AssemblyId, ct);
            var sh01 = rows.FirstOrDefault(r => r.ShotCode == "EP01-SC01-SH01");
            return (assembly.AssemblyId, sh01?.ArtifactId);
        }
        return (null, null);
    }

    static OfficialSh02AssembleLiveResult Fail(string reason, ProductionFfmpegMixAudit audit) =>
        new(false, false, reason, null, null, null, null, null, null, null, 0, 0, null, null, null,
            audit.ComposeExecutionCount, audit.ExecutionCount, null, null, null, null, null, null);

    sealed class LiveHostEnv : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "ck26";
        public string ContentRootPath { get; set; } =
            Path.GetFullPath(Path.Combine("E:", "KitPlatform", "src", "KitPlatform.Api"));
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
