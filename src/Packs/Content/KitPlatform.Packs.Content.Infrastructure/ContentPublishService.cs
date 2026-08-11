using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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
    private readonly ILogger<ContentPublishService> _logger;

    // Per-request media for scoped publish (cleared in finally).
    private EphemeralMedia? _ephemeral;

    public ContentPublishService(
        ContentRepository repo,
        IHttpClientFactory httpFactory,
        IConfiguration configuration,
        IHostEnvironment env,
        IOptions<ContentOptions> options,
        ILogger<ContentPublishService> logger)
    {
        _repo = repo;
        _httpFactory = httpFactory;
        _configuration = configuration;
        _env = env;
        _options = options.Value;
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
                var connector = ch.ChannelType is "facebook_page" or "instagram" or "linkedin"
                    ? ch.ChannelType
                    : "manual";
                if (ch.ChannelType == "facebook_page")
                    connector = "facebook_page";

                var id = await _repo.InsertPublishJobAsync(new ContentRepository.PublishJobRow
                {
                    TopicId = topicId,
                    BrandId = topic.BrandId,
                    TargetKind = "channel",
                    ChannelTargetId = ch.Id,
                    ConnectorType = connector,
                    Status = "Queued",
                    PublishAt = publishAt,
                }, cancellationToken);
                jobs.Add(await LoadJobAsync(id, cancellationToken));
            }

            if (request.RunImmediately)
            {
                var ran = new List<ContentPublishJobDto>();
                foreach (var job in jobs)
                {
                    var updated = await RunJobAsync(job.Id, cancellationToken);
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

    public async Task<ContentPublishJobDto?> RunJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _repo.GetPublishJobAsync(jobId, cancellationToken);
        if (job is null) return null;

        job.Status = "Running";
        job.LastError = null;
        await _repo.UpdatePublishJobAsync(job, cancellationToken);
        await _repo.InsertPublishLogAsync(jobId, "info", "Job started", "{}", cancellationToken);

        try
        {
            var topic = await _repo.GetTopicAsync(job.TopicId, cancellationToken)
                        ?? throw new InvalidOperationException("Topic missing");
            var variants = await _repo.ListVariantsAsync(job.TopicId, cancellationToken);
            var assets = await _repo.ListAssetsAsync(job.TopicId, cancellationToken);
            var selected = assets.FirstOrDefault(a => a.IsSelected) ?? assets.FirstOrDefault();

            var result = job.ConnectorType switch
            {
                "manual" => await RunManualAsync(topic, variants, selected, cancellationToken),
                "wordpress_rest" => await RunWordPressAsync(job, topic, variants, selected, cancellationToken),
                "facebook_page" => await RunFacebookAsync(job, topic, variants, selected, cancellationToken),
                "astro_git" => await RunAstroGitAsync(job, topic, variants, selected, cancellationToken),
                _ => throw new InvalidOperationException($"Unsupported connector '{job.ConnectorType}'"),
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
                topic.CtaUrl,
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
        var defaultStatus = GetConfigString(cfg, "status") ?? "draft";

        var web = variants.FirstOrDefault(v => v.Kind is "web_long" or "seo_meta")
                  ?? variants.FirstOrDefault();
        var title = web?.Title ?? topic.Title;
        var content = web?.BodyMarkdown ?? topic.Title;
        if (!string.IsNullOrWhiteSpace(topic.CtaUrl))
            content += $"\n\n[CTA]({topic.CtaUrl})";

        var http = _httpFactory.CreateClient("content-publish");
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));

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

        var payload = new Dictionary<string, object?>
        {
            ["title"] = title,
            ["content"] = content,
            ["status"] = status,
            ["format"] = "standard",
        };
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
            throw new InvalidOperationException($"WordPress {(int)res.StatusCode}: {Trim(body)}");

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
                imageKeptOnServer = false,
            }));
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
            throw new InvalidOperationException($"WordPress media {(int)res.StatusCode}: {Trim(body)}");

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("id", out var idEl))
            throw new InvalidOperationException("WordPress media response missing id");
        return idEl.GetInt32();
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
                 ?? variants.FirstOrDefault();
        var message = fb?.BodyMarkdown ?? topic.Title;
        if (!string.IsNullOrWhiteSpace(topic.CtaUrl))
            message = $"{message.Trim()}\n\n{topic.CtaUrl}";

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
                throw new InvalidOperationException($"Facebook photo {(int)res.StatusCode}: {Trim(body)}");

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
            throw new InvalidOperationException($"Facebook {(int)feedRes.StatusCode}: {Trim(feedBody)}");

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
        var owner = GetConfigString(cfg, "owner")
                    ?? throw new InvalidOperationException("astro_git config.owner required");
        var repo = GetConfigString(cfg, "repo")
                   ?? throw new InvalidOperationException("astro_git config.repo required");
        var branch = GetConfigString(cfg, "branch") ?? "main";
        var contentPath = (GetConfigString(cfg, "contentPath") ?? "src/content/blog").TrimEnd('/');
        var token = ResolveTargetSecret(site.SecretRef, site.ConfigJson)
                    ?? throw new InvalidOperationException(
                        "GitHub token missing — dán token vào form nơi đăng hoặc đặt env");

        var web = variants.FirstOrDefault(v => v.Kind == "web_long") ?? variants.FirstOrDefault();
        var slug = Slugify(web?.Title ?? topic.Title);
        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var filePath = $"{contentPath}/{date}-{slug}.md";
        var description = variants.FirstOrDefault(v => v.Kind == "seo_meta")?.BodyMarkdown
                          ?? topic.Title;
        var md = $"""
            ---
            title: "{EscapeYaml(web?.Title ?? topic.Title)}"
            description: "{EscapeYaml(Trim(description, 150))}"
            pubDate: {date}
            ---

            {web?.BodyMarkdown ?? topic.Title}

            {(string.IsNullOrWhiteSpace(topic.CtaUrl) ? "" : $"[Tìm hiểu thêm]({topic.CtaUrl})")}
            """;

        var http = _httpFactory.CreateClient("content-publish");
        var apiUrl =
            $"https://api.github.com/repos/{owner}/{repo}/contents/{filePath}";
        using var req = new HttpRequestMessage(HttpMethod.Put, apiUrl);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.UserAgent.ParseAdd("KitPlatform-ContentPark");
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        req.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                message = $"content(park): add {slug}",
                content = Convert.ToBase64String(Encoding.UTF8.GetBytes(md)),
                branch,
            }, JsonOpts),
            Encoding.UTF8,
            "application/json");

        using var res = await http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"GitHub {(int)res.StatusCode}: {Trim(body)}");

        _ = selected; // image commit can be Wave 1.1
        using var doc = JsonDocument.Parse(body);
        var sha = doc.RootElement.TryGetProperty("content", out var c)
                  && c.TryGetProperty("sha", out var shaEl)
            ? shaEl.GetString()
            : null;
        return new ConnectorResult(sha ?? filePath, body);
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

    private static string Trim(string s, int max = 500) =>
        s.Length <= max ? s : s[..max];

    private static string EscapeYaml(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string Slugify(string input)
    {
        var sb = new StringBuilder();
        foreach (var ch in input.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else if (ch is ' ' or '-' or '_') sb.Append('-');
        }
        var slug = sb.ToString().Trim('-');
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(slug) ? Guid.NewGuid().ToString("N")[..8] : slug[..Math.Min(80, slug.Length)];
    }

    private sealed record ConnectorResult(string? ExternalRef, string ResultJson);
}
