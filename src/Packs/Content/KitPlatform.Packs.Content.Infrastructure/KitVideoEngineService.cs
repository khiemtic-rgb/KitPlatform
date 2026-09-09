using System.Text.Json;
using System.Text.RegularExpressions;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoEngineService : IKitVideoEngineService
{
    private static readonly Regex CodeRe = new("^[A-Z][A-Z0-9_]{1,62}$", RegexOptions.Compiled);
    private readonly ContentRepository _repo;

    public KitVideoEngineService(ContentRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<KitVideoProjectDto>> ListProjectsAsync(CancellationToken cancellationToken = default)
    {
        var projects = await _repo.ListVideoProjectsAsync(cancellationToken);
        var universes = await _repo.ListVideoUniversesAsync(cancellationToken);
        return projects.Select(p => ToProject(p, universes.Where(u => u.ProjectId == p.Id).ToList())).ToList();
    }

    public async Task<KitVideoProjectDto> GetProjectAsync(string projectCode, CancellationToken cancellationToken = default)
    {
        var all = await ListProjectsAsync(cancellationToken);
        return all.FirstOrDefault(p => string.Equals(p.ProjectCode, Norm(projectCode), StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Video Engine không có project {Norm(projectCode)}.");
    }

    public async Task<IReadOnlyList<KitVideoProductionDto>> ListProductionsAsync(
        string? projectCode = null,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListVideoProductionsAsync(
            string.IsNullOrWhiteSpace(projectCode) ? null : Norm(projectCode),
            cancellationToken);
        var list = new List<KitVideoProductionDto>();
        foreach (var row in rows)
            list.Add(await ToProduction(row, cancellationToken));
        return list;
    }

    public async Task<KitVideoProductionDto> GetProductionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetVideoProductionAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Production không tồn tại.");
        return await ToProduction(row, cancellationToken);
    }

    public async Task<KitVideoProductionDto> EnsureProductionAsync(
        CreateKitVideoProductionRequest request,
        string? actor = null,
        CancellationToken cancellationToken = default)
    {
        var project = await GetProjectAsync(request.ProjectCode, cancellationToken);
        var universe = project.Universes.FirstOrDefault(u =>
            string.Equals(u.UniverseCode, Norm(request.UniverseCode), StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Universe {request.UniverseCode} không thuộc {project.ProjectCode}.");
        var code = Norm(request.ProductionCode);
        if (!CodeRe.IsMatch(code))
            throw new InvalidOperationException("Production code không hợp lệ.");
        var existing = (await _repo.ListVideoProductionsAsync(project.ProjectCode, cancellationToken))
            .FirstOrDefault(p => string.Equals(p.ProductionCode, code, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return await ToProduction(existing, cancellationToken);
        var row = await _repo.InsertVideoProductionAsync(
            Guid.NewGuid(),
            project.Id,
            universe.Id,
            code,
            (request.Title ?? "").Trim(),
            request.SeriesBuildId,
            cancellationToken);
        return await ToProduction(row, cancellationToken);
    }

    public async Task<KitVideoProductionDto> TransitionProductionAsync(
        Guid id,
        TransitionKitVideoProductionRequest request,
        string? actor = null,
        CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetVideoProductionAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Production không tồn tại.");
        var to = (request.ToState ?? "").Trim().ToUpperInvariant();
        KitVideoEngineStates.EnsureProduction(row.State, to);
        var updated = await _repo.UpdateVideoProductionStateAsync(
            id, to, actor, request.Reason, row.State, cancellationToken);
        return await ToProduction(updated, cancellationToken);
    }

    public async Task<KitVideoProductionDto> UpsertShotAsync(
        Guid productionId,
        string shotCode,
        UpsertKitVideoShotStateRequest request,
        CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetVideoProductionAsync(productionId, cancellationToken)
            ?? throw new InvalidOperationException("Production không tồn tại.");
        var shots = await _repo.ListVideoShotsAsync(productionId, cancellationToken);
        var code = (shotCode ?? "").Trim().ToUpperInvariant();
        if (code.Length == 0) throw new InvalidOperationException("Shot code bắt buộc.");
        var current = shots.FirstOrDefault(s => string.Equals(s.ShotCode, code, StringComparison.OrdinalIgnoreCase));
        var from = current?.State ?? "DRAFT";
        var to = (request.ToState ?? "").Trim().ToUpperInvariant();
        KitVideoEngineStates.EnsureShotMayCallProvider(from, to);
        var failed = request.Failed ?? string.Equals(to, "FAILED", StringComparison.OrdinalIgnoreCase);
        string? extra = null;
        if (!string.IsNullOrWhiteSpace(request.LastProvider) || !string.IsNullOrWhiteSpace(request.LastFailureCode))
            extra = KitVideoJobRepository.ShotExtra(request.LastProvider, request.LastFailureCode);
        await _repo.UpsertVideoShotStateAsync(productionId, code, to, failed, cancellationToken, extra);
        var shotsAfter = await _repo.ListVideoShotsAsync(productionId, cancellationToken);
        await _repo.UpdateVideoProductionRunStatusAsync(
            productionId,
            KitVideoEngineRules.RunStatusAfterShots(shotsAfter.Select(s => (s.State, s.Failed)).ToList()),
            cancellationToken);
        return await ToProduction(row, cancellationToken);
    }

    private async Task<KitVideoProductionDto> ToProduction(ContentRepository.VideoProductionRow row, CancellationToken ct)
    {
        var shots = await _repo.ListVideoShotsAsync(row.Id, ct);
        var run = string.IsNullOrWhiteSpace(row.RunStatus)
            ? KitVideoEngineRules.RunStatusAfterShots(shots.Select(s => (s.State, s.Failed)).ToList())
            : row.RunStatus;
        return new KitVideoProductionDto(
            row.Id,
            row.ProjectCode,
            row.UniverseCode,
            row.ProductionCode,
            row.Title,
            row.State,
            run,
            row.SeriesBuildId,
            shots.Select(s =>
            {
                var extra = ParseExtra(s.ExtraJson);
                return new KitVideoShotStateDto(s.ShotCode, s.State, s.Failed, extra.Provider, extra.Failure);
            }).ToList(),
            row.UpdatedAt);
    }

    private static (string? Provider, string? Failure) ParseExtra(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            var root = doc.RootElement;
            var provider = root.TryGetProperty("lastProvider", out var p) ? p.GetString() : null;
            var failure = root.TryGetProperty("lastFailureCode", out var f) ? f.GetString() : null;
            return (string.IsNullOrWhiteSpace(provider) ? null : provider, string.IsNullOrWhiteSpace(failure) ? null : failure);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static KitVideoProjectDto ToProject(
        ContentRepository.VideoProjectRow p,
        IReadOnlyList<ContentRepository.VideoUniverseRow> universes)
    {
        return new KitVideoProjectDto(
            p.Id,
            p.ProjectCode,
            p.Name,
            p.BrandCode,
            p.Status,
            Parse(p.VisualJson),
            Parse(p.RulesJson),
            universes.Select(u => new KitVideoUniverseDto(u.Id, u.UniverseCode, u.Name, u.Status, Parse(u.WorldJson))).ToList());
    }

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return doc.RootElement.Clone();
    }

    private static string Norm(string raw) => (raw ?? "").Trim().ToUpperInvariant();
}
