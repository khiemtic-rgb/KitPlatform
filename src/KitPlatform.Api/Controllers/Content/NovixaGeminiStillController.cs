using System.Text.Json;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Production;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitPlatform.Api.Controllers.Content;

/// <summary>One Gemini still for a film line. A library shot plan, when it matches, replaces the Novixa prompt.</summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[RequirePlatformModule(PlatformModuleCodes.KitContent)]
[Route("api/content/series/builds/{id:guid}/novixa-motion/stills/{index:int}/gemini")]
public sealed class NovixaGeminiStillController : ControllerBase
{
    readonly IGeminiProductionStillClient _gemini;

    public NovixaGeminiStillController(IGeminiProductionStillClient gemini) => _gemini = gemini;

    public sealed class GeminiStillBody
    {
        public string Text { get; set; } = "";
        public string? BrandId { get; set; }
        public string? BrandName { get; set; }
        public string? Colors { get; set; }
        public string? Style { get; set; }
        public string? ImageNotes { get; set; }
        public string? ScriptId { get; set; }
        public string? BrandCode { get; set; }
        public string? Orientation { get; set; }
        public List<string>? Lines { get; set; }
        public List<int>? Indexes { get; set; }
    }

    public sealed class GeminiStillResponse
    {
        public int Index { get; set; }
        public string Id { get; set; } = "";
        public string Text { get; set; } = "";
        public string Path { get; set; } = "";
        public string? Note { get; set; }
        public bool UsedPlan { get; set; }
        public int ShotFrom { get; set; }
        public int ShotTo { get; set; }
        public List<NovixaStillFile> Stills { get; set; } = new();
    }

    [HttpPost]
    [ServiceFilter(typeof(ProductionTenantActionFilter))]
    public async Task<ActionResult<GeminiStillResponse>> Post(Guid id, int index, [FromBody] GeminiStillBody? body, CancellationToken cancellationToken)
    {
        var text = (body?.Text ?? "").Trim();
        if (text.Length < 2)
            return BadRequest(new { message = "Chưa có câu để tạo ảnh." });
        var choice = Choose(body, index, text);
        GeminiProductionStillResult generated;
        try
        {
            generated = await _gemini.GenerateOnceAsync(choice.Prompt, choice.AspectRatio, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        if (!generated.Accepted || generated.Bytes is not { Length: >= 32 })
            return BadRequest(new { message = string.IsNullOrWhiteSpace(generated.Error) ? "Gemini không trả ảnh." : generated.Error });
        var extension = generated.Mime != null && generated.Mime.Contains("jpeg", StringComparison.OrdinalIgnoreCase) ? "jpg" : "png";
        var saved = NovixaStockMotionRunner.ReplaceStills(id, Targets(index, choice, body?.Indexes), generated.Bytes, extension);
        if (saved.Count == 0)
            return BadRequest(new { message = "Chưa có bộ ảnh local để thay. Hãy bấm Tạo ảnh trước." });
        var primary = saved.FirstOrDefault(item => item.Index == index) ?? saved[0];
        return Ok(new GeminiStillResponse
        {
            Index = primary.Index,
            Id = primary.Id,
            Text = primary.Text,
            Path = primary.Path,
            Note = choice.Note,
            UsedPlan = choice.UsedPlan,
            ShotFrom = choice.From,
            ShotTo = choice.To,
            Stills = saved,
        });
    }

    static NgvShotPlan.Choice Choose(GeminiStillBody? body, int index, string line)
    {
        var legacy = Prompt(line, body);
        if (!ContentScriptLibraryController.TryReadShotPlan(body?.BrandCode, body?.ScriptId, out var json, out _))
            return NgvShotPlan.Resolve(null, index, null, body?.Orientation, legacy);
        try
        {
            var lines = (body?.Lines ?? new List<string>()).Take(500).Select(item => item ?? "").ToList();
            return NgvShotPlan.Resolve(NgvShotPlan.Parse(json!), index, lines, body?.Orientation, legacy);
        }
        catch (JsonException)
        {
            return new NgvShotPlan.Choice(legacy, NgvShotPlan.LegacyAspect, "Không đọc được kế hoạch hình. Đã dùng prompt cũ.", index, index, false);
        }
    }

    static List<int> Targets(int index, NgvShotPlan.Choice choice, List<int>? requested)
    {
        if (!choice.UsedPlan)
            return new List<int> { index };
        var wanted = (requested ?? new List<int>())
            .Where(item => item >= choice.From && item <= choice.To)
            .Distinct()
            .Take(24)
            .ToList();
        if (wanted.Count == 0) wanted.Add(index);
        return wanted;
    }

    static string Prompt(string line, GeminiStillBody? brand)
    {
        var look = brand == null ? "" : string.Join(" ", new[]
        {
            string.IsNullOrWhiteSpace(brand.BrandName) ? "" : "Brand look: " + brand.BrandName.Trim() + ".",
            string.IsNullOrWhiteSpace(brand.Style) ? "" : "Style: " + brand.Style.Trim() + ".",
            string.IsNullOrWhiteSpace(brand.Colors) ? "" : "Colors: " + brand.Colors.Trim() + ".",
            string.IsNullOrWhiteSpace(brand.ImageNotes) ? "" : "Image notes: " + brand.ImageNotes.Trim() + ".",
        }.Where(part => part.Length > 0));
        return "Vertical 9:16 photoreal frame. Real materials only: wood, cardboard, plastic blister, and paper. "
            + "Packaging has no readable text, no letters, no numbers, no logo, and no watermark. "
            + "Do not draw the scene description as words in the image. "
            + look
            + " Scene description, not on-image text: " + line;
    }
}
