using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.LocalOs;

namespace KitPlatform.Api.Controllers.LocalOs;

/// <summary>KIT Local OS admin — tenant KIT_LOCAL, module local_os. Review queue only.</summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[RequirePlatformModule(PlatformModuleCodes.LocalOs)]
[Route("api/local-os")]
public sealed class LocalOsController : ControllerBase
{
    private readonly ILocalOsListingService _listings;

    public LocalOsController(ILocalOsListingService listings) => _listings = listings;

    [HttpGet("listings")]
    public async Task<ActionResult<IReadOnlyList<LocalListingDto>>> List(
        [FromQuery] string? kind,
        [FromQuery] string? q,
        [FromQuery] string? cityCode,
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await _listings.ListAsync(
            new LocalListingQuery(kind, q, cityCode, status, PublicOnly: false),
            cancellationToken));

    [HttpGet("listings/{id:guid}")]
    public async Task<ActionResult<LocalListingDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var row = await _listings.GetAsync(id, publicOnly: false, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("listings")]
    public async Task<ActionResult<LocalListingDto>> Create(
        [FromBody] UpsertLocalListingRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _listings.CreateAsync(request, cancellationToken));

    [HttpPut("listings/{id:guid}")]
    public async Task<ActionResult<LocalListingDto>> Update(
        Guid id,
        [FromBody] UpsertLocalListingRequest request,
        CancellationToken cancellationToken)
    {
        var row = await _listings.UpdateAsync(id, request, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("listings/{id:guid}/status")]
    public async Task<ActionResult<LocalListingDto>> SetStatus(
        Guid id,
        [FromBody] SetLocalListingStatusRequest request,
        CancellationToken cancellationToken)
    {
        var row = await _listings.SetStatusAsync(id, request.Status, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }
}
