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

        var sites = await _repo.ListSitesAsync(brand.Id, cancellationToken);
        var channels = await _repo.ListChannelsAsync(brand.Id, cancellationToken);
        var orgKinds = ContentRepository.ParseStringList(org.VariantKindsJson);
        var destPlan = ContentDestinationPlan.FromTargets(
            sites,
            channels,
            orgKinds,
            org.MaxImageCandidatesPerItem);

        var ai = await _gemini.ResolveConfigAsync(cancellationToken);
        var candidates = Math.Clamp(
            request.CandidateCount ?? destPlan.SuggestedImageCandidates,
            0,
            10);
        if (request.SkipImages || !ai.ImagesEnabled || !destPlan.NeedsImages)
            candidates = 0;
        // Images-only: still respect destination plan unless explicit candidateCount.
        if (request.ImagesOnly && request.CandidateCount is null && destPlan.NeedsImages)
            candidates = Math.Clamp(destPlan.SuggestedImageCandidates, 0, 10);
        if (request.ImagesOnly && !destPlan.NeedsImages)
            throw new InvalidOperationException(
                "Thương hiệu chưa có nơi đăng cần ảnh (website / fanpage…). Vào Thương hiệu → Nơi đăng để thêm.");

        var tier = string.IsNullOrWhiteSpace(brand.ImageTier)
            ? org.DefaultImageTier
            : brand.ImageTier!;
        var rates = ContentRepository.ParseRates(org.ImageRateUsdJson);
        var rate = rates.TryGetValue(tier, out var r) ? r : 0.05m;
        var imageEstimate = candidates * rate * org.RegenMultiplier;
        // Scale text estimate roughly by number of variants vs a full pack of ~4.
        var textEstimate = request.ImagesOnly
            ? 0m
            : Math.Round(
                org.TextPackEstimateUsd * Math.Max(0.35m, destPlan.VariantKinds.Count / 4m),
                4);
        var totalEstimate = textEstimate + imageEstimate;

        if (request.ImagesOnly && candidates == 0)
            throw new InvalidOperationException(
                "Không thể tạo ảnh — bật «Gen ảnh» trong Cấu hình AI, hoặc thêm nơi đăng cần ảnh.");

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

        if (!ai.ApiKeyConfigured)
            throw new InvalidOperationException(
                "Gemini API key missing — vào Nội dung → Cấu hình AI (Secret ref / key), hoặc env GEMINI_API_KEY");

        if (!request.ImagesOnly && string.IsNullOrWhiteSpace(brand.OperationalBrief))
            throw new InvalidOperationException(
                "Thương hiệu chưa có Brief vận hành. Vào Nội dung → Thương hiệu & nơi đăng → Sửa thương hiệu → dán brief tổng hợp (ChatGPT/SoT) rồi mới Nhờ AI.");

        await _repo.UpdateTopicStatusAsync(topicId, "Generating", cancellationToken);

        try
        {
            string imagePrompt;
            if (request.ImagesOnly)
            {
                imagePrompt =
                    $"Professional brand content photo for pharmacy/content marketing: {topic.Title}. " +
                    "Clean modern lighting, no text overlays, brand-safe.";
            }
            else
            {
                var kinds = destPlan.VariantKinds.ToList();
                if (kinds.Count == 0)
                    kinds = ["web_long"];

                var system =
                    "You are a Vietnamese multi-channel content writer for KitPlatform Content Park.\n" +
                    "Return valid JSON only. No markdown fences.\n" +
                    "You MUST follow the brand operational brief strictly: voice, claims allowed/forbidden, CTA rules, themes.\n" +
                    "Do not invent competitor prices, medical claims, or guarantees not in the brief.\n" +
                    "Adapt length to each variant kind.\n" +
                    "ONLY generate the variant kinds listed — do not invent extra channels.";

                var user =
                    "Brand: " + brand.Name + " (" + brand.Code + ")\n" +
                    "=== BRAND OPERATIONAL BRIEF (source of truth) ===\n" +
                    brand.OperationalBrief!.Trim() + "\n" +
                    "=== END BRIEF ===\n\n" +
                    "Publish destinations configured: " + destPlan.Summary + "\n" +
                    "Topic title: " + topic.Title + "\n" +
                    "Pillar: " + (topic.Pillar ?? "") + "\n" +
                    "Goal: " + topic.Goal + "\n" +
                    "CTA URL: " + (topic.CtaUrl ?? brand.DefaultCtaUrl ?? "") + "\n" +
                    "Article outline (optional): " + (topic.BodyOutline ?? "") + "\n" +
                    "Variant kinds required (ONLY these): " + string.Join(", ", kinds) + "\n\n" +
                    "Return JSON object with keys variants (array of {kind,title,bodyMarkdown,meta}) " +
                    "and imagePrompt (English visual prompt aligned with brand brief, no text overlays).\n" +
                    "Include exactly one entry per variant kind listed — no more, no less.";

                var raw = await _gemini.GenerateJsonAsync(system, user, cancellationToken);
                var parsed = JsonSerializer.Deserialize<GeminiPackResponse>(raw, JsonOpts)
                             ?? throw new InvalidOperationException("Failed to parse Gemini JSON");

                if (parsed.Variants is { Count: > 0 })
                {
                    var allow = kinds.ToHashSet(StringComparer.OrdinalIgnoreCase);
                    foreach (var v in parsed.Variants)
                    {
                        if (string.IsNullOrWhiteSpace(v.Kind)) continue;
                        var kind = v.Kind.Trim();
                        if (!allow.Contains(kind)) continue; // drop extras AI invented
                        await _repo.UpsertVariantAsync(
                            topicId,
                            kind,
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
                    kinds.Count,
                    textEstimate,
                    ContentRepository.ToJson(new
                    {
                        model = ai.TextModel,
                        kinds,
                        destinations = destPlan.Summary,
                    }),
                    cancellationToken);

                imagePrompt = string.IsNullOrWhiteSpace(parsed.ImagePrompt)
                    ? $"Professional brand content photo for: {topic.Title}. Clean modern lighting, no text."
                    : parsed.ImagePrompt.Trim();
            }

            var imageOk = 0;
            string? imageError = null;

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
                        request.ImagesOnly
                            ? "Chặn gen ảnh vì trần ngân sách."
                            : "Text đã gen; chặn gen ảnh vì trần ngân sách.",
                        cancellationToken);
                }

                await _repo.DeleteAssetsForTopicAsync(topicId, cancellationToken);
                var root = ResolveAssetRoot();
                Directory.CreateDirectory(root);

                for (var i = 0; i < candidates; i++)
                {
                    try
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
                            IsSelected = imageOk == 0,
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
                        imageOk++;
                    }
                    catch (Exception imgEx)
                    {
                        imageError = imgEx.Message;
                        _logger.LogWarning(imgEx, "Image candidate {I} failed for topic {TopicId}", i + 1, topicId);
                    }
                }

                if (imageOk == 0 && imageError is not null)
                {
                    _logger.LogError("All image candidates failed for topic {TopicId}: {Error}", topicId, imageError);
                }
            }

            await _repo.UpdateTopicStatusAsync(topicId, "Review", cancellationToken);
            string msg;
            if (request.ImagesOnly)
            {
                msg = imageOk > 0
                    ? $"Đã tạo {imageOk} ảnh ({destPlan.Summary})."
                    : "Không tạo được ảnh: " + (imageError ?? "chưa rõ lỗi — kiểm tra Cấu hình AI / model ảnh.");
            }
            else if (candidates == 0)
            {
                msg = $"Đã gen {destPlan.VariantKinds.Count} bản viết theo nơi đăng ({destPlan.Summary}).";
            }
            else if (imageOk > 0)
            {
                msg = $"Đã gen {destPlan.VariantKinds.Count} bản viết + {imageOk} ảnh · {destPlan.Summary}.";
                if (imageOk < candidates && imageError is not null)
                    msg += " Một số ảnh lỗi.";
            }
            else
            {
                msg = $"Đã gen chữ theo nơi đăng ({destPlan.Summary}); ảnh lỗi: " + (imageError ?? "không rõ") +
                      ". Bấm «Tạo ảnh» để thử lại.";
            }

            return await BuildResultAsync(
                topicId,
                totalEstimate,
                budgetBlocked: false,
                msg,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Content generate failed for topic {TopicId}", topicId);
            // Keep existing variants if any — only revert status when this run created nothing useful.
            var hasVariants = (await _repo.ListVariantsAsync(topicId, cancellationToken)).Count > 0;
            await _repo.UpdateTopicStatusAsync(topicId, hasVariants ? "Review" : "Draft", cancellationToken);
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
            row.CtaUrl, row.UtmCampaign, row.Priority, row.Status, row.BodyOutline, row.DisplayAt,
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
