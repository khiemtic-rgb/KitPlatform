using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os/families/{familyId:guid}")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
public sealed class FamilyOsEvidenceController : ControllerBase
{
    private readonly IFamilyEvidenceUploadService _uploads;

    public FamilyOsEvidenceController(IFamilyEvidenceUploadService uploads) => _uploads = uploads;

    /// <summary>Upload photo proof (P0.6 hard gates + P0.7 soft warnings).</summary>
    [HttpPost("evidence")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 5 * 1024 * 1024)]
    [ProducesResponseType(typeof(FamilyEvidenceUploadResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyEvidenceUploadResult>> Upload(
        Guid familyId,
        IFormFile file,
        [FromForm] Guid? memberId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (file is null)
                return BadRequest(new { code = "validation_error", message = "Chon anh de tai len." });

            await using var stream = file.OpenReadStream();
            var result = await _uploads.SaveAsync(
                familyId,
                memberId,
                stream,
                file.FileName,
                file.Length,
                cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            var code = ex.Message.Contains("giong anh da nop", StringComparison.OrdinalIgnoreCase)
                    || ex.Message.Contains("gi\u1ed1ng", StringComparison.OrdinalIgnoreCase)
                ? FamilyEvidenceImageProbe.DuplicateCode
                : ex.Message.Contains("nho", StringComparison.OrdinalIgnoreCase)
                    || ex.Message.Contains("nh\u1ecf", StringComparison.OrdinalIgnoreCase)
                    ? FamilyEvidenceImageProbe.TinyImageCode
                    : "validation_error";
            // Prefer exact message mapping via known VI helpers
            if (ex.Message == FamilyEvidenceImageProbe.HardBlockMessageVi(FamilyEvidenceImageProbe.DuplicateCode))
                code = FamilyEvidenceImageProbe.DuplicateCode;
            else if (ex.Message == FamilyEvidenceImageProbe.HardBlockMessageVi(FamilyEvidenceImageProbe.TinyImageCode))
                code = FamilyEvidenceImageProbe.TinyImageCode;
            return BadRequest(new { code, message = ex.Message });
        }
    }
}
