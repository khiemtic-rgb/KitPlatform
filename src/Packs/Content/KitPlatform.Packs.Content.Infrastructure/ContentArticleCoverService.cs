using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>Gắn một ảnh bìa vẽ tại máy khi bài chưa có ảnh. Không phát sinh phí API.</summary>
internal sealed class ContentArticleCoverService
{
    private readonly ContentRepository _repo;
    private readonly ContentOptions _options;
    private readonly IHostEnvironment _env;

    public ContentArticleCoverService(ContentRepository repo, IOptions<ContentOptions> options, IHostEnvironment env)
    {
        _repo = repo;
        _options = options.Value;
        _env = env;
    }

    public async Task<bool> TryAttachAsync(
        ContentRepository.TopicRow topic,
        IReadOnlyList<ContentRepository.VariantRow> variants,
        CancellationToken ct)
    {
        var existing = await _repo.ListAssetsAsync(topic.Id, ct);
        if (existing.Any(a => a.Kind == "image"))
            return false;

        var body = variants.FirstOrDefault(v => v.Kind is "fb_page" or "social_caption")
                   ?? variants.FirstOrDefault(v => v.Kind == "web_long")
                   ?? variants.FirstOrDefault(v => v.Kind != "seo_meta");
        var png = ContentArticleCoverComposer.Render(
            topic.BrandName,
            topic.Title,
            ContentArticleCoverComposer.Excerpt(body?.BodyMarkdown, topic.Title));
        if (png.Length == 0 || png.Length > ContentPublishExtrasService.MaxImageBytes)
            throw new InvalidOperationException("Ảnh bìa tại máy vượt quá 2 MB.");

        var rel = $"image/{topic.Id:N}/cover-local.png";
        var path = Path.Combine(AssetRoot(), rel.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, png, ct);

        await _repo.InsertAssetAsync(new ContentRepository.AssetRow
        {
            TopicId = topic.Id,
            Kind = "image",
            FileName = "cover-local.png",
            ContentType = "image/png",
            StoragePath = rel,
            Prompt = topic.Title,
            Model = "local-article-cover",
            ImageTier = "local",
            EstimateUsd = 0,
            IsSelected = true,
            MetaJson = JsonSerializer.Serialize(new
            {
                role = "cover",
                sizeBytes = png.Length,
                source = "local-article-cover",
            }),
        }, ct);
        return true;
    }

    private string AssetRoot()
    {
        var configured = string.IsNullOrWhiteSpace(_options.AssetRoot) ? "App_Data/content-assets" : _options.AssetRoot;
        return Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(_env.ContentRootPath, configured));
    }
}
