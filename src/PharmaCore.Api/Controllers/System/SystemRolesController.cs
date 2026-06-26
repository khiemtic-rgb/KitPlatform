using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Authorization;
using PharmaCore.Application.Identity;

namespace PharmaCore.Api.Controllers.IdentityAdmin;

[ApiController]
[Authorize]
[Route("api/system")]
public sealed class SystemRolesController : ControllerBase
{
    private readonly IIdentityAdminService _identity;

    public SystemRolesController(IIdentityAdminService identity) => _identity = identity;

    [HttpGet("roles")]
    [Authorize(Policy = IdentityPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<RoleAdminListItemDto>>> ListRoles(CancellationToken cancellationToken) =>
        Ok(await _identity.ListRolesAsync(cancellationToken));

    [HttpGet("roles/{roleId:guid}")]
    [Authorize(Policy = IdentityPolicies.Read)]
    public async Task<ActionResult<RoleDetailDto>> GetRole(Guid roleId, CancellationToken cancellationToken)
    {
        var item = await _identity.GetRoleAsync(roleId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPut("roles/{roleId:guid}/permissions")]
    [Authorize(Policy = IdentityPolicies.Write)]
    public async Task<ActionResult<RoleDetailDto>> UpdateRolePermissions(
        Guid roleId,
        [FromBody] UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _identity.UpdateRolePermissionsAsync(roleId, request, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("permissions")]
    [Authorize(Policy = IdentityPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<PermissionLookupDto>>> ListPermissions(
        CancellationToken cancellationToken) =>
        Ok(await _identity.ListPermissionsAsync(cancellationToken));

    [HttpGet("employees")]
    [Authorize(Policy = IdentityPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<EmployeeLookupDto>>> ListEmployees(
        CancellationToken cancellationToken) =>
        Ok(await _identity.ListEmployeesAsync(cancellationToken));
}
