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
    private readonly ILocalOsIngestService _ingest;
    private readonly ILocalOsSourceService _sources;
    private readonly ILocalOsWatchService _watch;

    public LocalOsController(
        ILocalOsListingService listings,
        ILocalOsIngestService ingest,
        ILocalOsSourceService sources,
        ILocalOsWatchService watch)
    {
        _listings = listings;
        _ingest = ingest;
        _sources = sources;
        _watch = watch;
    }

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

    [HttpGet("sources")]
    public async Task<ActionResult<IReadOnlyList<LocalSourceDto>>> ListSources(
        CancellationToken cancellationToken) =>
        Ok(await _sources.ListAsync(cancellationToken));

    [HttpPost("sources")]
    public async Task<ActionResult<LocalSourceDto>> CreateSource(
        [FromBody] UpsertLocalSourceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _sources.CreateAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("sources/{id:guid}")]
    public async Task<ActionResult<LocalSourceDto>> UpdateSource(
        Guid id,
        [FromBody] UpsertLocalSourceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var row = await _sources.UpdateAsync(id, request, cancellationToken);
            return row is null ? NotFound() : Ok(row);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("sources/{id:guid}/status")]
    public async Task<ActionResult<LocalSourceDto>> SetSourceStatus(
        Guid id,
        [FromBody] SetLocalSourceStatusRequest request,
        CancellationToken cancellationToken)
    {
        var row = await _sources.SetStatusAsync(id, request.Status, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpGet("watch/runs")]
    public async Task<ActionResult<IReadOnlyList<LocalWatchRunDto>>> WatchRuns(
        CancellationToken cancellationToken) =>
        Ok(await _watch.ListRunsAsync(10, cancellationToken));

    [HttpPost("watch/run")]
    public async Task<ActionResult<LocalWatchRunDto>> WatchRun(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _watch.RunAsync("manual", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("ingest")]
    public async Task<ActionResult<IngestFromSourceResult>> Ingest(
        [FromBody] IngestFromSourceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _ingest.IngestAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
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
