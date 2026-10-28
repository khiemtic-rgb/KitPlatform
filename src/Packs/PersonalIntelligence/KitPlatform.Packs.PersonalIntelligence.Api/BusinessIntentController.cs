using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.PersonalIntelligence.Intent;
using KitPlatform.Packs.PersonalIntelligence.Research;

namespace KitPlatform.Api.Controllers.PersonalIntelligence;

[ApiController]
[Route("api/personal-intelligence/intent")]
[Authorize(Policy = PersonalIntelligencePolicies.TaskCreate)]
[RequirePersonalIntelligenceModule]
public sealed class BusinessIntentController : ControllerBase
{
    readonly IBusinessIntentSession _session;
    readonly IResearchPublicationReader _publication;
    readonly BusinessIntentModelOptions _model;
    readonly ResearchPublicationOptions _publicationOptions;

    public BusinessIntentController(
        IBusinessIntentSession session,
        IResearchPublicationReader publication,
        IOptions<BusinessIntentModelOptions> model,
        IOptions<ResearchPublicationOptions> publicationOptions)
    {
        _session = session;
        _publication = publication;
        _model = model.Value;
        _publicationOptions = publicationOptions.Value;
    }

    [HttpGet("status")]
    public async Task<ActionResult<BusinessIntentRuntimeStatus>> Status(CancellationToken cancellationToken)
    {
        if (!CoreAuthority.TryResolveCaller(User, out _))
            return Unauthorized();
        var read = await _publication.ReadAsync(cancellationToken);
        return Ok(new BusinessIntentRuntimeStatus
        {
            ModelConfigured = _model.IsConfigured,
            ModelName = _model.IsConfigured ? _model.Model.Trim() : "",
            PublicationConfigured = !string.IsNullOrWhiteSpace(_publicationOptions.Root),
            PublicationAvailability = read.Availability,
            PublicationId = read.PublicationId,
            SourceLabel = _publicationOptions.SourceLabel.Trim(),
        });
    }

    [HttpPost]
    public async Task<ActionResult<BusinessIntentEvidence>> Post(
        [FromBody] BusinessIntentRequest? request,
        CancellationToken cancellationToken)
    {
        if (!CoreAuthority.TryResolveCaller(User, out var caller))
            return Unauthorized();
        if (request?.TenantId is Guid requested && requested != caller.TenantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Tenant context cannot switch tenant.",
            });
        }

        var permissions = User.FindAll(CoreAuthority.PermissionClaim)
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.Ordinal);
        var businessCaller = new BusinessCaller(
            caller.UserId,
            caller.TenantId,
            User.IsInRole("ADMIN"),
            permissions);
        return Ok(await _session.HandleAsync(request?.Utterance, businessCaller, cancellationToken));
    }
}

public sealed class BusinessIntentRequest
{
    public string? Utterance { get; set; }

    public Guid? TenantId { get; set; }
}

public sealed class BusinessIntentRuntimeStatus
{
    public bool ModelConfigured { get; init; }

    public string ModelName { get; init; } = "";

    public bool PublicationConfigured { get; init; }

    public string PublicationAvailability { get; init; } = "";

    public string? PublicationId { get; init; }

    public string SourceLabel { get; init; } = "";
}
