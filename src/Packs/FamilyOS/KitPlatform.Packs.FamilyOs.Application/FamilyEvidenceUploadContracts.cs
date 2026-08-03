namespace KitPlatform.Packs.FamilyOs;

public sealed record FamilyEvidenceUploadResult(
    string Url,
    bool LooksLikeStudy = true,
    IReadOnlyList<string>? WarningCodes = null,
    string? WarningMessageVi = null,
    bool WasDuplicateBlocked = false);

public sealed record FamilyEvidenceUploadRequestMeta(
    Guid FamilyId,
    Guid? MemberId);

public interface IFamilyEvidenceUploadService
{
    Task<FamilyEvidenceUploadResult> SaveAsync(
        Guid familyId,
        Guid? memberId,
        Stream content,
        string originalFileName,
        long declaredLength,
        CancellationToken cancellationToken = default);
}
