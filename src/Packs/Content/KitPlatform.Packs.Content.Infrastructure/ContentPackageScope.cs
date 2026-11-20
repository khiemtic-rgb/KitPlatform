namespace KitPlatform.Packs.Content.Infrastructure;

internal static class ContentPackageScope
{
    public static Guid RequireBrand(ContentRepository.PackageRow row) =>
        row.BrandId ?? throw new InvalidOperationException("Idea chưa gắn thương hiệu.");

    public static Guid RequireTopic(ContentRepository.PackageRow row) =>
        row.TopicId ?? throw new InvalidOperationException("Idea chưa có nội dung.");
}
