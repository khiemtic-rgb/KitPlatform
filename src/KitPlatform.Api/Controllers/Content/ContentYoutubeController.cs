using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Content;

namespace KitPlatform.Api.Controllers.Content;

/// <summary>Kết nối kênh YouTube + đính video vào bài (KIT Marketing Park).</summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[RequirePlatformModule(PlatformModuleCodes.KitContent)]
[Route("api/content")]
public sealed class ContentYoutubeController : ControllerBase
{
    private readonly IContentYoutubeConnectionService _yt;
    private readonly IContentVariantImportService _import;

    public ContentYoutubeController(IContentYoutubeConnectionService yt, IContentVariantImportService import)
    {
        _yt = yt;
        _import = import;
    }

    /// <summary>Nhập bản viết có sẵn (nguyên văn, không qua AI). Chỉ đặt Draft/Review; duyệt và đăng vẫn do người duyệt.</summary>
    [HttpPost("topics/{topicId:guid}/variants/import")]
    public async Task<ActionResult<ContentVariantImportResultDto>> ImportVariants(
        Guid topicId, [FromBody] ContentVariantImportRequest request, CancellationToken ct)
    {
        try
        {
            var r = await _import.ImportAsync(topicId, request, ct);
            return r is null ? NotFound() : Ok(r);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("youtube/oauth/start")]
    public async Task<ActionResult<ContentYoutubeStartDto>> Start([FromQuery] Guid brandId, CancellationToken ct)
    {
        try { return Ok(await _yt.StartAsync(brandId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("youtube/oauth/complete")]
    public async Task<ActionResult<ContentChannelTargetDto>> Complete(
        [FromBody] ContentYoutubeCompleteRequest request, CancellationToken ct)
    {
        try { return Ok(await _yt.CompleteAsync(request.Code, request.State, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (HttpRequestException ex) { return BadRequest(new { message = "Không gọi được Google: " + ex.Message }); }
    }

    [HttpPost("channels/{id:guid}/youtube/verify")]
    public async Task<ActionResult<ContentYoutubeVerifyDto>> Verify(Guid id, CancellationToken ct)
    {
        try { return Ok(await _yt.VerifyAsync(id, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("topics/{topicId:guid}/video")]
    [RequestSizeLimit(512L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 512L * 1024 * 1024)]
    public async Task<ActionResult<ContentYoutubeVideoDto>> UploadVideo(
        Guid topicId, IFormFile? file, CancellationToken ct)
    {
        try
        {
            file ??= Request.Form.Files.GetFile("file") ?? Request.Form.Files.FirstOrDefault();
            if (file is null || file.Length <= 0)
                return BadRequest(new { message = "Chọn file video (MP4/MOV/WEBM)." });
            await using var stream = file.OpenReadStream();
            var v = await _yt.UploadVideoAsync(topicId, stream, file.FileName, file.ContentType ?? "video/mp4", ct);
            return v is null ? NotFound() : Ok(v);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
