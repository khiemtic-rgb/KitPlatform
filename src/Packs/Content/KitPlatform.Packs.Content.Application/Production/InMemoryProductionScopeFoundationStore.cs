namespace KitPlatform.Packs.Content.Production;

public sealed class InMemoryProductionScopeFoundationStore : IProductionScopeFoundationStore
{
    readonly object _gate = new();
    readonly Dictionary<Guid, ProductionScopeAnchor> _anchors = [];
    readonly Dictionary<Guid, ProductionTestRun> _runs = [];
    readonly Dictionary<Guid, ProductionTestScope> _scopes = [];
    readonly Dictionary<string, ProductionTestShot> _shots = new(StringComparer.Ordinal);

    public Task InsertAnchorAsync(ProductionScopeAnchor anchor, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (_anchors.ContainsKey(anchor.ScopeId))
                throw new InvalidOperationException("SCOPE_ANCHOR_EXISTS");
            _anchors[anchor.ScopeId] = anchor;
        }
        return Task.CompletedTask;
    }

    public Task<ProductionScopeAnchor?> GetAnchorAsync(Guid scopeId, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult(_anchors.TryGetValue(scopeId, out var a) ? a : null);
    }

    public Task InsertTestRunAsync(ProductionTestRun run, CancellationToken ct = default)
    {
        lock (_gate) _runs[run.TestRunId] = run;
        return Task.CompletedTask;
    }

    public Task<ProductionTestRun?> GetTestRunAsync(Guid testRunId, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult(_runs.TryGetValue(testRunId, out var r) ? r : null);
    }

    public Task InsertTestScopeAsync(ProductionTestScope scope, CancellationToken ct = default)
    {
        lock (_gate) _scopes[scope.ScopeId] = scope;
        return Task.CompletedTask;
    }

    public Task<ProductionTestScope?> GetTestScopeAsync(Guid scopeId, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult(_scopes.TryGetValue(scopeId, out var s) ? s : null);
    }

    public Task<ProductionTestScope?> GetTestScopeByRunAsync(Guid testRunId, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult(_scopes.Values.FirstOrDefault(s => s.TestRunId == testRunId));
    }

    public Task InsertTestShotAsync(ProductionTestShot shot, CancellationToken ct = default)
    {
        lock (_gate) _shots[$"{shot.ScopeId:N}:{shot.ShotCode}"] = shot;
        return Task.CompletedTask;
    }

    public Task<ProductionTestShot?> GetTestShotAsync(Guid scopeId, string shotCode, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult(_shots.TryGetValue($"{scopeId:N}:{shotCode}", out var s) ? s : null);
    }

    public void SeedFilm(Guid seriesBuildId)
    {
        var id = ProductionScopeAnchorRules.FilmScopeId(seriesBuildId);
        lock (_gate)
        {
            _anchors[id] = new ProductionScopeAnchor(id, ProductionScopeAnchorRules.Film, id, null, DateTimeOffset.UtcNow);
        }
    }
}
