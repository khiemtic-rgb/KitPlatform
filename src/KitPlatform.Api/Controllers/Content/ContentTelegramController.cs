using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Content;

namespace KitPlatform.Api.Controllers.Content;

/// <summary>
/// Thông báo Telegram (KIT Marketing Park): một bot cho cả org, mỗi thương hiệu một chat_id.
/// Bot token chỉ ghi — không có endpoint nào trả token.
/// </summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[RequirePlatformModule(PlatformModuleCodes.KitContent)]
[Route("api/content/telegram")]
public sealed class ContentTelegramController : ControllerBase
{
    private readonly IContentTelegramSettingsService _telegram;

    public ContentTelegramController(IContentTelegramSettingsService telegram) => _telegram = telegram;

    /// <summary>GET api/content/telegram/settings?brandId= — cờ token + cài đặt của thương hiệu (mặc định nếu chưa lưu).</summary>
    [HttpGet("settings")]
    public async Task<ActionResult<ContentTelegramSettingsDto>> Get([FromQuery] Guid brandId, CancellationToken ct)
    {
        try { return Ok(await _telegram.GetAsync(brandId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>PUT api/content/telegram/settings?brandId= — lưu cài đặt thương hiệu; botToken null = giữ, "" = xóa.</summary>
    [HttpPut("settings")]
    public async Task<ActionResult<ContentTelegramSettingsDto>> Save(
        [FromQuery] Guid brandId,
        [FromBody] ContentTelegramSettingsRequest body,
        CancellationToken ct)
    {
        try { return Ok(await _telegram.SaveAsync(brandId, body ?? new ContentTelegramSettingsRequest(), ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>POST api/content/telegram/test?brandId= — gửi một tin thử tới chat_id đã lưu.</summary>
    [HttpPost("test")]
    public async Task<ActionResult<ContentTelegramTestResultDto>> Test([FromQuery] Guid brandId, CancellationToken ct)
    {
        try { return Ok(await _telegram.TestAsync(brandId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
