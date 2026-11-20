using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Content.VisualIntelligence;

namespace KitPlatform.Api.Controllers.Content;

[ApiController]
[Authorize(Roles = "ADMIN")]
[RequirePlatformModule(PlatformModuleCodes.KitContent)]
[ServiceFilter(typeof(ProductionTenantActionFilter))]
[Route("api/content/video-engine/visual-intelligence")]
public sealed class VisualIntelligenceController : ControllerBase
{
    private readonly IVisualIntelligenceGateway _visual;

    public VisualIntelligenceController(IVisualIntelligenceGateway visual) => _visual = visual;

    [HttpPost("resolve")]
    public ActionResult<object> Resolve([FromBody] VisualResolveBody body)
    {
        if (body.Demand is null
            || string.IsNullOrWhiteSpace(body.Demand.ShotId)
            || string.IsNullOrWhiteSpace(body.Demand.FilmId)
            || string.IsNullOrWhiteSpace(body.Demand.BrandCode))
            return BadRequest(new { message = "Cần shotId, filmId và brandCode." });
        // Search does not render. Regenerate stores a variant and leaves an approved binding in place.
        // GENERATION_REQUIRED is the future high-quality still handoff. Studio options never authorize a provider.
        var options = body.Regenerate
            ? VisualResolveOptions.Studio.AsRegenerate()
            : body.SearchOnly
                ? VisualResolveOptions.Search
                : VisualResolveOptions.Studio;
        var result = _visual.Resolve(body.Demand, options);
        var overlay = result.Contract?.ControlledOverlay ?? [];
        return Ok(new
        {
            result.Decision,
            result.AssetId,
            result.VariantAssetId,
            result.Score,
            result.ReasonCodes,
            result.Reason,
            result.PaidProviderCalls,
            result.LocalRenders,
            brandCode = body.Demand.BrandCode,
            providerId = result.Contract?.ProviderId,
            providerCallAuthorized = result.Contract?.ProviderCallAuthorized ?? false,
            rendererId = result.Frame?.RendererId ?? result.Contract?.RendererId,
            imagePrompt = result.Contract?.ImagePrompt,
            spoken = result.Contract?.Spoken,
            controlledOverlay = result.Contract?.ControlledOverlay,
            rejectedLabels = result.Contract?.RejectedLabels,
            candidates = result.Candidates,
            hasImage = result.Frame is not null,
            undrawable = IllustratedSceneRenderer.UndrawableLetters(string.Join(' ', overlay)),
            generate = false,
        });
    }

    [HttpGet("films/{filmId}/shots/{shotId}")]
    public ActionResult<VisualShotLink> Shot(string filmId, string shotId) =>
        Ok(_visual.ReadShot(filmId, shotId));

    [HttpPost("films/{filmId}/shots/{shotId}/approve")]
    public ActionResult<VisualShotLink> Approve(string filmId, string shotId)
    {
        try
        {
            return Ok(_visual.ApproveShot(filmId, shotId));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("assets/{assetId}/image")]
    public IActionResult Image(string assetId)
    {
        var bytes = _visual.ReadImage(assetId);
        if (bytes is null) return NotFound();
        return File(bytes, "image/png");
    }

    [HttpGet("regression")]
    public ActionResult<object> Regression()
    {
        var failures = SharedVisualIntelligenceV1Regression.Run();
        return Ok(new
        {
            suite = SharedVisualIntelligenceV1Regression.SuiteId,
            status = failures.Count == 0 ? "PASS" : "FAIL",
            fail = failures.Count,
            failures,
            paidProviderCalls = 0,
            generate = false,
        });
    }

    [HttpGet("performance/catalog")]
    public ActionResult PerformanceOptions() => Ok(new
    {
        engineId = new KitRigPerformanceEngine().EngineId,
        character = "lan-stand.jpg",
        limits = "Lan là hình vector phẳng: thân, đầu, tay, chân. Vai và khuỷu có mảnh tròn cùng màu áo. Bàn tay là hình tròn và ngón cái; ngón trỏ chỉ thêm khi chỉ. Phim có nghỉ, chỉ, vẫy, đi tới quầy và với tới. Chưa xác nhận bằng ảnh crop. Không dùng tranh đứng để uốn tay.",
        timelines = new[]
        {
            new { brandCode = PerformanceCatalog.NovixaCounter.BrandCode, plate = PerformanceCatalog.NovixaCounter.PlateFile, label = "Novixa — quầy thuốc" },
            new { brandCode = PerformanceCatalog.FamixaDoorway.BrandCode, plate = PerformanceCatalog.FamixaDoorway.PlateFile, label = "Famixa — bếp" },
        },
        clips = new[]
        {
            new { id = PerformanceCatalog.Explain.Id, label = "Chỉ" },
            new { id = PerformanceCatalog.Wave.Id, label = "Vẫy tay" },
            new { id = PerformanceCatalog.Grasp.Id, label = "Với tới" },
            new { id = PerformanceFilm.NovixaShortId, label = "Phim ngắn Novixa" },
        },
    });

    [HttpPost("performance")]
    public ActionResult Performance([FromBody] PerformanceBody body)
    {
        if (body.Duration <= 0)
            body.Duration = 2;
        try
        {
            var timeline = PerformanceCatalog.FindTimeline(body.BrandCode ?? "");
            IPerformanceEngine engine = new KitRigPerformanceEngine();
            if (body.ClipId == PerformanceFilm.NovixaShortId)
            {
                var total = body.Duration >= 2 && body.Duration <= 15 ? body.Duration : 15;
                var bytes = PerformanceFilm.EncodeSequence(engine, timeline, PerformanceCatalog.NovixaShort, total);
                return File(bytes, "video/mp4");
            }
            var clip = PerformanceCatalog.FindClip(body.ClipId ?? "");
            var framed = PerformanceFilm.Encode(engine, timeline, clip, body.Duration);
            return File(framed, "video/mp4");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    public sealed class PerformanceBody
    {
        public string? BrandCode { get; set; }
        public string? ClipId { get; set; }
        public float Duration { get; set; } = 2;
    }

    public sealed class VisualResolveBody
    {
        public ShotVisualDemand? Demand { get; set; }
        public bool SearchOnly { get; set; }
        public bool Regenerate { get; set; }
    }
}
