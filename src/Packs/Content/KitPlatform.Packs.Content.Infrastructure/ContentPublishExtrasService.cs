using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>Lưu ảnh bìa + thông tin đăng của bài. Ảnh lưu thành asset ảnh (role thumbnail/cover), còn lại vào topic.extra_json.publish.</summary>
internal sealed class ContentPublishExtrasService : IContentPublishExtrasService
{
    /// <summary>YouTube nhận ảnh bìa tối đa 2 MB; dùng chung giới hạn này cho mọi ảnh bìa.</summary>
    public const long MaxImageBytes = 2 * 1024 * 1024;

    private readonly ContentRepository _repo;
    private readonly ContentOptions _options;
    private readonly IHostEnvironment _env;

    public ContentPublishExtrasService(ContentRepository repo, IOptions<ContentOptions> options, IHostEnvironment env)
    {
        _repo = repo;
        _options = options.Value;
        _env = env;
    }

    public async Task<ContentPublishExtrasDto?> SaveAsync(
        Guid topicId,
        ContentPublishExtrasRequest request,
        ContentPublishImageUpload? thumbnail,
        ContentPublishImageUpload? cover,
        CancellationToken ct = default)
    {
        var topic = await _repo.GetTopicAsync(topicId, ct);
        if (topic is null) return null;

        var thumbId = thumbnail is null ? (Guid?)null : await SaveImageAsync(topicId, thumbnail, "thumbnail", ct);
        var coverId = cover is null ? (Guid?)null : await SaveImageAsync(topicId, cover, "cover", ct);

        var playlist = string.IsNullOrWhiteSpace(request.YoutubePlaylist) ? null : request.YoutubePlaylist.Trim();
        if (playlist is { Length: > 150 }) playlist = playlist[..150].TrimEnd();
        int? coverMs = request.CoverTimestampMs is { } ms ? Math.Clamp(ms, 0, 10 * 60 * 1000) : null;
        await _repo.MergeTopicExtraAsync(
            topicId,
            "publish",
            JsonSerializer.Serialize(new { youtubePlaylist = playlist, coverTimestampMs = coverMs, updatedAt = DateTimeOffset.UtcNow }),
            ct);

        return new ContentPublishExtrasDto(topicId, thumbId, coverId, playlist, coverMs);
    }

    private async Task<Guid> SaveImageAsync(Guid topicId, ContentPublishImageUpload upload, string role, CancellationToken ct)
    {
        var ext = Path.GetExtension(upload.FileName ?? "").ToLowerInvariant();
        var type = (upload.ContentType ?? "").ToLowerInvariant();
        var isPng = ext == ".png" || type == "image/png";
        var isJpg = ext is ".jpg" or ".jpeg" || type is "image/jpeg" or "image/jpg";
        if (!isPng && !isJpg)
            throw new InvalidOperationException("Ảnh bìa chỉ nhận JPG hoặc PNG.");

        var safe = $"{role}-{Guid.NewGuid():N}{(isPng ? ".png" : ".jpg")}";
        var rel = $"image/{topicId:N}/{safe}";
        var path = Path.Combine(AssetRoot(), rel.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        long size;
        await using (var fs = File.Create(path))
        {
            await upload.Content.CopyToAsync(fs, ct);
            size = fs.Length;
        }

        if (size <= 0 || size > MaxImageBytes)
        {
            File.Delete(path);
            throw new InvalidOperationException(size <= 0
                ? "File ảnh bìa rỗng."
                : $"Ảnh bìa {size / 1024} KB, tối đa 2 MB (YouTube chỉ nhận đến 2 MB).");
        }

        return await _repo.InsertAssetAsync(new ContentRepository.AssetRow
        {
            TopicId = topicId,
            Kind = "image",
            FileName = Path.GetFileName(upload.FileName ?? safe),
            ContentType = isPng ? "image/png" : "image/jpeg",
            StoragePath = rel,
            IsSelected = false,
            MetaJson = JsonSerializer.Serialize(new { role, sizeBytes = size, source = "video_studio_publish" }),
        }, ct);
    }

    private string AssetRoot()
    {
        var configured = string.IsNullOrWhiteSpace(_options.AssetRoot) ? "App_Data/content-assets" : _options.AssetRoot;
        return Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(_env.ContentRootPath, configured));
    }
}
