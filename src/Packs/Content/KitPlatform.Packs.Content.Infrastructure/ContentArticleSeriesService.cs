using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentArticleSeriesService : IContentArticleSeriesService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new FlexibleInt32JsonConverter() },
    };

    private readonly ContentArticleSeriesRepository _stories;
    private readonly ContentRepository _repo;
    private readonly ContentGeminiClient _gemini;
    private readonly IContentWorkQueueService _work;
    private readonly IContentTopicService _topics;
    private readonly ILogger<ContentArticleSeriesService> _logger;

    public ContentArticleSeriesService(
        ContentArticleSeriesRepository stories,
        ContentRepository repo,
        ContentGeminiClient gemini,
        IContentWorkQueueService work,
        IContentTopicService topics,
        ILogger<ContentArticleSeriesService> logger)
    {
        _stories = stories;
        _repo = repo;
        _gemini = gemini;
        _work = work;
        _topics = topics;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ContentArticleSeriesDto>> ListAsync(
        Guid? brandId,
        string? status,
        Guid? corePackageId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        var rows = await _stories.ListAsync(brandId, status, corePackageId, from, to, cancellationToken);
        return rows.Select(ContentArticleSeriesRepository.MapSeries).ToList();
    }

    public async Task<ContentArticleSeriesDetailDto?> GetDetailAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var series = await _stories.GetAsync(id, cancellationToken);
        if (series is null) return null;
        var episodes = await _stories.ListEpisodesAsync(id, cancellationToken);
        return new ContentArticleSeriesDetailDto(
            ContentArticleSeriesRepository.MapSeries(series),
            episodes.Select(ContentArticleSeriesRepository.MapEpisode).ToList());
    }

    public async Task<ContentArticleSeriesDto> CreateAsync(
        CreateContentArticleSeriesRequest request,
        CancellationToken cancellationToken = default)
    {
        var package = await _repo.GetPackageAsync(request.SourcePackageId, cancellationToken)
                      ?? throw new InvalidOperationException("Source package missing — chọn Brand Adaptation / Content Package.");
        var (_, fits) = ContentPackageExtra.Parse(package.ExtraJson);
        var own = fits.FirstOrDefault(f => f.BrandId == package.BrandId);
        var narrativeBlock = BrandNarrativeFit.SeriesEntryBlock(own);
        if (narrativeBlock is not null)
        {
            throw new InvalidOperationException(
                narrativeBlock == BrandNarrativeFit.NarrativeFitRequired
                    ? "NarrativeFitRequired — package chưa có Territory. Không suy ra CORE từ score."
                    : "OFF-BRAND — không đưa package này vào Series.");
        }

        var episodeCount = ContentArticleSeriesRules.ClampEpisodeCount(request.EpisodeCount);
        var prefix = ContentArticleSeriesRules.NormalizeBrandCode(package.BrandCode);
        var seq = await _stories.NextSeriesSequenceAsync(package.BrandId, prefix, cancellationToken);
        var code = ContentArticleSeriesRules.SeriesCode(package.BrandCode, seq);
        var name = string.IsNullOrWhiteSpace(request.Name)
            ? package.Title.Trim() + " — Series"
            : request.Name.Trim();
        var (core, _) = ContentPackageExtra.Parse(package.ExtraJson);
        var lineage = StampLineage(
            ContentArticleSeriesBlueprint.Empty with { EpisodeCount = episodeCount },
            package);
        var id = await _stories.InsertAsync(
            package.BrandId,
            package.Id,
            code,
            name,
            request.Description?.Trim(),
            package.Angle,
            package.Audience,
            core.CoreMessage,
            ContentArticleSeriesStatuses.Draft,
            episodeCount,
            request.StartDate,
            request.EndDate,
            ContentArticleSeriesRules.SerializeBlueprint(lineage),
            cancellationToken);
        var row = await _stories.GetAsync(id, cancellationToken)
                  ?? throw new InvalidOperationException("Không tạo được series.");
        return ContentArticleSeriesRepository.MapSeries(row);
    }

    public async Task<ContentArticleSeriesDto?> UpdateAsync(
        Guid id,
        UpdateContentArticleSeriesRequest request,
        CancellationToken cancellationToken = default)
    {
        var row = await _stories.GetAsync(id, cancellationToken);
        if (row is null) return null;
        var status = string.IsNullOrWhiteSpace(request.Status) ? row.Status : request.Status.Trim().ToUpperInvariant();
        if (!IsSeriesStatus(status))
            throw new InvalidOperationException("Invalid series status: " + status);
        var episodeCount = request.EpisodeCount is int n
            ? ContentArticleSeriesRules.ClampEpisodeCount(n)
            : row.EpisodeCount;
        var blueprint = request.Blueprint is { } el
            ? el.GetRawText()
            : row.BlueprintJson;
        await _stories.UpdateAsync(
            id,
            string.IsNullOrWhiteSpace(request.Name) ? row.Name : request.Name.Trim(),
            request.Description ?? row.Description,
            request.Objective ?? row.Objective,
            request.Audience ?? row.Audience,
            request.CoreMessage ?? row.CoreMessage,
            status,
            episodeCount,
            row.CurrentEpisodeNo,
            request.StartDate ?? (row.StartDate is { } sd ? DateOnly.FromDateTime(sd) : null),
            request.EndDate ?? (row.EndDate is { } ed ? DateOnly.FromDateTime(ed) : null),
            blueprint,
            cancellationToken);
        var fresh = await _stories.GetAsync(id, cancellationToken);
        return fresh is null ? null : ContentArticleSeriesRepository.MapSeries(fresh);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _stories.DeleteAsync(id, cancellationToken);

    public async Task<ContentArticleSeriesDto> ApproveAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var row = await _stories.GetAsync(id, cancellationToken)
                  ?? throw new InvalidOperationException("Series not found");
        if (string.IsNullOrWhiteSpace(row.Objective) || string.IsNullOrWhiteSpace(row.Audience)
            || string.IsNullOrWhiteSpace(row.CoreMessage))
            throw new InvalidOperationException("Series not approved — cần Objective, Audience, Core Message.");
        var blueprint = ContentArticleSeriesRules.ParseBlueprint(row.BlueprintJson);
        if (blueprint.EpisodeCount <= 0 && string.IsNullOrWhiteSpace(blueprint.CoreMessage))
            throw new InvalidOperationException("Series not approved — Blueprint chưa hoàn chỉnh.");
        var episodes = await _stories.ListEpisodesAsync(id, cancellationToken);
        if (episodes.Count == 0)
            throw new InvalidOperationException("Series not approved — chưa có Episode plan.");
        var next = row.Status == ContentArticleSeriesStatuses.Active
            ? ContentArticleSeriesStatuses.Active
            : ContentArticleSeriesStatuses.Planned;
        await _stories.UpdateAsync(
            id, row.Name, row.Description, row.Objective, row.Audience, row.CoreMessage,
            next, row.EpisodeCount, row.CurrentEpisodeNo,
            row.StartDate is { } sd ? DateOnly.FromDateTime(sd) : null,
            row.EndDate is { } ed ? DateOnly.FromDateTime(ed) : null,
            row.BlueprintJson, cancellationToken);
        var fresh = await _stories.GetAsync(id, cancellationToken)
                    ?? throw new InvalidOperationException("Series not found");
        return ContentArticleSeriesRepository.MapSeries(fresh);
    }

    public async Task<ContentArticleSeriesDetailDto> GenerateBlueprintAsync(
        Guid id,
        GenerateArticleSeriesBlueprintRequest request,
        CancellationToken cancellationToken = default)
    {
        var row = await _stories.GetAsync(id, cancellationToken)
                  ?? throw new InvalidOperationException("Series not found");
        if (!ContentArticleSeriesRules.CanMutatePlan(row.Status))
            throw new InvalidOperationException("Series " + row.Status + " — không regenerate Blueprint.");

        var episodeCount = ContentArticleSeriesRules.ClampEpisodeCount(request.EpisodeCount ?? row.EpisodeCount);
        var batch = ContentArticleSeriesRules.ClampPlanBatch(request.PlanBatch, episodeCount);
        var parsed = await CallBlueprintAiAsync(row, episodeCount, batch, cancellationToken);

        var source = await _repo.GetPackageAsync(row.SourcePackageId, cancellationToken)
                     ?? throw new InvalidOperationException("Source package missing");
        var blueprint = parsed.Blueprint ?? ContentArticleSeriesBlueprint.Empty;
        blueprint = StampLineage(blueprint with { Version = 1, EpisodeCount = episodeCount }, source);
        var name = string.IsNullOrWhiteSpace(parsed.Name) ? row.Name : parsed.Name.Trim();
        var objective = First(parsed.Objective, blueprint.CoreMessage, row.Objective);
        var audience = First(parsed.Audience, blueprint.AudienceNeed, row.Audience);
        var coreMessage = First(parsed.CoreMessage, blueprint.CoreMessage, row.CoreMessage);

        await _stories.UpdateAsync(
            id, name, First(parsed.Description, row.Description), objective, audience, coreMessage,
            row.Status, episodeCount, row.CurrentEpisodeNo,
            row.StartDate is { } sd ? DateOnly.FromDateTime(sd) : null,
            row.EndDate is { } ed ? DateOnly.FromDateTime(ed) : null,
            ContentArticleSeriesRules.SerializeBlueprint(blueprint),
            cancellationToken);

        if (request.ReplaceExistingPlan)
            await _stories.DeletePlannedWithoutTopicAsync(id, cancellationToken);

        await MaterializePlanAsync(id, row.Code, blueprint, parsed.Episodes, 1, batch, cancellationToken);
        return await GetDetailAsync(id, cancellationToken)
               ?? throw new InvalidOperationException("Series not found");
    }

    public async Task<IReadOnlyList<ContentArticleEpisodeDto>> ListEpisodesAsync(
        Guid seriesId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _stories.ListEpisodesAsync(seriesId, cancellationToken);
        return rows.Select(ContentArticleSeriesRepository.MapEpisode).ToList();
    }

    public async Task<ContentArticleEpisodeDetailDto?> GetEpisodeDetailAsync(
        Guid seriesId,
        Guid episodeId,
        CancellationToken cancellationToken = default)
    {
        var seriesRow = await _stories.GetAsync(seriesId, cancellationToken);
        if (seriesRow is null) return null;
        var episodeRow = await _stories.GetEpisodeAsync(seriesId, episodeId, cancellationToken);
        if (episodeRow is null) return null;
        var all = await _stories.ListEpisodesAsync(seriesId, cancellationToken);
        var live = all.Where(e => e.Status != ContentArticleEpisodeStatuses.Cancelled)
            .OrderBy(e => e.EpisodeNo)
            .ToList();
        var idx = live.FindIndex(e => e.Id == episodeId);
        var prevRow = idx > 0 ? live[idx - 1] : null;
        var nextRow = idx >= 0 && idx < live.Count - 1 ? live[idx + 1] : null;

        var series = ContentArticleSeriesRepository.MapSeries(seriesRow);
        var episode = ContentArticleSeriesRepository.MapEpisode(episodeRow);
        var previous = prevRow is null ? null : ContentArticleSeriesRepository.MapEpisode(prevRow);
        var next = nextRow is null ? null : ContentArticleSeriesRepository.MapEpisode(nextRow);

        var package = await _repo.GetPackageAsync(seriesRow.SourcePackageId, cancellationToken);
        var (core, _) = ContentPackageExtra.Parse(package?.ExtraJson);
        var brand = await _repo.GetBrandAsync(seriesRow.BrandId, cancellationToken);
        var brain = brand is null
            ? ""
            : ContentBrandKnowledge.FormatForPrompt(
                ContentBrandKnowledge.Parse(brand.ToneJson, brand.VisualKitJson),
                brand.OperationalBrief);
        var context = ContentArticleSeriesRules.BuildContinuityContext(
            series, episode, previous, next, core, brain, package?.Angle);

        ContentTopicDetailDto? topicDetail = null;
        if (episode.ContentTopicId is Guid tid)
            topicDetail = await _topics.GetDetailAsync(tid, cancellationToken);

        return new ContentArticleEpisodeDetailDto(series, episode, previous, next, topicDetail, context);
    }

    public async Task<ContentArticleEpisodeDto> AddEpisodeAsync(
        Guid seriesId,
        UpsertContentArticleEpisodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var series = await RequireMutableSeriesAsync(seriesId, cancellationToken);
        var existing = await _stories.ListEpisodesAsync(seriesId, cancellationToken);
        var nextNo = request.EpisodeNo ??
                     (existing.Count == 0 ? 1 : existing.Max(e => e.EpisodeNo) + 1);
        if (existing.Any(e => e.EpisodeNo == nextNo && e.Status != ContentArticleEpisodeStatuses.Cancelled))
            throw new InvalidOperationException("Invalid episode order — EP" + nextNo.ToString("000") + " đã tồn tại.");
        if (nextNo < 1 || nextNo > series.EpisodeCount)
            throw new InvalidOperationException("Invalid episode order — ngoài Blueprint (" + series.EpisodeCount + ").");
        var id = await _stories.InsertOrReuseEpisodeAsync(
            seriesId,
            nextNo,
            ContentArticleSeriesRules.EpisodeCode(series.Code, nextNo),
            string.IsNullOrWhiteSpace(request.Title) ? "Tập " + nextNo.ToString("000") : request.Title.Trim(),
            request.Objective,
            request.Angle,
            request.KeyMessage,
            string.IsNullOrWhiteSpace(request.Status) ? ContentArticleEpisodeStatuses.Planned : request.Status.Trim().ToUpperInvariant(),
            request.Continuity is { } c ? c.GetRawText() : "{}",
            cancellationToken);
        await _stories.RelinkAsync(seriesId, cancellationToken);
        var row = await _stories.GetEpisodeAsync(seriesId, id, cancellationToken)
                  ?? throw new InvalidOperationException("Episode not found");
        return ContentArticleSeriesRepository.MapEpisode(row);
    }

    public async Task<ContentArticleEpisodeDto?> UpdateEpisodeAsync(
        Guid seriesId,
        Guid episodeId,
        UpsertContentArticleEpisodeRequest request,
        CancellationToken cancellationToken = default)
    {
        await RequireMutableSeriesAsync(seriesId, cancellationToken);
        var row = await _stories.GetEpisodeAsync(seriesId, episodeId, cancellationToken);
        if (row is null) return null;
        if (request.EpisodeNo is int no && no != row.EpisodeNo)
        {
            var clash = await _stories.FindEpisodeByNoAsync(seriesId, no, cancellationToken);
            if (clash is not null && clash.Id != episodeId)
                throw new InvalidOperationException("Invalid episode order — số " + no + " đã dùng.");
        }

        var episodeNo = request.EpisodeNo ?? row.EpisodeNo;
        var series = await _stories.GetAsync(seriesId, cancellationToken)
                     ?? throw new InvalidOperationException("Series not found");
        await _stories.UpdateEpisodeAsync(
            row.Id,
            episodeNo,
            ContentArticleSeriesRules.EpisodeCode(series.Code, episodeNo),
            string.IsNullOrWhiteSpace(request.Title) ? row.Title : request.Title.Trim(),
            request.Objective ?? row.Objective,
            request.Angle ?? row.Angle,
            request.KeyMessage ?? row.KeyMessage,
            string.IsNullOrWhiteSpace(request.Status) ? row.Status : request.Status.Trim().ToUpperInvariant(),
            request.PlannedAt ?? row.PlannedAt,
            row.PublishedAt,
            row.PreviousEpisodeId,
            row.NextEpisodeId,
            request.Continuity is { } c ? c.GetRawText() : row.ContinuityJson,
            row.ContentTopicId,
            cancellationToken);
        await _stories.RelinkAsync(seriesId, cancellationToken);
        var fresh = await _stories.GetEpisodeAsync(seriesId, episodeId, cancellationToken);
        return fresh is null ? null : ContentArticleSeriesRepository.MapEpisode(fresh);
    }

    public async Task<bool> DeleteEpisodeAsync(
        Guid seriesId,
        Guid episodeId,
        CancellationToken cancellationToken = default)
    {
        await RequireMutableSeriesAsync(seriesId, cancellationToken);
        var ok = await _stories.DeleteEpisodeAsync(seriesId, episodeId, cancellationToken);
        if (ok) await _stories.RelinkAsync(seriesId, cancellationToken);
        return ok;
    }

    public async Task<IReadOnlyList<ContentArticleEpisodeDto>> ReorderEpisodesAsync(
        Guid seriesId,
        IReadOnlyList<Guid> episodeIds,
        CancellationToken cancellationToken = default)
    {
        var series = await RequireMutableSeriesAsync(seriesId, cancellationToken);
        var rows = await _stories.ListEpisodesAsync(seriesId, cancellationToken);
        var byId = rows.ToDictionary(r => r.Id);
        if (episodeIds.Count != rows.Count || episodeIds.Any(id => !byId.ContainsKey(id)))
            throw new InvalidOperationException("Invalid episode order — danh sách không khớp Series.");

        // Park numbers to avoid UNIQUE(series_id, episode_no) collisions while swapping.
        var n = 1;
        foreach (var id in episodeIds)
        {
            var row = byId[id];
            await _stories.UpdateEpisodeAsync(
                row.Id, 10_000 + n, row.Code, row.Title, row.Objective, row.Angle, row.KeyMessage,
                row.Status, row.PlannedAt, row.PublishedAt, null, null, row.ContinuityJson,
                row.ContentTopicId, cancellationToken);
            n++;
        }

        n = 1;
        foreach (var id in episodeIds)
        {
            var row = byId[id];
            await _stories.UpdateEpisodeAsync(
                row.Id, n, ContentArticleSeriesRules.EpisodeCode(series.Code, n),
                row.Title, row.Objective, row.Angle, row.KeyMessage,
                row.Status, row.PlannedAt, row.PublishedAt, null, null, row.ContinuityJson,
                row.ContentTopicId, cancellationToken);
            n++;
        }

        await _stories.RelinkAsync(seriesId, cancellationToken);
        return await ListEpisodesAsync(seriesId, cancellationToken);
    }

    public async Task<ContentArticleEpisodeDto> GenerateBriefAsync(
        Guid seriesId,
        Guid episodeId,
        CancellationToken cancellationToken = default)
    {
        var detail = await GetEpisodeDetailAsync(seriesId, episodeId, cancellationToken)
                     ?? throw new InvalidOperationException("Episode not found");
        if (!ContentArticleSeriesRules.CanMutatePlan(detail.Series.Status))
            throw new InvalidOperationException("Series " + detail.Series.Status + " — không generate brief.");

        var ai = await _gemini.ResolveConfigAsync(cancellationToken);
        if (!ai.ApiKeyConfigured)
            throw new InvalidOperationException("Brand Brain / Gemini missing — vào Model AI hoặc env GEMINI_API_KEY.");

        var system =
            "You are the KIT Marketing Series planner (article/social, not video).\n" +
            "Return JSON only: {title,objective,angle,keyMessage,continuity:{previousSummary,previousKeyPoints,mustContinueFrom,mustNotRepeat,nextEpisodeDirection,openLoops,references}}.\n" +
            ContentArticleSeriesRules.FactorySystemRules +
            "\nPrompt version: " + ContentArticleSeriesRules.PromptVersion;
        var user = detail.ContinuityContext + "\n\nWrite the Episode Brief for the CURRENT EPISODE only.";
        var raw = await _gemini.GenerateJsonAsync(system, user, cancellationToken, 4096, disableThinking: true);
        var parsed = JsonSerializer.Deserialize<BriefAiResponse>(StripFence(raw), JsonOpts)
                     ?? throw new InvalidOperationException("Generation failed — brief JSON không đọc được.");

        var continuity = parsed.Continuity ?? ContentArticleEpisodeContinuity.Empty;
        var row = await _stories.GetEpisodeAsync(seriesId, episodeId, cancellationToken)
                  ?? throw new InvalidOperationException("Episode not found");
        await _stories.UpdateEpisodeAsync(
            row.Id, row.EpisodeNo, row.Code,
            First(parsed.Title, row.Title)!,
            First(parsed.Objective, row.Objective),
            First(parsed.Angle, row.Angle),
            First(parsed.KeyMessage, row.KeyMessage),
            ContentArticleEpisodeStatuses.Ready,
            row.PlannedAt ?? DateTimeOffset.UtcNow,
            row.PublishedAt,
            row.PreviousEpisodeId, row.NextEpisodeId,
            ContentArticleSeriesRules.SerializeContinuity(continuity),
            row.ContentTopicId,
            cancellationToken);
        await RefreshTopicOutlineAsync(seriesId, episodeId, cancellationToken);
        _logger.LogInformation(
            "Article series brief series={SeriesId} episode={EpisodeId} brand={BrandId} package={PackageId} prompt={Prompt}",
            seriesId, episodeId, detail.Series.BrandId, detail.Series.SourcePackageId, ContentArticleSeriesRules.PromptVersion);
        var fresh = await _stories.GetEpisodeAsync(seriesId, episodeId, cancellationToken)
                    ?? throw new InvalidOperationException("Episode not found");
        return ContentArticleSeriesRepository.MapEpisode(fresh);
    }

    public async Task<GenerateArticleEpisodeResultDto> GenerateContentAsync(
        Guid seriesId,
        Guid episodeId,
        GenerateContentRequest? request,
        CancellationToken cancellationToken = default)
    {
        var seriesRow = await _stories.GetAsync(seriesId, cancellationToken)
                        ?? throw new InvalidOperationException("Series not found");
        if (!ContentArticleSeriesRules.CanGenerateContent(seriesRow.Status))
            throw new InvalidOperationException("Series not approved — duyệt Series (PLANNED/ACTIVE) trước khi generate content.");

        var episodeRow = await _stories.GetEpisodeAsync(seriesId, episodeId, cancellationToken)
                         ?? throw new InvalidOperationException("Episode not found");
        var brand = await _repo.GetBrandAsync(seriesRow.BrandId, cancellationToken)
                    ?? throw new InvalidOperationException("Brand Brain missing");
        var knowledge = ContentBrandKnowledge.Parse(brand.ToneJson, brand.VisualKitJson);
        if (!ContentBrandKnowledge.HasEnoughForGenerate(brand.OperationalBrief, knowledge))
            throw new InvalidOperationException(
                "Brand Brain missing — " + string.Join("; ", ContentBrandKnowledge.MissingBrain(brand.OperationalBrief, knowledge)));

        var topicId = episodeRow.ContentTopicId
                      ?? await CreateEpisodeTopicAsync(seriesRow, episodeRow, cancellationToken);
        episodeRow = await _stories.GetEpisodeAsync(seriesId, episodeId, cancellationToken)
                     ?? throw new InvalidOperationException("Episode not found");
        await EnsureEpisodeBriefAsync(seriesRow, episodeRow, cancellationToken);
        await RefreshTopicOutlineAsync(seriesId, episodeId, cancellationToken);
        await _stories.UpdateEpisodeAsync(
            episodeRow.Id, episodeRow.EpisodeNo, episodeRow.Code, episodeRow.Title,
            episodeRow.Objective, episodeRow.Angle, episodeRow.KeyMessage,
            ContentArticleEpisodeStatuses.Generating,
            episodeRow.PlannedAt, episodeRow.PublishedAt,
            episodeRow.PreviousEpisodeId, episodeRow.NextEpisodeId,
            episodeRow.ContinuityJson, topicId, cancellationToken);

        if (seriesRow.Status == ContentArticleSeriesStatuses.Planned)
        {
            await _stories.UpdateAsync(
                seriesRow.Id, seriesRow.Name, seriesRow.Description, seriesRow.Objective,
                seriesRow.Audience, seriesRow.CoreMessage, ContentArticleSeriesStatuses.Active,
                seriesRow.EpisodeCount, episodeRow.EpisodeNo,
                seriesRow.StartDate is { } sd ? DateOnly.FromDateTime(sd) : null,
                seriesRow.EndDate is { } ed ? DateOnly.FromDateTime(ed) : null,
                seriesRow.BlueprintJson, cancellationToken);
        }

        var work = await _work.EnqueueGenerateTopicAsync(
            topicId, request ?? new GenerateContentRequest(), cancellationToken);
        var fresh = await _stories.GetEpisodeAsync(seriesId, episodeId, cancellationToken)
                    ?? throw new InvalidOperationException("Episode not found");
        return new GenerateArticleEpisodeResultDto(
            ContentArticleSeriesRepository.MapEpisode(fresh),
            work,
            work.Message);
    }

    public async Task<GenerateArticleEpisodeBatchResultDto> GenerateNextAsync(
        Guid seriesId,
        GenerateArticleEpisodeNextRequest request,
        CancellationToken cancellationToken = default)
    {
        var row = await _stories.GetAsync(seriesId, cancellationToken)
                  ?? throw new InvalidOperationException("Series not found");
        var count = Math.Clamp(request.Count <= 0 ? 10 : request.Count, 1, 10);
        var mode = (request.Mode ?? "plan").Trim().ToLowerInvariant();

        if (mode == "content")
        {
            if (!ContentArticleSeriesRules.CanGenerateContent(row.Status))
                throw new InvalidOperationException("Series not approved — không generate Episode mới.");
            var episodes = await _stories.ListEpisodesAsync(seriesId, cancellationToken);
            var targets = episodes
                .Where(e => e.Status is not ContentArticleEpisodeStatuses.Cancelled
                    and not ContentArticleEpisodeStatuses.Published
                    and not ContentArticleEpisodeStatuses.Analyzed)
                .OrderBy(e => e.EpisodeNo)
                .Take(count)
                .ToList();
            var created = new List<ContentArticleEpisodeDto>();
            var jobs = new List<EnqueueWorkResultDto>();
            foreach (var target in targets)
            {
                var result = await GenerateContentAsync(seriesId, target.Id, new GenerateContentRequest(), cancellationToken);
                created.Add(result.Episode);
                if (result.Work is not null) jobs.Add(result.Work);
            }

            return new GenerateArticleEpisodeBatchResultDto(
                created, jobs, "Đã xếp " + jobs.Count + " job Content Factory (không chờ AI).");
        }

        if (!ContentArticleSeriesRules.CanMutatePlan(row.Status))
            throw new InvalidOperationException("Series " + row.Status + " — không lập Episode plan.");

        var blueprint = ContentArticleSeriesRules.ParseBlueprint(row.BlueprintJson);
        var existing = await _stories.ListEpisodesAsync(seriesId, cancellationToken);
        var nextNo = existing.Count == 0 ? 1 : existing.Max(e => e.EpisodeNo) + 1;
        if (nextNo > row.EpisodeCount)
            throw new InvalidOperationException("Episode plan đã đủ " + row.EpisodeCount + ".");

        var need = Math.Min(count, row.EpisodeCount - nextNo + 1);
        IReadOnlyList<EpisodePlanAi>? extra = null;
        var haveBeats = blueprint.NarrativeArc.Any(b => b.EpisodeNo >= nextNo);
        if (!haveBeats)
            extra = (await CallBlueprintAiAsync(row, row.EpisodeCount, need, cancellationToken, nextNo)).Episodes;

        var ids = await MaterializePlanAsync(seriesId, row.Code, blueprint, extra, nextNo, need, cancellationToken);
        var list = await ListEpisodesAsync(seriesId, cancellationToken);
        return new GenerateArticleEpisodeBatchResultDto(
            list.Where(e => ids.Contains(e.Id)).ToList(),
            [],
            "Đã lập " + ids.Count + " Episode (lazy, chưa generate content).");
    }

    private async Task<List<Guid>> MaterializePlanAsync(
        Guid seriesId,
        string seriesCode,
        ContentArticleSeriesBlueprint blueprint,
        IReadOnlyList<EpisodePlanAi>? planned,
        int fromNo,
        int count,
        CancellationToken ct)
    {
        var ids = new List<Guid>();
        var byNo = (planned ?? [])
            .Where(p => p.EpisodeNo > 0)
            .GroupBy(p => p.EpisodeNo)
            .ToDictionary(g => g.Key, g => g.First());
        var beats = blueprint.NarrativeArc
            .Where(b => b.EpisodeNo > 0)
            .GroupBy(b => b.EpisodeNo)
            .ToDictionary(g => g.Key, g => g.First());

        for (var no = fromNo; no < fromNo + count; no++)
        {
            byNo.TryGetValue(no, out var plan);
            beats.TryGetValue(no, out var beat);
            var title = First(plan?.Title, beat?.Title) ?? ("Tập " + no.ToString("000"));
            var id = await _stories.InsertOrReuseEpisodeAsync(
                seriesId,
                no,
                ContentArticleSeriesRules.EpisodeCode(seriesCode, no),
                title,
                First(plan?.Objective, beat?.Objective),
                First(plan?.Angle, beat?.Angle),
                First(plan?.KeyMessage, beat?.KeyMessage),
                ContentArticleEpisodeStatuses.Planned,
                ContentArticleSeriesRules.SerializeContinuity(plan?.Continuity),
                ct);
            ids.Add(id);
        }

        await _stories.RelinkAsync(seriesId, ct);
        return ids;
    }

    private async Task<Guid> CreateEpisodeTopicAsync(
        ContentArticleSeriesRepository.SeriesRow series,
        ContentArticleSeriesRepository.EpisodeRow episode,
        CancellationToken ct)
    {
        var source = await _repo.GetPackageAsync(series.SourcePackageId, ct)
                     ?? throw new InvalidOperationException("Source package missing");
        var outline = await BuildOutlineForEpisodeAsync(series.Id, episode.Id, ct);
        var topicId = await _repo.InsertTopicAsync(
            series.BrandId,
            episode.Title,
            source.Pillar,
            source.Goal,
            null,
            null,
            source.Priority,
            "Draft",
            outline,
            null,
            ct);
        var audience = string.IsNullOrWhiteSpace(series.Audience) ? source.Audience : series.Audience;
        var packageId = await _repo.InsertPackageAsync(
            series.BrandId,
            topicId,
            episode.Title,
            episode.Angle ?? source.Angle,
            audience,
            source.ContentType,
            source.Pillar,
            source.Goal,
            source.Priority,
            "Draft",
            series.SourcePackageId,
            ct);
        var extra = ContentPackageExtra.MergeBrief(
            source.ExtraJson,
            ContentArticleSeriesRules.EpisodeContentBrief(episode.Objective, episode.KeyMessage, series.Objective));
        await _repo.UpdatePackageExtraJsonAsync(packageId, extra, ct);
        await _stories.UpdateEpisodeAsync(
            episode.Id, episode.EpisodeNo, episode.Code, episode.Title,
            episode.Objective, episode.Angle, episode.KeyMessage,
            episode.Status, episode.PlannedAt, episode.PublishedAt,
            episode.PreviousEpisodeId, episode.NextEpisodeId,
            episode.ContinuityJson, topicId, ct);
        return topicId;
    }

    private async Task EnsureEpisodeBriefAsync(
        ContentArticleSeriesRepository.SeriesRow series,
        ContentArticleSeriesRepository.EpisodeRow episode,
        CancellationToken ct)
    {
        if (episode.ContentTopicId is not Guid topicId) return;
        var packageId = await _repo.GetPackageIdByTopicAsync(topicId, ct);
        if (packageId is not Guid id) return;
        var package = await _repo.GetPackageAsync(id, ct);
        if (package is null) return;
        var extra = ContentPackageExtra.MergeBrief(
            package.ExtraJson,
            ContentArticleSeriesRules.EpisodeContentBrief(episode.Objective, episode.KeyMessage, series.Objective));
        await _repo.UpdatePackageExtraJsonAsync(id, extra, ct);
    }

    private async Task RefreshTopicOutlineAsync(Guid seriesId, Guid episodeId, CancellationToken ct)
    {
        var detail = await GetEpisodeDetailAsync(seriesId, episodeId, ct);
        if (detail?.Episode.ContentTopicId is not Guid topicId) return;
        var topic = await _repo.GetTopicAsync(topicId, ct);
        if (topic is null) return;
        var outline = ContentArticleSeriesRules.BuildOutlineBlock(
            detail.Series, detail.Episode, detail.Previous, detail.Next, detail.ContinuityContext);
        await _repo.UpdateTopicAsync(
            topic.Id, topic.BrandId, topic.Title, topic.Pillar, topic.Goal, topic.CtaUrl,
            topic.UtmCampaign, topic.Priority, topic.Status, outline, topic.DisplayAt, ct);
    }

    private async Task<string> BuildOutlineForEpisodeAsync(Guid seriesId, Guid episodeId, CancellationToken ct)
    {
        var detail = await GetEpisodeDetailAsync(seriesId, episodeId, ct)
                     ?? throw new InvalidOperationException("Episode not found");
        return ContentArticleSeriesRules.BuildOutlineBlock(
            detail.Series, detail.Episode, detail.Previous, detail.Next, detail.ContinuityContext);
    }

    private async Task<BlueprintAiResponse> CallBlueprintAiAsync(
        ContentArticleSeriesRepository.SeriesRow series,
        int episodeCount,
        int planBatch,
        CancellationToken ct,
        int fromEpisode = 1)
    {
        var ai = await _gemini.ResolveConfigAsync(ct);
        if (!ai.ApiKeyConfigured)
            throw new InvalidOperationException("Generation failed — Gemini API key missing (Model AI / GEMINI_API_KEY).");

        var package = await _repo.GetPackageAsync(series.SourcePackageId, ct)
                      ?? throw new InvalidOperationException("Source package missing");
        var (core, fits) = ContentPackageExtra.Parse(package.ExtraJson);
        var own = fits.FirstOrDefault(f => f.BrandId == package.BrandId);
        var brand = await _repo.GetBrandAsync(series.BrandId, ct)
                    ?? throw new InvalidOperationException("Brand Brain missing");
        var knowledge = ContentBrandKnowledge.Parse(brand.ToneJson, brand.VisualKitJson);
        var brain = ContentBrandKnowledge.FormatForPrompt(knowledge, brand.OperationalBrief);
        var brief = ContentCreativeBriefDto.FormatForPrompt(ContentPackageExtra.ParseBrief(package.ExtraJson));

        var system =
            "You are the KIT Marketing Series strategist (article/social Content Series — not Famixa video).\n" +
            "Return valid JSON only. No markdown fences.\n" +
            "Schema: {name,description,objective,audience,coreMessage,blueprint:{version,narrativeArc:[{episodeNo,title,beat,objective,angle,keyMessage}],contentPillars,audienceNeed,coreMessage,tone,style,recommendedFormats,recommendedChannels,episodeCount,continuityRules,avoidRepetition,ctaStrategy,successDefinition,seriesTone,seriesStyle,seriesCta},episodes:[{episodeNo,title,objective,angle,keyMessage,continuity:{previousSummary,previousKeyPoints,mustContinueFrom,mustNotRepeat,nextEpisodeDirection,openLoops,references}}]}.\n" +
            "Narrative arc must be unique to THIS series — do not force Problem→Future.\n" +
            "episodes[] = detailed plan for the requested batch only. narrativeArc covers the full series length.\n" +
            "Do not write full articles. Prompt version: " + ContentArticleSeriesRules.PromptVersion;
        var user = new StringBuilder();
        user.AppendLine("BRAND");
        user.AppendLine(brand.Name + " (" + brand.Code + ")");
        user.AppendLine(brain);
        user.AppendLine();
        user.AppendLine("CORE IDEA");
        user.AppendLine(series.CoreIdeaTitle);
        user.AppendLine("Insight: " + (core.Insight ?? ""));
        user.AppendLine("Problem: " + (core.Problem ?? ""));
        user.AppendLine("Core message: " + (core.CoreMessage ?? ""));
        user.AppendLine();
        user.AppendLine("BRAND ADAPTATION");
        user.AppendLine(package.Title);
        user.AppendLine("Angle: " + (package.Angle ?? ""));
        user.AppendLine("Audience: " + (package.Audience ?? ""));
        user.AppendLine();
        user.AppendLine("SELECTED TERRITORY (locked — do not change)");
        user.AppendLine("TerritoryId: " + (own?.TerritoryId ?? ""));
        user.AppendLine("Type: " + (own?.Territory ?? ""));
        user.AppendLine("Brand Angle is locked: " + (own?.BrandAngle ?? package.Angle ?? ""));
        var dna = BrandNarrativeFit.Parse(brand.NarrativeJson);
        var territory = dna?.Find(own?.TerritoryId);
        if (territory is not null)
        {
            user.AppendLine("Name: " + territory.Name);
            user.AppendLine("Description: " + territory.Description);
            user.AppendLine("WhyItBelongs: " + territory.WhyItBelongs);
            if (territory.Boundaries.Count > 0)
                user.AppendLine("Boundaries: " + string.Join(" | ", territory.Boundaries));
        }

        if (!string.IsNullOrWhiteSpace(dna?.MustNotBecome))
            user.AppendLine("MustNotBecome: " + dna.MustNotBecome);
        user.AppendLine("Do not replace this territory or this Brand Angle. The next episode continues this story. Do not open a different topic.");
        if (!string.IsNullOrWhiteSpace(brief))
            user.AppendLine(brief);
        user.AppendLine();
        user.AppendLine("SERIES");
        user.AppendLine(series.Name + " [" + series.Code + "]");
        user.AppendLine("Requested length: " + episodeCount);
        user.AppendLine("Detail the episode batch from EP" + fromEpisode.ToString("000") + " count=" + planBatch);
        user.AppendLine("Existing objective: " + (series.Objective ?? ""));

        var raw = await _gemini.GenerateJsonAsync(system, user.ToString(), ct, 8192, disableThinking: true);
        _logger.LogInformation(
            "Article series blueprint series={SeriesId} brand={BrandId} package={PackageId} prompt={Prompt} model={Model}",
            series.Id, series.BrandId, series.SourcePackageId, ContentArticleSeriesRules.PromptVersion, ai.TextModel);
        var parsed = ParseBlueprintAi(StripFence(raw));
        if (parsed is null)
            throw new InvalidOperationException("Generation failed — Blueprint JSON không hợp lệ.");
        return parsed;
    }

    private async Task<ContentArticleSeriesRepository.SeriesRow> RequireMutableSeriesAsync(
        Guid seriesId,
        CancellationToken ct)
    {
        var row = await _stories.GetAsync(seriesId, ct)
                  ?? throw new InvalidOperationException("Series not found");
        if (!ContentArticleSeriesRules.CanMutatePlan(row.Status))
            throw new InvalidOperationException("Series " + row.Status + " — không sửa Episode plan.");
        return row;
    }

    private static bool IsSeriesStatus(string status) =>
        status is ContentArticleSeriesStatuses.Draft or ContentArticleSeriesStatuses.Planned
            or ContentArticleSeriesStatuses.Active or ContentArticleSeriesStatuses.Paused
            or ContentArticleSeriesStatuses.Completed or ContentArticleSeriesStatuses.Cancelled;

    private static ContentArticleSeriesBlueprint StampLineage(
        ContentArticleSeriesBlueprint blueprint,
        ContentRepository.PackageRow package)
    {
        var (_, fits) = ContentPackageExtra.Parse(package.ExtraJson);
        var own = fits.FirstOrDefault(f => f.BrandId == package.BrandId);
        return blueprint with
        {
            Territory = own?.Territory,
            TerritoryId = own?.TerritoryId,
            BrandAngle = First(own?.BrandAngle, package.Angle),
        };
    }

    private static string? First(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static BlueprintAiResponse? ParseBlueprintAi(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            return new BlueprintAiResponse
            {
                Name = ReadStr(root, "name"),
                Description = ReadStr(root, "description"),
                Objective = ReadStr(root, "objective"),
                Audience = ReadStr(root, "audience"),
                CoreMessage = ReadStr(root, "coreMessage"),
                Blueprint = MapBlueprint(ReadObj(root, "blueprint")),
                Episodes = MapEpisodes(ReadArr(root, "episodes")),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ContentArticleSeriesBlueprint MapBlueprint(JsonElement? obj)
    {
        if (obj is not { ValueKind: JsonValueKind.Object } el)
            return ContentArticleSeriesBlueprint.Empty;
        var version = ReadInt(el, "version");
        return new ContentArticleSeriesBlueprint(
            version <= 0 ? 1 : version,
            MapBeats(ReadArr(el, "narrativeArc")),
            ReadStrings(el, "contentPillars"),
            ReadStr(el, "audienceNeed"),
            ReadStr(el, "coreMessage"),
            ReadStr(el, "tone"),
            ReadStr(el, "style"),
            ReadStrings(el, "recommendedFormats"),
            ReadStrings(el, "recommendedChannels"),
            ReadInt(el, "episodeCount"),
            ReadStrings(el, "continuityRules"),
            ReadStrings(el, "avoidRepetition"),
            ReadStr(el, "ctaStrategy"),
            ReadStr(el, "successDefinition"),
            ReadStr(el, "seriesTone"),
            ReadStr(el, "seriesStyle"),
            ReadStr(el, "seriesCta"));
    }

    private static List<ContentArticleNarrativeBeat> MapBeats(JsonElement? arr)
    {
        var list = new List<ContentArticleNarrativeBeat>();
        if (arr is not { ValueKind: JsonValueKind.Array } items) return list;
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            list.Add(new ContentArticleNarrativeBeat(
                ReadInt(item, "episodeNo"),
                ReadStr(item, "title"),
                ReadStr(item, "beat"),
                ReadStr(item, "objective"),
                ReadStr(item, "angle"),
                ReadStr(item, "keyMessage")));
        }
        return list;
    }

    private static List<EpisodePlanAi> MapEpisodes(JsonElement? arr)
    {
        var list = new List<EpisodePlanAi>();
        if (arr is not { ValueKind: JsonValueKind.Array } items) return list;
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            list.Add(new EpisodePlanAi
            {
                EpisodeNo = ReadInt(item, "episodeNo"),
                Title = ReadStr(item, "title"),
                Objective = ReadStr(item, "objective"),
                Angle = ReadStr(item, "angle"),
                KeyMessage = ReadStr(item, "keyMessage"),
                Continuity = MapContinuity(ReadObj(item, "continuity")),
            });
        }
        return list;
    }

    private static ContentArticleEpisodeContinuity MapContinuity(JsonElement? obj)
    {
        if (obj is not { ValueKind: JsonValueKind.Object } el)
            return ContentArticleEpisodeContinuity.Empty;
        return new ContentArticleEpisodeContinuity(
            ReadStr(el, "previousSummary"),
            ReadStrings(el, "previousKeyPoints"),
            ReadStrings(el, "mustContinueFrom"),
            ReadStrings(el, "mustNotRepeat"),
            ReadStr(el, "nextEpisodeDirection"),
            ReadStrings(el, "openLoops"),
            ReadStrings(el, "references"));
    }

    private static JsonElement? ReadObj(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Object ? el : null;

    private static JsonElement? ReadArr(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Array ? el : null;

    private static string? ReadStr(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var el)) return null;
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
    }

    private static int ReadInt(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var el)) return 0;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n)) return n;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var d)) return (int)d;
        if (el.ValueKind != JsonValueKind.String) return 0;
        var raw = el.GetString();
        if (int.TryParse(raw, out var i)) return i;
        if (double.TryParse(raw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var f))
            return (int)f;
        var digits = new string((raw ?? "").Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var fromDigits) ? fromDigits : 0;
    }

    private static IReadOnlyList<string> ReadStrings(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var el)) return [];
        if (el.ValueKind == JsonValueKind.String)
        {
            var one = el.GetString();
            return string.IsNullOrWhiteSpace(one) ? [] : [one.Trim()];
        }
        if (el.ValueKind != JsonValueKind.Array) return [];
        return el.EnumerateArray()
            .Select(item => item.ValueKind switch
            {
                JsonValueKind.String => item.GetString(),
                JsonValueKind.Number => item.GetRawText(),
                JsonValueKind.Object when item.TryGetProperty("text", out var t) => t.GetString(),
                JsonValueKind.Object when item.TryGetProperty("value", out var v) => v.GetString(),
                _ => null,
            })
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim())
            .ToList();
    }

    private static string StripFence(string raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (!text.StartsWith("```", StringComparison.Ordinal)) return text;
        var nl = text.IndexOf('\n');
        if (nl < 0) return text;
        text = text[(nl + 1)..];
        var end = text.LastIndexOf("```", StringComparison.Ordinal);
        return end >= 0 ? text[..end].Trim() : text.Trim();
    }

    private sealed class BlueprintAiResponse
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Objective { get; set; }
        public string? Audience { get; set; }
        public string? CoreMessage { get; set; }
        public ContentArticleSeriesBlueprint? Blueprint { get; set; }
        public List<EpisodePlanAi>? Episodes { get; set; }
    }

    private sealed class EpisodePlanAi
    {
        public int EpisodeNo { get; set; }
        public string? Title { get; set; }
        public string? Objective { get; set; }
        public string? Angle { get; set; }
        public string? KeyMessage { get; set; }
        public ContentArticleEpisodeContinuity? Continuity { get; set; }
    }

    private sealed class BriefAiResponse
    {
        public string? Title { get; set; }
        public string? Objective { get; set; }
        public string? Angle { get; set; }
        public string? KeyMessage { get; set; }
        public ContentArticleEpisodeContinuity? Continuity { get; set; }
    }
}
