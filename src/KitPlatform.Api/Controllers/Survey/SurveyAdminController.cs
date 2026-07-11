using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Packs.Survey;

namespace KitPlatform.Api.Controllers.Survey;

[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/system/assessment")]
public sealed class SurveyAdminController : ControllerBase
{
    private readonly IAssessmentAdminService _admin;

    public SurveyAdminController(IAssessmentAdminService admin) => _admin = admin;

    [HttpGet("access")]
    [ProducesResponseType(typeof(AssessmentAdminAccessDto), StatusCodes.Status200OK)]
    public ActionResult<AssessmentAdminAccessDto> GetAccess() => Ok(_admin.GetAccess());

    [HttpGet("submissions")]
    [ProducesResponseType(typeof(AssessmentSubmissionListResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssessmentSubmissionListResultDto>> ListSubmissions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] bool? hasLead = null,
        [FromQuery] Guid? partnerId = null,
        [FromQuery] string? leadPipelineStatus = null,
        [FromQuery] bool archived = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _admin.ListSubmissionsAsync(
                new AssessmentSubmissionListQuery(page, pageSize, status, hasLead, partnerId, leadPipelineStatus, archived),
                cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
    }

    [HttpGet("submissions/{id:guid}")]
    [ProducesResponseType(typeof(AssessmentSubmissionDetailAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssessmentSubmissionDetailAdminDto>> GetSubmission(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var detail = await _admin.GetSubmissionAsync(id, cancellationToken);
            return detail is null ? NotFound() : Ok(detail);
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
    }

    [HttpGet("submissions/{id:guid}/report")]
    [ProducesResponseType(typeof(AssessmentFullReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssessmentFullReportDto>> GetSubmissionReport(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _admin.GetReportAsync(id, cancellationToken));
        }
        catch (AssessmentException ex) when (ex.StatusCode == 404)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (AssessmentException ex) when (ex.StatusCode == 403)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
    }

    [HttpGet("submissions/{id:guid}/report.pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubmissionReportPdf(
        Guid id,
        [FromQuery] string? kind,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pdfKind = KapReportPdfKindParser.Parse(kind);
            var (content, fileName, contentType) = await _admin.GetReportPdfAsync(id, pdfKind, cancellationToken);
            return File(content, contentType, fileName);
        }
        catch (AssessmentException ex) when (ex.StatusCode == 404)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (AssessmentException ex) when (ex.StatusCode == 403)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
    }

    [HttpPatch("submissions/{id:guid}/pipeline")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateLeadPipeline(
        Guid id,
        [FromBody] UpdateLeadPipelineRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var ok = await _admin.UpdateLeadPipelineAsync(id, request, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
    }

    [HttpPost("submissions/{id:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ArchiveSubmission(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var ok = await _admin.ArchiveSubmissionAsync(id, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
    }

    [HttpPost("submissions/{id:guid}/unarchive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnarchiveSubmission(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var ok = await _admin.UnarchiveSubmissionAsync(id, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
    }
}
