using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Core;
using KitPlatform.Application.Learning;

namespace KitPlatform.Api.Controllers.Learning;

[ApiController]
[Authorize]
[Route("api/learning")]
[RequirePlatformModule(PlatformModuleCodes.Learning)]
public sealed class LearningController : ControllerBase
{
    private readonly ILearningCatalogService _catalog;
    private readonly ILearningProgressService _progress;

    public LearningController(
        ILearningCatalogService catalog,
        ILearningProgressService progress)
    {
        _catalog = catalog;
        _progress = progress;
    }

    [HttpGet("programs")]
    [Authorize(Policy = LearningPolicies.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<LearningProgramListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LearningProgramListItemDto>>> ListPrograms(
        CancellationToken cancellationToken) =>
        Ok(await _catalog.ListProgramsAsync(cancellationToken));

    [HttpGet("programs/{id:guid}")]
    [Authorize(Policy = LearningPolicies.Read)]
    [ProducesResponseType(typeof(LearningProgramDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LearningProgramDetailDto>> GetProgram(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await _catalog.GetProgramAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("modules/{id:guid}")]
    [Authorize(Policy = LearningPolicies.Read)]
    [ProducesResponseType(typeof(LearningModuleDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LearningModuleDetailDto>> GetModule(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await _catalog.GetModuleAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPut("modules/{id:guid}/tenant-content")]
    [Authorize(Policy = LearningPolicies.Write)]
    [ProducesResponseType(typeof(LearningModuleDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LearningModuleDetailDto>> UpsertModuleTenantContent(
        Guid id,
        [FromBody] UpsertLearningModuleTenantContentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _catalog.UpsertModuleTenantContentAsync(id, request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("modules/{id:guid}/tenant-content")]
    [Authorize(Policy = LearningPolicies.Write)]
    [ProducesResponseType(typeof(LearningModuleDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LearningModuleDetailDto>> RevertModuleTenantContent(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _catalog.RevertModuleTenantContentAsync(id, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("me")]
    [Authorize(Policy = LearningPolicies.Read)]
    [ProducesResponseType(typeof(LearningMyLearningDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LearningMyLearningDto>> GetMyLearning(
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _progress.GetMyLearningAsync(cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("me/enroll/{programId:guid}")]
    [Authorize(Policy = LearningPolicies.Read)]
    [ProducesResponseType(typeof(LearningEnrollmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LearningEnrollmentDto>> EnrollMe(
        Guid programId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _progress.EnsureMyEnrollmentAsync(programId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("enrollments")]
    [Authorize(Policy = LearningPolicies.Write)]
    [ProducesResponseType(typeof(IReadOnlyList<LearningEnrollmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LearningEnrollmentDto>>> ListEnrollments(
        CancellationToken cancellationToken) =>
        Ok(await _progress.ListEnrollmentsAsync(cancellationToken));

    [HttpPost("enrollments")]
    [Authorize(Policy = LearningPolicies.Write)]
    [ProducesResponseType(typeof(LearningEnrollmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LearningEnrollmentDto>> Assign(
        [FromBody] AssignLearningProgramRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _progress.AssignAsync(request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("roster")]
    [Authorize(Policy = LearningPolicies.Write)]
    [ProducesResponseType(typeof(IReadOnlyList<LearningCompetencyRosterItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LearningCompetencyRosterItemDto>>> Roster(
        CancellationToken cancellationToken) =>
        Ok(await _progress.ListCompetencyRosterAsync(cancellationToken));

    [HttpGet("gates/check")]
    [Authorize(Policy = LearningPolicies.Read)]
    [ProducesResponseType(typeof(LearningGateCheckDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LearningGateCheckDto>> CheckGate(
        [FromQuery] string permission,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(permission))
            return BadRequest(new { message = "Thiếu permission." });
        try
        {
            return Ok(await _progress.CheckGateAsync(permission, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("gates/overrides")]
    [Authorize(Policy = LearningPolicies.Write)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CreateOverride(
        [FromBody] CreateLearningGateOverrideRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _progress.CreateGateOverrideAsync(request, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("evaluations")]
    [Authorize(Policy = LearningPolicies.Write)]
    [ProducesResponseType(typeof(IReadOnlyList<LearningEvaluationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LearningEvaluationDto>>> ListEvaluations(
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken cancellationToken) =>
        Ok(await _progress.ListEvaluationsAsync(year, month, cancellationToken));

    [HttpPost("evaluations")]
    [Authorize(Policy = LearningPolicies.Write)]
    [ProducesResponseType(typeof(LearningEvaluationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LearningEvaluationDto>> UpsertEvaluation(
        [FromBody] UpsertLearningEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _progress.UpsertEvaluationAsync(request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("recognitions")]
    [Authorize(Policy = LearningPolicies.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<LearningRecognitionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LearningRecognitionDto>>> ListRecognitions(
        [FromQuery] int take = 30,
        [FromQuery] string? scope = null,
        CancellationToken cancellationToken = default)
    {
        var mineOnly = string.Equals(scope, "me", StringComparison.OrdinalIgnoreCase)
            || string.Equals(scope, "mine", StringComparison.OrdinalIgnoreCase);
        try
        {
            return Ok(await _progress.ListRecognitionsAsync(take, mineOnly, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("customer-feedback/recent")]
    [Authorize(Policy = LearningPolicies.Write)]
    [ProducesResponseType(typeof(IReadOnlyList<LearningCustomerFeedbackDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LearningCustomerFeedbackDto>>> ListRecentCustomerFeedback(
        [FromQuery] int hours = 48,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await _progress.ListRecentCustomerFeedbackAsync(hours, take, cancellationToken));

    [HttpPost("recognitions")]
    [Authorize(Policy = LearningPolicies.Write)]
    [ProducesResponseType(typeof(LearningRecognitionDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LearningRecognitionDto>> CreateRecognition(
        [FromBody] CreateLearningRecognitionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _progress.CreateRecognitionAsync(request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("me/badges")]
    [Authorize(Policy = LearningPolicies.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<LearningBadgeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LearningBadgeDto>>> MyBadges(
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _progress.ListMyBadgesAsync(cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("me/evaluations")]
    [Authorize(Policy = LearningPolicies.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<LearningEvaluationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LearningEvaluationDto>>> MyEvaluations(
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _progress.ListMyEvaluationsAsync(cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("evaluations/{id:guid}/my-feedback")]
    [Authorize(Policy = LearningPolicies.Read)]
    [ProducesResponseType(typeof(LearningEvaluationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LearningEvaluationDto>> SubmitMyEvaluationFeedback(
        Guid id,
        [FromBody] SubmitLearningEvaluationFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _progress.SubmitMyEvaluationFeedbackAsync(id, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("me/habits")]
    [Authorize(Policy = LearningPolicies.Read)]
    [ProducesResponseType(typeof(LearningMyHabitsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LearningMyHabitsDto>> MyHabits(
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _progress.GetMyHabitsAsync(cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("career/levels")]
    [Authorize(Policy = LearningPolicies.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<LearningCareerLevelDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LearningCareerLevelDto>>> ListCareerLevels(
        CancellationToken cancellationToken) =>
        Ok(await _progress.ListCareerLevelsAsync(cancellationToken));

    [HttpGet("career/roster")]
    [Authorize(Policy = LearningPolicies.Write)]
    [ProducesResponseType(typeof(IReadOnlyList<LearningCareerRosterItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LearningCareerRosterItemDto>>> ListCareerRoster(
        CancellationToken cancellationToken) =>
        Ok(await _progress.ListCareerRosterAsync(cancellationToken));

    [HttpGet("career/promotions")]
    [Authorize(Policy = LearningPolicies.Write)]
    [ProducesResponseType(typeof(IReadOnlyList<LearningCareerPromotionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LearningCareerPromotionDto>>> ListCareerPromotions(
        [FromQuery] int take = 30,
        CancellationToken cancellationToken = default) =>
        Ok(await _progress.ListCareerPromotionsAsync(take, cancellationToken));

    [HttpPost("career/promotions")]
    [Authorize(Policy = LearningPolicies.Write)]
    [ProducesResponseType(typeof(LearningCareerPromotionDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LearningCareerPromotionDto>> PromoteCareer(
        [FromBody] PromoteLearningCareerRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _progress.PromoteCareerAsync(request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("people/dashboard")]
    [Authorize(Policy = LearningPolicies.Read)]
    [ProducesResponseType(typeof(LearningPeopleDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LearningPeopleDashboardDto>> PeopleDashboard(
        CancellationToken cancellationToken) =>
        Ok(await _progress.GetPeopleDashboardAsync(cancellationToken));

    [HttpGet("employees/{employeeId:guid}/evidence")]
    [Authorize(Policy = LearningPolicies.Write)]
    [ProducesResponseType(typeof(LearningEmployeeEvidenceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LearningEmployeeEvidenceDto>> EmployeeEvidence(
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var item = await _progress.GetEmployeeEvidenceAsync(employeeId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("modules/{id:guid}/start")]
    [Authorize(Policy = LearningPolicies.Read)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> StartModule(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _progress.StartModuleAsync(id, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("modules/{id:guid}/acknowledge")]
    [Authorize(Policy = LearningPolicies.Read)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Acknowledge(
        Guid id,
        [FromBody] AcknowledgeLearningModuleRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _progress.AcknowledgeModuleAsync(id, request?.SelfieUrl, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("upload-ack-selfie")]
    [Authorize(Policy = LearningPolicies.Read)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 5 * 1024 * 1024)]
    [ProducesResponseType(typeof(KitPlatform.Api.Controllers.UploadFileResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<KitPlatform.Api.Controllers.UploadFileResult>> UploadAckSelfie(
        IFormFile file,
        [FromServices] IWebHostEnvironment environment,
        [FromServices] ITenantContext tenant,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = await SaveLearningSelfieAsync(file, environment, tenant, cancellationToken);
            return Ok(new KitPlatform.Api.Controllers.UploadFileResult(url));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<string> SaveLearningSelfieAsync(
        IFormFile file,
        IWebHostEnvironment environment,
        ITenantContext tenant,
        CancellationToken cancellationToken)
    {
        if (file.Length <= 0) throw new InvalidOperationException("File trống.");
        if (file.Length > 5 * 1024 * 1024) throw new InvalidOperationException("Ảnh tối đa 5MB.");
        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext)
            || !new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" }
                .Contains(ext))
            throw new InvalidOperationException("Chỉ hỗ trợ JPG, PNG, WebP.");

        var folder = Path.Combine(
            environment.ContentRootPath,
            "uploads",
            "learning-ack",
            tenant.TenantId.ToString("N"));
        Directory.CreateDirectory(folder);
        var name = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var path = Path.Combine(folder, name);
        await using (var stream = System.IO.File.Create(path))
            await file.CopyToAsync(stream, cancellationToken);
        return $"/uploads/learning-ack/{tenant.TenantId:N}/{name}";
    }

    [HttpPost("modules/{id:guid}/quiz")]
    [Authorize(Policy = LearningPolicies.Read)]
    [ProducesResponseType(typeof(SubmitLearningQuizResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SubmitLearningQuizResultDto>> SubmitQuiz(
        Guid id,
        [FromBody] SubmitLearningQuizRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _progress.SubmitQuizAsync(id, request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("me/monthly-drill")]
    [Authorize(Policy = LearningPolicies.Read)]
    [ProducesResponseType(typeof(LearningMonthlyDrillStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LearningMonthlyDrillStatusDto>> GetMonthlyDrill(
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _progress.GetMonthlyDrillStatusAsync(cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("me/monthly-drill/start")]
    [Authorize(Policy = LearningPolicies.Read)]
    [ProducesResponseType(typeof(LearningMonthlyDrillStartDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LearningMonthlyDrillStartDto>> StartMonthlyDrill(
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _progress.StartMonthlyDrillAsync(cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("me/monthly-drill/submit")]
    [Authorize(Policy = LearningPolicies.Read)]
    [ProducesResponseType(typeof(LearningMonthlyDrillResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LearningMonthlyDrillResultDto>> SubmitMonthlyDrill(
        [FromBody] SubmitLearningMonthlyDrillRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _progress.SubmitMonthlyDrillAsync(request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("observations/pending")]
    [Authorize(Policy = LearningPolicies.Write)]
    [ProducesResponseType(typeof(IReadOnlyList<LearningObservationPendingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LearningObservationPendingDto>>> ListPendingObservations(
        CancellationToken cancellationToken) =>
        Ok(await _progress.ListPendingObservationsAsync(cancellationToken));

    [HttpPost("observations")]
    [Authorize(Policy = LearningPolicies.Write)]
    [ProducesResponseType(typeof(LearningObservationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LearningObservationDto>> SubmitObservation(
        [FromBody] SubmitLearningObservationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _progress.SubmitObservationAsync(request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("modules/{id:guid}/my-observation")]
    [Authorize(Policy = LearningPolicies.Read)]
    [ProducesResponseType(typeof(LearningObservationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LearningObservationDto>> GetMyModuleObservation(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _progress.GetMyModuleObservationAsync(id, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("mail/threads")]
    [Authorize(Policy = LearningPolicies.Write)]
    [ProducesResponseType(typeof(CreateLearningMailThreadsResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CreateLearningMailThreadsResultDto>> CreateMailThread(
        [FromBody] CreateLearningMailThreadRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _progress.CreateMailThreadAsync(request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("mail/threads")]
    [Authorize(Policy = LearningPolicies.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<LearningMailThreadListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LearningMailThreadListItemDto>>> ListMailThreads(
        CancellationToken cancellationToken) =>
        Ok(await _progress.ListMailThreadsAsync(cancellationToken));

    [HttpGet("mail/threads/{id:guid}")]
    [Authorize(Policy = LearningPolicies.Read)]
    [ProducesResponseType(typeof(LearningMailThreadDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LearningMailThreadDetailDto>> GetMailThread(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await _progress.GetMailThreadAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("mail/threads/{id:guid}/messages")]
    [Authorize(Policy = LearningPolicies.Read)]
    [ProducesResponseType(typeof(LearningMailMessageDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LearningMailMessageDto>> ReplyMailThread(
        Guid id,
        [FromBody] ReplyLearningMailRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _progress.ReplyMailThreadAsync(id, request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("mail/threads/{id:guid}/read")]
    [Authorize(Policy = LearningPolicies.Read)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkMailThreadRead(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            await _progress.MarkMailThreadReadAsync(id, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("mail/unread-count")]
    [Authorize(Policy = LearningPolicies.Read)]
    [ProducesResponseType(typeof(LearningMailUnreadCountDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LearningMailUnreadCountDto>> GetMailUnreadCount(
        CancellationToken cancellationToken) =>
        Ok(await _progress.GetMailUnreadCountAsync(cancellationToken));
}
