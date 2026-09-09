using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoAssetService : IKitVideoAssetService, IAssetResolver, IImagePromptCompiler
{
    private readonly KitVideoAssetRepository _repo;

    public KitVideoAssetService(KitVideoAssetRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<KitVideoAssetDto>> ListAsync(
        string? projectCode = null,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListAsync(projectCode, cancellationToken);
        return await MapMany(rows, cancellationToken);
    }

    public async Task<KitVideoAssetDto> GetAsync(
        string projectCode,
        string assetCode,
        CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetAsync(Norm(projectCode), NormCode(assetCode), cancellationToken)
            ?? throw new InvalidOperationException($"Asset {assetCode} không thuộc {projectCode}.");
        return (await MapMany([row], cancellationToken))[0];
    }

    public async Task<KitVideoAssetDto> CreateVersionAsync(
        string projectCode,
        string assetCode,
        CreateKitVideoAssetVersionRequest request,
        string? actor = null,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = actor;
        var row = await Require(projectCode, assetCode, cancellationToken);
        var next = KitVideoAssetRules.NextVersion(row.CurrentVersion);
        await _repo.InsertVersionAsync(
            Guid.NewGuid(), row.Id, next, row.CurrentEra, "DRAFT", row.CanonJson, cancellationToken);
        return await GetAsync(projectCode, assetCode, cancellationToken);
    }

    public async Task<KitVideoAssetDto> PutCanonAsync(
        string projectCode,
        string assetCode,
        UpsertKitVideoAssetCanonRequest request,
        CancellationToken cancellationToken = default)
    {
        var row = await Require(projectCode, assetCode, cancellationToken);
        KitVideoAssetRules.EnsureNotLocked(row.Lifecycle);
        if (row.VersionId is null)
            throw new InvalidOperationException("Asset chưa có version.");
        var json = request.Canon.GetRawText();
        if (json.Contains("data:", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Canon không chứa dataUrl.");
        await _repo.UpdateCanonAsync(row.VersionId.Value, json, cancellationToken);
        return await GetAsync(projectCode, assetCode, cancellationToken);
    }

    public async Task<KitVideoAssetDto> SetLifecycleAsync(
        string projectCode,
        string assetCode,
        string lifecycle,
        CancellationToken cancellationToken = default)
    {
        var to = (lifecycle ?? "").Trim().ToUpperInvariant();
        if (!KitVideoAssetRules.Lifecycle.Contains(to))
            throw new InvalidOperationException("Lifecycle không hợp lệ.");
        var row = await Require(projectCode, assetCode, cancellationToken);
        if (to == "LOCKED" && !string.Equals(row.Lifecycle, "APPROVED", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("APPROVE trước. APPROVE ≠ LOCK.");
        if (row.VersionId is null)
            throw new InvalidOperationException("Asset chưa có version.");
        await _repo.SetLifecycleAsync(row.Id, row.VersionId.Value, to, cancellationToken);
        return await GetAsync(projectCode, assetCode, cancellationToken);
    }

    public async Task<KitVideoAssetUsageDto> UsageAsync(
        string projectCode,
        string assetCode,
        CancellationToken cancellationToken = default)
    {
        var row = await Require(projectCode, assetCode, cancellationToken);
        var shots = await _repo.ListUsageAsync(row.Id, cancellationToken);
        var codes = shots.Select(s => s.ShotCode).ToList();
        return new KitVideoAssetUsageDto(row.AssetCode, codes.Count, KitVideoAssetRules.ImpactMessage(codes.Count), codes);
    }

    public async Task<KitVideoShotPackageDto> ResolveAsync(
        KitVideoResolveRequest request,
        CancellationToken cancellationToken = default)
    {
        var catalog = await ListAsync(request.ProjectCode, cancellationToken);
        return Resolve(request, catalog);
    }

    public KitVideoShotPackageDto Resolve(KitVideoResolveRequest request, IReadOnlyList<KitVideoAssetDto> catalog)
    {
        var spec = request.Spec;
        var blocked = new List<string>();
        var missing = new List<string>();
        KitVideoAssetDto? Find(string raw) =>
            catalog.FirstOrDefault(a =>
                string.Equals(a.AssetCode, raw, StringComparison.OrdinalIgnoreCase)
                || string.Equals(a.Name, raw, StringComparison.OrdinalIgnoreCase));

        var characters = new List<KitVideoAssetDto>();
        foreach (var raw in spec.RequiredCharacters)
        {
            var hit = Find(raw);
            if (hit is null)
            {
                missing.Add(raw);
                blocked.Add($"MISSING:{raw}");
                continue;
            }
            if (!KitVideoAssetRules.CanUseInProduction(hit.Lifecycle))
                blocked.Add($"NOT_APPROVED:{hit.AssetCode}");
            else
                characters.Add(hit);
            if (string.Equals(spec.Era, "ERA-02", StringComparison.OrdinalIgnoreCase)
                && hit.Eras.Any(e => e.Era == "ERA-02" && e.Status == "DRAFT"))
                blocked.Add($"ERA_NOT_READY:{hit.AssetCode}:ERA-02");
        }

        var locations = new List<KitVideoAssetDto>();
        if (!string.IsNullOrWhiteSpace(spec.RequiredLocation))
        {
            var loc = Find(spec.RequiredLocation);
            if (loc is null)
            {
                missing.Add(spec.RequiredLocation);
                blocked.Add($"MISSING:{spec.RequiredLocation}");
            }
            else if (!KitVideoAssetRules.CanUseInProduction(loc.Lifecycle))
                blocked.Add($"NOT_APPROVED:{loc.AssetCode}");
            else
                locations.Add(loc);
        }

        var props = new List<KitVideoAssetDto>();
        foreach (var raw in spec.RequiredProps ?? [])
        {
            var hit = Find(raw);
            if (hit is null)
            {
                missing.Add(raw);
                blocked.Add($"MISSING:{raw}");
            }
            else
                props.Add(hit);
        }

        var wardrobe = new List<KitVideoAssetDto>();
        foreach (var ch in characters)
        {
            var code = spec.RequiredWardrobe is not null && spec.RequiredWardrobe.TryGetValue(ch.AssetCode, out var w)
                ? w
                : ReadDefaultWardrobe(ch.Canon);
            if (string.IsNullOrWhiteSpace(code)) continue;
            var hit = Find(code);
            if (hit is not null) wardrobe.Add(hit);
            else
            {
                missing.Add(code);
                blocked.Add($"MISSING:{code}");
            }
        }

        if (request.SceneMaster is null)
            blocked.Add("SCENE_MASTER_MISSING");

        return new KitVideoShotPackageDto(
            spec.ShotCode,
            spec.SceneCode,
            characters,
            locations,
            props,
            wardrobe,
            blocked,
            missing,
            request.SceneMaster,
            request.Previous?.ShotCode);
    }

    public async Task<KitVideoPreflightDto> PreflightAsync(
        KitVideoResolveRequest request,
        CancellationToken cancellationToken = default)
    {
        var pkg = await ResolveAsync(request, cancellationToken);
        var blocked = pkg.Blocked.ToList();
        if (pkg.Characters.Any(c => c.AssetKind == "CHARACTER" && c.References.Count == 0))
            blocked.Add("REFERENCE_MISSING");
        if (pkg.SceneMaster is null)
            blocked.Add("SCENE_MASTER_MISSING");
        if (!request.Spec.ShotCode.EndsWith("-01", StringComparison.Ordinal)
            && request.Previous is not { Approved: true })
            blocked.Add("PREVIOUS_REQUIRED");
        return new KitVideoPreflightDto(blocked.Count == 0 && pkg.Missing.Count == 0, blocked.Distinct().ToList(), pkg.Missing);
    }

    public async Task<KitVideoCompiledPromptDto> CompileAsync(
        KitVideoResolveRequest request,
        CancellationToken cancellationToken = default)
    {
        var pkg = await ResolveAsync(request, cancellationToken);
        var project = (await ListAsync(request.ProjectCode, cancellationToken))
            .FirstOrDefault()?.ProjectCode;
        return Compile(pkg, request.Spec, project);
    }

    public KitVideoCompiledPromptDto Compile(KitVideoShotPackageDto package, KitVideoShotSpecDto spec, string? projectStyle)
    {
        if (!string.IsNullOrWhiteSpace(spec.I2vImageSource)
            && !spec.I2vImageSource.Equals("APPROVED_KEYFRAME", StringComparison.OrdinalIgnoreCase))
        {
            return new KitVideoCompiledPromptDto(false, "", false, "", "I2V must use APPROVED KEYFRAME — not character crop/sheet.");
        }
        var layers = package.Characters
            .Select(c => $"CharacterCanon {c.AssetCode}")
            .Concat(package.Locations.Select(l => $"LocationCanon {l.AssetCode}"))
            .ToList();
        try
        {
            var prompt = KitVideoAssetRules.CompileImagePrompt(new KitVideoCompileRequest(
                projectStyle,
                layers,
                spec.Action,
                spec.Camera,
                spec.Speaker,
                spec.Emotion,
                spec.Dialogue,
                spec.I2vImageSource ?? "APPROVED_KEYFRAME"));
            var hasDialogue = !string.IsNullOrWhiteSpace(spec.Dialogue) && prompt.Contains(spec.Dialogue, StringComparison.Ordinal);
            return new KitVideoCompiledPromptDto(!hasDialogue, prompt, hasDialogue, "APPROVED_KEYFRAME");
        }
        catch (InvalidOperationException ex)
        {
            return new KitVideoCompiledPromptDto(false, "", false, "", ex.Message);
        }
    }

    public Task<KitVideoKeyframeQa> QaAsync(KitVideoKeyframeQaInput request, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return Task.FromResult(KitVideoAssetRules.EvaluateKeyframe(request));
    }

    public async Task<KitVideoSceneMasterDto> UpsertSceneMasterAsync(
        UpsertKitVideoSceneMasterRequest request,
        CancellationToken cancellationToken = default)
    {
        var status = (request.Status ?? "DRAFT").Trim().ToUpperInvariant();
        if (status is not ("DRAFT" or "LOCKED"))
            throw new InvalidOperationException("Scene Master chỉ DRAFT hoặc LOCKED.");
        var canon = request.Canon?.GetRawText() ?? "{}";
        var presence = request.Presence?.GetRawText() ?? "[]";
        await _repo.UpsertSceneMasterAsync(
            request.ProductionId,
            NormCode(request.SceneCode),
            NormCode(request.LocationCode),
            status,
            canon,
            presence,
            cancellationToken);
        return new KitVideoSceneMasterDto(
            NormCode(request.SceneCode),
            status,
            NormCode(request.LocationCode),
            request.Canon ?? JsonDocument.Parse("{}").RootElement.Clone(),
            request.Presence ?? JsonDocument.Parse("[]").RootElement.Clone());
    }

    public async Task ApproveSnapshotAsync(
        ApproveKitVideoSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        await _repo.InsertSnapshotAsync(
            request.ProductionId,
            NormCode(request.ShotCode),
            request.Snapshot.GetRawText(),
            cancellationToken);
    }

    private async Task<KitVideoAssetRepository.AssetRow> Require(string project, string code, CancellationToken ct) =>
        await _repo.GetAsync(Norm(project), NormCode(code), ct)
        ?? throw new InvalidOperationException($"Asset {code} không thuộc {project}.");

    private async Task<IReadOnlyList<KitVideoAssetDto>> MapMany(
        IReadOnlyList<KitVideoAssetRepository.AssetRow> rows,
        CancellationToken ct)
    {
        var refs = await _repo.ListRefsAsync(rows.Where(r => r.VersionId.HasValue).Select(r => r.VersionId!.Value).ToList(), ct);
        var list = new List<KitVideoAssetDto>(rows.Count);
        foreach (var row in rows)
        {
            var eras = await _repo.ListErasAsync(row.Id, ct);
            var ageOf = (string json) =>
            {
                try
                {
                    using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
                    if (doc.RootElement.TryGetProperty("identity", out var id)
                        && id.TryGetProperty("age", out var age)
                        && age.TryGetInt32(out var n))
                        return (int?)n;
                }
                catch (JsonException)
                {
                    /* structured canon only */
                }
                return (int?)null;
            };
            list.Add(new KitVideoAssetDto(
                row.Id,
                row.ProjectCode,
                row.AssetCode,
                row.AssetKind,
                row.Name,
                row.Lifecycle,
                row.Version,
                row.Era,
                Parse(row.CanonJson),
                refs.Where(r => r.VersionId == row.VersionId)
                    .Select(r => new KitVideoAssetRefDto(r.Kind, r.Path, r.IsPrimary, r.IsSecondary, r.QaStatus))
                    .ToList(),
                eras.Where(e => !string.IsNullOrWhiteSpace(e.Era))
                    .GroupBy(e => e.Era)
                    .Select(g => new KitVideoAssetEraDto(g.Key, ageOf(g.First().CanonJson), g.First().Status))
                    .ToList()));
        }
        return list;
    }

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return doc.RootElement.Clone();
    }

    private static string? ReadDefaultWardrobe(JsonElement canon)
    {
        if (canon.ValueKind == JsonValueKind.Object
            && canon.TryGetProperty("wardrobe", out var w)
            && w.TryGetProperty("defaultId", out var id))
            return id.GetString();
        return null;
    }

    private static string Norm(string raw) => (raw ?? "").Trim().ToUpperInvariant();
    private static string NormCode(string raw) => (raw ?? "").Trim().ToUpperInvariant();
}
