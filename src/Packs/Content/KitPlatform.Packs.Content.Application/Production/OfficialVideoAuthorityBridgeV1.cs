using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// CK21A — Director/UI video authority. Reuses CK20 GetAuthoritativeVideoTake.
/// Playback order: verified CURRENT:MIX, then verified CURRENT:LIPSYNC, then ACCEPTED:VIDEO_TAKE,
/// then verified CURRENT:ASSEMBLY / VISIBLE:ASSEMBLY, then VISIBLE video, then CURRENT:VIDEO_TAKE.
/// A binding whose artifact role does not match is fail-closed. It does not fall through to another role.
/// ResolveVideoTake stays on the video-take bindings and does not return a mix, lipsync, or assembly.
/// </summary>
public sealed record OfficialVideoAuthorityHit(
    Guid ArtifactId,
    string ArtifactRole,
    string BindingRole,
    string ByteSha256,
    string StorageKey,
    string MimeType,
    decimal DurationSec,
    long SizeBytes,
    string VerificationStatus,
    string? AcceptanceStatus,
    Guid TaskId,
    Guid AttemptId,
    string? ProviderId,
    string? ModelId);

public sealed record OfficialVideoAuthorityResolution(
    OfficialVideoAuthorityHit? Hit,
    bool FailClosed,
    string? Reason)
{
    public bool IsEmpty => Hit is null;
}

public static class OfficialVideoAuthorityBridgeV1
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_OFFICIAL_VIDEO_AUTHORITY_BRIDGE_V1";

    public const string CurrentVideoTakeStatus = "CURRENT";

    public static async Task<AuthoritativeVideoTake?> GetAuthoritativeVideoTakeAsync(
        IProductionWorkStore store,
        Guid buildId,
        string shotCode,
        CancellationToken ct = default)
    {
        var accepted = await OfficialVeoVideoTakeAcceptanceV1.GetAuthoritativeVideoTakeAsync(store, buildId, shotCode, ct);
        if (accepted is not null) return accepted;
        if (ProductionScopeAnchorRules.IsOfficialFilm(buildId)) return null;
        return await ResolveTenantCurrentVideoTakeAsync(store, buildId, shotCode, ct);
    }

    static async Task<AuthoritativeVideoTake?> ResolveTenantCurrentVideoTakeAsync(
        IProductionWorkStore store,
        Guid buildId,
        string shotCode,
        CancellationToken ct)
    {
        var bind = await store.GetActiveBindingAsync(
            buildId, shotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.VideoTake), ct);
        if (bind is null) return null;
        var artifact = await store.GetArtifactAsync(bind.ArtifactId, ct);
        if (artifact is null
            || artifact.Role != ProductionArtifactRoles.VideoTake
            || artifact.VerificationStatus != ProductionArtifactStatuses.Verified)
            return null;
        var task = await store.GetTaskAsync(artifact.TaskId, ct);
        var attempt = await store.GetAttemptAsync(artifact.AttemptId, ct);
        var snap = await store.GetSnapshotByTaskAsync(artifact.TaskId, ct);
        if (task is null || attempt is null || snap is null) return null;
        if (task.Scope.BuildId != buildId) return null;
        if (!string.Equals(task.Scope.ShotCode, shotCode, StringComparison.OrdinalIgnoreCase)) return null;
        return new AuthoritativeVideoTake(
            task.Scope.BuildId,
            task.Scope.ShotCode,
            task.Action,
            task.TaskId,
            attempt.AttemptId,
            snap.SnapshotId,
            snap.SnapshotHash,
            attempt.ProviderId,
            attempt.ModelId,
            Guid.Empty,
            "",
            artifact.ArtifactId,
            artifact.Role,
            artifact.ByteSha256,
            artifact.SizeBytes,
            artifact.MimeType,
            artifact.VerificationStatus,
            CurrentVideoTakeStatus);
    }

    public static Task<OfficialVideoAuthorityResolution> ResolveAsync(
        IProductionWorkStore store,
        Guid buildId,
        string shotCode,
        CancellationToken ct = default)
        => ResolveAsync(store, buildId, shotCode, videoTakeOnly: false, ct);

    public static Task<OfficialVideoAuthorityResolution> ResolveVideoTakeAsync(
        IProductionWorkStore store,
        Guid buildId,
        string shotCode,
        CancellationToken ct = default)
        => ResolveAsync(store, buildId, shotCode, videoTakeOnly: true, ct);

    static async Task<OfficialVideoAuthorityResolution> ResolveAsync(
        IProductionWorkStore store,
        Guid buildId,
        string shotCode,
        bool videoTakeOnly,
        CancellationToken ct)
    {
        if (!videoTakeOnly)
        {
            var mix = await TryExactCurrentAsync(
                store, buildId, shotCode, ProductionArtifactRoles.Mix, ct);
            if (mix.FailClosed) return new(null, true, mix.Reason);
            if (mix.Hit is not null) return new(mix.Hit, false, null);
            var lipsync = await TryExactCurrentAsync(
                store, buildId, shotCode, ProductionArtifactRoles.Lipsync, ct);
            if (lipsync.FailClosed) return new(null, true, lipsync.Reason);
            if (lipsync.Hit is not null) return new(lipsync.Hit, false, null);
        }

        var acceptedBind = await store.GetActiveBindingAsync(
            buildId, shotCode, OfficialVeoVideoTakeAcceptanceV1.AcceptanceRole, ct);
        if (acceptedBind is not null)
        {
            var take = await OfficialVeoVideoTakeAcceptanceV1.GetAuthoritativeVideoTakeAsync(store, buildId, shotCode, ct);
            if (take is null)
                return new(null, true, ProductionFailureReasons.OutputArtifactNotFound);
            var art = await store.GetArtifactAsync(take.ArtifactId, ct);
            if (art is null
                || art.Role != ProductionArtifactRoles.VideoTake
                || art.VerificationStatus != ProductionArtifactStatuses.Verified)
                return new(null, true, ProductionFailureReasons.OutputArtifactNotFound);
            return new(From(art, OfficialVeoVideoTakeAcceptanceV1.AcceptanceRole, take), false, null);
        }

        if (!videoTakeOnly)
        {
            var assembled = await TryVerifiedAssemblyAsync(store, buildId, shotCode, ct);
            if (assembled is not null) return new(assembled, false, null);
        }

        foreach (var role in new[]
        {
            ProductionBindingRoles.VisibleFor(ProductionArtifactRoles.VideoTake),
            ProductionBindingRoles.Visible,
        })
        {
            var hit = await TryRoleAsync(store, buildId, shotCode, role, videoTakeOnly, ct);
            if (hit is not null) return new(hit, false, null);
        }

        var currentTyped = await TryRoleAsync(
            store, buildId, shotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.VideoTake), true, ct);
        if (currentTyped is not null) return new(currentTyped, false, null);

        var currentBare = await TryRoleAsync(
            store, buildId, shotCode, ProductionBindingRoles.Current, true, ct);
        if (currentBare is not null) return new(currentBare, false, null);

        return new(null, false, ProductionFailureReasons.VisibleMissing);
    }

    static async Task<(OfficialVideoAuthorityHit? Hit, bool FailClosed, string? Reason)> TryExactCurrentAsync(
        IProductionWorkStore store,
        Guid buildId,
        string shotCode,
        string artifactRole,
        CancellationToken ct)
    {
        var role = ProductionBindingRoles.CurrentFor(artifactRole);
        var bind = await store.GetActiveBindingAsync(buildId, shotCode, role, ct);
        if (bind is null) return (null, false, null);
        var art = await store.GetArtifactAsync(bind.ArtifactId, ct);
        if (art is null
            || art.Role != artifactRole
            || art.VerificationStatus != ProductionArtifactStatuses.Verified
            || string.IsNullOrWhiteSpace(art.ByteSha256)
            || string.IsNullOrWhiteSpace(art.StorageKey))
            return (null, true, ProductionFailureReasons.FailedIdentity);
        return (From(art, role, null), false, null);
    }

    static async Task<OfficialVideoAuthorityHit?> TryVerifiedAssemblyAsync(
        IProductionWorkStore store,
        Guid buildId,
        string shotCode,
        CancellationToken ct)
    {
        foreach (var role in new[]
        {
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly),
            ProductionBindingRoles.VisibleFor(ProductionArtifactRoles.Assembly),
        })
        {
            var bind = await store.GetActiveBindingAsync(buildId, shotCode, role, ct);
            if (bind is null) continue;
            var art = await store.GetArtifactAsync(bind.ArtifactId, ct);
            if (art is null
                || art.Role != ProductionArtifactRoles.Assembly
                || art.VerificationStatus != ProductionArtifactStatuses.Verified)
                continue;
            return From(art, role, null);
        }

        return null;
    }

    static async Task<OfficialVideoAuthorityHit?> TryRoleAsync(
        IProductionWorkStore store,
        Guid buildId,
        string shotCode,
        string role,
        bool videoTakeOnly,
        CancellationToken ct)
    {
        var bind = await store.GetActiveBindingAsync(buildId, shotCode, role, ct);
        if (bind is null) return null;
        var art = await store.GetArtifactAsync(bind.ArtifactId, ct);
        if (art is null || art.Role == ProductionArtifactRoles.Keyframe) return null;
        if (videoTakeOnly)
            return art.Role == ProductionArtifactRoles.VideoTake ? From(art, role, null) : null;
        if (art.Role != ProductionArtifactRoles.VideoTake
            && art.Role != ProductionArtifactRoles.Mix
            && art.Role != ProductionArtifactRoles.Lipsync)
            return null;
        return From(art, role, null);
    }

    static OfficialVideoAuthorityHit From(ProductionArtifact art, string bindingRole, AuthoritativeVideoTake? take) =>
        new(
            art.ArtifactId,
            art.Role,
            bindingRole,
            art.ByteSha256,
            art.StorageKey,
            art.MimeType,
            art.DurationSec,
            art.SizeBytes,
            art.VerificationStatus,
            take?.AcceptanceStatus ?? (bindingRole == OfficialVeoVideoTakeAcceptanceV1.AcceptanceRole
                ? OfficialVeoVideoTakeAcceptanceV1.AcceptedStatus
                : null),
            take?.TaskId ?? art.TaskId,
            take?.AttemptId ?? art.AttemptId,
            take?.ProviderId,
            take?.ModelId);
}
