using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentVariantImportService : IContentVariantImportService
{
    private static readonly HashSet<string> AllowedKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "web_long", "seo_meta", "fb_page", "fb_short", "social_caption", "youtube", "video_caption",
    };

    private static readonly HashSet<string> AllowedStatus = new(StringComparer.OrdinalIgnoreCase)
    {
        "Draft", "Review",
    };

    private readonly ContentRepository _repo;

    public ContentVariantImportService(ContentRepository repo) => _repo = repo;

    public async Task<ContentVariantImportResultDto?> ImportAsync(
        Guid topicId, ContentVariantImportRequest request, CancellationToken ct = default)
    {
        var topic = await _repo.GetTopicAsync(topicId, ct);
        if (topic is null) return null;
        var items = (request.Variants ?? []).ToList();
        if (items.Count == 0)
            throw new InvalidOperationException("Chưa có bản viết nào để nhập.");

        foreach (var v in items)
        {
            var kind = (v.Kind ?? "").Trim();
            if (!AllowedKinds.Contains(kind))
                throw new InvalidOperationException($"Loại bản viết «{kind}» không hợp lệ.");
            if (string.IsNullOrWhiteSpace(v.BodyMarkdown))
                throw new InvalidOperationException($"Bản viết «{kind}» đang trống.");
        }

        foreach (var v in items)
        {
            await _repo.UpsertVariantAsync(
                topicId,
                v.Kind.Trim().ToLowerInvariant(),
                string.IsNullOrWhiteSpace(v.Title) ? topic.Title : v.Title.Trim(),
                v.BodyMarkdown.Replace("\r\n", "\n").Trim(),
                "{\"source\":\"manual_import\"}",
                ct);
        }

        var status = string.IsNullOrWhiteSpace(request.Status) ? "Review" : request.Status.Trim();
        if (!AllowedStatus.Contains(status))
            throw new InvalidOperationException("Trạng thái chỉ được là Draft hoặc Review (bạn tự duyệt/đăng sau).");
        await _repo.UpdateTopicStatusAsync(topicId, status, ct);
        return new ContentVariantImportResultDto(topicId, items.Count, status);
    }
}
