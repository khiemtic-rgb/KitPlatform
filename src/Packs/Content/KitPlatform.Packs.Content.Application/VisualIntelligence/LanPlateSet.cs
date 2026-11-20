using System.Text.Json;
using SkiaSharp;

namespace KitPlatform.Packs.Content.VisualIntelligence;

/// <summary>
/// Rest pose of the cut plates. Not wired into the film engine yet.
/// </summary>
internal static class LanPlateSet
{
    internal readonly record struct RestMetrics(
        int Opaque,
        int PinkFringe,
        int InteriorOver18,
        int EdgeOver18,
        int MissingFigure,
        int OutsideFigure,
        int MaxDelta);

    internal static (SKBitmap Composite, SKBitmap Stand, RestMetrics Metrics) DrawRest(string plateDir, string standPath)
    {
        var rig = JsonSerializer.Deserialize<PlateRig>(File.ReadAllText(Path.Combine(plateDir, "rig.json")), JsonOptions)
            ?? throw new InvalidOperationException("rig.json trống.");
        if (rig.Plates.Count != 14)
            throw new InvalidOperationException("Cần đúng 14 mảnh, có " + rig.Plates.Count + ".");
        var stand = SKBitmap.Decode(standPath) ?? throw new InvalidOperationException("Không đọc được tranh gốc.");
        var composite = new SKBitmap(new SKImageInfo(stand.Width, stand.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(composite))
        {
            canvas.Clear(SKColors.Transparent);
            foreach (var name in rig.Draw)
            {
                var spot = rig.Plates[name];
                using var part = SKBitmap.Decode(Path.Combine(plateDir, spot.File))
                    ?? throw new InvalidOperationException("Không đọc được " + spot.File);
                canvas.DrawBitmap(part, spot.X, spot.Y);
            }
        }

        return (composite, stand, Measure(composite, stand));
    }

    private static RestMetrics Measure(SKBitmap composite, SKBitmap stand)
    {
        var opaque = 0;
        var pink = 0;
        var interior = 0;
        var edge = 0;
        var missing = 0;
        var outside = 0;
        var max = 0;
        var width = composite.Width;
        var height = composite.Height;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = composite.GetPixel(x, y);
                var source = stand.GetPixel(x, y);
                var painted = !IsBackgroundPink(source);
                var on = pixel.Alpha >= 200;
                if (pixel.Alpha >= 16 && IsFringe(pixel))
                    pink++;
                if (on && painted)
                {
                    opaque++;
                    var delta = Math.Abs(pixel.Red - source.Red) + Math.Abs(pixel.Green - source.Green) + Math.Abs(pixel.Blue - source.Blue);
                    if (delta > max)
                        max = delta;
                    if (delta > 18)
                    {
                        if (NearClear(composite, x, y))
                            edge++;
                        else
                            interior++;
                    }
                }
                else if (on && !painted)
                {
                    outside++;
                }
                else if (!on && painted && pixel.Alpha < 16)
                {
                    missing++;
                }
            }
        }

        return new RestMetrics(opaque, pink, interior, edge, missing, outside, max);
    }

    private static bool NearClear(SKBitmap bitmap, int x, int y)
    {
        for (var dy = -2; dy <= 2; dy += 2)
        {
            for (var dx = -2; dx <= 2; dx += 2)
            {
                var xx = x + dx;
                var yy = y + dy;
                if (xx < 0 || yy < 0 || xx >= bitmap.Width || yy >= bitmap.Height)
                    return true;
                if (bitmap.GetPixel(xx, yy).Alpha < 200)
                    return true;
            }
        }

        return false;
    }

    private static bool IsBackgroundPink(SKColor color) =>
        color.Red > 185 && color.Green < 75 && color.Blue > 85 && color.Blue < 230
        && color.Red > color.Blue + 10 && color.Red > color.Green + 70;

    private static bool IsFringe(SKColor color) =>
        color.Red > 160 && color.Green < 140 && color.Blue > 90
        && color.Red > color.Green + 25 && color.Blue > color.Green + 8 && color.Red > 140;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed class PlateRig
    {
        public Dictionary<string, PlateSpot> Plates { get; set; } = new();
        public string[] Draw { get; set; } = [];
    }

    private sealed class PlateSpot
    {
        public string File { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
    }
}
