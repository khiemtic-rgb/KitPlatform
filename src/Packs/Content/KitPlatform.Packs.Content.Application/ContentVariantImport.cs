namespace KitPlatform.Packs.Content;

public sealed record ContentVariantImportItem(string Kind, string? Title, string BodyMarkdown);

public sealed record ContentVariantImportRequest(
    IReadOnlyList<ContentVariantImportItem> Variants,
    string? Status);

public sealed record ContentVariantImportResultDto(Guid TopicId, int Imported, string Status);

/// <summary>Nhập bản viết có sẵn (nguyên văn) vào bài — không qua AI. Dùng cho trang pháp lý, nội dung đã soạn tay.</summary>
public interface IContentVariantImportService
{
    Task<ContentVariantImportResultDto?> ImportAsync(
        Guid topicId,
        ContentVariantImportRequest request,
        CancellationToken cancellationToken = default);
}
