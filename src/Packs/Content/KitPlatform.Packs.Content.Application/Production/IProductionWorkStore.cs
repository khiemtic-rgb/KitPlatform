namespace KitPlatform.Packs.Content.Production;

public interface IProductionWorkStore
{
    Task<ProductionRequest?> GetRequestAsync(Guid requestId, CancellationToken ct = default);
    Task<ProductionRequest?> GetRequestByIdempotencyAsync(string idempotencyKey, CancellationToken ct = default);
    Task InsertRequestAsync(ProductionRequest request, CancellationToken ct = default);
    Task UpdateRequestAsync(ProductionRequest request, CancellationToken ct = default);

    Task<ProductionTask?> GetTaskAsync(Guid taskId, CancellationToken ct = default);
    Task<IReadOnlyList<ProductionTask>> ListTasksByRequestAsync(Guid requestId, CancellationToken ct = default);
    Task<IReadOnlyList<ProductionTask>> ListTasksByScopeAsync(Guid buildId, string shotCode, CancellationToken ct = default);
    Task InsertTaskAsync(ProductionTask task, CancellationToken ct = default);
    Task UpdateTaskAsync(ProductionTask task, CancellationToken ct = default);
    Task LockTaskAsync(Guid taskId, CancellationToken ct = default);

    Task<ProductionTaskInputSnapshot?> GetSnapshotAsync(Guid snapshotId, CancellationToken ct = default);
    Task<ProductionTaskInputSnapshot?> GetSnapshotByTaskAsync(Guid taskId, CancellationToken ct = default);
    Task InsertSnapshotAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default);

    Task<ProductionAttempt?> GetAttemptAsync(Guid attemptId, CancellationToken ct = default);
    Task<IReadOnlyList<ProductionAttempt>> ListAttemptsAsync(Guid taskId, CancellationToken ct = default);
    Task InsertAttemptAsync(ProductionAttempt attempt, CancellationToken ct = default);
    Task UpdateAttemptAsync(ProductionAttempt attempt, CancellationToken ct = default);

    Task<ProductionArtifact?> GetArtifactAsync(Guid artifactId, CancellationToken ct = default);
    Task<IReadOnlyList<ProductionArtifact>> ListArtifactsByTaskAsync(Guid taskId, CancellationToken ct = default);
    Task InsertArtifactAsync(ProductionArtifact artifact, CancellationToken ct = default);

    Task<ProductionBinding?> GetActiveBindingAsync(Guid buildId, string shotCode, string role, CancellationToken ct = default);
    Task<IReadOnlyList<ProductionBinding>> ListActiveBindingsAsync(Guid buildId, string shotCode, CancellationToken ct = default);
    Task InsertBindingAsync(ProductionBinding binding, CancellationToken ct = default);
    Task UnbindAsync(Guid bindingId, DateTimeOffset unboundAt, CancellationToken ct = default);

    Task InsertEventAsync(ProductionTaskEvent ev, CancellationToken ct = default);
    Task<IReadOnlyList<ProductionTaskEvent>> ListEventsByTaskAsync(Guid taskId, CancellationToken ct = default);

    Task InsertAssemblyAsync(FilmAssembly assembly, CancellationToken ct = default);
    Task InsertAssemblyShotAsync(FilmAssemblyShot shot, CancellationToken ct = default);
    Task<IReadOnlyList<FilmAssemblyShot>> ListAssemblyShotsAsync(Guid assemblyId, CancellationToken ct = default);
    Task<IReadOnlyList<FilmAssembly>> ListAssembliesByBuildAsync(Guid buildId, CancellationToken ct = default);

    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default);
}
