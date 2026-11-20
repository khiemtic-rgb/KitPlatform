using KitPlatform.Packs.Content.Infrastructure;
using KitPlatform.Packs.Content.Production;
using SkiaSharp;
using Xunit;

namespace KitPlatform.Content.Tests.Linesheet;

/// <summary>Ghép lớp và thẻ chữ bằng SkiaSharp thật (chạy trên máy dev Windows có native Win32).</summary>
public sealed class FamixaLinesheetSkiaComposerTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "linesheet-skia-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* tạm */ }
    }

    string Png(string name, int w, int h, SKColor color, bool transparentTop = false)
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, name);
        using var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var c = new SKCanvas(bmp))
        {
            c.Clear(transparentTop ? SKColors.Transparent : color);
            if (transparentTop) c.DrawRect(0, h * 0.8f, w, h * 0.2f, new SKPaint { Color = color });
        }

        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    static (int W, int H) Size(string png)
    {
        using var b = SKBitmap.Decode(png);
        return (b.Width, b.Height);
    }

    LinesheetComposeRequest Request(string preset, bool vertical)
    {
        var settings = FamixaLinesheetService.ParseSettings("""
            { "scene_to_setting": { "S": "stage" },
              "settings": { "stage": { "cast": ["QUAN", "MAI"] } },
              "characters": { "QUAN": { "name": "Quân", "height": 0.62 }, "MAI": { "name": "Mai", "height": 0.58 } },
              "chat_self": ["Quân"] }
            """);
        var shot = new LinesheetShot("E01_SH001", "S", "stage", "E01_001", "E01_002", ["QUAN", "MAI"], "QUAN", preset, "fade", "", "");
        var outDir = Path.Combine(_dir, "out-" + preset + (vertical ? "-v" : ""));
        Directory.CreateDirectory(outDir);
        return new LinesheetComposeRequest(shot, settings,
            Png("bg.png", 1920, 1080, new SKColor(40, 30, 80)),
            Png("mid.png", 1920, 1080, new SKColor(20, 20, 20), transparentTop: true),
            new Dictionary<string, string> { ["QUAN"] = Png("QUAN.png", 400, 900, SKColors.CornflowerBlue), ["MAI"] = Png("MAI.png", 400, 860, SKColors.Teal) },
            vertical ? 540 : 1280, vertical ? 960 : 720, vertical, outDir);
    }

    [Fact]
    public void Flat_shot_is_oversized_for_zoompan()
    {
        var composer = new SkiaLinesheetLayerComposer(new FamixaLinesheetFileStore(Path.Combine(_dir, "root")));
        var images = composer.ComposeShot(Request(LinesheetCameraPresets.SlowZoomIn, vertical: false));
        Assert.False(images.IsParallax);
        Assert.Equal(((int)(1280 * 1.25 * 1.5), (int)(720 * 1.25 * 1.5)), Size(images.FlatPng!));
    }

    [Fact]
    public void Parallax_shot_has_wide_front_layer()
    {
        var composer = new SkiaLinesheetLayerComposer(new FamixaLinesheetFileStore(Path.Combine(_dir, "root")));
        var images = composer.ComposeShot(Request(LinesheetCameraPresets.Parallax, vertical: false));
        Assert.True(images.IsParallax);
        Assert.Equal(((int)(1280 * 1.3 * 1.5), (int)(720 * 1.5)), Size(images.FrontPng!));
    }

    [Fact]
    public void Cards_render_vietnamese_text()
    {
        var store = new FamixaLinesheetFileStore(Path.Combine(_dir, "root"));
        Assert.True(File.Exists(store.FontRegularPath) && File.Exists(store.FontBoldPath), "fonts extracted");
        var composer = new SkiaLinesheetLayerComposer(store);
        Directory.CreateDirectory(_dir);
        var title = composer.RenderCard(new LinesheetTextLayer("E01_009", "LỆCH TÔNG — Chuyện năm mười tám tuổi", LinesheetTextKinds.Title, 0, 3),
            1280, 720, false, ["Quân"], Path.Combine(_dir, "title.png"));
        Assert.Equal((1280, 720), Size(title.Png));
        var chat = composer.RenderCard(new LinesheetTextLayer("E01_050", "Mai: Cậu đến chưa? / Quân: Tớ đang đến đây", LinesheetTextKinds.Chat, 1, 4),
            540, 960, true, ["Quân"], Path.Combine(_dir, "chat.png"));
        // từ 2026-11-05 tin nhắn là cảnh chèn điện thoại kín khung, nền phía sau làm mờ
        Assert.Equal(0, chat.X);
        Assert.True(chat.Blur);
        Assert.Equal((540, 960), Size(chat.Png));
    }

    [Fact]
    public void File_store_rejects_foreign_tenant_and_traversal()
    {
        var store = new FamixaLinesheetFileStore(Path.Combine(_dir, "root"));
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var key = store.Key(a, Guid.NewGuid(), "audio/E01_002.wav");
        Assert.StartsWith(a.ToString("N") + "/", key);
        Assert.Throws<InvalidOperationException>(() => store.PathFor(b, key));
        Assert.Throws<InvalidOperationException>(() => store.Key(a, Guid.NewGuid(), "../x.wav"));
        Assert.Throws<InvalidOperationException>(() => store.PathFor(a, a.ToString("N") + "/../" + b.ToString("N") + "/x"));
    }
}
