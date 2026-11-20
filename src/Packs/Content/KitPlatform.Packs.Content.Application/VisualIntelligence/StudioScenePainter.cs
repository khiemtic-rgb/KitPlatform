using SkiaSharp;

namespace KitPlatform.Packs.Content.VisualIntelligence;

/// <summary>
/// Shared illustration rig. Identity (face, hair, mark, wardrobe color) stays on the character.
/// Brand changes the room, the light, and the props.
/// </summary>
public static partial class StudioScenePainter
{
    public const int Width = 1280;
    public const int Height = 720;

    public sealed class PaintedScene
    {
        public byte[] Rgb { get; init; } = [];
        public List<string> Parts { get; init; } = [];
        public Pack? Box { get; init; }
        public bool Identity { get; init; }
        public int HairX { get; init; }
        public int HairY { get; init; }
        public int OutfitX { get; init; }
        public int OutfitY { get; init; }
        public uint Hair { get; init; }
        public uint Outfit { get; init; }
        public string Face { get; init; } = "";
        public string Mark { get; init; } = "";
    }

    public readonly record struct Pack(int X, int Y, int W, int H);

    public static PaintedScene Paint(VisualBrandStyle brand, ShotVisualDemand demand, CharacterIdentity? identity, bool schematic)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(C(brand.Background));
        var parts = new List<string> { brand.Motif };
        var staged = schematic
            ? ComposeDesigned(canvas, brand, demand, identity, true, parts)
            : ComposeFromLibrary(canvas, brand, demand, identity, parts);
        canvas.Flush();
        return new PaintedScene
        {
            Rgb = RgbOf(bitmap),
            Parts = parts,
            Box = staged.Box,
            Identity = staged.Identity,
            HairX = staged.HairX,
            HairY = staged.HairY,
            OutfitX = staged.OutfitX,
            OutfitY = staged.OutfitY,
            Hair = staged.Hair,
            Outfit = staged.Outfit,
            Face = staged.Face,
            Mark = staged.Mark,
        };
    }

    private readonly record struct CastPoint(int HairX, int HairY, int OutfitX, int OutfitY);

    private static CastPoint DrawCast(SKCanvas canvas, ShotVisualDemand demand, CharacterIdentity who, VisualBrandStyle brand, float shiftX)
    {
        var spec = PoseOf(demand.Pose);
        var hip = new P(0, -176);
        var shoulder = At(hip, 132, spec.Lean);
        var head = At(shoulder, 62, spec.Lean);
        var shoulderL = new P(shoulder.X - 40, shoulder.Y + 10);
        var shoulderR = new P(shoulder.X + 40, shoulder.Y + 10);
        var elbowL = At(shoulderL, 74, spec.ArmL);
        var handL = At(elbowL, 66, spec.ElbowL);
        var elbowR = At(shoulderR, 74, spec.ArmR);
        var handR = At(elbowR, 66, spec.ElbowR);
        var hipL = new P(hip.X - 18, hip.Y + 6);
        var hipR = new P(hip.X + 18, hip.Y + 6);
        var kneeL = At(hipL, 84, spec.LegL);
        var footL = At(kneeL, 80, spec.KneeL);
        var kneeR = At(hipR, 84, spec.LegR);
        var footR = At(kneeR, 80, spec.KneeR);
        var lowest = new[] { footL.Y, footR.Y, kneeL.Y, kneeR.Y, handL.Y, handR.Y, hip.Y }.Max();
        P Drop(P p) => new(p.X, p.Y - lowest);
        hip = Drop(hip);
        shoulder = Drop(shoulder);
        head = Drop(head);
        shoulderL = Drop(shoulderL);
        shoulderR = Drop(shoulderR);
        elbowL = Drop(elbowL);
        handL = Drop(handL);
        elbowR = Drop(elbowR);
        handR = Drop(handR);
        hipL = Drop(hipL);
        hipR = Drop(hipR);
        kneeL = Drop(kneeL);
        footL = Drop(footL);
        kneeR = Drop(kneeR);
        footR = Drop(footR);

        var span = Math.Max(160f, -head.Y);
        var headTarget = demand.Camera switch { "close" => 210f, "wide" => 270f, _ => 145f };
        var footTarget = demand.Camera switch { "close" => 1040f, "wide" => 590f, _ => 650f };
        if (demand.Pose == "sit")
        {
            headTarget = demand.Camera == "wide" ? 240f : 130f;
            footTarget = demand.Camera == "close" ? 760f : 690f;
        }
        var scale = (footTarget - headTarget) / span;
        var ground = footTarget;
        var anchor = demand.Pose switch
        {
            "sit" or "write" => 640f,
            "reach" or "point" => 760f,
            "walk" => 820f,
            "kneel" => 520f,
            "lean" => 680f,
            "phone" or "think" => 700f,
            _ => 700f,
        };
        if (demand.Setting == "pharmacy-counter")
            anchor = 990f;
        else if (demand.Setting == "work-counter")
            anchor = brand.Motif == "counter-grid" ? 990f : brand.Motif == "tea-terrace" ? 980f : 900f;
        else if (demand.Setting is "kitchen")
            anchor = 980f;
        else if (demand.Setting is "bedroom" or "living-room" or "expiry-shelf")
            anchor = 1000f;
        else if (demand.Setting == "doorway")
            anchor = demand.Pose is "sit" or "write" ? 860f : 260f;
        else if (demand.Setting == "consult-corner")
            anchor = 700f;
        anchor += shiftX;
        float Sx(float x) => anchor + x * scale;
        float Sy(float y) => ground + y * scale;

        var pants = ((who.OutfitColor & 0xFF) > ((who.OutfitColor >> 16) & 0xFF)) ? 0x6B8448u : 0x5A4634u;
        canvas.Save();
        canvas.Translate(anchor, ground);
        canvas.Scale(scale);
        DrawIllustrated(canvas, demand, who, brand, hip, shoulder, head, shoulderL, elbowL, handL, shoulderR, elbowR, handR, hipL, kneeL, footL, hipR, kneeR, footR, pants);
        canvas.Restore();

        var hairLocal = new P(head.X, head.Y - (who.FaceShape == "oval" ? 46 : 40));
        var shirtLocal = new P((shoulder.X + hip.X) / 2f, (shoulder.Y + hip.Y) / 2f);
        return new CastPoint((int)Sx(hairLocal.X), (int)Sy(hairLocal.Y), (int)Sx(shirtLocal.X), (int)Sy(shirtLocal.Y));
    }

    private readonly record struct P(float X, float Y);

    private readonly record struct PoseSpec(float Lean, float ArmL, float ElbowL, float ArmR, float ElbowR, float LegL, float KneeL, float LegR, float KneeR);

    private static PoseSpec PoseOf(string pose) => pose switch
    {
        "sit" or "write" => new(pose == "write" ? 16 : 6, 150, 132, pose == "write" ? 78 : 168, pose == "write" ? 108 : 124, 100, 176, 94, 174),
        "reach" => new(2, 168, 176, 86, 84, 176, 180, 184, 182),
        "walk" => new(6, 200, 176, 158, 184, 192, 172, 166, 178),
        "lean" => new(24, 140, 168, 50, 78, 172, 178, 190, 184),
        "kneel" => new(8, 160, 150, 196, 170, 118, 70, 168, 176),
        "point" => new(2, -76, -82, 168, 174, 176, 180, 184, 182),
        "phone" => new(0, 196, 184, 24, 6, 176, 180, 182, 180),
        "think" => new(4, 188, 176, 42, 10, 176, 180, 182, 180),
        _ => new(0, 198, 190, 162, 170, 178, 180, 182, 180),
    };

    private static P At(P origin, float length, float degrees)
    {
        var r = degrees * MathF.PI / 180f;
        return new P(origin.X + MathF.Sin(r) * length, origin.Y - MathF.Cos(r) * length);
    }

    private static void Torso(SKCanvas canvas, P hip, P shoulder, uint cloth, uint skin, uint ink)
    {
        var dx = shoulder.X - hip.X;
        var dy = shoulder.Y - hip.Y;
        var len = MathF.Max(1, MathF.Sqrt(dx * dx + dy * dy));
        var nx = -dy / len;
        var ny = dx / len;
        var path = new SKPath();
        var mid = new P((hip.X + shoulder.X) / 2f, (hip.Y + shoulder.Y) / 2f);
        path.MoveTo(hip.X - nx * 30, hip.Y - ny * 30);
        path.QuadTo(mid.X - nx * 24, mid.Y - ny * 24, shoulder.X - nx * 44, shoulder.Y - ny * 44);
        path.LineTo(shoulder.X + nx * 44, shoulder.Y + ny * 44);
        path.QuadTo(mid.X + nx * 24, mid.Y + ny * 24, hip.X + nx * 30, hip.Y + ny * 30);
        path.Close();
        using var shade = Fill(Mix(cloth, ink, 0.2f));
        canvas.Save();
        canvas.Translate(5, 6);
        canvas.DrawPath(path, shade);
        canvas.Restore();
        using var body = Fill(cloth);
        canvas.DrawPath(path, body);
        FillOval(canvas, new SKRect(shoulder.X - 18, shoulder.Y - 4, shoulder.X + 18, shoulder.Y + 16), skin);
        StrokeArc(canvas, shoulder.X, shoulder.Y + 6, 16, 10, Mix(cloth, ink, 0.25f), 20, 140, 2);
    }

    private static void Neck(SKCanvas canvas, P shoulder, P head, uint skin, uint ink)
    {
        Volume(canvas, shoulder, new P(head.X, head.Y + 36), 14, skin, ink);
    }

    private static void HeadAndFace(SKCanvas canvas, P head, CharacterIdentity who, string expression, VisualBrandStyle brand, uint skinHi)
    {
        var rx = who.FaceShape == "oval" ? 38f : 46f;
        var ry = who.FaceShape == "oval" ? 52f : 50f;
        var hair = who.HairColor;
        var hairShade = Mix(hair, 0x000000, 0.18f);
        if (who.Hair == "bob")
        {
            FillOval(canvas, new SKRect(head.X - rx - 14, head.Y - ry - 18, head.X + rx + 14, head.Y + ry * 0.2f), hair);
            SideLock(canvas, head, rx, ry, hairShade, true);
            SideLock(canvas, head, rx, ry, hairShade, false);
        }
        else
        {
            FillOval(canvas, new SKRect(head.X - rx - 4, head.Y - ry - 16, head.X + rx + 4, head.Y - ry * 0.05f), hair);
        }

        FillOval(canvas, new SKRect(head.X - rx + 3, head.Y - ry + 8, head.X + rx + 5, head.Y + ry + 4), Mix(brand.Skin, 0x000000, 0.08f));
        FillOval(canvas, new SKRect(head.X - rx, head.Y - ry, head.X + rx, head.Y + ry), brand.Skin);
        FillOval(canvas, new SKRect(head.X - rx - 4, head.Y - 2, head.X - rx + 10, head.Y + 16), brand.Skin);
        FillOval(canvas, new SKRect(head.X + rx - 10, head.Y - 2, head.X + rx + 4, head.Y + 16), brand.Skin);
        Blush(canvas, head.X - rx * 0.55f, head.Y + ry * 0.18f, skinHi);
        Blush(canvas, head.X + rx * 0.55f, head.Y + ry * 0.18f, skinHi);

        if (who.Hair == "bob")
            Bangs(canvas, head, rx, ry, hair);
        else
            FillOval(canvas, new SKRect(head.X - rx * 0.95f, head.Y - ry - 10, head.X + rx * 0.95f, head.Y - ry * 0.35f), hair);

        var eyeY = head.Y - ry * 0.02f;
        var gap = rx * 0.34f;
        Eye(canvas, head.X - gap, eyeY, expression, who.HairColor);
        Eye(canvas, head.X + gap, eyeY, expression, who.HairColor);
        Brow(canvas, head.X - gap, eyeY - 16, expression, who.HairColor, true);
        Brow(canvas, head.X + gap, eyeY - 16, expression, who.HairColor, false);
        FillOval(canvas, new SKRect(head.X - 3, eyeY + 12, head.X + 3, eyeY + 22), Mix(brand.Skin, 0x000000, 0.12f));
        Mouth(canvas, head.X, head.Y + ry * 0.38f, expression);
        if (who.Mark == "freckle")
        {
            var spot = Mix(0xC47A55, brand.Ink, 0.15f);
            Dot(canvas, head.X + rx * 0.46f, head.Y + 2, spot);
            Dot(canvas, head.X + rx * 0.58f, head.Y + 8, spot);
            Dot(canvas, head.X + rx * 0.42f, head.Y + 12, spot);
            Dot(canvas, head.X - rx * 0.48f, head.Y + 6, spot);
            Dot(canvas, head.X - rx * 0.4f, head.Y + 12, spot);
        }
        else if (who.Mark == "glasses")
        {
            Stroke(canvas, new SKRect(head.X - rx * 0.72f, eyeY - 16, head.X - rx * 0.02f, eyeY + 16), 0x1A1A1A, 3);
            Stroke(canvas, new SKRect(head.X + rx * 0.02f, eyeY - 16, head.X + rx * 0.72f, eyeY + 16), 0x1A1A1A, 3);
            StrokeLine(canvas, head.X - rx * 0.02f, eyeY, head.X + rx * 0.02f, eyeY, 0x1A1A1A, 3);
            StrokeLine(canvas, head.X - rx * 0.72f, eyeY - 2, head.X - rx - 4, eyeY + 6, 0x1A1A1A, 3);
            StrokeLine(canvas, head.X + rx * 0.72f, eyeY - 2, head.X + rx + 4, eyeY + 6, 0x1A1A1A, 3);
        }
    }

    private static void SideLock(SKCanvas canvas, P head, float rx, float ry, uint hair, bool left)
    {
        var s = left ? -1f : 1f;
        using var path = new SKPath();
        path.MoveTo(head.X + s * rx * 0.15f, head.Y - ry * 0.85f);
        path.QuadTo(head.X + s * (rx + 18), head.Y - ry * 0.1f, head.X + s * (rx + 4), head.Y + ry * 0.42f);
        path.QuadTo(head.X + s * (rx - 10), head.Y + ry * 0.15f, head.X + s * rx * 0.2f, head.Y - ry * 0.2f);
        path.Close();
        using var paint = Fill(hair);
        canvas.DrawPath(path, paint);
    }

    private static void Bangs(SKCanvas canvas, P head, float rx, float ry, uint hair)
    {
        using var path = new SKPath();
        path.MoveTo(head.X - rx * 0.95f, head.Y - ry * 0.72f);
        path.QuadTo(head.X, head.Y - ry - 20, head.X + rx * 0.95f, head.Y - ry * 0.72f);
        path.LineTo(head.X + rx * 0.72f, head.Y - ry * 0.22f);
        path.QuadTo(head.X + rx * 0.2f, head.Y - ry * 0.08f, head.X, head.Y - ry * 0.2f);
        path.QuadTo(head.X - rx * 0.2f, head.Y - ry * 0.08f, head.X - rx * 0.72f, head.Y - ry * 0.22f);
        path.Close();
        using var paint = Fill(hair);
        canvas.DrawPath(path, paint);
    }

    private static void Blush(SKCanvas canvas, float x, float y, uint color)
    {
        using var paint = Fill(color);
        paint.Color = paint.Color.WithAlpha(80);
        canvas.DrawOval(new SKRect(x - 10, y - 6, x + 10, y + 6), paint);
    }

    private static void Eye(SKCanvas canvas, float x, float y, string expression, uint hair)
    {
        var h = expression == "focus" ? 7f : expression == "smile" ? 9f : 11f;
        FillOval(canvas, new SKRect(x - 13, y - h, x + 13, y + h), 0xFFFFFF);
        StrokeArc(canvas, x, y - 1, 13, h, Mix(hair, 0x000000, 0.15f), 200, 150, 2.4f);
        FillOval(canvas, new SKRect(x - 6, y - 6, x + 6, y + 6), 0x5A3A28);
        FillOval(canvas, new SKRect(x - 3, y - 3, x + 3, y + 3), 0x1A120E);
        FillOval(canvas, new SKRect(x + 2, y - 5, x + 5, y - 2), 0xFFFFFF);
    }

    private static void Brow(SKCanvas canvas, float x, float y, string expression, uint hair, bool left)
    {
        var lift = expression switch
        {
            "concern" => left ? -7f : 5f,
            "focus" => left ? 4f : -4f,
            "smile" => -3f,
            _ => 0f,
        };
        StrokeLine(canvas, x - 14, y + (left ? lift : 0), x + 12, y + (left ? 0 : lift) - 1, hair, 3.5f);
    }

    private static void Mouth(SKCanvas canvas, float x, float y, string expression)
    {
        const uint lip = 0xC46A62;
        if (expression == "smile")
        {
            FillOval(canvas, new SKRect(x - 14, y - 2, x + 14, y + 14), lip);
            FillRound(canvas, new SKRect(x - 10, y, x + 10, y + 6), 0xFFFFFF, 2);
            StrokeArc(canvas, x, y - 4, 14, 8, 0x8A403C, 15, 150, 2);
        }
        else if (expression == "concern")
        {
            StrokeArc(canvas, x, y + 12, 12, 7, lip, 200, 140, 3);
        }
        else if (expression == "focus")
        {
            StrokeLine(canvas, x - 8, y + 2, x + 10, y + 2, lip, 3);
        }
        else
        {
            StrokeArc(canvas, x, y, 11, 6, lip, 15, 150, 2.5f);
        }
    }

    private static void Hand(SKCanvas canvas, P elbow, P hand, uint skin)
    {
        var dx = hand.X - elbow.X;
        var dy = hand.Y - elbow.Y;
        var len = MathF.Max(1f, MathF.Sqrt(dx * dx + dy * dy));
        var ux = dx / len;
        var uy = dy / len;
        var nx = -uy;
        var ny = ux;
        FillOval(canvas, new SKRect(hand.X - 11, hand.Y - 10, hand.X + 11, hand.Y + 12), skin);
        FillOval(canvas, new SKRect(hand.X + nx * 9 - 5, hand.Y + ny * 9 - 5, hand.X + nx * 9 + 5, hand.Y + ny * 9 + 6), skin);
        for (var i = -1; i <= 1; i++)
        {
            var fx = hand.X + ux * 12 + nx * i * 5;
            var fy = hand.Y + uy * 12 + ny * i * 5;
            FillOval(canvas, new SKRect(fx - 3.5f, fy - 5, fx + 3.5f, fy + 5), skin);
        }
    }

    private static void Shoe(SKCanvas canvas, P foot, uint ink)
    {
        FillOval(canvas, new SKRect(foot.X - 8, foot.Y - 6, foot.X + 26, foot.Y + 8), ink);
        FillOval(canvas, new SKRect(foot.X - 4, foot.Y - 8, foot.X + 16, foot.Y + 2), Mix(ink, 0xFFFFFF, 0.18f));
    }

    private static void Volume(SKCanvas canvas, P a, P b, float radius, uint color, uint ink)
    {
        Taper(canvas, new P(a.X + 3, a.Y + 4), new P(b.X + 3, b.Y + 4), radius, radius * 0.7f, Mix(color, ink, 0.28f));
        Taper(canvas, a, b, radius * 0.9f, radius * 0.62f, color);
    }

    private static void Taper(SKCanvas canvas, P a, P b, float r0, float r1, uint color)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var len = MathF.Max(1f, MathF.Sqrt(dx * dx + dy * dy));
        var nx = -dy / len;
        var ny = dx / len;
        using var path = new SKPath();
        path.MoveTo(a.X + nx * r0, a.Y + ny * r0);
        path.LineTo(a.X - nx * r0, a.Y - ny * r0);
        path.LineTo(b.X - nx * r1, b.Y - ny * r1);
        path.LineTo(b.X + nx * r1, b.Y + ny * r1);
        path.Close();
        using var paint = Fill(color);
        canvas.DrawPath(path, paint);
        FillOval(canvas, new SKRect(a.X - r0, a.Y - r0, a.X + r0, a.Y + r0), color);
        FillOval(canvas, new SKRect(b.X - r1, b.Y - r1, b.X + r1, b.Y + r1), color);
    }

    private static Pack? DrawPlace(SKCanvas canvas, VisualBrandStyle brand, ShotVisualDemand demand, List<string> parts)
    {
        var warm = brand.Motif is "home-threshold" or "tea-terrace";
        if (brand.Motif != "candle-axis")
            Stage(canvas, brand, warm);
        return brand.Motif switch
        {
            "home-threshold" => Home(canvas, brand, demand.Setting, parts),
            "counter-grid" => Pharmacy(canvas, brand, demand.Setting, parts),
            "candle-axis" => Trade(canvas, brand, demand, parts),
            "tea-terrace" => Tea(canvas, brand, demand.Setting, parts),
            _ => Registered(canvas, brand, demand.Setting, parts),
        };
    }

    private static void Stage(SKCanvas canvas, VisualBrandStyle brand, bool warm)
    {
        const float horizon = 318f;
        using var wall = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 2),
                new SKPoint(0, horizon),
                [C(Mix(brand.Background, 0xFFFFFF, warm ? 0.16f : 0.06f)), C(brand.Background)],
                SKShaderTileMode.Clamp),
        };
        canvas.DrawRect(new SKRect(0, 2, Width, horizon), wall);
        var floor = Mix(brand.Secondary, brand.Background, warm ? 0.55f : 0.22f);
        FillRound(canvas, new SKRect(0, horizon, Width, Height), floor, 0);
        FillRound(canvas, new SKRect(0, horizon - 8, Width, horizon + 8), Mix(brand.Prop, brand.Ink, 0.18f), 0);
        var vanishX = Width * 0.48f;
        for (var row = 0; row < 7; row++)
        {
            var t0 = row / 7f;
            var t1 = (row + 1) / 7f;
            var y0 = horizon + 10 + (Height - horizon - 16) * t0 * t0;
            var y1 = horizon + 10 + (Height - horizon - 16) * t1 * t1;
            var plank = Mix(floor, row % 2 == 0 ? 0xFFFFFF : brand.Ink, row % 2 == 0 ? 0.08f : 0.06f);
            FillRound(canvas, new SKRect(0, y0, Width, y1), plank, 0);
            StrokeLine(canvas, 0, y0, Width, y0, Mix(brand.Ink, floor, 0.55f), 1.1f);
        }
        for (var board = -3; board <= 3; board++)
        {
            if (board == 0) continue;
            StrokeLine(canvas, vanishX + board * 18f, horizon + 12, vanishX + board * 150f, Height - 4, Mix(brand.Ink, floor, 0.62f), 1.1f);
        }
    }

    private static Pack? Home(SKCanvas canvas, VisualBrandStyle brand, string setting, List<string> parts)
    {
        switch (setting)
        {
            case "kitchen":
                parts.Add("window"); parts.Add("table"); parts.Add("bowl");
                WallCabinet(canvas, 36, 58, 300, 176, brand);
                Window(canvas, 360, 70, 280, 180, brand);
                WallCabinet(canvas, 668, 58, 300, 176, brand);
                FillRound(canvas, new SKRect(36, 248, 968, 312), Mix(brand.Prop, 0xFFFFFF, 0.55f), 0);
                var wood = Mix(brand.Prop, brand.Ink, 0.08f);
                FillRound(canvas, new SKRect(36, 500, 960, 690), wood, 6);
                FillRound(canvas, new SKRect(28, 488, 980, 528), Mix(brand.Prop, 0xFFFFFF, 0.22f), 4);
                Stroke(canvas, new SKRect(28, 488, 980, 528), brand.Ink, 1.8f);
                for (var door = 0; door < 4; door++)
                {
                    var dx = 56 + door * 220;
                    Stroke(canvas, new SKRect(dx, 544, dx + 190, 672), brand.Ink, 1.6f);
                    FillRound(canvas, new SKRect(dx + 82, 598, dx + 108, 608), brand.Ink, 2);
                }
                Bowl(canvas, 220, 460, brand);
                Cup(canvas, 420, 468, brand);
                break;
            case "doorway":
                parts.Add("door"); parts.Add("plant"); parts.Add("shoes");
                Door(canvas, 470, 80, 250, 470, brand);
                Plant(canvas, 900, 430, brand);
                Shoe(canvas, new P(250, 600), brand.Ink);
                Shoe(canvas, new P(310, 604), brand.Ink);
                break;
            case "living-room":
                parts.Add("sofa"); parts.Add("lamp"); parts.Add("rug");
                FillOval(canvas, new SKRect(80, 600, 1040, 710), Mix(brand.Secondary, brand.Background, 0.35f));
                Prism(canvas, 80, 680, 700, 150, 50, brand.Accent, brand.Ink);
                Prism(canvas, 100, 560, 660, 160, 28, Mix(brand.Accent, brand.Ink, 0.25f), brand.Ink);
                FillRound(canvas, new SKRect(200, 500, 340, 555), Mix(brand.Accent, 0xFFFFFF, 0.22f), 16);
                FillRound(canvas, new SKRect(380, 500, 540, 555), Mix(brand.Secondary, 0xFFFFFF, 0.12f), 16);
                FillRound(canvas, new SKRect(560, 500, 760, 555), Mix(brand.Accent, 0xFFFFFF, 0.08f), 16);
                Lamp(canvas, 1080, 220, brand);
                break;
            case "park":
                parts.Add("horizon"); parts.Add("tree"); parts.Add("path");
                FillRound(canvas, new SKRect(0, 2, Width, 300), Mix(brand.Background, brand.Secondary, 0.28f), 0);
                var path = new SKPath();
                path.MoveTo(520, 330);
                path.LineTo(760, 330);
                path.LineTo(980, 700);
                path.LineTo(300, 700);
                path.Close();
                using (var paint = Fill(Mix(brand.Prop, brand.Background, 0.15f)))
                    canvas.DrawPath(path, paint);
                Tree(canvas, 140, 180, brand);
                Tree(canvas, 1040, 200, brand);
                Tree(canvas, 360, 230, brand);
                break;
            case "classroom":
                parts.Add("board"); parts.Add("bench");
                Prism(canvas, 280, 300, 720, 200, 16, Mix(brand.Secondary, 0xFFFFFF, 0.35f), brand.Ink);
                Stroke(canvas, new SKRect(300, 80, 960, 270), brand.Ink, brand.LineWeight);
                for (var i = 0; i < 3; i++)
                {
                    Prism(canvas, 80 + i * 400, 640, 280, 36, 24, brand.Prop, brand.Ink);
                    Prism(canvas, 100 + i * 400, 640, 16, 70, 8, brand.Ink, brand.Ink);
                    Prism(canvas, 320 + i * 400, 640, 16, 70, 8, brand.Ink, brand.Ink);
                }
                break;
            case "night-window":
                parts.Add("moon"); parts.Add("window"); parts.Add("dark-wall");
                FillRound(canvas, new SKRect(0, 2, Width, Height), Mix(brand.Ink, brand.Background, 0.55f), 0);
                FillOval(canvas, new SKRect(980, 70, 1100, 190), brand.Prop);
                Window(canvas, 120, 90, 280, 210, brand);
                break;
            case "yard":
                parts.Add("fence"); parts.Add("ground");
                for (var i = 0; i < 14; i++)
                    Prism(canvas, 40 + i * 88, 470, 18, 180, 10, brand.Prop, brand.Ink);
                Prism(canvas, 40, 340, 1180, 16, 12, brand.Prop, brand.Ink);
                break;
            case "bedroom":
                parts.Add("bed"); parts.Add("pillow");
                Prism(canvas, 80, 690, 780, 180, 70, brand.Prop, brand.Ink);
                Prism(canvas, 80, 640, 40, 300, 18, Mix(brand.Prop, brand.Ink, 0.45f), brand.Ink);
                FillRound(canvas, new SKRect(160, 500, 340, 570), 0xFFFFFF, 14);
                FillRound(canvas, new SKRect(360, 540, 760, 650), brand.Accent, 12);
                break;
            case "balcony":
                parts.Add("rail"); parts.Add("sky");
                FillRound(canvas, new SKRect(0, 2, Width, 340), Mix(brand.Accent, 0xFFFFFF, 0.7f), 0);
                for (var i = 0; i < 16; i++)
                    StrokeLine(canvas, 50 + i * 76, 360, 80 + i * 70, 560, brand.Ink, brand.LineWeight);
                StrokeLine(canvas, 40, 360, 1240, 360, brand.Ink, brand.LineWeight + 1);
                break;
            case "market-walk":
                parts.Add("awning"); parts.Add("stall");
                Stall(canvas, 80, brand.Accent, brand);
                Stall(canvas, 620, brand.Secondary, brand);
                break;
            case "school-gate":
                parts.Add("gate"); parts.Add("posts");
                Prism(canvas, 280, 600, 36, 420, 20, brand.Ink, brand.Ink);
                Prism(canvas, 900, 600, 36, 420, 20, brand.Ink, brand.Ink);
                Prism(canvas, 280, 220, 656, 28, 16, brand.Ink, brand.Ink);
                break;
            case "rain-window":
                parts.Add("rain"); parts.Add("window");
                Window(canvas, 360, 60, 420, 280, brand);
                for (var i = 0; i < 18; i++)
                    StrokeLine(canvas, 40 + i * 70, 20, 10 + i * 70, 420, Mix(brand.Ink, brand.Accent, 0.35f), 2);
                break;
            case "work-counter":
                parts.Add("low-table"); parts.Add("plant"); parts.Add("window");
                Window(canvas, 70, 60, 240, 180, brand);
                Plant(canvas, 180, 430, brand);
                Prism(canvas, 280, 600, 520, 70, 70, brand.Prop, brand.Ink);
                FillRound(canvas, new SKRect(340, 500, 470, 545), 0xFFFaf6, 3);
                Stroke(canvas, new SKRect(340, 500, 470, 545), brand.Ink, 1.6f);
                StrokeLine(canvas, 360, 518, 450, 518, Mix(brand.Ink, brand.Background, 0.4f), 1.4f);
                StrokeLine(canvas, 360, 528, 430, 528, Mix(brand.Ink, brand.Background, 0.4f), 1.4f);
                Cup(canvas, 560, 470, brand);
                break;
            default:
                parts.Add("room");
                Window(canvas, 80, 70, 220, 160, brand);
                Prism(canvas, 420, 580, 380, 60, 50, brand.Prop, brand.Ink);
                break;
        }
        return null;
    }

    private static Pack? Pharmacy(SKCanvas canvas, VisualBrandStyle brand, string setting, List<string> parts)
    {
        Plus(canvas, 1180, 48, brand.Accent);
        Pack? box = null;
        switch (setting)
        {
            case "pharmacy-counter":
            case "work-counter":
                parts.Add("counter"); parts.Add("shelf"); parts.Add("blank-pack");
                FillOval(canvas, new SKRect(80, 330, 860, 390), Mix(brand.Ink, brand.Background, 0.82f));
                Cabinet(canvas, 70, 78, 720, 250, brand);
                Plant(canvas, 900, 250, brand);
                StrokeLine(canvas, 420, 2, 420, 46, brand.Ink, 1.5f);
                FillOval(canvas, new SKRect(396, 38, 444, 78), brand.Secondary);
                FillOval(canvas, new SKRect(30, 630, 780, 700), Mix(brand.Ink, brand.Background, 0.78f));
                Prism(canvas, 40, 640, 720, 86, 56, Mix(brand.Prop, 0xFFFFFF, 0.12f), brand.Ink);
                FillRound(canvas, new SKRect(48, 548, 752, 592), Mix(brand.Prop, 0xFFFFFF, 0.28f), 4);
                FillRound(canvas, new SKRect(330, 468, 470, 560), Mix(brand.Ink, brand.Background, 0.08f), 8);
                FillRound(canvas, new SKRect(344, 482, 456, 546), Mix(brand.Ink, brand.Background, 0.55f), 4);
                Bottle(canvas, 500, 470, 86, brand.Accent, brand.Ink);
                Jar(canvas, 590, 500, brand);
                MedicineBox(canvas, 680, 548, 48, 58, brand.Secondary, brand.Ink);
                Blister(canvas, 760, 548, brand.Accent, brand.Ink);
                MedicineBox(canvas, 100, 548, 36, 44, Mix(brand.Accent, 0xFFFFFF, 0.2f), brand.Ink);
                for (var drawer = 0; drawer < 3; drawer++)
                {
                    var dx = 90 + drawer * 200;
                    Stroke(canvas, new SKRect(dx, 656, dx + 150, 708), brand.Ink, 1.6f);
                    FillRound(canvas, new SKRect(dx + 58, 676, dx + 92, 684), brand.Ink, 2);
                }
                box = new Pack(150, 500, 140, 90);
                break;
            case "shelf-aisle":
                parts.Add("tall-shelf"); parts.Add("gap");
                TallBay(canvas, 80, brand);
                TallBay(canvas, 480, brand);
                TallBay(canvas, 880, brand);
                FillRound(canvas, new SKRect(540, 220, 720, 340), brand.Background, 6);
                break;
            case "stock-room":
                parts.Add("crate"); parts.Add("stack");
                Prism(canvas, 120, 580, 240, 140, 50, brand.Prop, brand.Ink);
                Prism(canvas, 180, 440, 220, 120, 40, brand.Secondary, brand.Ink);
                Prism(canvas, 700, 600, 300, 180, 60, brand.Accent, brand.Ink);
                break;
            case "delivery-door":
                parts.Add("door"); parts.Add("crate");
                Door(canvas, 620, 70, 280, 500, brand);
                Prism(canvas, 120, 600, 220, 140, 40, brand.Prop, brand.Ink);
                break;
            case "consult-corner":
                parts.Add("chair"); parts.Add("low-table");
                Chair(canvas, 180, brand);
                Chair(canvas, 860, brand);
                Prism(canvas, 460, 580, 280, 50, 40, brand.Prop, brand.Ink);
                break;
            case "cash-desk":
                parts.Add("drawer"); parts.Add("counter");
                Prism(canvas, 80, 620, 1000, 90, 50, brand.Prop, brand.Ink);
                FillRound(canvas, new SKRect(180, 500, 360, 560), brand.Background, 6);
                Stroke(canvas, new SKRect(180, 500, 360, 560), brand.Ink, brand.LineWeight);
                break;
            case "night-close":
                parts.Add("shutter");
                FillRound(canvas, new SKRect(120, 70, 1120, 520), brand.Ink, 6);
                for (var y = 90; y < 500; y += 28)
                    FillRound(canvas, new SKRect(146, y, 1094, y + 8), brand.Background, 2);
                break;
            case "cleaning":
                parts.Add("bucket"); parts.Add("floor");
                Prism(canvas, 240, 580, 90, 80, 30, brand.Accent, brand.Ink);
                FillRound(canvas, new SKRect(0, 560, Width, 620), Mix(brand.Ink, brand.Background, 0.1f), 0);
                break;
            case "waiting-bench":
                parts.Add("bench"); parts.Add("clock-face");
                Prism(canvas, 140, 600, 700, 36, 30, brand.Prop, brand.Ink);
                Clock(canvas, 1040, 140, brand);
                break;
            case "handoff-counter":
                parts.Add("counter"); parts.Add("two-places");
                Prism(canvas, 60, 640, 1120, 64, 40, brand.Prop, brand.Ink);
                break;
            case "expiry-shelf":
                parts.Add("shelf"); parts.Add("blank-pack");
                ShelfRun(canvas, 80, 150, brand);
                Bottle(canvas, 520, 360, 80, brand.Prop, brand.Ink);
                box = new Pack(180, 250, 140, 100);
                break;
            case "opening-gate":
                parts.Add("gate"); parts.Add("counter");
                Prism(canvas, 160, 600, 28, 360, 16, brand.Ink, brand.Ink);
                Prism(canvas, 460, 600, 28, 360, 16, brand.Ink, brand.Ink);
                Prism(canvas, 520, 620, 640, 48, 30, brand.Prop, brand.Ink);
                break;
            default:
                parts.Add("counter");
                Prism(canvas, 80, 630, 1080, 60, 40, brand.Prop, brand.Ink);
                break;
        }
        return box;
    }

    private static Pack? Trade(SKCanvas canvas, VisualBrandStyle brand, ShotVisualDemand demand, List<string> parts)
    {
        var setting = demand.Setting;
        parts.Add("axis");
        FillRound(canvas, new SKRect(0, 2, Width, Height), brand.Background, 0);
        FillRound(canvas, new SKRect(0, 80, Width, 420), Mix(brand.Background, brand.Ink, 0.18f), 0);
        Prism(canvas, 40, 680, 1180, 80, 50, Mix(brand.Prop, brand.Background, 0.35f), brand.Ink);
        var panel = setting == "dual-screen" ? new SKRect(70, 80, 560, 400) : new SKRect(120, 60, 820, 400);
        Monitor(canvas, panel, brand);
        FillRound(canvas, new SKRect(panel.MidX - 14, panel.Bottom, panel.MidX + 14, 590), Mix(brand.Ink, brand.Background, 0.35f), 3);
        FillRound(canvas, new SKRect(panel.MidX - 70, 575, panel.MidX + 70, 600), Mix(brand.Ink, brand.Background, 0.28f), 4);
        Prism(canvas, 860, 640, 280, 150, 16, Mix(brand.Prop, brand.Background, 0.2f), brand.Ink);
        var candles = setting switch { "risk-board" => 4, "candle-cluster" => 14, "range-bars" => 8, "session-open" => 5, _ => 9 };
        Candles(canvas, panel, candles, brand);
        parts.Add(setting == "dual-screen" ? "two-monitors" : "candles");
        if (setting == "dual-screen")
        {
            var second = new SKRect(620, 90, 1180, 430);
            Monitor(canvas, second, brand);
            Candles(canvas, second, 6, brand);
        }
        else if (setting == "review-table")
        {
            parts.Add("blank-pages");
            Prism(canvas, 900, 400, 140, 160, 18, brand.Prop, brand.Ink);
        }
        else
        {
            parts.Add("monitor");
        }
        if (setting is "trading-desk" or "work-counter" or "review-table" or "session-open" or "session-close")
        {
            var trader = new CharacterIdentity("trader", brand.Code, "Trader", "oval", "crop", 0x1A1A1A, 0xE1B12C, "glasses", 28);
            var seated = demand with { Setting = "review-table", Pose = "point", Expression = "focus", Camera = "medium", CharacterId = null };
            DrawCast(canvas, seated, trader, brand, 180f);
            Laptop(canvas, 860, 560, brand);
        }
        return null;
    }

    private static Pack? Tea(SKCanvas canvas, VisualBrandStyle brand, string setting, List<string> parts)
    {
        parts.Add("terrace");
        FillRound(canvas, new SKRect(0, 2, Width, 280), Mix(brand.Accent, 0xFFFFFF, 0.55f), 0);
        Peak(canvas, 220, 70, 460, Mix(brand.Ink, brand.Background, 0.28f));
        Peak(canvas, 680, 28, 560, Mix(brand.Secondary, brand.Ink, 0.18f));
        Peak(canvas, 1100, 64, 420, Mix(brand.Ink, brand.Accent, 0.22f));
        using (var mist = Fill(Mix(brand.Background, 0xFFFFFF, 0.5f)))
        {
            mist.Color = mist.Color.WithAlpha(110);
            canvas.DrawOval(new SKRect(40, 150, 1240, 300), mist);
            canvas.DrawOval(new SKRect(180, 190, 980, 280), mist);
        }
        Hill(canvas, 240, Mix(brand.Secondary, 0xFFFFFF, 0.35f));
        Hill(canvas, 320, brand.Secondary);
        Hill(canvas, 420, Mix(brand.Secondary, brand.Accent, 0.25f));
        for (var row = 0; row < 6; row++)
        {
            var y0 = 248f + row * 18f + row * row * 5.5f;
            var y1 = y0 + 16f + row * 4f;
            var pinch = 520f - row * 70f;
            var lip = Mix(brand.Accent, 0x123018, 0.05f + row * 0.05f);
            var soil = Mix(brand.Prop, brand.Ink, 0.28f);
            using var band = new SKPath();
            band.MoveTo(pinch, y0);
            band.LineTo(Width - pinch, y0);
            band.LineTo(Width - pinch + 90 + row * 24, y1);
            band.LineTo(pinch - 90 - row * 24, y1);
            band.Close();
            using (var paint = Fill(lip))
                canvas.DrawPath(band, paint);
            FillRound(canvas, new SKRect(pinch - 90 - row * 24, y1 - 4, Width - pinch + 90 + row * 24, y1 + 8), soil, 0);
            var bushes = 6 + row * 2;
            for (var i = 0; i < bushes; i++)
            {
                var t = (i + 0.5f) / bushes;
                var x = pinch + (Width - pinch * 2) * t;
                var r = 8f + row * 3f;
                FillOval(canvas, new SKRect(x - r, y0 - r * 0.45f, x + r, y0 + r * 0.35f), Mix(brand.Accent, brand.Ink, 0.1f + (i % 3) * 0.07f));
            }
        }
        if (setting == "tea-pour")
        {
            parts.Add("pot"); parts.Add("stream");
            Pot(canvas, 520, 470, brand);
            StrokeLine(canvas, 600, 470, 720, 540, brand.Ink, brand.LineWeight);
            Cup(canvas, 760, 530, brand);
        }
        else if (setting == "tea-path")
        {
            parts.Add("path"); parts.Add("bush");
            FillOval(canvas, new SKRect(480, 520, 800, 680), Mix(brand.Prop, brand.Background, 0.15f));
            Bush(canvas, 220, 400, brand);
            Bush(canvas, 980, 380, brand);
        }
        else
        {
            parts.Add("cup"); parts.Add("round-table");
            FillOval(canvas, new SKRect(300, 590, 860, 710), Mix(brand.Ink, brand.Background, 0.78f));
            FillOval(canvas, new SKRect(340, 520, 820, 640), Mix(brand.Prop, brand.Ink, 0.08f));
            FillOval(canvas, new SKRect(380, 500, 780, 590), brand.Prop);
            FillOval(canvas, new SKRect(410, 512, 750, 560), Mix(brand.Prop, 0xFFFFFF, 0.2f));
            TeaPot(canvas, 470, 470, brand);
            Cup(canvas, 620, 478, brand);
            Cup(canvas, 700, 486, brand);
            FillOval(canvas, new SKRect(540, 500, 600, 528), Mix(brand.Accent, brand.Ink, 0.15f));
            for (var leaf = 0; leaf < 4; leaf++)
                FillOval(canvas, new SKRect(548 + leaf * 12, 496, 560 + leaf * 12, 512), brand.Accent);
        }
        return null;
    }

    private static Pack? Registered(SKCanvas canvas, VisualBrandStyle brand, string setting, List<string> parts)
    {
        parts.Add("registered-motif");
        var hash = 17;
        foreach (var ch in brand.Motif + "/" + setting)
            hash = hash * 31 + ch;
        var count = 3 + Math.Abs(hash) % 4;
        for (var i = 0; i < count; i++)
        {
            var x = 80 + ((hash >> (i * 3)) & 255) % 800;
            var y = 420 + (i * 30);
            if (((hash >> i) & 1) == 0) Plant(canvas, x, y - 80, brand);
            else Prism(canvas, x, y, 120, 70, 24, i % 2 == 0 ? brand.Accent : brand.Prop, brand.Ink);
        }
        parts.Add("n" + count);
        return null;
    }

    private static void Prism(SKCanvas canvas, float x, float y, float w, float h, float depth, uint color, uint ink)
    {
        var side = new SKPath();
        side.MoveTo(x + w, y - h);
        side.LineTo(x + w + depth, y - h - depth * 0.42f);
        side.LineTo(x + w + depth, y - depth * 0.42f);
        side.LineTo(x + w, y);
        side.Close();
        using (var paint = Fill(Mix(color, ink, 0.34f)))
            canvas.DrawPath(side, paint);
        var top = new SKPath();
        top.MoveTo(x, y - h);
        top.LineTo(x + depth * 0.7f, y - h - depth * 0.42f);
        top.LineTo(x + w + depth * 0.7f, y - h - depth * 0.42f);
        top.LineTo(x + w, y - h);
        top.Close();
        using (var paint = Fill(Mix(color, 0xFFFFFF, 0.24f)))
            canvas.DrawPath(top, paint);
        FillRound(canvas, new SKRect(x, y - h, x + w, y), Mix(color, ink, 0.08f), 3);
    }

    private static void Window(SKCanvas canvas, float x, float y, float w, float h, VisualBrandStyle brand)
    {
        Prism(canvas, x, y + h, w, h, 18, brand.Prop, brand.Ink);
        FillRound(canvas, new SKRect(x + 14, y + 14, x + w - 14, y + h - 14), Mix(0xFFFFFF, brand.Accent, 0.16f), 4);
        StrokeLine(canvas, x + w / 2, y + 14, x + w / 2, y + h - 14, brand.Ink, brand.LineWeight);
        StrokeLine(canvas, x + 8, y + h / 2, x + w - 8, y + h / 2, brand.Ink, brand.LineWeight);
        var light = new SKPath();
        light.MoveTo(x + 20, y + h);
        light.LineTo(x + w - 10, y + h);
        light.LineTo(x + w + 180, 640);
        light.LineTo(x - 40, 640);
        light.Close();
        using var glow = Fill(Mix(brand.Accent, 0xFFFFFF, 0.72f));
        glow.Color = glow.Color.WithAlpha(90);
        canvas.DrawPath(light, glow);
    }

    private static void Door(SKCanvas canvas, float x, float y, float w, float h, VisualBrandStyle brand)
    {
        Prism(canvas, x, y + h, w, h, 28, brand.Prop, brand.Ink);
        FillRound(canvas, new SKRect(x + 28, y + 28, x + w - 28, y + h - 36), Mix(brand.Accent, brand.Background, 0.3f), 4);
        FillOval(canvas, new SKRect(x + w - 58, y + h * 0.55f, x + w - 40, y + h * 0.55f + 18), brand.Secondary);
    }

    private static void Bowl(SKCanvas canvas, float x, float y, VisualBrandStyle brand)
    {
        FillOval(canvas, new SKRect(x - 40, y + 8, x + 40, y + 36), Mix(brand.Accent, brand.Ink, 0.15f));
        FillRound(canvas, new SKRect(x - 36, y - 6, x + 36, y + 24), brand.Accent, 12);
        FillOval(canvas, new SKRect(x - 30, y - 16, x + 30, y + 8), Mix(brand.Accent, 0xFFFFFF, 0.3f));
        Stroke(canvas, new SKRect(x - 30, y - 16, x + 30, y + 8), brand.Ink, 2);
    }

    private static void Cup(SKCanvas canvas, float x, float y, VisualBrandStyle brand)
    {
        FillRound(canvas, new SKRect(x - 18, y, x + 18, y + 36), brand.Background, 6);
        Stroke(canvas, new SKRect(x - 18, y, x + 18, y + 36), brand.Ink, 2);
        FillOval(canvas, new SKRect(x - 18, y - 8, x + 18, y + 8), Mix(brand.Background, brand.Accent, 0.25f));
        StrokeArc(canvas, x + 22, y + 16, 10, 10, brand.Ink, -70, 200, 2);
    }

    private static void Plant(SKCanvas canvas, float x, float y, VisualBrandStyle brand)
    {
        Prism(canvas, x - 24, y + 90, 48, 46, 16, brand.Prop, brand.Ink);
        FillOval(canvas, new SKRect(x - 40, y, x + 10, y + 56), brand.Secondary);
        FillOval(canvas, new SKRect(x - 8, y - 16, x + 46, y + 48), Mix(brand.Secondary, brand.Accent, 0.35f));
        FillOval(canvas, new SKRect(x - 16, y + 20, x + 28, y + 70), brand.Accent);
    }

    private static void Tree(SKCanvas canvas, float x, float y, VisualBrandStyle brand)
    {
        using var trunk = new SKPath();
        trunk.MoveTo(x - 8, y + 150);
        trunk.LineTo(x + 8, y + 150);
        trunk.LineTo(x + 5, y + 46);
        trunk.LineTo(x - 5, y + 46);
        trunk.Close();
        using (var wood = Fill(Mix(brand.Prop, brand.Ink, 0.35f)))
            canvas.DrawPath(trunk, wood);
        using var canopy = new SKPath();
        canopy.MoveTo(x, y + 78);
        canopy.CubicTo(x - 96, y + 64, x - 88, y - 8, x - 18, y - 28);
        canopy.CubicTo(x - 8, y - 72, x + 36, y - 64, x + 22, y - 16);
        canopy.CubicTo(x + 98, y - 24, x + 104, y + 58, x + 6, y + 78);
        canopy.Close();
        using (var leaf = Fill(brand.Secondary))
            canvas.DrawPath(canopy, leaf);
        using (var shade = Fill(Mix(brand.Secondary, brand.Ink, 0.22f)))
            canvas.DrawOval(new SKRect(x - 18, y + 10, x + 54, y + 62), shade);
        using (var light = Fill(Mix(brand.Secondary, 0xFFFFFF, 0.28f)))
            canvas.DrawOval(new SKRect(x - 36, y - 18, x + 8, y + 16), light);
        using var stroke = Line(brand.Ink, 2f);
        canvas.DrawPath(canopy, stroke);
    }

    private static void WallCabinet(SKCanvas canvas, float x, float y, float w, float h, VisualBrandStyle brand)
    {
        var wood = Mix(brand.Prop, brand.Ink, 0.08f);
        FillRound(canvas, new SKRect(x, y, x + w, y + h), wood, 6);
        FillRound(canvas, new SKRect(x + w - 16, y + 6, x + w, y + h), Mix(brand.Prop, brand.Ink, 0.28f), 0);
        Stroke(canvas, new SKRect(x, y, x + w, y + h), brand.Ink, 2f);
        StrokeLine(canvas, x + w / 2f, y + 10, x + w / 2f, y + h - 10, brand.Ink, 1.6f);
        FillRound(canvas, new SKRect(x + w / 2f - 28, y + h / 2f - 4, x + w / 2f - 12, y + h / 2f + 4), brand.Ink, 2);
        FillRound(canvas, new SKRect(x + w / 2f + 12, y + h / 2f - 4, x + w / 2f + 28, y + h / 2f + 4), brand.Ink, 2);
    }

    private static void Peak(SKCanvas canvas, float cx, float top, float half, uint color)
    {
        using var path = new SKPath();
        path.MoveTo(cx - half, 250);
        path.LineTo(cx, top);
        path.LineTo(cx + half * 0.72f, 250);
        path.Close();
        using var paint = Fill(color);
        canvas.DrawPath(path, paint);
        using var snow = new SKPath();
        snow.MoveTo(cx - 28, top + 36);
        snow.LineTo(cx, top);
        snow.LineTo(cx + 22, top + 30);
        snow.Close();
        using var cap = Fill(Mix(color, 0xFFFFFF, 0.55f));
        canvas.DrawPath(snow, cap);
    }

    private static void Lamp(SKCanvas canvas, float x, float y, VisualBrandStyle brand)
    {
        FillOval(canvas, new SKRect(x - 28, y + 250, x + 28, y + 268), brand.Prop);
        StrokeLine(canvas, x, y + 36, x, y + 255, brand.Prop, 8);
        FillOval(canvas, new SKRect(x - 46, y, x + 46, y + 54), brand.Secondary);
        FillOval(canvas, new SKRect(x - 20, y + 8, x + 10, y + 28), Mix(brand.Secondary, 0xFFFFFF, 0.35f));
    }

    private static void Stall(SKCanvas canvas, float x, uint awning, VisualBrandStyle brand)
    {
        Prism(canvas, x, 600, 320, 180, 40, brand.Prop, brand.Ink);
        var path = new SKPath();
        path.MoveTo(x, 430);
        path.QuadTo(x + 160, 300, x + 320, 430);
        path.LineTo(x, 430);
        path.Close();
        using var paint = Fill(awning);
        canvas.DrawPath(path, paint);
    }

    private static void ShelfRun(SKCanvas canvas, float x, float y, VisualBrandStyle brand)
    {
        Prism(canvas, x, y + 20, 760, 16, 18, brand.Ink, brand.Ink);
        Prism(canvas, x, y + 150, 760, 16, 18, brand.Ink, brand.Ink);
    }

    private static void Cabinet(SKCanvas canvas, float x, float y, float w, float h, VisualBrandStyle brand)
    {
        var body = Mix(brand.Background, brand.Ink, 0.16f);
        using var side = new SKPath();
        side.MoveTo(x + w, y);
        side.LineTo(x + w + 28, y + 16);
        side.LineTo(x + w + 28, y + h + 16);
        side.LineTo(x + w, y + h);
        side.Close();
        using (var paint = Fill(Mix(brand.Ink, brand.Background, 0.22f)))
            canvas.DrawPath(side, paint);
        FillRound(canvas, new SKRect(x, y, x + w, y + h), body, 4);
        Stroke(canvas, new SKRect(x, y, x + w, y + h), brand.Ink, 2.2f);
        StrokeLine(canvas, x + w / 3f, y + 8, x + w / 3f, y + h - 8, brand.Ink, 1.4f);
        StrokeLine(canvas, x + 2 * w / 3f, y + 8, x + 2 * w / 3f, y + h - 8, brand.Ink, 1.4f);
        for (var row = 0; row < 3; row++)
        {
            var sy = y + 36 + row * ((h - 20) / 3f);
            Prism(canvas, x + 18, sy, w - 36, 8, 10, brand.Ink, brand.Ink);
            for (var col = 0; col < 6; col++)
            {
                var px = x + 56 + col * ((w - 70) / 6f);
                switch ((row * 3 + col) % 5)
                {
                    case 0:
                        MedicineBox(canvas, px, sy - 4, 32, 38, brand.Accent, brand.Ink);
                        break;
                    case 1:
                        MedicineBox(canvas, px, sy - 2, 26, 50, brand.Secondary, brand.Ink);
                        break;
                    case 2:
                        Bottle(canvas, px, sy - 58, 48, brand.Accent, brand.Ink);
                        break;
                    case 3:
                        Bottle(canvas, px, sy - 42, 32, Mix(brand.Secondary, 0xFFFFFF, 0.25f), brand.Ink);
                        break;
                    default:
                        Blister(canvas, px, sy - 8, brand.Accent, brand.Ink);
                        break;
                }
            }
        }
    }

    private static void Blister(SKCanvas canvas, float x, float y, uint color, uint ink)
    {
        FillRound(canvas, new SKRect(x - 22, y - 36, x + 22, y), Mix(color, 0xFFFFFF, 0.55f), 4);
        Stroke(canvas, new SKRect(x - 22, y - 36, x + 22, y), ink, 1.6f);
        for (var row = 0; row < 2; row++)
            for (var col = 0; col < 3; col++)
                FillOval(canvas, new SKRect(x - 16 + col * 12, y - 30 + row * 14, x - 8 + col * 12, y - 20 + row * 14), Mix(color, 0xFFFFFF, 0.15f));
    }

    private static void MedicineBox(SKCanvas canvas, float x, float y, float w, float h, uint color, uint ink)
    {
        Prism(canvas, x - w / 2, y, w, h, 10, color, ink);
        FillRound(canvas, new SKRect(x - w / 2 + 6, y - h + 8, x + w / 2 - 6, y - h + 18), Mix(color, 0xFFFFFF, 0.55f), 2);
    }

    private static void Jar(SKCanvas canvas, float x, float y, VisualBrandStyle brand)
    {
        var glass = Mix(brand.Prop, 0xFFFFFF, 0.35f);
        FillRound(canvas, new SKRect(x - 26, y, x + 26, y + 52), glass, 10);
        FillRound(canvas, new SKRect(x - 20, y + 8, x - 12, y + 40), Mix(glass, 0xFFFFFF, 0.45f), 3);
        FillRound(canvas, new SKRect(x - 28, y - 10, x + 28, y + 6), brand.Secondary, 3);
        Stroke(canvas, new SKRect(x - 26, y, x + 26, y + 52), brand.Ink, 1.8f);
        Stroke(canvas, new SKRect(x - 28, y - 10, x + 28, y + 6), brand.Ink, 1.6f);
    }

    private static void Bottle(SKCanvas canvas, float x, float y, float body, uint glass, uint ink)
    {
        FillOval(canvas, new SKRect(x - 18, y + body - 12, x + 18, y + body + 8), Mix(glass, ink, 0.2f));
        FillRound(canvas, new SKRect(x - 16, y, x + 16, y + body), glass, 8);
        FillRound(canvas, new SKRect(x - 6, y - 4, x - 1, y + body - 8), Mix(glass, 0xFFFFFF, 0.45f), 2);
        FillRound(canvas, new SKRect(x - 7, y - 22, x + 7, y + 4), Mix(glass, 0xFFFFFF, 0.15f), 3);
        FillRound(canvas, new SKRect(x - 9, y - 32, x + 9, y - 18), ink, 3);
    }

    private static void TallBay(SKCanvas canvas, float x, VisualBrandStyle brand)
    {
        Prism(canvas, x, 560, 280, 420, 36, Mix(brand.Prop, brand.Ink, 0.2f), brand.Ink);
        for (var row = 0; row < 3; row++)
        {
            Bottle(canvas, x + 70, 180 + row * 120, 54, row % 2 == 0 ? brand.Accent : brand.Secondary, brand.Ink);
            Bottle(canvas, x + 140, 190 + row * 120, 46, brand.Background, brand.Ink);
        }
    }

    private static void Chair(SKCanvas canvas, float x, VisualBrandStyle brand)
    {
        Prism(canvas, x, 560, 140, 36, 30, brand.Accent, brand.Ink);
        Prism(canvas, x, 420, 18, 160, 12, brand.Ink, brand.Ink);
    }

    private static void Clock(SKCanvas canvas, float x, float y, VisualBrandStyle brand)
    {
        FillOval(canvas, new SKRect(x - 46, y - 46, x + 46, y + 46), brand.Background);
        Stroke(canvas, new SKRect(x - 46, y - 46, x + 46, y + 46), brand.Ink, 4);
        StrokeLine(canvas, x, y, x, y - 26, brand.Ink, 3);
        StrokeLine(canvas, x, y, x + 18, y + 8, brand.Ink, 3);
    }

    private static void Plus(SKCanvas canvas, float x, float y, uint color)
    {
        FillRound(canvas, new SKRect(x - 10, y - 36, x + 10, y + 36), color, 4);
        FillRound(canvas, new SKRect(x - 36, y - 10, x + 36, y + 10), color, 4);
    }

    private static void Monitor(SKCanvas canvas, SKRect panel, VisualBrandStyle brand)
    {
        FillRound(canvas, new SKRect(panel.Left - 16, panel.Top - 16, panel.Right + 16, panel.Bottom + 16), Mix(brand.Ink, brand.Background, 0.2f), 12);
        FillRound(canvas, panel, Mix(brand.Background, brand.Ink, 0.16f), 8);
        var faint = Mix(brand.Ink, 0xFFFFFF, 0.7f);
        for (var x = panel.Left + 28; x < panel.Right - 8; x += 36)
            StrokeLine(canvas, x, panel.Top + 16, x, panel.Bottom - 16, faint, 1);
        for (var y = panel.Top + 28; y < panel.Bottom - 8; y += 32)
            StrokeLine(canvas, panel.Left + 16, y, panel.Right - 16, y, faint, 1);
        StrokeLine(canvas, panel.Left + 20, panel.Bottom - 24, panel.Right - 16, panel.Bottom - 24, brand.Ink, 2);
        StrokeLine(canvas, panel.Left + 20, panel.Top + 16, panel.Left + 20, panel.Bottom - 24, brand.Ink, 2);
    }

    private static void Candles(SKCanvas canvas, SKRect panel, int count, VisualBrandStyle brand)
    {
        var left = panel.Left + 28;
        var right = panel.Right - 16;
        var top = panel.Top + 28;
        var baseY = panel.Bottom - 54;
        var span = MathF.Max(40f, baseY - top);
        var grid = Mix(brand.Ink, 0xFFFFFF, 0.72f);
        for (var row = 0; row < 4; row++)
        {
            var y = top + span * row / 3f;
            StrokeLine(canvas, left, y, right, y, grid, 1f);
        }
        StrokeLine(canvas, left, top, left, panel.Bottom - 18, brand.Ink, 1.6f);
        StrokeLine(canvas, left, baseY, right, baseY, brand.Ink, 1.6f);
        float[] move = [0.42f, 0.48f, 0.4f, 0.55f, 0.5f, 0.62f, 0.58f, 0.7f, 0.64f, 0.78f, 0.72f, 0.86f, 0.8f, 0.9f];
        var n = Math.Clamp(count, 4, move.Length);
        using var average = new SKPath();
        var started = false;
        for (var i = 0; i < n; i++)
        {
            var x = left + 16 + (i + 0.5f) * ((right - left - 24) / n);
            var open = move[i];
            var close = move[Math.Min(move.Length - 1, i + 1)];
            var high = MathF.Max(open, close) + 0.06f;
            var low = MathF.Min(open, close) - 0.05f;
            float Y(float v) => baseY - Math.Clamp(v, 0.05f, 0.98f) * span;
            var up = close >= open;
            StrokeLine(canvas, x, Y(high), x, Y(low), brand.Ink, 1.6f);
            var bodyTop = MathF.Min(Y(open), Y(close));
            var bodyBot = MathF.Max(Y(open), Y(close));
            if (bodyBot - bodyTop < 3) bodyBot = bodyTop + 3;
            FillRound(canvas, new SKRect(x - 7, bodyTop, x + 7, bodyBot), up ? brand.Accent : brand.Secondary, 1);
            var vol = 8 + (i % 4) * 6;
            FillRound(canvas, new SKRect(x - 5, panel.Bottom - 16 - vol, x + 5, panel.Bottom - 16), Mix(up ? brand.Accent : brand.Secondary, brand.Background, 0.25f), 1);
            var ay = Y((open + close) * 0.5f);
            if (!started)
            {
                average.MoveTo(x, ay);
                started = true;
            }
            else average.LineTo(x, ay);
        }
        using var line = Line(Mix(brand.Accent, 0xFFFFFF, 0.35f), 2f);
        canvas.DrawPath(average, line);
    }

    private static void Laptop(SKCanvas canvas, float x, float y, VisualBrandStyle brand)
    {
        FillRound(canvas, new SKRect(x, y, x + 150, y + 14), Mix(brand.Ink, brand.Background, 0.25f), 4);
        FillRound(canvas, new SKRect(x + 18, y - 78, x + 132, y + 4), Mix(brand.Background, brand.Ink, 0.2f), 4);
        FillRound(canvas, new SKRect(x + 26, y - 70, x + 124, y - 8), Mix(brand.Accent, brand.Background, 0.55f), 2);
        Stroke(canvas, new SKRect(x + 18, y - 78, x + 132, y + 4), brand.Ink, 1.6f);
    }

    private static void TeaPot(SKCanvas canvas, float x, float y, VisualBrandStyle brand)
    {
        var clay = Mix(brand.Prop, brand.Ink, 0.22f);
        FillOval(canvas, new SKRect(x - 8, y + 28, x + 46, y + 42), Mix(brand.Ink, brand.Background, 0.7f));
        FillRound(canvas, new SKRect(x, y, x + 54, y + 36), clay, 12);
        FillOval(canvas, new SKRect(x + 6, y - 8, x + 48, y + 14), Mix(clay, 0xFFFFFF, 0.18f));
        FillRound(canvas, new SKRect(x + 16, y - 14, x + 38, y + 2), Mix(clay, 0x000000, 0.12f), 4);
        using var spout = new SKPath();
        spout.MoveTo(x + 48, y + 10);
        spout.CubicTo(x + 78, y + 4, x + 86, y + 18, x + 70, y + 26);
        spout.LineTo(x + 50, y + 22);
        spout.Close();
        using (var fill = Fill(clay))
            canvas.DrawPath(spout, fill);
        using var handle = Line(brand.Ink, 3f);
        canvas.DrawArc(new SKRect(x - 16, y + 4, x + 10, y + 32), 80, 200, false, handle);
        Stroke(canvas, new SKRect(x, y, x + 54, y + 36), brand.Ink, 1.7f);
    }

    private static void InfoChart(SKCanvas canvas, VisualBrandStyle brand)
    {
        var card = new SKRect(690, 78, 1220, 430);
        FillRound(canvas, card, Mix(brand.Background, 0xFFFFFF, 0.08f), 16);
        Stroke(canvas, card, brand.Ink, 2f);
        var plot = new SKRect(card.Left + 36, card.Top + 36, card.Right - 24, card.Bottom - 28);
        StrokeLine(canvas, plot.Left, plot.Top, plot.Left, plot.Bottom, brand.Ink, 1.6f);
        StrokeLine(canvas, plot.Left, plot.Bottom, plot.Right, plot.Bottom, brand.Ink, 1.6f);
        float[] bars = [0.35f, 0.62f, 0.48f, 0.8f, 0.7f, 0.92f];
        uint[] tones = [brand.Accent, brand.Secondary, brand.Ink, brand.Accent, brand.Secondary, brand.Accent];
        var gap = plot.Width / bars.Length;
        for (var i = 0; i < bars.Length; i++)
        {
            var h = (plot.Height - 16) * bars[i];
            var x = plot.Left + 10 + i * gap;
            FillRound(canvas, new SKRect(x, plot.Bottom - h, x + gap * 0.62f, plot.Bottom), tones[i], 3);
        }
        using var trend = new SKPath();
        trend.MoveTo(plot.Left + 16, plot.Bottom - (plot.Height - 16) * 0.3f);
        trend.CubicTo(plot.Left + plot.Width * 0.35f, plot.Bottom - plot.Height * 0.7f, plot.Left + plot.Width * 0.6f, plot.Bottom - plot.Height * 0.4f, plot.Right - 8, plot.Top + 18);
        using var trendPaint = Line(brand.Ink, 2.4f);
        canvas.DrawPath(trend, trendPaint);
        FillOval(canvas, new SKRect(card.Left + 28, card.Bottom - 22, card.Left + 44, card.Bottom - 8), brand.Accent);
        FillOval(canvas, new SKRect(card.Left + 56, card.Bottom - 22, card.Left + 72, card.Bottom - 8), brand.Secondary);
        FillRound(canvas, new SKRect(card.Right - 70, card.Top + 16, card.Right - 28, card.Top + 40), brand.Accent, 4);
    }

    private static void Hill(SKCanvas canvas, float y, uint fill)
    {
        var path = new SKPath();
        path.MoveTo(0, Height);
        path.LineTo(0, y + 80);
        path.CubicTo(180, y, 420, y + 40, 640, y + 20);
        path.CubicTo(860, y, 1080, y + 70, Width, y + 10);
        path.LineTo(Width, Height);
        path.Close();
        using var paint = Fill(fill);
        canvas.DrawPath(path, paint);
    }

    private static void Pot(SKCanvas canvas, float x, float y, VisualBrandStyle brand)
    {
        FillOval(canvas, new SKRect(x - 36, y - 10, x + 36, y + 28), brand.Prop);
        StrokeLine(canvas, x + 30, y, x + 70, y - 24, brand.Ink, brand.LineWeight);
    }

    private static void Bush(SKCanvas canvas, float x, float y, VisualBrandStyle brand)
    {
        FillOval(canvas, new SKRect(x - 36, y, x + 36, y + 48), brand.Accent);
        FillOval(canvas, new SKRect(x - 10, y - 8, x + 16, y + 16), Mix(brand.Accent, 0xFFFFFF, 0.2f));
    }

    private static void Limb(SKCanvas canvas, float x0, float y0, float x1, float y1, float radius, uint color)
    {
        using var paint = new SKPaint
        {
            Color = C(color),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = radius * 2,
            StrokeCap = SKStrokeCap.Round,
        };
        canvas.DrawLine(x0, y0, x1, y1, paint);
    }

    private static void FillRound(SKCanvas canvas, SKRect rect, uint color, float radius)
    {
        using var paint = Fill(color);
        canvas.DrawRoundRect(rect, radius, radius, paint);
    }

    private static void FillOval(SKCanvas canvas, SKRect rect, uint color)
    {
        using var paint = Fill(color);
        canvas.DrawOval(rect, paint);
    }

    private static void Stroke(SKCanvas canvas, SKRect rect, uint color, float width)
    {
        using var paint = new SKPaint
        {
            Color = C(color),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(1, width),
        };
        canvas.DrawRoundRect(rect, 6, 6, paint);
    }

    private static void StrokeLine(SKCanvas canvas, float x0, float y0, float x1, float y1, uint color, float width)
    {
        using var paint = new SKPaint
        {
            Color = C(color),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(1, width),
            StrokeCap = SKStrokeCap.Round,
        };
        canvas.DrawLine(x0, y0, x1, y1, paint);
    }

    private static void StrokeArc(SKCanvas canvas, float cx, float cy, float rx, float ry, uint color, float start, float sweep, float width)
    {
        using var paint = new SKPaint
        {
            Color = C(color),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(1, width),
            StrokeCap = SKStrokeCap.Round,
        };
        canvas.DrawArc(new SKRect(cx - rx, cy - ry, cx + rx, cy + ry), start, sweep, false, paint);
    }

    private static void Dot(SKCanvas canvas, float x, float y, uint color)
    {
        FillOval(canvas, new SKRect(x - 2.2f, y - 2.2f, x + 2.2f, y + 2.2f), color);
    }

    private static SKPaint Fill(uint color) => new()
    {
        Color = C(color),
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
    };

    private static SKColor C(uint rgb) => new((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));

    private static uint Mix(uint a, uint b, float t)
    {
        byte Channel(int shift)
        {
            var av = (int)((a >> shift) & 0xFF);
            var bv = (int)((b >> shift) & 0xFF);
            return (byte)(av + (bv - av) * t);
        }
        return ((uint)Channel(16) << 16) | ((uint)Channel(8) << 8) | Channel(0);
    }

    private static byte[] RgbOf(SKBitmap bitmap)
    {
        var pixels = bitmap.Pixels;
        var rgb = new byte[pixels.Length * 3];
        for (var i = 0; i < pixels.Length; i++)
        {
            rgb[i * 3] = pixels[i].Red;
            rgb[i * 3 + 1] = pixels[i].Green;
            rgb[i * 3 + 2] = pixels[i].Blue;
        }
        return rgb;
    }
}
