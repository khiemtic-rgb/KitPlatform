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

    public ContentAgentController(IContentScheduleAdvisor advisor, IContentScheduleProposalService proposals)
    {
        _advisor = advisor;
        _proposals = proposals;
    }

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
