using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os/families/{familyId:guid}/agreements")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
public sealed class FamilyOsAgreementsController : ControllerBase
{
    private readonly IFamilyAgreementService _agreements;

    public FamilyOsAgreementsController(IFamilyAgreementService agreements) =>
        _agreements = agreements;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FamilyAgreementDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FamilyAgreementDto>>> List(
        Guid familyId,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _agreements.ListAsync(familyId, status, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpGet("~/api/family-os/consequence-library")]
    [ProducesResponseType(typeof(IReadOnlyList<FamilyConsequenceLibrary.Item>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<FamilyConsequenceLibrary.Item>> ConsequenceLibrary() =>
        Ok(_agreements.ListConsequenceLibrary());

    [HttpGet("{agreementId:guid}")]
    [ProducesResponseType(typeof(FamilyAgreementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FamilyAgreementDto>> Get(
        Guid familyId,
        Guid agreementId,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _agreements.GetAsync(familyId, agreementId, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(FamilyAgreementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyAgreementDto>> Create(
        Guid familyId,
        [FromBody] CreateFamilyAgreementRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await _agreements.CreateAsync(familyId, request, cancellationToken);
            return Created(
                $"api/family-os/families/{familyId}/agreements/{created.Id}",
                created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("{agreementId:guid}/decide")]
    [ProducesResponseType(typeof(FamilyAgreementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyAgreementDto>> Decide(
        Guid familyId,
        Guid agreementId,
        [FromBody] DecideFamilyAgreementRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _agreements.DecideAsync(
                familyId, agreementId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
