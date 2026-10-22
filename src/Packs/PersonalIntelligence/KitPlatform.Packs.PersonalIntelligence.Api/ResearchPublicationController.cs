using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Packs.PersonalIntelligence.Research;

namespace KitPlatform.Api.Controllers.PersonalIntelligence;

[ApiController]
[Route("api/personal-intelligence/research")]
[Authorize(Policy = PersonalIntelligencePolicies.ResearchRead)]
[RequirePersonalIntelligenceModule]
public sealed class ResearchPublicationController : ControllerBase
{
    readonly IResearchPublicationReader _reader;

    public ResearchPublicationController(IResearchPublicationReader reader) => _reader = reader;

    [HttpGet("publication")]
    public async Task<IActionResult> Publication(CancellationToken cancellationToken) =>
        Ok(Envelope(await ReadSafe(cancellationToken)));

    [HttpGet("experiments")]
    public async Task<IActionResult> Experiments(CancellationToken cancellationToken)
    {
        var read = await ReadSafe(cancellationToken);
        return Ok(new
        {
            availability = read.Availability,
            experiments = Readable(read) ? read.Experiments : [],
            readError = read.ReadError,
        });
    }

    [HttpGet("experiments/{experimentId}")]
    public async Task<IActionResult> Experiment(string experimentId, CancellationToken cancellationToken)
    {
        if (!ResearchExperimentIds.IsCatalogId(experimentId))
            return NotFound(new { code = "NOT_IN_PUBLICATION" });
        var read = await ReadSafe(cancellationToken);
        if (!Readable(read))
        {
            return Ok(new
            {
                availability = read.Availability,
                experiment = (ResearchExperimentRead?)null,
                readError = read.ReadError,
            });
        }

        var row = read.Experiments.FirstOrDefault(item => item.ExperimentId == experimentId);
        if (row is null)
            return NotFound(new { code = "NOT_IN_PUBLICATION" });
        return Ok(new { availability = read.Availability, experiment = row, readError = (ResearchReadError?)null });
    }

    [HttpGet("paper/status")]
    public async Task<IActionResult> PaperStatus(CancellationToken cancellationToken)
    {
        var read = await ReadSafe(cancellationToken);
        return Ok(new
        {
            availability = read.Availability,
            status = Readable(read) ? read.PaperStatus : null,
            readError = read.ReadError,
        });
    }

    [HttpGet("paper/report")]
    public async Task<IActionResult> PaperReport(CancellationToken cancellationToken)
    {
        var read = await ReadSafe(cancellationToken);
        return Ok(new
        {
            availability = read.Availability,
            report = Readable(read) ? read.PaperReport : null,
            readError = read.ReadError,
        });
    }

    async Task<ResearchPublicationRead> ReadSafe(CancellationToken cancellationToken)
    {
        try
        {
            return await _reader.ReadAsync(cancellationToken);
        }
        catch (Exception)
        {
            return new ResearchPublicationRead(
                ResearchAvailability.Unreadable,
                null,
                null,
                null,
                null,
                [],
                new ResearchReadError("UNREADABLE", "Publication could not be read."),
                [],
                null,
                null);
        }
    }

    static bool Readable(ResearchPublicationRead read) =>
        read.Availability is ResearchAvailability.Fresh or ResearchAvailability.Stale;

    static object Envelope(ResearchPublicationRead read) => new
    {
        availability = read.Availability,
        schemaVersion = read.SchemaVersion,
        publicationId = read.PublicationId,
        publishedAt = read.PublishedAt,
        publisher = read.Publisher,
        objects = read.Objects,
        readError = read.ReadError,
    };
}
