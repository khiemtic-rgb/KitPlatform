using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentPublishService : IContentPublishService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed class EphemeralMedia
    {
        public required byte[] Bytes { get; init; }
        public required string FileName { get; init; }
        public required string ContentType { get; init; }
        public DateTimeOffset? PublishAt { get; init; }
    }

    private readonly ContentRepository _repo;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _env;
    private readonly ContentOptions _options;
    private readonly IContentFacebookConnectionService _facebook;
    private readonly IContentLocalOsPublisher _localOs;
    private readonly ILogger<ContentPublishService> _logger;

    // Per-request media for scoped publish (cleared in finally).
    private EphemeralMedia? _ephemeral;

    public ContentPublishService(
        ContentRepository repo,
        IHttpClientFactory httpFactory,
        IConfiguration configuration,
        IHostEnvironment env,
        IOptions<ContentOptions> options,
        IContentFacebookConnectionService facebook,
        IContentLocalOsPublisher localOs,
        ILogger<ContentPublishService> logger)
    {
        _repo = repo;
        _httpFactory = httpFactory;
        _configuration = configuration;
        _env = env;
        _options = options.Value;
        _facebook = facebook;
        _localOs = localOs;
        _logger = logger;
    }

    public async Task<PublishContentResultDto> PublishAsync(
        Guid topicId,
        PublishContentRequest request,
        CancellationToken cancellationToken = default)
    {
        var topic = await _repo.GetTopicAsync(topicId, cancellationToken)
                    ?? throw new InvalidOperationException("Topic not found");
        if (topic.Status is not ("Review" or "Approved" or "Scheduled" or "Published" or "Draft"))
            throw new InvalidOperationException(
                $"Topic status '{topic.Status}' cannot publish — cần có bản viết (Review/Approved).");

        // Allow Draft if variants exist (local-image publish path after gen).
        if (topic.Status == "Draft")
        {
            var hasVariants = (await _repo.ListVariantsAsync(topicId, cancellationToken)).Count > 0;
            if (!hasVariants)
                throw new InvalidOperationException("Chưa có bản viết — bấm Nhờ AI trước khi xuất bản.");
        }

        await EnforceQualityGateAsync(topicId, topic, cancellationToken, connectorType: "mixed");

        var publishAt = request.PublishAt ?? topic.DisplayAt;
        _ephemeral = null;
        if (!string.IsNullOrWhiteSpace(request.ImageBase64))
        {
            try
            {
                var bytes = Convert.FromBase64String(request.ImageBase64.Trim());
                if (bytes.Length > 0)
                {
                    _ephemeral = new EphemeralMedia
                    {
                        Bytes = bytes,
                        FileName = string.IsNullOrWhiteSpace(request.ImageFileName)
                            ? "cover.jpg"
                            : request.ImageFileName.Trim(),
                        ContentType = string.IsNullOrWhiteSpace(request.ImageContentType)
                            ? "image/jpeg"
                            : request.ImageContentType.Trim(),
                        PublishAt = publishAt,
                    };
                    _logger.LogInformation(
                        "Content publish {TopicId}: ephemeral image {Bytes} bytes ({File})",
                        topicId,
                        bytes.Length,
                        _ephemeral.FileName);
                }
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("ImageBase64 không hợp lệ");
            }
        }
        else if (publishAt is not null)
        {
            _logger.LogWarning("Content publish {TopicId}: no image attached (text-only / MD-only)", topicId);
            _ephemeral = new EphemeralMedia
            {
                Bytes = [],
                FileName = "",
                ContentType = "application/octet-stream",
                PublishAt = publishAt,
            };
        }

        try
        {
            var sites = await _repo.ListSitesAsync(topic.BrandId, cancellationToken);
            var channels = await _repo.ListChannelsAsync(topic.BrandId, cancellationToken);

            var siteIds = request.SiteTargetIds?.ToHashSet() ?? sites.Where(s => s.IsActive).Select(s => s.Id).ToHashSet();
            var channelIds = request.ChannelTargetIds?.ToHashSet()
                             ?? channels.Where(c => c.IsActive).Select(c => c.Id).ToHashSet();

            var jobs = new List<ContentPublishJobDto>();

            if (request.IncludeManualExport)
            {
                var id = await _repo.InsertPublishJobAsync(new ContentRepository.PublishJobRow
                {
                    TopicId = topicId,
                    BrandId = topic.BrandId,
                    TargetKind = "manual",
                    ConnectorType = "manual",
                    Status = "Queued",
                    PublishAt = publishAt,
                }, cancellationToken);
                jobs.Add(await LoadJobAsync(id, cancellationToken));
            }

            foreach (var site in sites.Where(s => siteIds.Contains(s.Id) && s.IsActive))
            {
                if (!IsAutoSiteConnector(site.ConnectorType))
                    continue;
                var id = await _repo.InsertPublishJobAsync(new ContentRepository.PublishJobRow
                {
                    TopicId = topicId,
                    BrandId = topic.BrandId,
                    TargetKind = "site",
                    SiteTargetId = site.Id,
                    ConnectorType = site.ConnectorType,
                    Status = "Queued",
                    PublishAt = publishAt,
                }, cancellationToken);
                jobs.Add(await LoadJobAsync(id, cancellationToken));
            }

            foreach (var ch in channels.Where(c => channelIds.Contains(c.Id) && c.IsActive))
            {
                if (!IsAutoChannelConnector(ch.ChannelType))
                    continue;
                var id = await _repo.InsertPublishJobAsync(new ContentRepository.PublishJobRow
                {
                    TopicId = topicId,
                    BrandId = topic.BrandId,
                    TargetKind = "channel",
                    ChannelTargetId = ch.Id,
                    ConnectorType = "facebook_page",
                    Status = "Queued",
                    PublishAt = publishAt,
                }, cancellationToken);
                jobs.Add(await LoadJobAsync(id, cancellationToken));
            }

            if (jobs.Count == 0)
            {
                throw new InvalidOperationException(
                    "Không có kênh đăng tự động. Chỉ Fanpage, Astro Git, WordPress, Thái Nguyên Life. Instagram / LinkedIn / group: copy tay ở Đăng tay.");
            }

            if (request.RunImmediately)
            {
                var ran = new List<ContentPublishJobDto>();
                foreach (var job in jobs)
                {
                    var updated = await RunJobAsync(job.Id, null, cancellationToken);
                    if (updated is not null) ran.Add(updated);
                }
                jobs = ran;
            }

            var anyOk = jobs.Any(j => j.Status == "Succeeded");
            if (anyOk)
            {
                var scheduled = publishAt is { } at && at > DateTimeOffset.UtcNow.AddMinutes(5);
                await _repo.UpdateTopicStatusAsync(
                    topicId,
                    scheduled ? "Scheduled" : "Published",
                    cancellationToken);
            }

            return new PublishContentResultDto(jobs);
        }
        finally
        {
            _ephemeral = null;
        }
    }

    private async Task EnforceQualityGateAsync(
        Guid topicId,
        ContentRepository.TopicRow topic,
        CancellationToken cancellationToken,
        string? connectorType = null)
    {
        if (ContentQualityGate.RequiresWebStructure(connectorType))
            await RepairWebLongHeadingsAsync(topicId, cancellationToken);

        var packageId = await _repo.GetPackageIdByTopicAsync(topicId, cancellationToken);
        if (packageId is Guid pid)
        {
            var package = await _repo.GetPackageAsync(pid, cancellationToken);
            if (package is null) return;
            var gate = await ContentQualityRunner.EvaluateAsync(_repo, package, cancellationToken);
            await ContentQualityRunner.PersistAsync(_repo, package.Id, package.ExtraJson, gate, cancellationToken);
            ContentQualityRunner.ThrowIfCannotPublish(gate, connectorType);
            return;
        }

        var brand = await _repo.GetBrandAsync(topic.BrandId, cancellationToken)
                    ?? throw new InvalidOperationException("Brand not found");
        var knowledge = ContentBrandKnowledge.Parse(brand.ToneJson, brand.VisualKitJson);
        var variants = await _repo.ListVariantsAsync(topicId, cancellationToken);
        var legacy = ContentQualityGate.Evaluate(
            knowledge,
            null,
            topic.Title,
            variants.Select(v => (v.Kind, v.BodyMarkdown)).ToList(),
            brand.Name);
        ContentQualityRunner.ThrowIfCannotPublish(legacy, connectorType);
    }

    private async Task RepairWebLongHeadingsAsync(Guid topicId, CancellationToken cancellationToken)
    {
        var variants = await _repo.ListVariantsAsync(topicId, cancellationToken);
        var web = variants.FirstOrDefault(v =>
            string.Equals(v.Kind, "web_long", StringComparison.OrdinalIgnoreCase));
        if (web is null) return;
        if (ContentQualityGate.CountMarkdownH2(web.BodyMarkdown) >= 2) return;
        var fixedBody = ContentWebLongRepair.EnsureHeadings(web.BodyMarkdown);
        if (string.Equals(fixedBody, web.BodyMarkdown, StringComparison.Ordinal)) return;
        await _repo.UpsertVariantAsync(
            topicId,
            "web_long",
            web.Title,
            fixedBody,
            string.IsNullOrWhiteSpace(web.MetaJson) ? "{}" : web.MetaJson,
            cancellationToken);
    }

    public async Task<ContentPublishJobDto?> RunJobAsync(
        Guid jobId,
        PublishContentRequest? mediaRequest = null,
        CancellationToken cancellationToken = default)
    {
        var job = await _repo.GetPublishJobAsync(jobId, cancellationToken);
        if (job is null) return null;

        _ephemeral = null;
        if (mediaRequest is not null)
        {
            var publishAt = mediaRequest.PublishAt ?? job.PublishAt;
            if (!string.IsNullOrWhiteSpace(mediaRequest.ImageBase64))
            {
                try
                {
                    var bytes = Convert.FromBase64String(mediaRequest.ImageBase64.Trim());
                    if (bytes.Length > 0)
                    {
                        _ephemeral = new EphemeralMedia
                        {
                            Bytes = bytes,
                            FileName = string.IsNullOrWhiteSpace(mediaRequest.ImageFileName)
                                ? "cover.jpg"
                                : mediaRequest.ImageFileName.Trim(),
                            ContentType = string.IsNullOrWhiteSpace(mediaRequest.ImageContentType)
                                ? "image/jpeg"
                                : mediaRequest.ImageContentType.Trim(),
                            PublishAt = publishAt,
                        };
                    }
                }
                catch (FormatException)
                {
                    throw new InvalidOperationException("ImageBase64 không hợp lệ");
                }
            }
            else if (publishAt is not null)
            {
                _ephemeral = new EphemeralMedia
                {
                    Bytes = [],
                    FileName = "",
                    ContentType = "application/octet-stream",
                    PublishAt = publishAt,
                };
            }
        }

        job.Status = "Running";
        job.LastError = null;
        await _repo.UpdatePublishJobAsync(job, cancellationToken);
        await _repo.InsertPublishLogAsync(jobId, "info", "Job started", "{}", cancellationToken);

        try
        {
            var topic = await _repo.GetTopicAsync(job.TopicId, cancellationToken)
                        ?? throw new InvalidOperationException("Topic missing");
            if (!string.Equals(job.ConnectorType, "manual", StringComparison.OrdinalIgnoreCase))
                await EnforceQualityGateAsync(job.TopicId, topic, cancellationToken, job.ConnectorType);
            var variants = await _repo.ListVariantsAsync(job.TopicId, cancellationToken);
            var assets = await _repo.ListAssetsAsync(job.TopicId, cancellationToken);
            var selected = assets.FirstOrDefault(a => a.IsSelected) ?? assets.FirstOrDefault();

            var result = job.ConnectorType switch
            {
                "manual" => await RunManualAsync(topic, variants, selected, cancellationToken),
                "wordpress_rest" => await RunWordPressAsync(job, topic, variants, selected, cancellationToken),
                "facebook_page" => await RunFacebookAsync(job, topic, variants, selected, cancellationToken),
                "astro_git" => await RunAstroGitAsync(job, topic, variants, selected, cancellationToken),
                "local_os" => await RunLocalOsAsync(job, topic, variants, selected, cancellationToken),
                _ => throw new InvalidOperationException(
                    $"Kênh «{job.ConnectorType}» không đăng tự động — copy tay ở Đăng tay. Chỉ auto: Fanpage, Astro, WordPress, Thái Nguyên Life."),
            };

            job.Status = "Succeeded";
            job.ExternalRef = result.ExternalRef;
            job.ResultJson = result.ResultJson;
            job.LastError = null;
            await _repo.UpdatePublishJobAsync(job, cancellationToken);
            await _repo.InsertPublishLogAsync(jobId, "info", "Job succeeded", result.ResultJson, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Content publish job {JobId} failed", jobId);
            job.Status = "Failed";
            job.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            await _repo.UpdatePublishJobAsync(job, cancellationToken);
            await _repo.InsertPublishLogAsync(
                jobId,
                "error",
                job.LastError,
                ContentRepository.ToJson(new { type = ex.GetType().Name }),
                cancellationToken);
        }
        finally
        {
            _ephemeral = null;
        }

        return await LoadJobAsync(jobId, cancellationToken);
    }

    public async Task<IReadOnlyList<ContentPublishJobDto>> ListJobsAsync(
        Guid? topicId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListPublishJobsAsync(topicId, cancellationToken);
        return rows.Select(ContentTopicService.MapJob).ToList();
    }

    public async Task<(byte[] Bytes, string ContentType, string FileName)?> GetAssetFileAsync(
        Guid assetId,
        CancellationToken cancellationToken = default)
    {
        var asset = await _repo.GetAssetAsync(assetId, cancellationToken);
        if (asset is null) return null;
        var path = Path.Combine(ResolveAssetRoot(), asset.StoragePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path)) return null;
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        return (bytes, asset.ContentType, asset.FileName);
    }

    private async Task<ConnectorResult> RunManualAsync(
        ContentRepository.TopicRow topic,
        IReadOnlyList<ContentRepository.VariantRow> variants,
        ContentRepository.AssetRow? selected,
        CancellationToken ct)
    {
        await Task.CompletedTask;
        var payload = new
        {
            topic = new
            {
                topic.Id,
                topic.Title,
                topic.BrandCode,
                CtaUrl = TopicCta(topic),
                topic.UtmCampaign,
                topic.Goal,
            },
            variants = variants.Select(v => new { v.Kind, v.Title, v.BodyMarkdown }),
            selectedAssetId = selected?.Id,
            assetFile = selected?.FileName,
            exportedAt = DateTimeOffset.UtcNow,
        };
        return new ConnectorResult(null, JsonSerializer.Serialize(payload, JsonOpts));
    }

    private async Task<ConnectorResult> RunLocalOsAsync(
        ContentRepository.PublishJobRow job,
        ContentRepository.TopicRow topic,
        IReadOnlyList<ContentRepository.VariantRow> variants,
        ContentRepository.AssetRow? selected,
        CancellationToken ct)
    {
        var web = variants.FirstOrDefault(v =>
                      string.Equals(v.Kind, "web_long", StringComparison.OrdinalIgnoreCase))
                  ?? variants.FirstOrDefault(v =>
                      !string.Equals(v.Kind, "seo_meta", StringComparison.OrdinalIgnoreCase));
        var seo = variants.FirstOrDefault(v =>
            string.Equals(v.Kind, "seo_meta", StringComparison.OrdinalIgnoreCase));
        var title = (web?.Title ?? topic.Title ?? "").Trim();
        var body = ContentCtaRouter.RewriteThaiNguyenLifeHost((web?.BodyMarkdown ?? "").Trim());
        var seoText = MarkdownToPlainText(seo?.BodyMarkdown ?? "", singleLine: true);
        var brand = await _repo.GetBrandAsync(topic.BrandId, ct);
        var cover = await ResolvePublishImageBytesAsync(selected, ct);
        var published = await _localOs.PublishArticleAsync(
            new ContentLocalOsPublishRequest(
                topic.Id,
                title,
                body,
                string.IsNullOrWhiteSpace(seoText) ? null : seoText,
                brand?.Name,
                topic.BrandCode,
                cover?.Bytes,
                cover?.ContentType,
                cover?.FileName),
            ct);
        _ = job;
        return new ConnectorResult(published.ListingId.ToString("D"), published.ResultJson);
    }

    private async Task<ConnectorResult> RunWordPressAsync(
        ContentRepository.PublishJobRow job,
        ContentRepository.TopicRow topic,
        IReadOnlyList<ContentRepository.VariantRow> variants,
        ContentRepository.AssetRow? selected,
        CancellationToken ct)
    {
        var site = job.SiteTargetId is { } sid
            ? await _repo.GetSiteAsync(sid, ct)
            : null;
        if (site is null)
            throw new InvalidOperationException("Site target missing for WordPress job");

        var baseUrl = (site.BaseUrl ?? "").TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("WordPress base_url is required");

        var cfg = ParseConfig(site.ConfigJson);
        var username = GetConfigString(cfg, "username")
                       ?? throw new InvalidOperationException("WordPress config.username required");
        var password = ResolveTargetSecret(site.SecretRef, site.ConfigJson)
                       ?? GetConfigString(cfg, "applicationPassword")
                       ?? throw new InvalidOperationException(
                           "WordPress token missing — dán mật khẩu ứng dụng vào form nơi đăng hoặc đặt env");
        // Application Passwords are shown with spaces; WP accepts both forms — normalize for Basic auth.
        password = password.Replace(" ", "", StringComparison.Ordinal).Trim();
        username = username.Trim();
        var defaultStatus = GetConfigString(cfg, "status") ?? "draft";

        // Full article only — never use seo_meta as post body (that was short description only).
        var web = variants.FirstOrDefault(v => v.Kind == "web_long")
                  ?? variants.FirstOrDefault(v => v.Kind is "fb_page" or "social_caption")
                  ?? variants.FirstOrDefault(v => v.Kind != "seo_meta");
        var seo = variants.FirstOrDefault(v => v.Kind == "seo_meta");
        var title = web?.Title ?? topic.Title;
        var bodyMd = web?.BodyMarkdown ?? topic.Title;
        var ctaUrl = TopicCta(topic);
        bodyMd = ContentCtaRouter.RewriteBody(topic.BrandCode, bodyMd, ctaUrl);
        if (!string.IsNullOrWhiteSpace(ctaUrl)
            && !bodyMd.Contains(ctaUrl, StringComparison.OrdinalIgnoreCase))
            bodyMd += $"\n\n[Tìm hiểu thêm]({ctaUrl})";
        var contentHtml = MarkdownToSimpleHtml(bodyMd);
        var excerpt = MarkdownToPlainText(
            seo?.BodyMarkdown ?? Trim(bodyMd, 180),
            singleLine: true);

        var http = _httpFactory.CreateClient("content-publish");
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));

        // Probe auth before media/post — clearer error when Authorization is stripped or App Password wrong.
        await EnsureWordPressAuthAsync(http, baseUrl, basic, username, ct);

        int? featuredMediaId = null;
        var mediaBytes = await ResolvePublishImageBytesAsync(selected, ct);
        if (mediaBytes is { } media && media.Bytes.Length > 0)
        {
            featuredMediaId = await UploadWordPressMediaAsync(
                http, baseUrl, basic, media.Bytes, media.FileName, media.ContentType, ct);
        }

        var publishAt = _ephemeral?.PublishAt ?? job.PublishAt ?? topic.DisplayAt;
        var isFuture = publishAt is { } at && at > DateTimeOffset.UtcNow.AddMinutes(5);
        var status = isFuture ? "future" : defaultStatus;

        var categoryIds = await ResolveWordPressCategoryIdsAsync(
            http, baseUrl, basic, cfg, title, topic.Pillar, bodyMd, ct);

        var payload = new Dictionary<string, object?>
        {
            ["title"] = title,
            ["content"] = contentHtml,
            ["excerpt"] = excerpt,
            ["status"] = status,
            ["format"] = "standard",
        };
        if (categoryIds.Count > 0)
            payload["categories"] = categoryIds;
        if (isFuture && publishAt is { } when)
            payload["date_gmt"] = when.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss");
        if (featuredMediaId is { } mid)
            payload["featured_media"] = mid;

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/wp-json/wp/v2/posts");
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        req.Content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOpts),
            Encoding.UTF8,
            "application/json");

        using var res = await http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(HumanizeWordPressError("post", (int)res.StatusCode, body, username));

        using var doc = JsonDocument.Parse(body);
        var id = doc.RootElement.TryGetProperty("id", out var idEl) ? idEl.ToString() : null;
        var link = doc.RootElement.TryGetProperty("link", out var linkEl) ? linkEl.GetString() : null;
        return new ConnectorResult(
            id ?? link,
            ContentRepository.ToJson(new
            {
                wordpressId = id,
                link,
                scheduled = isFuture,
                publishAt,
                featuredMediaId,
                categories = categoryIds,
                hasFullBody = contentHtml.Length > 400,
                imageKeptOnServer = false,
            }));
    }

    private async Task<List<int>> ResolveWordPressCategoryIdsAsync(
        HttpClient http,
        string baseUrl,
        string basicAuth,
        Dictionary<string, JsonElement> cfg,
        string title,
        string? pillar,
        string bodyMd,
        CancellationToken ct)
    {
        var ids = new List<int>();

        // Explicit IDs in config: "wpCategoryIds": [3,5] or "3,5"
        if (cfg.TryGetValue("wpCategoryIds", out var idsEl))
        {
            if (idsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var x in idsEl.EnumerateArray())
                {
                    if (x.ValueKind == JsonValueKind.Number && x.TryGetInt32(out var n) && n > 0)
                        ids.Add(n);
                    else if (x.ValueKind == JsonValueKind.String && int.TryParse(x.GetString(), out var ns) && ns > 0)
                        ids.Add(ns);
                }
            }
            else if (idsEl.ValueKind == JsonValueKind.String)
            {
                foreach (var part in (idsEl.GetString() ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (int.TryParse(part, out var n) && n > 0)
                        ids.Add(n);
                }
            }
        }

        var slugHints = new List<string>();
        var configuredSlugs = GetConfigString(cfg, "wpCategories")
                              ?? GetConfigString(cfg, "wpCategory")
                              ?? GetConfigString(cfg, "blogCategory");
        if (!string.IsNullOrWhiteSpace(configuredSlugs))
        {
            slugHints.AddRange(configuredSlugs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        // Vân Đỉnh Trà: Journal menu only shows category "journal" (id 3) — never leave Uncategorized.
        var isVanDinhTra = baseUrl.Contains("vandinhtra", StringComparison.OrdinalIgnoreCase);
        if (isVanDinhTra && slugHints.Count == 0 && ids.Count == 0)
        {
            slugHints.AddRange(InferVanDinhTraCategorySlugs(title, pillar, bodyMd));
        }

        if (slugHints.Count > 0)
        {
            var map = await FetchWordPressCategorySlugMapAsync(http, baseUrl, basicAuth, ct);
            foreach (var slug in slugHints)
            {
                var key = slug.Trim().ToLowerInvariant();
                if (map.TryGetValue(key, out var id) && id > 0)
                    ids.Add(id);
            }
        }

        if (isVanDinhTra && ids.Count == 0)
        {
            // Hard fallback known Journal category on vandinhtra.vn
            ids.Add(3);
        }

        return ids.Distinct().ToList();
    }

    private static IReadOnlyList<string> InferVanDinhTraCategorySlugs(string title, string? pillar, string body)
    {
        var hay = $"{title}\n{pillar}\n{Trim(body, 400)}".ToLowerInvariant();
        var slugs = new List<string> { "journal" };

        if (ContainsAny(hay, "câu chuyện", "cau chuyen", "hành trình", "hanh trinh", "sứ mệnh", "su menh", "triết lý", "triet ly"))
            slugs.Add("cau-chuyen");
        if (ContainsAny(hay, "kiến thức", "kien thuc", "pha trà", "pha tra", "bảo quản", "bao quan", "phân loại", "phan loai"))
            slugs.Add("kien-thuc-tra");
        if (ContainsAny(hay, "thái nguyên", "thai nguyen", "tân cương", "tan cuong", "vùng chè", "vung che", "đồi chè"))
            slugs.Add("vung-che-thai-nguyen");
        if (ContainsAny(hay, "mùa chè", "mua che", "nhật ký", "nhat ky"))
            slugs.Add("nhat-ky-mua-che");

        var pillarSlug = pillar?.Trim().ToLowerInvariant().Replace(' ', '-');
        if (!string.IsNullOrWhiteSpace(pillarSlug))
            slugs.Add(pillarSlug!);

        return slugs.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static async Task<Dictionary<string, int>> FetchWordPressCategorySlugMapAsync(
        HttpClient http,
        string baseUrl,
        string basicAuth,
        CancellationToken ct)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var url = $"{baseUrl}/wp-json/wp/v2/categories?per_page=100";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
            return map;
        var body = await res.Content.ReadAsStringAsync(ct);
        try
        {
            using var doc = JsonDocument.Parse(body);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var slug = el.TryGetProperty("slug", out var s) ? s.GetString() : null;
                var id = el.TryGetProperty("id", out var idEl) && idEl.TryGetInt32(out var n) ? n : 0;
                if (!string.IsNullOrWhiteSpace(slug) && id > 0)
                    map[slug!] = id;
            }
        }
        catch
        {
            /* ignore */
        }
        return map;
    }

    /// <summary>Minimal Markdown → HTML for WordPress post content.</summary>
    private static string MarkdownToSimpleHtml(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return "<p></p>";
        var s = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        s = Regex.Replace(s, @"```[\s\S]*?```", m =>
            "<pre><code>" + System.Net.WebUtility.HtmlEncode(m.Value.Trim('`').Trim()) + "</code></pre>");
        s = Regex.Replace(s, @"^######\s+(.+)$", "<h6>$1</h6>", RegexOptions.Multiline);
        s = Regex.Replace(s, @"^#####\s+(.+)$", "<h5>$1</h5>", RegexOptions.Multiline);
        s = Regex.Replace(s, @"^####\s+(.+)$", "<h4>$1</h4>", RegexOptions.Multiline);
        s = Regex.Replace(s, @"^###\s+(.+)$", "<h3>$1</h3>", RegexOptions.Multiline);
        s = Regex.Replace(s, @"^##\s+(.+)$", "<h2>$1</h2>", RegexOptions.Multiline);
        s = Regex.Replace(s, @"^#\s+(.+)$", "<h1>$1</h1>", RegexOptions.Multiline);
        s = Regex.Replace(s, @"!\[([^\]]*)\]\(([^)]+)\)", "<img src=\"$2\" alt=\"$1\" />");
        s = Regex.Replace(s, @"\[([^\]]+)\]\(([^)]+)\)", "<a href=\"$2\">$1</a>");
        s = Regex.Replace(s, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        s = Regex.Replace(s, @"__(.+?)__", "<strong>$1</strong>");
        s = Regex.Replace(s, @"(?<!\w)\*(?!\*)(.+?)(?<!\*)\*(?!\w)", "<em>$1</em>");
        s = Regex.Replace(s, @"^>\s?(.+)$", "<blockquote><p>$1</p></blockquote>", RegexOptions.Multiline);
        s = Regex.Replace(s, @"^\s*[-*+]\s+(.+)$", "<li>$1</li>", RegexOptions.Multiline);
        s = Regex.Replace(s, @"(?:<li>.*?</li>\n?)+", m => "<ul>" + m.Value + "</ul>");
        s = Regex.Replace(s, @"^\s*\d+\.\s+(.+)$", "<li>$1</li>", RegexOptions.Multiline);

        var blocks = Regex.Split(s, @"\n{2,}");
        var sb = new StringBuilder();
        foreach (var block in blocks)
        {
            var b = block.Trim();
            if (b.Length == 0) continue;
            if (b.StartsWith('<') && Regex.IsMatch(b, @"^<(h[1-6]|ul|ol|pre|blockquote|p|img)\b"))
            {
                sb.AppendLine(b.Replace("\n", " ", StringComparison.Ordinal));
                continue;
            }
            sb.Append("<p>").Append(b.Replace("\n", "<br />", StringComparison.Ordinal)).AppendLine("</p>");
        }
        return sb.ToString().Trim();
    }

    private async Task<int> UploadWordPressMediaAsync(
        HttpClient http,
        string baseUrl,
        string basicAuth,
        byte[] bytes,
        string fileName,
        string contentType,
        CancellationToken ct)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/wp-json/wp/v2/media");
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
        req.Content = content;

        using var res = await http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(HumanizeWordPressError("media", (int)res.StatusCode, body));

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("id", out var idEl))
            throw new InvalidOperationException("WordPress media response missing id");
        return idEl.GetInt32();
    }

    private static async Task EnsureWordPressAuthAsync(
        HttpClient http,
        string baseUrl,
        string basicAuth,
        string username,
        CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/wp-json/wp/v2/users/me?context=edit");
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
        using var res = await http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (res.IsSuccessStatusCode)
            return;
        throw new InvalidOperationException(HumanizeWordPressError("auth", (int)res.StatusCode, body, username));
    }

    private static string HumanizeWordPressError(string mode, int statusCode, string body, string? username = null)
    {
        var raw = Trim(body, 800);
        string? wpMessage = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var m))
                wpMessage = m.GetString();
        }
        catch
        {
            /* ignore */
        }

        if (statusCode is 401 or 403
            || raw.Contains("rest_cannot_create", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("rest_forbidden", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("rest_not_logged_in", StringComparison.OrdinalIgnoreCase))
        {
            var who = string.IsNullOrWhiteSpace(username) ? "" : $" (username đang dùng: «{username}»).";
            return
                $"WordPress {statusCode}: đăng nhập REST thất bại{who} " +
                "Role Admin vẫn lỗi thường vì: (1) chưa dán Application Password (đừng dùng mật khẩu đăng nhập web), " +
                "(2) username sai — phải đúng «admin», " +
                "(3) hosting/nginx nuốt header Authorization — cần cấu hình pass Authorization tới PHP, " +
                "(4) plugin bảo mật chặn Application Passwords. " +
                $"Chi tiết: {wpMessage ?? raw}";
        }

        return $"WordPress {mode} {statusCode}: {wpMessage ?? raw}";
    }

    private async Task<ConnectorResult> RunFacebookAsync(
        ContentRepository.PublishJobRow job,
        ContentRepository.TopicRow topic,
        IReadOnlyList<ContentRepository.VariantRow> variants,
        ContentRepository.AssetRow? selected,
        CancellationToken ct)
    {
        var channel = job.ChannelTargetId is { } cid
            ? await _repo.GetChannelAsync(cid, ct)
            : null;
        if (channel is null)
            throw new InvalidOperationException("Channel target missing for Facebook job");

        var pageId = channel.ExternalId?.Trim()
                     ?? throw new InvalidOperationException("Facebook external_id (page id) required");
        var token = ResolveTargetSecret(channel.SecretRef, channel.ConfigJson)
                    ?? throw new InvalidOperationException(
                        "Facebook token missing — dán Page Access Token vào form nơi đăng hoặc đặt env");

        var fb = variants.FirstOrDefault(v => v.Kind is "fb_page" or "fb_short")
                 ?? variants.FirstOrDefault(v => v.Kind is "social_caption");
        // Never post raw markdown (web_long) to Facebook — shows ** as draft-looking text.
        var ctaUrl = TopicCta(topic);
        var message = MarkdownToPlainText(
            ContentCtaRouter.RewriteBody(topic.BrandCode, fb?.BodyMarkdown ?? topic.Title, ctaUrl));
        if (!string.IsNullOrWhiteSpace(ctaUrl) && !message.Contains(ctaUrl, StringComparison.OrdinalIgnoreCase))
            message = $"{message.Trim()}\n\n{ctaUrl}";

        var publishAt = _ephemeral?.PublishAt ?? job.PublishAt ?? topic.DisplayAt;
        var isFuture = publishAt is { } at && at > DateTimeOffset.UtcNow.AddMinutes(10);
        var http = _httpFactory.CreateClient("content-publish");

        var mediaOpt = await ResolvePublishImageBytesAsync(selected, ct);
        if (mediaOpt is { } media && media.Bytes.Length > 0)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(message), "caption");
            form.Add(new StringContent(token), "access_token");
            if (isFuture && publishAt is { } when)
            {
                form.Add(new StringContent("false"), "published");
                form.Add(new StringContent(when.ToUnixTimeSeconds().ToString()), "scheduled_publish_time");
            }
            else
            {
                form.Add(new StringContent("true"), "published");
            }

            var fileContent = new ByteArrayContent(media.Bytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(media.ContentType);
            form.Add(fileContent, "source", media.FileName);

            var url = $"https://graph.facebook.com/v21.0/{Uri.EscapeDataString(pageId)}/photos";
            using var res = await http.PostAsync(url, form, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    await FacebookFailAsync(channel.Id, "photo", (int)res.StatusCode, body, ct));

            using var doc = JsonDocument.Parse(body);
            var postId = doc.RootElement.TryGetProperty("post_id", out var pid) ? pid.GetString()
                : doc.RootElement.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            return new ConnectorResult(
                postId,
                ContentRepository.ToJson(new { facebook = "photo", scheduled = isFuture, publishAt, postId, imageKeptOnServer = false }));
        }

        var fields = new Dictionary<string, string>
        {
            ["message"] = message,
            ["access_token"] = token,
        };
        if (isFuture && publishAt is { } when2)
        {
            fields["published"] = "false";
            fields["scheduled_publish_time"] = when2.ToUnixTimeSeconds().ToString();
        }

        var feedUrl = $"https://graph.facebook.com/v21.0/{Uri.EscapeDataString(pageId)}/feed";
        using var formContent = new FormUrlEncodedContent(fields);
        using var feedRes = await http.PostAsync(feedUrl, formContent, ct);
        var feedBody = await feedRes.Content.ReadAsStringAsync(ct);
        if (!feedRes.IsSuccessStatusCode)
            throw new InvalidOperationException(
                await FacebookFailAsync(channel.Id, "feed", (int)feedRes.StatusCode, feedBody, ct));

        using var feedDoc = JsonDocument.Parse(feedBody);
        var feedId = feedDoc.RootElement.TryGetProperty("id", out var fid) ? fid.GetString() : null;
        return new ConnectorResult(
            feedId,
            ContentRepository.ToJson(new { facebook = "feed", scheduled = isFuture, publishAt, feedId }));
    }

    private async Task<(byte[] Bytes, string FileName, string ContentType)?> ResolvePublishImageBytesAsync(
        ContentRepository.AssetRow? selected,
        CancellationToken ct)
    {
        if (_ephemeral is { Bytes.Length: > 0 } e)
            return (e.Bytes, e.FileName, e.ContentType);

        if (selected is null) return null;
        var path = Path.Combine(ResolveAssetRoot(), selected.StoragePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path)) return null;
        var bytes = await File.ReadAllBytesAsync(path, ct);
        return (bytes, selected.FileName, selected.ContentType);
    }

    private async Task<ConnectorResult> RunAstroGitAsync(
        ContentRepository.PublishJobRow job,
        ContentRepository.TopicRow topic,
        IReadOnlyList<ContentRepository.VariantRow> variants,
        ContentRepository.AssetRow? selected,
        CancellationToken ct)
    {
        var site = job.SiteTargetId is { } sid
            ? await _repo.GetSiteAsync(sid, ct)
            : null;
        if (site is null)
            throw new InvalidOperationException("Site target missing for Astro Git job");

        var cfg = ParseConfig(site.ConfigJson);
        var owner = GetConfigString(cfg, "owner");
        var repo = GetConfigString(cfg, "repo");
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
            throw new InvalidOperationException(
                "Astro/Git chưa đủ cấu hình: vào Thương hiệu → Nơi đăng → chọn website Astro → điền GitHub owner + tên repo → Lưu. Rồi bấm Chạy lại.");
        var branch = GetConfigString(cfg, "branch") ?? "main";
        var contentPath = (GetConfigString(cfg, "contentPath") ?? "novixa-site/src/content/tin-tuc").TrimEnd('/');
        var token = ResolveTargetSecret(site.SecretRef, site.ConfigJson)
                    ?? throw new InvalidOperationException(
                        "Thiếu GitHub token — vào Thương hiệu → Nơi đăng Astro → dán token (ghp_…) → Lưu.");

        var web = variants.FirstOrDefault(v => v.Kind == "web_long") ?? variants.FirstOrDefault();
        var slug = Slugify(web?.Title ?? topic.Title);
        var displayDate = (topic.DisplayAt ?? job.PublishAt ?? DateTimeOffset.UtcNow).ToString("yyyy-MM-dd");
        var description = MarkdownToPlainText(
            variants.FirstOrDefault(v => v.Kind == "seo_meta")?.BodyMarkdown ?? topic.Title,
            singleLine: true);
        var title = web?.Title ?? topic.Title;
        var bodyMd = ContentCtaRouter.RewriteBody(topic.BrandCode, web?.BodyMarkdown ?? topic.Title, TopicCta(topic));
        var ctaUrl = TopicCta(topic);
        var cta = !string.IsNullOrWhiteSpace(ctaUrl) && !bodyMd.Contains(ctaUrl, StringComparison.OrdinalIgnoreCase)
            ? $"\n\n[Tìm hiểu thêm]({ctaUrl})"
            : "";

        var pathLower = contentPath.Replace('\\', '/').ToLowerInvariant();
        var isPharmacyKienThuc = pathLower.Contains("pharmacy-storefront", StringComparison.Ordinal)
            || pathLower.Contains("kien-thuc", StringComparison.Ordinal)
            || string.Equals(GetConfigString(cfg, "contentFormat"), "pharmacy", StringComparison.OrdinalIgnoreCase);
        var isNovixaTinTuc = !isPharmacyKienThuc && pathLower.Contains("tin-tuc", StringComparison.Ordinal);
        var isFamixaBlog = pathLower.Contains("famixa-site", StringComparison.Ordinal)
            || string.Equals(GetConfigString(cfg, "contentFormat"), "famixa", StringComparison.OrdinalIgnoreCase);
        var isKittechInsights = pathLower.Contains("insights", StringComparison.Ordinal)
            || string.Equals(GetConfigString(cfg, "contentFormat"), "insights", StringComparison.OrdinalIgnoreCase);

        string? imagePublicPath = GetConfigString(cfg, "defaultImage");
        string? imageRepoPath = null;
        byte[]? imageBytes = null;
        var mediaOpt = await ResolvePublishImageBytesAsync(selected, ct);
        if (mediaOpt is { } media && media.Bytes.Length > 0)
        {
            var ext = GuessImageExt(media.FileName, media.ContentType);
            if (isPharmacyKienThuc)
            {
                var imageDir = GetConfigString(cfg, "imagePath")?.TrimEnd('/')
                               ?? "client/pharmacy-storefront/public/images/kien-thuc";
                imageRepoPath = $"{imageDir}/{slug}.{ext}";
                imagePublicPath = $"/images/kien-thuc/{slug}.{ext}";
                imageBytes = media.Bytes;
            }
            else if (isNovixaTinTuc)
            {
                var imageDir = GetConfigString(cfg, "imagePath")?.TrimEnd('/')
                               ?? "novixa-site/public/images/tin-tuc";
                imageRepoPath = $"{imageDir}/{slug}.{ext}";
                imagePublicPath = $"/images/tin-tuc/{slug}.{ext}";
                imageBytes = media.Bytes;
            }
            else if (isFamixaBlog)
            {
                var imageDir = GetConfigString(cfg, "imagePath")?.TrimEnd('/')
                               ?? "famixa-site/public/images/blog";
                imageRepoPath = $"{imageDir}/{slug}.{ext}";
                imagePublicPath = $"/images/blog/{slug}.{ext}";
                imageBytes = media.Bytes;
            }
            else if (isKittechInsights)
            {
                // kittech.vn — public/images/insights/{slug}.ext → heroImage
                var imageDir = GetConfigString(cfg, "imagePath")?.TrimEnd('/')
                               ?? "public/images/insights";
                imageRepoPath = $"{imageDir}/{slug}.{ext}";
                imagePublicPath = $"/images/insights/{slug}.{ext}";
                imageBytes = media.Bytes;
            }
        }

        string filePath;
        string md;
        string commitPrefix;
        string? publishedCategory = null;
        string? publishedSection = null;

        if (isPharmacyKienThuc)
        {
            // xuanhoa.novixa.vn — Astro collection kien-thuc
            filePath = $"{contentPath}/{slug}.md";
            commitPrefix = "content(xuanhoa)";
            var category = GetConfigString(cfg, "newsCategory")
                           ?? GetConfigString(cfg, "category")
                           ?? "Kiến thức sức khỏe";
            publishedCategory = category.Trim();
            if (string.IsNullOrWhiteSpace(imagePublicPath))
                imagePublicPath = GetConfigString(cfg, "defaultImage") ?? "/brand/articles/default.png";
            var fm = new StringBuilder();
            fm.AppendLine("---");
            fm.Append("title: \"").Append(EscapeYaml(title)).AppendLine("\"");
            fm.Append("description: \"").Append(EscapeYaml(Trim(description, 280))).AppendLine("\"");
            fm.Append("pubDate: ").AppendLine(displayDate);
            fm.Append("category: \"").Append(EscapeYaml(category.Trim())).AppendLine("\"");
            if (!string.IsNullOrWhiteSpace(imagePublicPath))
                fm.Append("image: ").AppendLine(imagePublicPath.Trim());
            fm.AppendLine("---");
            fm.AppendLine();
            fm.Append(bodyMd);
            fm.Append(cta);
            md = fm.ToString();
        }
        else if (isNovixaTinTuc)
        {
            // novixa.vn — Astro collection tinTuc (novixa-site/src/content.config.ts)
            filePath = $"{contentPath}/{slug}.md";
            commitPrefix = "content(novixa)";
            var category = GetConfigString(cfg, "newsCategory")
                           ?? GetConfigString(cfg, "category")
                           ?? "van-hanh";
            var subcategory = GetConfigString(cfg, "newsSubcategory")
                              ?? GetConfigString(cfg, "subcategory");
            publishedCategory = category.Trim();
            var fm = new StringBuilder();
            fm.AppendLine("---");
            fm.Append("title: \"").Append(EscapeYaml(title)).AppendLine("\"");
            fm.Append("description: \"").Append(EscapeYaml(Trim(description, 280))).AppendLine("\"");
            fm.Append("category: ").AppendLine(category.Trim());
            if (!string.IsNullOrWhiteSpace(subcategory))
                fm.Append("subcategory: ").AppendLine(subcategory.Trim());
            if (!string.IsNullOrWhiteSpace(imagePublicPath))
                fm.Append("image: ").AppendLine(imagePublicPath.Trim());
            fm.Append("pubDate: ").AppendLine(displayDate);
            fm.AppendLine("lang: vi");
            fm.AppendLine("---");
            fm.AppendLine();
            fm.Append(bodyMd);
            fm.Append(cta);
            md = fm.ToString();
        }
        else if (isFamixaBlog)
        {
            // famixa.vn — Next.js MD in famixa-site/content/blog/{slug}.md
            // Live URL: /vi/goi-cha-me/{slug}
            filePath = $"{contentPath}/{slug}.md";
            commitPrefix = "content(famixa)";
            var configuredCategory = GetConfigString(cfg, "blogCategory")
                                     ?? GetConfigString(cfg, "category")
                                     ?? GetConfigString(cfg, "insightCategory");
            var autoCategory = !string.Equals(
                GetConfigString(cfg, "autoCategory"), "false", StringComparison.OrdinalIgnoreCase);
            var category = autoCategory
                ? InferFamixaCategory(title, topic.Pillar, bodyMd, configuredCategory)
                : (NormalizeFamixaCategory(configuredCategory) ?? "nuoi-day");
            publishedCategory = category;
            if (string.IsNullOrWhiteSpace(imagePublicPath))
                imagePublicPath = GetConfigString(cfg, "defaultImage") ?? "/images/blog/default.png";
            var fm = new StringBuilder();
            fm.AppendLine("---");
            fm.Append("title: ").AppendLine(YamlScalar(title));
            fm.Append("description: ").AppendLine(YamlScalar(Trim(description, 280)));
            fm.Append("category: ").AppendLine(category);
            fm.Append("image: ").AppendLine(imagePublicPath.Trim());
            fm.Append("pubDate: ").AppendLine(displayDate);
            fm.AppendLine("draft: false");
            fm.AppendLine("lang: vi");
            fm.AppendLine("---");
            fm.AppendLine();
            fm.Append(bodyMd);
            fm.Append(cta);
            md = fm.ToString();
        }
        else if (isKittechInsights)
        {
            // kittech.vn — Kit-Technology: src/content/insights/{locale}/{category}/{slug}.md
            // Live URL (vi): /vi/blog/{category}/{slug} · (en): /en/insights/{category}/{slug}
            var locale = (GetConfigString(cfg, "locale") ?? "vi").Trim().ToLowerInvariant();
            if (locale is not ("vi" or "en"))
                locale = "vi";
            var configuredCategory = GetConfigString(cfg, "insightCategory")
                                     ?? GetConfigString(cfg, "category");
            var autoCategory = !string.Equals(
                GetConfigString(cfg, "autoCategory"), "false", StringComparison.OrdinalIgnoreCase);
            var category = autoCategory
                ? InferKittechCategory(title, topic.Pillar, topic.BrandCode, topic.BrandName, bodyMd, configuredCategory)
                : (configuredCategory?.Trim().Length > 0 ? configuredCategory.Trim() : "technology");
            var section = InferKittechSection(category, GetConfigString(cfg, "insightSection") ?? GetConfigString(cfg, "section"));
            publishedCategory = category;
            publishedSection = section;
            filePath = $"{contentPath}/{locale}/{category}/{slug}.md";
            commitPrefix = "content(kittech)";
            var fm = new StringBuilder();
            fm.AppendLine("---");
            fm.Append("title: \"").Append(EscapeYaml(title)).AppendLine("\"");
            fm.Append("description: \"").Append(EscapeYaml(Trim(description, 150))).AppendLine("\"");
            fm.Append("locale: ").AppendLine(locale);
            fm.Append("category: ").AppendLine(category);
            fm.Append("section: ").AppendLine(section);
            fm.Append("publishDate: ").AppendLine(displayDate);
            fm.AppendLine("draft: false");
            fm.Append("translationId: \"").Append(EscapeYaml(slug)).AppendLine("\"");
            fm.AppendLine("tags: []");
            if (!string.IsNullOrWhiteSpace(imagePublicPath))
                fm.Append("heroImage: \"").Append(EscapeYaml(imagePublicPath.Trim())).AppendLine("\"");
            fm.AppendLine("---");
            fm.AppendLine();
            fm.Append(bodyMd);
            fm.Append(cta);
            md = fm.ToString();
        }
        else
        {
            filePath = $"{contentPath}/{displayDate}-{slug}.md";
            commitPrefix = "content(park)";
            md = $"""
                ---
                title: "{EscapeYaml(title)}"
                description: "{EscapeYaml(Trim(description, 150))}"
                pubDate: {displayDate}
                ---

                {bodyMd}{cta}
                """;
        }

        var http = _httpFactory.CreateClient("content-publish");

        // Preflight: GitHub returns 404 both for missing repo AND for fine-grained tokens
        // that do not include the target repository — surface a clearer message.
        await EnsureGitHubRepoAccessibleAsync(http, owner!, repo!, token!, ct);

        string? imageSha = null;
        if (imageBytes is { Length: > 0 } && !string.IsNullOrWhiteSpace(imageRepoPath))
        {
            var imgPut = await PutGitHubFileAsync(
                http, owner!, repo!, branch, token!, imageRepoPath!, imageBytes,
                $"{commitPrefix}: image {slug}", ct);
            imageSha = imgPut.Sha;
        }

        var mdPut = await PutGitHubFileAsync(
            http, owner!, repo!, branch, token!, filePath, Encoding.UTF8.GetBytes(md),
            $"{commitPrefix}: {(imageSha is null ? "add" : "publish")} {slug}", ct);

        return new ConnectorResult(
            mdPut.Sha ?? filePath,
            ContentRepository.ToJson(new
            {
                astro = "git",
                path = filePath,
                imagePath = imageRepoPath,
                image = imagePublicPath,
                hasImage = imageBytes is { Length: > 0 },
                sha = mdPut.Sha,
                owner,
                repo,
                branch,
                category = publishedCategory,
                section = publishedSection,
            }));
    }

    private static async Task EnsureGitHubRepoAccessibleAsync(
        HttpClient http,
        string owner,
        string repo,
        string token,
        CancellationToken ct)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.UserAgent.ParseAdd("KitPlatform-ContentPark");
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var res = await http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (res.IsSuccessStatusCode)
            return;

        if ((int)res.StatusCode is 401 or 403)
        {
            throw new InvalidOperationException(
                $"GitHub {(int)res.StatusCode}: token không hợp lệ hoặc thiếu quyền ghi cho {owner}/{repo}. " +
                "Dùng PAT classic quyền repo, hoặc fine-grained token chọn đúng repo + Contents: Read and write.");
        }

        if ((int)res.StatusCode == 404)
        {
            throw new InvalidOperationException(
                $"GitHub 404: không truy cập được repo {owner}/{repo}. " +
                "Kiểm tra: (1) Owner = khiemtic-rgb, Repo = Kit-Technology (đúng dấu gạch); " +
                "(2) Fine-grained token phải chọn đúng repo Kit-Technology (không chọn Account permissions); " +
                "(3) Thư mục bài = src/content/insights. " +
                $"Chi tiết API: {Trim(body)}");
        }

        throw new InvalidOperationException($"GitHub {(int)res.StatusCode} khi mở {owner}/{repo}: {Trim(body)}");
    }

    private sealed record GitHubPutResult(string? Sha, string RawBody);

    private async Task<GitHubPutResult> PutGitHubFileAsync(
        HttpClient http,
        string owner,
        string repo,
        string branch,
        string token,
        string filePath,
        byte[] bytes,
        string commitMessage,
        CancellationToken ct)
    {
        var apiUrl =
            $"https://api.github.com/repos/{owner}/{repo}/contents/{Uri.EscapeDataString(filePath).Replace("%2F", "/", StringComparison.Ordinal)}";

        async Task<GitHubPutResult> PutAsync(string? sha)
        {
            using var putReq = new HttpRequestMessage(HttpMethod.Put, apiUrl);
            putReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            putReq.Headers.UserAgent.ParseAdd("KitPlatform-ContentPark");
            putReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            var payload = sha is null
                ? (object)new
                {
                    message = commitMessage,
                    content = Convert.ToBase64String(bytes),
                    branch,
                }
                : new
                {
                    message = commitMessage,
                    content = Convert.ToBase64String(bytes),
                    branch,
                    sha,
                };
            putReq.Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOpts),
                Encoding.UTF8,
                "application/json");
            using var putRes = await http.SendAsync(putReq, ct);
            var putBody = await putRes.Content.ReadAsStringAsync(ct);
            if (!putRes.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"GitHub {(int)putRes.StatusCode} khi ghi {owner}/{repo}/{filePath} (branch {branch}): {Trim(putBody)}");
            using var putDoc = JsonDocument.Parse(putBody);
            var outSha = putDoc.RootElement.TryGetProperty("content", out var c)
                         && c.TryGetProperty("sha", out var shaEl)
                ? shaEl.GetString()
                : sha;
            return new GitHubPutResult(outSha, putBody);
        }

        try
        {
            return await PutAsync(null);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("422", StringComparison.Ordinal)
                                                   || ex.Message.Contains("sha", StringComparison.OrdinalIgnoreCase)
                                                   || ex.Message.Contains("\"code\":\"sha_missing\"", StringComparison.Ordinal)
                                                   || ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            using var getReq = new HttpRequestMessage(HttpMethod.Get, apiUrl + $"?ref={Uri.EscapeDataString(branch)}");
            getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            getReq.Headers.UserAgent.ParseAdd("KitPlatform-ContentPark");
            getReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using var getRes = await http.SendAsync(getReq, ct);
            var getBody = await getRes.Content.ReadAsStringAsync(ct);
            if (!getRes.IsSuccessStatusCode)
                throw new InvalidOperationException($"GitHub get {(int)getRes.StatusCode} {owner}/{repo}/{filePath}: {Trim(getBody)}");
            using var existing = JsonDocument.Parse(getBody);
            var existingSha = existing.RootElement.TryGetProperty("sha", out var shaNode)
                ? shaNode.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(existingSha))
                throw;
            return await PutAsync(existingSha);
        }
    }

    private static string GuessImageExt(string fileName, string contentType)
    {
        var ext = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        if (ext is "png" or "jpg" or "jpeg" or "webp" or "gif")
            return ext == "jpeg" ? "jpg" : ext;
        if (contentType.Contains("png", StringComparison.OrdinalIgnoreCase)) return "png";
        if (contentType.Contains("webp", StringComparison.OrdinalIgnoreCase)) return "webp";
        if (contentType.Contains("gif", StringComparison.OrdinalIgnoreCase)) return "gif";
        return "jpg";
    }

    private async Task<ContentPublishJobDto> LoadJobAsync(Guid id, CancellationToken ct)
    {
        var row = await _repo.GetPublishJobAsync(id, ct)
                  ?? throw new InvalidOperationException("Job missing");
        return ContentTopicService.MapJob(row);
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

    private string? ResolveTargetSecret(string? secretRef, string? configJson)
    {
        var stored = ContentTargetSecrets.ExtractStored(configJson);
        if (!string.IsNullOrWhiteSpace(stored))
            return stored;
        return ResolveSecret(secretRef);
    }

    private string? ResolveSecret(string? secretRef)
    {
        if (string.IsNullOrWhiteSpace(secretRef)) return null;
        var key = secretRef.Trim();
        return _configuration[key]
               ?? Environment.GetEnvironmentVariable(key)
               ?? _configuration[$"Content:Secrets:{key}"];
    }

    private static Dictionary<string, JsonElement> ParseConfig(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOpts)
                   ?? new Dictionary<string, JsonElement>();
        }
        catch
        {
            return new Dictionary<string, JsonElement>();
        }
    }

    private static string? GetConfigString(Dictionary<string, JsonElement> cfg, string key)
    {
        if (!cfg.TryGetValue(key, out var el)) return null;
        return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
    }

    /// <summary>Strip Markdown for Facebook captions (keep line breaks). Sites keep raw MD in body.</summary>
    private static string MarkdownToPlainText(string markdown, bool singleLine = false)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return "";
        var s = markdown.Replace("\r\n", "\n", StringComparison.Ordinal);

        s = Regex.Replace(s, @"```[\s\S]*?```", "");
        s = Regex.Replace(s, @"!\[([^\]]*)\]\([^)]+\)", "$1");
        s = Regex.Replace(s, @"\[([^\]]+)\]\(([^)]+)\)", "$1");
        s = Regex.Replace(s, @"\*\*\*(.+?)\*\*\*", "$1");
        s = Regex.Replace(s, @"\*\*(.+?)\*\*", "$1");
        s = Regex.Replace(s, @"__(.+?)__", "$1");
        s = Regex.Replace(s, @"(?<!\w)\*(?!\*)(.+?)(?<!\*)\*(?!\w)", "$1");
        s = Regex.Replace(s, @"(?<!\w)_(?!_)(.+?)(?<!_)_(?!\w)", "$1");
        s = Regex.Replace(s, @"^#{1,6}\s*", "", RegexOptions.Multiline);
        s = Regex.Replace(s, @"^>\s?", "", RegexOptions.Multiline);
        s = Regex.Replace(s, @"^\s*[-*+]\s+", "• ", RegexOptions.Multiline);
        s = Regex.Replace(s, @"^\s*\d+\.\s+", "", RegexOptions.Multiline);
        s = Regex.Replace(s, @"^---+\s*$", "", RegexOptions.Multiline);
        s = s.Replace("`", "", StringComparison.Ordinal);
        s = s.Replace("**", "", StringComparison.Ordinal);
        s = Regex.Replace(s, @"\n{3,}", "\n\n");
        if (singleLine)
            s = Regex.Replace(s, @"\s+", " ");
        return s.Trim();
    }

    private async Task<string> FacebookFailAsync(
        Guid channelId,
        string mode,
        int statusCode,
        string body,
        CancellationToken ct)
    {
        var message = HumanizeFacebookError(mode, statusCode, body);
        if (IsFacebookAuthError(body))
        {
            try
            {
                await _facebook.MarkNeedReconnectAsync(channelId, message, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không ghi được trạng thái NEED_RECONNECT cho kênh {ChannelId}", channelId);
            }
        }
        return message;
    }

    private static bool IsFacebookAuthError(string body)
    {
        var raw = body ?? "";
        return raw.Contains("expired", StringComparison.OrdinalIgnoreCase)
               || raw.Contains("\"code\":190", StringComparison.Ordinal)
               || raw.Contains("error_subcode\":463", StringComparison.Ordinal)
               || raw.Contains("Error validating access token", StringComparison.OrdinalIgnoreCase)
               || raw.Contains("(#200)", StringComparison.Ordinal)
               || raw.Contains("OAuthException", StringComparison.OrdinalIgnoreCase);
    }

    private static string HumanizeFacebookError(string mode, int statusCode, string body)
    {
        var raw = Trim(body, 800);
        if (raw.Contains("expired", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("\"code\":190", StringComparison.Ordinal)
            || raw.Contains("error_subcode\":463", StringComparison.Ordinal)
            || raw.Contains("Error validating access token", StringComparison.OrdinalIgnoreCase))
        {
            return "Facebook: mất quyền đăng. Bấm Kết nối lại trên hàng này, rồi Đăng lại + ảnh.";
        }

        if (raw.Contains("permissions", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("(#200)", StringComparison.Ordinal))
        {
            return "Facebook: thiếu quyền pages_manage_posts. Bấm Kết nối lại trên hàng này và chọn đúng Page.";
        }

        return $"Facebook {mode} {statusCode}: {raw}";
    }

    private static readonly HashSet<string> FamixaCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "nuoi-day", "routine", "man-hinh", "tu-giac", "famixa",
    };

    /// <summary>famixa.vn blog categories (famixa-site/lib/blog/categories.ts).</summary>
    private static string InferFamixaCategory(string title, string? pillar, string? body, string? configuredFallback)
    {
        var pillarSlug = NormalizeFamixaCategory(pillar);
        if (pillarSlug is not null)
            return pillarSlug;

        var hay = $"{title}\n{pillar}\n{Trim(body ?? "", 400)}".ToLowerInvariant();
        if (ContainsAny(hay, "màn hình", "man hinh", "screen time", "điện thoại", "dien thoai", "thiết bị", "thiet bi", "youtube", "tiktok"))
            return "man-hinh";
        if (ContainsAny(hay, "tự giác", "tu giac", "bớt nhắc", "bot nhac", "không cần nhắc", "trưởng thành", "truong thanh", "hợp tác", "hop tac"))
            return "tu-giac";
        if (ContainsAny(hay, "routine", "nhịp", "nhip", "giờ ngủ", "gio ngu", "thói quen", "thoi quen", "lịch", "lich sinh hoạt", "việc nhà", "viec nha"))
            return "routine";
        if (ContainsAny(hay, "famixa", "app famixa", "dùng famixa", "dung famixa", "gói peace", "goi peace", "ai coach"))
            return "famixa";
        if (ContainsAny(hay, "nuôi dạy", "nuoi day", "xin lỗi", "xin loi", "kỷ luật", "ky luat", "khen", "phạt", "phat", "cha mẹ", "cha me"))
            return "nuoi-day";

        return NormalizeFamixaCategory(configuredFallback) ?? "nuoi-day";
    }

    private static string? NormalizeFamixaCategory(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim().ToLowerInvariant().Replace(' ', '-').Replace('_', '-');
        s = s switch
        {
            "nuoiday" or "parenting" or "nuoi-day-con" => "nuoi-day",
            "screen" or "screen-time" or "manhinh" => "man-hinh",
            "tugiac" or "autonomy" => "tu-giac",
            "nhịp" or "nhip" or "habits" => "routine",
            "app" or "product" => "famixa",
            _ => s,
        };
        return FamixaCategories.Contains(s) ? s : null;
    }

    private static string YamlScalar(string s)
    {
        if (string.IsNullOrEmpty(s)) return "''";
        return "\"" + EscapeYaml(s) + "\"";
    }

    private static readonly HashSet<string> KittechCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "ai", "healthcare", "digital-transformation", "engineering", "company-news",
        "business", "technology", "solutions", "products", "faq",
    };

    /// <summary>
    /// Map topic title / pillar / brand → kittech.vn insight category folder.
    /// Configured category wins only when auto is off, or as weak fallback when nothing matches.
    /// </summary>
    private static string InferKittechCategory(
        string title,
        string? pillar,
        string? brandCode,
        string? brandName,
        string? body,
        string? configuredFallback)
    {
        // Exact pillar slug (e.g. user typed "healthcare" / "ai" in Chủ đề)
        var pillarSlug = NormalizeCategorySlug(pillar);
        if (pillarSlug is not null && KittechCategories.Contains(pillarSlug))
            return pillarSlug.ToLowerInvariant();

        var hay = $"{title}\n{pillar}\n{brandCode}\n{brandName}\n{Trim(body ?? "", 400)}".ToLowerInvariant();

        // Brand-first signals
        if (ContainsAny(hay, "novixa", "nhà thuốc", "nha thuoc", "dược", "duoc", "pharmacy", "gpp", "pos nhà thuốc"))
            return "healthcare";
        if (ContainsAny(hay, "famixa", "gia đình", "gia dinh", "family", "cha mẹ", "cha me", "con cái"))
            return "products";

        // Topic keywords (ordered by specificity)
        if (ContainsAny(hay, "faq", "hỏi đáp", "hoi dap", "câu hỏi thường", "cau hoi thuong"))
            return "faq";
        if (ContainsAny(hay, "ai agent", "trí tuệ nhân tạo", "tri tue nhan tao", "machine learning", "llm",
                "chatgpt", "generative ai", " ai ", "ai-", "-ai ", "ai trong", "ứng dụng ai", "ung dung ai"))
            return "ai";
        if (ContainsAny(hay, "chuyển đổi số", "chuyen doi so", "digital transformation", "số hóa", "so hoa"))
            return "digital-transformation";
        if (ContainsAny(hay, "devops", "docker", "kubernetes", "api-first", "cloud-native", "flutter", "postgresql",
                "kiến trúc", "kien truc", "engineering", "kỹ thuật", "ky thuat", "microservices"))
            return "engineering";
        if (ContainsAny(hay, "company", "tin công ty", "tin cong ty", "tuyển dụng", "tuyen dung", "văn hóa", "van hoa kit"))
            return "company-news";
        if (ContainsAny(hay, "doanh nghiệp", "doanh nghiep", "kinh doanh", "crm", "loyalty", "omnichannel",
                "bán hàng", "ban hang", "roi", "growth"))
            return "business";
        if (ContainsAny(hay, "sản phẩm", "san pham", "product", "tính năng", "tinh nang", "giải pháp", "giai phap",
                "platform", "nền tảng", "nen tang"))
            return ContainsAny(hay, "giải pháp", "giai phap", "solution") ? "solutions" : "products";
        if (ContainsAny(hay, "công nghệ", "cong nghe", "technology", "mobile-first", "pwa", "saas", "bảo mật", "bao mat"))
            return "technology";
        if (ContainsAny(hay, "y tế", "y te", "healthcare", "sức khỏe", "suc khoe", "bệnh", "benh"))
            return "healthcare";

        var fallback = NormalizeCategorySlug(configuredFallback);
        if (fallback is not null && KittechCategories.Contains(fallback))
            return fallback.ToLowerInvariant();
        return "technology";
    }

    private static string InferKittechSection(string category, string? configured)
    {
        var cfg = NormalizeCategorySlug(configured);
        if (cfg is "insights" or "technology" or "solutions" or "products" or "company" or "faq")
            return cfg;

        return category.ToLowerInvariant() switch
        {
            "technology" or "engineering" or "ai" or "digital-transformation" => "technology",
            "solutions" => "solutions",
            "products" => "products",
            "company-news" => "company",
            "faq" => "faq",
            _ => "insights",
        };
    }

    private static string? NormalizeCategorySlug(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim().ToLowerInvariant()
            .Replace(' ', '-')
            .Replace('_', '-');
        // common aliases
        s = s switch
        {
            "health" or "y-te" or "yte" or "nha-thuoc" or "pharmacy" => "healthcare",
            "tech" or "cong-nghe" => "technology",
            "dx" or "chuyen-doi-so" => "digital-transformation",
            "company" or "tin-tuc" or "news" => "company-news",
            "product" => "products",
            "solution" => "solutions",
            "eng" => "engineering",
            _ => s,
        };
        return s;
    }

    private static bool ContainsAny(string hay, params string[] needles)
    {
        foreach (var n in needles)
        {
            if (hay.Contains(n, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool IsAutoSiteConnector(string? type) =>
        type is "astro_git" or "wordpress_rest" or "local_os";

    private static bool IsAutoChannelConnector(string? type) =>
        type == "facebook_page";

    private static string? TopicCta(ContentRepository.TopicRow topic) =>
        ContentCtaRouter.Resolve(
            topic.BrandCode, topic.Title, topic.Pillar, topic.Goal, topic.BodyOutline, topic.CtaUrl);

    private static string Trim(string s, int max = 500) =>
        s.Length <= max ? s : s[..max];

    private static string EscapeYaml(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);

    private static string Slugify(string input)
    {
        var ascii = RemoveDiacritics(input.Trim().ToLowerInvariant());
        var sb = new StringBuilder();
        foreach (var ch in ascii)
        {
            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9') sb.Append(ch);
            else if (ch is ' ' or '-' or '_' or '.') sb.Append('-');
        }
        var slug = sb.ToString().Trim('-');
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(slug) ? Guid.NewGuid().ToString("N")[..8] : slug[..Math.Min(80, slug.Length)];
    }

    private static string RemoveDiacritics(string text)
    {
        var formD = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        }
        return sb.ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace('đ', 'd')
            .Replace('Đ', 'd');
    }

    private sealed record ConnectorResult(string? ExternalRef, string ResultJson);
}
