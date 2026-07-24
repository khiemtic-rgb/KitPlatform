using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os/families/{familyId:guid}")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
public sealed class FamilyOsEvidenceController : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp",
    };

    private const long MaxFileBytes = 5 * 1024 * 1024;

    private readonly IFamilyGraphService _families;
    private readonly ITenantContext _tenant;
    private readonly IWebHostEnvironment _environment;

    public FamilyOsEvidenceController(
        IFamilyGraphService families,
        ITenantContext tenant,
        IWebHostEnvironment environment)
    {
        _families = families;
        _tenant = tenant;
        _environment = environment;
    }

    /// <summary>Upload photo proof for a completed commitment (JPG/PNG/WebP, max 5 MB).</summary>
    [HttpPost("evidence")]
    [RequestSizeLimit(MaxFileBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFileBytes)]
    [ProducesResponseType(typeof(FamilyEvidenceUploadResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyEvidenceUploadResult>> Upload(
        Guid familyId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        try
        {
            _ = await _families.GetFamilyAsync(familyId, cancellationToken)
                ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

            var url = await SaveEvidenceAsync(file, cancellationToken);
            return Ok(new FamilyEvidenceUploadResult(url));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    private async Task<string> SaveEvidenceAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
            throw new InvalidOperationException("Chọn ảnh để tải lên.");

        if (file.Length > MaxFileBytes)
            throw new InvalidOperationException("Ảnh tối đa 5 MB.");

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            throw new InvalidOperationException("Chỉ hỗ trợ ảnh JPG, PNG hoặc WebP.");

        var tenantFolder = _tenant.TenantId.ToString("N");
        var directory = Path.Combine(_environment.ContentRootPath, "uploads", "family-os", tenantFolder);
        Directory.CreateDirectory(directory);

        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(directory, fileName);

        await using (var stream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return $"/uploads/family-os/{tenantFolder}/{fileName}";
    }
}
