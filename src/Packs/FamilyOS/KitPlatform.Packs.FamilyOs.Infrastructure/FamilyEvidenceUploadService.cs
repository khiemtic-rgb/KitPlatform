using KitPlatform.Application.Abstractions;
using KitPlatform.Packs.FamilyOs;
using Microsoft.Extensions.Hosting;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyEvidenceUploadService : IFamilyEvidenceUploadService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp",
    };

    private const long MaxFileBytes = 5 * 1024 * 1024;

    private readonly FamilyEvidenceUploadRepository _repo;
    private readonly IFamilyGraphService _families;
    private readonly ITenantContext _tenant;
    private readonly IHostEnvironment _environment;

    public FamilyEvidenceUploadService(
        FamilyEvidenceUploadRepository repo,
        IFamilyGraphService families,
        ITenantContext tenant,
        IHostEnvironment environment)
    {
        _repo = repo;
        _families = families;
        _tenant = tenant;
        _environment = environment;
    }

    public async Task<FamilyEvidenceUploadResult> SaveAsync(
        Guid familyId,
        Guid? memberId,
        Stream content,
        string originalFileName,
        long declaredLength,
        CancellationToken cancellationToken = default)
    {
        _ = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Khong tim thay gia dinh.");

        if (declaredLength <= 0)
            throw new InvalidOperationException("Chon anh de tai len.");
        if (declaredLength > MaxFileBytes)
            throw new InvalidOperationException("Anh toi da 5 MB.");

        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            throw new InvalidOperationException("Chi ho tro anh JPG, PNG hoac WebP.");

        await using var ms = new MemoryStream(capacity: (int)Math.Min(declaredLength, MaxFileBytes));
        await content.CopyToAsync(ms, cancellationToken);
        var bytes = ms.ToArray();
        if (bytes.Length == 0)
            throw new InvalidOperationException("Chon anh de tai len.");
        if (bytes.Length > MaxFileBytes)
            throw new InvalidOperationException("Anh toi da 5 MB.");

        var probe = FamilyEvidenceImageProbe.Probe(bytes);
        if (probe.HardBlockCodes.Count > 0)
        {
            var code = probe.HardBlockCodes[0];
            throw new InvalidOperationException(FamilyEvidenceImageProbe.HardBlockMessageVi(code));
        }

        var dupAt = await _repo.FindRecentDuplicateAsync(
            familyId,
            probe.Sha256Hex,
            FamilyEvidenceImageProbe.DuplicateLookbackDays,
            cancellationToken);
        if (dupAt is not null)
            throw new InvalidOperationException(
                FamilyEvidenceImageProbe.HardBlockMessageVi(FamilyEvidenceImageProbe.DuplicateCode));

        var tenantFolder = _tenant.TenantId.ToString("N");
        var directory = Path.Combine(_environment.ContentRootPath, "uploads", "family-os", tenantFolder);
        Directory.CreateDirectory(directory);
        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(directory, fileName);
        await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken);
        var url = $"/uploads/family-os/{tenantFolder}/{fileName}";

        await _repo.InsertAsync(
            familyId, memberId, probe.Sha256Hex, probe.ByteSize, probe.Width, probe.Height, url, cancellationToken);

        var warnings = probe.SoftWarningCodes;
        return new FamilyEvidenceUploadResult(
            url,
            probe.LooksLikeStudy,
            warnings.Count == 0 ? null : warnings,
            warnings.Count == 0 ? null : FamilyEvidenceImageProbe.SoftWarningMessageVi(warnings));
    }
}
