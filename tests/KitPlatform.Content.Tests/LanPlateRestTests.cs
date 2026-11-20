using KitPlatform.Packs.Content.VisualIntelligence;
using SkiaSharp;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class LanPlateRestTests
{
    [Fact]
    public void Rest_plates_draw_at_scale_one_against_the_painting()
    {
        var repo = FindRepo();
        var plates = Path.Combine(repo, "reference", "lan-plates");
        var stand = Path.Combine(repo, "src", "Packs", "Content", "KitPlatform.Packs.Content.Application", "VisualIntelligence", "Masters", "lan-stand.jpg");
        var (composite, source, metrics) = LanPlateSet.DrawRest(plates, stand);
        using (composite)
        using (source)
        {
            var dir = Path.Combine(repo, "proof", "vector-v4");
            Directory.CreateDirectory(dir);
            using (var image = SKImage.FromBitmap(composite))
            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            using (var stream = File.Open(Path.Combine(dir, "rest-skia.png"), FileMode.Create, FileAccess.Write))
                data.SaveTo(stream);
            WriteDiff(composite, source, Path.Combine(dir, "rest-skia-diff.png"));
            File.WriteAllText(Path.Combine(dir, "rest-metrics.json"),
                "opaque " + metrics.Opaque
                + "\npinkFringe " + metrics.PinkFringe
                + "\ninteriorOver18 " + metrics.InteriorOver18
                + "\nedgeOver18 " + metrics.EdgeOver18
                + "\nmissingFigure " + metrics.MissingFigure
                + "\noutsideFigure " + metrics.OutsideFigure
                + "\nmaxDelta " + metrics.MaxDelta
                + "\n");
            Assert.Equal(source.Width, composite.Width);
            Assert.Equal(source.Height, composite.Height);
            Assert.True(metrics.Opaque > 100_000);
        }
    }

    private static void WriteDiff(SKBitmap composite, SKBitmap source, string path)
    {
        using var diff = new SKBitmap(new SKImageInfo(composite.Width, composite.Height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        for (var y = 0; y < composite.Height; y++)
        {
            for (var x = 0; x < composite.Width; x++)
            {
                var pixel = composite.GetPixel(x, y);
                var painted = source.GetPixel(x, y);
                var background = painted.Red > 185 && painted.Green < 75 && painted.Blue > 85 && painted.Blue < 230
                    && painted.Red > painted.Blue + 10 && painted.Red > painted.Green + 70;
                SKColor mark;
                if (pixel.Alpha >= 200 && !background)
                {
                    var delta = Math.Abs(pixel.Red - painted.Red) + Math.Abs(pixel.Green - painted.Green) + Math.Abs(pixel.Blue - painted.Blue);
                    mark = delta > 18 ? new SKColor(220, 40, 40) : new SKColor(pixel.Red, pixel.Green, pixel.Blue);
                }
                else if (pixel.Alpha < 16 && !background)
                {
                    mark = new SKColor(40, 180, 70);
                }
                else
                {
                    mark = new SKColor(24, 24, 24);
                }

                diff.SetPixel(x, y, mark);
            }
        }

        using var image = SKImage.FromBitmap(diff);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        using var stream = File.Open(path, FileMode.Create, FileAccess.Write);
        data.SaveTo(stream);
    }

    private static string FindRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "KitPlatform.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root was not found.");
    }
}
