namespace KitPlatform.Packs.FamilyOs;

public static class FamilyMomentMediaKinds
{
    public const string Photo = "photo";
    public const string Audio = "audio";
}

public sealed record FamilyMomentUploadResult(
    string Url,
    string MediaKind);

public interface IFamilyMomentUploadService
{
    Task<FamilyMomentUploadResult> SaveAsync(
        Guid familyId,
        Guid? memberId,
        Stream content,
        string originalFileName,
        string? contentType,
        long declaredLength,
        CancellationToken cancellationToken = default);
}
