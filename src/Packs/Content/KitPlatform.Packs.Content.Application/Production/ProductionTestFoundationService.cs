namespace KitPlatform.Packs.Content.Production;

public sealed class ProductionTestFoundationService : IProductionTestFoundationService
{
    public const string DefaultShotCode = "TEST-SC01-SH01";
    public const string DefaultEpisode = "TEST";
    public const string DefaultScene = "SC01";

    readonly IProductionScopeFoundationStore _store;

    public ProductionTestFoundationService(IProductionScopeFoundationStore store) => _store = store;

    public async Task<ProductionTestFoundation> CreateAsync(string name, string createdBy, CancellationToken ct = default)
    {
        var label = (name ?? "").Trim();
        if (label.Length == 0)
            throw new InvalidOperationException("TEST_RUN_NAME_REQUIRED");

        Guid scopeId;
        do
        {
            scopeId = Guid.NewGuid();
        }
        while (ProductionScopeAnchorRules.IsOfficialFilm(scopeId)
            || await _store.GetAnchorAsync(scopeId, ct) is not null);

        var now = DateTimeOffset.UtcNow;
        var run = new ProductionTestRun(Guid.NewGuid(), label, "OPEN", now, createdBy, "{}");
        var anchor = new ProductionScopeAnchor(scopeId, ProductionScopeAnchorRules.Test, null, run.TestRunId, now);
        var scope = new ProductionTestScope(scopeId, run.TestRunId, DefaultEpisode, DefaultScene, now, "{}");
        var shot = new ProductionTestShot(
            Guid.NewGuid(), scopeId, DefaultShotCode, DefaultEpisode, DefaultScene, DefaultShotCode, now, "{}");

        await _store.InsertTestRunAsync(run, ct);
        await _store.InsertAnchorAsync(anchor, ct);
        await _store.InsertTestScopeAsync(scope, ct);
        await _store.InsertTestShotAsync(shot, ct);
        return new ProductionTestFoundation(run, anchor, scope, shot);
    }

    public async Task<ProductionTestFoundation?> GetAsync(Guid testRunId, CancellationToken ct = default)
    {
        var run = await _store.GetTestRunAsync(testRunId, ct);
        if (run is null) return null;
        var scope = await _store.GetTestScopeByRunAsync(testRunId, ct);
        if (scope is null) return null;
        var anchor = await _store.GetAnchorAsync(scope.ScopeId, ct);
        var shot = await _store.GetTestShotAsync(scope.ScopeId, DefaultShotCode, ct);
        if (anchor is null || shot is null) return null;
        return new ProductionTestFoundation(run, anchor, scope, shot);
    }

    public async Task<ProductionScopeKind> ResolveKindAsync(Guid scopeId, CancellationToken ct = default)
    {
        var anchor = await _store.GetAnchorAsync(scopeId, ct);
        return ProductionScopeGuard.Resolve(anchor, scopeId);
    }

    public async Task RequireArtifactAccessAsync(Guid requestScopeId, Guid ownerScopeId, CancellationToken ct = default)
    {
        var request = await ResolveKindAsync(requestScopeId, ct);
        var owner = await ResolveKindAsync(ownerScopeId, ct);
        ProductionScopeGuard.RequireSameScope(requestScopeId, request, ownerScopeId, owner);
    }
}
