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

    [HttpPost("roles")]
    [Authorize(Policy = IdentityPolicies.Write)]
    public async Task<ActionResult<RoleDetailDto>> CreateRole(
        [FromBody] CreateRoleRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _identity.CreateRoleAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetRole), new { roleId = item.Id }, item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("roles/{roleId:guid}")]
    [Authorize(Policy = IdentityPolicies.Write)]
    public async Task<ActionResult<RoleDetailDto>> UpdateRole(
        Guid roleId,
        [FromBody] UpdateRoleRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _identity.UpdateRoleAsync(roleId, request, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
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

    [HttpGet("employees/next-code")]
    [Authorize(Policy = IdentityPolicies.Read)]
    public async Task<ActionResult<NextEmployeeCodeDto>> NextEmployeeCode(CancellationToken cancellationToken) =>
        Ok(new NextEmployeeCodeDto(await _identity.GetNextEmployeeCodeAsync(cancellationToken)));

    [HttpPost("employees")]
    [Authorize(Policy = IdentityPolicies.Write)]
    public async Task<ActionResult<EmployeeLookupDto>> CreateEmployee(
        [FromBody] CreateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _identity.CreateEmployeeAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetEmployee), new { employeeId = item.Id }, item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("employees/{employeeId:guid}")]
    [Authorize(Policy = IdentityPolicies.Read)]
    public async Task<ActionResult<EmployeeDetailDto>> GetEmployee(
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var item = await _identity.GetEmployeeAsync(employeeId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPut("employees/{employeeId:guid}/branches")]
    [Authorize(Policy = IdentityPolicies.Write)]
    public async Task<ActionResult<EmployeeDetailDto>> UpdateEmployeeBranches(
        Guid employeeId,
        [FromBody] UpdateEmployeeBranchesRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _identity.UpdateEmployeeBranchesAsync(employeeId, request, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("employees/{employeeId:guid}")]
    [Authorize(Policy = IdentityPolicies.Write)]
    public async Task<IActionResult> DeleteEmployee(Guid employeeId, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _identity.DeleteEmployeeAsync(employeeId, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
