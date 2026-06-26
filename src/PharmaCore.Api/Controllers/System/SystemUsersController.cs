using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Authorization;
using PharmaCore.Application.Identity;

namespace PharmaCore.Api.Controllers.IdentityAdmin;

[ApiController]
[Authorize]
[Route("api/system/users")]
public sealed class SystemUsersController : ControllerBase
{
    private readonly IIdentityAdminService _identity;

    public SystemUsersController(IIdentityAdminService identity) => _identity = identity;

    [HttpGet]
    [Authorize(Policy = IdentityPolicies.Read)]
    public async Task<ActionResult<PagedUsersResult>> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await _identity.ListUsersAsync(search, page, pageSize, cancellationToken));

    [HttpGet("{userId:guid}")]
    [Authorize(Policy = IdentityPolicies.Read)]
    public async Task<ActionResult<UserDetailDto>> Get(Guid userId, CancellationToken cancellationToken)
    {
        var item = await _identity.GetUserAsync(userId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = IdentityPolicies.Write)]
    public async Task<ActionResult<UserDetailDto>> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _identity.CreateUserAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { userId = item.Id }, item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{userId:guid}")]
    [Authorize(Policy = IdentityPolicies.Write)]
    public async Task<ActionResult<UserDetailDto>> Update(
        Guid userId,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _identity.UpdateUserAsync(userId, request, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
