using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentOrgSettingsService : IContentOrgSettingsService
{
    private readonly ContentRepository _repo;
    private readonly ContentGeminiClient _gemini;
    private readonly IConfiguration _configuration;
    private readonly ContentOptions _options;

    public ContentOrgSettingsService(
        ContentRepository repo,
        ContentGeminiClient gemini,
        IConfiguration configuration,
        IOptions<ContentOptions> options)
    {
        _repo = repo;
        _gemini = gemini;
        _configuration = configuration;
        _options = options.Value;
    }

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

        if (request.Ai is { } ai)
        {
            var state = ContentAiConfigParser.Parse(row.AiConfigJson);
            if (!string.IsNullOrWhiteSpace(ai.Provider))
                state.Provider = ai.Provider.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(ai.TextModel))
                state.TextModel = ai.TextModel.Trim();
            if (ai.ImageModel is not null)
                state.ImageModel = string.IsNullOrWhiteSpace(ai.ImageModel) ? null : ai.ImageModel.Trim();
            if (ai.ImagesEnabled is { } imagesEnabled)
                state.ImagesEnabled = imagesEnabled;
            if (ai.GeminiApiKeySecretRef is not null)
                state.GeminiApiKeySecretRef = string.IsNullOrWhiteSpace(ai.GeminiApiKeySecretRef)
                    ? null
                    : ai.GeminiApiKeySecretRef.Trim();
            // Write-only key: null = keep; "" = clear; non-empty = replace
            if (ai.GeminiApiKey is not null)
                state.GeminiApiKey = string.IsNullOrWhiteSpace(ai.GeminiApiKey) ? null : ai.GeminiApiKey.Trim();
            row.AiConfigJson = ContentAiConfigParser.ToJson(state);
        }

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

    public async Task<ContentAiTestResultDto> TestAiAsync(CancellationToken cancellationToken = default)
    {
        var resolved = await _gemini.ResolveConfigAsync(cancellationToken);
        if (!resolved.ApiKeyConfigured)
        {
            return new ContentAiTestResultDto(
                false,
                "Chưa có API key — đặt Secret ref (env) hoặc dán key (chỉ ghi, không hiện lại).",
                false,
                resolved.TextModel);
        }

        try
        {
            var json = await _gemini.GenerateJsonAsync(
                "Return valid JSON only.",
                "Reply with JSON: {\"ok\":true,\"ping\":\"content-park\"}",
                cancellationToken);
            var ok = json.Contains("ok", StringComparison.OrdinalIgnoreCase);
            return new ContentAiTestResultDto(
                ok,
                ok ? $"Kết nối OK · model {resolved.TextModel}" : $"Phản hồi lạ: {json[..Math.Min(120, json.Length)]}",
                true,
                resolved.TextModel);
        }
        catch (Exception ex)
        {
            return new ContentAiTestResultDto(
                false,
                ex.Message.Length > 400 ? ex.Message[..400] : ex.Message,
                true,
                resolved.TextModel);
        }
    }

    private async Task<decimal> MonthSpendAsync(Guid? brandId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var from = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        return await _repo.SumSpendAsync(brandId, from, ct);
    }

    private ContentOrgSettingsDto Map(ContentRepository.OrgSettingsRow row, decimal spend)
    {
        var aiState = ContentAiConfigParser.Parse(row.AiConfigJson);
        var resolved = ContentAiConfigParser.Resolve(aiState, _options, _configuration);
        return new(
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
            ContentAiConfigParser.ToDto(aiState, resolved.ApiKeyConfigured),
            spend,
            Math.Max(0, row.MonthlyCeilingUsd - spend),
            row.UpdatedAt);
    }

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
        if (request.OperationalBrief is null)
            row.OperationalBrief = existing.OperationalBrief;
        if (request.Knowledge is null)
        {
            row.ToneJson = existing.ToneJson;
            row.VisualKitJson = existing.VisualKitJson;
        }
        await _repo.UpdateBrandAsync(row, cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<ContentSiteTargetDto>> ListSitesAsync(
        Guid brandId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListSitesAsync(brandId, cancellationToken);
        return rows.Select(MapSite).ToList();
    }

    public async Task<ContentSiteTargetDto> UpsertSiteAsync(
        Guid brandId,
        UpsertContentSiteTargetRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = request.Code.Trim();
        var existing = (await _repo.ListSitesAsync(brandId, cancellationToken))
            .FirstOrDefault(s => string.Equals(s.Code, code, StringComparison.OrdinalIgnoreCase));
        var configJson = ContentTargetSecrets.MergeConfig(
            request.ConfigJson,
            existing?.ConfigJson,
            request.Secret);

        var id = await _repo.UpsertSiteAsync(brandId, new ContentRepository.SiteRow
        {
            Code = code,
            Name = request.Name.Trim(),
            ConnectorType = request.ConnectorType.Trim(),
            BaseUrl = request.BaseUrl?.Trim(),
            ConfigJson = configJson,
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
        return rows.Select(MapChannel).ToList();
    }

    public async Task<ContentChannelTargetDto> UpsertChannelAsync(
        Guid brandId,
        UpsertContentChannelTargetRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = request.Code.Trim();
        var existing = (await _repo.ListChannelsAsync(brandId, cancellationToken))
            .FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));
        var configJson = ContentTargetSecrets.MergeConfig(
            request.ConfigJson,
            existing?.ConfigJson,
            request.Secret);

        var id = await _repo.UpsertChannelAsync(brandId, new ContentRepository.ChannelRow
        {
            Code = code,
            Name = request.Name.Trim(),
            ChannelType = request.ChannelType.Trim(),
            ExternalId = request.ExternalId?.Trim(),
            ConfigJson = configJson,
            SecretRef = string.IsNullOrWhiteSpace(request.SecretRef) ? null : request.SecretRef.Trim(),
            IsActive = request.IsActive ?? true,
            SortOrder = request.SortOrder ?? 100,
        }, cancellationToken);
        var channels = await ListChannelsAsync(brandId, cancellationToken);
        return channels.First(c => c.Id == id);
    }

    private static ContentSiteTargetDto MapSite(ContentRepository.SiteRow r)
    {
        var (redacted, configured) = ContentTargetSecrets.RedactForClient(r.ConfigJson, r.SecretRef);
        return new ContentSiteTargetDto(
            r.Id, r.BrandId, r.Code, r.Name, r.ConnectorType, r.BaseUrl,
            redacted, r.SecretRef, configured, r.IsActive, r.SortOrder);
    }

    private static ContentChannelTargetDto MapChannel(ContentRepository.ChannelRow r)
    {
        var (redacted, configured) = ContentTargetSecrets.RedactForClient(r.ConfigJson, r.SecretRef);
        return new ContentChannelTargetDto(
            r.Id, r.BrandId, r.Code, r.Name, r.ChannelType, r.ExternalId,
            redacted, r.SecretRef, configured, r.IsActive, r.SortOrder);
    }

    private async Task<decimal> MonthSpendAsync(Guid brandId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var from = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        return await _repo.SumSpendAsync(brandId, from, ct);
    }

    private static ContentBrandDto Map(ContentRepository.BrandRow r, decimal spend) =>
        new(
            r.Id, r.Code, r.Name, r.DefaultCtaUrl, r.DefaultCtaLabel, r.MonthlyCeilingUsd,
            r.ImageTier, r.PauseWhenExceeded, r.IsActive, r.SortOrder, r.OperationalBrief,
            ContentBrandKnowledge.Parse(r.ToneJson, r.VisualKitJson),
            spend, r.UpdatedAt);

    private static ContentRepository.BrandRow ToRow(Guid id, UpsertContentBrandRequest request)
    {
        var (toneJson, visualJson) = ContentBrandKnowledge.Serialize(request.Knowledge);
        return new()
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
            OperationalBrief = string.IsNullOrWhiteSpace(request.OperationalBrief)
                ? null
                : request.OperationalBrief.Trim(),
            ToneJson = toneJson,
            VisualKitJson = visualJson,
        };
    }

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
            request.DisplayAt,
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
            request.DisplayAt ?? existing.DisplayAt,
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

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _repo.GetTopicAsync(id, cancellationToken);
        if (existing is null) return false;
        return await _repo.DeleteTopicAsync(id, cancellationToken);
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
            r.CtaUrl, r.UtmCampaign, r.Priority, r.Status, r.BodyOutline, r.DisplayAt,
            r.CreatedAt, r.UpdatedAt);

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

internal sealed class ContentPackageService : IContentPackageService
{
    private readonly ContentRepository _repo;
    private readonly IContentTopicService _topics;
    private readonly IContentGenerateService _generate;

    public ContentPackageService(
        ContentRepository repo,
        IContentTopicService topics,
        IContentGenerateService generate)
    {
        _repo = repo;
        _topics = topics;
        _generate = generate;
    }

    public async Task<IReadOnlyList<ContentPackageDto>> ListAsync(
        Guid? brandId,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListPackagesAsync(brandId, status, cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<ContentPackageDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetPackageAsync(id, cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<ContentPackageDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var package = await GetAsync(id, cancellationToken);
        if (package is null) return null;
        var detail = await _topics.GetDetailAsync(package.TopicId, cancellationToken);
        if (detail is null) return null;
        return new ContentPackageDetailDto(package, detail);
    }

    public async Task<ContentPackageDto> CreateAsync(
        UpsertContentPackageRequest request,
        CancellationToken cancellationToken = default)
    {
        var brand = await _repo.GetBrandAsync(request.BrandId, cancellationToken)
                    ?? throw new InvalidOperationException("Brand not found");

        var title = request.Title.Trim();
        if (title.Length == 0) throw new InvalidOperationException("Title is required");

        var priority = NormalizePriority(request.Priority);
        var goal = string.IsNullOrWhiteSpace(request.Goal) ? "traffic" : request.Goal.Trim();
        var contentType = string.IsNullOrWhiteSpace(request.ContentType) ? "educational" : request.ContentType.Trim();
        var outline = BuildOutline(request.Angle, request.Audience, request.BodyOutline);

        var topicId = await _repo.InsertTopicAsync(
            request.BrandId,
            title,
            request.Pillar?.Trim(),
            goal,
            request.CtaUrl?.Trim() ?? brand.DefaultCtaUrl,
            utm: null,
            priority,
            "Draft",
            outline,
            request.DisplayAt,
            cancellationToken);

        var packageId = await _repo.InsertPackageAsync(
            request.BrandId,
            topicId,
            title,
            request.Angle?.Trim(),
            request.Audience?.Trim(),
            contentType,
            request.Pillar?.Trim(),
            goal,
            priority,
            "Draft",
            sourcePackageId: null,
            cancellationToken);

        return (await GetAsync(packageId, cancellationToken))!;
    }

    public async Task<ContentPackageDto?> UpdateAsync(
        Guid id,
        UpsertContentPackageRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repo.GetPackageAsync(id, cancellationToken);
        if (existing is null) return null;

        var title = request.Title.Trim();
        if (title.Length == 0) throw new InvalidOperationException("Title is required");

        var priority = NormalizePriority(request.Priority ?? existing.Priority);
        var goal = string.IsNullOrWhiteSpace(request.Goal) ? existing.Goal : request.Goal.Trim();
        var contentType = string.IsNullOrWhiteSpace(request.ContentType)
            ? existing.ContentType
            : request.ContentType.Trim();
        var outline = BuildOutline(request.Angle, request.Audience, request.BodyOutline);

        await _repo.UpdatePackageAsync(
            id,
            title,
            request.Angle?.Trim(),
            request.Audience?.Trim(),
            contentType,
            request.Pillar?.Trim(),
            goal,
            priority,
            cancellationToken);

        var topic = await _repo.GetTopicAsync(existing.TopicId, cancellationToken);
        if (topic is not null)
        {
            await _repo.UpdateTopicAsync(
                existing.TopicId,
                existing.BrandId,
                title,
                request.Pillar?.Trim() ?? topic.Pillar,
                goal,
                request.CtaUrl?.Trim() ?? topic.CtaUrl,
                topic.UtmCampaign,
                priority,
                topic.Status,
                outline ?? topic.BodyOutline,
                request.DisplayAt ?? topic.DisplayAt,
                cancellationToken);
        }

        return await GetAsync(id, cancellationToken);
    }

    public async Task<GenerateContentResultDto> GenerateAllAsync(
        Guid id,
        GenerateContentRequest request,
        CancellationToken cancellationToken = default)
    {
        var package = await _repo.GetPackageAsync(id, cancellationToken)
                      ?? throw new InvalidOperationException("Package not found");

        await _repo.UpdatePackageStatusAsync(id, "Generating", cancellationToken);
        try
        {
            var result = await _generate.GenerateAsync(package.TopicId, request, cancellationToken);
            await _repo.UpdatePackageStatusAsync(id, result.Topic.Status, cancellationToken);
            return result;
        }
        catch
        {
            var topic = await _repo.GetTopicAsync(package.TopicId, cancellationToken);
            await _repo.UpdatePackageStatusAsync(id, topic?.Status ?? "Draft", cancellationToken);
            throw;
        }
    }

    public async Task<ContentPackageDto> AdaptAsync(
        Guid id,
        AdaptContentPackageRequest request,
        CancellationToken cancellationToken = default)
    {
        var source = await _repo.GetPackageAsync(id, cancellationToken)
                     ?? throw new InvalidOperationException("Source package not found");
        if (request.TargetBrandId == source.BrandId)
            throw new InvalidOperationException("Chọn thương hiệu đích khác với brand nguồn.");

        var targetBrand = await _repo.GetBrandAsync(request.TargetBrandId, cancellationToken)
                          ?? throw new InvalidOperationException("Target brand not found");

        var sourceTopic = await _repo.GetTopicAsync(source.TopicId, cancellationToken)
                          ?? throw new InvalidOperationException("Source topic missing");

        var title = string.IsNullOrWhiteSpace(request.Title)
            ? source.Title
            : request.Title.Trim();
        var angle = string.IsNullOrWhiteSpace(request.Angle)
            ? $"Góc nhìn {targetBrand.Name}: {source.Angle ?? source.Title}"
            : request.Angle.Trim();
        var outline = BuildOutline(
            angle,
            source.Audience,
            request.BodyOutline
            ?? $"Biến thể từ package {source.BrandName}: «{source.Title}».\n\n{sourceTopic.BodyOutline ?? ""}".Trim());

        var topicId = await _repo.InsertTopicAsync(
            request.TargetBrandId,
            title,
            source.Pillar,
            source.Goal,
            targetBrand.DefaultCtaUrl ?? sourceTopic.CtaUrl,
            utm: null,
            source.Priority,
            "Draft",
            outline,
            request.DisplayAt ?? sourceTopic.DisplayAt,
            cancellationToken);

        var packageId = await _repo.InsertPackageAsync(
            request.TargetBrandId,
            topicId,
            title,
            angle,
            source.Audience,
            source.ContentType,
            source.Pillar,
            source.Goal,
            source.Priority,
            "Draft",
            sourcePackageId: source.Id,
            cancellationToken);

        return (await GetAsync(packageId, cancellationToken))!;
    }

    public async Task<ContentPackageDto?> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var package = await _repo.GetPackageAsync(id, cancellationToken);
        if (package is null) return null;

        var variants = await _repo.ListVariantsAsync(package.TopicId, cancellationToken);
        if (variants.Count == 0)
            throw new InvalidOperationException("Chưa có bản viết — Generate All trước khi duyệt.");

        await _repo.UpdateTopicStatusAsync(package.TopicId, "Approved", cancellationToken);
        await _repo.UpdatePackageStatusAsync(id, "Approved", cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<BatchApprovePackagesResultDto> ApproveBatchAsync(
        BatchApprovePackagesRequest request,
        CancellationToken cancellationToken = default)
    {
        var ids = (request.PackageIds ?? [])
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
            return new BatchApprovePackagesResultDto(0, 0, [], "Không có package nào được chọn.");

        var failed = new List<Guid>();
        var approved = 0;
        foreach (var id in ids)
        {
            try
            {
                var row = await ApproveAsync(id, cancellationToken);
                if (row is null) failed.Add(id);
                else approved++;
            }
            catch
            {
                failed.Add(id);
            }
        }

        return new BatchApprovePackagesResultDto(
            ids.Count,
            approved,
            failed,
            failed.Count == 0
                ? $"Đã duyệt {approved} package."
                : $"Đã duyệt {approved}/{ids.Count}; {failed.Count} lỗi (thiếu bản viết hoặc không tìm thấy).");
    }

    private static ContentPackageDto Map(ContentRepository.PackageRow r) =>
        new(
            r.Id, r.BrandId, r.BrandCode, r.BrandName, r.TopicId, r.Title, r.Angle, r.Audience,
            r.ContentType, r.Pillar, r.Goal, r.Priority, r.Status, r.SourcePackageId, r.DisplayAt,
            r.VariantCount, r.CreatedAt, r.UpdatedAt);

    private static string? BuildOutline(string? angle, string? audience, string? bodyOutline)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(angle)) parts.Add("Angle: " + angle.Trim());
        if (!string.IsNullOrWhiteSpace(audience)) parts.Add("Audience: " + audience.Trim());
        if (!string.IsNullOrWhiteSpace(bodyOutline)) parts.Add(bodyOutline.Trim());
        return parts.Count == 0 ? null : string.Join("\n\n", parts);
    }

    private static string NormalizePriority(string? p) =>
        p is "P0" or "P1" or "P2" ? p : "P1";
}
