using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Content.Application;

namespace KitPlatform.Api.Controllers.Content;

/// <summary>
/// Bàn nhân vật Sân gạch đỏ. Ảnh lấy từ thư mục mẫu trên máy, không gọi Runway hay Fal.
/// </summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[RequirePlatformModule(PlatformModuleCodes.KitContent)]
[Route("api/content/san-gach-do")]
public sealed class SanGachDoCharacterController : ControllerBase
{
    readonly string _root;

    public SanGachDoCharacterController(IConfiguration config)
    {
        _root = config["SanGachDo:CharacterRoot"] ?? @"F:\Noi_Com_Chung\Nhan_Vat\Bo_Chuan";
    }

    [HttpGet("characters")]
    public IActionResult List()
    {
        var people = SanGachDoSheetCatalog.Read(_root);
        return Ok(new
        {
            film = SanGachDoSheetCatalog.FilmTitle,
            root = _root,
            folderReady = Directory.Exists(_root),
            blenderReady = BlenderReady(),
            matched = false,
            motionReady = false,
            previewReady = System.IO.File.Exists(PreviewPath),
            characters = people.Select(p => new
            {
                p.Id,
                p.Name,
                p.Kind,
                p.Sheets,
                status = "CHUA_DUNG",
            }),
        });
    }

    const string PreviewPath = @"E:\KitPlatform\.dev\san-gach-do\bo\bo_khung_1.png";

    [HttpGet("preview/bo")]
    public IActionResult Preview()
    {
        if (!System.IO.File.Exists(PreviewPath)) return NotFound();
        return PhysicalFile(PreviewPath, "image/png");
    }

    [HttpGet("characters/{id}/sheets/{kind}")]
    public IActionResult Sheet(string id, string kind)
    {
        var path = SanGachDoSheetCatalog.SheetPath(_root, id, kind);
        if (path == null) return NotFound();
        return PhysicalFile(path, "image/png");
    }

    static bool BlenderReady()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        };
        if (roots.Any(root =>
            !string.IsNullOrWhiteSpace(root)
            && Directory.Exists(Path.Combine(root, "Blender Foundation"))))
            return true;
        var portable = Path.Combine(@"E:\KitPlatform\.dev\blender");
        return Directory.Exists(portable)
            && Directory.EnumerateFiles(portable, "blender.exe", SearchOption.AllDirectories).Any();
    }
}
