using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ProductionProgressService : IProductionProgressService
{
    private readonly ContentRepository _repo;
    private readonly ICharacterProductionLibraryService _library;
    private readonly ImageGenerationExecutionRepository _stills;
    private readonly ImageGenerationDirectorReviewRepository _reviews;
    private readonly ProductionVideoContractRepository _videos;
    private readonly VideoGenerationExecutionRepository _vge;

    public ProductionProgressService(
        ContentRepository repo,
        ICharacterProductionLibraryService library,
        ImageGenerationExecutionRepository stills,
        ImageGenerationDirectorReviewRepository reviews,
        ProductionVideoContractRepository videos,
        VideoGenerationExecutionRepository vge)
    {
        _repo = repo;
        _library = library;
        _stills = stills;
        _reviews = reviews;
        _videos = videos;
        _vge = vge;
    }

    public IReadOnlyList<string> RunRegression() => ProductionProgressV1Regression.Run();

    public async Task<ProductionProgressDto> GetBuildAsync(Guid buildId, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetSeriesBuildAsync(buildId, cancellationToken)
            ?? throw new InvalidOperationException("PRODUCTION_PROGRESS_NOT_FOUND");
        return await ResolveRowAsync(row, cancellationToken);
    }

    public async Task<ProductionProgressListDto> ListAsync(string seriesCode, CancellationToken cancellationToken = default)
    {
        var code = string.IsNullOrWhiteSpace(seriesCode) ? "FAMIXA" : seriesCode.Trim();
        var rows = await _repo.ListSeriesBuildsWithGraphAsync(code, cancellationToken);
        var items = new List<ProductionProgressDto>();
        foreach (var row in rows)
            items.Add(await ResolveRowAsync(row, cancellationToken));
        return new ProductionProgressListDto(items, items.Count, false);
    }

    private async Task<ProductionProgressDto> ResolveRowAsync(ContentRepository.SeriesBuildRow row, CancellationToken ct)
    {
        var library = await _library.ListAsync(null, "all", "series", "ERA-01", ct);
        var known = (library.Items ?? [])
            .Select(x => new ProductionProgressRules.CharacterFact(
                x.CharacterId,
                string.IsNullOrWhiteSpace(x.DisplayName) ? x.Name : x.DisplayName,
                x.CanUse,
                x.MissingTypes ?? []))
            .ToList();
        var fact = await BuildFactAsync(row, known, ct);
        return ToDto(ProductionProgressRules.Resolve(fact));
    }

    private async Task<ProductionProgressRules.EpisodeFact> BuildFactAsync(
        ContentRepository.SeriesBuildRow row,
        IReadOnlyList<ProductionProgressRules.CharacterFact> known,
        CancellationToken ct)
    {
        JsonNode? node;
        try { node = JsonNode.Parse(string.IsNullOrWhiteSpace(row.GraphJson) ? "{}" : row.GraphJson); }
        catch (JsonException) { node = new JsonObject(); }
        var obj = node as JsonObject ?? [];
        var episode = obj["episode"] as JsonObject;
        var scriptLocked = Flag(obj["scriptLocked"]);
        var published = Flag(obj["published"]);
        var shots = ParseShots(episode?["shots"] as JsonArray);
        var scenes = ParseScenes(obj["scenes"] as JsonArray, shots);
        var used = scenes.SelectMany(s => s.CharacterIds)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var characters = used
            .Select(id => known.FirstOrDefault(k => string.Equals(k.CharacterId, id, StringComparison.OrdinalIgnoreCase))
                ?? new ProductionProgressRules.CharacterFact(id, id, false, []))
            .ToList();

        var resolvedScenes = new List<ProductionProgressRules.SceneFact>();
        foreach (var scene in scenes)
        {
            var resolvedShots = new List<ProductionProgressRules.ShotFact>();
            foreach (var shot in scene.Shots)
                resolvedShots.Add(await BindShotAsync(shot, ct));
            resolvedScenes.Add(scene with { Shots = resolvedShots });
        }

        return new ProductionProgressRules.EpisodeFact(
            row.Id,
            row.SeriesCode,
            row.EpisodeCode,
            string.IsNullOrWhiteSpace(row.Title) ? (episode?["title"]?.ToString() ?? "Tập") : row.Title,
            scriptLocked,
            resolvedScenes,
            characters,
            row.KfCount,
            row.VideoCount,
            published);
    }

    private async Task<ProductionProgressRules.ShotFact> BindShotAsync(ProductionProgressRules.ShotFact shot, CancellationToken ct)
    {
        if (!Guid.TryParse(shot.ShotId, out var productionId))
            return shot;
        var still = await _stills.GetLatestAsync(productionId, ct);
        var review = await _reviews.GetLatestAsync(productionId, ct);
        var contract = await _videos.GetLatestAsync(productionId, ct);
        var video = await _vge.GetLatestAsync(productionId, ct);
        return shot with
        {
            HasWorkflowImage = ProductionProgressRules.IsWorkflowImage(still?.ExecutionStatus, still?.ArtifactPath),
            ImageDirectorApproved = ProductionProgressRules.IsImageDirectorApproved(review?.DirectorApproval),
            VideoContractApproved = ProductionProgressRules.IsVideoContractApproved(contract?.ContractStatus),
            HasWorkflowVideo = ProductionProgressRules.IsWorkflowVideo(video?.ExecutionStatus, video?.ArtifactPath),
            VideoDirectorApproved = ProductionProgressRules.IsVideoDirectorApproved(video?.ExecutionStatus, video?.ApprovedAt),
        };
    }

    private static List<ProductionProgressRules.SceneFact> ParseScenes(JsonArray? scenes, List<RawShot> shots)
    {
        var grouped = new Dictionary<string, ProductionProgressRules.SceneFact>(StringComparer.OrdinalIgnoreCase);
        if (scenes is not null)
        {
            var n = 0;
            foreach (var item in scenes)
            {
                if (item is not JsonObject sc) continue;
                n++;
                var id = Text(sc["id"]) is { Length: > 0 } sid ? sid : $"SC{n:00}";
                var chars = Ids(sc["characterIds"]);
                grouped[id] = new ProductionProgressRules.SceneFact(
                    id,
                    Text(sc["title"]) is { Length: > 0 } title ? title : id,
                    chars,
                    []);
            }
        }

        var byScene = new Dictionary<string, List<ProductionProgressRules.ShotFact>>(StringComparer.OrdinalIgnoreCase);
        var seq = 0;
        foreach (var shot in shots)
        {
            seq++;
            var sceneId = shot.SceneId;
            if (string.IsNullOrWhiteSpace(sceneId) && grouped.Count == 1)
                sceneId = grouped.Keys.First();
            if (string.IsNullOrWhiteSpace(sceneId) && shot.SceneTitle.Length > 0)
                sceneId = shot.SceneTitle;
            if (string.IsNullOrWhiteSpace(sceneId))
                sceneId = $"SC{seq:00}";
            if (!grouped.ContainsKey(sceneId))
            {
                grouped[sceneId] = new ProductionProgressRules.SceneFact(
                    sceneId,
                    shot.SceneTitle.Length > 0 ? shot.SceneTitle : sceneId,
                    shot.CharacterIds,
                    []);
            }
            if (!byScene.TryGetValue(sceneId, out var list))
            {
                list = [];
                byScene[sceneId] = list;
            }
            var node = grouped[sceneId];
            var chars = node.CharacterIds.Count > 0 ? node.CharacterIds : shot.CharacterIds;
            list.Add(new ProductionProgressRules.ShotFact(
                shot.ProductionShotId ?? shot.ShotId,
                sceneId,
                list.Count + 1,
                chars,
                false, false, false, false, false));
            if (node.CharacterIds.Count == 0 && shot.CharacterIds.Count > 0)
                grouped[sceneId] = node with { CharacterIds = shot.CharacterIds };
        }

        return grouped.Values
            .Select(scene => scene with { Shots = byScene.TryGetValue(scene.SceneId, out var list) ? list : [] })
            .Where(scene => scene.Shots.Count > 0 || grouped.Count == 1)
            .ToList();
    }

    private sealed record RawShot(string ShotId, string SceneId, string SceneTitle, IReadOnlyList<string> CharacterIds, string? ProductionShotId);

    private static List<RawShot> ParseShots(JsonArray? shots)
    {
        var rows = new List<RawShot>();
        if (shots is null) return rows;
        foreach (var item in shots)
        {
            if (item is not JsonObject sh) continue;
            var id = Text(sh["id"]);
            var sceneId = Text(sh["sceneId"]);
            var scene = Text(sh["scene"]);
            var chars = Ids(sh["characterIds"]);
            if (chars.Count == 0) chars = Ids(sh["characters"]);
            var prod = Text(sh["productionShotId"]);
            if (prod.Length == 0) prod = Text(sh["videoEngineShotId"]);
            rows.Add(new RawShot(id, sceneId, scene, chars, prod.Length > 0 ? prod : null));
        }
        return rows;
    }

    private static ProductionProgressDto ToDto(ProductionProgressRules.EpisodeProgress row) =>
        new(
            row.BuildId,
            row.SeriesCode,
            row.EpisodeCode,
            row.Title,
            row.SceneCount,
            row.ShotCount,
            row.CharacterCount,
            row.ImageMade,
            row.ImageApproved,
            row.VideoMade,
            row.VideoApproved,
            row.Finalization,
            row.Publication,
            row.CurrentStep,
            row.NextAction,
            row.BlockingReason,
            row.StoryLine,
            row.CompletedStages,
            row.TotalStages,
            row.Tone,
            row.Stages.Select(s => new ProductionProgressStageDto(s.Id, s.Label, s.Done, s.Detail)).ToList(),
            row.Scenes.Select(s => new ProductionProgressSceneDto(
                s.SceneId,
                s.SceneName,
                s.ShotCount,
                s.CharacterCount,
                s.CharacterNames,
                s.ImageMade,
                s.ImageApproved,
                s.VideoMade,
                s.VideoApproved,
                s.NextAction,
                s.BlockingReason,
                s.Shots.Select(x => new ProductionProgressShotDto(
                    x.ShotId, x.SceneId, x.ShotNumber, x.CharacterIds, x.CharacterNames,
                    x.CurrentStep, x.NextAction, x.StaffStatus, x.BlockingReason,
                    x.ImageMade, x.ImageApproved, x.VideoMade, x.VideoApproved)).ToList())).ToList());

    private static string Text(JsonNode? n) => (n?.ToString() ?? "").Trim();

    private static bool Flag(JsonNode? n) =>
        n is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    private static IReadOnlyList<string> Ids(JsonNode? n)
    {
        if (n is JsonArray arr)
        {
            return arr.Select(x => Text(x)).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        var one = Text(n);
        return one.Length == 0 ? [] : [one];
    }
}
