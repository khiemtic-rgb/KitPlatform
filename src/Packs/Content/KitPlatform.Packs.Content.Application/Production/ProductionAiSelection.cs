using System.Security.Cryptography;
using System.Text;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionAiSelectionStages
{
    public const string Video = "VIDEO";
    public const string Voice = "VOICE";
}

public static class ProductionAiSelectionModes
{
    public const string Explicit = "EXPLICIT";
}

public static class ProductionAiSelectionStatuses
{
    public const string Active = "ACTIVE";
    public const string Superseded = "SUPERSEDED";
}

public sealed record ProductionAiSelection(
    Guid SelectionId,
    Guid BuildId,
    string ShotCode,
    string EpisodeCode,
    string SceneCode,
    string Stage,
    string ProviderId,
    string ModelId,
    string SelectionMode,
    string SelectedBy,
    DateTimeOffset SelectedAt,
    string Reason,
    string Status,
    string SelectionHash,
    string? VoiceId = null);

public sealed record UpsertProductionAiSelectionCommand(
    Guid BuildId,
    string ShotCode,
    string EpisodeCode,
    string SceneCode,
    string Stage,
    string ProviderId,
    string ModelId,
    string SelectedBy,
    string Reason,
    string? VoiceId = null);

public sealed record SupersedeProductionAiSelectionCommand(
    Guid ExpectedSelectionId,
    UpsertProductionAiSelectionCommand Next);

public sealed record ProductionAiSelectionSupersedeResult(
    ProductionAiSelection Superseded,
    ProductionAiSelection Active);

public interface IProductionAiSelectionStore
{
    Task<ProductionAiSelection> UpsertActiveAsync(UpsertProductionAiSelectionCommand command, CancellationToken ct = default);
    Task<ProductionAiSelectionSupersedeResult> SupersedeActiveAsync(
        SupersedeProductionAiSelectionCommand command, CancellationToken ct = default);
    Task<ProductionAiSelection?> GetActiveAsync(Guid buildId, string shotCode, string stage, CancellationToken ct = default);
}

public sealed class InMemoryProductionAiSelectionStore : IProductionAiSelectionStore
{
    readonly object _gate = new();
    readonly List<ProductionAiSelection> _rows = [];

    public Task<ProductionAiSelection> UpsertActiveAsync(UpsertProductionAiSelectionCommand command, CancellationToken ct = default)
    {
        var row = ProductionAiSelectionCanon.Create(command);
        lock (_gate)
        {
            var existing = _rows.FirstOrDefault(r =>
                r.BuildId == row.BuildId
                && string.Equals(r.ShotCode, row.ShotCode, StringComparison.Ordinal)
                && string.Equals(r.Stage, row.Stage, StringComparison.Ordinal)
                && r.Status == ProductionAiSelectionStatuses.Active);
            if (existing is not null)
            {
                if (!string.Equals(existing.SelectionHash, row.SelectionHash, StringComparison.Ordinal))
                    throw new InvalidOperationException(ProductionFailureReasons.SelectionConflict);
                return Task.FromResult(existing);
            }
            _rows.Add(row);
            return Task.FromResult(row);
        }
    }

    public Task<ProductionAiSelectionSupersedeResult> SupersedeActiveAsync(
        SupersedeProductionAiSelectionCommand command, CancellationToken ct = default)
    {
        var next = ProductionAiSelectionCanon.Create(command.Next);
        lock (_gate)
        {
            var index = _rows.FindIndex(r =>
                r.BuildId == next.BuildId
                && string.Equals(r.ShotCode, next.ShotCode, StringComparison.Ordinal)
                && string.Equals(r.Stage, next.Stage, StringComparison.Ordinal)
                && r.Status == ProductionAiSelectionStatuses.Active);
            if (index < 0)
                throw new InvalidOperationException(ProductionFailureReasons.MissingProductionAiSelection);
            var existing = _rows[index];
            if (existing.SelectionId != command.ExpectedSelectionId)
                throw new InvalidOperationException(ProductionFailureReasons.SelectionConflict);
            if (string.Equals(existing.SelectionHash, next.SelectionHash, StringComparison.Ordinal))
                return Task.FromResult(new ProductionAiSelectionSupersedeResult(existing, existing));
            var superseded = existing with { Status = ProductionAiSelectionStatuses.Superseded };
            var active = next with { Reason = AuditReason(next.Reason, existing.SelectionId) };
            _rows[index] = superseded;
            _rows.Add(active);
            return Task.FromResult(new ProductionAiSelectionSupersedeResult(superseded, active));
        }
    }

    static string AuditReason(string reason, Guid previousSelectionId)
    {
        var text = (reason ?? "").Trim();
        if (text.Length == 0)
            throw new InvalidOperationException(ProductionFailureReasons.SelectionConflict);
        var marker = "supersedes=" + previousSelectionId.ToString("D");
        return text.Contains(marker, StringComparison.OrdinalIgnoreCase) ? text : text + "; " + marker;
    }

    public Task<ProductionAiSelection?> GetActiveAsync(Guid buildId, string shotCode, string stage, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_rows.FirstOrDefault(r =>
                r.BuildId == buildId
                && string.Equals(r.ShotCode, shotCode, StringComparison.Ordinal)
                && string.Equals(r.Stage, stage, StringComparison.Ordinal)
                && r.Status == ProductionAiSelectionStatuses.Active));
        }
    }

    public void AddExisting(ProductionAiSelection row)
    {
        lock (_gate) _rows.Add(row);
    }
}

public static class ProductionAiSelectionCanon
{
    public static ProductionAiSelection Create(UpsertProductionAiSelectionCommand command)
    {
        var stage = (command.Stage ?? "").Trim().ToUpperInvariant();
        var provider = (command.ProviderId ?? "").Trim();
        var model = (command.ModelId ?? "").Trim();
        var shot = (command.ShotCode ?? "").Trim();
        var voice = (command.VoiceId ?? "").Trim();
        var mode = ProductionAiSelectionModes.Explicit;
        if (stage.Length == 0 || provider.Length == 0 || model.Length == 0 || shot.Length == 0)
            throw new InvalidOperationException(ProductionFailureReasons.MissingProductionAiSelection);
        if (stage == ProductionAiSelectionStages.Voice && voice.Length == 0)
            throw new InvalidOperationException(ProductionFailureReasons.VoiceIdProductionDecisionRequired);
        var reason = (command.Reason ?? "").Trim();
        if (voice.Length > 0 && ProductionVoiceSelectionCanon.VoiceIdOf(reason).Length == 0)
            reason = string.IsNullOrWhiteSpace(reason)
                ? ProductionVoiceSelectionCanon.ReasonOf(voice, "", "", "")
                : reason + ";" + ProductionVoiceSelectionCanon.VoiceRefKey + voice;
        var hash = HashOf(command.BuildId, shot, stage, provider, model, mode, voice);
        return new ProductionAiSelection(
            Guid.NewGuid(),
            command.BuildId,
            shot,
            (command.EpisodeCode ?? "").Trim(),
            (command.SceneCode ?? "").Trim(),
            stage,
            provider,
            model,
            mode,
            (command.SelectedBy ?? "").Trim(),
            DateTimeOffset.UtcNow,
            reason,
            ProductionAiSelectionStatuses.Active,
            hash,
            voice.Length == 0 ? null : voice);
    }

    public static string HashOf(Guid buildId, string shotCode, string stage, string providerId, string modelId, string mode, string? voiceId = null)
    {
        var parts = new List<string>
        {
            buildId.ToString("D"),
            (shotCode ?? "").Trim(),
            (stage ?? "").Trim().ToUpperInvariant(),
            (providerId ?? "").Trim().ToLowerInvariant(),
            (modelId ?? "").Trim(),
            (mode ?? "").Trim().ToUpperInvariant(),
        };
        var voice = (voiceId ?? "").Trim();
        if (voice.Length > 0) parts.Add(voice);
        return "SELECTION_HASH_V1:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', parts)))).ToLowerInvariant();
    }
}
