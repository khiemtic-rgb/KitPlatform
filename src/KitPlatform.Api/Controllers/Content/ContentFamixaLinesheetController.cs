using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Api.Controllers.Content;

/// <summary>
/// M1 "Dựng tập từ bảng câu": kịch bản CSV + giọng + ảnh tách lớp → video nháp, không gọi AI trả phí.
/// Tenant lấy từ claim (ProductionTenantActionFilter + IProductionCallerTenant), không nhận tenant từ body hay đường dẫn.
/// </summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[RequirePlatformModule(PlatformModuleCodes.KitContent)]
[ServiceFilter(typeof(ProductionTenantActionFilter))]
[Route("api/content/famixa/linesheet")]
public sealed class ContentFamixaLinesheetController : ControllerBase
{
    const long MaxZipBytes = 1100L * 1024 * 1024;
    const long MaxCsvBytes = 5L * 1024 * 1024;

    readonly IFamixaLinesheetService _svc;

    public ContentFamixaLinesheetController(IFamixaLinesheetService svc) => _svc = svc;

    // ---------- sản xuất ----------

    [HttpGet("productions")]
    public Task<IActionResult> List(CancellationToken ct) => Guard(async () => Ok(await _svc.ListProductionsAsync(ct)));

    [HttpPost("productions")]
    public Task<IActionResult> Create([FromBody] LinesheetCreateProductionRequest body, CancellationToken ct) =>
        Guard(async () => Ok(await _svc.CreateProductionAsync(body, ct)));

    [HttpGet("productions/{id:guid}/status")]
    public Task<IActionResult> Status(Guid id, CancellationToken ct) => Guard(async () => Ok(await _svc.GetStatusAsync(id, ct)));

    // ---------- kịch bản, cấu hình, file ----------

    [HttpPost("productions/{id:guid}/script")]
    [RequestSizeLimit(MaxCsvBytes)]
    public Task<IActionResult> ImportScript(Guid id, IFormFile file, CancellationToken ct) =>
        Guard(async () => Ok(new { version = await _svc.ImportScriptAsync(id, await ReadSmall(file, ct), ct) }));

    [HttpGet("productions/{id:guid}/settings")]
    public Task<IActionResult> GetSettings(Guid id, CancellationToken ct) =>
        Guard(async () => Content(await _svc.GetSettingsAsync(id, ct), "application/json", Encoding.UTF8));

    [HttpPut("productions/{id:guid}/settings")]
    public Task<IActionResult> PutSettings(Guid id, [FromBody] JsonElement body, CancellationToken ct) =>
        Guard(async () =>
        {
            await _svc.PutSettingsAsync(id, body.GetRawText(), ct);
            return Ok(await _svc.GetStatusAsync(id, ct));
        });

    [HttpPost("productions/{id:guid}/files")]
    [RequestSizeLimit(MaxZipBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxZipBytes)]
    public Task<IActionResult> UploadFiles(Guid id, IFormFile file, CancellationToken ct) =>
        Guard(async () =>
        {
            if (file is null || file.Length == 0) return BadRequest(new { message = LinesheetErrorCodes.BadUpload + ": chọn một file .zip." });
            await using var stream = file.OpenReadStream();
            return Ok(await _svc.UploadFilesAsync(id, stream, ct));
        });

    // ---------- giọng ElevenLabs ----------

    /// <summary>Xem trước: câu nào sẽ gửi, kèm thẻ cảm xúc, tổng ký tự. Không gọi ElevenLabs.</summary>
    [HttpGet("productions/{id:guid}/tts")]
    public Task<IActionResult> PreviewTts(Guid id, CancellationToken ct) => Guard(async () => Ok(await _svc.PreviewTtsAsync(id, ct)));

    /// <summary>Gọi ElevenLabs (tốn tiền). Phải gửi đúng số ký tự đã xem trước trong confirmChars.</summary>
    [HttpPost("productions/{id:guid}/tts")]
    public Task<IActionResult> GenerateTts(Guid id, [FromBody] LinesheetTtsRequest body, CancellationToken ct) =>
        Guard(async () => Ok(await _svc.GenerateTtsAsync(id, body, ct)));

    /// <summary>Nghe thử giọng đã lưu của một câu.</summary>
    [HttpGet("productions/{id:guid}/audio/{rowId}")]
    public Task<IActionResult> LineAudio(Guid id, string rowId, CancellationToken ct) =>
        Guard(async () =>
        {
            var d = await _svc.GetLineAudioAsync(id, rowId, ct);
            return PhysicalFile(d.Path, d.ContentType, d.FileName, enableRangeProcessing: true);
        });

    // ---------- chuyển động bằng video AI ----------

    /// <summary>Xem trước: shot nào cần tạo chuyển động, bao nhiêu credit. Không gọi Runway.</summary>
    [HttpGet("productions/{id:guid}/motion")]
    public Task<IActionResult> PreviewMotion(Guid id, CancellationToken ct) => Guard(async () => Ok(await _svc.PreviewMotionAsync(id, ct)));

    /// <summary>Gọi Runway (tốn credit). Phải gửi đúng số credit đã xem trước trong confirmCredits.</summary>
    [HttpPost("productions/{id:guid}/motion")]
    public Task<IActionResult> GenerateMotion(Guid id, [FromBody] LinesheetMotionRequest body, CancellationToken ct) =>
        Guard(async () => Ok(await _svc.GenerateMotionAsync(id, body, ct)));

    [HttpGet("productions/{id:guid}/motion/{shotId}")]
    public Task<IActionResult> MotionClip(Guid id, string shotId, CancellationToken ct) =>
        Guard(async () =>
        {
            var d = await _svc.GetMotionClipAsync(id, shotId, ct);
            return PhysicalFile(d.Path, d.ContentType, d.FileName, enableRangeProcessing: true);
        });

    // ---------- ghép môi ----------

    /// <summary>Xem trước: câu nào ghép môi được, bao nhiêu USD. Không gọi fal.</summary>
    [HttpGet("productions/{id:guid}/lipsync")]
    public Task<IActionResult> PreviewLipsync(Guid id, CancellationToken ct) => Guard(async () => Ok(await _svc.PreviewLipsyncAsync(id, ct)));

    /// <summary>Gọi fal (tốn tiền). Phải gửi đúng số USD đã xem trước trong confirmUsd.</summary>
    [HttpPost("productions/{id:guid}/lipsync")]
    public Task<IActionResult> GenerateLipsync(Guid id, [FromBody] LinesheetLipsyncRequest body, CancellationToken ct) =>
        Guard(async () => Ok(await _svc.GenerateLipsyncAsync(id, body, ct)));

    // ---------- tạo ảnh cảnh bằng API ----------

    /// <summary>Xem trước: ảnh cảnh nào trong settings.keyframe_specs sẽ tạo, bao nhiêu credit. Không gọi Runway.</summary>
    [HttpGet("productions/{id:guid}/keyframes")]
    public Task<IActionResult> PreviewKeyframes(Guid id, CancellationToken ct) => Guard(async () => Ok(await _svc.PreviewKeyframeGenAsync(id, ct)));

    /// <summary>Gọi Runway (tốn credit). Phải gửi đúng số credit đã xem trước trong confirmCredits.</summary>
    [HttpPost("productions/{id:guid}/keyframes")]
    public Task<IActionResult> GenerateKeyframes(Guid id, [FromBody] LinesheetKeyframeGenRequest body, CancellationToken ct) =>
        Guard(async () => Ok(await _svc.GenerateKeyframesAsync(id, body, ct)));

    [HttpGet("productions/{id:guid}/keyframes/{name}")]
    public Task<IActionResult> KeyframeImage(Guid id, string name, CancellationToken ct) =>
        Guard(async () =>
        {
            var d = await _svc.GetKeyframeImageAsync(id, name, ct);
            return PhysicalFile(d.Path, d.ContentType, d.FileName);
        });

    // ---------- dòng thời gian, phụ đề, shot ----------

    [HttpPost("productions/{id:guid}/timeline")]
    public Task<IActionResult> BuildTimeline(Guid id, [FromQuery] bool allowMissing, CancellationToken ct) =>
        Guard(async () => Ok(await _svc.BuildTimelineAsync(id, allowMissing, ct)));

    [HttpGet("productions/{id:guid}/timeline.csv")]
    public Task<IActionResult> TimelineCsv(Guid id, CancellationToken ct) =>
        Guard(async () => Text(await _svc.GetTimelineCsvAsync(id, ct), "text/csv", "timeline.csv"));

    [HttpGet("productions/{id:guid}/subtitles/{lang}")]
    public Task<IActionResult> Subtitles(Guid id, string lang, CancellationToken ct) =>
        Guard(async () => Text(await _svc.GetSubtitlesAsync(id, lang, ct), "application/x-subrip", $"subtitles_{lang}.srt"));

    [HttpPost("productions/{id:guid}/shots/draft")]
    public Task<IActionResult> DraftShots(Guid id, [FromQuery] bool force, CancellationToken ct) =>
        Guard(async () => Ok(new { shots = await _svc.DraftShotsAsync(id, force, ct) }));

    [HttpGet("productions/{id:guid}/shots.csv")]
    public Task<IActionResult> ShotsCsv(Guid id, CancellationToken ct) =>
        Guard(async () => Text(await _svc.GetShotsCsvAsync(id, ct), "text/csv", "shots.csv"));

    [HttpPut("productions/{id:guid}/shots")]
    [RequestSizeLimit(MaxCsvBytes)]
    public Task<IActionResult> PutShots(Guid id, IFormFile file, CancellationToken ct) =>
        Guard(async () => Ok(new { shots = await _svc.PutShotsCsvAsync(id, await ReadSmall(file, ct), ct) }));

    // ---------- dựng ----------

    [HttpPost("productions/{id:guid}/renders")]
    public Task<IActionResult> QueueRender(Guid id, [FromBody] LinesheetQueueRenderRequest body, CancellationToken ct) =>
        Guard(async () => Ok(await _svc.QueueRenderAsync(id, body, ct)));

    [HttpGet("productions/{id:guid}/renders")]
    public Task<IActionResult> ListRuns(Guid id, CancellationToken ct) => Guard(async () => Ok(await _svc.ListRunsAsync(id, ct)));

    [HttpGet("renders/{runId:guid}")]
    public Task<IActionResult> GetRun(Guid runId, CancellationToken ct) => Guard(async () => Ok(await _svc.GetRunAsync(runId, ct)));

    [HttpPost("renders/{runId:guid}/cancel")]
    public Task<IActionResult> CancelRun(Guid runId, CancellationToken ct) =>
        Guard(async () => Ok(new { cancelled = await _svc.CancelRunAsync(runId, ct) }));

    /// <summary><c>what</c>: video | srt-vi | srt-en.</summary>
    [HttpGet("renders/{runId:guid}/download/{what}")]
    public Task<IActionResult> Download(Guid runId, string what, CancellationToken ct) =>
        Guard(async () =>
        {
            var d = await _svc.DownloadRunFileAsync(runId, what, ct);
            return PhysicalFile(d.Path, d.ContentType, d.FileName, enableRangeProcessing: true);
        });

    // ---------- tiện ích ----------

    static async Task<byte[]> ReadSmall(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw new InvalidOperationException(LinesheetErrorCodes.Empty + ": chưa chọn file.");
        if (file.Length > MaxCsvBytes) throw new InvalidOperationException(LinesheetErrorCodes.BadUpload + ": file quá lớn.");
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    FileContentResult Text(string text, string type, string name) =>
        File(new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray(), type + "; charset=utf-8", name);

    async Task<IActionResult> Guard(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (LinesheetValidationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message,
                errors = ex.Errors.Take(200).Select(e => new { e.Line, e.RowId, e.Code, e.Message }),
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith(LinesheetErrorCodes.NotFound, StringComparison.Ordinal))
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith(ProductionTenantBoundary.Forbidden, StringComparison.Ordinal))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith(ProductionTenantBoundary.Required, StringComparison.Ordinal))
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
