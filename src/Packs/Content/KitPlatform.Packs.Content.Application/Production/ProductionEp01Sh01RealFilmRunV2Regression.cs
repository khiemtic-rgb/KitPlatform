using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionEp01Sh01RealFilmRunV2Regression
{
    public const string SuiteId = "FAMIXA_REAL_FILM_RUN_V2";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }
        try { RunAsync(Ok).GetAwaiter().GetResult(); }
        catch (Exception ex) { fail.Add("UNCAUGHT:" + ex.GetType().Name + ":" + ex.Message); }
        return fail;
    }

    static async Task RunAsync(Action<bool, string> Ok)
    {
        var blocked = Harness();
        blocked.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        var shot = ProductionEp01Sh01IngressCanon.Scope();
        await SeedReadyOfficialAsync(blocked, shot);
        Ok(await Rejects(() => blocked.Svc.CreateVideoAsync(Command(shot, blocked.KfId, blocked.Snap, "run-v2-blocked", blocked.Credential)),
            PerformanceContractIds.OfficialFilmForbidden), "official film without permit remains forbidden");

        var h = Harness();
        h.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        h.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp99);
        await SeedReadyOfficialAsync(h, shot);
        var permit = await h.Authority.IssuePermitAsync(new IssueProductionExecutionPermitCommand(
            shot.BuildId, shot.ShotCode, shot.EpisodeCode, shot.SceneCode, ProductionActions.CreateVideo,
            ProductionMotionCatalog.RunwayProviderId, ProductionMotionCatalog.RunwayGen4Turbo, "run-v2", "one-shot"));
        Ok(permit.PermitId != Guid.Empty && permit.Status == ProductionExecutionPermitStatuses.Issued, "permit ISSUED before Start");

        var ready = await h.Svc.EvaluateCreateVideoAsync(shot);
        Ok(ready.Available
            && ready.DurationSec == 5.0m
            && ready.KeyframeArtifactId == h.KfId
            && ready.PerformanceSnapshotId == h.Snap.PerformanceSnapshotId
            && ready.ProviderId == "runway"
            && ready.ModelId == "gen4_turbo", "CREATE_VIDEO ready before Start");
        Ok((await h.Authority.GetIssuedPermitAsync(
            shot.BuildId, shot.ShotCode, ProductionActions.CreateVideo, "runway", "gen4_turbo"))
            !.Status == ProductionExecutionPermitStatuses.Issued, "evaluate does not consume");

        Ok(await RejectsPrefix(() => h.Svc.CreateVideoAsync(Command(shot, h.KfId, h.Snap, "run-v2-sh01", h.Credential)),
            ProductionFailureReasons.RunwayPromptOverCap), "joined SH01 prompt over 1000 characters is refused");
        Ok(h.Transport.CreateCalls == 0, "refused SH01 prompt does not call Runway");

        var kf = await h.Store.GetArtifactAsync(h.KfId);
        Ok(kf is not null && kf.ByteSha256 == h.KfSha, "keyframe artifact unchanged");

        var sh02 = new ProductionScope(shot.BuildId, "EP01", "SC01", "EP01-SC01-SH02");
        var ep99 = new ProductionScope(ProductionScopeAnchorRules.OfficialEp99, "EP99", "SC01", "EP99-SC01-SH01");
        Ok((await h.Svc.GetShotStateAsync(sh02)).VideoArtifactId is null, "cross-shot protection");
        Ok((await h.Svc.GetShotStateAsync(ep99)).VideoArtifactId is null, "EP99 unchanged");

        var net = Harness(paidNetwork: true);
        net.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        await SeedReadyOfficialAsync(net, shot);
        await net.Authority.IssuePermitAsync(new IssueProductionExecutionPermitCommand(
            shot.BuildId, shot.ShotCode, shot.EpisodeCode, shot.SceneCode, ProductionActions.CreateVideo,
            "runway", "gen4_turbo", "run-v2", "network"));
        Ok(await Rejects(() => net.Svc.CreateVideoAsync(Command(shot, net.KfId, net.Snap, "run-v2-net", net.Credential)),
            ProductionFailureReasons.PaidNetworkForbidden), "paid official film requires AuthorizePaidNetwork");
        Ok(await RejectsPrefix(() => net.Svc.CreateVideoAsync(Command(shot, net.KfId, net.Snap, "run-v2-net", net.Credential, authorize: true)),
            ProductionFailureReasons.RunwayPromptOverCap), "authorized joined SH01 prompt over 1000 characters is refused");
        Ok(net.Transport.CreateCalls == 0, "authorized refusal does not call Runway");
    }

    static async Task SeedReadyOfficialAsync(HarnessBundle h, ProductionScope shot)
    {
        var pixels = Encoding.ASCII.GetBytes("EP01-SH01-RUN-V2-PIXELS-" + new string('R', 48));
        var art = await h.Svc.RegisterKeyframeAsync(new RegisterKeyframeCommand(
            shot, pixels, ProductionEp01Sh01IngressCanon.Mime, ProductionEp01Sh01IngressCanon.Story, "run-v2",
            ProductionEp01Sh01IngressCanon.ActingBeat, ProductionEp01Sh01IngressCanon.VisualContract, "",
            0, ProductionEp01Sh01IngressCanon.PictureRevisionId, ProductionEp01Sh01IngressCanon.ProvenanceJson()));
        await h.Authority.UpsertSelectionAsync(new UpsertProductionAiSelectionCommand(
            shot.BuildId, shot.ShotCode, shot.EpisodeCode, shot.SceneCode, ProductionAiSelectionStages.Video,
            ProductionMotionCatalog.RunwayProviderId, ProductionMotionCatalog.RunwayGen4Turbo, "run-v2", "locked"));
        var facts = await h.Svc.EvaluateCreateVideoAsync(shot);
        var snap = await h.Authority.MaterializeDirectorTimingAsync(new MaterializeDirectorTimingCommand(
            shot, ProductionDirectorTiming.Ep01Sh01DurationSec, facts.Story, facts.ActingBeat, facts.VisualContract,
            art.ArtifactId, art.ByteSha256, "director"));
        h.KfId = art.ArtifactId;
        h.KfSha = art.ByteSha256;
        h.Snap = snap;
    }

    static CreateVideoApiCommand Command(
        ProductionScope scope, Guid kf, PerformanceSnapshot snap, string idem,
        ProductionCredentialReference credential,
        bool authorize = false, Guid? preparedAttemptId = null,
        ProductionTaskInputSnapshot? replayFrom = null)
    {
        var input = new ShotExecutionInput(
            replayFrom?.Story ?? ProductionEp01Sh01IngressCanon.Story,
            replayFrom?.ActingBeat ?? ProductionEp01Sh01IngressCanon.ActingBeat,
            replayFrom?.VisualContract ?? ProductionEp01Sh01IngressCanon.VisualContract,
            "", replayFrom?.KeyframeHash ?? "", "5.0s", "", "",
            replayFrom?.KeyframeArtifactId ?? kf,
            replayFrom?.KeyframeHash ?? "",
            "16:9",
            replayFrom?.DurationSec ?? 5.0m,
            ShotExecutionCompiler.Version, [kf],
            PerformanceSnapshotId: replayFrom?.PerformanceSnapshotId ?? snap.PerformanceSnapshotId,
            PerformanceHash: replayFrom?.PerformanceHash ?? snap.PerformanceHash);
        return new(scope, input, "regression", idem, null, null, "runway", "gen4_turbo", false, authorize, false, preparedAttemptId, credential);
    }

    static async Task<bool> Rejects(Func<Task> act, string reason)
    {
        try { await act(); return false; }
        catch (InvalidOperationException ex) { return ex.Message == reason; }
    }

    static async Task<bool> RejectsPrefix(Func<Task> act, string prefix)
    {
        try { await act(); return false; }
        catch (InvalidOperationException ex) { return ex.Message.StartsWith(prefix, StringComparison.Ordinal); }
    }

    static HarnessBundle Harness(bool paidNetwork = false)
    {
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var transport = paidNetwork ? new NetworkFakeRunwayTransport() : new FakeRunwayTransport();
        var fixture = new FixtureMotionAdapter();
        var runway = new RunwayMotionExecutionAdapter(transport);
        var resolver = new ProductionMotionAdapterResolver([fixture, runway]);
        var scopes = new InMemoryProductionScopeFoundationStore();
        var selections = new InMemoryProductionAiSelectionStore();
        var permits = new InMemoryProductionExecutionPermitStore();
        var snaps = new InMemoryPerformanceSnapshotStore();
        var perf = new PerformanceContractService(snaps);
        var pin = ProductionOfficialHarnessCredentialFixture.Runway();
        var os = new ProductionExecutionService(store, fixture, scopes, snaps, selections, permits, pin.Resolver);
        var svc = new ProductionCreateVideoService(
            os, store, files, fixture, scopes, resolver, officialPermits: permits);
        var authority = new ProductionReadinessAuthority(selections, permits, perf, snaps);
        return new HarnessBundle(svc, store, files, transport, scopes, selections, permits, snaps, authority, pin.Reference);
    }

    sealed class HarnessBundle(
        ProductionCreateVideoService Svc,
        InMemoryProductionWorkStore Store,
        InMemoryProductionArtifactFileStore Files,
        FakeRunwayTransport Transport,
        InMemoryProductionScopeFoundationStore Scopes,
        InMemoryProductionAiSelectionStore Selections,
        InMemoryProductionExecutionPermitStore Permits,
        InMemoryPerformanceSnapshotStore Snaps,
        ProductionReadinessAuthority Authority,
        ProductionCredentialReference Credential)
    {
        public ProductionCreateVideoService Svc { get; } = Svc;
        public InMemoryProductionWorkStore Store { get; } = Store;
        public InMemoryProductionArtifactFileStore Files { get; } = Files;
        public FakeRunwayTransport Transport { get; } = Transport;
        public InMemoryProductionScopeFoundationStore Scopes { get; } = Scopes;
        public InMemoryProductionAiSelectionStore Selections { get; } = Selections;
        public InMemoryProductionExecutionPermitStore Permits { get; } = Permits;
        public InMemoryPerformanceSnapshotStore Snaps { get; } = Snaps;
        public ProductionReadinessAuthority Authority { get; } = Authority;
        public ProductionCredentialReference Credential { get; } = Credential;
        public Guid KfId { get; set; }
        public string KfSha { get; set; } = "";
        public PerformanceSnapshot Snap { get; set; } = null!;
    }

    sealed class NetworkFakeRunwayTransport : FakeRunwayTransport
    {
        public override bool RequiresNetwork => true;
    }
}
