using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentGenerateService : IContentGenerateService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ContentRepository _repo;
    private readonly ContentGeminiClient _gemini;
    private readonly ContentOptions _options;
    private readonly IHostEnvironment _env;
    private readonly ILogger<ContentGenerateService> _logger;

    public ContentGenerateService(
        ContentRepository repo,
        ContentGeminiClient gemini,
        IOptions<ContentOptions> options,
        IHostEnvironment env,
        ILogger<ContentGenerateService> logger)
    {
        _repo = repo;
        _gemini = gemini;
        _options = options.Value;
        _env = env;
        _logger = logger;
    }

    public async Task<GenerateContentResultDto> GenerateAsync(
        Guid topicId,
        GenerateContentRequest request,
        CancellationToken cancellationToken = default)
    {
        var topic = await _repo.GetTopicAsync(topicId, cancellationToken)
                    ?? throw new InvalidOperationException("Topic not found");
        var brand = await _repo.GetBrandAsync(topic.BrandId, cancellationToken)
                    ?? throw new InvalidOperationException("Brand not found");
        var org = await _repo.GetOrgSettingsAsync(cancellationToken);

        var candidates = Math.Clamp(
            request.CandidateCount ?? org.MaxImageCandidatesPerItem,
            0,
            10);
        if (request.SkipImages) candidates = 0;

        var tier = string.IsNullOrWhiteSpace(brand.ImageTier)
            ? org.DefaultImageTier
            : brand.ImageTier!;
        var rates = ContentRepository.ParseRates(org.ImageRateUsdJson);
        var rate = rates.TryGetValue(tier, out var r) ? r : 0.05m;
        var imageEstimate = candidates * rate * org.RegenMultiplier;
        var textEstimate = org.TextPackEstimateUsd;
        var totalEstimate = textEstimate + imageEstimate;

        var monthStart = new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var globalSpend = await _repo.SumSpendAsync(null, monthStart, cancellationToken);
        var brandSpend = await _repo.SumSpendAsync(brand.Id, monthStart, cancellationToken);
        var brandCeiling = brand.MonthlyCeilingUsd ?? org.MonthlyCeilingUsd;

        var globalBlocked = globalSpend + totalEstimate > org.MonthlyCeilingUsd;
        var brandBlocked = brand.PauseWhenExceeded && brandSpend + totalEstimate > brandCeiling;

        if (globalBlocked || brandBlocked)
        {
            await _repo.UpdateTopicStatusAsync(topicId, "BudgetBlocked", cancellationToken);
            await _repo.InsertUsageAsync(
                brand.Id,
                topicId,
                "policy_block",
                tier,
                candidates,
                0,
                ContentRepository.ToJson(new
                {
                    reason = globalBlocked ? "global_ceiling" : "brand_ceiling",
                    globalSpend,
                    brandSpend,
                    totalEstimate,
                    ceiling = globalBlocked ? org.MonthlyCeilingUsd : brandCeiling,
                }),
                cancellationToken);

            var blockedTopic = await ReloadTopicAsync(topicId, cancellationToken);
            return new GenerateContentResultDto(
                blockedTopic,
                [],
                [],
                totalEstimate,
                BudgetBlocked: true,
                Message: globalBlocked
                    ? $"Chạm trần org (${org.MonthlyCeilingUsd:0.##}/tháng). Spend={globalSpend:0.##}, estimate={totalEstimate:0.##}."
                    : $"Chạm trần brand {brand.Code} (${brandCeiling:0.##}/tháng). Spend={brandSpend:0.##}, estimate={totalEstimate:0.##}.");
        }

        if (!_gemini.HasApiKey)
            throw new InvalidOperationException(
                "Gemini API key missing — set Content:GeminiApiKey or GEMINI_API_KEY");

        await _repo.UpdateTopicStatusAsync(topicId, "Generating", cancellationToken);

        try
        {
            var kinds = ContentRepository.ParseStringList(org.VariantKindsJson);
            if (kinds.Count == 0)
                kinds = ["web_long", "fb_page", "fb_short", "seo_meta"];

            var system = """
                You are a Vietnamese multi-channel content writer for KitPlatform Content Park.
                Return valid JSON only. No markdown fences.
                Voice: practical, trustworthy, not hype. Adapt length to each variant kind.
                """;
            var user =
                "Brand: " + brand.Name + " (" + brand.Code + ")\n" +
                "Topic title: " + topic.Title + "\n" +
                "Pillar: " + (topic.Pillar ?? "") + "\n" +
                "Goal: " + topic.Goal + "\n" +
                "CTA URL: " + (topic.CtaUrl ?? brand.DefaultCtaUrl ?? "") + "\n" +
                "Outline: " + (topic.BodyOutline ?? "") + "\n" +
                "Variant kinds required: " + string.Join(", ", kinds) + "\n\n" +
                "Return JSON object with keys variants (array of {kind,title,bodyMarkdown,meta}) " +
                "and imagePrompt (English visual prompt, no text overlays).\n" +
                "Include exactly one entry per variant kind listed.";

            var raw = await _gemini.GenerateJsonAsync(system, user, cancellationToken);
            var parsed = JsonSerializer.Deserialize<GeminiPackResponse>(raw, JsonOpts)
                         ?? throw new InvalidOperationException("Failed to parse Gemini JSON");

            if (parsed.Variants is { Count: > 0 })
            {
                foreach (var v in parsed.Variants)
                {
                    if (string.IsNullOrWhiteSpace(v.Kind)) continue;
                    await _repo.UpsertVariantAsync(
                        topicId,
                        v.Kind.Trim(),
                        v.Title?.Trim(),
                        v.BodyMarkdown?.Trim() ?? "",
                        ContentRepository.ToJson(v.Meta ?? new Dictionary<string, JsonElement>()),
                        cancellationToken);
                }
            }

            await _repo.InsertUsageAsync(
                brand.Id,
                topicId,
                "text_pack",
                null,
                1,
                textEstimate,
                ContentRepository.ToJson(new { model = _options.TextModel }),
                cancellationToken);

            var imagePrompt = string.IsNullOrWhiteSpace(parsed.ImagePrompt)
                ? $"Professional brand content photo for: {topic.Title}. Clean modern lighting, no text."
                : parsed.ImagePrompt.Trim();

            // Re-check before calling image APIs (images are the main cost).
            if (candidates > 0)
            {
                var spendNow = await _repo.SumSpendAsync(null, monthStart, cancellationToken);
                var brandNow = await _repo.SumSpendAsync(brand.Id, monthStart, cancellationToken);
                if (spendNow + imageEstimate > org.MonthlyCeilingUsd
                    || (brand.PauseWhenExceeded && brandNow + imageEstimate > brandCeiling))
                {
                    await _repo.UpdateTopicStatusAsync(topicId, "BudgetBlocked", cancellationToken);
                    await _repo.InsertUsageAsync(
                        brand.Id,
                        topicId,
                        "policy_block_images",
                        tier,
                        candidates,
                        0,
                        ContentRepository.ToJson(new { imageEstimate, spendNow, brandNow }),
                        cancellationToken);

                    return await BuildResultAsync(
                        topicId,
                        imageEstimate + textEstimate,
                        budgetBlocked: true,
                        "Text đã gen; chặn gen ảnh vì trần ngân sách.",
                        cancellationToken);
                }

                await _repo.DeleteAssetsForTopicAsync(topicId, cancellationToken);
                var root = ResolveAssetRoot();
                Directory.CreateDirectory(root);

                for (var i = 0; i < candidates; i++)
                {
                    var (bytes, model) = await _gemini.GenerateImageAsync(
                        $"{imagePrompt}\nVariation {i + 1} of {candidates}.",
                        cancellationToken);
                    var assetId = Guid.CreateVersion7();
                    var fileName = $"{assetId:N}.png";
                    var relDir = Path.Combine(topicId.ToString("N"));
                    var absDir = Path.Combine(root, relDir);
                    Directory.CreateDirectory(absDir);
                    var absPath = Path.Combine(absDir, fileName);
                    await File.WriteAllBytesAsync(absPath, bytes, cancellationToken);
                    var storagePath = Path.Combine(relDir, fileName).Replace('\\', '/');

                    await _repo.InsertAssetAsync(new ContentRepository.AssetRow
                    {
                        Id = assetId,
                        TopicId = topicId,
                        Kind = "image",
                        FileName = fileName,
                        ContentType = "image/png",
                        StoragePath = storagePath,
                        Prompt = imagePrompt,
                        Model = model,
                        ImageTier = tier,
                        EstimateUsd = rate,
                        IsSelected = i == 0,
                        MetaJson = "{}",
                    }, cancellationToken);

                    await _repo.InsertUsageAsync(
                        brand.Id,
                        topicId,
                        "image_gen",
                        tier,
                        1,
                        rate,
                        ContentRepository.ToJson(new { model, candidate = i + 1 }),
                        cancellationToken);
                }
            }

            await _repo.UpdateTopicStatusAsync(topicId, "Review", cancellationToken);
            return await BuildResultAsync(
                topicId,
                totalEstimate,
                budgetBlocked: false,
                "Đã generate variants" + (candidates > 0 ? $" và {candidates} ảnh." : " (không ảnh)."),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Content generate failed for topic {TopicId}", topicId);
            await _repo.UpdateTopicStatusAsync(topicId, "Draft", cancellationToken);
            throw;
        }
    }

    private async Task<GenerateContentResultDto> BuildResultAsync(
        Guid topicId,
        decimal estimate,
        bool budgetBlocked,
        string message,
        CancellationToken ct)
    {
        var topic = await ReloadTopicAsync(topicId, ct);
        var variants = (await _repo.ListVariantsAsync(topicId, ct))
            .Select(v => new ContentVariantDto(v.Id, v.TopicId, v.Kind, v.Title, v.BodyMarkdown, v.MetaJson, v.UpdatedAt))
            .ToList();
        var assets = (await _repo.ListAssetsAsync(topicId, ct))
            .Select(a => new ContentAssetDto(
                a.Id, a.TopicId, a.Kind, a.FileName, a.ContentType, a.Prompt, a.Model,
                a.ImageTier, a.EstimateUsd, a.IsSelected, a.CreatedAt))
            .ToList();
        return new GenerateContentResultDto(topic, variants, assets, estimate, budgetBlocked, message);
    }

    private async Task<ContentTopicDto> ReloadTopicAsync(Guid topicId, CancellationToken ct)
    {
        var row = await _repo.GetTopicAsync(topicId, ct)
                  ?? throw new InvalidOperationException("Topic missing after generate");
        return new ContentTopicDto(
            row.Id, row.BrandId, row.BrandCode, row.BrandName, row.Title, row.Pillar, row.Goal,
            row.CtaUrl, row.UtmCampaign, row.Priority, row.Status, row.BodyOutline,
            row.CreatedAt, row.UpdatedAt);
    }

    private string ResolveAssetRoot()
    {
        var configured = string.IsNullOrWhiteSpace(_options.AssetRoot)
            ? "App_Data/content-assets"
            : _options.AssetRoot;
        return Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(_env.ContentRootPath, configured));
    }

    private sealed class GeminiPackResponse
    {
        public List<GeminiVariant>? Variants { get; set; }
        public string? ImagePrompt { get; set; }
    }

    private sealed class GeminiVariant
    {
        public string? Kind { get; set; }
        public string? Title { get; set; }
        public string? BodyMarkdown { get; set; }
        public Dictionary<string, JsonElement>? Meta { get; set; }
    }
}
