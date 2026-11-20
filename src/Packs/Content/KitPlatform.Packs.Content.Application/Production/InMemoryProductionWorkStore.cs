namespace KitPlatform.Packs.Content.Production;

public sealed class InMemoryProductionWorkStore : IProductionWorkStore
{
    readonly object _gate = new();
    readonly SemaphoreSlim _transaction = new(1, 1);
    readonly AsyncLocal<int> _transactionDepth = new();
    readonly IProductionCallerTenant? _caller;
    readonly Dictionary<Guid, Guid> _owner = [];
    readonly Dictionary<Guid, Guid> _buildTenant = [];
    readonly Dictionary<Guid, ProductionRequest> _requests = [];

    public InMemoryProductionWorkStore(IProductionCallerTenant? caller = null) => _caller = caller;

    public void RegisterBuild(Guid buildId, Guid tenantId)
    {
        ProductionTenantBoundary.Require(tenantId);
        lock (_gate) _buildTenant[buildId] = tenantId;
    }

    Guid? ActiveTenant() => _caller is null ? null : _caller.RequireTenant();

    bool Visible(Guid id)
    {
        var tenant = ActiveTenant();
        if (tenant is null) return true;
        return _owner.TryGetValue(id, out var owner) && owner == tenant.Value;
    }

    void RequireVisible(Guid id)
    {
        if (_caller is not null && !Visible(id))
            throw new InvalidOperationException(ProductionTenantBoundary.Forbidden);
    }

    void GuardNew(Guid id, Guid? parentId, Guid? buildId, string? storageKey)
    {
        var tenant = ActiveTenant();
        if (tenant is null) return;
        if (buildId is Guid build)
            ProductionBuildTenantGate.Require(tenant.Value, _buildTenant.TryGetValue(build, out var known) ? known : null);
        if (parentId is Guid parent && !(_owner.TryGetValue(parent, out var parentOwner) && parentOwner == tenant.Value))
            throw new InvalidOperationException(ProductionTenantBoundary.Forbidden);
        if (storageKey is not null && !ProductionTenantBoundary.OwnsStorageKey(tenant.Value, storageKey))
            throw new InvalidOperationException(ProductionTenantBoundary.Forbidden);
        _owner[id] = tenant.Value;
    }

    string IdemKey(string key)
    {
        var tenant = ActiveTenant();
        return tenant is null ? key : tenant.Value.ToString("N") + "\n" + key;
    }
    readonly Dictionary<string, Guid> _idempotency = new(StringComparer.Ordinal);
    readonly Dictionary<Guid, ProductionTask> _tasks = [];
    readonly Dictionary<Guid, ProductionTaskInputSnapshot> _snapshots = [];
    readonly Dictionary<Guid, ProductionAttempt> _attempts = [];
    readonly Dictionary<Guid, ProductionArtifact> _artifacts = [];
    readonly Dictionary<Guid, ProductionBinding> _bindings = [];
    readonly List<ProductionTaskEvent> _events = [];
    readonly Dictionary<Guid, FilmAssembly> _assemblies = [];
    readonly List<FilmAssemblyShot> _assemblyShots = [];

    public Task<ProductionRequest?> GetRequestAsync(Guid requestId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (!Visible(requestId)) return Task.FromResult<ProductionRequest?>(null);
            return Task.FromResult(_requests.TryGetValue(requestId, out var r) ? r : null);
        }
    }

    public Task<ProductionRequest?> GetRequestByIdempotencyAsync(string idempotencyKey, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (!_idempotency.TryGetValue(IdemKey(idempotencyKey), out var id)) return Task.FromResult<ProductionRequest?>(null);
            return Task.FromResult(_requests.TryGetValue(id, out var r) && Visible(id) ? r : null);
        }
    }

    public Task InsertRequestAsync(ProductionRequest request, CancellationToken ct = default)
    {
        lock (_gate)
        {
            GuardNew(request.RequestId, null, request.Scope.BuildId, null);
            var idem = IdemKey(request.IdempotencyKey);
            if (_idempotency.ContainsKey(idem))
                throw new InvalidOperationException("IDEMPOTENCY_CONFLICT");
            _requests[request.RequestId] = request;
            _idempotency[idem] = request.RequestId;
        }
        return Task.CompletedTask;
    }

    public Task UpdateRequestAsync(ProductionRequest request, CancellationToken ct = default)
    {
        lock (_gate)
        {
            RequireVisible(request.RequestId);
            _requests[request.RequestId] = request;
        }
        return Task.CompletedTask;
    }

    public Task<ProductionTask?> GetTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        lock (_gate) return Task.FromResult(_tasks.TryGetValue(taskId, out var t) && Visible(taskId) ? t : null);
    }

    public Task<IReadOnlyList<ProductionTask>> ListTasksByRequestAsync(Guid requestId, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<ProductionTask>>(_tasks.Values.Where(t => t.RequestId == requestId && Visible(t.TaskId)).ToArray());
    }

    public Task<IReadOnlyList<ProductionTask>> ListTasksByScopeAsync(Guid buildId, string shotCode, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<ProductionTask>>(
                _tasks.Values.Where(t => t.Scope.BuildId == buildId && t.Scope.ShotCode == shotCode && Visible(t.TaskId)).ToArray());
    }

    public Task InsertTaskAsync(ProductionTask task, CancellationToken ct = default)
    {
        lock (_gate)
        {
            GuardNew(task.TaskId, task.RequestId, task.Scope.BuildId, null);
            _tasks[task.TaskId] = task;
        }
        return Task.CompletedTask;
    }

    public Task UpdateTaskAsync(ProductionTask task, CancellationToken ct = default)
    {
        lock (_gate)
        {
            RequireVisible(task.TaskId);
            _tasks[task.TaskId] = task;
        }
        return Task.CompletedTask;
    }

    public Task LockTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (!_tasks.ContainsKey(taskId) || !Visible(taskId))
                throw new InvalidOperationException("TASK_MISSING");
        }
        return Task.CompletedTask;
    }

    public Task<ProductionTaskInputSnapshot?> GetSnapshotAsync(Guid snapshotId, CancellationToken ct = default)
    {
        lock (_gate) return Task.FromResult(_snapshots.TryGetValue(snapshotId, out var s) && Visible(snapshotId) ? s : null);
    }

    public Task<ProductionTaskInputSnapshot?> GetSnapshotByTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult(_snapshots.Values.FirstOrDefault(s => s.TaskId == taskId && Visible(s.SnapshotId)));
    }

    public Task InsertSnapshotAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
    {
        lock (_gate)
        {
            GuardNew(snapshot.SnapshotId, snapshot.TaskId, null, null);
            if (_snapshots.Values.Any(s => s.TaskId == snapshot.TaskId))
                throw new InvalidOperationException("SNAPSHOT_IMMUTABLE");
            _snapshots[snapshot.SnapshotId] = snapshot;
        }
        return Task.CompletedTask;
    }

    public Task<ProductionAttempt?> GetAttemptAsync(Guid attemptId, CancellationToken ct = default)
    {
        lock (_gate) return Task.FromResult(_attempts.TryGetValue(attemptId, out var a) && Visible(attemptId) ? a : null);
    }

    public Task<IReadOnlyList<ProductionAttempt>> ListAttemptsAsync(Guid taskId, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<ProductionAttempt>>(
                _attempts.Values.Where(a => a.TaskId == taskId && Visible(a.AttemptId)).OrderBy(a => a.AttemptNumber).ToArray());
    }

    public Task InsertAttemptAsync(ProductionAttempt attempt, CancellationToken ct = default)
    {
        lock (_gate)
        {
            GuardNew(attempt.AttemptId, attempt.TaskId, null, null);
            if (_attempts.Values.Any(a => a.TaskId == attempt.TaskId && a.AttemptNumber == attempt.AttemptNumber && Visible(a.AttemptId)))
                throw new InvalidOperationException("ATTEMPT_NUMBER_CONFLICT");
            _attempts[attempt.AttemptId] = attempt;
        }
        return Task.CompletedTask;
    }

    public Task UpdateAttemptAsync(ProductionAttempt attempt, CancellationToken ct = default)
    {
        lock (_gate)
        {
            RequireVisible(attempt.AttemptId);
            if (!_attempts.TryGetValue(attempt.AttemptId, out var prior))
                throw new InvalidOperationException("ATTEMPT_MISSING");
            if (prior.ProviderId != attempt.ProviderId || prior.ModelId != attempt.ModelId)
                throw new InvalidOperationException(ProductionFailureReasons.ProviderChanged);
            _attempts[attempt.AttemptId] = attempt;
        }
        return Task.CompletedTask;
    }

    public Task<ProductionArtifact?> GetArtifactAsync(Guid artifactId, CancellationToken ct = default)
    {
        lock (_gate) return Task.FromResult(_artifacts.TryGetValue(artifactId, out var a) && Visible(artifactId) ? a : null);
    }

    public Task<IReadOnlyList<ProductionArtifact>> ListArtifactsByTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<ProductionArtifact>>(
                _artifacts.Values.Where(a => a.TaskId == taskId && Visible(a.ArtifactId)).ToArray());
    }

    public Task InsertArtifactAsync(ProductionArtifact artifact, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (artifact.VerificationStatus == ProductionArtifactStatuses.Verified
                && (artifact.ByteSha256.Length != 64 || artifact.SizeBytes <= 0))
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
            if (ActiveTenant() is Guid tenant
                && !(_owner.TryGetValue(artifact.AttemptId, out var attemptOwner) && attemptOwner == tenant))
                throw new InvalidOperationException(ProductionTenantBoundary.Forbidden);
            if (_artifacts.TryGetValue(artifact.ArtifactId, out var prior))
            {
                if (!string.Equals(prior.ByteSha256, artifact.ByteSha256, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(prior.StorageKey, artifact.StorageKey, StringComparison.Ordinal))
                    throw new InvalidOperationException(ProductionArtifactPhysicalWrite.RowLocked);
                return Task.CompletedTask;
            }
            GuardNew(artifact.ArtifactId, artifact.TaskId, null, artifact.StorageKey);
            _artifacts[artifact.ArtifactId] = artifact;
        }
        return Task.CompletedTask;
    }

    public Task<ProductionBinding?> GetActiveBindingAsync(Guid buildId, string shotCode, string role, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult(_bindings.Values.FirstOrDefault(b =>
                b.BuildId == buildId && b.ShotCode == shotCode && b.Role == role && b.UnboundAt is null && Visible(b.BindingId)));
    }

    public Task<IReadOnlyList<ProductionBinding>> ListActiveBindingsAsync(Guid buildId, string shotCode, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<ProductionBinding>>(
                _bindings.Values.Where(b => b.BuildId == buildId && b.ShotCode == shotCode && b.UnboundAt is null && Visible(b.BindingId)).ToArray());
    }

    public Task InsertBindingAsync(ProductionBinding binding, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (_bindings.Values.Any(b =>
                    b.BuildId == binding.BuildId && b.ShotCode == binding.ShotCode
                    && b.Role == binding.Role && b.UnboundAt is null && Visible(b.BindingId)))
                throw new InvalidOperationException("ACTIVE_BINDING_EXISTS");
            if (!_artifacts.ContainsKey(binding.ArtifactId) || !Visible(binding.ArtifactId))
                throw new InvalidOperationException("BINDING_ARTIFACT_MISSING");
            if (ActiveTenant() is Guid tenant
                && !(_owner.TryGetValue(binding.BoundByTaskId, out var taskOwner) && taskOwner == tenant))
                throw new InvalidOperationException(ProductionTenantBoundary.Forbidden);
            GuardNew(binding.BindingId, binding.ArtifactId, binding.BuildId, null);
            _bindings[binding.BindingId] = binding;
        }
        return Task.CompletedTask;
    }

    public Task UnbindAsync(Guid bindingId, DateTimeOffset unboundAt, CancellationToken ct = default)
    {
        lock (_gate)
        {
            RequireVisible(bindingId);
            if (_caller is not null && !_bindings.ContainsKey(bindingId))
                throw new InvalidOperationException(ProductionTenantBoundary.Forbidden);
            if (_bindings.TryGetValue(bindingId, out var b))
                _bindings[bindingId] = b with { UnboundAt = unboundAt };
        }
        return Task.CompletedTask;
    }

    public Task InsertEventAsync(ProductionTaskEvent ev, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (ev.TaskId is Guid taskId)
                RequireVisible(taskId);
            GuardNew(ev.EventId, ev.TaskId, null, null);
            _events.Add(ev);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ProductionTaskEvent>> ListEventsByTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<ProductionTaskEvent>>(_events.Where(e => e.TaskId == taskId && Visible(e.EventId)).ToArray());
    }

    public Task InsertAssemblyAsync(FilmAssembly assembly, CancellationToken ct = default)
    {
        lock (_gate)
        {
            GuardNew(assembly.AssemblyId, null, assembly.BuildId, null);
            _assemblies[assembly.AssemblyId] = assembly;
        }
        return Task.CompletedTask;
    }

    public Task InsertAssemblyShotAsync(FilmAssemblyShot shot, CancellationToken ct = default)
    {
        lock (_gate)
        {
            RequireVisible(shot.AssemblyId);
            if (!Visible(shot.ArtifactId))
                throw new InvalidOperationException(ProductionTenantBoundary.Forbidden);
            GuardNew(shot.AssemblyShotId, shot.AssemblyId, null, null);
            _assemblyShots.Add(shot);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FilmAssemblyShot>> ListAssemblyShotsAsync(Guid assemblyId, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<FilmAssemblyShot>>(
                _assemblyShots.Where(s => s.AssemblyId == assemblyId && Visible(s.AssemblyShotId)).OrderBy(s => s.Order).ToArray());
    }

    public Task<IReadOnlyList<FilmAssembly>> ListAssembliesByBuildAsync(Guid buildId, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<FilmAssembly>>(
                _assemblies.Values.Where(a => a.BuildId == buildId && Visible(a.AssemblyId)).OrderBy(a => a.CreatedAt).ToArray());
    }

    public async Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default)
    {
        if (_transactionDepth.Value > 0)
            return await work(ct);
        await _transaction.WaitAsync(ct);
        _transactionDepth.Value = 1;
        try
        {
            return await work(ct);
        }
        finally
        {
            _transactionDepth.Value = 0;
            _transaction.Release();
        }
    }
}
