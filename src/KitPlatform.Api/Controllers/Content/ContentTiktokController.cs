using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Content;

namespace KitPlatform.Api.Controllers.Content;

/// <summary>Kết nối tài khoản TikTok (Login Kit) để Agent gửi video vào hộp nháp (KIT Marketing Park).</summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[RequirePlatformModule(PlatformModuleCodes.KitContent)]
[Route("api/content")]
public sealed class ContentTiktokController : ControllerBase
{
    private readonly IContentTiktokConnectionService _tt;

    public ContentTiktokController(IContentTiktokConnectionService tt) => _tt = tt;

    [HttpGet("tiktok/oauth/start")]
    public async Task<ActionResult<ContentTiktokStartDto>> Start([FromQuery] Guid brandId, CancellationToken ct)
    {
        try { return Ok(await _tt.StartAsync(brandId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("tiktok/oauth/complete")]
    public async Task<ActionResult<ContentChannelTargetDto>> Complete(
        [FromBody] ContentTiktokCompleteRequest request, CancellationToken ct)
    {
        try { return Ok(await _tt.CompleteAsync(request.Code, request.State, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (HttpRequestException ex) { return BadRequest(new { message = "Không gọi được TikTok: " + ex.Message }); }
    }

    [HttpPost("channels/{id:guid}/tiktok/verify")]
    public async Task<ActionResult<ContentTiktokVerifyDto>> Verify(Guid id, CancellationToken ct)
    {
        try { return Ok(await _tt.VerifyAsync(id, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (HttpRequestException ex) { return BadRequest(new { message = "Không gọi được TikTok: " + ex.Message }); }
    }
}
