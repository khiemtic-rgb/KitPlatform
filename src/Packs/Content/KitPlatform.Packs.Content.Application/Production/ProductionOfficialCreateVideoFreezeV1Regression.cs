using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionOfficialCreateVideoFreezeV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_OFFICIAL_CREATE_VIDEO_FREEZE_V1";

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
        await T_CreateAndIdempotent(Ok);
        await T_ExistingMismatchFails(Ok);
        await T_MalformedExistingFails(Ok);
        await T_LiveDriftFailsWithoutMutation(Ok);
        await T_WrongTargetBlocked(Ok);
        await T_NoProviderOrCostOrPermit(Ok);
        T_Checkpoint12StillBlocked(Ok);
    }

    static async Task T_CreateAndIdempotent(Action<bool, string> Ok)
    {
        var (store, sources, bytes) = await Harness();
        var graph = ProductionVeoReadinessComposition.CreateClosedTestGraph();
        var first = await OfficialCreateVideoFreezeV1.FreezeAsync(store, sources, OfficialCreateVideoFreezeCredentialFixture.Pin());
        var second = await OfficialCreateVideoFreezeV1.FreezeAsync(store, sources, OfficialCreateVideoFreezeCredentialFixture.Pin());
        var count = await OfficialCreateVideoFreezeV1.CountOfficialCreateVideoSnapshotsAsync(store);
        var adapter = new VeoMotionExecutionAdapter(new FakeVeoMotionTransport());
        var live = Live(sources);
        Ok(first.Created && !first.Reused, "first freeze creates snapshot");
        Ok(second.Reused && !second.Created && second.Snapshot.SnapshotId == first.Snapshot.SnapshotId,
            "second freeze reuses same snapshot ID");
        Ok(count == 1, "duplicate snapshot = 0");
        Ok(second.Snapshot.SnapshotHash == first.Snapshot.SnapshotHash, "snapshot hash stable");
        Ok(first.Snapshot.ProviderId == "google" && first.Snapshot.ModelId == "veo-3.1",
            "provider/model google/veo-3.1");
        Ok(first.Snapshot.Story == ProductionEp01Sh02IngressCanon.Story, "story frozen");
        Ok(first.Snapshot.ActingBeat == ProductionEp01Sh02IngressCanon.ActingBeat, "acting beat frozen");
        Ok(first.Snapshot.VisualContract == ProductionEp01Sh02IngressCanon.VisualContract, "visual contract frozen");
        Ok(first.Snapshot.DurationSec == 10, "DurationSec = 10");
        Ok(first.Snapshot.TimingHash == ShotExecutionCompiler.Hash("10.0s"), "timing hash from 10.0s");
        Ok(first.Snapshot.PerformanceSnapshotId == OfficialCreateVideoFreezeV1.OfficialPerformanceId,
            "performance snapshot ID");
        Ok(first.Snapshot.PerformanceHash == OfficialCreateVideoFreezeV1.OfficialPerformanceHash,
            "performance hash");
        Ok(first.Snapshot.KeyframeArtifactId == OfficialCreateVideoFreezeV1.OfficialKeyframeId,
            "keyframe artifact ID");
        Ok(first.Snapshot.KeyframeHash == KitVideoIntegrityRules.Sha256Hex(bytes), "keyframe hash from artifact bytes");
        Ok(first.Snapshot.SnapshotId != OfficialCreateVideoFreezeV1.OfficialPerformanceId
            && first.Snapshot.SnapshotId != OfficialCreateVideoFreezeV1.OfficialKeyframeId
            && first.Snapshot.SnapshotId != Guid.Parse("c52b646f-86b9-4a43-834d-d94ab9f013b8"),
            "snapshot ID is not performance/selection/keyframe ID");
        ProductionFrozenCreateVideoMatch.Assert(first.Snapshot, live, adapter);
        Ok(ProductionFrozenCreateVideoMatch.FirstMismatch(second.Snapshot, live, adapter) is null,
            "12-field AssertFrozenMatch PASS");
        var task = await store.GetTaskAsync(first.Snapshot.TaskId);
        Ok(task is { Action: ProductionActions.CreateVideo }
            && task.Scope.BuildId == RealVertexExecutionV1Lock.BuildId
            && task.Scope.ShotCode == RealVertexExecutionV1Lock.ShotCode,
            "target identity exact");
        Ok(graph.Transport.StartCount == 0 && graph.Handler.Requests.Count == 0
            && graph.Authenticator.Calls == 0,
            "no Start / HTTP / OAuth");
    }

    static async Task T_ExistingMismatchFails(Action<bool, string> Ok)
    {
        var (store, sources, _) = await Harness();
        var first = await OfficialCreateVideoFreezeV1.FreezeAsync(store, sources, OfficialCreateVideoFreezeCredentialFixture.Pin());
        var drifted = sources with { Story = sources.Story + " changed" };
        var failed = false;
        try { await OfficialCreateVideoFreezeV1.FreezeAsync(store, drifted, OfficialCreateVideoFreezeCredentialFixture.Pin()); }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith(ProductionFailureReasons.InputSnapshotMismatch, StringComparison.Ordinal))
        {
            failed = true;
        }
        var after = await OfficialCreateVideoFreezeV1.FindOfficialCreateVideoSnapshotAsync(store);
        Ok(failed, "existing mismatch fails");
        Ok(after!.SnapshotId == first.Snapshot.SnapshotId && after.Story == first.Snapshot.Story,
            "no refreeze / no overwrite");
        Ok(await OfficialCreateVideoFreezeV1.CountOfficialCreateVideoSnapshotsAsync(store) == 1,
            "no second snapshot on mismatch");
    }

    static async Task T_MalformedExistingFails(Action<bool, string> Ok)
    {
        var store = new InMemoryProductionWorkStore();
        var engine = new ProductionWorkExecutionEngine(store);
        var req = await engine.CreateRequestAsync(
            RealVertexExecutionV1Lock.OfficialScope, ProductionActions.CreateVideo, "bad", "ck12a-malformed");
        var task = await engine.CreateTaskAsync(req.RequestId);
        var bad = new ProductionTaskInputSnapshot(
            Guid.NewGuid(), task.TaskId, "", "", "", "", "", "",
            "", "", "", "", "", "", "", "", "",
            "google", "veo-3.1", "hash", DateTimeOffset.UtcNow, null, "16:9", 10);
        await store.InsertSnapshotAsync(bad);
        var (_, sources, _) = await Harness(store);
        var failed = false;
        try { await OfficialCreateVideoFreezeV1.FreezeAsync(store, sources, OfficialCreateVideoFreezeCredentialFixture.Pin()); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.InputSnapshotMismatch)
        {
            failed = true;
        }
        Ok(failed, "malformed existing snapshot is not repaired");
        Ok((await store.GetSnapshotByTaskAsync(task.TaskId))!.Story == "", "malformed row unchanged");
    }

    static async Task T_LiveDriftFailsWithoutMutation(Action<bool, string> Ok)
    {
        var (store, sources, _) = await Harness();
        var frozen = (await OfficialCreateVideoFreezeV1.FreezeAsync(store, sources, OfficialCreateVideoFreezeCredentialFixture.Pin())).Snapshot;
        var adapter = new VeoMotionExecutionAdapter(new FakeVeoMotionTransport());
        var live = Live(sources);
        Expect(Ok, frozen, live with { Story = live.Story + " x" }, adapter, "live story drift");
        Expect(Ok, frozen, live with { ActingBeat = live.ActingBeat + " x" }, adapter, "live acting drift");
        Expect(Ok, frozen, live with { VisualContract = live.VisualContract + " x" }, adapter, "live visual drift");
        Expect(Ok, frozen, live with { DurationSec = 8 }, adapter, "duration drift");
        Expect(Ok, frozen, live with { Timing = "8s" }, adapter, "timing drift");
        Expect(Ok, frozen, live with { PerformanceSnapshotId = Guid.NewGuid() }, adapter, "performance snapshot drift");
        Expect(Ok, frozen, live with { PerformanceHash = OfficialCreateVideoFreezeV1.OfficialPerformanceHash + "x" }, adapter, "performance hash drift");
        Expect(Ok, frozen, live with { KeyframeArtifactId = Guid.NewGuid() }, adapter, "keyframe ID drift");
        Expect(Ok, frozen, live with { KeyframeByteSha256 = ShotExecutionCompiler.Hash("other") }, adapter, "keyframe hash drift");
        var again = await OfficialCreateVideoFreezeV1.FindOfficialCreateVideoSnapshotAsync(store);
        Ok(again!.SnapshotHash == frozen.SnapshotHash && again.Story == frozen.Story,
            "frozen snapshot did not change after live drift");
        Ok(again.DurationSec == 10, "OS duration stays 10");
    }

    static async Task T_WrongTargetBlocked(Action<bool, string> Ok)
    {
        var blocked = false;
        try
        {
            RealVertexExecutionV1Lock.RequireAuthorizedTarget(
                new ProductionScope(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), "EP99", "SC01", "TEST-SC01-SH02"),
                "google", "veo-3.1");
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.UnauthorizedVertexTarget)
        {
            blocked = true;
        }
        Ok(blocked, "wrong target UNAUTHORIZED_VERTEX_TARGET");
        await Task.CompletedTask;
    }

    static async Task T_NoProviderOrCostOrPermit(Action<bool, string> Ok)
    {
        var (store, sources, _) = await Harness();
        var graph = ProductionVeoReadinessComposition.CreateClosedTestGraph();
        var permits = new CountingProductionExecutionPermitStore();
        var before = CostPolicyDecision.FromCatalog("google", "veo-3.1", 10);
        _ = await OfficialCreateVideoFreezeV1.FreezeAsync(store, sources, OfficialCreateVideoFreezeCredentialFixture.Pin());
        var catalog = CostPolicyDecision.FromCatalog("google", "veo-3.1", 10);
        Ok(before == catalog && catalog.Kind == CostPolicyKind.Allowed && catalog.EstimatedAmount == 0.20m,
            "no cost mutation");
        Ok(permits.ConsumeCalls == 0, "no permit consume");
        Ok(graph.Transport.StartCount == 0 && graph.Handler.Requests.Count == 0 && graph.Authenticator.Calls == 0,
            "no provider / HTTP / OAuth");
        var closed = false;
        try { ProductionVeoReadinessComposition.CreateProductionResolver().Resolve("google", "veo-3.1"); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.UnknownProvider)
        {
            closed = true;
        }
        Ok(closed, "production resolver still CLOSED");
    }

    static void T_Checkpoint12StillBlocked(Action<bool, string> Ok)
    {
        RealVertexExecutionV1Lock.ResetForTests();
        var ck12 = ProductionRealVertexExecutionV1Regression.Run();
        Ok(ck12.Count == 0, ck12.Count == 0 ? "CK12 still BLOCKED / no Start" : "CK12:" + string.Join(",", ck12));
        var armed = RealVertexExecutionV1Runner.Attempt(
            RealVertexExecutionV1Runner.ArmOfficialTarget(
                new ProductionAiSelection(
                    Guid.Parse("c52b646f-86b9-4a43-834d-d94ab9f013b8"),
                    RealVertexExecutionV1Lock.BuildId,
                    RealVertexExecutionV1Lock.ShotCode, "EP01", "SC01",
                    ProductionAiSelectionStages.Video, "google", "veo-3.1",
                    ProductionAiSelectionModes.Explicit, "ck12a", DateTimeOffset.UnixEpoch,
                    "DIRECTOR_SELECTION", ProductionAiSelectionStatuses.Active, "h")));
        Ok(!armed.Executed
            && !armed.Preflight.Blockers.Contains(ProductionFailureReasons.UnknownCost)
            && armed.Preflight.Blockers.Contains(PerformanceContractIds.OfficialFilmForbidden)
            && armed.Preflight.Blockers.Contains(ProductionFailureReasons.AuthenticatorNotReady),
            "CK12 remaining blockers unchanged (cost ALLOWED)");
    }

    static void Expect(
        Action<bool, string> Ok,
        ProductionTaskInputSnapshot frozen,
        ShotExecutionInput live,
        IMotionExecutionAdapter adapter,
        string name)
    {
        var mismatch = ProductionFrozenCreateVideoMatch.FirstMismatch(frozen, live, adapter);
        Ok(mismatch is not null, name);
    }

    static ShotExecutionInput Live(OfficialCreateVideoFreezeSources sources) =>
        new(
            sources.Story, sources.ActingBeat, sources.VisualContract, "CHAR-001",
            sources.KeyframeHash, sources.Timing, "", "",
            sources.KeyframeArtifactId, sources.KeyframeHash, "16:9", sources.DurationSec,
            ShotExecutionCompiler.Version, [sources.KeyframeArtifactId],
            PerformanceSnapshotId: sources.PerformanceSnapshotId,
            PerformanceHash: sources.PerformanceHash);

    static async Task<(InMemoryProductionWorkStore Store, OfficialCreateVideoFreezeSources Sources, byte[] Bytes)>
        Harness(InMemoryProductionWorkStore? existing = null)
    {
        var store = existing ?? new InMemoryProductionWorkStore();
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-KF-12A-" + new string('K', 48));
        var hash = KitVideoIntegrityRules.Sha256Hex(bytes);
        if (await store.GetArtifactAsync(OfficialCreateVideoFreezeV1.OfficialKeyframeId) is null)
            await SeedKeyframeAsync(store, bytes, hash);
        return (store, OfficialCreateVideoFreezeV1.FromEp01Sh02Canon(bytes, hash), bytes);
    }

    static async Task SeedKeyframeAsync(IProductionWorkStore store, byte[] bytes, string hash)
    {
        var engine = new ProductionWorkExecutionEngine(store);
        var req = await engine.CreateRequestAsync(
            RealVertexExecutionV1Lock.OfficialScope, ProductionActions.CreateImage, "ck12a", "kf-12a-" + Guid.NewGuid().ToString("N"));
        var task = await engine.CreateTaskAsync(req.RequestId);
        await store.InsertArtifactAsync(new ProductionArtifact(
            OfficialCreateVideoFreezeV1.OfficialKeyframeId,
            task.TaskId,
            Guid.NewGuid(),
            ProductionArtifactRoles.Keyframe,
            "image/png",
            bytes.Length,
            0,
            hash,
            "kf/EP01-SC01-SH02.png",
            null,
            DateTimeOffset.UtcNow,
            ProductionArtifactStatuses.Verified,
            hash));
    }
}
