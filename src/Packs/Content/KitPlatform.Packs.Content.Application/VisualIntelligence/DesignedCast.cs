using SkiaSharp;

namespace KitPlatform.Packs.Content.VisualIntelligence;

/// <summary>
/// Designed-contour figures. Each pose is a layered silhouette (sleeves, torso, legs, hands),
/// not a joint chain. The head is the shared identity.
/// </summary>
public static partial class StudioScenePainter
{
    private readonly record struct DesignedResult(
        Pack? Box,
        bool Identity,
        int HairX,
        int HairY,
        int OutfitX,
        int OutfitY,
        uint Hair,
        uint Outfit,
        string Face,
        string Mark);

    private readonly record struct ActorStamp(int HairX, int HairY, int OutfitX, int OutfitY);

    private static ActorStamp DrawActor(SKCanvas canvas, ShotVisualDemand demand, CharacterIdentity who, VisualBrandStyle brand, float shiftX)
    {
        var pants = ((who.OutfitColor & 0xFF) > ((who.OutfitColor >> 16) & 0xFF)) ? 0x6B8448u : 0x5A4634u;
        var wear = ResolveWardrobe(demand, who, brand, pants);
        if (brand.Motif == "counter-grid" && wear.Cut == "shirt")
            wear = wear with { Cut = "coat", Cloth = 0xF7F5F1, Trim = who.OutfitColor == brand.Accent ? brand.Accent : who.OutfitColor };
        var turn = ViewTurn(demand);
        var (footX, footY, height) = FrameOf(demand, brand);
        footX += shiftX;
        var pose = demand.Pose;
        canvas.Save();
        canvas.Translate(footX, footY);
        var scale = height / 480f;
        canvas.Scale(scale);
        DrawDesignedBody(canvas, demand, who, brand, wear, turn, pose);
        canvas.Restore();

        var headY = HeadYOf(pose);
        var hairLocal = headY - (who.Hair == "crop" ? 56f : 68f);
        var outfitLocal = pose is "sit" or "write" ? -190f : pose == "kneel" ? -150f : -255f;
        if (turn >= 1.5f)
            hairLocal = headY - 20f;
        return new ActorStamp(
            Clamp((int)footX),
            ClampY((int)(footY + hairLocal * scale)),
            Clamp((int)footX),
            ClampY((int)(footY + outfitLocal * scale)));
    }

    private static int Clamp(int v) => Math.Clamp(v, 8, Width - 8);

    private static int ClampY(int v) => Math.Clamp(v, 8, Height - 8);

    private static (float X, float Y, float Height) FrameOf(ShotVisualDemand demand, VisualBrandStyle brand)
    {
        var height = demand.Camera switch { "close" => 620f, "wide" => 340f, _ => 500f };
        var footY = demand.Camera switch { "close" => 760f, "wide" => 640f, _ => 688f };
        if (demand.Pose is "sit" or "write")
        {
            height *= 0.84f;
            footY = demand.Camera == "close" ? 748f : 704f;
        }

        var footX = (brand.Motif, demand.Setting) switch
        {
            ("counter-grid", "pharmacy-counter" or "work-counter" or "expiry-shelf") => 1060f,
            ("counter-grid", "shelf-aisle") => 1080f,
            ("counter-grid", "handoff-counter") => 860f,
            ("counter-grid", "consult-corner") => 780f,
            ("home-threshold", "kitchen") => 1040f,
            ("home-threshold", "doorway") => demand.Pose is "sit" or "write" ? 760f : 300f,
            ("home-threshold", "work-counter") => 990f,
            ("home-threshold", "park") => 720f,
            ("home-threshold", "bedroom" or "living-room") => 960f,
            ("tea-terrace", _) => 1020f,
            ("candle-axis", _) => 1020f,
            _ => 860f,
        };
        footX += demand.Pose switch
        {
            "walk" => 36f,
            "reach" => -40f,
            "kneel" => -20f,
            _ => 0f,
        };
        return (footX, footY, height);
    }

    private static float HeadYOf(string pose) => pose switch
    {
        "sit" or "write" => -304f,
        "kneel" => -250f,
        _ => -386f,
    };

    private static void DrawDesignedBody(
        SKCanvas canvas,
        ShotVisualDemand demand,
        CharacterIdentity who,
        VisualBrandStyle brand,
        Wardrobe wear,
        float turn,
        string pose)
    {
        var ink = brand.Ink;
        var skin = brand.Skin;
        var headY = HeadYOf(pose);
        var shoulderY = headY + 38f;
        var hipY = pose is "sit" or "write" ? headY + 156f : pose == "kneel" ? headY + 132f : headY + 188f;
        var seated = pose is "sit" or "write";
        var holdBottle = demand.Prop.Equals("bottle", StringComparison.OrdinalIgnoreCase);

        using (var shadow = Fill(0x000000))
        {
            shadow.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 14);
            shadow.Color = shadow.Color.WithAlpha(70);
            canvas.DrawOval(new SKRect(-84, -10, 90, 18), shadow);
        }

        if (seated)
            ChairUnder(canvas, hipY, brand);

        if (turn >= 1.5f)
        {
            DrawBackBody(canvas, who, brand, wear, headY, shoulderY, hipY, pose);
            return;
        }

        if (turn is >= 0.95f and < 1.5f)
        {
            DrawProfileBody(canvas, demand, who, brand, wear, headY, shoulderY, hipY, pose);
            return;
        }

        if (turn is > 0.3f and < 0.9f)
        {
            canvas.Scale(0.92f, 1f, 0, headY);
        }

        var (left, right) = Wrists(pose, shoulderY, hipY, holdBottle);
        using (var sleeve = Limb(-46, shoulderY + 32, left.X, left.Y, 15, 11, left.Bow))
            Form(canvas, sleeve, wear.Cloth, ink);
        using (var sleeve = Limb(46, shoulderY + 32, right.X, right.Y, 15, 11, right.Bow))
            Form(canvas, sleeve, Mix(wear.Cloth, 0x000000, 0.06f), ink);

        if (pose == "kneel")
            KneelLegs(canvas, hipY, wear.Pants, ink);
        else if (seated)
            SitLegs(canvas, hipY, wear.Pants, ink);
        else
            StandLegs(canvas, hipY, pose, wear.Pants, ink);

        if (seated)
        {
            ShoeAt(canvas, -58, 0, true);
            ShoeAt(canvas, 62, 0, false);
        }
        else if (pose == "kneel")
        {
            ShoeAt(canvas, -26, 2, true);
            ShoeAt(canvas, 58, -2, false);
        }
        else if (pose == "walk")
        {
            ShoeAt(canvas, -34, -2, true);
            ShoeAt(canvas, 26, -14, false);
        }
        else
        {
            ShoeAt(canvas, -18, -2, true);
            ShoeAt(canvas, 18, -2, false);
        }

        var hem = seated ? hipY - 4f : hipY + 14f;
        var cloth = wear.Cloth;
        using (var shirt = TorsoPath(shoulderY, hem, pose == "lean" ? 18f : 0f))
            Form(canvas, shirt, cloth, ink, 2.15f);
        GarmentLayers(canvas, wear, who, shoulderY, hem, ink);

        NeckColumn(canvas, headY, shoulderY, skin);
        if (wear.Cut == "sport")
            Hood(canvas, headY, wear.Cloth, ink);
        FrontHead(canvas, new P(0, headY), who, demand.Expression, skin, ink, turn, false);
        if (wear.Hat)
            StrawHat(canvas, headY, who);
        if (wear.Scarf)
            Scarf(canvas, shoulderY);
        HandAt(canvas, left.X, left.Y, left.Angle, skin, ink, pose == "point" ? "point" : "open");
        HandAt(canvas, right.X, right.Y, right.Angle, skin, ink, "open");

        if (pose == "reach")
            Clipboard(canvas, right.X + 8, right.Y - 36, brand);
        if (pose == "write")
            Pen(canvas, right.X + 6, right.Y + 4, ink);
        if (holdBottle)
            Vial(canvas, right.X - 8, right.Y + 52, 78, brand.Accent, ink);
        if (brand.Motif == "tea-terrace" && pose is "stand" or "walk")
            Sprig(canvas, left.X - 4, left.Y + 8, brand.Accent, ink);
    }

    private readonly record struct Wrist(float X, float Y, float Bow, float Angle);

    private static (Wrist Left, Wrist Right) Wrists(string pose, float shoulderY, float hipY, bool bottle)
    {
        Wrist L(float x, float y, float bow) => new(x, y, bow, MathF.Atan2(y - shoulderY, x + 26f) * 180f / MathF.PI + 90f);
        Wrist R(float x, float y, float bow) => new(x, y, bow, MathF.Atan2(y - shoulderY, x - 26f) * 180f / MathF.PI + 90f);
        return pose switch
        {
            "reach" => (L(-40, shoulderY + 150, 16), R(124, shoulderY + 70, -8)),
            "point" => (L(-150, shoulderY + 36, 10), R(40, shoulderY + 168, -16)),
            "walk" => (L(34, shoulderY + 130, -8), R(-46, shoulderY + 150, 14)),
            "phone" => (L(-42, shoulderY + 170, 14), R(18, shoulderY - 36, -20)),
            "think" => (L(-44, shoulderY + 160, 12), R(22, shoulderY - 28, -18)),
            "sit" => (L(-36, hipY + 22, 6), R(38, hipY + 22, -6)),
            "write" => (L(-30, hipY + 22, 6), R(58, hipY + 26, -4)),
            "lean" => (L(-36, shoulderY + 150, 10), R(78, shoulderY + 120, -6)),
            "kneel" => (L(-40, shoulderY + 120, 12), R(36, shoulderY + 130, -10)),
            _ when bottle => (L(-44, shoulderY + 188, 16), R(58, shoulderY + 150, -12)),
            _ => (L(-46, shoulderY + 196, 18), R(46, shoulderY + 196, -18)),
        };
    }

    private static void StandLegs(SKCanvas canvas, float hipY, string pose, uint pants, uint ink)
    {
        FillRound(canvas, new SKRect(-34, hipY - 6, 34, hipY + 30), pants, 14);
        var leftFoot = pose == "walk" ? (-34f, -4f) : (-18f, -8f);
        var rightFoot = pose == "walk" ? (26f, -18f) : (18f, -8f);
        using (var leg = Limb(-16, hipY + 12, leftFoot.Item1, leftFoot.Item2, 18, 12, pose == "walk" ? 8 : 12))
            Form(canvas, leg, pants, ink, 1.8f);
        using (var leg = Limb(16, hipY + 12, rightFoot.Item1, rightFoot.Item2, 18, 12, pose == "walk" ? -6 : -12))
            Form(canvas, leg, Mix(pants, 0x000000, 0.05f), ink, 1.8f);
    }

    private static void SitLegs(SKCanvas canvas, float hipY, uint pants, uint ink)
    {
        FillOval(canvas, new SKRect(-86, hipY - 6, 8, hipY + 42), pants);
        FillOval(canvas, new SKRect(-8, hipY - 6, 90, hipY + 42), pants);
        using (var shin = Limb(-62, hipY + 28, -58, -2, 16, 12, 2))
            Form(canvas, shin, Mix(pants, 0x000000, 0.05f), ink, 1.7f);
        using (var shin = Limb(66, hipY + 28, 62, -2, 16, 12, -2))
            Form(canvas, shin, pants, ink, 1.7f);
        using var stroke = Line(ink, 1.6f);
        canvas.DrawOval(new SKRect(-86, hipY - 6, 8, hipY + 42), stroke);
        canvas.DrawOval(new SKRect(-8, hipY - 6, 90, hipY + 42), stroke);
    }

    private static void KneelLegs(SKCanvas canvas, float hipY, uint pants, uint ink)
    {
        FillRound(canvas, new SKRect(-36, hipY - 4, 36, hipY + 22), pants, 12);
        using (var thigh = Limb(-10, hipY + 8, -28, -8, 18, 14, 6))
            Form(canvas, thigh, pants, ink, 1.8f);
        using (var thigh = Limb(12, hipY + 8, 48, hipY + 36, 18, 15, -6))
            Form(canvas, thigh, pants, ink, 1.8f);
        using (var shin = Limb(48, hipY + 32, 58, -4, 14, 11, 3))
            Form(canvas, shin, Mix(pants, 0x000000, 0.05f), ink, 1.8f);
    }

    private static void DrawProfileBody(
        SKCanvas canvas,
        ShotVisualDemand demand,
        CharacterIdentity who,
        VisualBrandStyle brand,
        Wardrobe wear,
        float headY,
        float shoulderY,
        float hipY,
        string pose)
    {
        var ink = brand.Ink;
        var skin = brand.Skin;
        using (var backLeg = Limb(-6, hipY + 10, -4, -6, 16, 11, 4))
            Form(canvas, backLeg, Mix(wear.Pants, 0x000000, 0.12f), ink, 1.7f);
        using (var arm = Limb(6, shoulderY + 28, 32, shoulderY + 150, 14, 11, 12))
            Form(canvas, arm, Mix(wear.Cloth, 0x000000, 0.08f), ink);
        using (var leg = Limb(10, hipY + 8, 16, -6, 17, 12, 10))
            Form(canvas, leg, wear.Pants, ink, 1.8f);
        ShoeAt(canvas, 18, -2, false);
        using (var torso = ProfileTorso(shoulderY, hipY + 10))
            Form(canvas, torso, wear.Cloth, ink, 2.1f);
        NeckColumn(canvas, headY, shoulderY, skin);
        ProfileHead(canvas, who, demand.Expression, skin, who.HairColor, ink, headY, wear.Hat);
        HandAt(canvas, 32, shoulderY + 150, 180, skin, ink, "open");
    }

    private static void DrawBackBody(
        SKCanvas canvas,
        CharacterIdentity who,
        VisualBrandStyle brand,
        Wardrobe wear,
        float headY,
        float shoulderY,
        float hipY,
        string pose)
    {
        var ink = brand.Ink;
        using (var sleeve = Limb(-44, shoulderY + 32, -48, shoulderY + 170, 15, 11, 14))
            Form(canvas, sleeve, wear.Cloth, ink);
        using (var sleeve = Limb(44, shoulderY + 32, 48, shoulderY + 170, 15, 11, -14))
            Form(canvas, sleeve, wear.Cloth, ink);
        StandLegs(canvas, hipY, pose, wear.Pants, ink);
        ShoeAt(canvas, -18, -2, true);
        ShoeAt(canvas, 18, -2, false);
        using (var shirt = TorsoPath(shoulderY, hipY + 14, 0))
            Form(canvas, shirt, wear.Cloth, ink, 2.1f);
        StrokeLine(canvas, 0, shoulderY + 16, 0, hipY, Mix(wear.Cloth, ink, 0.35f), 1.6f);
        NeckColumn(canvas, headY, shoulderY, brand.Skin);
        BackHead(canvas, who, headY, ink);
        HandAt(canvas, -48, shoulderY + 170, 175, brand.Skin, ink, "open");
        HandAt(canvas, 48, shoulderY + 170, 185, brand.Skin, ink, "open");
    }

    private static SKPath TorsoPath(float shoulderY, float hemY, float lean)
    {
        var path = new SKPath();
        var collar = shoulderY + 2f;
        path.MoveTo(-22 + lean * 0.1f, collar + 2);
        path.CubicTo(-62 + lean, collar, -76 + lean, collar + 18, -58 + lean, collar + 40);
        path.CubicTo(-50, (collar + hemY) / 2f, -42, hemY - 8, -38, hemY);
        path.QuadTo(0, hemY + 16, 38, hemY);
        path.CubicTo(42, hemY - 8, 50, (collar + hemY) / 2f, 58 + lean, collar + 40);
        path.CubicTo(76 + lean, collar + 18, 62 + lean, collar, 22, collar + 2);
        path.QuadTo(0, collar + 14, -22 + lean * 0.1f, collar + 2);
        path.Close();
        return path;
    }

    private static SKPath ProfileTorso(float shoulderY, float hemY)
    {
        var path = new SKPath();
        path.MoveTo(-16, shoulderY + 2);
        path.CubicTo(-32, shoulderY + 18, -34, (shoulderY + hemY) / 2f, -22, hemY);
        path.CubicTo(-4, hemY + 12, 18, hemY + 8, 26, hemY - 6);
        path.CubicTo(38, (shoulderY + hemY) / 2f, 40, shoulderY + 30, 22, shoulderY + 6);
        path.CubicTo(8, shoulderY - 6, -4, shoulderY - 2, -18, shoulderY + 4);
        path.Close();
        return path;
    }

    private static SKPath Limb(float x0, float y0, float x1, float y1, float w0, float w1, float bow)
    {
        var dx = x1 - x0;
        var dy = y1 - y0;
        var len = MathF.Max(1f, MathF.Sqrt(dx * dx + dy * dy));
        var nx = -dy / len;
        var ny = dx / len;
        var mx = (x0 + x1) / 2f + nx * bow;
        var my = (y0 + y1) / 2f + ny * bow;
        var path = new SKPath();
        path.MoveTo(x0 + nx * w0, y0 + ny * w0);
        path.QuadTo(mx + nx * (w0 + w1) * 0.5f, my + ny * (w0 + w1) * 0.5f, x1 + nx * w1, y1 + ny * w1);
        path.QuadTo(x1 + dx / len * 10f, y1 + dy / len * 10f, x1 - nx * w1, y1 - ny * w1);
        path.QuadTo(mx - nx * w1 * 0.4f, my - ny * w1 * 0.4f, x0 - nx * w0 * 0.62f, y0 - ny * w0 * 0.62f);
        path.Close();
        return path;
    }

    private static void Form(SKCanvas canvas, SKPath path, uint color, uint ink, float weight = 1.45f)
    {
        var bounds = path.Bounds;
        if (bounds.Width < 1 || bounds.Height < 1)
            return;
        canvas.Save();
        canvas.Translate(2.5f, 4f);
        using (var shadow = Fill(0x000000))
        {
            shadow.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 5.5f);
            shadow.Color = shadow.Color.WithAlpha(46);
            canvas.DrawPath(path, shadow);
        }
        canvas.Restore();
        using (var fill = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(bounds.Left, bounds.Top),
                new SKPoint(bounds.Right, bounds.Bottom),
                [C(Mix(color, 0xFFFFFF, 0.5f)), C(color), C(Mix(color, 0x000000, 0.34f))],
                [0f, 0.46f, 1f],
                SKShaderTileMode.Clamp),
        })
            canvas.DrawPath(path, fill);
        canvas.Save();
        canvas.ClipPath(path, SKClipOperation.Intersect, true);
        using (var occ = Fill(Mix(color, 0x000000, 0.4f)))
        {
            occ.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 7);
            occ.Color = occ.Color.WithAlpha(80);
            canvas.DrawOval(new SKRect(bounds.Left, bounds.Bottom - bounds.Height * 0.28f, bounds.Right, bounds.Bottom + 8), occ);
        }
        canvas.Restore();
        using var stroke = Line(Mix(ink, color, 0.42f), weight);
        canvas.DrawPath(path, stroke);
    }

    private static void NeckColumn(SKCanvas canvas, float headY, float shoulderY, uint skin)
    {
        using var path = new SKPath();
        var top = headY + 32f;
        var bot = shoulderY + 10f;
        path.MoveTo(-14, top);
        path.CubicTo(-18, (top + bot) / 2f, -24, bot - 4, -22, bot);
        path.LineTo(22, bot);
        path.CubicTo(24, bot - 4, 18, (top + bot) / 2f, 14, top);
        path.Close();
        using var fill = Fill(skin);
        canvas.DrawPath(path, fill);
        canvas.Save();
        canvas.ClipPath(path, SKClipOperation.Intersect, true);
        using var shade = Fill(Mix(skin, 0x000000, 0.22f));
        shade.Color = shade.Color.WithAlpha(70);
        canvas.DrawRect(new SKRect(4, top, 20, bot), shade);
        canvas.Restore();
    }

    private static void GarmentLayers(SKCanvas canvas, Wardrobe wear, CharacterIdentity who, float shoulderY, float hem, uint ink)
    {
        if (wear.Cut is "coat" or "blouse")
        {
            using var placket = new SKPath();
            placket.MoveTo(-7, shoulderY + 8);
            placket.LineTo(7, shoulderY + 8);
            placket.LineTo(9, hem - 6);
            placket.LineTo(-9, hem - 6);
            placket.Close();
            using var fill = Fill(wear.Trim);
            canvas.DrawPath(placket, fill);
            FillOval(canvas, new SKRect(-3.5f, shoulderY + 28, 3.5f, shoulderY + 36), Mix(wear.Trim, 0xFFFFFF, 0.35f));
            FillOval(canvas, new SKRect(-3.5f, shoulderY + 52, 3.5f, shoulderY + 60), Mix(wear.Trim, 0xFFFFFF, 0.35f));
        }
        else
        {
            StrokeLine(canvas, 0, shoulderY + 14, 0, hem - 18, Mix(wear.Cloth, ink, 0.28f), 2f);
        }

        if (wear.Cut == "sport")
        {
            FillRound(canvas, new SKRect(-34, shoulderY + 36, 34, shoulderY + 48), wear.Trim, 3);
        }

        if (wear.Cut is "apron" or "overalls")
        {
            using var apron = new SKPath();
            apron.MoveTo(-22, shoulderY + 28);
            apron.CubicTo(-36, shoulderY + 80, -40, hem + 10, -32, hem + 36);
            apron.LineTo(32, hem + 36);
            apron.CubicTo(40, hem + 10, 36, shoulderY + 80, 22, shoulderY + 28);
            apron.Close();
            Form(canvas, apron, Mix(who.OutfitColor, 0xFFFFFF, 0.15f), ink, 1.7f);
            StrokeLine(canvas, -16, shoulderY + 6, -22, shoulderY + 30, ink, 2f);
            StrokeLine(canvas, 16, shoulderY + 6, 22, shoulderY + 30, ink, 2f);
        }
    }

    private static void StrawHat(SKCanvas canvas, float headY, CharacterIdentity who)
    {
        var rx = (who.FaceShape == "oval" ? 38f : 44f) * who.HeadScale / 34f;
        var ry = 48f * who.HeadScale / 34f;
        var brim = headY - ry * 0.62f;
        FillOval(canvas, new SKRect(-rx * 0.55f, headY - ry - 6, rx * 0.55f, brim + 4), 0xD7B56A);
        FillOval(canvas, new SKRect(-rx - 30, brim - 6, rx + 30, brim + 10), 0xE7C98A);
        FillOval(canvas, new SKRect(-rx - 18, brim - 2, rx + 18, brim + 6), 0xF3D7A1);
        StrokeLine(canvas, -rx * 0.35f, brim - 2, rx * 0.35f, brim - 2, 0xC4554A, 2.2f);
        using var stroke = Line(0x5C4630, 1.5f);
        canvas.DrawOval(new SKRect(-rx - 30, brim - 6, rx + 30, brim + 10), stroke);
    }

    private static void Hood(SKCanvas canvas, float headY, uint cloth, uint ink)
    {
        using var path = new SKPath();
        path.AddOval(new SKRect(-58, headY - 70, 58, headY + 10));
        Form(canvas, path, Mix(cloth, 0x000000, 0.08f), ink, 1.6f);
    }

    private static void Scarf(SKCanvas canvas, float shoulderY)
    {
        FillOval(canvas, new SKRect(-32, shoulderY - 6, 30, shoulderY + 20), 0x8E3A36);
        FillOval(canvas, new SKRect(6, shoulderY + 4, 28, shoulderY + 46), 0xA34842);
        StrokeLine(canvas, -18, shoulderY + 6, 16, shoulderY + 8, 0x6E2C28, 1.4f);
    }

    private static void ChairUnder(SKCanvas canvas, float hipY, VisualBrandStyle brand)
    {
        var wood = Mix(brand.Prop, brand.Ink, 0.16f);
        var seat = hipY + 28f;
        var rail = seat - 86f;
        FillRound(canvas, new SKRect(-92, rail, -78, 10), wood, 3);
        FillRound(canvas, new SKRect(78, rail, 92, 10), wood, 3);
        FillRound(canvas, new SKRect(-70, seat + 6, -56, 10), wood, 3);
        FillRound(canvas, new SKRect(56, seat + 6, 70, 10), wood, 3);
        FillRound(canvas, new SKRect(-96, rail - 4, 96, rail + 12), Mix(wood, 0xFFFFFF, 0.12f), 4);
        FillRound(canvas, new SKRect(-88, seat - 8, 88, seat + 16), Mix(brand.Prop, 0xFFFFFF, 0.22f), 10);
        Stroke(canvas, new SKRect(-88, seat - 8, 88, seat + 16), brand.Ink, 1.6f);
    }

    private static void ShoeAt(SKCanvas canvas, float x, float y, bool left)
    {
        var dir = left ? -1f : 1f;
        using var path = new SKPath();
        path.MoveTo(x - 10, y - 6);
        path.CubicTo(x - 14, y + 4, x + dir * 4, y + 10, x + dir * 20, y + 6);
        path.CubicTo(x + dir * 26, y + 3, x + dir * 18, y - 8, x + 4, y - 10);
        path.CubicTo(x - 4, y - 12, x - 10, y - 8, x - 10, y - 6);
        path.Close();
        Form(canvas, path, 0x2C2826, 0x1A1614, 1.5f);
        using var sole = new SKPath();
        sole.MoveTo(x - 8, y + 4);
        sole.CubicTo(x + dir * 8, y + 12, x + dir * 22, y + 8, x + dir * 18, y + 2);
        using var paint = Line(0x14110F, 2.2f);
        canvas.DrawPath(sole, paint);
    }

    private static void HandAt(SKCanvas canvas, float x, float y, float angle, uint skin, uint ink, string kind)
    {
        canvas.Save();
        canvas.Translate(x, y);
        canvas.RotateDegrees(angle);
        var line = Mix(skin, ink, 0.45f);
        FillRound(canvas, new SKRect(-11, 0, 11, 18), skin, 8);
        Stroke(canvas, new SKRect(-11, 0, 11, 18), line, 1.15f);
        FillRound(canvas, new SKRect(-22, 6, -8, 18), skin, 5);
        Stroke(canvas, new SKRect(-22, 6, -8, 18), line, 1.1f);
        if (kind == "point")
        {
            FillRound(canvas, new SKRect(-3.2f, -32, 3.2f, 2), skin, 3.2f);
            Stroke(canvas, new SKRect(-3.2f, -32, 3.2f, 2), line, 1.1f);
            FillRound(canvas, new SKRect(4, -8, 12, 4), skin, 3);
        }
        else
        {
            for (var i = -1; i <= 1; i++)
            {
                var fx = i * 8f;
                var len = i == 0 ? -26f : -22f;
                FillRound(canvas, new SKRect(fx - 3.4f, len, fx + 3.4f, 3), skin, 3.4f);
                Stroke(canvas, new SKRect(fx - 3.4f, len, fx + 3.4f, 3), line, 1.05f);
            }
        }
        canvas.Restore();
    }

    private static void ProfileHead(SKCanvas canvas, CharacterIdentity who, string expression, uint skin, uint hair, uint ink, float headY, bool hat)
    {
        var rx = (who.FaceShape == "oval" ? 36f : 42f) * who.HeadScale / 34f;
        var ry = (who.FaceShape == "oval" ? 50f : 48f) * who.HeadScale / 34f;
        var cy = headY;
        if (who.Hair == "bob")
        {
            using var mass = new SKPath();
            mass.MoveTo(-rx * 0.2f, cy - ry - 16);
            mass.CubicTo(rx * 0.9f, cy - ry - 8, rx * 0.7f, cy + 10, rx * 0.15f, cy + ry * 0.55f);
            mass.CubicTo(-rx * 0.2f, cy + ry * 0.85f, -rx * 1.15f, cy + ry * 0.2f, -rx * 1.05f, cy - ry * 0.2f);
            mass.CubicTo(-rx * 1.2f, cy - ry * 0.8f, -rx * 0.4f, cy - ry - 18, -rx * 0.1f, cy - ry - 14);
            mass.Close();
            Form(canvas, mass, hair, ink, 1.8f);
        }
        else
        {
            using var cap = new SKPath();
            cap.AddOval(new SKRect(-rx * 0.9f, cy - ry - 12, rx * 0.55f, cy - ry * 0.15f));
            Form(canvas, cap, hair, ink, 1.6f);
        }

        if (hat)
            HatBrim(canvas, new P(0, cy), rx, ry);

        using var face = new SKPath();
        face.MoveTo(-rx * 0.15f, cy - ry * 0.82f);
        face.CubicTo(rx * 0.2f, cy - ry * 0.88f, rx * 0.34f, cy - ry * 0.15f, rx * 0.3f, cy + 4);
        face.CubicTo(rx * 0.16f, cy + 10, rx * 0.22f, cy + ry * 0.22f, rx * 0.1f, cy + ry * 0.32f);
        face.CubicTo(rx * 0.28f, cy + ry * 0.4f, rx * 0.05f, cy + ry * 0.62f, -rx * 0.15f, cy + ry * 0.78f);
        face.CubicTo(-rx * 0.7f, cy + ry * 0.4f, -rx * 0.85f, cy - ry * 0.1f, -rx * 0.15f, cy - ry * 0.82f);
        face.Close();
        using (var fill = Fill(skin))
            canvas.DrawPath(face, fill);
        using (var stroke = Line(ink, 2f))
            canvas.DrawPath(face, stroke);
        if (who.Hair == "bob")
        {
            using var bang = new SKPath();
            bang.MoveTo(-rx * 0.55f, cy - ry * 0.55f);
            bang.CubicTo(rx * 0.1f, cy - ry - 8, rx * 0.4f, cy - ry * 0.35f, rx * 0.15f, cy - ry * 0.22f);
            bang.CubicTo(-rx * 0.1f, cy - ry * 0.28f, -rx * 0.4f, cy - ry * 0.2f, -rx * 0.55f, cy - ry * 0.55f);
            bang.Close();
            Form(canvas, bang, hair, ink, 1.4f);
        }

        if (hat)
            HatCrown(canvas, new P(0, cy), rx, ry);
        var eyeX = rx * 0.05f;
        var eyeY = cy - ry * 0.02f;
        Eye(canvas, eyeX, eyeY, 0.92f, expression, expression == "wink", hair, ink);
        Brow(canvas, eyeX, eyeY - ry * 0.28f, expression, hair, false, 0.9f);
        Mouth(canvas, eyeX - 2, cy + ry * 0.36f, expression, ink);
        if (who.Mark == "freckle")
        {
            Dot(canvas, eyeX + 10, cy + 6, 0xC47A55);
            Dot(canvas, eyeX + 16, cy + 12, 0xC47A55);
        }
        else if (who.Mark == "glasses")
        {
            Stroke(canvas, new SKRect(eyeX - 16, eyeY - 12, eyeX + 16, eyeY + 12), 0x1A1A1A, 2.4f);
            StrokeLine(canvas, eyeX + 16, eyeY, eyeX + rx * 0.4f, eyeY + 2, 0x1A1A1A, 2.2f);
        }
    }

    private static void BackHead(SKCanvas canvas, CharacterIdentity who, float headY, uint ink)
    {
        var rx = (who.FaceShape == "oval" ? 36f : 44f) * who.HeadScale / 34f;
        var ry = (who.FaceShape == "oval" ? 48f : 46f) * who.HeadScale / 34f;
        var hair = who.HairColor;
        using var mass = new SKPath();
        if (who.Hair == "bob")
        {
            mass.MoveTo(0, headY - ry - 16);
            mass.CubicTo(rx + 18, headY - ry, rx + 8, headY + 20, rx - 4, headY + ry * 0.7f);
            mass.CubicTo(rx * 0.2f, headY + ry * 0.15f, -rx * 0.2f, headY + ry * 0.15f, -rx + 4, headY + ry * 0.7f);
            mass.CubicTo(-rx - 8, headY + 20, -rx - 18, headY - ry, 0, headY - ry - 16);
        }
        else
        {
            mass.AddOval(new SKRect(-rx - 6, headY - ry - 12, rx + 6, headY + 8));
        }
        mass.Close();
        Form(canvas, mass, hair, ink, 1.8f);
        Ear(canvas, -rx - 2, headY + 4, 0xE6C2A8, ink, true);
        Ear(canvas, rx + 2, headY + 4, 0xE6C2A8, ink, false);
    }

    private static void Clipboard(SKCanvas canvas, float x, float y, VisualBrandStyle brand)
    {
        var board = new SKRect(x, y, x + 54, y + 72);
        FillRound(canvas, board, 0xF4F0E6, 6);
        Stroke(canvas, board, brand.Ink, 1.8f);
        FillRound(canvas, new SKRect(x + 16, y - 6, x + 38, y + 8), Mix(brand.Ink, 0xFFFFFF, 0.2f), 3);
        StrokeLine(canvas, x + 10, y + 22, x + 44, y + 22, Mix(brand.Ink, 0xFFFFFF, 0.35f), 1.4f);
        StrokeLine(canvas, x + 10, y + 32, x + 40, y + 32, Mix(brand.Ink, 0xFFFFFF, 0.35f), 1.4f);
        StrokeLine(canvas, x + 10, y + 42, x + 36, y + 42, Mix(brand.Ink, 0xFFFFFF, 0.35f), 1.4f);
    }

    private static void Pen(SKCanvas canvas, float x, float y, uint ink)
    {
        StrokeLine(canvas, x, y, x + 22, y - 16, 0x1C3A6E, 2.4f);
        FillOval(canvas, new SKRect(x + 18, y - 20, x + 26, y - 12), 0xC4554A);
    }

    private static void Sprig(SKCanvas canvas, float x, float y, uint leaf, uint ink)
    {
        StrokeLine(canvas, x, y, x + 8, y - 28, Mix(leaf, ink, 0.4f), 1.8f);
        FillOval(canvas, new SKRect(x - 2, y - 30, x + 14, y - 16), leaf);
        FillOval(canvas, new SKRect(x + 4, y - 22, x + 18, y - 8), Mix(leaf, 0xFFFFFF, 0.2f));
    }
}
