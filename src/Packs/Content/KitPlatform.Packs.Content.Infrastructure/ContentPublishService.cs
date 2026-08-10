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

    private readonly ContentRepository _repo;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _env;
    private readonly ContentOptions _options;
    private readonly ILogger<ContentPublishService> _logger;

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
        if (topic.Status is not ("Review" or "Approved" or "Scheduled" or "Published"))
            throw new InvalidOperationException(
                $"Topic status '{topic.Status}' cannot publish — cần Review/Approved trước.");

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
            await _repo.UpdateTopicStatusAsync(topicId, "Published", cancellationToken);

        return new PublishContentResultDto(jobs);
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
                "facebook_page" => await RunFacebookAsync(job, topic, variants, cancellationToken),
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
        var password = ResolveSecret(site.SecretRef)
                       ?? GetConfigString(cfg, "applicationPassword")
                       ?? throw new InvalidOperationException(
                           "WordPress secret_ref / applicationPassword missing");
        var status = GetConfigString(cfg, "status") ?? "draft";

        var web = variants.FirstOrDefault(v => v.Kind is "web_long" or "seo_meta")
                  ?? variants.FirstOrDefault();
        var title = web?.Title ?? topic.Title;
        var content = web?.BodyMarkdown ?? topic.Title;
        if (!string.IsNullOrWhiteSpace(topic.CtaUrl))
            content += $"\n\n[CTA]({topic.CtaUrl})";

        var http = _httpFactory.CreateClient("content-publish");
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/wp-json/wp/v2/posts");
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        req.Content = new StringContent(
            JsonSerializer.Serialize(new { title, content, status, format = "standard" }, JsonOpts),
            Encoding.UTF8,
            "application/json");

        using var res = await http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"WordPress {(int)res.StatusCode}: {Trim(body)}");

        using var doc = JsonDocument.Parse(body);
        var id = doc.RootElement.TryGetProperty("id", out var idEl) ? idEl.ToString() : null;
        var link = doc.RootElement.TryGetProperty("link", out var linkEl) ? linkEl.GetString() : null;
        return new ConnectorResult(id ?? link, body);
    }

    private async Task<ConnectorResult> RunFacebookAsync(
        ContentRepository.PublishJobRow job,
        ContentRepository.TopicRow topic,
        IReadOnlyList<ContentRepository.VariantRow> variants,
        CancellationToken ct)
    {
        var channel = job.ChannelTargetId is { } cid
            ? await _repo.GetChannelAsync(cid, ct)
            : null;
        if (channel is null)
            throw new InvalidOperationException("Channel target missing for Facebook job");

        var pageId = channel.ExternalId?.Trim()
                     ?? throw new InvalidOperationException("Facebook external_id (page id) required");
        var token = ResolveSecret(channel.SecretRef)
                    ?? throw new InvalidOperationException("Facebook secret_ref (page access token) missing");

        var fb = variants.FirstOrDefault(v => v.Kind is "fb_page" or "fb_short")
                 ?? variants.FirstOrDefault();
        var message = fb?.BodyMarkdown ?? topic.Title;
        if (!string.IsNullOrWhiteSpace(topic.CtaUrl))
            message = $"{message.Trim()}\n\n{topic.CtaUrl}";

        var http = _httpFactory.CreateClient("content-publish");
        var url =
            $"https://graph.facebook.com/v21.0/{Uri.EscapeDataString(pageId)}/feed";
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["message"] = message,
            ["access_token"] = token,
        });
        using var res = await http.PostAsync(url, content, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Facebook {(int)res.StatusCode}: {Trim(body)}");

        using var doc = JsonDocument.Parse(body);
        var postId = doc.RootElement.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        return new ConnectorResult(postId, body);
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
        var token = ResolveSecret(site.SecretRef)
                    ?? throw new InvalidOperationException("astro_git secret_ref (GitHub token) missing");

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
