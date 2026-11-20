namespace KitPlatform.Packs.Content.Production;

public sealed record PerformanceSnapshotBindResult(
    Guid PerformanceSnapshotId,
    string PerformanceHash,
    string PerformancePayloadJson,
    PerformanceDraft Draft,
    PerformanceSnapshot Snapshot);

public static class PerformanceSnapshotBinding
{
    public static bool HasClaim(ShotExecutionInput input) =>
        input.PerformanceSnapshotId is not null
        || !string.IsNullOrWhiteSpace(input.PerformanceHash)
        || !string.IsNullOrWhiteSpace(input.PerformancePayloadJson);

    public static async Task<PerformanceSnapshotBindResult> BindAsync(
        IPerformanceSnapshotStore store,
        ShotExecutionInput input,
        ProductionScope scope,
        CancellationToken ct = default)
    {
        if (ProductionScopeAnchorRules.IsOfficialFilm(scope.BuildId)
            && input.PerformanceSnapshotId is null)
            throw new InvalidOperationException(PerformanceContractIds.OfficialFilmForbidden);

        if (!string.IsNullOrWhiteSpace(input.PerformanceHash)
            && !PerformanceContractCanon.IsValidPerformanceHash(input.PerformanceHash))
            throw new InvalidOperationException(PerformanceContractIds.InvalidPerformanceHash);

        if (input.PerformanceSnapshotId is null)
            throw new InvalidOperationException(PerformanceContractIds.SnapshotNotFound);

        var snap = await store.GetAsync(input.PerformanceSnapshotId.Value, ct);
        if (snap is null)
            throw new InvalidOperationException(PerformanceContractIds.SnapshotNotFound);

        if (!PerformanceContractCanon.IsValidPerformanceHash(snap.PerformanceHash))
            throw new InvalidOperationException(PerformanceContractIds.InvalidPerformanceHash);

        var payloadHash = PerformanceContractIds.HashPrefix
            + PerformanceContractCanon.Sha256Hex(System.Text.Encoding.UTF8.GetBytes(snap.PayloadJson ?? ""));
        if (!string.Equals(payloadHash, snap.PerformanceHash, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);

        if (!string.IsNullOrWhiteSpace(input.PerformanceHash)
            && !string.Equals(input.PerformanceHash, snap.PerformanceHash, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);

        if (!string.IsNullOrWhiteSpace(input.PerformancePayloadJson)
            && !string.Equals(input.PerformancePayloadJson, snap.PayloadJson, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);

        if (ProductionScopeAnchorRules.IsOfficialFilm(snap.Draft.BuildId)
            && !ProductionDirectorTiming.HasDirectorTiming(snap.Source))
            throw new InvalidOperationException(PerformanceContractIds.OfficialFilmForbidden);
        if (snap.Draft.BuildId != scope.BuildId)
            throw new InvalidOperationException(PerformanceContractIds.CrossScopeForbidden);
        if (!string.Equals(snap.Draft.ShotCode, scope.ShotCode, StringComparison.Ordinal))
            throw new InvalidOperationException(PerformanceContractIds.CrossShotForbidden);

        var forbidden = PerformanceContractCanon.ScanForbidden(snap.PayloadJson);
        if (forbidden.Count > 0)
            throw new InvalidOperationException(forbidden[0].Code);

        return new PerformanceSnapshotBindResult(
            snap.PerformanceSnapshotId,
            snap.PerformanceHash,
            snap.PayloadJson ?? "",
            snap.Draft,
            snap);
    }
}
