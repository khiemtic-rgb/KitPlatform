using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentOrgSettingsService : IContentOrgSettingsService
{
    private readonly ContentRepository _repo;

    public ContentOrgSettingsService(ContentRepository repo) => _repo = repo;

    public async Task<ContentOrgSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetOrgSettingsAsync(cancellationToken);
        var spend = await MonthSpendAsync(brandId: null, cancellationToken);
        return Map(row, spend);
    }

    public async Task<ContentOrgSettingsDto> UpdateAsync(
        UpdateContentOrgSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetOrgSettingsAsync(cancellationToken);
        if (request.MonthlyCeilingUsd is { } ceiling)
            row.MonthlyCeilingUsd = ceiling < 0 ? 0 : ceiling;
        if (request.MaxImageCandidatesPerItem is { } n)
            row.MaxImageCandidatesPerItem = Math.Clamp(n, 1, 10);
        if (request.RegenMultiplier is { } regen)
            row.RegenMultiplier = regen < 1 ? 1 : regen;
        if (!string.IsNullOrWhiteSpace(request.DefaultImageTier))
            row.DefaultImageTier = NormalizeTier(request.DefaultImageTier);
        if (request.ImageRateUsd is { Count: > 0 })
            row.ImageRateUsdJson = ContentRepository.ToJson(request.ImageRateUsd);
        if (request.TextPackEstimateUsd is { } textEst)
            row.TextPackEstimateUsd = textEst < 0 ? 0 : textEst;
        if (request.VariantKinds is { Count: > 0 })
            row.VariantKindsJson = ContentRepository.ToJson(request.VariantKinds);
        if (request.ConnectorTypes is { Count: > 0 })
            row.ConnectorTypesJson = ContentRepository.ToJson(request.ConnectorTypes);
        if (request.ChannelTypes is { Count: > 0 })
            row.ChannelTypesJson = ContentRepository.ToJson(request.ChannelTypes);

        await _repo.UpdateOrgSettingsAsync(row, cancellationToken);
        return await GetAsync(cancellationToken);
    }

    public async Task<ContentBudgetSnapshotDto> GetBudgetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var org = await _repo.GetOrgSettingsAsync(cancellationToken);
        var globalSpend = await MonthSpendAsync(null, cancellationToken);
        var brands = await _repo.ListBrandsAsync(activeOnly: true, cancellationToken);
        var brandDtos = new List<ContentBrandBudgetDto>();
        foreach (var b in brands)
        {
            var spend = await MonthSpendAsync(b.Id, cancellationToken);
            var ceiling = b.MonthlyCeilingUsd ?? org.MonthlyCeilingUsd;
            var tier = string.IsNullOrWhiteSpace(b.ImageTier) ? org.DefaultImageTier : b.ImageTier!;
            brandDtos.Add(new ContentBrandBudgetDto(
                b.Id,
                b.Code,
                b.Name,
                ceiling,
                spend,
                Math.Max(0, ceiling - spend),
                tier,
                b.PauseWhenExceeded));
        }

        return new ContentBudgetSnapshotDto(
            org.MonthlyCeilingUsd,
            globalSpend,
            Math.Max(0, org.MonthlyCeilingUsd - globalSpend),
            org.DefaultImageTier,
            brandDtos);
    }

    private async Task<decimal> MonthSpendAsync(Guid? brandId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var from = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        return await _repo.SumSpendAsync(brandId, from, ct);
    }

    private static ContentOrgSettingsDto Map(ContentRepository.OrgSettingsRow row, decimal spend) =>
        new(
            row.Id,
            row.MonthlyCeilingUsd,
            row.MaxImageCandidatesPerItem,
            row.RegenMultiplier,
            row.DefaultImageTier,
            ContentRepository.ParseRates(row.ImageRateUsdJson),
            row.TextPackEstimateUsd,
            ContentRepository.ParseStringList(row.VariantKindsJson),
            ContentRepository.ParseStringList(row.ConnectorTypesJson),
            ContentRepository.ParseStringList(row.ChannelTypesJson),
            spend,
            Math.Max(0, row.MonthlyCeilingUsd - spend),
            row.UpdatedAt);

    private static string NormalizeTier(string tier)
    {
        var t = tier.Trim().ToLowerInvariant();
        return t is "lean" or "balanced" or "premium" ? t : "balanced";
    }
}

internal sealed class ContentBrandService : IContentBrandService
{
    private readonly ContentRepository _repo;

    public ContentBrandService(ContentRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<ContentBrandDto>> ListAsync(
        bool? activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListBrandsAsync(activeOnly, cancellationToken);
        var list = new List<ContentBrandDto>();
        foreach (var r in rows)
        {
            var spend = await MonthSpendAsync(r.Id, cancellationToken);
            list.Add(Map(r, spend));
        }
        return list;
    }

    public async Task<ContentBrandDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetBrandAsync(id, cancellationToken);
        if (row is null) return null;
        var spend = await MonthSpendAsync(id, cancellationToken);
        return Map(row, spend);
    }

    public async Task<ContentBrandDto> CreateAsync(
        UpsertContentBrandRequest request,
        CancellationToken cancellationToken = default)
    {
        var id = await _repo.InsertBrandAsync(ToRow(Guid.Empty, request), cancellationToken);
        return (await GetAsync(id, cancellationToken))!;
    }

    public async Task<ContentBrandDto?> UpdateAsync(
        Guid id,
        UpsertContentBrandRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repo.GetBrandAsync(id, cancellationToken);
        if (existing is null) return null;
        var row = ToRow(id, request);
        row.PauseWhenExceeded = request.PauseWhenExceeded ?? existing.PauseWhenExceeded;
        row.IsActive = request.IsActive ?? existing.IsActive;
        row.SortOrder = request.SortOrder ?? existing.SortOrder;
        await _repo.UpdateBrandAsync(row, cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<ContentSiteTargetDto>> ListSitesAsync(
        Guid brandId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListSitesAsync(brandId, cancellationToken);
        return rows.Select(r => new ContentSiteTargetDto(
            r.Id, r.BrandId, r.Code, r.Name, r.ConnectorType, r.BaseUrl,
            r.ConfigJson, r.SecretRef, r.IsActive, r.SortOrder)).ToList();
    }

    public async Task<ContentSiteTargetDto> UpsertSiteAsync(
        Guid brandId,
        UpsertContentSiteTargetRequest request,
        CancellationToken cancellationToken = default)
    {
        var id = await _repo.UpsertSiteAsync(brandId, new ContentRepository.SiteRow
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            ConnectorType = request.ConnectorType.Trim(),
            BaseUrl = request.BaseUrl?.Trim(),
            ConfigJson = NormalizeJson(request.ConfigJson),
            SecretRef = string.IsNullOrWhiteSpace(request.SecretRef) ? null : request.SecretRef.Trim(),
            IsActive = request.IsActive ?? true,
            SortOrder = request.SortOrder ?? 100,
        }, cancellationToken);
        var sites = await ListSitesAsync(brandId, cancellationToken);
        return sites.First(s => s.Id == id);
    }

    public async Task<IReadOnlyList<ContentChannelTargetDto>> ListChannelsAsync(
        Guid brandId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListChannelsAsync(brandId, cancellationToken);
        return rows.Select(r => new ContentChannelTargetDto(
            r.Id, r.BrandId, r.Code, r.Name, r.ChannelType, r.ExternalId,
            r.ConfigJson, r.SecretRef, r.IsActive, r.SortOrder)).ToList();
    }

    public async Task<ContentChannelTargetDto> UpsertChannelAsync(
        Guid brandId,
        UpsertContentChannelTargetRequest request,
        CancellationToken cancellationToken = default)
    {
        var id = await _repo.UpsertChannelAsync(brandId, new ContentRepository.ChannelRow
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            ChannelType = request.ChannelType.Trim(),
            ExternalId = request.ExternalId?.Trim(),
            ConfigJson = NormalizeJson(request.ConfigJson),
            SecretRef = string.IsNullOrWhiteSpace(request.SecretRef) ? null : request.SecretRef.Trim(),
            IsActive = request.IsActive ?? true,
            SortOrder = request.SortOrder ?? 100,
        }, cancellationToken);
        var channels = await ListChannelsAsync(brandId, cancellationToken);
        return channels.First(c => c.Id == id);
    }

    private async Task<decimal> MonthSpendAsync(Guid brandId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var from = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        return await _repo.SumSpendAsync(brandId, from, ct);
    }

    private static ContentBrandDto Map(ContentRepository.BrandRow r, decimal spend) =>
        new(r.Id, r.Code, r.Name, r.DefaultCtaUrl, r.DefaultCtaLabel, r.MonthlyCeilingUsd,
            r.ImageTier, r.PauseWhenExceeded, r.IsActive, r.SortOrder, spend, r.UpdatedAt);

    private static ContentRepository.BrandRow ToRow(Guid id, UpsertContentBrandRequest request) =>
        new()
        {
            Id = id,
            Code = request.Code.Trim().ToLowerInvariant(),
            Name = request.Name.Trim(),
            DefaultCtaUrl = request.DefaultCtaUrl?.Trim(),
            DefaultCtaLabel = request.DefaultCtaLabel?.Trim(),
            MonthlyCeilingUsd = request.MonthlyCeilingUsd,
            ImageTier = string.IsNullOrWhiteSpace(request.ImageTier) ? null : request.ImageTier.Trim().ToLowerInvariant(),
            PauseWhenExceeded = request.PauseWhenExceeded ?? true,
            IsActive = request.IsActive ?? true,
            SortOrder = request.SortOrder ?? 100,
        };

    private static string NormalizeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "{}";
        try
        {
            using var _ = System.Text.Json.JsonDocument.Parse(json);
            return json.Trim();
        }
        catch
        {
            return "{}";
        }
    }
}

internal sealed class ContentTopicService : IContentTopicService
{
    private readonly ContentRepository _repo;

    public ContentTopicService(ContentRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<ContentTopicDto>> ListAsync(
        Guid? brandId,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListTopicsAsync(brandId, status, cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<ContentTopicDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetTopicAsync(id, cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<ContentTopicDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var topic = await GetAsync(id, cancellationToken);
        if (topic is null) return null;
        var variants = (await _repo.ListVariantsAsync(id, cancellationToken))
            .Select(v => new ContentVariantDto(v.Id, v.TopicId, v.Kind, v.Title, v.BodyMarkdown, v.MetaJson, v.UpdatedAt))
            .ToList();
        var assets = (await _repo.ListAssetsAsync(id, cancellationToken))
            .Select(a => new ContentAssetDto(
                a.Id, a.TopicId, a.Kind, a.FileName, a.ContentType, a.Prompt, a.Model,
                a.ImageTier, a.EstimateUsd, a.IsSelected, a.CreatedAt))
            .ToList();
        var jobs = (await _repo.ListPublishJobsAsync(id, cancellationToken)).Select(MapJob).ToList();
        return new ContentTopicDetailDto(topic, variants, assets, jobs);
    }

    public async Task<ContentTopicDto> CreateAsync(
        UpsertContentTopicRequest request,
        CancellationToken cancellationToken = default)
    {
        var id = await _repo.InsertTopicAsync(
            request.BrandId,
            request.Title.Trim(),
            request.Pillar?.Trim(),
            string.IsNullOrWhiteSpace(request.Goal) ? "traffic" : request.Goal.Trim(),
            request.CtaUrl?.Trim(),
            request.UtmCampaign?.Trim(),
            NormalizePriority(request.Priority),
            NormalizeStatus(request.Status),
            request.BodyOutline,
            cancellationToken);
        return (await GetAsync(id, cancellationToken))!;
    }

    public async Task<ContentTopicDto?> UpdateAsync(
        Guid id,
        UpsertContentTopicRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repo.GetTopicAsync(id, cancellationToken);
        if (existing is null) return null;
        await _repo.UpdateTopicAsync(
            id,
            request.BrandId,
            request.Title.Trim(),
            request.Pillar?.Trim(),
            string.IsNullOrWhiteSpace(request.Goal) ? existing.Goal : request.Goal.Trim(),
            request.CtaUrl?.Trim(),
            request.UtmCampaign?.Trim(),
            NormalizePriority(request.Priority ?? existing.Priority),
            NormalizeStatus(request.Status ?? existing.Status),
            request.BodyOutline ?? existing.BodyOutline,
            cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<ContentTopicDto?> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _repo.GetTopicAsync(id, cancellationToken);
        if (existing is null) return null;
        await _repo.UpdateTopicStatusAsync(id, "Approved", cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<bool> SelectAssetAsync(Guid topicId, Guid assetId, CancellationToken cancellationToken = default)
    {
        var asset = await _repo.GetAssetAsync(assetId, cancellationToken);
        if (asset is null || asset.TopicId != topicId) return false;
        await _repo.SelectAssetAsync(topicId, assetId, cancellationToken);
        return true;
    }

    private static ContentTopicDto Map(ContentRepository.TopicRow r) =>
        new(r.Id, r.BrandId, r.BrandCode, r.BrandName, r.Title, r.Pillar, r.Goal,
            r.CtaUrl, r.UtmCampaign, r.Priority, r.Status, r.BodyOutline, r.CreatedAt, r.UpdatedAt);

    internal static ContentPublishJobDto MapJob(ContentRepository.PublishJobRow r) =>
        new(r.Id, r.TopicId, r.BrandId, r.TargetKind, r.SiteTargetId, r.ChannelTargetId,
            r.ConnectorType, r.Status, r.PublishAt, r.ExternalRef, r.LastError, r.ResultJson,
            r.CreatedAt, r.UpdatedAt);

    private static string NormalizePriority(string? p) =>
        p is "P0" or "P1" or "P2" ? p : "P1";

    private static string NormalizeStatus(string? s) =>
        s is "Draft" or "Generating" or "Review" or "Approved" or "Scheduled"
            or "Published" or "BudgetBlocked" or "Rejected"
            ? s
            : "Draft";
}
