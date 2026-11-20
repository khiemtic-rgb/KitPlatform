namespace KitPlatform.Packs.Content.Production;

public interface IPerformanceContractService
{
    PerformanceDraft CreateDraft(PerformanceProjectionSource source);
    PerformanceValidation ValidateDraft(PerformanceDraft draft, string? extraJson = null);
    Task<PerformanceSnapshot> FreezeAsync(PerformanceDraft draft, string createdBy, PerformanceSourceRef? source = null, string? extraJson = null, CancellationToken ct = default);
    Task<PerformanceSnapshot> GetAsync(Guid performanceSnapshotId, CancellationToken ct = default);
    void BindToScope(PerformanceSnapshot snapshot, Guid buildId, string shotCode);
}

public sealed class PerformanceContractService : IPerformanceContractService
{
    readonly IPerformanceSnapshotStore _store;

    public PerformanceContractService(IPerformanceSnapshotStore store) => _store = store;

    public PerformanceDraft CreateDraft(PerformanceProjectionSource source) =>
        PerformanceContractRules.CreateDraft(source);

    public PerformanceValidation ValidateDraft(PerformanceDraft draft, string? extraJson = null) =>
        PerformanceContractRules.Validate(draft, extraJson);

    public async Task<PerformanceSnapshot> FreezeAsync(
        PerformanceDraft draft,
        string createdBy,
        PerformanceSourceRef? source = null,
        string? extraJson = null,
        CancellationToken ct = default)
    {
        var verdict = PerformanceContractRules.ValidateForFreeze(draft, extraJson);
        if (!verdict.Ok)
        {
            var first = verdict.Issues[0];
            throw new InvalidOperationException(first.Code);
        }

        var payload = PerformanceContractCanon.CanonicalJson(draft);
        var hash = PerformanceContractCanon.PerformanceHashOf(draft);
        var existing = await _store.GetByHashAsync(draft.BuildId, draft.ShotCode, hash, ct);
        if (existing is not null)
            return existing;
        var frozen = new PerformanceSnapshot(
            Guid.NewGuid(),
            hash,
            DateTimeOffset.UtcNow,
            (createdBy ?? "").Trim(),
            source ?? new PerformanceSourceRef(false, draft.Lines.Select(l => l.Delivery.Locked).ToArray(), draft.Lines.Any(l => !l.Delivery.Locked), PerformanceContractCanon.DraftFingerprintOf(draft)),
            draft,
            payload);
        await _store.InsertAsync(frozen, ct);
        return frozen;
    }

    public async Task<PerformanceSnapshot> GetAsync(Guid performanceSnapshotId, CancellationToken ct = default)
    {
        var row = await _store.GetAsync(performanceSnapshotId, ct);
        if (row is null) throw new InvalidOperationException(PerformanceContractIds.SnapshotNotFound);
        if (!PerformanceContractCanon.IsValidPerformanceHash(row.PerformanceHash))
            throw new InvalidOperationException(PerformanceContractIds.InvalidPerformanceHash);
        var live = PerformanceContractIds.HashPrefix
            + PerformanceContractCanon.Sha256Hex(System.Text.Encoding.UTF8.GetBytes(row.PayloadJson));
        if (!string.Equals(live, row.PerformanceHash, StringComparison.Ordinal))
            throw new InvalidOperationException(PerformanceContractIds.SnapshotImmutable);
        return row;
    }

    public void BindToScope(PerformanceSnapshot snapshot, Guid buildId, string shotCode)
    {
        if (snapshot.Draft.BuildId != buildId)
            throw new InvalidOperationException(PerformanceContractIds.CrossScopeForbidden);
        if (!string.Equals(snapshot.Draft.ShotCode, shotCode, StringComparison.Ordinal))
            throw new InvalidOperationException(PerformanceContractIds.CrossShotForbidden);
        if (ProductionScopeAnchorRules.IsOfficialFilm(buildId)
            && !ProductionDirectorTiming.HasDirectorTiming(snapshot.Source))
            throw new InvalidOperationException(PerformanceContractIds.OfficialFilmForbidden);
    }
}
