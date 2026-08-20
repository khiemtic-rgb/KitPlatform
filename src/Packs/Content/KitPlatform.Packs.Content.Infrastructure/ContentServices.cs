using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentOrgSettingsService : IContentOrgSettingsService
{
    private readonly ContentRepository _repo;
    private readonly ContentGeminiClient _gemini;
    private readonly ContentCreatomateClient _creatomate;
    private readonly ContentElevenLabsClient _elevenLabs;
    private readonly ContentRunwayClient _runway;
    private readonly ContentFalClient _fal;
    private readonly ContentFacebookClient _facebook;
    private readonly IConfiguration _configuration;
    private readonly ContentOptions _options;

    public ContentOrgSettingsService(
        ContentRepository repo,
        ContentGeminiClient gemini,
        ContentCreatomateClient creatomate,
        ContentElevenLabsClient elevenLabs,
        ContentRunwayClient runway,
        ContentFalClient fal,
        ContentFacebookClient facebook,
        IConfiguration configuration,
        IOptions<ContentOptions> options)
    {
        _repo = repo;
        _gemini = gemini;
        _creatomate = creatomate;
        _elevenLabs = elevenLabs;
        _runway = runway;
        _fal = fal;
        _facebook = facebook;
        _configuration = configuration;
        _options = options.Value;
    }

    public async Task<ContentOrgSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetOrgSettingsAsync(cancellationToken);
        var spend = await MonthSpendAsync(brandId: null, cancellationToken);
        return await MapAsync(row, spend, cancellationToken);
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

        if (request.Video is { } video)
        {
            var state = ContentVideoConfigParser.Parse(row.VideoConfigJson);
            if (video.CreatomateApiKeySecretRef is not null)
                state.CreatomateApiKeySecretRef = string.IsNullOrWhiteSpace(video.CreatomateApiKeySecretRef)
                    ? null
                    : video.CreatomateApiKeySecretRef.Trim();
            if (video.CreatomateApiKey is not null)
                state.CreatomateApiKey = string.IsNullOrWhiteSpace(video.CreatomateApiKey)
                    ? null
                    : video.CreatomateApiKey.Trim();
            if (video.ElevenLabsApiKeySecretRef is not null)
                state.ElevenLabsApiKeySecretRef = string.IsNullOrWhiteSpace(video.ElevenLabsApiKeySecretRef)
                    ? null
                    : video.ElevenLabsApiKeySecretRef.Trim();
            if (video.ElevenLabsApiKey is not null)
                state.ElevenLabsApiKey = string.IsNullOrWhiteSpace(video.ElevenLabsApiKey)
                    ? null
                    : video.ElevenLabsApiKey.Trim();
            if (video.ElevenLabsVoiceId is not null)
                state.ElevenLabsVoiceId = string.IsNullOrWhiteSpace(video.ElevenLabsVoiceId)
                    ? null
                    : video.ElevenLabsVoiceId.Trim();
            if (video.PublicMediaBaseUrl is not null)
                state.PublicMediaBaseUrl = string.IsNullOrWhiteSpace(video.PublicMediaBaseUrl)
                    ? null
                    : video.PublicMediaBaseUrl.Trim().TrimEnd('/');
            if (video.CreatomateTemplateId is not null)
            {
                state.CreatomateTemplateId = string.IsNullOrWhiteSpace(video.CreatomateTemplateId)
                    ? null
                    : video.CreatomateTemplateId.Trim();
                await _repo.SetVideoTemplateExternalIdByCodeAsync(
                    ContentVideoConfigParser.CreatomateTemplateCode,
                    state.CreatomateTemplateId,
                    cancellationToken);
            }
            if (video.RunwayApiKeySecretRef is not null)
                state.RunwayApiKeySecretRef = string.IsNullOrWhiteSpace(video.RunwayApiKeySecretRef)
                    ? null
                    : video.RunwayApiKeySecretRef.Trim();
            if (video.RunwayApiKey is not null)
                state.RunwayApiKey = string.IsNullOrWhiteSpace(video.RunwayApiKey)
                    ? null
                    : video.RunwayApiKey.Trim();
            if (video.FalApiKeySecretRef is not null)
                state.FalApiKeySecretRef = string.IsNullOrWhiteSpace(video.FalApiKeySecretRef)
                    ? null
                    : video.FalApiKeySecretRef.Trim();
            if (video.FalApiKey is not null)
                state.FalApiKey = string.IsNullOrWhiteSpace(video.FalApiKey)
                    ? null
                    : video.FalApiKey.Trim();
            row.VideoConfigJson = ContentVideoConfigParser.ToJson(state);
        }

        if (request.Facebook is { } facebook)
        {
            var state = ContentFacebookConfigParser.Parse(row.FacebookConfigJson);
            if (facebook.AppId is not null)
                state.AppId = string.IsNullOrWhiteSpace(facebook.AppId) ? null : facebook.AppId.Trim();
            if (facebook.AppIdSecretRef is not null)
                state.AppIdSecretRef = string.IsNullOrWhiteSpace(facebook.AppIdSecretRef)
                    ? null
                    : facebook.AppIdSecretRef.Trim();
            if (facebook.AppSecretSecretRef is not null)
                state.AppSecretSecretRef = string.IsNullOrWhiteSpace(facebook.AppSecretSecretRef)
                    ? null
                    : facebook.AppSecretSecretRef.Trim();
            if (facebook.AppSecret is not null)
                state.AppSecret = string.IsNullOrWhiteSpace(facebook.AppSecret) ? null : facebook.AppSecret.Trim();
            if (facebook.RedirectUri is not null)
                state.RedirectUri = string.IsNullOrWhiteSpace(facebook.RedirectUri)
                    ? null
                    : facebook.RedirectUri.Trim();
            row.FacebookConfigJson = ContentFacebookConfigParser.ToJson(state);
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

    public async Task<ContentVideoTestResultDto> TestVideoAsync(CancellationToken cancellationToken = default)
    {
        var resolved = await _creatomate.ResolveAsync(cancellationToken);
        var creatomate = await _creatomate.TestConnectionAsync(cancellationToken);
        var eleven = await _elevenLabs.TestConnectionAsync(cancellationToken);
        var runway = await _runway.TestConnectionAsync(cancellationToken);
        var runwayResolved = await _runway.ResolveAsync(cancellationToken);
        var fal = await _fal.TestConnectionAsync(cancellationToken);
        var falResolved = await _fal.ResolveAsync(cancellationToken);
        return new ContentVideoTestResultDto(
            creatomate.Ok,
            creatomate.Message,
            resolved.CreatomateConfigured,
            eleven.Ok,
            eleven.Message,
            resolved.ElevenLabsConfigured,
            resolved.VoiceId,
            runway.Ok,
            runway.Message,
            runwayResolved.RunwayConfigured,
            fal.Ok,
            fal.Message,
            falResolved.FalConfigured);
    }

    public async Task<ContentFacebookTestResultDto> TestFacebookAsync(CancellationToken cancellationToken = default)
    {
        var resolved = await _facebook.ResolveAsync(cancellationToken);
        var test = await _facebook.TestAppAsync(cancellationToken);
        return new ContentFacebookTestResultDto(
            test.Ok,
            test.Message,
            resolved.AppSecretConfigured,
            resolved.AppId);
    }

    private async Task<decimal> MonthSpendAsync(Guid? brandId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var from = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        return await _repo.SumSpendAsync(brandId, from, ct);
    }

    private async Task<ContentOrgSettingsDto> MapAsync(ContentRepository.OrgSettingsRow row, decimal spend, CancellationToken ct)
    {
        var aiState = ContentAiConfigParser.Parse(row.AiConfigJson);
        var resolved = ContentAiConfigParser.Resolve(aiState, _options, _configuration);
        var videoState = ContentVideoConfigParser.Parse(row.VideoConfigJson);
        var videoResolved = ContentVideoConfigParser.Resolve(videoState, _options, _configuration);
        var tmpl = await _repo.GetVideoTemplateByCodeAsync(ContentVideoConfigParser.CreatomateTemplateCode, ct);
        if (!string.IsNullOrWhiteSpace(tmpl?.ExternalTemplateId))
            videoState.CreatomateTemplateId = tmpl.ExternalTemplateId.Trim();
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
            ContentVideoConfigParser.ToDto(videoState, videoResolved),
            ContentFacebookConfigParser.ToDto(
                ContentFacebookConfigParser.Parse(row.FacebookConfigJson),
                ContentFacebookConfigParser.Resolve(
                    ContentFacebookConfigParser.Parse(row.FacebookConfigJson), _options, _configuration)),
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

    public async Task<IReadOnlyList<ContentWritePlanDto>> ListWritePlansAsync(
        Guid? brandId = null,
        CancellationToken cancellationToken = default)
    {
        var brands = await _repo.ListBrandsAsync(true, cancellationToken);
        if (brandId is { } id)
            brands = brands.Where(b => b.Id == id).ToList();
        var org = await _repo.GetOrgSettingsAsync(cancellationToken);
        var list = new List<ContentWritePlanDto>();
        foreach (var brand in brands)
        {
            var sites = await _repo.ListSitesAsync(brand.Id, cancellationToken);
            var channels = await _repo.ListChannelsAsync(brand.Id, cancellationToken);
            var plan = ContentDestinationPlan.FromTargets(sites, channels, org.MaxImageCandidatesPerItem);
            list.Add(new ContentWritePlanDto(
                brand.Id,
                brand.Code,
                brand.Name,
                plan.Slots.Select(s => new ContentWriteSlotDto(s.Key, s.Label, s.DestType, s.VariantKinds)).ToList(),
                plan.VariantKinds,
                plan.Summary));
        }
        return list;
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

    private static ContentBrandDto Map(ContentRepository.BrandRow r, decimal spend)
    {
        var knowledge = ContentBrandKnowledge.Parse(r.ToneJson, r.VisualKitJson);
        var missing = ContentBrandKnowledge.MissingBrain(r.OperationalBrief, knowledge);
        return new(
            r.Id, r.Code, r.Name, r.DefaultCtaUrl, r.DefaultCtaLabel, r.MonthlyCeilingUsd,
            r.ImageTier, r.PauseWhenExceeded, r.IsActive, r.SortOrder, r.OperationalBrief,
            knowledge, spend, r.UpdatedAt, missing.Count == 0, missing);
    }

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
        var brand = await _repo.GetBrandAsync(request.BrandId, cancellationToken)
                    ?? throw new InvalidOperationException("Brand not found");
        var title = request.Title.Trim();
        var pillar = request.Pillar?.Trim();
        var goal = string.IsNullOrWhiteSpace(request.Goal) ? "traffic" : request.Goal.Trim();
        var cta = ContentCtaRouter.Resolve(
            brand.Code, title, pillar, goal, request.BodyOutline, request.CtaUrl, brand.DefaultCtaUrl);
        var id = await _repo.InsertTopicAsync(
            request.BrandId,
            title,
            pillar,
            goal,
            cta,
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
        var brand = await _repo.GetBrandAsync(request.BrandId, cancellationToken)
                    ?? throw new InvalidOperationException("Brand not found");
        var title = request.Title.Trim();
        var pillar = request.Pillar?.Trim();
        var goal = string.IsNullOrWhiteSpace(request.Goal) ? existing.Goal : request.Goal.Trim();
        var outline = request.BodyOutline ?? existing.BodyOutline;
        var cta = ContentCtaRouter.Resolve(
            brand.Code, title, pillar, goal, outline, request.CtaUrl, brand.DefaultCtaUrl);
        await _repo.UpdateTopicAsync(
            id,
            request.BrandId,
            title,
            pillar,
            goal,
            cta,
            request.UtmCampaign?.Trim(),
            NormalizePriority(request.Priority ?? existing.Priority),
            NormalizeStatus(request.Status ?? existing.Status),
            outline,
            request.DisplayAt ?? existing.DisplayAt,
            cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<ContentTopicDto?> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _repo.GetTopicAsync(id, cancellationToken);
        if (existing is null) return null;
        await _repo.UpdateTopicStatusAsync(id, "Approved", cancellationToken);
        await _repo.EnsureTopicDisplayAtAsync(id, cancellationToken);
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
            r.CreatedAt, r.UpdatedAt, r.VariantCount, r.CorePackageId, r.CoreTitle);

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
    private readonly IContentPublishService _publish;
    private readonly ContentGeminiClient _gemini;

    public ContentPackageService(
        ContentRepository repo,
        IContentTopicService topics,
        IContentGenerateService generate,
        IContentPublishService publish,
        ContentGeminiClient gemini)
    {
        _repo = repo;
        _topics = topics;
        _generate = generate;
        _publish = publish;
        _gemini = gemini;
    }

    public async Task<IReadOnlyList<ContentPackageDto>> ListAsync(
        Guid? brandId,
        string? status,
        bool coresOnly = false,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListPackagesAsync(brandId, status, coresOnly, cancellationToken);
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
        var children = await _repo.ListPackagesBySourceAsync(id, cancellationToken);
        return new ContentPackageDetailDto(package, detail, children.Select(Map).ToList());
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
            ContentCtaRouter.Resolve(
                brand.Code, title, request.Pillar, goal, outline, request.CtaUrl, brand.DefaultCtaUrl),
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

        var core = ContentPackageExtra.FromRequest(request);
        var extra = ContentPackageExtra.Merge("{}", core, []);
        extra = ContentPackageExtra.MergeBrief(extra, request.CreativeBrief);
        await _repo.UpdatePackageExtraJsonAsync(packageId, extra, cancellationToken);

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

        var existingExtra = (await _repo.GetPackageAsync(id, cancellationToken))?.ExtraJson;
        var (prevCore, prevFits) = ContentPackageExtra.Parse(existingExtra);
        var core = ContentPackageExtra.FromRequest(request, prevCore);
        var extra = ContentPackageExtra.Merge(existingExtra, core, prevFits);
        if (request.CreativeBrief is not null)
            extra = ContentPackageExtra.MergeBrief(extra, request.CreativeBrief);
        await _repo.UpdatePackageExtraJsonAsync(id, extra, cancellationToken);

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
        if (request.TargetBrandId == source.BrandId && source.SourcePackageId is not null)
            throw new InvalidOperationException("Góc brand không nhân bản thêm lần nữa cho cùng brand.");

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
            ContentCtaRouter.Resolve(
                targetBrand.Code,
                title,
                source.Pillar,
                source.Goal,
                outline,
                targetBrand.DefaultCtaUrl ?? sourceTopic.CtaUrl,
                targetBrand.DefaultCtaUrl),
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

        var (core, _) = ContentPackageExtra.Parse(source.ExtraJson);
        var extra = ContentPackageExtra.Merge("{}", core, []);
        extra = ContentPackageExtra.MergeBrief(extra, ContentPackageExtra.ParseBrief(source.ExtraJson));
        await _repo.UpdatePackageExtraJsonAsync(packageId, extra, cancellationToken);

        return (await GetAsync(packageId, cancellationToken))!;
    }

    public async Task<IReadOnlyList<ContentBrandFitDto>> AnalyzeAndAdaptAsync(
        Guid id,
        AnalyzeAdaptRequest request,
        CancellationToken cancellationToken = default)
    {
        var source = await _repo.GetPackageAsync(id, cancellationToken)
                     ?? throw new InvalidOperationException("Package not found");
        var sourceTopic = await _repo.GetTopicAsync(source.TopicId, cancellationToken)
                          ?? throw new InvalidOperationException("Source topic missing");
        var (core, _) = ContentPackageExtra.Parse(source.ExtraJson);

        var brands = (await _repo.ListBrandsAsync(true, cancellationToken))
            .Where(b => b.IsActive && (request.IncludeSourceBrand || b.Id != source.BrandId))
            .ToList();
        if (request.BrandIds is { Count: > 0 })
        {
            var allow = request.BrandIds.ToHashSet();
            brands = brands.Where(b => allow.Contains(b.Id)).ToList();
        }

        if (brands.Count == 0)
            throw new InvalidOperationException("Không còn thương hiệu nào để chấm Fit.");

        var brandBlocks = new System.Text.StringBuilder();
        foreach (var brand in brands)
        {
            var knowledge = ContentBrandKnowledge.Parse(brand.ToneJson, brand.VisualKitJson);
            brandBlocks.AppendLine("---");
            brandBlocks.AppendLine("BrandCode: " + brand.Code);
            brandBlocks.AppendLine("BrandName: " + brand.Name);
            brandBlocks.AppendLine(ContentBrandKnowledge.FormatForPrompt(knowledge, brand.OperationalBrief));
            if (!string.IsNullOrWhiteSpace(brand.DefaultCtaLabel) || !string.IsNullOrWhiteSpace(brand.DefaultCtaUrl))
                brandBlocks.AppendLine("Default CTA: " + (brand.DefaultCtaLabel ?? "") + " " + (brand.DefaultCtaUrl ?? ""));
            brandBlocks.AppendLine();
        }

        var system =
            "You are a Vietnamese brand strategist for KIT Marketing Park.\n" +
            "Return valid JSON only. No markdown fences.\n" +
            "A Core Idea is brand-independent. Each brand must get a DISTINCT angle — never translate or copy the same article.\n" +
            "Read the FULL Brand Brain (problems, claims forbidden, products, pillars, good/bad examples).\n" +
            "verdict=skip when: wrong audience; idea hits claimsForbidden; products/pillars cannot carry the topic; " +
            "or forcing a fit would cheapen the brand (pharmacy SOP on tea/family/city; job listings on pharmacy/tea/family).\n" +
            "If factOrOpinion=fact and source/evidence is missing, do not invent numbers — skip brands that would need invented proof.\n" +
            "verdict must be exactly: fit | maybe | skip.\n" +
            "score is 0-100. Skip should be below 45. Fit is 70+.\n" +
            "Do not invent medical claims, prices, salaries, or guarantees.";

        var user =
            "CORE IDEA\n" +
            "Title: " + source.Title + "\n" +
            "Insight: " + (core.Insight ?? source.Angle ?? "") + "\n" +
            "Problem: " + (core.Problem ?? "") + "\n" +
            "Core message: " + (core.CoreMessage ?? source.Angle ?? "") + "\n" +
            "Audience (origin): " + (source.Audience ?? "") + "\n" +
            "Objective: " + source.Goal + "\n" +
            "Pillar: " + (source.Pillar ?? "") + "\n" +
            "Source: " + (core.Source ?? "") + "\n" +
            "Source URL: " + (core.SourceUrl ?? "") + "\n" +
            "Source type: " + (core.SourceType ?? "") + "\n" +
            "Evidence: " + (core.Evidence ?? "") + "\n" +
            "Fact or opinion: " + (core.FactOrOpinion ?? "") + "\n" +
            "Outline: " + (sourceTopic.BodyOutline ?? "") + "\n\n" +
            "BRANDS TO SCORE (only these codes):\n" +
            brandBlocks + "\n" +
            "Return JSON: { coreIdea: { insight, problem, coreMessage, keywords: [] }, " +
            "fits: [ { brandCode, verdict, score, reason, title, angle, audience, cta, outline } ] }\n" +
            "Include exactly one fits[] row per BrandCode listed. No extra brands.\n" +
            "Angles must differ across brands that are fit/maybe.";

        var raw = await _gemini.GenerateJsonAsync(system, user, cancellationToken);
        var parsed = JsonSerializer.Deserialize<AdaptAiResponse>(raw, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidOperationException("AI không trả JSON Brand Fit hợp lệ.");

        if (parsed.CoreIdea is not null)
        {
            core = new ContentCoreIdeaDto(
                parsed.CoreIdea.Insight ?? core.Insight,
                parsed.CoreIdea.Problem ?? core.Problem,
                parsed.CoreIdea.CoreMessage ?? core.CoreMessage,
                parsed.CoreIdea.Keywords is { Count: > 0 } ? parsed.CoreIdea.Keywords : core.Keywords,
                core.Source,
                core.SourceUrl,
                core.SourceType,
                core.Evidence,
                core.FactOrOpinion);
        }

        var byCode = brands.ToDictionary(b => b.Code, StringComparer.OrdinalIgnoreCase);
        var results = new List<ContentBrandFitDto>();

        foreach (var row in parsed.Fits ?? [])
        {
            if (string.IsNullOrWhiteSpace(row.BrandCode)) continue;
            if (!byCode.TryGetValue(row.BrandCode.Trim(), out var brand)) continue;

            var verdict = (row.Verdict ?? "skip").Trim().ToLowerInvariant();
            if (verdict is not ("fit" or "maybe" or "skip"))
                verdict = "skip";
            var score = Math.Clamp(row.Score, 0, 100);

            Guid? createdId = null;
            var shouldCreate = request.CreatePackages
                && (verdict == "fit" || (verdict == "maybe" && request.IncludeMaybe));
            if (shouldCreate)
            {
                createdId = await MaterializeFitAsync(
                    source,
                    brand,
                    string.IsNullOrWhiteSpace(row.Title) ? source.Title : row.Title.Trim(),
                    string.IsNullOrWhiteSpace(row.Angle)
                        ? $"Góc {brand.Name}: {source.Title}"
                        : row.Angle.Trim(),
                    string.IsNullOrWhiteSpace(row.Audience) ? source.Audience : row.Audience.Trim(),
                    row.Outline,
                    request.GenerateFits && verdict == "fit"
                        ? new GenerateContentRequest()
                        : null,
                    cancellationToken);
            }

            results.Add(new ContentBrandFitDto(
                brand.Id,
                brand.Code,
                brand.Name,
                verdict,
                score,
                row.Reason,
                row.Title,
                row.Angle,
                row.Audience,
                row.Cta,
                createdId,
                row.Outline));
        }

        var scored = results.Select(f => f.BrandId).ToHashSet();
        foreach (var brand in brands)
        {
            if (scored.Contains(brand.Id)) continue;
            results.Add(new ContentBrandFitDto(
                brand.Id,
                brand.Code,
                brand.Name,
                "skip",
                0,
                "AI không trả Fit cho brand này — đã bỏ để tránh ép nội dung.",
                null, null, null, null, null));
        }

        await _repo.UpdatePackageExtraJsonAsync(
            id,
            ContentPackageExtra.Merge(source.ExtraJson, core, results),
            cancellationToken);

        return results;
    }

    public async Task<CreatePoolIdeasResultDto> CreatePoolAsync(
        CreatePoolIdeasRequest request,
        CancellationToken cancellationToken = default)
    {
        var drafts = (request.Ideas ?? [])
            .Where(i => !string.IsNullOrWhiteSpace(i.Title))
            .Take(20)
            .ToList();
        if (drafts.Count == 0)
            throw new InvalidOperationException("Nhập ít nhất một ý tưởng (tối đa 20).");

        var brands = await _repo.ListBrandsAsync(true, cancellationToken);
        var home = request.HomeBrandId is Guid hid
            ? brands.FirstOrDefault(b => b.Id == hid && b.IsActive)
            : brands.FirstOrDefault(b => b.IsActive && b.Code.Contains("KIT", StringComparison.OrdinalIgnoreCase))
              ?? brands.FirstOrDefault(b => b.IsActive);
        if (home is null)
            throw new InvalidOperationException("Chưa có thương hiệu active để lưu Core Idea.");

        var created = new List<ContentPackageDto>();
        foreach (var draft in drafts)
        {
            var pkg = await CreateAsync(
                new UpsertContentPackageRequest(
                    home.Id,
                    draft.Title.Trim(),
                    draft.Angle?.Trim(),
                    draft.Audience?.Trim(),
                    "educational",
                    null,
                    string.IsNullOrWhiteSpace(draft.Goal) ? "traffic" : draft.Goal.Trim(),
                    "P1",
                    null,
                    null,
                    null,
                    draft.Insight,
                    draft.Problem,
                    draft.CoreMessage,
                    null,
                    draft.Source,
                    draft.SourceUrl,
                    draft.SourceType,
                    draft.Evidence,
                    draft.FactOrOpinion),
                cancellationToken);
            created.Add(pkg);
        }

        return new CreatePoolIdeasResultDto(
            created,
            $"Đã thêm {created.Count} Core Idea. Chấm Brand Fit trước khi tạo góc — không ép đủ {brands.Count(b => b.IsActive)} brand.");
    }

    public async Task<ApplyPoolFitsResultDto> ApplyPoolFitsAsync(
        ApplyPoolFitsRequest request,
        CancellationToken cancellationToken = default)
    {
        var items = (request.Items ?? [])
            .Where(i => i.PackageId != Guid.Empty && i.BrandId != Guid.Empty)
            .DistinctBy(i => (i.PackageId, i.BrandId))
            .ToList();
        if (items.Count == 0)
            return new ApplyPoolFitsResultDto(0, 0, 0, [], "Chưa chọn ô nào.");

        var brands = (await _repo.ListBrandsAsync(true, cancellationToken))
            .Where(b => b.IsActive)
            .ToDictionary(b => b.Id);
        var created = 0;
        var skipped = 0;
        var applied = new List<ContentBrandFitDto>();

        foreach (var group in items.GroupBy(i => i.PackageId))
        {
            var source = await _repo.GetPackageAsync(group.Key, cancellationToken);
            if (source is null || source.SourcePackageId is not null)
            {
                skipped += group.Count();
                continue;
            }

            var (_, fits) = ContentPackageExtra.Parse(source.ExtraJson);
            var nextFits = fits.ToList();

            foreach (var item in group)
            {
                if (!brands.TryGetValue(item.BrandId, out var brand))
                {
                    skipped++;
                    continue;
                }

                var fit = nextFits.FirstOrDefault(f => f.BrandId == brand.Id);
                var title = string.IsNullOrWhiteSpace(fit?.Title) ? source.Title : fit!.Title!.Trim();
                var angle = string.IsNullOrWhiteSpace(fit?.Angle)
                    ? $"Góc {brand.Name}: {source.Title}"
                    : fit!.Angle!.Trim();
                var audience = string.IsNullOrWhiteSpace(fit?.Audience) ? source.Audience : fit!.Audience!.Trim();
                var outline = fit?.Outline;

                var packageId = await MaterializeFitAsync(
                    source,
                    brand,
                    title,
                    angle,
                    audience,
                    outline,
                    request.GenerateFits && (fit?.Verdict == "fit")
                        ? new GenerateContentRequest(VariantKinds: request.VariantKinds)
                        : null,
                    cancellationToken);
                created++;

                var updated = new ContentBrandFitDto(
                    brand.Id,
                    brand.Code,
                    brand.Name,
                    fit?.Verdict ?? "fit",
                    fit?.Score ?? 0,
                    fit?.Reason,
                    title,
                    angle,
                    audience,
                    fit?.Cta,
                    packageId,
                    outline);
                var idx = nextFits.FindIndex(f => f.BrandId == brand.Id);
                if (idx >= 0) nextFits[idx] = updated;
                else nextFits.Add(updated);
                applied.Add(updated);
            }

            await _repo.UpdatePackageExtraJsonAsync(
                source.Id,
                ContentPackageExtra.Merge(source.ExtraJson, null, nextFits),
                cancellationToken);
        }

        return new ApplyPoolFitsResultDto(
            items.Count,
            created,
            skipped,
            applied,
            skipped == 0
                ? $"Đã tạo {created} góc brand từ {items.Select(i => i.PackageId).Distinct().Count()} ý tưởng."
                : $"Đã tạo {created}; bỏ {skipped} ô (thiếu package/brand).");
    }

    public async Task<SuggestPoolIdeasResultDto> SuggestPoolIdeasAsync(
        SuggestPoolIdeasRequest request,
        CancellationToken cancellationToken = default)
    {
        var want = Math.Clamp(request.Limit <= 0 ? 4 : request.Limit, 1, 6);
        var all = await _repo.ListPackagesAsync(null, null, coresOnly: false, cancellationToken);
        var cores = all.Where(p => p.SourcePackageId is null).ToList();
        if (request.PackageIds is { Count: > 0 })
        {
            var pick = request.PackageIds.ToHashSet();
            cores = cores.Where(p => pick.Contains(p.Id)).ToList();
        }

        if (cores.Count == 0)
            throw new InvalidOperationException(
                "Chưa có ý tưởng gốc. Thêm 1–2 ý ở tab Pool rồi mới gợi ý tiếp.");

        cores = cores.Take(12).ToList();
        var existingTitles = all
            .Select(p => p.Title.Trim())
            .Where(t => t.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var catalog = new StringBuilder();
        foreach (var core in cores)
        {
            var (idea, _) = ContentPackageExtra.Parse(core.ExtraJson);
            catalog.Append("- ").Append(Clip(core.Title, 80));
            if (!string.IsNullOrWhiteSpace(idea.Insight))
                catalog.Append(" | ").Append(Clip(idea.Insight, 70));
            catalog.AppendLine();
        }

        var brands = (await _repo.ListBrandsAsync(true, cancellationToken))
            .Where(b => b.IsActive)
            .Select(b => b.Name)
            .ToList();

        const string system =
            "You are the editorial strategist for KIT Marketing Park.\n"
            + "Return compact JSON only: {\"ideas\":[{\"title\":\"\",\"insight\":\"\",\"whyNext\":\"\","
            + "\"fromTitle\":\"\",\"gap\":\"\",\"suggestedBrands\":\"\",\"factOrOpinion\":\"opinion\"}]}.\n"
            + "Each field ≤ 90 characters. Each idea is the NEXT thesis in the same system — not a rewrite.\n"
            + "fromTitle must copy an existing core title. Do not duplicate titles. Vietnamese. No stats.";

        var user =
            "Existing cores (do not repeat):\n" + catalog + "\n"
            + "Brands: " + string.Join(", ", brands) + "\n"
            + "Propose " + want + " next core ideas.";

        var rows = new List<SuggestPoolIdeaDto>();
        try
        {
            using var geminiCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            geminiCts.CancelAfter(TimeSpan.FromSeconds(22));
            var raw = await _gemini.GenerateJsonAsync(system, user, geminiCts.Token, 1024);
            rows = MapSuggestRows(ParseSuggestIdeas(raw), cores, existingTitles, want);
        }
        catch
        {
            /* Gemini timeout / truncated JSON — catalog fallback below */
        }

        if (rows.Count == 0)
            rows = FallbackSuggest(cores, existingTitles, want);

        if (rows.Count == 0)
            throw new InvalidOperationException(
                "Chưa gợi ý được ý mới. Thêm vài ý gốc khác rồi thử lại.");

        return new SuggestPoolIdeasResultDto(
            rows,
            $"Gợi ý {rows.Count} ý tiếp theo từ {cores.Count} ý đã có. Tick rồi thêm vào pool — không tự tạo góc.");
    }

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string Clip(string? s, int max)
    {
        var t = (s ?? "").Trim();
        if (t.Length <= max) return t;
        return t[..max].TrimEnd() + "…";
    }

    private static readonly JsonSerializerOptions SuggestJsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static List<SuggestPoolIdeaDto> MapSuggestRows(
        IEnumerable<SuggestAiIdea> parsed,
        IReadOnlyList<ContentRepository.PackageRow> cores,
        HashSet<string> existingTitles,
        int want) =>
        parsed
            .Where(i => !string.IsNullOrWhiteSpace(i.Title))
            .Select(i =>
            {
                var title = i.Title!.Trim();
                var from = cores.FirstOrDefault(c =>
                    !string.IsNullOrWhiteSpace(i.FromTitle)
                    && c.Title.Equals(i.FromTitle.Trim(), StringComparison.OrdinalIgnoreCase));
                return new SuggestPoolIdeaDto(
                    title,
                    NullIfEmpty(i.Insight),
                    NullIfEmpty(i.Problem),
                    null,
                    NullIfEmpty(i.WhyNext),
                    from?.Title ?? NullIfEmpty(i.FromTitle),
                    from?.Id,
                    NullIfEmpty(i.Gap),
                    NullIfEmpty(i.SuggestedBrands),
                    string.IsNullOrWhiteSpace(i.FactOrOpinion) ? "opinion" : i.FactOrOpinion.Trim());
            })
            .Where(i => !existingTitles.Contains(i.Title))
            .DistinctBy(i => i.Title, StringComparer.OrdinalIgnoreCase)
            .Take(want)
            .ToList();

    private static List<SuggestPoolIdeaDto> FallbackSuggest(
        IReadOnlyList<ContentRepository.PackageRow> cores,
        HashSet<string> existingTitles,
        int want)
    {
        var stems = new (string Suffix, string Why, string Gap)[]
        {
            (" — việc cần làm tuần này", "Cùng thesis, đưa ra hành động.", "Chưa có bước làm cụ thể"),
            (" — chỗ hay tự dối", "Đào sâu lỗ của ý gốc.", "Chưa chỉ ra chỗ tự lừa"),
            (" — dấu hiệu đã lệch", "Cùng hệ, góc nhận diện sớm.", "Chưa có tín hiệu cảnh báo"),
        };
        var list = new List<SuggestPoolIdeaDto>();
        foreach (var core in cores)
        {
            foreach (var s in stems)
            {
                var seed = Clip(core.Title, 36).TrimEnd('…', '.', ' ', '—');
                var title = seed + s.Suffix;
                if (existingTitles.Contains(title)) continue;
                existingTitles.Add(title);
                list.Add(new SuggestPoolIdeaDto(
                    title, null, null, null, s.Why, core.Title, core.Id, s.Gap, null, "opinion"));
                if (list.Count >= want) return list;
            }
        }

        return list;
    }

    private static List<SuggestAiIdea> ParseSuggestIdeas(string raw)
    {
        var text = raw.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var start = text.IndexOf('[');
            if (start < 0) start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start >= 0 && end > start)
                text = text[start..(end + 1)];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<SuggestAiResponse>(text, SuggestJsonOpts);
            if (parsed?.Ideas is { Count: > 0 })
                return parsed.Ideas.Where(i => !string.IsNullOrWhiteSpace(i.Title)).ToList();
        }
        catch (JsonException)
        {
            /* truncated — take complete inner objects */
        }

        var ideas = new List<SuggestAiIdea>();
        var arrayAt = text.IndexOf('[');
        var i = arrayAt >= 0 ? arrayAt : 0;
        while (i < text.Length)
        {
            var open = text.IndexOf('{', i);
            if (open < 0) break;
            var close = FindMatchingBrace(text, open);
            if (close < 0) break;
            var slice = text[open..(close + 1)];
            i = close + 1;
            if (!slice.Contains("\"title\"", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                var idea = JsonSerializer.Deserialize<SuggestAiIdea>(slice, SuggestJsonOpts);
                if (idea is not null && !string.IsNullOrWhiteSpace(idea.Title))
                    ideas.Add(idea);
            }
            catch (JsonException)
            {
                /* skip broken object */
            }
        }

        if (ideas.Count == 0)
            throw new JsonException("empty suggest payload");
        return ideas;
    }

    private static int FindMatchingBrace(string text, int open)
    {
        var depth = 0;
        var inStr = false;
        var escape = false;
        for (var i = open; i < text.Length; i++)
        {
            var c = text[i];
            if (inStr)
            {
                if (escape) escape = false;
                else if (c == '\\') escape = true;
                else if (c == '"') inStr = false;
                continue;
            }

            if (c == '"') inStr = true;
            else if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0) return i;
            }
        }

        return -1;
    }

    public async Task<ContentPackageDto?> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var package = await _repo.GetPackageAsync(id, cancellationToken);
        if (package is null) return null;

        var variants = await _repo.ListVariantsAsync(package.TopicId, cancellationToken);
        if (variants.Count == 0)
            throw new InvalidOperationException("Chưa có bản viết — Generate All trước khi duyệt.");

        var gate = await ContentQualityRunner.EvaluateAsync(_repo, package, cancellationToken);
        await ContentQualityRunner.PersistAsync(_repo, package.Id, package.ExtraJson, gate, cancellationToken);
        ContentQualityRunner.ThrowIfCannotApprove(gate);

        await _repo.UpdateTopicStatusAsync(package.TopicId, "Approved", cancellationToken);
        await _repo.EnsureTopicDisplayAtAsync(package.TopicId, cancellationToken);
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
                : $"Đã duyệt {approved}/{ids.Count}; {failed.Count} lỗi (thiếu bản viết, Brief, hoặc gate chặn).");
    }

    public async Task<ContentPackageDto?> UpdateBriefAsync(
        Guid id,
        ContentCreativeBriefDto brief,
        CancellationToken cancellationToken = default)
    {
        var package = await _repo.GetPackageAsync(id, cancellationToken);
        if (package is null) return null;

        var extra = ContentPackageExtra.MergeBrief(package.ExtraJson, brief);
        await _repo.UpdatePackageExtraJsonAsync(id, extra, cancellationToken);
        package = await _repo.GetPackageAsync(id, cancellationToken);
        if (package is null) return null;

        if (package.VariantCount > 0)
        {
            var nextGate = await ContentQualityRunner.EvaluateAsync(_repo, package, cancellationToken);
            await ContentQualityRunner.PersistAsync(_repo, package.Id, package.ExtraJson, nextGate, cancellationToken);
        }

        return await GetAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<ContentPerformanceDto>> ListPerformanceAsync(
        Guid packageId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListPerformanceAsync(packageId, cancellationToken);
        return rows.Select(MapPerformance).ToList();
    }

    public async Task<ContentPerformanceDto> IngestPerformanceAsync(
        Guid packageId,
        IngestContentPerformanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var package = await _repo.GetPackageAsync(packageId, cancellationToken)
                      ?? throw new InvalidOperationException("Package not found");
        var channel = (request.Channel ?? "").Trim().ToLowerInvariant();
        if (channel.Length == 0)
            throw new InvalidOperationException("Chọn kênh đo (Fanpage / website / khác).");

        var day = request.MetricDate == default
            ? DateTime.UtcNow.Date
            : request.MetricDate.Date;
        var id = await _repo.InsertPerformanceAsync(
            package.Id,
            package.TopicId,
            package.BrandId,
            channel,
            day,
            request.Impressions,
            request.Views,
            request.Clicks,
            request.Engagements,
            request.Comments,
            request.Shares,
            request.UtmCampaign?.Trim(),
            request.UtmSource?.Trim(),
            request.UtmMedium?.Trim(),
            request.Notes?.Trim(),
            cancellationToken);
        var row = await _repo.GetPerformanceAsync(id, cancellationToken)
                  ?? throw new InvalidOperationException("Không lưu được số liệu.");
        return MapPerformance(row);
    }

    public async Task<(byte[] Bytes, string FileName)> ExportManualPackAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var detail = await GetDetailAsync(id, cancellationToken)
                     ?? throw new InvalidOperationException("Package not found");
        var pkg = detail.Package;
        var variants = detail.TopicDetail.Variants;
        if (variants.Count == 0)
            throw new InvalidOperationException("Chưa có bản viết — Generate kênh trước khi xuất pack.");

        var topic = detail.TopicDetail.Topic;
        var caption = variants.FirstOrDefault(v => v.Kind is "social_caption" or "fb_short" or "fb_page");
        var script = variants.FirstOrDefault(v => v.Kind == "tiktok_script");
        var link = topic.CtaUrl ?? "";

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteZip(zip, "README.txt",
                $"Brand: {pkg.BrandName} ({pkg.BrandCode})\n" +
                $"Title: {pkg.Title}\n" +
                $"Angle: {pkg.Angle}\n" +
                $"Status: {pkg.Status}\n" +
                $"CTA: {link}\n" +
                $"Source: {pkg.CoreIdea?.Source}\n" +
                $"Source URL: {pkg.CoreIdea?.SourceUrl}\n" +
                "Publish = chép tay / đăng thủ công. Không auto-post Meta.\n");

            if (!string.IsNullOrWhiteSpace(link))
                WriteZip(zip, "link.txt", link + "\n");
            if (caption is not null)
                WriteZip(zip, "caption.txt", (caption.Title + "\n\n" + caption.BodyMarkdown).Trim() + "\n");
            if (script is not null)
                WriteZip(zip, "script.txt", (script.Title + "\n\n" + script.BodyMarkdown).Trim() + "\n");

            foreach (var v in variants)
            {
                var safe = string.Join("_", (v.Kind ?? "variant").Split(Path.GetInvalidFileNameChars()));
                WriteZip(zip, $"variants/{safe}.md", $"# {v.Title ?? pkg.Title}\n\n{v.BodyMarkdown}\n");
            }

            foreach (var asset in detail.TopicDetail.Assets)
            {
                var file = await _publish.GetAssetFileAsync(asset.Id, cancellationToken);
                if (file is null) continue;
                var name = string.IsNullOrWhiteSpace(file.Value.FileName) ? $"{asset.Id:N}.bin" : file.Value.FileName;
                var entry = zip.CreateEntry("images/" + name, CompressionLevel.Fastest);
                await using var es = entry.Open();
                await es.WriteAsync(file.Value.Bytes, cancellationToken);
            }
        }

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd");
        var fileName = $"content-pack-{pkg.BrandCode}-{stamp}.zip";
        return (ms.ToArray(), fileName);
    }

    private static void WriteZip(ZipArchive zip, string path, string text)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Fastest);
        using var w = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        w.Write(text);
    }

    private static ContentPackageDto Map(ContentRepository.PackageRow r)
    {
        var (core, fits) = ContentPackageExtra.Parse(r.ExtraJson);
        return new(
            r.Id, r.BrandId, r.BrandCode, r.BrandName, r.TopicId, r.Title, r.Angle, r.Audience,
            r.ContentType, r.Pillar, r.Goal, r.Priority, r.Status, r.SourcePackageId, r.SourceTitle, r.DisplayAt,
            r.VariantCount, r.CreatedAt, r.UpdatedAt, core, fits, r.AdaptationCount,
            ContentPackageExtra.ParseGate(r.ExtraJson),
            ContentPackageExtra.ParseBrief(r.ExtraJson));
    }

    private static ContentPerformanceDto MapPerformance(ContentRepository.PerformanceRow r) =>
        new(
            r.Id, r.PackageId, r.TopicId, r.BrandId, r.BrandCode, r.BrandName,
            r.Channel, r.MetricDate, r.Impressions, r.Views, r.Clicks,
            r.Engagements, r.Comments, r.Shares,
            r.UtmCampaign, r.UtmSource, r.UtmMedium, r.Notes, r.CreatedAt);

    private sealed class SuggestAiResponse
    {
        public List<SuggestAiIdea>? Ideas { get; set; }
    }

    private sealed class SuggestAiIdea
    {
        public string? Title { get; set; }
        public string? Insight { get; set; }
        public string? Problem { get; set; }
        public string? CoreMessage { get; set; }
        public string? WhyNext { get; set; }
        public string? FromTitle { get; set; }
        public string? Gap { get; set; }
        public string? SuggestedBrands { get; set; }
        public string? FactOrOpinion { get; set; }
    }

    private sealed class AdaptAiResponse
    {
        public ContentCoreIdeaDto? CoreIdea { get; set; }
        public List<AdaptAiFit>? Fits { get; set; }
    }

    private sealed class AdaptAiFit
    {
        public string? BrandCode { get; set; }
        public string? Verdict { get; set; }
        public int Score { get; set; }
        public string? Reason { get; set; }
        public string? Title { get; set; }
        public string? Angle { get; set; }
        public string? Audience { get; set; }
        public string? Cta { get; set; }
        public string? Outline { get; set; }
    }

    private async Task<Guid> MaterializeFitAsync(
        ContentRepository.PackageRow source,
        ContentRepository.BrandRow brand,
        string title,
        string angle,
        string? audience,
        string? outline,
        GenerateContentRequest? generate,
        CancellationToken cancellationToken)
    {
        Guid packageId;
        var existingId = source.SourcePackageId is null
            ? await _repo.GetPackageIdBySourceAndBrandAsync(source.Id, brand.Id, cancellationToken)
            : null;
        var adaptReq = new AdaptContentPackageRequest(
            brand.Id,
            title,
            angle,
            outline,
            source.DisplayAt);
        if (existingId is Guid eid)
        {
            await _repo.UpdatePackageAsync(
                eid,
                title,
                angle,
                audience,
                source.ContentType,
                source.Pillar,
                source.Goal,
                source.Priority,
                cancellationToken);
            packageId = eid;
        }
        else
        {
            var created = await AdaptAsync(source.Id, adaptReq, cancellationToken);
            packageId = created.Id;
            if (!string.IsNullOrWhiteSpace(audience))
            {
                await _repo.UpdatePackageAsync(
                    created.Id,
                    created.Title,
                    created.Angle,
                    audience,
                    created.ContentType,
                    created.Pillar,
                    created.Goal,
                    created.Priority,
                    cancellationToken);
            }
        }

        if (generate is not null)
        {
            try
            {
                await GenerateAllAsync(packageId, generate, cancellationToken);
            }
            catch
            {
                /* keep Draft for human review */
            }
        }

        return packageId;
    }

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
