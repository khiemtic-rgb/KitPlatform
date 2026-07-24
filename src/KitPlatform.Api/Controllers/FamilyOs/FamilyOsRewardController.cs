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

public sealed class FamilyOsRewardController : ControllerBase

{

    private readonly IFamilyRewardService _rewards;



    public FamilyOsRewardController(IFamilyRewardService rewards) => _rewards = rewards;



    [HttpGet("reward-catalog")]

    [ProducesResponseType(typeof(IReadOnlyList<RewardCatalogItemDto>), StatusCodes.Status200OK)]

    [ProducesResponseType(StatusCodes.Status400BadRequest)]

    public async Task<ActionResult<IReadOnlyList<RewardCatalogItemDto>>> GetCatalog(

        Guid familyId,

        CancellationToken cancellationToken)

    {

        try

        {

            return Ok(await _rewards.GetCatalogAsync(familyId, cancellationToken));

        }

        catch (InvalidOperationException ex)

        {

            return BadRequest(new { code = "validation_error", message = ex.Message });

        }

    }



    [HttpPost("reward-catalog")]

    [ProducesResponseType(typeof(RewardCatalogItemDto), StatusCodes.Status201Created)]

    [ProducesResponseType(StatusCodes.Status400BadRequest)]

    public async Task<ActionResult<RewardCatalogItemDto>> CreateCatalogItem(

        Guid familyId,

        [FromBody] UpsertRewardCatalogRequest request,

        CancellationToken cancellationToken)

    {

        try

        {

            var item = await _rewards.CreateCatalogItemAsync(familyId, request, cancellationToken);

            return CreatedAtAction(nameof(GetCatalog), new { familyId }, item);

        }

        catch (InvalidOperationException ex)

        {

            return BadRequest(new { code = "validation_error", message = ex.Message });

        }

    }



    [HttpPatch("reward-catalog/{catalogId:guid}")]

    [ProducesResponseType(typeof(RewardCatalogItemDto), StatusCodes.Status200OK)]

    [ProducesResponseType(StatusCodes.Status400BadRequest)]

    public async Task<ActionResult<RewardCatalogItemDto>> UpdateCatalogItem(

        Guid familyId,

        Guid catalogId,

        [FromBody] UpsertRewardCatalogRequest request,

        CancellationToken cancellationToken)

    {

        try

        {

            return Ok(await _rewards.UpdateCatalogItemAsync(familyId, catalogId, request, cancellationToken));

        }

        catch (InvalidOperationException ex)

        {

            return BadRequest(new { code = "validation_error", message = ex.Message });

        }

    }



    [HttpDelete("reward-catalog/{catalogId:guid}")]

    [ProducesResponseType(StatusCodes.Status204NoContent)]

    [ProducesResponseType(StatusCodes.Status400BadRequest)]

    public async Task<IActionResult> DeactivateCatalogItem(

        Guid familyId,

        Guid catalogId,

        CancellationToken cancellationToken)

    {

        try

        {

            await _rewards.DeactivateCatalogItemAsync(familyId, catalogId, cancellationToken);

            return NoContent();

        }

        catch (InvalidOperationException ex)

        {

            return BadRequest(new { code = "validation_error", message = ex.Message });

        }

    }



    [HttpGet("members/{memberId:guid}/reward-redemptions")]

    [ProducesResponseType(typeof(IReadOnlyList<RewardRedemptionDto>), StatusCodes.Status200OK)]

    [ProducesResponseType(StatusCodes.Status400BadRequest)]

    public async Task<ActionResult<IReadOnlyList<RewardRedemptionDto>>> ListRedemptions(

        Guid familyId,

        Guid memberId,

        CancellationToken cancellationToken)

    {

        try

        {

            return Ok(await _rewards.ListRedemptionsAsync(familyId, memberId, cancellationToken));

        }

        catch (InvalidOperationException ex)

        {

            return BadRequest(new { code = "validation_error", message = ex.Message });

        }

    }



    [HttpGet("reward-redemptions")]

    [ProducesResponseType(typeof(IReadOnlyList<RewardRedemptionDto>), StatusCodes.Status200OK)]

    [ProducesResponseType(StatusCodes.Status400BadRequest)]

    public async Task<ActionResult<IReadOnlyList<RewardRedemptionDto>>> ListAllRedemptions(

        Guid familyId,

        CancellationToken cancellationToken)

    {

        try

        {

            return Ok(await _rewards.ListRedemptionsAsync(familyId, null, cancellationToken));

        }

        catch (InvalidOperationException ex)

        {

            return BadRequest(new { code = "validation_error", message = ex.Message });

        }

    }



    [HttpPost("members/{memberId:guid}/reward-redeem")]

    [ProducesResponseType(typeof(RewardRedeemResultDto), StatusCodes.Status200OK)]

    [ProducesResponseType(StatusCodes.Status400BadRequest)]

    public async Task<ActionResult<RewardRedeemResultDto>> Redeem(

        Guid familyId,

        Guid memberId,

        [FromBody] RewardRedeemRequest request,

        CancellationToken cancellationToken)

    {

        try

        {

            return Ok(await _rewards.RedeemAsync(familyId, memberId, request, cancellationToken));

        }

        catch (InvalidOperationException ex)

        {

            return BadRequest(new { code = "validation_error", message = ex.Message });

        }

    }



    [HttpPost("reward-redemptions/{redemptionId:guid}/fulfill")]

    [ProducesResponseType(typeof(RewardRedemptionDto), StatusCodes.Status200OK)]

    [ProducesResponseType(StatusCodes.Status400BadRequest)]

    public async Task<ActionResult<RewardRedemptionDto>> Fulfill(

        Guid familyId,

        Guid redemptionId,

        [FromBody] RewardFulfillRequest request,

        CancellationToken cancellationToken)

    {

        try

        {

            return Ok(await _rewards.FulfillAsync(familyId, redemptionId, request, cancellationToken));

        }

        catch (InvalidOperationException ex)

        {

            return BadRequest(new { code = "validation_error", message = ex.Message });

        }

    }

}

