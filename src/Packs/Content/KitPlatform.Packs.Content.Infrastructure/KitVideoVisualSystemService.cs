using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoVisualSystemService : IKitVideoVisualSystemService
{
    private readonly KitVideoVisualSystemRepository _repo;

    public KitVideoVisualSystemService(KitVideoVisualSystemRepository repo) => _repo = repo;

    public async Task<KitVideoVisualSystemDto?> GetAsync(
        string projectCode,
        string? systemCode = null,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        KitVideoVisualSystemRules.EnsureProject(projectCode);
        var row = await _repo.GetAsync(
            projectCode.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(systemCode) ? KitVideoVisualSystemRules.SystemCode : systemCode.Trim(),
            string.IsNullOrWhiteSpace(version) ? KitVideoVisualSystemRules.DocVersion : version.Trim(),
            cancellationToken);
        return row is null ? null : ToDto(row);
    }

    public async Task<KitVideoVisualSystemDto> LockAsync(Guid id, string? actor, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Visual System không tồn tại.");
        if (row.Status.Equals("locked", StringComparison.OrdinalIgnoreCase))
            return ToDto(row);
        await _repo.LockAsync(id, actor, cancellationToken);
        return ToDto(await _repo.GetByIdAsync(id, cancellationToken) ?? row);
    }

    private static KitVideoVisualSystemDto ToDto(KitVideoVisualSystemRepository.Row row)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(row.RulesJson) ? "{}" : row.RulesJson);
        return new KitVideoVisualSystemDto(
            row.Id, row.ProjectId, row.ProjectCode, row.SystemCode, row.Version, row.Status,
            doc.RootElement.Clone(), row.LockedAt, row.LockedBy);
    }
}
