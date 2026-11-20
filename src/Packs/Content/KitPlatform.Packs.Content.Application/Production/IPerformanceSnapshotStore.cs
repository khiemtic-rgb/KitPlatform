namespace KitPlatform.Packs.Content.Production;

public interface IPerformanceSnapshotStore
{
    Task InsertAsync(PerformanceSnapshot snapshot, CancellationToken ct = default);
    Task<PerformanceSnapshot?> GetAsync(Guid performanceSnapshotId, CancellationToken ct = default);
    Task<PerformanceSnapshot?> GetByScopeAsync(Guid buildId, string shotCode, CancellationToken ct = default);
    Task<PerformanceSnapshot?> GetByHashAsync(Guid buildId, string shotCode, string performanceHash, CancellationToken ct = default);
}

public sealed class InMemoryPerformanceSnapshotStore : IPerformanceSnapshotStore
{
    readonly object _gate = new();
    readonly Dictionary<Guid, PerformanceSnapshot> _rows = [];

    public Task InsertAsync(PerformanceSnapshot snapshot, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (_rows.ContainsKey(snapshot.PerformanceSnapshotId))
                throw new InvalidOperationException(PerformanceContractIds.SnapshotImmutable);
            _rows[snapshot.PerformanceSnapshotId] = snapshot;
        }
        return Task.CompletedTask;
    }

    public Task<PerformanceSnapshot?> GetAsync(Guid performanceSnapshotId, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult(_rows.TryGetValue(performanceSnapshotId, out var row) ? row : null);
    }

    public Task<PerformanceSnapshot?> GetByScopeAsync(Guid buildId, string shotCode, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var hit = _rows.Values
                .Where(r => r.Draft.BuildId == buildId
                    && string.Equals(r.Draft.ShotCode, shotCode, StringComparison.Ordinal))
                .OrderByDescending(r => r.FrozenAt)
                .FirstOrDefault();
            return Task.FromResult(hit);
        }
    }

    public Task<PerformanceSnapshot?> GetByHashAsync(Guid buildId, string shotCode, string performanceHash, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var hit = _rows.Values.FirstOrDefault(r =>
                r.Draft.BuildId == buildId
                && string.Equals(r.Draft.ShotCode, shotCode, StringComparison.Ordinal)
                && string.Equals(r.PerformanceHash, performanceHash, StringComparison.Ordinal));
            return Task.FromResult(hit);
        }
    }
}
