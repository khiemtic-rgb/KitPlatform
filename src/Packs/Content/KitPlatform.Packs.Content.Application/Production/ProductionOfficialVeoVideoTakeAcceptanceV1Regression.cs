using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionOfficialVeoVideoTakeAcceptanceV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_OFFICIAL_VEO_VIDEO_TAKE_ACCEPTANCE_V1";
    const string OperationName = "projects/ck16-compose-project/locations/us-central1/operations/op-ck20";

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
        await T_AcceptAndIdempotent(Ok);
        await T_MissingArtifact(Ok);
        await T_HashMismatch(Ok);
        await T_LengthMismatch(Ok);
        await T_MimeMismatch(Ok);
        await T_RoleMismatch(Ok);
        await T_TaskMismatch(Ok);
        await T_AttemptMismatch(Ok);
        await T_SnapshotMismatch(Ok);
        await T_ProviderMismatch(Ok);
        await T_ModelMismatch(Ok);
        await T_OperationMismatch(Ok);
        await T_MissingOperation(Ok);
        await T_AmbiguousTake(Ok);
        await T_NoTake(Ok);
        T_StartForbidden(Ok);
        T_PollForbidden(Ok);
        await T_PermitMutationForbidden(Ok);
        await T_SnapshotSelectionUnchanged(Ok);
        T_Ck19StillGreen(Ok);
    }

    static async Task T_AcceptAndIdempotent(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var first = await OfficialVeoVideoTakeAcceptanceV1.AcceptAsync(ready.Request);
        Ok(first.Accepted && first.Take!.ArtifactId == ready.Artifact.ArtifactId, "accept exact VIDEO_TAKE");
        Ok(first.StartCount == 0 && first.PollCount == 0 && first.ProviderJobCount == 0, "no provider");
        var lookup = await OfficialVeoVideoTakeAcceptanceV1.GetAuthoritativeVideoTakeAsync(
            ready.Store, RealVertexExecutionV1Lock.BuildId, RealVertexExecutionV1Lock.ShotCode);
        Ok(lookup is not null && lookup.ArtifactId == ready.Artifact.ArtifactId
            && lookup.AcceptanceStatus == OfficialVeoVideoTakeAcceptanceV1.AcceptedStatus
            && lookup.VerificationStatus == ProductionArtifactStatuses.Verified, "authoritative lookup");
        var second = await OfficialVeoVideoTakeAcceptanceV1.AcceptAsync(ready.Request);
        Ok(second.Replayed && second.Take!.ArtifactId == first.Take!.ArtifactId, "replay same artifact");
        Ok((await ready.Store.ListArtifactsByTaskAsync(ready.Task.TaskId))
            .Count(a => a.Role == ProductionArtifactRoles.VideoTake) == 1, "VIDEO_TAKE stays 1");
        Ok(ready.Permits.IssueCalls == 0 && ready.Permits.ConsumeCalls == 0, "no permit mutation");
        var events = await ready.Store.ListEventsByTaskAsync(ready.Task.TaskId);
        Ok(events.Count(e => e.EventType == ProductionEventTypes.VideoTakeAccepted) == 1, "acceptance event once");
        Ok(first.RedactionCheck.Count == 0, "no secrets");
    }

    static async Task T_MissingArtifact(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = await OfficialVeoVideoTakeAcceptanceV1.AcceptAsync(ready.Request with { ExpectedArtifactId = Guid.NewGuid() });
        Ok(!r.Accepted && r.BlockReason == ProductionFailureReasons.OutputArtifactNotFound, "A missing artifact");
    }

    static async Task T_HashMismatch(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var original = ready.Files.Read(ready.Artifact.StorageKey) ?? Array.Empty<byte>();
        var tampered = (byte[])original.Clone();
        if (tampered.Length == 0)
        {
            Ok(false, "B hash mismatch");
            return;
        }
        tampered[^1] ^= 0xFF;
        ready.Files.PlantExternalBytes(ready.Artifact.StorageKey, tampered);
        var r = await OfficialVeoVideoTakeAcceptanceV1.AcceptAsync(ready.Request);
        Ok(!r.Accepted && r.BlockReason == ProductionFailureReasons.ArtifactHashMismatch, "B hash mismatch");
    }

    static async Task T_LengthMismatch(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var original = ready.Files.Read(ready.Artifact.StorageKey) ?? Array.Empty<byte>();
        var longer = new byte[original.Length + 1];
        Buffer.BlockCopy(original, 0, longer, 0, original.Length);
        ready.Files.PlantExternalBytes(ready.Artifact.StorageKey, longer);
        var r = await OfficialVeoVideoTakeAcceptanceV1.AcceptAsync(ready.Request);
        Ok(!r.Accepted && r.BlockReason == ProductionFailureReasons.ArtifactLengthMismatch, "C length mismatch");
    }

    static async Task T_MimeMismatch(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = await OfficialVeoVideoTakeAcceptanceV1.AcceptAsync(ready.Request with { ExpectedMime = "image/png" });
        Ok(!r.Accepted && r.BlockReason == ProductionFailureReasons.ArtifactMimeMismatch, "D MIME mismatch");
    }

    static async Task T_RoleMismatch(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var mix = ready.Artifact with { ArtifactId = Guid.NewGuid(), Role = ProductionArtifactRoles.Mix };
        await ready.Store.InsertArtifactAsync(mix);
        var r = await OfficialVeoVideoTakeAcceptanceV1.AcceptAsync(ready.Request with
        {
            ExpectedArtifactId = mix.ArtifactId,
            ExpectedByteLength = mix.SizeBytes,
            ExpectedSha256 = mix.ByteSha256,
        });
        Ok(!r.Accepted && r.BlockReason == ProductionFailureReasons.ArtifactRoleMismatch, "E role mismatch");
    }

    static async Task T_TaskMismatch(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = await OfficialVeoVideoTakeAcceptanceV1.AcceptAsync(ready.Request with { ExpectedTaskId = Guid.NewGuid() });
        Ok(!r.Accepted && r.BlockReason == ProductionFailureReasons.OutputTaskMismatch, "F task mismatch");
    }

    static async Task T_AttemptMismatch(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = await OfficialVeoVideoTakeAcceptanceV1.AcceptAsync(ready.Request with { ExpectedAttemptId = Guid.NewGuid() });
        Ok(!r.Accepted && r.BlockReason == ProductionFailureReasons.OutputAttemptMismatch, "G attempt mismatch");
    }

    static async Task T_SnapshotMismatch(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = await OfficialVeoVideoTakeAcceptanceV1.AcceptAsync(ready.Request with
        {
            ExpectedSnapshotHash = "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
        });
        Ok(!r.Accepted && r.BlockReason == ProductionFailureReasons.FreezeMismatch, "H snapshot mismatch");
    }

    static async Task T_ProviderMismatch(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = await OfficialVeoVideoTakeAcceptanceV1.AcceptAsync(ready.Request with { ExpectedProviderId = "runway" });
        Ok(!r.Accepted && r.BlockReason == ProductionFailureReasons.ProviderModelMismatch, "I provider mismatch");
    }

    static async Task T_ModelMismatch(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = await OfficialVeoVideoTakeAcceptanceV1.AcceptAsync(ready.Request with { ExpectedModelId = "gen4_turbo" });
        Ok(!r.Accepted && r.BlockReason == ProductionFailureReasons.ProviderModelMismatch, "J model mismatch");
    }

    static async Task T_OperationMismatch(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = await OfficialVeoVideoTakeAcceptanceV1.AcceptAsync(ready.Request with
        {
            ExpectedOperationName = "projects/other/locations/us-central1/operations/nope",
        });
        Ok(!r.Accepted && r.BlockReason == ProductionFailureReasons.OperationProvenanceMismatch, "K operation mismatch");
    }

    static async Task T_MissingOperation(Action<bool, string> Ok)
    {
        var ready = await Ready(jobId: "");
        var r = await OfficialVeoVideoTakeAcceptanceV1.AcceptAsync(ready.Request with
        {
            ExpectedEncodedOperationId = "",
            ExpectedOperationName = OperationName,
        });
        Ok(!r.Accepted && r.BlockReason == ProductionFailureReasons.OperationProvenanceMissing, "L missing operation");
    }

    static async Task T_AmbiguousTake(Action<bool, string> Ok)
    {
        var ready = await Ready();
        await ready.Store.InsertArtifactAsync(ready.Artifact with { ArtifactId = Guid.NewGuid() });
        var r = await OfficialVeoVideoTakeAcceptanceV1.AcceptAsync(ready.Request);
        Ok(!r.Accepted && r.BlockReason == ProductionFailureReasons.OutputArtifactAmbiguous, "M ambiguous VIDEO_TAKE");
    }

    static async Task T_NoTake(Action<bool, string> Ok)
    {
        var ready = await Ready(insertTake: false);
        var r = await OfficialVeoVideoTakeAcceptanceV1.AcceptAsync(ready.Request);
        Ok(!r.Accepted && r.BlockReason == ProductionFailureReasons.OutputArtifactNotFound, "N no VIDEO_TAKE");
    }

    static void T_StartForbidden(Action<bool, string> Ok)
    {
        var t = new OfficialVeoAcceptanceClosedTransport();
        try
        {
            t.CreateImageToVideoAsync(new VeoMotionTransportCreateRequest(
                Guid.Empty, Guid.Empty, "google", "veo-3.1", "", "", "", 8, 10, "16:9", 1, "720p", false, "", "", null)).GetAwaiter().GetResult();
            Ok(false, "O START_FORBIDDEN");
        }
        catch (InvalidOperationException ex)
        {
            Ok(ex.Message == ProductionFailureReasons.StartForbidden, "O START_FORBIDDEN");
        }
    }

    static void T_PollForbidden(Action<bool, string> Ok)
    {
        var t = new OfficialVeoAcceptanceClosedTransport();
        try
        {
            t.GetOperationAsync("veo_x").GetAwaiter().GetResult();
            Ok(false, "P POLL_FORBIDDEN");
        }
        catch (InvalidOperationException ex)
        {
            Ok(ex.Message == ProductionFailureReasons.PollForbidden, "P POLL_FORBIDDEN");
        }
    }

    static async Task T_PermitMutationForbidden(Action<bool, string> Ok)
    {
        var ready = await Ready();
        try
        {
            await ready.Permits.IssueAsync(new IssueProductionExecutionPermitCommand(
                RealVertexExecutionV1Lock.BuildId, RealVertexExecutionV1Lock.ShotCode, "EP01", "SC01",
                ProductionActions.CreateVideo, "google", "veo-3.1", "ck20", "x"));
            Ok(false, "Q permit issue");
        }
        catch (InvalidOperationException ex)
        {
            Ok(ex.Message == ProductionFailureReasons.PermitMutationForbidden, "Q PERMIT_MUTATION_FORBIDDEN");
        }
        try
        {
            await ready.Permits.TryConsumeAsync(
                RealVertexExecutionV1Lock.BuildId, RealVertexExecutionV1Lock.ShotCode,
                ProductionActions.CreateVideo, "google", "veo-3.1");
            Ok(false, "R permit consume");
        }
        catch (InvalidOperationException ex)
        {
            Ok(ex.Message == ProductionFailureReasons.PermitMutationForbidden, "R PERMIT_MUTATION_FORBIDDEN");
        }
        var accepted = await OfficialVeoVideoTakeAcceptanceV1.AcceptAsync(ready.Request);
        Ok(accepted.Accepted && ready.Permits.IssueCalls == 1 && ready.Permits.ConsumeCalls == 1, "CK20 itself does not issue/consume");
    }

    static async Task T_SnapshotSelectionUnchanged(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var snapBefore = (await ready.Store.GetSnapshotAsync(ready.Frozen.SnapshotId))!.SnapshotHash;
        var selBefore = (await ready.Selections.GetActiveAsync(
            RealVertexExecutionV1Lock.BuildId, RealVertexExecutionV1Lock.ShotCode, ProductionAiSelectionStages.Video))!.SelectionId;
        var count = await OfficialCreateVideoFreezeV1.CountOfficialCreateVideoSnapshotsAsync(ready.Store);
        _ = await OfficialVeoVideoTakeAcceptanceV1.AcceptAsync(ready.Request);
        Ok((await ready.Store.GetSnapshotAsync(ready.Frozen.SnapshotId))!.SnapshotHash == snapBefore
            && await OfficialCreateVideoFreezeV1.CountOfficialCreateVideoSnapshotsAsync(ready.Store) == count, "S snapshot unchanged");
        Ok((await ready.Selections.GetActiveAsync(
            RealVertexExecutionV1Lock.BuildId, RealVertexExecutionV1Lock.ShotCode, ProductionAiSelectionStages.Video))!.SelectionId == selBefore,
            "T selection unchanged");
    }

    static void T_Ck19StillGreen(Action<bool, string> Ok)
    {
        var ck19 = ProductionOfficialVeoPollRecoverMaterializationV1Regression.Run();
        Ok(ck19.Count == 0, ck19.Count == 0 ? "CK19 still green" : "CK19:" + string.Join(",", ck19));
    }

    static async Task<ReadyState> Ready(string? jobId = null, bool insertTake = true)
    {
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var permits = new OfficialVeoAcceptanceClosedPermitStore();
        var selections = new InMemoryProductionAiSelectionStore();
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-KF-20-" + new string('K', 48));
        var hash = KitVideoIntegrityRules.Sha256Hex(bytes);
        var engine = new ProductionWorkExecutionEngine(store);
        var req = await engine.CreateRequestAsync(
            RealVertexExecutionV1Lock.OfficialScope, ProductionActions.CreateImage, "ck20",
            "kf-20-" + Guid.NewGuid().ToString("N"));
        var kfTask = await engine.CreateTaskAsync(req.RequestId);
        await store.InsertArtifactAsync(new ProductionArtifact(
            OfficialCreateVideoFreezeV1.OfficialKeyframeId,
            kfTask.TaskId, Guid.NewGuid(), ProductionArtifactRoles.Keyframe,
            "image/png", bytes.Length, 0, hash, "kf/EP01-SC01-SH02.png", null,
            DateTimeOffset.UtcNow, ProductionArtifactStatuses.Verified, hash));
        var sources = OfficialCreateVideoFreezeV1.FromEp01Sh02Canon(bytes, hash);
        var frozen = (await OfficialCreateVideoFreezeV1.FreezeAsync(store, sources, OfficialCreateVideoFreezeCredentialFixture.Pin())).Snapshot;
        var task = await store.GetTaskAsync(frozen.TaskId) ?? throw new InvalidOperationException("TASK");
        var encoded = string.IsNullOrEmpty(jobId) && jobId is not null
            ? ""
            : VeoVertexOperationId.Encode(OperationName);
        var attempt = await engine.StartAttemptAsync(task.TaskId, hash);
        if (!string.IsNullOrEmpty(encoded))
            attempt = await engine.SubmitProviderJobAsync(attempt.AttemptId, encoded);
        var mp4 = ProductionTestMp4.Encode("ck20-out", 10);
        var sha = KitVideoIntegrityRules.Sha256Hex(mp4);
        var artifactId = Guid.NewGuid();
        var storage = insertTake ? files.Write(OfficialVeoPollRecoverMaterializationV1.Folder, attempt.AttemptId.ToString("N"), mp4) : "os-veo-out/missing";
        var art = new ProductionArtifact(
            artifactId, task.TaskId, attempt.AttemptId, ProductionArtifactRoles.VideoTake,
            ProductionTestMp4.Mime, mp4.Length, 10, sha, storage, attempt.ProviderJobId,
            DateTimeOffset.UtcNow, ProductionArtifactStatuses.Verified, frozen.SnapshotHash,
            frozen.ParentArtifactIds);
        if (insertTake)
        {
            await store.InsertArtifactAsync(art);
            if (!string.IsNullOrWhiteSpace(attempt.ProviderJobId)
                && attempt.Status != ProductionAttemptStatuses.ProviderSucceeded)
                await engine.RecordProviderSucceededAsync(attempt.AttemptId, sha);
            await store.UpdateTaskAsync((await store.GetTaskAsync(task.TaskId))! with
            {
                Status = ProductionTaskStatuses.Succeeded,
                CompletedAt = DateTimeOffset.UtcNow,
            });
        }
        var selection = new ProductionAiSelection(
            OfficialCreateVideoPermitAuthorizationV1.OfficialSelectionId,
            RealVertexExecutionV1Lock.BuildId,
            RealVertexExecutionV1Lock.ShotCode, "EP01", "SC01",
            ProductionAiSelectionStages.Video, "google", "veo-3.1",
            ProductionAiSelectionModes.Explicit, "checkpoint-20",
            OfficialVeoExecutionAuthorizationV1.OfficialSelectedAt,
            "DIRECTOR_SELECTION", ProductionAiSelectionStatuses.Active, "SELECTION_HASH_V1:ck20");
        selections.AddExisting(selection);
        var request = new OfficialVeoVideoTakeAcceptanceRequest(
            store, files, selections, permits, new OfficialVeoAcceptanceClosedTransport(),
            art.ArtifactId, task.TaskId, attempt.AttemptId, frozen.SnapshotId, frozen.SnapshotHash,
            selection.SelectionId, "AUTH_HASH_V1:ck20", OperationName, encoded, sha, mp4.Length, ProductionTestMp4.Mime);
        return new ReadyState(store, files, permits, selections, frozen, task, attempt, art, request);
    }

    sealed record ReadyState(
        InMemoryProductionWorkStore Store,
        InMemoryProductionArtifactFileStore Files,
        OfficialVeoAcceptanceClosedPermitStore Permits,
        InMemoryProductionAiSelectionStore Selections,
        ProductionTaskInputSnapshot Frozen,
        ProductionTask Task,
        ProductionAttempt Attempt,
        ProductionArtifact Artifact,
        OfficialVeoVideoTakeAcceptanceRequest Request);
}
