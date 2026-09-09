using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoContinuityService : IKitVideoContinuityService
{
    private readonly KitVideoContinuityRepository _repo;
    private readonly IContinuityValidator _validator;

    public KitVideoContinuityService(KitVideoContinuityRepository repo, IContinuityValidator validator)
    {
        _repo = repo;
        _validator = validator;
    }

    public async Task<KitVideoStoryGraphDto?> GetAsync(Guid productionId, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetAsync(productionId, cancellationToken);
        if (row is null) return null;
        return await Map(row, cancellationToken);
    }

    public async Task<KitVideoStoryGraphDto> CompileAsync(
        KitVideoStoryCompileRequest request,
        CancellationToken cancellationToken = default)
    {
        var script = request.Script ?? "";
        var graph = KitVideoStoryCompiler.Compile(script);
        var hash = KitVideoStoryCompiler.ScriptHash(script);
        await _repo.UpsertAsync(request.ProductionId, script, hash, graph.GetRawText(), cancellationToken);
        return (await GetAsync(request.ProductionId, cancellationToken))!;
    }

    public KitVideoContinuityResult Validate(KitVideoContinuityValidateRequest request) =>
        _validator.Validate(request.Shot, request.PreviousSnapshot);

    public KitVideoStoryGateResult Gate(KitVideoStoryGateRequest request) =>
        KitVideoContinuityRules.PreGenerationGate(request.Shot, request.DetectedCharacters);

    public async Task<KitVideoContinuityOverrideDto> OverrideAsync(
        KitVideoContinuityOverrideRequest request,
        string? approvedBy,
        CancellationToken cancellationToken = default)
    {
        var actor = (approvedBy ?? "").Trim();
        KitVideoContinuityRules.EnsureOverride(request.Reason, actor);
        await _repo.InsertOverrideAsync(request.ProductionId, request.ShotId, request.Reason.Trim(), actor, cancellationToken);
        return new KitVideoContinuityOverrideDto(request.ShotId, request.Reason.Trim(), actor, DateTimeOffset.UtcNow);
    }

    public KitVideoScriptImpactResult Impact(KitVideoScriptImpactRequest request) =>
        KitVideoStoryCompiler.Impact(request.PreviousScript, request.NextScript);

    private async Task<KitVideoStoryGraphDto> Map(KitVideoContinuityRepository.GraphRow row, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(row.GraphJson) ? "{}" : row.GraphJson);
        var graph = doc.RootElement.Clone();
        var unassigned = graph.ValueKind == JsonValueKind.Object
            && graph.TryGetProperty("unassigned", out var u)
            && u.ValueKind == JsonValueKind.Array
            ? u.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s.Length > 0).ToList()
            : [];
        var overrides = (await _repo.ListOverridesAsync(row.ProductionId, ct))
            .Select(o => new KitVideoContinuityOverrideDto(o.ShotId, o.Reason, o.ApprovedBy, o.CreatedAt))
            .ToList();
        return new KitVideoStoryGraphDto(row.ProductionId, row.ScriptHash, graph, unassigned, overrides);
    }
}
