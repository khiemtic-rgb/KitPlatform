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
public sealed class FamilyOsMomentMediaController : ControllerBase
{
    private readonly IFamilyMomentUploadService _uploads;

    public FamilyOsMomentMediaController(IFamilyMomentUploadService uploads) => _uploads = uploads;

    /// <summary>Upload photo or short voice for kid relation moments (not study evidence).</summary>
    [HttpPost("moment-media")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 5 * 1024 * 1024)]
    [ProducesResponseType(typeof(FamilyMomentUploadResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyMomentUploadResult>> Upload(
        Guid familyId,
        IFormFile file,
        [FromForm] Guid? memberId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (file is null)
                return BadRequest(new { code = "validation_error", message = "Chọn ảnh hoặc ghi âm để tải lên." });

            await using var stream = file.OpenReadStream();
            var result = await _uploads.SaveAsync(
                familyId,
                memberId,
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
