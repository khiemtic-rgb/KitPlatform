using System.Globalization;
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

    [HttpGet("publications")]
    public async Task<IActionResult> Publications(CancellationToken cancellationToken)
    {
        try
        {
            var index = await _reader.ListAsync(cancellationToken);
            return Ok(new
            {
                latestPublicationId = index.LatestPublicationId,
                publications = index.Publications,
                readError = (ResearchReadError?)null,
            });
        }
        catch (Exception)
        {
            return Ok(new
            {
                latestPublicationId = (string?)null,
                publications = Array.Empty<ResearchPublicationIndexEntry>(),
                readError = new ResearchReadError("UNREADABLE", "Publications could not be listed."),
            });
        }
    }

    [HttpGet("publication")]
    public async Task<IActionResult> Publication([FromQuery] string? publicationId, CancellationToken cancellationToken)
    {
        if (!TryPublicationId(publicationId, out var id, out var error))
            return error!;
        return Ok(Envelope(await ReadSafe(id, cancellationToken), id));
    }

    [HttpGet("experiments")]
    public async Task<IActionResult> Experiments([FromQuery] string? publicationId, CancellationToken cancellationToken)
    {
        if (!TryPublicationId(publicationId, out var id, out var error))
            return error!;
        var read = await ReadSafe(id, cancellationToken);
        return Ok(new
        {
            availability = read.Availability,
            publicationId = read.PublicationId,
            experiments = Readable(read) ? read.Experiments : [],
            readError = read.ReadError,
        });
    }

    [HttpGet("experiments/{experimentId}")]
    public async Task<IActionResult> Experiment(string experimentId, [FromQuery] string? publicationId, CancellationToken cancellationToken)
    {
        if (!ResearchExperimentIds.IsCatalogId(experimentId) || !TryPublicationId(publicationId, out var id, out _))
            return NotFound(new { code = "NOT_IN_PUBLICATION" });
        var read = await ReadSafe(id, cancellationToken);
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
    public async Task<IActionResult> PaperStatus([FromQuery] string? publicationId, CancellationToken cancellationToken)
    {
        if (!TryPublicationId(publicationId, out var id, out var error))
            return error!;
        var read = await ReadSafe(id, cancellationToken);
        return Ok(new
        {
            availability = read.Availability,
            status = Readable(read) ? read.PaperStatus : null,
            readError = read.ReadError,
        });
    }

    [HttpGet("evaluations")]
    public async Task<IActionResult> Evaluations([FromQuery] string? publicationId, CancellationToken cancellationToken)
    {
        if (!TryPublicationId(publicationId, out var id, out var error))
            return error!;
        return Ok(ResearchEvaluationProjector.Project(await ReadSafe(id, cancellationToken)));
    }

    [HttpGet("evaluations/{experimentId}")]
    public async Task<IActionResult> Evaluation(string experimentId, [FromQuery] string? publicationId, CancellationToken cancellationToken)
    {
        if (!ResearchExperimentIds.IsCatalogId(experimentId) || !TryPublicationId(publicationId, out var id, out _))
            return NotFound(new { code = "NOT_IN_PUBLICATION" });
        var projected = ResearchEvaluationProjector.ProjectOne(await ReadSafe(id, cancellationToken), experimentId);
        if (projected.NotInPublication)
            return NotFound(new { code = "NOT_IN_PUBLICATION" });
        return Ok(new
        {
            availability = projected.Availability,
            evaluation = projected.Evaluation,
            readError = projected.ReadError,
        });
    }

    [HttpGet("paper/report")]
    public async Task<IActionResult> PaperReport([FromQuery] string? publicationId, CancellationToken cancellationToken)
    {
        if (!TryPublicationId(publicationId, out var id, out var error))
            return error!;
        var read = await ReadSafe(id, cancellationToken);
        return Ok(new
        {
            availability = read.Availability,
            report = Readable(read) ? read.PaperReport : null,
            readError = read.ReadError,
        });
    }

    bool TryPublicationId(string? publicationId, out string? id, out IActionResult? error)
    {
        if (string.IsNullOrWhiteSpace(publicationId))
        {
            id = null;
            error = null;
            return true;
        }

        id = publicationId.Trim();
        if (ResearchPublicationIds.IsPublicationId(id))
        {
            error = null;
            return true;
        }

        error = NotFound(new { code = "NOT_IN_PUBLICATION" });
        return false;
    }

    async Task<ResearchPublicationRead> ReadSafe(string? publicationId, CancellationToken cancellationToken)
    {
        try
        {
            return await _reader.ReadAsync(publicationId, cancellationToken);
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

    static object Envelope(ResearchPublicationRead read, string? requestedId)
    {
        var evaluation = ResearchEvaluationProjector.Project(read);
        var open = Readable(read) ? read.PaperStatus?.LastProcessedOpenTime : null;
        string? dataOpenTimeUtc = null;
        if (open is long milliseconds && milliseconds > 0 && milliseconds < 4_102_444_800_000L)
            dataOpenTimeUtc = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        return new
        {
            availability = read.Availability,
            schemaVersion = read.SchemaVersion,
            publicationId = read.PublicationId,
            publishedAt = read.PublishedAt,
            publisher = read.Publisher,
            selectedBy = requestedId is null ? "latest" : "publicationId",
            snapshotSha256 = evaluation.SameSnapshot ? evaluation.SnapshotSha256 : null,
            dataOpenTime = open,
            dataOpenTimeUtc,
            verdict = Readable(read) ? evaluation.Verdict : null,
            objects = read.Objects,
            readError = read.ReadError,
        };
    }
}
