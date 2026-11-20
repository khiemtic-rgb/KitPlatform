using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

public sealed record OfficialVeoVideoTakeAcceptanceLiveResult(
    OfficialVeoVideoTakeAcceptanceResult First,
    OfficialVeoVideoTakeAcceptanceResult Second,
    AuthoritativeVideoTake? Lookup,
    string Verdict,
    string Handoff,
    int StartCount,
    int PollCount);

public static class OfficialVeoVideoTakeAcceptanceLiveV1
{
    public static readonly Guid ExpectedArtifactId = Guid.Parse("b44e530b-5b59-422f-9b80-e058a3a639b5");
    public static readonly Guid ExpectedTaskId = OfficialVeoPollRecoverMaterializationLiveV1.ExpectedTaskId;
    public static readonly Guid ExpectedAttemptId = OfficialVeoPollRecoverMaterializationLiveV1.ExpectedAttemptId;
    public static readonly Guid ExpectedSnapshotId = OfficialVeoPollRecoverMaterializationLiveV1.ExpectedSnapshotId;
    public const string ExpectedSnapshotHash = OfficialVeoPollRecoverMaterializationLiveV1.ExpectedSnapshotHash;
    public const string ExpectedAuthorizationHash = OfficialVeoPollRecoverMaterializationLiveV1.ExpectedStartAuthorizationHash;
    public const string ExpectedOperationName = OfficialVeoPollRecoverMaterializationLiveV1.ExpectedOperationName;
    public const string ExpectedEncodedOperationId = OfficialVeoPollRecoverMaterializationLiveV1.ExpectedEncodedOperationId;
    public const string ExpectedSha256 = "21700e4f81c9e97422e99651effd4e33f9271539f7c546fb45f442dbda476f34";
    public const long ExpectedByteLength = 1437239;
    public const string ExpectedMime = "video/mp4";
    public const string Ck20Complete = "CK20_COMPLETE";
    public const string NotReady = "CK20_NOT_COMPLETE";

    public static async Task<OfficialVeoVideoTakeAcceptanceLiveResult> AcceptDevelopmentAsync(
        string connectionString,
        string videoRoot,
        CancellationToken ct = default)
    {
        var db = new NpgsqlConnectionFactory(connectionString);
        var store = new ProductionWorkRepository(db);
        var selections = new ProductionAiSelectionRepository(db);
        var files = new DirectoryProductionArtifactFileStore(videoRoot);
        var permits = new OfficialVeoAcceptanceClosedPermitStore();
        var transport = new OfficialVeoAcceptanceClosedTransport();
        var request = new OfficialVeoVideoTakeAcceptanceRequest(
            store,
            files,
            selections,
            permits,
            transport,
            ExpectedArtifactId,
            ExpectedTaskId,
            ExpectedAttemptId,
            ExpectedSnapshotId,
            ExpectedSnapshotHash,
            OfficialCreateVideoPermitAuthorizationV1.OfficialSelectionId,
            ExpectedAuthorizationHash,
            ExpectedOperationName,
            ExpectedEncodedOperationId,
            ExpectedSha256,
            ExpectedByteLength,
            ExpectedMime);

        var first = await OfficialVeoVideoTakeAcceptanceV1.AcceptAsync(request, ct);
        var second = await OfficialVeoVideoTakeAcceptanceV1.AcceptAsync(request, ct);
        var lookup = await OfficialVeoVideoTakeAcceptanceV1.GetAuthoritativeVideoTakeAsync(
            store, RealVertexExecutionV1Lock.BuildId, RealVertexExecutionV1Lock.ShotCode, ct);
        var pass = first.Accepted && second.Replayed
            && first.Take?.ArtifactId == ExpectedArtifactId
            && lookup?.ArtifactId == ExpectedArtifactId
            && first.StartCount == 0 && first.PollCount == 0
            && second.StartCount == 0 && permits.IssueCalls == 0 && permits.ConsumeCalls == 0;
        return new OfficialVeoVideoTakeAcceptanceLiveResult(
            first,
            second,
            lookup,
            pass ? OfficialVeoVideoTakeAcceptanceV1.VerdictPass : "BLOCKED — " + (first.BlockReason ?? first.Outcome),
            pass ? Ck20Complete : NotReady,
            first.StartCount + second.StartCount,
            first.PollCount + second.PollCount);
    }
}
