using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Abstractions;
using KitPlatform.Packs.Pharmacy.Knowledge;

namespace KitPlatform.Api.Controllers.Pharmacy;

[ApiController]
[Authorize]
[Route("api/platform/pharmacy-knowledge")]
public sealed class PharmacyKnowledgePublicationController : ControllerBase
{
    private readonly IPharmacyKnowledgePublicationService _publication;
    private readonly ITenantContext _tenant;

    public PharmacyKnowledgePublicationController(
        IPharmacyKnowledgePublicationService publication,
        ITenantContext tenant)
    {
        _publication = publication;
        _tenant = tenant;
    }

    [HttpGet("versions")]
    [Authorize(Policy = PharmacyKnowledgePolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<PharmacyKnowledgeVersionDto>>> List(
        CancellationToken cancellationToken) =>
        Ok(await _publication.ListVersionsAsync(cancellationToken));

    [HttpGet("versions/{ruleCode}/{version}")]
    [Authorize(Policy = PharmacyKnowledgePolicies.Read)]
    public async Task<ActionResult<PharmacyKnowledgeVersionDto>> Get(
        string ruleCode,
        string version,
        CancellationToken cancellationToken)
    {
        var row = await _publication.GetVersionAsync(ruleCode, version, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpGet("history")]
    [Authorize(Policy = PharmacyKnowledgePolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<PharmacyKnowledgePublicationEventDto>>> History(
        [FromQuery] string? ruleCode,
        CancellationToken cancellationToken) =>
        Ok(await _publication.ListHistoryAsync(ruleCode, cancellationToken));

    [HttpPost("versions/{ruleCode}/{version}/publish")]
    [Authorize(Policy = PharmacyKnowledgePolicies.Publish)]
    public Task<ActionResult<PharmacyKnowledgeVersionDto>> Publish(
        string ruleCode,
        string version,
        CancellationToken cancellationToken) =>
        Change(ruleCode, version, published: true, cancellationToken);

    [HttpPost("versions/{ruleCode}/{version}/retire")]
    [Authorize(Policy = PharmacyKnowledgePolicies.Publish)]
    public Task<ActionResult<PharmacyKnowledgeVersionDto>> Retire(
        string ruleCode,
        string version,
        CancellationToken cancellationToken) =>
        Change(ruleCode, version, published: false, cancellationToken);

    [HttpPost("versions/{ruleCode}/{version}/submit-review")]
    [Authorize(Policy = PharmacyKnowledgePolicies.Review)]
    public Task<ActionResult<PharmacyKnowledgeVersionDto>> SubmitReview(
        string ruleCode,
        string version,
        CancellationToken cancellationToken) =>
        Act(() => _publication.SubmitReviewAsync(ruleCode, version, _tenant.UserId, cancellationToken));

    [HttpPost("versions/{ruleCode}/{version}/return-draft")]
    [Authorize(Policy = PharmacyKnowledgePolicies.Review)]
    public Task<ActionResult<PharmacyKnowledgeVersionDto>> ReturnToDraft(
        string ruleCode,
        string version,
        CancellationToken cancellationToken) =>
        Act(() => _publication.ReturnToDraftAsync(ruleCode, version, _tenant.UserId, cancellationToken));

    [HttpPut("versions/{ruleCode}/{version}/source")]
    [Authorize(Policy = PharmacyKnowledgePolicies.VerifySource)]
    public Task<ActionResult<PharmacyKnowledgeVersionDto>> VerifySource(
        string ruleCode,
        string version,
        [FromBody] PharmacyKnowledgeSourceVerificationRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return Task.FromResult<ActionResult<PharmacyKnowledgeVersionDto>>(
                new BadRequestObjectResult(new { message = "Thiếu nội dung nguồn." }));
        return Act(() => _publication.VerifySourceAsync(ruleCode, version, _tenant.UserId, request, cancellationToken));
    }

    [HttpPut("versions/{ruleCode}/{version}/content")]
    [Authorize(Policy = PharmacyKnowledgePolicies.Review)]
    public Task<ActionResult<PharmacyKnowledgeVersionDto>> UpdateContent(
        string ruleCode,
        string version,
        [FromBody] PharmacyKnowledgeContentUpdateRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return Task.FromResult<ActionResult<PharmacyKnowledgeVersionDto>>(
                new BadRequestObjectResult(new { message = "Thiếu nội dung phiên bản." }));
        return Act(() => _publication.UpdateUnpublishedContentAsync(ruleCode, version, request, cancellationToken));
    }

    private async Task<ActionResult<PharmacyKnowledgeVersionDto>> Change(
        string ruleCode,
        string version,
        bool published,
        CancellationToken cancellationToken)
    {
        try
        {
            var row = published
                ? await _publication.PublishAsync(ruleCode, version, _tenant.UserId, cancellationToken)
                : await _publication.RetireAsync(ruleCode, version, _tenant.UserId, cancellationToken);
            return Ok(row);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private async Task<ActionResult<PharmacyKnowledgeVersionDto>> Act(
        Func<Task<PharmacyKnowledgeVersionDto>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
