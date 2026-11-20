using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Abstractions;
using KitPlatform.Packs.TestPack;

namespace KitPlatform.Api.Controllers.TestPack;

[ApiController]
[Route("api/test-pack")]
public sealed class TestPackController : ControllerBase
{
    private readonly ITenantContext _tenant;
    private readonly ITestPackProbe _probe;

    public TestPackController(ITenantContext tenant, ITestPackProbe probe)
    {
        _tenant = tenant;
        _probe = probe;
    }

    [HttpGet("ping")]
    [Authorize(Policy = TestPackPolicies.Read)]
    [RequirePlatformModule(TestPackDefinition.PrimaryModuleCode)]
    public async Task<ActionResult<TestPackPing>> Ping(CancellationToken cancellationToken)
    {
        var tenantId = _tenant.TenantId;
        return Ok(new TestPackPing(
            TestPackDefinition.PackCode,
            TestPackDefinition.PrimaryModuleCode,
            tenantId,
            await _probe.ReadAsync(tenantId, cancellationToken)));
    }

    [HttpPut("marker")]
    [Authorize(Policy = TestPackPolicies.Read)]
    [RequirePlatformModule(TestPackDefinition.PrimaryModuleCode)]
    public async Task<IActionResult> Remember(
        [FromBody] TestPackMarkerWrite request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Marker))
            return BadRequest(new { message = "Marker is required." });

        await _probe.RememberAsync(_tenant.TenantId, request.Marker.Trim(), cancellationToken);
        return NoContent();
    }
}
