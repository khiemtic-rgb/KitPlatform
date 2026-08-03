using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os/families/{familyId:guid}/morning-note")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
public sealed class FamilyOsMorningNoteController : ControllerBase
{
    private readonly IFamilyMorningNoteService _notes;

    public FamilyOsMorningNoteController(IFamilyMorningNoteService notes) => _notes = notes;

    [HttpGet]
    [ProducesResponseType(typeof(FamilyMorningNoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyMorningNoteDto>> Get(
        Guid familyId,
        [FromQuery] Guid? memberId,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        try
        {
            var note = await _notes.GetMorningNoteAsync(familyId, memberId, date, cancellationToken);
            return Ok(note);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
