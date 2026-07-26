using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os/families/{familyId:guid}")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
public sealed class FamilyOsAfeController : ControllerBase
{
    private readonly IFamilyChildRequestService _requests;
    private readonly IFamilyAiProposalService _proposals;
    private readonly IFamilyDecisionInboxService _inbox;
    private readonly IFamilyModeService _modes;
    private readonly IFamilyScreenWalletService _wallet;
    private readonly IFamilyScoreService _score;

    public FamilyOsAfeController(
        IFamilyChildRequestService requests,
        IFamilyAiProposalService proposals,
        IFamilyDecisionInboxService inbox,
        IFamilyModeService modes,
        IFamilyScreenWalletService wallet,
        IFamilyScoreService score)
    {
        _requests = requests;
        _proposals = proposals;
        _inbox = inbox;
        _modes = modes;
        _wallet = wallet;
        _score = score;
    }

    [HttpGet("decision-inbox")]
    [ProducesResponseType(typeof(FamilyDecisionInboxDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FamilyDecisionInboxDto>> DecisionInbox(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _inbox.GetInboxAsync(familyId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpGet("requests")]
    [ProducesResponseType(typeof(IReadOnlyList<FamilyChildRequestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FamilyChildRequestDto>>> ListRequests(
        Guid familyId,
        [FromQuery] string? status,
        [FromQuery] Guid? memberId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _requests.ListAsync(familyId, status, memberId, 40, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("requests")]
    [ProducesResponseType(typeof(FamilyChildRequestDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FamilyChildRequestDto>> CreateRequest(
        Guid familyId,
        [FromBody] FamilyChildRequestCreateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _requests.CreateAsync(familyId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("requests/{requestId:guid}/decide")]
    [ProducesResponseType(typeof(FamilyChildRequestDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FamilyChildRequestDto>> DecideRequest(
        Guid familyId,
        Guid requestId,
        [FromBody] FamilyChildRequestDecideRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _requests.DecideAsync(familyId, requestId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpGet("ai-proposals")]
    [ProducesResponseType(typeof(IReadOnlyList<FamilyAiProposalDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FamilyAiProposalDto>>> ListProposals(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _proposals.ListPendingAsync(familyId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("ai-proposals/{proposalId:guid}/decide")]
    [ProducesResponseType(typeof(FamilyAiProposalDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FamilyAiProposalDto>> DecideProposal(
        Guid familyId,
        Guid proposalId,
        [FromBody] FamilyAiProposalDecideRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _proposals.DecideAsync(familyId, proposalId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("ai-proposals/scan")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> ScanAdaptive(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        try
        {
            var n = await _proposals.ScanAdaptiveAsync(familyId, cancellationToken);
            return Ok(new { created = n });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("family-modes/activate")]
    [ProducesResponseType(typeof(FamilyModeActivateResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<FamilyModeActivateResult>> ActivateMode(
        Guid familyId,
        [FromBody] FamilyModeActivateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _modes.ActivateAsync(familyId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpGet("screen-wallet")]
    [ProducesResponseType(typeof(IReadOnlyList<FamilyScreenWalletDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FamilyScreenWalletDto>>> ListWallet(
        Guid familyId,
        [FromQuery] int? isoYear,
        [FromQuery] int? isoWeek,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _wallet.ListWeekAsync(familyId, isoYear, isoWeek, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("screen-wallet/propose")]
    [ProducesResponseType(typeof(FamilyScreenWalletDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FamilyScreenWalletDto>> ProposeWallet(
        Guid familyId,
        [FromBody] FamilyScreenWalletProposeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _wallet.ProposeBudgetAsync(familyId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("screen-wallet/{walletId:guid}/activate")]
    [ProducesResponseType(typeof(FamilyScreenWalletDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FamilyScreenWalletDto>> ActivateWallet(
        Guid familyId,
        Guid walletId,
        [FromQuery] Guid decidedByMemberId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _wallet.ActivateAsync(familyId, walletId, decidedByMemberId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("screen-wallet/spend")]
    [ProducesResponseType(typeof(FamilyScreenWalletDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FamilyScreenWalletDto>> SpendWallet(
        Guid familyId,
        [FromBody] FamilyScreenWalletSpendRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _wallet.SpendAsync(familyId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpGet("family-score")]
    [ProducesResponseType(typeof(FamilyScoreDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FamilyScoreDto>> FamilyScore(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _score.GetWeekScoreAsync(familyId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
