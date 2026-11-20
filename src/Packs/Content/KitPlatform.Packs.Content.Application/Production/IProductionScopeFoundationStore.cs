namespace KitPlatform.Packs.Content.Production;

public interface IProductionScopeFoundationStore
{
    Task InsertAnchorAsync(ProductionScopeAnchor anchor, CancellationToken ct = default);
    Task<ProductionScopeAnchor?> GetAnchorAsync(Guid scopeId, CancellationToken ct = default);
    Task InsertTestRunAsync(ProductionTestRun run, CancellationToken ct = default);
    Task<ProductionTestRun?> GetTestRunAsync(Guid testRunId, CancellationToken ct = default);
    Task InsertTestScopeAsync(ProductionTestScope scope, CancellationToken ct = default);
    Task<ProductionTestScope?> GetTestScopeAsync(Guid scopeId, CancellationToken ct = default);
    Task<ProductionTestScope?> GetTestScopeByRunAsync(Guid testRunId, CancellationToken ct = default);
    Task InsertTestShotAsync(ProductionTestShot shot, CancellationToken ct = default);
    Task<ProductionTestShot?> GetTestShotAsync(Guid scopeId, string shotCode, CancellationToken ct = default);
}

public interface IProductionTestFoundationService
{
    Task<ProductionTestFoundation> CreateAsync(string name, string createdBy, CancellationToken ct = default);
    Task<ProductionTestFoundation?> GetAsync(Guid testRunId, CancellationToken ct = default);
    Task<ProductionScopeKind> ResolveKindAsync(Guid scopeId, CancellationToken ct = default);
    Task RequireArtifactAccessAsync(Guid requestScopeId, Guid ownerScopeId, CancellationToken ct = default);
}
