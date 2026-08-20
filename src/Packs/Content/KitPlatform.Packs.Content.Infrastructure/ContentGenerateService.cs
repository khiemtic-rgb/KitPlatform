using System.Text;
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
        var destPlan = ContentDestinationPlan.Restrict(
            ContentDestinationPlan.FromTargets(sites, channels, org.MaxImageCandidatesPerItem),
            request.VariantKinds);

        var ai = await _gemini.ResolveConfigAsync(cancellationToken);
        var candidates = Math.Clamp(
            request.CandidateCount ?? destPlan.SuggestedImageCandidates,
            0,
            10);
        if (request.SkipImages || !ai.ImagesEnabled)
            candidates = 0;
        else if (candidates < 1)
            candidates = 1;
        if (request.ImagesOnly && request.CandidateCount is null)
            candidates = Math.Clamp(Math.Max(destPlan.SuggestedImageCandidates, 1), 1, 10);
        if (request.ImagesOnly && !ai.ImagesEnabled)
            throw new InvalidOperationException(
                "Không thể tạo ảnh — bật «Gen ảnh» trong Cấu hình AI.");

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

        if (!request.ImagesOnly)
        {
            var knowledgeGate = ContentBrandKnowledge.Parse(brand.ToneJson, brand.VisualKitJson);
            if (!ContentBrandKnowledge.HasEnoughForGenerate(brand.OperationalBrief, knowledgeGate))
            {
                var miss = ContentBrandKnowledge.MissingBrain(brand.OperationalBrief, knowledgeGate);
                throw new InvalidOperationException(
                    "Thiếu Brand Brain — vào Thương hiệu → Kiến thức thương hiệu: " + string.Join("; ", miss));
            }
        }

        await _repo.UpdateTopicStatusAsync(topicId, "Generating", cancellationToken);

        try
        {
            string? seedImagePrompt = null;
            if (!request.ImagesOnly)
            {
                var kinds = destPlan.VariantKinds.ToList();
                if (kinds.Count == 0)
                    throw new InvalidOperationException(
                        "Thương hiệu chưa có nơi đăng — vào Thương hiệu → Nơi đăng (Website / Fanpage / nhóm).");

                var wantGroup = kinds.Any(k => k.Equals("group_suggested", StringComparison.OrdinalIgnoreCase));
                var wantWeb = kinds.Any(k => k.Equals("web_long", StringComparison.OrdinalIgnoreCase));
                var packKinds = kinds
                    .Where(k =>
                        !k.Equals("group_suggested", StringComparison.OrdinalIgnoreCase)
                        && !k.Equals("web_long", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var knowledge = ContentBrandKnowledge.Parse(brand.ToneJson, brand.VisualKitJson);
                var brandContext = ContentBrandKnowledge.FormatForPrompt(knowledge, brand.OperationalBrief);
                var packageId = await _repo.GetPackageIdByTopicAsync(topicId, cancellationToken);
                var package = packageId is Guid pid ? await _repo.GetPackageAsync(pid, cancellationToken) : null;
                var (core, _) = ContentPackageExtra.Parse(package?.ExtraJson);
                var brief = ContentPackageExtra.ParseBrief(package?.ExtraJson);
                var briefBlock = ContentCreativeBriefDto.FormatForPrompt(brief);
                var ctaUrl = ContentCtaRouter.Resolve(
                    brand.Code,
                    package?.Title ?? topic.Title,
                    topic.Pillar,
                    topic.Goal,
                    topic.BodyOutline,
                    topic.CtaUrl,
                    brand.DefaultCtaUrl);
                if (!string.Equals(topic.CtaUrl, ctaUrl, StringComparison.Ordinal))
                    await _repo.UpdateTopicCtaAsync(topicId, ctaUrl, cancellationToken);

                var parsed = new GeminiPackResponse();
                if (packKinds.Count > 0)
                {
                    var system =
                        "You are a Vietnamese multi-channel content writer for KIT Marketing Park.\n" +
                        "Return valid JSON only. No markdown fences.\n" +
                        "You MUST follow the FULL Brand Brain: voice, claims allowed/forbidden, proof, good/bad examples.\n" +
                        "Do not invent competitor prices, medical claims, salaries, or guarantees not in the brief or evidence.\n" +
                        "If factOrOpinion=fact and source is missing, do not invent numbers — write qualitatively.\n" +
                        "Never write in another brand's voice. Use this brand's angle only.\n" +
                        "Adapt length to each variant kind.\n" +
                        "Kind guides: seo_meta=title+description; " +
                        "fb_page=PLAIN TEXT 120–220 words, ONE thesis (same as Angle), 3 concrete beats, 1 CTA/question — no **bold**; " +
                        "fb_short=hook+CTA plain text; tiktok_script=timed beats HOOK/PROBLEM/INSIGHT/SOLUTION/CTA 45–60s; " +
                        "social_caption=short post + hashtags line (plain text); linkedin/instagram=native tone.\n" +
                        "Do NOT write web_long or group_suggested here.\n" +
                        "ONLY generate the variant kinds listed — do not invent extra channels.\n" +
                        "Ban filler: «trong thời đại», «không thể phủ nhận», «hãy cùng tìm hiểu», «điều này cho thấy».";
                    if (string.Equals(ctaUrl, ContentCtaRouter.NovixaHealthCheck, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(ctaUrl, ContentCtaRouter.NovixaSpaHealthCheck, StringComparison.OrdinalIgnoreCase))
                    {
                        system +=
                            "\nFor this topic the ONLY allowed CTA URL is " + ctaUrl +
                            " — never homepage novixa.vn or survey.novixa.vn.";
                    }

                    var user =
                        "Brand: " + brand.Name + " (" + brand.Code + ")\n" +
                        brandContext + "\n\n" +
                        "CORE IDEA / ANGLE\n" +
                        "Title: " + (package?.Title ?? topic.Title) + "\n" +
                        "Angle: " + (package?.Angle ?? "") + "\n" +
                        "Audience: " + (package?.Audience ?? "") + "\n" +
                        "Insight: " + (core.Insight ?? "") + "\n" +
                        "Problem: " + (core.Problem ?? "") + "\n" +
                        "Core message: " + (core.CoreMessage ?? "") + "\n" +
                        "Source: " + (core.Source ?? "") + "\n" +
                        "Source URL: " + (core.SourceUrl ?? "") + "\n" +
                        "Evidence: " + (core.Evidence ?? "") + "\n" +
                        "Fact or opinion: " + (core.FactOrOpinion ?? "") + "\n" +
                        (string.IsNullOrWhiteSpace(briefBlock) ? "" : "CREATIVE BRIEF\n" + briefBlock + "\n") +
                        "\nPublish destinations configured: " + destPlan.Summary + "\n" +
                        "Topic title: " + topic.Title + "\n" +
                        "Pillar: " + (topic.Pillar ?? "") + "\n" +
                        "Goal: " + topic.Goal + "\n" +
                        "CTA URL: " + (ctaUrl ?? "") + "\n" +
                        "Article outline (optional): " + (topic.BodyOutline ?? "") + "\n" +
                        "Variant kinds required (ONLY these): " + string.Join(", ", packKinds) + "\n\n" +
                        "Return JSON object with keys variants (array of {kind,title,bodyMarkdown,meta}) " +
                        "and imagePrompt (English visual only: scene, light, people, mood from brand brief. "
                        + "NEVER ask to paint headlines, slogans, brand names, or any letters).\n" +
                        "Include exactly one entry per variant kind listed — no more, no less.";

                    var raw = await _gemini.GenerateJsonAsync(system, user, cancellationToken);
                    parsed = JsonSerializer.Deserialize<GeminiPackResponse>(raw, JsonOpts)
                             ?? throw new InvalidOperationException("Failed to parse Gemini JSON");

                    if (parsed.Variants is { Count: > 0 })
                    {
                        var allow = packKinds.ToHashSet(StringComparer.OrdinalIgnoreCase);
                        foreach (var v in parsed.Variants)
                        {
                            if (string.IsNullOrWhiteSpace(v.Kind)) continue;
                            var kind = v.Kind.Trim();
                            if (!allow.Contains(kind)) continue;
                            await _repo.UpsertVariantAsync(
                                topicId,
                                kind,
                                v.Title?.Trim(),
                                ContentCtaRouter.RewriteBody(brand.Code, v.BodyMarkdown?.Trim() ?? "", ctaUrl),
                                ContentRepository.ToJson(v.Meta ?? new Dictionary<string, JsonElement>()),
                                cancellationToken);
                        }
                    }
                }

                if (wantWeb)
                {
                    await WriteWebLongAsync(
                        topicId,
                        brand,
                        brandContext,
                        package,
                        core,
                        topic,
                        destPlan.Summary,
                        ctaUrl,
                        briefBlock,
                        cancellationToken);
                }

                if (wantGroup)
                {
                    await WriteGroupShareAsync(
                        topicId,
                        brand,
                        knowledge,
                        package,
                        core,
                        topic,
                        cancellationToken);
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

                seedImagePrompt = string.IsNullOrWhiteSpace(parsed.ImagePrompt)
                    ? null
                    : parsed.ImagePrompt.Trim();

                if (package is not null)
                {
                    var written = await _repo.ListVariantsAsync(topicId, cancellationToken);
                    var gate = ContentQualityGate.Evaluate(
                        knowledge,
                        core,
                        package.Angle,
                        written.Select(v => (v.Kind, v.BodyMarkdown)).ToList(),
                        brand.Name,
                        brief);
                    await _repo.UpdatePackageExtraJsonAsync(
                        package.Id,
                        ContentPackageExtra.MergeGate(package.ExtraJson, gate),
                        cancellationToken);
                    if (!gate.Passed && gate.Issues.Count > 0)
                    {
                        destPlan = destPlan with
                        {
                            Summary = destPlan.Summary + " · gate: " + string.Join("; ", gate.Issues.Take(3)),
                        };
                    }
                }
            }

            var imageOk = 0;
            string? imageError = null;
            string imagePrompt = "";

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

                var directed = await DirectImagePromptAsync(topic, brand, seedImagePrompt, cancellationToken);
                imagePrompt = directed.Scene;
                var altScene = directed.AltScene;

                await _repo.DeleteAssetsForTopicAsync(topicId, cancellationToken);
                var root = ResolveAssetRoot();
                Directory.CreateDirectory(root);

                for (var i = 0; i < candidates; i++)
                {
                    try
                    {
                        var shot = i == 0 || string.IsNullOrWhiteSpace(altScene)
                            ? imagePrompt
                            : altScene;
                        var (bytes, model) = await _gemini.GenerateImageAsync(
                            $"{shot}\nSame thesis, composition {i + 1} of {candidates}.",
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

    private async Task WriteWebLongAsync(
        Guid topicId,
        ContentRepository.BrandRow brand,
        string brandContext,
        ContentRepository.PackageRow? package,
        ContentCoreIdeaDto core,
        ContentRepository.TopicRow topic,
        string destSummary,
        string? ctaUrl,
        string? briefBlock,
        CancellationToken cancellationToken)
    {
        var thesis = FirstNonEmpty(package?.Angle, core.CoreMessage, core.Insight, topic.Title);
        var system =
            "You are a senior Vietnamese brand editor for KIT Marketing Park.\n" +
            "Return valid JSON only: {\"title\":\"...\",\"bodyMarkdown\":\"...\"}. No markdown fences.\n" +
            "Write ONE flagship article (web_long). Not a multi-channel pack.\n" +
            "ONE thesis only — the Angle. Every H2 must prove that thesis. Do not survey the topic.\n" +
            "Structure:\n" +
            "- Lead (80–120 words): one scene + the problem. No «trong thời đại», «hãy cùng tìm hiểu».\n" +
            "- Exactly 3 ## headings. Each: one argument + one concrete example from Brand Brain (problems/proof).\n" +
            "- Close: one takeaway + CTA URL if provided (plain markdown link).\n" +
            "900–1400 words Vietnamese. Markdown OK (##, lists). No invented numbers, medical claims, or other-brand voice.\n" +
            "If evidence/source is empty, stay qualitative — do not fabricate stats.";

        var user =
            "Brand: " + brand.Name + " (" + brand.Code + ")\n" +
            brandContext + "\n\n" +
            "THESIS (must be the spine): " + thesis + "\n" +
            "Title hint: " + (package?.Title ?? topic.Title) + "\n" +
            "Audience: " + (package?.Audience ?? "") + "\n" +
            "Insight: " + (core.Insight ?? "") + "\n" +
            "Problem: " + (core.Problem ?? "") + "\n" +
            "Core message: " + (core.CoreMessage ?? "") + "\n" +
            "Source: " + (core.Source ?? "") + "\n" +
            "Source URL: " + (core.SourceUrl ?? "") + "\n" +
            "Evidence: " + (core.Evidence ?? "") + "\n" +
            "Fact or opinion: " + (core.FactOrOpinion ?? "") + "\n" +
            (string.IsNullOrWhiteSpace(briefBlock) ? "" : "CREATIVE BRIEF\n" + briefBlock + "\n") +
            "Pillar: " + (topic.Pillar ?? "") + "\n" +
            "Goal: " + topic.Goal + "\n" +
            "CTA URL: " + (ctaUrl ?? "") + "\n" +
            "Outline (optional): " + (topic.BodyOutline ?? "") + "\n" +
            "Destinations (context only): " + destSummary + "\n\n" +
            "Write the article now. title = published headline. bodyMarkdown = full article.";

        string? title = null;
        string? body = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var prompt = attempt == 0
                ? user
                : user + "\n\nBài trước quá mỏng hoặc loãng. Viết LẠI: đúng 3 mục ##, mỗi mục một luận điểm + ví dụ hiện trường, 900–1400 từ.";
            var raw = await _gemini.GenerateJsonAsync(system, prompt, cancellationToken, maxOutputTokens: 8192);
            var parsed = JsonSerializer.Deserialize<GeminiGroupShareResponse>(raw, JsonOpts);
            var nextTitle = (parsed?.Title ?? "").Trim();
            var nextBody = ContentCtaRouter.RewriteBody(brand.Code, parsed?.BodyMarkdown?.Trim() ?? "", ctaUrl);
            if (nextBody.Length < 400) continue;
            title = nextTitle;
            body = nextBody;
            var h2 = ContentQualityGate.CountMarkdownH2(nextBody);
            if (nextBody.Length >= 2200 && h2 >= 2) break;
        }

        if (string.IsNullOrWhiteSpace(body) || body.Length < 400)
            throw new InvalidOperationException("Bài web quá mỏng — Generate lại.");

        body = ContentWebLongRepair.EnsureHeadings(body);

        await _repo.UpsertVariantAsync(
            topicId,
            "web_long",
            string.IsNullOrWhiteSpace(title) ? topic.Title : title,
            body,
            "{}",
            cancellationToken);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
        }

        return "";
    }

    private async Task WriteGroupShareAsync(
        Guid topicId,
        ContentRepository.BrandRow brand,
        ContentBrandKnowledgeDto knowledge,
        ContentRepository.PackageRow? package,
        ContentCoreIdeaDto core,
        ContentRepository.TopicRow topic,
        CancellationToken cancellationToken)
    {
        var banned = GroupBannedTerms(brand.Name, brand.Code, knowledge);
        var situation = StripBanned(core.Problem ?? package?.Angle ?? topic.Title, banned);
        var feeling = StripBanned(core.Insight ?? "", banned);
        var who = StripBanned(package?.Audience ?? "mọi người trong group", banned);
        var theme = StripBanned(package?.Title ?? topic.Title, banned);

        var system =
            "Bạn là thành viên group Facebook, viết bài tâm sự gọn — gần gũi nhưng trình bày sạch, " +
            "không phải copywriter fanpage, không viết giúp thương hiệu.\n" +
            "Return valid JSON only: {\"title\":\"...\",\"bodyMarkdown\":\"...\"}. No markdown fences.\n" +
            "title chỉ để admin nhận diện (không dán lên Facebook). Toàn bộ bài nằm trong bodyMarkdown.\n" +
            "Giọng: lịch sự, đời thường, 1 tình huống + 1 câu hỏi. Không headline marketing, không \"các bác ạ\", không sến.\n" +
            "BỐ CỤC bodyMarkdown (bắt buộc, đúng thứ tự, có \\n):\n" +
            "1) Hook: 1 câu, tối đa 1 emoji ở cuối câu.\n" +
            "2) Dòng trống.\n" +
            "3) Bối cảnh: 2–3 câu, mỗi câu một dòng (không viết thành khối).\n" +
            "4) Dòng trống rồi dòng 'Mình thấy:' rồi 3–4 dòng bắt đầu bằng • (không dùng dấu -).\n" +
            "5) Dòng trống rồi 1 câu hỏi mở cho group (không lời khuyên, không giải pháp).\n" +
            "6) Dòng trống rồi đúng 2 hashtag chủ đề cụ thể (vd #nuoidaycon #manhinh). Cấm #tamsu #cuocsong #giađình chung chung. Cấm hashtag thương hiệu.\n" +
            "Tối đa 3 emoji cả bài. Không **bold**, không heading.\n" +
            "CẤM: tên thương hiệu/sản phẩm/app, URL, CTA, \"mình đang dùng\", \"nên thử\".\n" +
            "Độ dài 70–160 từ.";

        var user =
            "Tình huống mọi người đang bàn (chỉ đời sống, không bán gì):\n" +
            "Gợi ý chủ đề: " + theme + "\n" +
            "Vấn đề đời thường: " + situation + "\n" +
            "Cảm xúc (không biến thành sản phẩm): " + feeling + "\n" +
            "Ai trong group (mô tả người, không phải khách hàng): " + who + "\n\n" +
            "Các từ/cụm SAU TUYỆT ĐỐI không được xuất hiện:\n- " +
            string.Join("\n- ", banned) + "\n\n" +
            "Viết đúng bố cục hook / bối cảnh / • / câu hỏi / 2 hashtag. Không lặp title trong body.";

        string? title = null;
        string? body = null;
        var lastHits = new List<string>();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var prompt = attempt == 0
                ? user
                : user + "\n\nBài trước lệch bố cục hoặc giọng bán. Viết LẠI đúng hook / • / câu hỏi / 2 hashtag. Lỗi: " +
                  string.Join("; ", lastHits);
            var raw = await _gemini.GenerateJsonAsync(system, prompt, cancellationToken);
            var parsed = JsonSerializer.Deserialize<GeminiGroupShareResponse>(raw, JsonOpts);
            var nextTitle = (parsed?.Title ?? "").Trim();
            var nextBody = ContentGroupShareFormat.Normalize(parsed?.BodyMarkdown);
            if (nextBody.Length < 40) continue;
            title = nextTitle;
            body = nextBody;
            lastHits = ContentQualityGate.GroupShareIssues(body, knowledge, brand.Name).ToList();
            if (lastHits.Count == 0 && !LooksLikeGroupPostLayout(body) && attempt < 2)
                lastHits.Add("thiếu xuống dòng / icon / gạch đầu dòng — viết lại như bài group, không một khối");
            if (lastHits.Count == 0) break;
        }

        if (string.IsNullOrWhiteSpace(body) || body.Length < 40)
            throw new InvalidOperationException("Bài nhóm quá ngắn — thử Generate lại.");
        if (lastHits.Count > 0)
            throw new InvalidOperationException(
                "Bài nhóm vẫn giọng bán hàng — Generate lại. " + string.Join("; ", lastHits.Take(3)));

        await _repo.UpsertVariantAsync(
            topicId,
            "group_suggested",
            string.IsNullOrWhiteSpace(title) ? null : title,
            body,
            "{}",
            cancellationToken);
    }

    private static bool LooksLikeGroupPostLayout(string body)
    {
        var lines = body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines.Length < 4) return false;
        var hasBullet = lines.Count(l => l.TrimStart().StartsWith("• ", StringComparison.Ordinal)) >= 3;
        var hasBlank = lines.Any(l => l.Trim().Length == 0);
        var hasHash = body.Contains('#');
        return hasBullet && hasBlank && hasHash;
    }

    private static List<string> GroupBannedTerms(string brandName, string brandCode, ContentBrandKnowledgeDto k)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? s)
        {
            var t = (s ?? "").Trim();
            if (t.Length < 3) return;
            set.Add(t);
            if (t.StartsWith('#')) set.Add(t.TrimStart('#'));
        }

        Add(brandName);
        Add(brandCode);
        foreach (var p in k.Products) Add(p);
        foreach (var p in k.Services) Add(p);
        foreach (var p in k.Hashtags) Add(p);
        foreach (var extra in new[] { "Famixa", "Novixa", "KIT Tech", "landing", "demo", "sản phẩm", "ứng dụng" })
            Add(extra);
        return set.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string StripBanned(string? raw, IReadOnlyList<string> banned)
    {
        var t = (raw ?? "").Trim();
        if (t.Length == 0) return "";
        foreach (var b in banned.OrderByDescending(x => x.Length))
        {
            if (b.Length < 3) continue;
            t = t.Replace(b, "…", StringComparison.OrdinalIgnoreCase);
        }
        return t;
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
            row.CreatedAt, row.UpdatedAt, row.VariantCount, row.CorePackageId, row.CoreTitle);
    }

    private sealed record DirectedImagePrompt(string Scene, string? AltScene);

    private async Task<DirectedImagePrompt> DirectImagePromptAsync(
        ContentRepository.TopicRow topic,
        ContentRepository.BrandRow brand,
        string? seedPrompt,
        CancellationToken ct)
    {
        var knowledge = ContentBrandKnowledge.Parse(brand.ToneJson, brand.VisualKitJson);
        var packageId = await _repo.GetPackageIdByTopicAsync(topic.Id, ct);
        var package = packageId is Guid pid ? await _repo.GetPackageAsync(pid, ct) : null;
        var brief = ContentPackageExtra.ParseBrief(package?.ExtraJson);
        var variants = await _repo.ListVariantsAsync(topic.Id, ct);
        var beats = new StringBuilder();
        foreach (var v in variants.Take(4))
        {
            var body = (v.BodyMarkdown ?? "").Trim();
            if (body.Length > 260) body = body[..260];
            beats.Append("- ").Append(v.Kind);
            if (!string.IsNullOrWhiteSpace(v.Title)) beats.Append(": ").Append(v.Title);
            if (body.Length > 0) beats.Append(" — ").Append(body);
            beats.AppendLine();
        }

        var fallback = SealImagePrompt(
            string.IsNullOrWhiteSpace(seedPrompt)
                ? "Cinematic documentary still of a specific human moment that makes the article thesis feel true. "
                  + "Close or medium shot, one clear subject, shallow depth of field, magazine-cover energy. Not a generic shop interior."
                : seedPrompt.Trim(),
            topic.Title);

        try
        {
            const string system =
                "You are an art director for Vietnamese social and web marketing photos.\n"
                + "Return JSON only: {\"scene\":\"...\",\"altScene\":\"...\"}.\n"
                + "scene = one photorealistic still that proves the ARTICLE THESIS — a specific person doing a real action, "
                + "emotion first, scroll-stopping. Not a catalog pharmacy interior, not a stock handshake.\n"
                + "altScene = same thesis, different distance or viewpoint (detail vs wide).\n"
                + "English visual description only. Never request letters, signs, logos, slogans, or captions.";
            var user =
                "Brand visual: " + (knowledge.VisualStyle ?? "") + " / " + (knowledge.VisualColors ?? "") +
                " / " + (knowledge.ImageNotes ?? "") + "\n" +
                "Audience: " + (knowledge.Audience ?? "") + "\n" +
                (string.IsNullOrWhiteSpace(ContentCreativeBriefDto.FormatForPrompt(brief))
                    ? ""
                    : "CREATIVE BRIEF\n" + ContentCreativeBriefDto.FormatForPrompt(brief) + "\n") +
                "Angle: " + (package?.Angle ?? "") + "\n" +
                "Title: " + topic.Title + "\n" +
                "Pillar: " + (topic.Pillar ?? "") + "\n" +
                "Goal: " + topic.Goal + "\n" +
                "Outline: " + (topic.BodyOutline ?? "") + "\n" +
                "Copy beats:\n" + beats +
                (string.IsNullOrWhiteSpace(seedPrompt) ? "" : "Seed: " + seedPrompt.Trim() + "\n");

            var raw = await _gemini.GenerateJsonAsync(system, user, ct, 900);
            var dto = JsonSerializer.Deserialize<ImageDirectorResponse>(raw, JsonOpts);
            var scene = dto?.Scene?.Trim();
            if (string.IsNullOrWhiteSpace(scene))
                return new DirectedImagePrompt(fallback, null);

            var alt = string.IsNullOrWhiteSpace(dto?.AltScene)
                ? null
                : SealImagePrompt(dto.AltScene.Trim(), topic.Title);
            return new DirectedImagePrompt(SealImagePrompt(scene, topic.Title), alt);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Image director failed for topic {TopicId} — using fallback prompt", topic.Id);
            return new DirectedImagePrompt(fallback, null);
        }
    }

    /// <summary>
    /// Image models garble Vietnamese/Latin letters. Caption lives on the post, never in the pixels.
    /// </summary>
    private static string SealImagePrompt(string scene, string? title)
    {
        var body = scene.Trim();
        if (!string.IsNullOrWhiteSpace(title))
            body += "\nMood/theme only (do not paint these words): " + title.Trim();
        return body
            + "\nHARD RULES: photorealistic photograph. Zero written language in the frame. "
            + "No letters, numbers, logos, captions, watermarks, posters, price tags, product labels, "
            + "or shop signs with readable text. Storefront boards and shelf labels must be blank, "
            + "blurred, or turned away. Screens show graphics only, never words. "
            + "Do not invent brand names. Vietnamese and English text are both forbidden.";
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

    private sealed class ImageDirectorResponse
    {
        public string? Scene { get; set; }
        public string? AltScene { get; set; }
    }

    private sealed class GeminiGroupShareResponse
    {
        public string? Title { get; set; }
        public string? BodyMarkdown { get; set; }
    }
}
