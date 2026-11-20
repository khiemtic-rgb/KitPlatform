using System.Security.Cryptography;
using System.Text;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionExecutionPermitStatuses
{
    public const string Issued = "ISSUED";
    public const string Consumed = "CONSUMED";
    public const string Expired = "EXPIRED";
    public const string Revoked = "REVOKED";
}

public sealed record ProductionExecutionPermit(
    Guid PermitId,
    Guid BuildId,
    string ShotCode,
    string EpisodeCode,
    string SceneCode,
    string Action,
    string ProviderId,
    string ModelId,
    string Status,
    string IssuedBy,
    DateTimeOffset IssuedAt,
    DateTimeOffset? ConsumedAt,
    string Reason,
    string PermitHash);

public sealed record IssueProductionExecutionPermitCommand(
    Guid BuildId,
    string ShotCode,
    string EpisodeCode,
    string SceneCode,
    string Action,
    string ProviderId,
    string ModelId,
    string IssuedBy,
    string Reason);

public interface IProductionExecutionPermitStore
{
    Task<ProductionExecutionPermit> IssueAsync(IssueProductionExecutionPermitCommand command, CancellationToken ct = default);
    Task<ProductionExecutionPermit?> GetIssuedAsync(
        Guid buildId, string shotCode, string action, string providerId, string modelId, CancellationToken ct = default);
    Task<ProductionExecutionPermit?> GetLatestAsync(
        Guid buildId, string shotCode, string action, string providerId, string modelId, CancellationToken ct = default);
    Task<bool> TryConsumeAsync(
        Guid buildId, string shotCode, string action, string providerId, string modelId, CancellationToken ct = default);
    Task<IReadOnlyList<ProductionExecutionPermit>> ListByTargetAsync(
        Guid buildId, string shotCode, string action, string providerId, string modelId, CancellationToken ct = default);
}

public sealed class InMemoryProductionExecutionPermitStore : IProductionExecutionPermitStore
{
    readonly object _gate = new();
    readonly List<ProductionExecutionPermit> _rows = [];

    public Task<ProductionExecutionPermit> IssueAsync(IssueProductionExecutionPermitCommand command, CancellationToken ct = default)
    {
        var row = ProductionExecutionPermitCanon.Create(command);
        lock (_gate)
        {
            var existing = FindIssued(row.BuildId, row.ShotCode, row.Action, row.ProviderId, row.ModelId);
            if (existing is not null)
            {
                if (!string.Equals(existing.PermitHash, row.PermitHash, StringComparison.Ordinal))
                    throw new InvalidOperationException(ProductionFailureReasons.PermitConflict);
                return Task.FromResult(existing);
            }
            _rows.Add(row);
            return Task.FromResult(row);
        }
    }

    public Task<ProductionExecutionPermit?> GetIssuedAsync(
        Guid buildId, string shotCode, string action, string providerId, string modelId, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult(FindIssued(buildId, shotCode, action, providerId, modelId));
    }

    public Task<ProductionExecutionPermit?> GetLatestAsync(
        Guid buildId, string shotCode, string action, string providerId, string modelId, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult(_rows
                .Where(r => SameScope(r, buildId, shotCode, action, providerId, modelId))
                .OrderByDescending(r => r.IssuedAt)
                .FirstOrDefault());
    }

    public Task<bool> TryConsumeAsync(
        Guid buildId, string shotCode, string action, string providerId, string modelId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var existing = FindIssued(buildId, shotCode, action, providerId, modelId);
            if (existing is null) return Task.FromResult(false);
            var idx = _rows.IndexOf(existing);
            _rows[idx] = existing with
            {
                Status = ProductionExecutionPermitStatuses.Consumed,
                ConsumedAt = DateTimeOffset.UtcNow,
            };
            return Task.FromResult(true);
        }
    }

    public Task<IReadOnlyList<ProductionExecutionPermit>> ListByTargetAsync(
        Guid buildId, string shotCode, string action, string providerId, string modelId, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<ProductionExecutionPermit>>(
                _rows.Where(r => SameScope(r, buildId, shotCode, action, providerId, modelId)).ToArray());
    }

    public void AddExisting(ProductionExecutionPermit permit)
    {
        lock (_gate) _rows.Add(permit);
    }

    ProductionExecutionPermit? FindIssued(Guid buildId, string shotCode, string action, string providerId, string modelId) =>
        _rows.FirstOrDefault(r =>
            SameScope(r, buildId, shotCode, action, providerId, modelId)
            && r.Status == ProductionExecutionPermitStatuses.Issued);

    static bool SameScope(
        ProductionExecutionPermit r, Guid buildId, string shotCode, string action, string providerId, string modelId) =>
        r.BuildId == buildId
        && string.Equals(r.ShotCode, shotCode, StringComparison.Ordinal)
        && string.Equals(r.Action, action, StringComparison.Ordinal)
        && string.Equals(r.ProviderId, providerId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(r.ModelId, modelId, StringComparison.OrdinalIgnoreCase);
}

public static class ProductionExecutionPermitCanon
{
    public static bool Matches(
        ProductionExecutionPermit permit,
        Guid buildId,
        string shotCode,
        string action,
        string providerId,
        string modelId) =>
        permit.Status == ProductionExecutionPermitStatuses.Issued
        && permit.BuildId == buildId
        && string.Equals(permit.ShotCode, shotCode, StringComparison.Ordinal)
        && string.Equals(permit.Action, action, StringComparison.Ordinal)
        && string.Equals(permit.ProviderId, providerId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(permit.ModelId, modelId, StringComparison.OrdinalIgnoreCase);

    public static bool MatchesConsumed(
        ProductionExecutionPermit permit,
        Guid buildId,
        string shotCode,
        string action,
        string providerId,
        string modelId) =>
        permit.Status == ProductionExecutionPermitStatuses.Consumed
        && permit.BuildId == buildId
        && string.Equals(permit.ShotCode, shotCode, StringComparison.Ordinal)
        && string.Equals(permit.Action, action, StringComparison.Ordinal)
        && string.Equals(permit.ProviderId, providerId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(permit.ModelId, modelId, StringComparison.OrdinalIgnoreCase);

    public static ProductionExecutionPermit Create(IssueProductionExecutionPermitCommand command)
    {
        var action = (command.Action ?? "").Trim();
        var provider = (command.ProviderId ?? "").Trim();
        var model = (command.ModelId ?? "").Trim();
        var shot = (command.ShotCode ?? "").Trim();
        if (action.Length == 0 || provider.Length == 0 || model.Length == 0 || shot.Length == 0)
            throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
        var hash = HashOf(command.BuildId, shot, action, provider, model);
        return new ProductionExecutionPermit(
            Guid.NewGuid(),
            command.BuildId,
            shot,
            (command.EpisodeCode ?? "").Trim(),
            (command.SceneCode ?? "").Trim(),
            action,
            provider,
            model,
            ProductionExecutionPermitStatuses.Issued,
            (command.IssuedBy ?? "").Trim(),
            DateTimeOffset.UtcNow,
            null,
            (command.Reason ?? "").Trim(),
            hash);
    }

    public static string HashOf(Guid buildId, string shotCode, string action, string providerId, string modelId)
    {
        var canonical = string.Join('|',
            buildId.ToString("D"),
            (shotCode ?? "").Trim(),
            (action ?? "").Trim(),
            (providerId ?? "").Trim().ToLowerInvariant(),
            (modelId ?? "").Trim());
        return "PERMIT_HASH_V1:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}
