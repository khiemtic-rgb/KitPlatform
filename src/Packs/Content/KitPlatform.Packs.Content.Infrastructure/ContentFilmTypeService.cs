using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentFilmTypeService : IContentFilmTypeService
{
    private readonly ContentRepository _repo;

    public ContentFilmTypeService(ContentRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<FilmTypeDto>> ListAsync(Guid brandId, CancellationToken cancellationToken = default)
    {
        if (!await _repo.HasTableAsync("film_type", cancellationToken))
            return [];
        var brand = await _repo.GetBrandAsync(brandId, cancellationToken)
                    ?? throw new InvalidOperationException("Brand không tồn tại.");
        var rows = await _repo.ListFilmTypesAsync(brand.Id, cancellationToken);
        var counts = await _repo.CountSeriesBuildsByFilmTypeAsync(brand.Id, cancellationToken);
        return rows.Select(row => Map(row, counts.GetValueOrDefault(row.Id))).ToList();
    }

    public async Task<FilmTypeDto> CreateAsync(CreateFilmTypeRequest request, CancellationToken cancellationToken = default)
    {
        await RequireTableAsync(cancellationToken);
        var brand = await _repo.GetBrandAsync(request.BrandId, cancellationToken)
                    ?? throw new InvalidOperationException("Brand không tồn tại.");
        var code = (request.Code ?? "").Trim();
        var name = (request.Name ?? "").Trim();
        var workflow = (request.WorkflowKey ?? "").Trim();
        if (code.Length == 0 || name.Length == 0)
            throw new InvalidOperationException("Pipeline cần mã và tên.");
        if (VideoPipelineRules.SeriesCodeForPipeline(code) is null)
            throw new InvalidOperationException("Mã pipeline cần bắt đầu bằng chữ và dài 2–64 ký tự.");
        var aspect = request.Aspect ?? "";
        if (VideoPipelineRules.ConfigError(workflow, "active", request.Steps, aspect) is string configError)
            throw new InvalidOperationException(configError);
        var json = VideoPipelineRules.WriteConfig(request.Purpose, request.SeriesLabel, aspect, request.IsDefault, request.Steps);
        var id = await _repo.InsertFilmTypeAsync(brand.Id, code, name, workflow, "active", json, cancellationToken);
        if (request.IsDefault)
            await ClearOtherDefaultsAsync(brand.Id, id, cancellationToken);
        var row = await _repo.GetFilmTypeAsync(id, cancellationToken)
                  ?? throw new InvalidOperationException("Không đọc lại được pipeline.");
        return Map(row, 0);
    }

    public async Task<FilmTypeDto> UpdateAsync(Guid filmTypeId, UpdateFilmTypeRequest request, CancellationToken cancellationToken = default)
    {
        await RequireTableAsync(cancellationToken);
        var film = await _repo.GetFilmTypeAsync(filmTypeId, cancellationToken)
                   ?? throw new InvalidOperationException("Pipeline không tồn tại.");
        var name = (request.Name ?? "").Trim();
        var workflow = (request.WorkflowKey ?? "").Trim();
        var status = (request.Status ?? "").Trim();
        if (name.Length == 0)
            throw new InvalidOperationException("Pipeline cần tên.");
        var aspect = request.Aspect ?? "";
        if (VideoPipelineRules.ConfigError(workflow, status, request.Steps, aspect) is string configError)
            throw new InvalidOperationException(configError);
        var counts = await _repo.CountSeriesBuildsByFilmTypeAsync(film.BrandId, cancellationToken);
        counts.TryGetValue(film.Id, out var films);
        var activeProject = await _repo.FilmTypeHasActiveProjectAsync(film.Id, cancellationToken);
        if (VideoPipelineRules.WorkflowChangeRefusal(film.WorkflowKey, workflow, films, activeProject) is string workflowRefusal)
            throw new InvalidOperationException(workflowRefusal);
        var json = VideoPipelineRules.WriteConfig(request.Purpose, request.SeriesLabel, aspect, request.IsDefault, request.Steps);
        await _repo.UpdateFilmTypeAsync(film.Id, name, workflow, status, json, cancellationToken);
        if (request.IsDefault)
            await ClearOtherDefaultsAsync(film.BrandId, film.Id, cancellationToken);
        var row = await _repo.GetFilmTypeAsync(film.Id, cancellationToken)
                  ?? throw new InvalidOperationException("Không đọc lại được pipeline.");
        return Map(row, films);
    }

    public async Task BindProjectAsync(Guid projectId, Guid filmTypeId, CancellationToken cancellationToken = default)
    {
        await RequireTableAsync(cancellationToken);
        var film = await _repo.GetFilmTypeAsync(filmTypeId, cancellationToken)
                   ?? throw new InvalidOperationException("Film Type không tồn tại.");
        var project = (await _repo.ListVideoProjectsAsync(cancellationToken)).FirstOrDefault(p => p.Id == projectId)
                      ?? throw new InvalidOperationException("Project không tồn tại.");
        if (KitMktWorkspaceRules.FilmTypeBindRefusal(project.BrandId, film.BrandId) is string bindRefusal)
            throw new InvalidOperationException(bindRefusal);
        if (film.Status != "active")
            throw new InvalidOperationException("Film Type chưa active.");
        await _repo.BindVideoProjectFilmTypeAsync(project.Id, film.BrandId, film.Id, film.BrandCode, cancellationToken);
    }

    public async Task ChangeWorkflowAsync(Guid filmTypeId, string workflowKey, CancellationToken cancellationToken = default)
    {
        await RequireTableAsync(cancellationToken);
        var next = (workflowKey ?? "").Trim();
        if (!KitMktWorkspaceRules.KnownWorkflow(next))
            throw new InvalidOperationException("workflow_key không thuộc famixa_series, factory_short, guided_production.");
        var film = await _repo.GetFilmTypeAsync(filmTypeId, cancellationToken)
                   ?? throw new InvalidOperationException("Pipeline không tồn tại.");
        var counts = await _repo.CountSeriesBuildsByFilmTypeAsync(film.BrandId, cancellationToken);
        counts.TryGetValue(film.Id, out var films);
        var active = await _repo.FilmTypeHasActiveProjectAsync(film.Id, cancellationToken);
        if (VideoPipelineRules.WorkflowChangeRefusal(film.WorkflowKey, next, films, active) is string workflowRefusal)
            throw new InvalidOperationException(workflowRefusal);
        await _repo.UpdateFilmTypeWorkflowAsync(film.Id, next, cancellationToken);
    }

    public async Task SetTopicFormatAsync(Guid topicId, string format, CancellationToken cancellationToken = default)
    {
        if (!await _repo.HasColumnAsync("topic", "content_format", cancellationToken))
            throw new InvalidOperationException("Migration 396 chưa được áp dụng.");
        if (!KitMktWorkspaceRules.KnownFormat(format))
            throw new InvalidOperationException("content_format không hợp lệ.");
        var topic = await _repo.GetTopicAsync(topicId, cancellationToken)
                    ?? throw new InvalidOperationException("Content không tồn tại.");
        await _repo.SetTopicFormatAsync(topic.Id, format.Trim(), cancellationToken);
    }

    public async Task LinkProductionAsync(Guid productionId, Guid? topicId, CancellationToken cancellationToken = default)
    {
        if (!await _repo.HasColumnAsync("video_production", "content_topic_id", cancellationToken))
            throw new InvalidOperationException("Migration 396 chưa được áp dụng.");
        var production = await _repo.GetVideoProductionAsync(productionId, cancellationToken)
                         ?? throw new InvalidOperationException("Production không tồn tại.");
        if (topicId is Guid id)
        {
            var topic = await _repo.GetTopicAsync(id, cancellationToken)
                        ?? throw new InvalidOperationException("Content không tồn tại.");
            if (!string.Equals(topic.ContentFormat, "video", StringComparison.Ordinal))
                throw new InvalidOperationException("Chỉ content format video mới gắn vào production.");
            var project = (await _repo.ListVideoProjectsAsync(cancellationToken))
                .FirstOrDefault(p => p.Id == production.ProjectId);
            if (!KitMktWorkspaceRules.ProjectFilmBrandMatch(project?.BrandId, topic.BrandId))
                throw new InvalidOperationException("Content và project không cùng brand.");
        }
        await _repo.LinkProductionContentAsync(production.Id, topicId, cancellationToken);
    }

    public async Task SelectProductionForPublishAsync(Guid productionId, CancellationToken cancellationToken = default)
    {
        if (!await _repo.HasColumnAsync("video_production", "selected_for_publish", cancellationToken))
            throw new InvalidOperationException("Migration 396 chưa được áp dụng.");
        var production = await _repo.GetVideoProductionAsync(productionId, cancellationToken)
                         ?? throw new InvalidOperationException("Production không tồn tại.");
        if (!KitMktWorkspaceRules.MaySelectProductionForPublish(production.ContentTopicId))
            throw new InvalidOperationException("Phim chưa có Content thì chưa được chọn để xuất bản.");
        await _repo.SelectProductionForPublishAsync(production.Id, production.ContentTopicId!.Value, cancellationToken);
    }

    private async Task RequireTableAsync(CancellationToken cancellationToken)
    {
        if (!await _repo.HasTableAsync("film_type", cancellationToken))
            throw new InvalidOperationException("Migration 395 chưa được áp dụng.");
    }

    private async Task ClearOtherDefaultsAsync(Guid brandId, Guid keepId, CancellationToken cancellationToken)
    {
        var rows = await _repo.ListFilmTypesAsync(brandId, cancellationToken);
        foreach (var row in rows)
        {
            if (row.Id == keepId) continue;
            var config = VideoPipelineRules.ReadConfig(row.ConfigJson);
            if (!config.IsDefault) continue;
            var json = VideoPipelineRules.WriteConfig(config.Purpose, config.SeriesLabel, config.Aspect, false, config.Steps);
            await _repo.UpdateFilmTypeAsync(row.Id, row.Name, row.WorkflowKey, row.Status, json, cancellationToken);
        }
    }

    private static FilmTypeDto Map(ContentRepository.FilmTypeRow row, int filmCount)
    {
        var config = VideoPipelineRules.ReadConfig(row.ConfigJson);
        return new(
            row.Id,
            row.BrandId,
            row.BrandCode,
            row.Code,
            row.Name,
            row.WorkflowKey,
            row.Status,
            config.Purpose,
            config.SeriesLabel,
            config.Aspect,
            config.IsDefault,
            config.Steps,
            filmCount);
    }
}
