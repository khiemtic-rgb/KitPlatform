using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentWorkQueueService : IContentWorkQueueService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ContentWorkRepository _work;
    private readonly ContentRepository _repo;
    private readonly IContentGenerateService _generate;
    private readonly IContentPackageService _packages;
    private readonly IContentPublishService _publish;
    private readonly IContentVideoService _videos;
    private readonly ContentOptions _options;
    private readonly IHostEnvironment _env;
    private readonly ILogger<ContentWorkQueueService> _logger;

    public ContentWorkQueueService(
        ContentWorkRepository work,
        ContentRepository repo,
        IContentGenerateService generate,
        IContentPackageService packages,
        IContentPublishService publish,
        IContentVideoService videos,
        IOptions<ContentOptions> options,
        IHostEnvironment env,
        ILogger<ContentWorkQueueService> logger)
    {
        _work = work;
        _repo = repo;
        _generate = generate;
        _packages = packages;
        _publish = publish;
        _videos = videos;
        _options = options.Value;
        _env = env;
        _logger = logger;
    }

    public Task<EnqueueWorkResultDto> EnqueueGenerateTopicAsync(
        Guid topicId,
        GenerateContentRequest request,
        CancellationToken cancellationToken = default) =>
        EnqueueAsync(
            ContentWorkKinds.GenerateTopic,
            topicId: topicId,
            request: request,
            cancellationToken: cancellationToken);

    public Task<EnqueueWorkResultDto> EnqueueGeneratePackageAsync(
        Guid packageId,
        GenerateContentRequest request,
        CancellationToken cancellationToken = default) =>
        EnqueueAsync(
            ContentWorkKinds.GeneratePackage,
            packageId: packageId,
            request: request,
            cancellationToken: cancellationToken);

    public async Task<EnqueueWorkResultDto> EnqueuePublishTopicAsync(
        Guid topicId,
        PublishContentRequest request,
        CancellationToken cancellationToken = default)
    {
        var topic = await _repo.GetTopicAsync(topicId, cancellationToken)
                    ?? throw new InvalidOperationException("Topic not found");
        var packageId = await _repo.GetPackageIdByTopicAsync(topicId, cancellationToken);
        var availableAt = request.PublishAt is { } at && at > DateTimeOffset.UtcNow.AddMinutes(1)
            ? at
            : DateTimeOffset.UtcNow;

        var payload = new PublishPayload
        {
            SiteTargetIds = request.SiteTargetIds?.ToList(),
            ChannelTargetIds = request.ChannelTargetIds?.ToList(),
            IncludeManualExport = request.IncludeManualExport,
            PublishAt = request.PublishAt,
            ImageBase64 = request.ImageBase64,
            ImageFileName = request.ImageFileName,
            ImageContentType = request.ImageContentType,
        };

        return await InsertOrReuseAsync(
            ContentWorkKinds.PublishTopic,
            topic.BrandId,
            topicId,
            packageId,
            videoJobId: null,
            topic.Title,
            payload,
            availableAt,
            markGenerating: false,
            cancellationToken);
    }

    public async Task<EnqueueWorkResultDto> EnqueueVideoMvpAsync(
        Guid videoJobId,
        RunVideoMvpPipelineRequest request,
        CancellationToken cancellationToken = default)
    {
        var video = await _videos.GetJobAsync(videoJobId, cancellationToken)
                    ?? throw new InvalidOperationException("Video job not found");
        return await InsertOrReuseAsync(
            ContentWorkKinds.VideoMvp,
            video.BrandId,
            video.TopicId,
            video.PackageId,
            videoJobId,
            video.Title,
            request,
            DateTimeOffset.UtcNow,
            markGenerating: false,
            cancellationToken);
    }

    public async Task<EnqueueWorkResultDto> EnqueueVideoRenderAsync(
        Guid videoJobId,
        CancellationToken cancellationToken = default)
    {
        var video = await _videos.GetJobAsync(videoJobId, cancellationToken)
                    ?? throw new InvalidOperationException("Video job not found");
        return await InsertOrReuseAsync(
            ContentWorkKinds.VideoRender,
            video.BrandId,
            video.TopicId,
            video.PackageId,
            videoJobId,
            video.Title,
            new { },
            DateTimeOffset.UtcNow,
            markGenerating: false,
            cancellationToken);
    }

    public async Task<EnqueueWorkResultDto> EnqueueBrandAdaptAsync(
        Guid packageId,
        AnalyzeAdaptRequest request,
        CancellationToken cancellationToken = default)
    {
        var package = await _repo.GetPackageAsync(packageId, cancellationToken)
                      ?? throw new InvalidOperationException("Package not found");
        return await InsertOrReuseAsync(
            ContentWorkKinds.BrandAdapt,
            package.BrandId,
            package.TopicId,
            packageId,
            videoJobId: null,
            package.Title,
            request,
            DateTimeOffset.UtcNow,
            markGenerating: false,
            cancellationToken);
    }

    public async Task<AnalyzePoolResultDto> EnqueueBrandAdaptBatchAsync(
        AnalyzePoolRequest request,
        CancellationToken cancellationToken = default)
    {
        var ids = (request.PackageIds ?? [])
            .Where(x => x != Guid.Empty)
            .Distinct()
            .Take(20)
            .ToList();
        if (ids.Count == 0)
            throw new InvalidOperationException("Chọn ít nhất một Core Idea để chấm Fit.");

        var analyze = new AnalyzeAdaptRequest(
            request.BrandIds,
            request.IncludeMaybe,
            GenerateFits: false,
            CreatePackages: false,
            IncludeSourceBrand: true);

        var jobs = new List<EnqueueWorkResultDto>();
        foreach (var id in ids)
            jobs.Add(await EnqueueBrandAdaptAsync(id, analyze, cancellationToken));

        return new AnalyzePoolResultDto(
            jobs,
            $"Đã xếp {jobs.Count} job chấm Brand Fit. Chưa tạo góc — bạn tick ô rồi bấm Tạo góc đã chọn.");
    }

    public async Task<ContentWorkJobDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _work.GetAsync(id, cancellationToken);
        return row is null ? null : MapJob(row);
    }

    public async Task<IReadOnlyList<ContentWorkJobDto>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _work.ListActiveAsync(cancellationToken);
        return rows.Select(MapJob).ToList();
    }

    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        await _work.RequeueOrphanedRunningAsync(TimeSpan.FromMinutes(3), cancellationToken);
        var claimed = await _work.ClaimNextAsync(cancellationToken);
        if (claimed is null) return false;

        try
        {
            var resultJson = await RunClaimedAsync(claimed, cancellationToken);
            await _work.MarkSucceededAsync(claimed.Id, resultJson, cancellationToken);
            _logger.LogInformation("Content work {JobId} {Kind} succeeded", claimed.Id, claimed.Kind);
        }
        catch (Exception ex)
        {
            var nextRetry = claimed.RetryCount + 1;
            var delay = TimeSpan.FromSeconds(Math.Min(30 * nextRetry, 180));
            await _work.MarkRetryOrFailAsync(
                claimed.Id,
                ex.Message,
                nextRetry,
                claimed.MaxRetries,
                nextRetry < claimed.MaxRetries ? DateTimeOffset.UtcNow.Add(delay) : null,
                cancellationToken);
            _logger.LogWarning(ex, "Content work {JobId} {Kind} failed (retry {Retry}/{Max})",
                claimed.Id, claimed.Kind, nextRetry, claimed.MaxRetries);
        }

        return true;
    }

    public async Task<int> ProcessDuePublishJobsAsync(int limit = 3, CancellationToken cancellationToken = default)
    {
        var ids = await _work.ListDuePublishJobIdsAsync(limit, cancellationToken);
        var ran = 0;
        foreach (var id in ids)
        {
            try
            {
                await _publish.RunJobAsync(id, null, cancellationToken);
                ran++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Due publish job {JobId} failed", id);
            }
        }

        return ran;
    }

    private async Task<EnqueueWorkResultDto> EnqueueAsync(
        string kind,
        Guid? topicId = null,
        Guid? packageId = null,
        GenerateContentRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        Guid brandId;
        string title;
        Guid? resolvedTopic = topicId;
        Guid? resolvedPackage = packageId;

        if (packageId is Guid pkgId)
        {
            var package = await _repo.GetPackageAsync(pkgId, cancellationToken)
                          ?? throw new InvalidOperationException("Package not found");
            brandId = package.BrandId;
            title = package.Title;
            resolvedTopic = package.TopicId;
        }
        else if (topicId is Guid tid)
        {
            var topic = await _repo.GetTopicAsync(tid, cancellationToken)
                        ?? throw new InvalidOperationException("Topic not found");
            brandId = topic.BrandId;
            title = topic.Title;
            resolvedPackage = await _repo.GetPackageIdByTopicAsync(tid, cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("Thiếu topic hoặc package.");
        }

        return await InsertOrReuseAsync(
            kind,
            brandId,
            resolvedTopic,
            resolvedPackage,
            videoJobId: null,
            title,
            request ?? new GenerateContentRequest(),
            DateTimeOffset.UtcNow,
            markGenerating: true,
            cancellationToken);
    }

    private async Task<EnqueueWorkResultDto> InsertOrReuseAsync(
        string kind,
        Guid brandId,
        Guid? topicId,
        Guid? packageId,
        Guid? videoJobId,
        string title,
        object payload,
        DateTimeOffset availableAt,
        bool markGenerating,
        CancellationToken cancellationToken)
    {
        var existing = await _work.FindActiveAsync(kind, topicId, packageId, videoJobId, cancellationToken);
        if (existing is not null)
        {
            return new EnqueueWorkResultDto(
                MapJob(existing),
                existing.Status == ContentWorkStatuses.Running
                    ? "Job đang chạy."
                    : "Job đã có trong hàng đợi.");
        }

        if (kind == ContentWorkKinds.PublishTopic && payload is PublishPayload pub)
            payload = await PersistPublishImageAsync(pub, cancellationToken);

        var id = await _work.InsertAsync(new ContentWorkRepository.WorkJobRow
        {
            Kind = kind,
            Status = ContentWorkStatuses.Queued,
            BrandId = brandId,
            TopicId = topicId,
            PackageId = packageId,
            VideoJobId = videoJobId,
            Title = title.Length > 480 ? title[..480] : title,
            PayloadJson = JsonSerializer.Serialize(payload, JsonOpts),
            MaxRetries = Math.Clamp(_options.WorkerMaxRetries, 0, 10),
            AvailableAt = availableAt,
        }, cancellationToken);

        if (markGenerating)
        {
            if (topicId is Guid tid)
                await _repo.UpdateTopicStatusAsync(tid, "Generating", cancellationToken);
            if (packageId is Guid pkg)
                await _repo.UpdatePackageStatusAsync(pkg, "Generating", cancellationToken);
        }

        var row = await _work.GetAsync(id, cancellationToken)
                  ?? throw new InvalidOperationException("Không tạo được work job.");
        var when = availableAt > DateTimeOffset.UtcNow.AddMinutes(1)
            ? " Đã lên lịch " + availableAt.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM HH:mm") + "."
            : "";
        return new EnqueueWorkResultDto(MapJob(row), "Đã đưa vào hàng đợi." + when);
    }

    private async Task<string> RunClaimedAsync(
        ContentWorkRepository.WorkJobRow job,
        CancellationToken cancellationToken)
    {
        switch (job.Kind)
        {
            case ContentWorkKinds.GenerateTopic:
            {
                if (job.TopicId is not Guid topicId)
                    throw new InvalidOperationException("generate_topic thiếu topicId.");
                var req = Deserialize<GenerateContentRequest>(job.PayloadJson) ?? new GenerateContentRequest();
                var result = await _generate.GenerateAsync(topicId, req, cancellationToken);
                if (job.PackageId is Guid pkg)
                    await _repo.UpdatePackageStatusAsync(pkg, result.Topic.Status, cancellationToken);
                return JsonSerializer.Serialize(new
                {
                    budgetBlocked = result.BudgetBlocked,
                    message = result.Message,
                    variantCount = result.Variants.Count,
                    assetCount = result.Assets.Count,
                }, JsonOpts);
            }
            case ContentWorkKinds.GeneratePackage:
            {
                if (job.PackageId is not Guid packageId)
                    throw new InvalidOperationException("generate_package thiếu packageId.");
                var req = Deserialize<GenerateContentRequest>(job.PayloadJson) ?? new GenerateContentRequest();
                var result = await _packages.GenerateAllAsync(packageId, req, cancellationToken);
                return JsonSerializer.Serialize(new
                {
                    budgetBlocked = result.BudgetBlocked,
                    message = result.Message,
                    variantCount = result.Variants.Count,
                    assetCount = result.Assets.Count,
                }, JsonOpts);
            }
            case ContentWorkKinds.PublishTopic:
            {
                if (job.TopicId is not Guid topicId)
                    throw new InvalidOperationException("publish_topic thiếu topicId.");
                var payload = Deserialize<PublishPayload>(job.PayloadJson) ?? new PublishPayload();
                var request = new PublishContentRequest
                {
                    SiteTargetIds = payload.SiteTargetIds,
                    ChannelTargetIds = payload.ChannelTargetIds,
                    IncludeManualExport = payload.IncludeManualExport,
                    RunImmediately = true,
                    PublishAt = payload.PublishAt,
                    ImageFileName = payload.ImageFileName,
                    ImageContentType = payload.ImageContentType,
                    ImageBase64 = await ReadPublishImageAsync(payload),
                };
                var result = await _publish.PublishAsync(topicId, request, cancellationToken);
                if (job.PackageId is Guid pkg)
                {
                    var topic = await _repo.GetTopicAsync(topicId, cancellationToken);
                    if (topic is not null)
                        await _repo.UpdatePackageStatusAsync(pkg, topic.Status, cancellationToken);
                }

                return JsonSerializer.Serialize(new
                {
                    jobCount = result.Jobs.Count,
                    succeeded = result.Jobs.Count(j => j.Status == "Succeeded"),
                }, JsonOpts);
            }
            case ContentWorkKinds.VideoMvp:
            {
                if (job.VideoJobId is not Guid videoId)
                    throw new InvalidOperationException("video_mvp thiếu videoJobId.");
                var req = Deserialize<RunVideoMvpPipelineRequest>(job.PayloadJson)
                          ?? new RunVideoMvpPipelineRequest();
                await _videos.RunMvpPipelineAsync(videoId, req, cancellationToken);
                return "{}";
            }
            case ContentWorkKinds.VideoRender:
            {
                if (job.VideoJobId is not Guid videoId)
                    throw new InvalidOperationException("video_render thiếu videoJobId.");
                await _videos.QueueRenderAsync(videoId, cancellationToken);
                return "{}";
            }
            case ContentWorkKinds.BrandAdapt:
            {
                if (job.PackageId is not Guid packageId)
                    throw new InvalidOperationException("brand_adapt thiếu packageId.");
                var req = Deserialize<AnalyzeAdaptRequest>(job.PayloadJson) ?? new AnalyzeAdaptRequest();
                var fits = await _packages.AnalyzeAndAdaptAsync(packageId, req, cancellationToken);
                return JsonSerializer.Serialize(new
                {
                    fit = fits.Count(f => f.Verdict == "fit"),
                    maybe = fits.Count(f => f.Verdict == "maybe"),
                    skip = fits.Count(f => f.Verdict == "skip"),
                    created = fits.Count(f => f.PackageId is not null),
                }, JsonOpts);
            }
            default:
                throw new InvalidOperationException("Unknown work kind: " + job.Kind);
        }
    }

    private async Task<PublishPayload> PersistPublishImageAsync(
        PublishPayload payload,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.ImageBase64))
            return payload;

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(payload.ImageBase64.Trim());
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("ImageBase64 không hợp lệ");
        }

        if (bytes.Length == 0) return payload;

        var root = Path.GetFullPath(Path.Combine(_env.ContentRootPath, _options.WorkAssetRoot));
        Directory.CreateDirectory(root);
        var name = Guid.NewGuid().ToString("N") + ".bin";
        var path = Path.Combine(root, name);
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        payload.ImageBase64 = null;
        payload.ImagePath = path;
        return payload;
    }

    private static async Task<string?> ReadPublishImageAsync(PublishPayload payload)
    {
        if (!string.IsNullOrWhiteSpace(payload.ImageBase64))
            return payload.ImageBase64;
        if (string.IsNullOrWhiteSpace(payload.ImagePath) || !File.Exists(payload.ImagePath))
            return null;
        var bytes = await File.ReadAllBytesAsync(payload.ImagePath);
        return bytes.Length == 0 ? null : Convert.ToBase64String(bytes);
    }

    private static T? Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
            return default;
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOpts);
        }
        catch
        {
            return default;
        }
    }

    internal static ContentWorkJobDto MapJob(ContentWorkRepository.WorkJobRow r) =>
        new(
            r.Id, r.Kind, r.Status, r.BrandId, r.BrandCode, r.BrandName,
            r.TopicId, r.PackageId, r.VideoJobId, r.Title, r.ErrorMessage,
            r.RetryCount, r.MaxRetries, r.AvailableAt, r.CreatedAt, r.StartedAt, r.CompletedAt,
            Message: r.Status switch
            {
                ContentWorkStatuses.Queued => "Đang chờ worker.",
                ContentWorkStatuses.Running => "Worker đang xử lý.",
                ContentWorkStatuses.Succeeded => "Xong.",
                ContentWorkStatuses.Failed => r.ErrorMessage ?? "Thất bại.",
                _ => r.Status,
            });

    private sealed class PublishPayload
    {
        public List<Guid>? SiteTargetIds { get; set; }
        public List<Guid>? ChannelTargetIds { get; set; }
        public bool IncludeManualExport { get; set; } = true;
        public DateTimeOffset? PublishAt { get; set; }
        public string? ImageBase64 { get; set; }
        public string? ImagePath { get; set; }
        public string? ImageFileName { get; set; }
        public string? ImageContentType { get; set; }
    }
}
