using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Content;

namespace KitPlatform.Api.Controllers.Content;

/// <summary>
/// Agent quản trị đăng bài (KIT Marketing Park).
/// schedule-plan: chỉ đọc. proposals: lưu đề xuất; chỉ khi duyệt mới tạo job đăng qua đường đăng hiện có.
/// </summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[RequirePlatformModule(PlatformModuleCodes.KitContent)]
[Route("api/content/agent")]
public sealed class ContentAgentController : ControllerBase
{
    private readonly IContentScheduleAdvisor _advisor;
    private readonly IContentScheduleProposalService _proposals;
    private readonly IContentPublishPlanStore _plans;
    private readonly IContentAutopilotService _autopilot;
    private readonly IContentMediaCleanupService _cleanup;
    private readonly IContentPlatformSyncService _platform;

    public ContentAgentController(
        IContentScheduleAdvisor advisor,
        IContentScheduleProposalService proposals,
        IContentPublishPlanStore plans,
        IContentAutopilotService autopilot,
        IContentMediaCleanupService cleanup,
        IContentPlatformSyncService platform)
    {
        _advisor = advisor;
        _proposals = proposals;
        _plans = plans;
        _autopilot = autopilot;
        _cleanup = cleanup;
        _platform = platform;
    }

    /// <summary>GET api/content/agent/media-storage — dung lượng file video đính bài trên máy này, phần dọn được ngay.</summary>
    [HttpGet("media-storage")]
    public Task<ActionResult<ContentMediaStorageDto>> MediaStorage(CancellationToken cancellationToken = default)
        => Guard<ContentMediaStorageDto>("x", () => _cleanup.GetStorageAsync(cancellationToken));

    /// <summary>POST api/content/agent/media-cleanup — dọn ngay file video của bài đã đăng / đã huỷ quá hạn giữ.</summary>
    [HttpPost("media-cleanup")]
    public Task<ActionResult<ContentMediaCleanupReportDto>> MediaCleanup(CancellationToken cancellationToken = default)
        => Guard<ContentMediaCleanupReportDto>("x", () => _cleanup.CleanupAsync(Actor, cancellationToken));

    /// <summary>
    /// POST api/content/agent/autopilot/run?brandId= — chạy ngay một lượt Viết tự động (bỏ giới hạn 1 bài/ngày,
    /// vẫn giữ hạn mức tuần và số bài chờ duyệt). Trả về từng bước Agent đã làm.
    /// </summary>
    [HttpPost("autopilot/run")]
    public Task<ActionResult<ContentAutopilotReportDto>> RunAutopilot(
        [FromQuery] Guid brandId,
        CancellationToken cancellationToken = default)
        => Guard<ContentAutopilotReportDto>(brandId == Guid.Empty ? "" : "x", () => _autopilot.RunBrandAsync(brandId, true, cancellationToken));

    /// <summary>GET api/content/agent/schedule-settings?brandId= — cài đặt lịch đăng của thương hiệu (mặc định nếu chưa lưu).</summary>
    [HttpGet("schedule-settings")]
    public Task<ActionResult<ContentBrandScheduleSettingsDto>> GetScheduleSettings(
        [FromQuery] Guid brandId,
        CancellationToken cancellationToken = default)
        => Guard<ContentBrandScheduleSettingsDto>(brandId == Guid.Empty ? "" : "x", () => _plans.GetSettingsAsync(brandId, cancellationToken));

    /// <summary>PUT api/content/agent/schedule-settings?brandId= — lưu khung giờ, khoảng cách và hạn mức dài/ngắn mỗi kênh.</summary>
    [HttpPut("schedule-settings")]
    public Task<ActionResult<ContentBrandScheduleSettingsDto>> SaveScheduleSettings(
        [FromQuery] Guid brandId,
        [FromBody] ContentBrandScheduleSettingsRequest body,
        CancellationToken cancellationToken = default)
        => Guard<ContentBrandScheduleSettingsDto>(brandId == Guid.Empty ? "" : "x", () => _plans.SaveSettingsAsync(brandId, body ?? new(), cancellationToken));

    /// <summary>
    /// POST api/content/agent/schedule-topics — Video Studio gửi các bài vừa duyệt (dài/ngắn, loại kênh).
    /// Agent xếp vào khung trống theo cài đặt lịch và duyệt luôn (người dùng đã duyệt ở Studio).
    /// </summary>
    [HttpPost("schedule-topics")]
    public Task<ActionResult<ContentScheduleTopicsResultDto>> ScheduleTopics(
        [FromBody] ContentScheduleTopicsRequest body,
        CancellationToken cancellationToken = default)
        => Guard<ContentScheduleTopicsResultDto>(body?.Brand ?? "", () => _proposals.ScheduleTopicsAsync(body!, "Video Studio · " + Actor, cancellationToken));

    private string Actor =>
        User.FindFirst("email")?.Value
        ?? User.Identity?.Name
        ?? User.FindFirst("sub")?.Value
        ?? "admin";

    /// <summary>GET api/content/agent/schedule-plan?brand=CODE&amp;days=7&amp;maxPerDay=1 — chỉ đọc.</summary>
    [HttpGet("schedule-plan")]
    public Task<ActionResult<ContentSchedulePlanDto>> SchedulePlan(
        [FromQuery] string brand,
        [FromQuery] int days = 7,
        [FromQuery] int maxPerDay = 1,
        CancellationToken cancellationToken = default)
        => Guard<ContentSchedulePlanDto>(brand, () => _advisor.PlanAsync(brand, days, maxPerDay, cancellationToken));

    /// <summary>POST api/content/agent/proposals/generate?brand=CODE — lưu đề xuất (Proposed). Không đăng.</summary>
    [HttpPost("proposals/generate")]
    public Task<ActionResult<ContentScheduleGenerateResultDto>> Generate(
        [FromQuery] string brand,
        [FromQuery] int days = 7,
        [FromQuery] int maxPerDay = 1,
        CancellationToken cancellationToken = default)
        => Guard<ContentScheduleGenerateResultDto>(brand, () => _proposals.GenerateAsync(brand, days, maxPerDay, Actor, cancellationToken));

    [HttpGet("proposals")]
    public async Task<ActionResult<IReadOnlyList<ContentScheduleProposalRecordDto>>> List(
        [FromQuery] string? brand,
        [FromQuery] string? status,
        CancellationToken cancellationToken = default)
        => Ok(await _proposals.ListAsync(brand, status, cancellationToken));

    /// <summary>Duyệt = hành động của người. Tạo publish_job (đúng giờ đề xuất hoặc giờ bạn chọn).</summary>
    [HttpPost("proposals/{id:guid}/approve")]
    public Task<ActionResult<ContentScheduleProposalRecordDto>> Approve(
        Guid id,
        [FromBody] ContentScheduleApproveRequest? body,
        CancellationToken cancellationToken = default)
        => Guard<ContentScheduleProposalRecordDto>("x", () => _proposals.ApproveAsync(id, body?.PublishAt, Actor, cancellationToken));

    [HttpPost("proposals/{id:guid}/reject")]
    public Task<ActionResult<ContentScheduleProposalRecordDto>> Reject(
        Guid id,
        [FromBody] ContentScheduleDecisionRequest? body,
        CancellationToken cancellationToken = default)
        => Guard<ContentScheduleProposalRecordDto>("x", () => _proposals.RejectAsync(id, body?.Note, Actor, cancellationToken));

    /// <summary>Huỷ đề xuất đã duyệt khi job đăng chưa chạy.</summary>
    [HttpPost("proposals/{id:guid}/cancel")]
    public Task<ActionResult<ContentScheduleProposalRecordDto>> Cancel(
        Guid id,
        [FromBody] ContentScheduleDecisionRequest? body,
        CancellationToken cancellationToken = default)
        => Guard<ContentScheduleProposalRecordDto>("x", () => _proposals.CancelAsync(id, body?.Note, Actor, cancellationToken));

    /// <summary>
    /// Đăng lại lượt đã duyệt có job lỗi, job kẹt hoặc job còn chờ: job về hàng chờ với giờ đăng = bây giờ.
    /// Job đã đăng xong thì từ chối để tránh trùng bài.
    /// </summary>
    [HttpPost("proposals/{id:guid}/retry")]
    public Task<ActionResult<ContentScheduleProposalRecordDto>> Retry(
        Guid id,
        CancellationToken cancellationToken = default)
        => Guard<ContentScheduleProposalRecordDto>("x", () => _proposals.RetryAsync(id, Actor, cancellationToken));

    /// <summary>Đánh dấu lượt đăng tay (Fanpage chưa có API Meta) là đã đăng. Body: { "note": "link bài (không bắt buộc)" }.</summary>
    [HttpPost("proposals/{id:guid}/posted")]
    public Task<ActionResult<ContentScheduleProposalRecordDto>> Posted(
        Guid id,
        [FromBody] ContentScheduleDecisionRequest? body,
        CancellationToken cancellationToken = default)
        => Guard<ContentScheduleProposalRecordDto>("x", () => _proposals.MarkPostedAsync(id, body?.Note, Actor, cancellationToken));

    /// <summary>POST api/content/agent/platform-sync?brandId= — đọc lịch Fanpage và YouTube, không sửa bài trên nền tảng.</summary>
    [HttpPost("platform-sync")]
    public Task<ActionResult<ContentPlatformSyncReportDto>> PlatformSync(
        [FromQuery] Guid brandId,
        CancellationToken cancellationToken = default)
        => Guard<ContentPlatformSyncReportDto>(brandId == Guid.Empty ? "" : "x", () => _platform.SyncBrandAsync(brandId, cancellationToken));

    /// <summary>GET api/content/agent/platform-posts?brandId= — bài đã đọc từ nền tảng. unmatchedOnly mặc định.</summary>
    [HttpGet("platform-posts")]
    public Task<ActionResult<IReadOnlyList<ContentPlatformPostDto>>> PlatformPosts(
        [FromQuery] Guid brandId,
        [FromQuery] bool unmatchedOnly = true,
        CancellationToken cancellationToken = default)
        => Guard<IReadOnlyList<ContentPlatformPostDto>>(brandId == Guid.Empty ? "" : "x", () => _platform.ListAsync(brandId, unmatchedOnly, cancellationToken));

    /// <summary>PUT api/content/agent/platform-posts/{id}/match — ghép tay với topic. topicId null = bỏ ghép.</summary>
    [HttpPut("platform-posts/{id:guid}/match")]
    public Task<ActionResult<ContentPlatformPostDto>> MatchPlatformPost(
        Guid id,
        [FromBody] ContentPlatformMatchRequest? body,
        CancellationToken cancellationToken = default)
        => Guard<ContentPlatformPostDto>("x", () => _platform.MatchAsync(id, body?.TopicId, cancellationToken));

    /// <summary>POST api/content/agent/platform-posts/manual — lịch hẹn Business Suite mà Graph không trả. Không đăng bài.</summary>
    [HttpPost("platform-posts/manual")]
    public Task<ActionResult<ContentPlatformPostDto>> MarkPlatformOutside(
        [FromBody] ContentPlatformManualScheduleRequest? body,
        CancellationToken cancellationToken = default)
        => Guard<ContentPlatformPostDto>(
            body is null || body.TopicId == Guid.Empty ? "" : "x",
            () => _platform.MarkOutsideAsync(body!.TopicId, body.ChannelId, body.PublishAt, body.Kind, cancellationToken));

    private async Task<ActionResult<T>> Guard<T>(string brandOrDummy, Func<Task<T>> run)
    {
        if (string.IsNullOrWhiteSpace(brandOrDummy))
            return BadRequest(new { message = "Thiếu tham số brand (mã, tên hoặc Guid thương hiệu)." });
        try
        {
            return Ok(await run());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
