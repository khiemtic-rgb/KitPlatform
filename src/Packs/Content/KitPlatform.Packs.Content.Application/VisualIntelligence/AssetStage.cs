using SkiaSharp;

namespace KitPlatform.Packs.Content.VisualIntelligence;

/// <summary>
/// Composites the locked master library. Plates and figures are designed art.
/// Brand recolor shifts only the shirt, and keeps the painted light and shade.
/// </summary>
public static partial class StudioScenePainter
{
    private const uint DesignedShirt = 0x335C8A;
    private static readonly object MasterGate = new();
    private static readonly Dictionary<string, SKBitmap> Masters = new();

    private static string? FinishedScene(VisualBrandStyle brand, ShotVisualDemand demand)
    {
        if (brand.Motif == "home-threshold" && demand.Pose is "sit" or "write" && demand.Setting == "doorway")
            return "scene-famixa-sit.jpg";
        if (brand.Motif == "home-threshold" && demand.Pose == "stand" && demand.Camera is "medium" or "auto" && demand.Setting is "kitchen" or "work-counter")
            return "scene-famixa-stand.jpg";
        if (brand.Motif == "counter-grid" && demand.Pose == "stand" && demand.Camera is "medium" or "auto" && demand.Setting is "pharmacy-counter" or "work-counter")
            return "scene-novixa-stand.jpg";
        if (brand.Motif == "tea-terrace" && demand.Pose == "stand" && demand.Setting == "work-counter")
            return "scene-tea-stand.jpg";
        return null;
    }

    private static DesignedResult PaintFinished(
        SKCanvas canvas,
        VisualBrandStyle brand,
        ShotVisualDemand demand,
        CharacterIdentity? identity,
        List<string> parts,
        string file)
    {
        var scene = Master(file);
        using var paint = new SKPaint { IsAntialias = true };
        canvas.DrawBitmap(scene, new SKRect(0, 0, Width, Height), paint);
        var box = RecordParts(brand, demand, parts);
        if (identity is null)
        {
            parts.Add(demand.Setting == "handoff-counter" ? "two-simplified-figures" : "simplified-figure");
            return new DesignedResult(box, false, 0, 0, 0, 0, 0, 0, "", "");
        }

        parts.Add("identity-figure");
        parts.Add(demand.Pose);
        parts.Add(demand.Camera);
        return new DesignedResult(
            box, true, 640, 150, 640, 320,
            identity.HairColor, identity.OutfitColor, identity.FaceShape, identity.Mark);
    }

    private static DesignedResult ComposeFromLibrary(
        SKCanvas canvas,
        VisualBrandStyle brand,
        ShotVisualDemand demand,
        CharacterIdentity? identity,
        List<string> parts)
    {
        var finished = FinishedScene(brand, demand);
        if (finished is not null)
            return PaintFinished(canvas, brand, demand, identity, parts, finished);
        DrawPlate(canvas, brand, demand);
        var box = RecordParts(brand, demand, parts);
        if (identity is null)
        {
            parts.Add(demand.Setting == "handoff-counter" ? "two-simplified-figures" : "simplified-figure");
            PlaceMaster(canvas, demand.Pose, FigureX(brand, demand, 0), FootY(demand.Camera), demand.Camera, ShirtOf(demand, brand.Accent));
            if (demand.Setting == "handoff-counter")
                PlaceMaster(canvas, "stand", FigureX(brand, demand, -280), FootY(demand.Camera), demand.Camera, brand.Secondary);
            return new DesignedResult(box, false, 0, 0, 0, 0, 0, 0, "", "");
        }

        parts.Add("identity-figure");
        parts.Add(demand.Pose);
        parts.Add(demand.Camera);
        var placed = PlaceMaster(canvas, demand.Pose, FigureX(brand, demand, 0), FootY(demand.Camera), demand.Camera, ShirtOf(demand, identity.OutfitColor));
        return new DesignedResult(
            box, true, placed.HairX, placed.HairY, placed.OutfitX, placed.OutfitY,
            identity.HairColor, identity.OutfitColor, identity.FaceShape, identity.Mark);
    }

    private readonly record struct Placed(int HairX, int HairY, int OutfitX, int OutfitY);

    private static readonly Dictionary<string, SKBitmap> Recolored = new();

    private static SKBitmap Figure(string cut, uint shirt)
    {
        var source = Master(cut);
        if (shirt == DesignedShirt)
            return source;
        var key = cut + ":" + shirt.ToString("X6");
        lock (MasterGate)
        {
            if (Recolored.TryGetValue(key, out var cached))
                return cached;
            var colored = RecolorShirt(source, shirt);
            Recolored[key] = colored;
            return colored;
        }
    }
    private static Placed PlaceMaster(SKCanvas canvas, string pose, float centerX, float footY, string camera, uint shirt)
    {
        var cut = pose is "sit" or "write" ? "lan-sit.jpg" : "lan-stand.jpg";
        var figure = Figure(cut, shirt);
        var height = camera switch
        {
            "close" => 700f,
            "wide" => 460f,
            _ => 640f,
        };
        height += pose switch
        {
            "kneel" => 28f,
            "walk" => -18f,
            "lean" => 14f,
            "reach" => -22f,
            "point" => 10f,
            "phone" => -10f,
            "write" => 18f,
            _ => 0f,
        };
        if (pose is "sit" or "write")
            height *= 0.92f;
        height = Math.Min(height, Height - 36);
        var width = height * figure.Width / figure.Height;
        if (width > Width - 24)
        {
            width = Width - 24;
            height = width * figure.Height / figure.Width;
        }
        var left = Math.Clamp(centerX - width / 2f, 8f, Width - width - 8f);
        var top = Math.Clamp(footY - height, 8f, Height - height - 8f);
        var dest = new SKRect(left, top, left + width, top + height);
        using (var shadow = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(20, 16, 12, 88),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 14),
        })
        {
            var ground = dest.Bottom - 8;
            canvas.DrawOval(new SKRect(dest.Left + width * 0.22f, ground - 10, dest.Right - width * 0.22f, ground + 16), shadow);
        }
        using var paint = new SKPaint { IsAntialias = true };
        canvas.DrawBitmap(figure, dest, paint);
        var hairX = Clamp((int)(dest.MidX), 8, Width - 8);
        var hairY = Clamp((int)(dest.Top + dest.Height * 0.11f), 8, Height - 8);
        var outfitX = Clamp((int)(dest.MidX), 8, Width - 8);
        var outfitY = Clamp((int)(dest.Top + dest.Height * (pose is "sit" or "write" ? 0.36f : 0.32f)), 8, Height - 8);
        return new Placed(hairX, hairY, outfitX, outfitY);
    }

    private static void DrawPlate(SKCanvas canvas, VisualBrandStyle brand, ShotVisualDemand demand)
    {
        var file = brand.Motif switch
        {
            "counter-grid" => "plate-pharmacy.jpg",
            "tea-terrace" => "plate-tea.jpg",
            _ => "plate-home.jpg",
        };
        var plate = Master(file);
        var dest = PlateDest(demand);
        using var paint = new SKPaint { IsAntialias = true };
        canvas.DrawBitmap(plate, dest, paint);
    }

    private static SKRect PlateDest(ShotVisualDemand demand)
    {
        var nudge = Slot(demand.Setting) * 16f;
        return demand.Camera switch
        {
            "close" => new SKRect(-200 - nudge, -110, Width + 60, Height + 30),
            "wide" => new SKRect(36, 20, Width - 36, Height - 12),
            _ => new SKRect(-24 - nudge, -12, Width + 24, Height + 12),
        };
    }

    private static Pack MapPlate(ShotVisualDemand demand, float sx, float sy, float sw, float sh)
    {
        var dest = PlateDest(demand);
        var scaleX = dest.Width / 1280f;
        var scaleY = dest.Height / 720f;
        var w = Math.Max(48, (int)(sw * scaleX));
        var h = Math.Max(36, (int)(sh * scaleY));
        var x = Math.Clamp((int)(dest.Left + sx * scaleX), 8, Width - w - 8);
        var y = Math.Clamp((int)(dest.Top + sy * scaleY), 8, Height - h - 80);
        return new Pack(x, y, w, h);
    }

    private static float FigureX(VisualBrandStyle brand, ShotVisualDemand demand, float shift)
    {
        var x = brand.Motif switch
        {
            "counter-grid" => 560f,
            "tea-terrace" => 340f,
            _ => demand.Pose is "sit" or "write" ? 390f : 520f,
        };
        x += (Slot(demand.Setting) - 4) * 36f;
        x += demand.Pose switch
        {
            "walk" => 78f,
            "reach" => -86f,
            "kneel" => 120f,
            "lean" => -46f,
            "point" => 150f,
            "phone" => -130f,
            "write" => 96f,
            _ => 0f,
        };
        return x + shift;
    }

    private static int Slot(string setting) => setting switch
    {
        "kitchen" or "pharmacy-counter" => 0,
        "doorway" or "shelf-aisle" or "tea-pour" => 1,
        "living-room" or "stock-room" or "tea-path" => 2,
        "park" or "delivery-door" => 3,
        "classroom" or "consult-corner" => 4,
        "night-window" or "cash-desk" => 5,
        "yard" or "night-close" => 6,
        "bedroom" or "cleaning" => 7,
        "balcony" or "waiting-bench" => 8,
        "market-walk" or "handoff-counter" => 9,
        "school-gate" or "expiry-shelf" => 10,
        "rain-window" or "opening-gate" => 11,
        _ => 6,
    };

    private static uint ShirtOf(ShotVisualDemand demand, uint fallback)
    {
        if (demand.Tokens is null)
            return fallback;
        foreach (var token in demand.Tokens)
        {
            if (!token.StartsWith("garment:", StringComparison.Ordinal))
                continue;
            return token["garment:".Length..] switch
            {
                "coat" => 0xF4F1EA,
                "family" => 0xC4554A,
                "sport" => 0x2F6F4E,
                "outdoor" => 0x6E8B74,
                "apron" => 0xE4C98A,
                "shirt" => DesignedShirt,
                _ => fallback,
            };
        }
        return fallback;
    }

    private static float FootY(string camera) => camera switch
    {
        "close" => 700f,
        "wide" => 640f,
        _ => 688f,
    };

    private static Pack? RecordParts(VisualBrandStyle brand, ShotVisualDemand demand, List<string> parts)
    {
        var setting = demand.Setting;
        switch (brand.Motif)
        {
            case "counter-grid":
                return RecordPharmacy(demand, parts);
            case "tea-terrace":
                parts.Add("terrace");
                if (setting == "tea-pour")
                {
                    parts.Add("pot");
                    parts.Add("stream");
                }
                else if (setting == "tea-path")
                {
                    parts.Add("path");
                    parts.Add("bush");
                }
                else
                {
                    parts.Add("cup");
                    parts.Add("round-table");
                }
                return null;
            case "home-threshold":
                RecordHome(setting, parts);
                return null;
            default:
                parts.Add("registered-motif");
                var hash = 17;
                foreach (var ch in brand.Motif + "/" + setting)
                    hash = hash * 31 + ch;
                parts.Add("n" + (3 + Math.Abs(hash) % 4));
                return null;
        }
    }

    private static void RecordHome(string setting, List<string> parts)
    {
        switch (setting)
        {
            case "kitchen": parts.Add("window"); parts.Add("table"); parts.Add("bowl"); break;
            case "doorway": parts.Add("door"); parts.Add("plant"); parts.Add("shoes"); break;
            case "living-room": parts.Add("sofa"); parts.Add("lamp"); parts.Add("rug"); break;
            case "park": parts.Add("horizon"); parts.Add("tree"); parts.Add("path"); break;
            case "classroom": parts.Add("board"); parts.Add("bench"); break;
            case "night-window": parts.Add("moon"); parts.Add("window"); parts.Add("dark-wall"); break;
            case "yard": parts.Add("fence"); parts.Add("ground"); break;
            case "bedroom": parts.Add("bed"); parts.Add("pillow"); break;
            case "balcony": parts.Add("rail"); parts.Add("sky"); break;
            case "market-walk": parts.Add("awning"); parts.Add("stall"); break;
            case "school-gate": parts.Add("gate"); parts.Add("posts"); break;
            case "rain-window": parts.Add("rain"); parts.Add("window"); break;
            case "work-counter": parts.Add("low-table"); parts.Add("plant"); parts.Add("window"); break;
            default: parts.Add("room"); break;
        }
    }

    private static Pack? RecordPharmacy(ShotVisualDemand demand, List<string> parts)
    {
        switch (demand.Setting)
        {
            case "pharmacy-counter":
            case "work-counter":
                parts.Add("counter"); parts.Add("shelf"); parts.Add("blank-pack");
                return NeedsLabel(demand) ? MapPlate(demand, 830, 328, 130, 72) : null;
            case "shelf-aisle":
                parts.Add("tall-shelf"); parts.Add("gap");
                return null;
            case "stock-room":
                parts.Add("crate"); parts.Add("stack");
                return null;
            case "delivery-door":
                parts.Add("door"); parts.Add("crate");
                return null;
            case "consult-corner":
                parts.Add("chair"); parts.Add("low-table");
                return null;
            case "cash-desk":
                parts.Add("drawer"); parts.Add("counter");
                return null;
            case "night-close":
                parts.Add("shutter");
                return null;
            case "cleaning":
                parts.Add("bucket"); parts.Add("floor");
                return null;
            case "waiting-bench":
                parts.Add("bench"); parts.Add("clock-face");
                return null;
            case "handoff-counter":
                parts.Add("counter"); parts.Add("two-places");
                return null;
            case "expiry-shelf":
                parts.Add("shelf"); parts.Add("blank-pack");
                return NeedsLabel(demand) ? new Pack(180, 250, 140, 100) : null;
            case "opening-gate":
                parts.Add("gate"); parts.Add("counter");
                return null;
            default:
                parts.Add("counter");
                return null;
        }
    }

    internal static SKBitmap BorrowMaster(string file) => Master(file);

    private static SKBitmap Master(string file)
    {
        lock (MasterGate)
        {
            if (Masters.TryGetValue(file, out var cached))
                return cached;
            var asm = typeof(StudioScenePainter).Assembly;
            var name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(file, StringComparison.Ordinal))
                ?? throw new InvalidOperationException("Thiếu tài nguyên gốc " + file);
            using var stream = asm.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException("Không đọc được " + file);
            using var encoded = SKBitmap.Decode(stream)
                ?? throw new InvalidOperationException("Không giải mã được " + file);
            var bitmap = file.StartsWith("lan-", StringComparison.Ordinal) ? KeyMagenta(encoded) : encoded.Copy();
            Masters[file] = bitmap;
            return bitmap;
        }
    }

    private static SKBitmap KeyMagenta(SKBitmap source)
    {
        var info = new SKImageInfo(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var keyed = new SKBitmap(info);
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var c = source.GetPixel(x, y);
                var pink = c.Red > 185 && c.Green < 75 && c.Blue > 85 && c.Blue < 230
                    && c.Red > c.Blue + 10 && c.Red > c.Green + 70;
                byte alpha = pink ? (byte)0 : (byte)255;
                if (!pink && c.Red > 180 && c.Green < 110 && c.Blue > 100 && c.Red > c.Green + 60 && c.Red > c.Blue)
                {
                    var spill = Math.Clamp((c.Green - 20) / 70f, 0f, 1f);
                    alpha = (byte)(255 * spill);
                }
                keyed.SetPixel(x, y, new SKColor(c.Red, c.Green, c.Blue, alpha));
            }
        }
        ErodePinkRim(keyed);
        for (var y = 0; y < keyed.Height; y++)
        {
            for (var x = 0; x < keyed.Width; x++)
                keyed.SetPixel(x, y, Despill(keyed.GetPixel(x, y)));
        }
        return keyed;
    }

    private static void ErodePinkRim(SKBitmap bitmap)
    {
        var clear = new List<(int X, int Y)>();
        for (var y = 1; y < bitmap.Height - 1; y++)
        {
            for (var x = 1; x < bitmap.Width - 1; x++)
            {
                var color = bitmap.GetPixel(x, y);
                if (color.Alpha < 20)
                    continue;
                var edge = bitmap.GetPixel(x - 1, y).Alpha < 20
                    || bitmap.GetPixel(x + 1, y).Alpha < 20
                    || bitmap.GetPixel(x, y - 1).Alpha < 20
                    || bitmap.GetPixel(x, y + 1).Alpha < 20;
                if (!edge)
                    continue;
                if (color.Red > color.Green + 25 && color.Blue > color.Green + 8 && color.Red > 140)
                    clear.Add((x, y));
            }
        }
        foreach (var (x, y) in clear)
            bitmap.SetPixel(x, y, SKColors.Transparent);
    }

    private static bool NeedsLabel(ShotVisualDemand demand) =>
        demand.Prop.Contains("blank", StringComparison.OrdinalIgnoreCase)
        || demand.RequiredLabels is { Length: > 0 };

    private static SKColor Despill(SKColor color)
    {
        if (color.Alpha == 0)
            return color;
        var red = (int)color.Red;
        var green = (int)color.Green;
        var blue = (int)color.Blue;
        if (red <= green + 35 || blue <= green + 10 || red <= 150)
            return color;
        var mix = color.Alpha < 250 ? 0.72f : 0.45f;
        red = (int)(red * (1 - mix) + green * mix);
        blue = (int)(blue * (1 - mix) + green * mix);
        return new SKColor((byte)red, (byte)green, (byte)blue, color.Alpha);
    }

    private static SKBitmap RecolorShirt(SKBitmap source, uint shirt)
    {
        var copy = source.Copy();
        var targetR = (byte)((shirt >> 16) & 255);
        var targetG = (byte)((shirt >> 8) & 255);
        var targetB = (byte)(shirt & 255);
        for (var y = 0; y < copy.Height; y++)
        {
            for (var x = 0; x < copy.Width; x++)
            {
                var c = copy.GetPixel(x, y);
                if (c.Alpha < 200 || !IsShirt(c))
                    continue;
                var shade = (c.Red * 0.3f + c.Green * 0.59f + c.Blue * 0.11f) / 52f;
                shade = Math.Clamp(shade, 0.28f, 1.35f);
                copy.SetPixel(x, y, new SKColor(
                    (byte)Math.Clamp(targetR * shade, 0, 255),
                    (byte)Math.Clamp(targetG * shade, 0, 255),
                    (byte)Math.Clamp(targetB * shade, 0, 255),
                    c.Alpha));
            }
        }
        return copy;
    }

    private static bool IsShirt(SKColor c) =>
        c.Blue > c.Red + 18 && c.Blue > c.Green && c.Red < 150 && c.Green < 160 && c.Blue > 50;

    private static int Clamp(int value, int min, int max) => Math.Clamp(value, min, max);
}
