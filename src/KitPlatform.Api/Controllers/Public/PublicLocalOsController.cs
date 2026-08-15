using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Packs.LocalOs;

namespace KitPlatform.Api.Controllers.Public;

/// <summary>Thái Nguyên Life public API — no login to browse. Submit stays NEEDS_REVIEW.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/public/local-os")]
public sealed class PublicLocalOsController : ControllerBase
{
    private readonly ILocalOsListingService _listings;
    private readonly ILocalOsPublisherService _publishers;

    public PublicLocalOsController(
        ILocalOsListingService listings,
        ILocalOsPublisherService publishers)
    {
        _listings = listings;
        _publishers = publishers;
    }

    [HttpGet("listings")]
    public async Task<ActionResult<IReadOnlyList<LocalListingDto>>> List(
        [FromQuery] string? kind,
        [FromQuery] string? q,
        [FromQuery] string? cityCode,
        CancellationToken cancellationToken) =>
        Ok(await _listings.ListAsync(
            new LocalListingQuery(kind, q, cityCode, Status: null, PublicOnly: true),
            cancellationToken));

    [HttpGet("listings/{id:guid}")]
    public async Task<ActionResult<LocalListingDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var row = await _listings.GetAsync(id, publicOnly: true, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("publisher/otp")]
    public async Task<ActionResult<RequestPublisherOtpResult>> RequestOtp(
        [FromBody] RequestPublisherOtpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _publishers.RequestOtpAsync(request.Phone, cancellationToken));

    [HttpPost("publisher/verify")]
    public async Task<ActionResult<PublisherSessionDto>> VerifyOtp(
        [FromBody] VerifyPublisherOtpRequest request,
        CancellationToken cancellationToken)
    {
        var session = await _publishers.VerifyOtpAsync(request.Phone, request.Code, cancellationToken);
        return session is null
            ? Unauthorized(new { message = "Mã OTP không đúng hoặc đã hết hạn." })
            : Ok(session);
    }

    [HttpPost("listings/jobs")]
    public async Task<ActionResult<PublishJobResult>> PublishJob(
        [FromBody] PublishJobRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _publishers.PublishJobAsync(request, cancellationToken));

    [HttpGet("groups")]
    public async Task<ActionResult<IReadOnlyList<CommunityGroupDto>>> RecommendGroups(
        [FromQuery] string? category,
        [FromQuery] string? audience,
        CancellationToken cancellationToken) =>
        Ok(await _publishers.RecommendGroupsAsync(category, audience ?? "student", cancellationToken));

    [HttpPost("share-events")]
    public async Task<IActionResult> TrackShare(
        [FromBody] TrackShareRequest request,
        CancellationToken cancellationToken)
    {
        await _publishers.TrackShareAsync(request, cancellationToken);
        return NoContent();
    }
}
