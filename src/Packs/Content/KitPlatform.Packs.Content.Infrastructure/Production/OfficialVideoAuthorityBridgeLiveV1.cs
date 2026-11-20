using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

public sealed record OfficialVideoAuthorityBridgeLiveResult(
    AuthoritativeVideoTake? Take,
    OfficialVideoAuthorityResolution Motion,
    ProductionPreview Preview,
    ShotProductionState State,
    IReadOnlyList<string> BindingRoles,
    int ArtifactCount,
    string Verdict,
    string Handoff);

/// <summary>CK21A live read-only. Never Accept / Start / Poll / CREATE_VOICE.</summary>
public static class OfficialVideoAuthorityBridgeLiveV1
{
    public static readonly Guid ExpectedArtifactId = OfficialVeoVideoTakeAcceptanceLiveV1.ExpectedArtifactId;
    public static readonly Guid KeyframeId = Guid.Parse("91daaf4b-5550-4cbc-b9f9-0bd0be556cef");
    public const string Ck21aPass = "CK21A_LIVE_PASS";
    public const string NotReady = "CK21A_LIVE_NOT_READY";

    public static async Task<OfficialVideoAuthorityBridgeLiveResult> ReadDevelopmentAsync(
        string connectionString,
        CancellationToken ct = default)
    {
        var db = new NpgsqlConnectionFactory(connectionString);
        var store = new ProductionWorkRepository(db);
        var os = new ProductionExecutionService(store, new FixtureMotionAdapter());
        var scope = RealVertexExecutionV1Lock.OfficialScope;

        var take = await OfficialVideoAuthorityBridgeV1.GetAuthoritativeVideoTakeAsync(
            store, scope.BuildId, scope.ShotCode, ct);
        var motion = await OfficialVideoAuthorityBridgeV1.ResolveVideoTakeAsync(
            store, scope.BuildId, scope.ShotCode, ct);
        var preview = await os.GetPreviewAsync(scope, ct);
        ShotProductionState? state = null;
        try { state = await os.GetShotStateAsync(scope, null, ct); }
        catch { /* read-only forensic: authority can still pass without action catalog */ }
        var bindings = await store.ListActiveBindingsAsync(scope.BuildId, scope.ShotCode, ct);
        var arts = await store.ListArtifactsByTaskAsync(OfficialVeoVideoTakeAcceptanceLiveV1.ExpectedTaskId, ct);

        var motionNode = state?.Nodes.FirstOrDefault(n => n.Node == ShotNodeIds.Motion);
        var pass = take is { ArtifactId: var id, ArtifactRole: ProductionArtifactRoles.VideoTake, AcceptanceStatus: OfficialVeoVideoTakeAcceptanceV1.AcceptedStatus }
            && id == ExpectedArtifactId
            && motion.Hit?.ArtifactId == ExpectedArtifactId
            && motion.Hit.ArtifactRole == ProductionArtifactRoles.VideoTake
            && preview.ArtifactId == ExpectedArtifactId
            && preview.Role == OfficialVeoVideoTakeAcceptanceV1.AcceptanceRole
            && preview.ArtifactId != KeyframeId
            && motion.Hit.ArtifactId != KeyframeId
            && (state is null
                || (state.VideoArtifactId == ExpectedArtifactId
                    && state.AcceptedArtifactId == ExpectedArtifactId
                    && motionNode?.ArtifactId == ExpectedArtifactId
                    && motionNode.Status == ShotNodeStatuses.Accepted
                    && motionNode.ArtifactId != KeyframeId));

        return new OfficialVideoAuthorityBridgeLiveResult(
            take,
            motion,
            preview,
            state ?? new ShotProductionState(scope, false, null, null, null, [], []),
            bindings.Select(b => b.Role + "=" + b.ArtifactId.ToString("D")).ToArray(),
            arts.Count,
            pass ? OfficialVideoAuthorityBridgeV1.SuiteId + " LIVE PASS" : "BLOCKED",
            pass ? Ck21aPass : NotReady);
    }
}
