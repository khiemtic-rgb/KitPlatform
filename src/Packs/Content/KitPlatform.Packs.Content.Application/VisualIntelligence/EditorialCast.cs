using SkiaSharp;

namespace KitPlatform.Packs.Content.VisualIntelligence;

public static partial class StudioScenePainter
{
    private readonly record struct Wardrobe(string Cut, uint Cloth, uint Trim, uint Pants, bool Scarf, bool Hat);

    private static void DrawEditorial(
        SKCanvas canvas,
        ShotVisualDemand demand,
        CharacterIdentity who,
        VisualBrandStyle brand,
        P hip,
        P shoulder,
        P head,
        P shoulderL,
        P elbowL,
        P handL,
        P shoulderR,
        P elbowR,
        P handR,
        P hipL,
        P kneeL,
        P footL,
        P hipR,
        P kneeR,
        P footR,
        uint pants)
    {
        var ink = brand.Ink;
        var turn = ViewTurn(demand);
        var wear = ResolveWardrobe(demand, who, brand, pants);
        if (turn is >= 0.95f and < 1.5f)
        {
            P Flat(P p) => new(hip.X + (p.X - hip.X) * 0.38f + 10f, p.Y);
            hip = Flat(hip);
            shoulder = Flat(shoulder);
            head = Flat(head);
            shoulderL = Flat(shoulderL);
            elbowL = Flat(elbowL);
            handL = Flat(handL);
            shoulderR = Flat(shoulderR);
            elbowR = Flat(elbowR);
            handR = Flat(handR);
            hipL = Flat(hipL);
            kneeL = Flat(kneeL);
            footL = Flat(footL);
            hipR = Flat(hipR);
            kneeR = Flat(kneeR);
            footR = Flat(footR);
        }

        var skin = brand.Skin;
        using (var shadow = Fill(Mix(ink, brand.Background, 0.35f)))
        {
            shadow.Color = shadow.Color.WithAlpha(64);
            canvas.DrawOval(new SKRect(hip.X - 86, -6, hip.X + 78, 18), shadow);
        }

        var seated = demand.Pose is "sit" or "write";
        if (seated)
        {
            Seat(canvas, hip, brand);
            SeatBack(canvas, hip, shoulder, brand);
        }

        FillRound(canvas, new SKRect(hip.X - 34, hip.Y - 6, hip.X + 34, hip.Y + 36), wear.Pants, 10);
        var rearLeg = footL.Y >= footR.Y;
        if (rearLeg)
        {
            Trouser(canvas, hipL, kneeL, footL, wear.Pants, ink);
            Trouser(canvas, hipR, kneeR, footR, wear.Pants, ink);
        }
        else
        {
            Trouser(canvas, hipR, kneeR, footR, wear.Pants, ink);
            Trouser(canvas, hipL, kneeL, footL, wear.Pants, ink);
        }
        ShoeOf(canvas, footL, hipL);
        ShoeOf(canvas, footR, hipR);

        Sleeve(canvas, Tuck(shoulderL, hip), elbowL, handL, wear.Cloth, ink);
        Sleeve(canvas, Tuck(shoulderR, hip), elbowR, handR, wear.Cloth, ink);

        if (wear.Cut == "sport")
            Hood(canvas, head, who);
        TorsoOf(canvas, hip, shoulder, wear, who.OutfitColor, skin, ink);
        if (wear.Cut is "coat" or "blouse")
            Lapels(canvas, hip, shoulder, ink);
        if (wear.Cut is "apron" or "overalls")
            Apron(canvas, hip, shoulder, brand, ink);
        CuffBand(canvas, elbowL, handL, wear.Cloth, wear.Trim, ink);
        CuffBand(canvas, elbowR, handR, wear.Cloth, wear.Trim, ink);

        if (turn < 1.5f)
            NeckColumn(canvas, shoulder, head, skin, ink);
        if (wear.Scarf)
            Scarf(canvas, shoulder, head);

        if (turn >= 1.5f)
            BackHead(canvas, head, who, ink);
        else if (turn >= 0.95f)
            ProfileHead(canvas, head, who, demand.Expression, skin, ink);
        else
            FrontHead(canvas, head, who, demand.Expression, skin, ink, turn, wear.Hat);

        var pointLeft = demand.Pose == "point";
        HandOf(canvas, elbowL, handL, skin, ink, pointLeft ? "point" : "open");
        HandOf(canvas, elbowR, handR, skin, ink, "open");
        PropOf(canvas, demand, brand, elbowL, handL, elbowR, handR);
    }

    private static float ViewTurn(ShotVisualDemand demand)
    {
        if (demand.Tokens is null) return 0;
        foreach (var token in demand.Tokens)
        {
            if (token.Equals("view:back", StringComparison.OrdinalIgnoreCase)) return 2f;
            if (token.Equals("view:profile", StringComparison.OrdinalIgnoreCase)) return 1f;
            if (token.Equals("view:three-quarter", StringComparison.OrdinalIgnoreCase)
                || token.Equals("view:3/4", StringComparison.OrdinalIgnoreCase))
                return 0.55f;
        }
        return 0;
    }

    private static Wardrobe ResolveWardrobe(ShotVisualDemand demand, CharacterIdentity who, VisualBrandStyle brand, uint pants)
    {
        var cut = "shirt";
        var hat = brand.Motif == "tea-terrace";
        if (demand.Tokens is not null)
        {
            foreach (var token in demand.Tokens)
            {
                if (token.StartsWith("garment:", StringComparison.OrdinalIgnoreCase) && token.Length > 8)
                    cut = token[8..].ToLowerInvariant();
                if (token.Equals("hat", StringComparison.OrdinalIgnoreCase)) hat = true;
                if (token.Equals("no-hat", StringComparison.OrdinalIgnoreCase)) hat = false;
            }
        }

        var cloth = who.OutfitColor;
        var trim = Mix(cloth, 0xFFFFFF, 0.34f);
        var scarf = false;
        switch (cut)
        {
            case "coat":
            case "blouse":
                cloth = 0xF7F5F1;
                trim = who.OutfitColor;
                break;
            case "family":
                cloth = 0xC4554A;
                trim = 0xE7B2A4;
                break;
            case "sport":
                cloth = 0x3E6B4A;
                trim = 0xF4F1E8;
                break;
            case "outdoor":
                cloth = 0xC6A882;
                trim = 0xE7D7C0;
                scarf = true;
                break;
            case "apron":
            case "overalls":
                trim = Mix(who.OutfitColor, 0xFFFFFF, 0.4f);
                break;
        }
        return new Wardrobe(cut, cloth, trim, pants, scarf, hat);
    }

    private static void Seat(SKCanvas canvas, P hip, VisualBrandStyle brand)
    {
        var wood = Mix(brand.Prop, brand.Ink, 0.12f);
        var seat = hip.Y + 8;
        FillRound(canvas, new SKRect(hip.X - 46, seat, hip.X - 34, 8), wood, 3);
        FillRound(canvas, new SKRect(hip.X + 34, seat, hip.X + 46, 8), wood, 3);
        FillRound(canvas, new SKRect(hip.X - 62, seat - 10, hip.X + 62, seat + 12), Mix(brand.Prop, 0xFFFFFF, 0.18f), 8);
        Stroke(canvas, new SKRect(hip.X - 62, seat - 10, hip.X + 62, seat + 12), brand.Ink, 2f);
    }

    private static void SeatBack(SKCanvas canvas, P hip, P shoulder, VisualBrandStyle brand)
    {
        var wood = Mix(brand.Prop, brand.Ink, 0.2f);
        var top = shoulder.Y - 8;
        FillRound(canvas, new SKRect(hip.X - 78, top, hip.X - 64, hip.Y + 10), wood, 4);
        FillRound(canvas, new SKRect(hip.X + 64, top, hip.X + 78, hip.Y + 10), wood, 4);
        FillRound(canvas, new SKRect(hip.X - 80, top - 10, hip.X + 80, top + 8), wood, 6);
        Stroke(canvas, new SKRect(hip.X - 80, top - 10, hip.X + 80, top + 8), brand.Ink, 1.8f);
    }

    private static void Trouser(SKCanvas canvas, P hip, P knee, P foot, uint color, uint ink)
    {
        RoundLimb(canvas, hip, knee, foot, 23, 16, 12, color, ink);
    }

    private static P Tuck(P shoulder, P hip) => new(shoulder.X * 0.72f + hip.X * 0.28f, shoulder.Y + 10f);

    private static void ShoeOf(SKCanvas canvas, P foot, P hip)
    {
        var side = foot.X < hip.X ? -1f : 1f;
        FillRound(canvas, new SKRect(foot.X - 14, foot.Y - 5, foot.X + 16 + side * 4, foot.Y + 9), 0x2A2622, 5);
        FillRound(canvas, new SKRect(foot.X - 12, foot.Y + 4, foot.X + 18 + side * 4, foot.Y + 10), 0x1A1816, 2);
    }

    private static void Sleeve(SKCanvas canvas, P shoulder, P elbow, P hand, uint color, uint ink)
    {
        RoundLimb(canvas, shoulder, elbow, Along(elbow, hand, 0.86f), 16, 13, 11, color, ink);
    }

    private static void CuffBand(SKCanvas canvas, P elbow, P wrist, uint cloth, uint trim, uint ink)
    {
        var start = Along(elbow, wrist, 0.78f);
        RoundLimb(canvas, start, Along(elbow, wrist, 0.9f), wrist, 12, 11, 11, Mix(cloth, trim, 0.45f), ink);
    }

    private static void RoundLimb(SKCanvas canvas, P a, P b, P c, float w0, float w1, float w2, uint color, uint ink)
    {
        var outer = OffsetChain([a, b, c], w0, w1, w2, 1);
        var inner = OffsetChain([a, b, c], w0, w1, w2, -1);
        using var path = new SKPath();
        path.MoveTo(outer[0].X, outer[0].Y);
        path.CubicTo(outer[1].X, outer[1].Y, outer[1].X, outer[1].Y, outer[2].X, outer[2].Y);
        path.LineTo(inner[2].X, inner[2].Y);
        path.CubicTo(inner[1].X, inner[1].Y, inner[1].X, inner[1].Y, inner[0].X, inner[0].Y);
        path.Close();
        Cloth(canvas, path, color, ink);
    }

    private static void TorsoOf(SKCanvas canvas, P hip, P shoulder, Wardrobe wear, uint identity, uint skin, uint ink)
    {
        var dx = shoulder.X - hip.X;
        var dy = shoulder.Y - hip.Y;
        var len = MathF.Max(1f, MathF.Sqrt(dx * dx + dy * dy));
        var nx = -dy / len;
        var ny = dx / len;
        var ax = dx / len;
        var ay = dy / len;
        P AtBody(float along, float across) => new(
            hip.X + ax * along + nx * across,
            hip.Y + ay * along + ny * across);

        _ = skin;
        var cloth = wear.Cloth;
        using var path = new SKPath();
        var shoulderAcross = 48f;
        var waistAcross = 34f;
        var hemAcross = 40f;
        var hemAlong = -14f;
        var neck = len - 2f;
        path.MoveTo(AtBody(neck, -16).X, AtBody(neck, -16).Y);
        path.QuadTo(AtBody(len - 6, -shoulderAcross).X, AtBody(len - 6, -shoulderAcross).Y, AtBody(len * 0.7f, -waistAcross - 4).X, AtBody(len * 0.7f, -waistAcross - 4).Y);
        path.QuadTo(AtBody(len * 0.3f, -hemAcross).X, AtBody(len * 0.3f, -hemAcross).Y, AtBody(hemAlong, -hemAcross + 6).X, AtBody(hemAlong, -hemAcross + 6).Y);
        path.QuadTo(AtBody(hemAlong - 6, 0).X, AtBody(hemAlong - 6, 0).Y, AtBody(hemAlong, hemAcross - 6).X, AtBody(hemAlong, hemAcross - 6).Y);
        path.QuadTo(AtBody(len * 0.3f, hemAcross).X, AtBody(len * 0.3f, hemAcross).Y, AtBody(len * 0.7f, waistAcross + 4).X, AtBody(len * 0.7f, waistAcross + 4).Y);
        path.QuadTo(AtBody(len - 6, shoulderAcross).X, AtBody(len - 6, shoulderAcross).Y, AtBody(neck, 16).X, AtBody(neck, 16).Y);
        path.QuadTo(AtBody(neck - 10, 0).X, AtBody(neck - 10, 0).Y, AtBody(neck, -16).X, AtBody(neck, -16).Y);
        path.Close();
        Cloth(canvas, path, cloth, ink);
        canvas.Save();
        canvas.ClipPath(path, SKClipOperation.Intersect, true);
        using (var shade = Fill(Mix(cloth, 0x000000, 0.16f)))
        {
            shade.Color = shade.Color.WithAlpha(90);
            var side = AtBody(len * 0.4f, waistAcross);
            canvas.DrawOval(new SKRect(side.X - 28, side.Y - 40, side.X + 18, side.Y + 70), shade);
        }
        canvas.Restore();

        if (wear.Cut is "coat" or "blouse")
        {
            using var placket = new SKPath();
            placket.MoveTo(AtBody(neck - 2, -10).X, AtBody(neck - 2, -10).Y);
            placket.LineTo(AtBody(hemAlong + 16, -8).X, AtBody(hemAlong + 16, -8).Y);
            placket.LineTo(AtBody(hemAlong + 16, 8).X, AtBody(hemAlong + 16, 8).Y);
            placket.LineTo(AtBody(neck - 2, 10).X, AtBody(neck - 2, 10).Y);
            placket.Close();
            Cloth(canvas, placket, identity, ink);
        }

        if (wear.Cut == "sport")
        {
            var bandA = AtBody(len * 0.42f, -waistAcross + 2);
            var bandB = AtBody(len * 0.42f, waistAcross - 2);
            StrokeLine(canvas, bandA.X, bandA.Y, bandB.X, bandB.Y, wear.Trim, 6f);
        }
    }

    private static void Lapels(SKCanvas canvas, P hip, P shoulder, uint ink)
    {
        var mid = new P((hip.X + shoulder.X) / 2f, (hip.Y + shoulder.Y) / 2f);
        StrokeLine(canvas, shoulder.X - 16, shoulder.Y + 8, mid.X - 12, hip.Y - 6, ink, 2f);
        StrokeLine(canvas, shoulder.X + 16, shoulder.Y + 8, mid.X + 12, hip.Y - 6, ink, 2f);
    }

    private static void Apron(SKCanvas canvas, P hip, P shoulder, VisualBrandStyle brand, uint ink)
    {
        var cloth = Mix(brand.Prop, 0xFFFFFF, 0.2f);
        using var bib = new SKPath();
        bib.MoveTo(shoulder.X - 16, shoulder.Y + 36);
        bib.LineTo(shoulder.X + 16, shoulder.Y + 36);
        bib.LineTo(hip.X + 28, hip.Y + 8);
        bib.QuadTo(hip.X, hip.Y + 22, hip.X - 28, hip.Y + 8);
        bib.Close();
        Cloth(canvas, bib, cloth, ink);
        StrokeLine(canvas, shoulder.X - 28, shoulder.Y + 6, shoulder.X - 12, shoulder.Y + 36, ink, 2f);
        StrokeLine(canvas, shoulder.X + 28, shoulder.Y + 6, shoulder.X + 12, shoulder.Y + 36, ink, 2f);
        FillRound(canvas, new SKRect(hip.X - 10, hip.Y - 6, hip.X + 10, hip.Y + 4), Mix(brand.Ink, brand.Prop, 0.2f), 3);
    }

    private static void Hood(SKCanvas canvas, P head, CharacterIdentity who)
    {
        var rx = who.FaceShape == "oval" ? 48f : 54f;
        var ry = who.FaceShape == "oval" ? 58f : 56f;
        FillOval(canvas, new SKRect(head.X - rx, head.Y - ry - 8, head.X + rx, head.Y + ry * 0.35f), 0x2C4A34);
    }

    private static void Scarf(SKCanvas canvas, P shoulder, P head)
    {
        var y = (shoulder.Y + head.Y) * 0.5f + 24f;
        using var path = new SKPath();
        path.MoveTo(shoulder.X - 28, y);
        path.QuadTo(shoulder.X, y + 16, shoulder.X + 30, y - 2);
        path.LineTo(shoulder.X + 24, y + 12);
        path.QuadTo(shoulder.X, y + 28, shoulder.X - 22, y + 12);
        path.Close();
        Cloth(canvas, path, 0xB94A3E, 0x2C2420);
    }

    private static void NeckColumn(SKCanvas canvas, P shoulder, P head, uint skin, uint ink)
    {
        var cx = head.X * 0.65f + shoulder.X * 0.35f;
        var top = head.Y + 26f;
        var bot = shoulder.Y + 18f;
        using var path = new SKPath();
        path.MoveTo(cx - 13, top);
        path.LineTo(cx + 13, top);
        path.LineTo(cx + 18, bot);
        path.LineTo(cx - 18, bot);
        path.Close();
        using (var fill = Fill(Mix(skin, 0x000000, 0.05f)))
            canvas.DrawPath(path, fill);
        using (var shade = Fill(Mix(skin, 0x000000, 0.18f)))
        {
            shade.Color = shade.Color.WithAlpha(80);
            canvas.DrawOval(new SKRect(cx + 2, top, cx + 16, bot), shade);
        }
        using var stroke = Line(Mix(skin, ink, 0.28f), 1.5f);
        canvas.DrawPath(path, stroke);
    }

    private static void FrontHead(SKCanvas canvas, P head, CharacterIdentity who, string expression, uint skin, uint ink, float turn, bool hat)
    {
        var rx = (who.FaceShape == "oval" ? 38f : 44f) * who.HeadScale / 34f;
        var ry = (who.FaceShape == "oval" ? 50f : 48f) * who.HeadScale / 34f;
        var hair = who.HairColor;
        if (who.Hair == "bob")
            BobMass(canvas, head, rx, ry, hair, ink);
        else
            CropMass(canvas, head, rx, ry, hair, ink);

        if (hat)
            HatBrim(canvas, head, rx, ry);

        Ear(canvas, head.X - rx - 1, head.Y + 2, skin, ink, true);
        Ear(canvas, head.X + rx + 1, head.Y + 2, skin, ink, false);

        using var face = FaceOval(head, rx, ry, turn);
        using (var fill = Fill(skin))
            canvas.DrawPath(face, fill);
        canvas.Save();
        canvas.ClipPath(face, SKClipOperation.Intersect, true);
        using (var blush = Fill(0xE09080))
        {
            blush.Color = blush.Color.WithAlpha(78);
            canvas.DrawOval(new SKRect(head.X - rx * 0.72f, head.Y + ry * 0.05f, head.X - rx * 0.28f, head.Y + ry * 0.38f), blush);
            canvas.DrawOval(new SKRect(head.X + rx * 0.28f, head.Y + ry * 0.05f, head.X + rx * 0.72f, head.Y + ry * 0.38f), blush);
        }
        using (var jaw = Fill(Mix(skin, 0x000000, 0.12f)))
        {
            jaw.Color = jaw.Color.WithAlpha(70);
            canvas.DrawOval(new SKRect(head.X - rx * 0.45f, head.Y + ry * 0.35f, head.X + rx * 0.45f, head.Y + ry * 0.85f), jaw);
        }
        canvas.Restore();
        using (var stroke = Line(ink, 2.3f))
            canvas.DrawPath(face, stroke);

        if (who.Hair == "bob")
            BobFront(canvas, head, rx, ry, hair, ink);
        else
            CropFront(canvas, head, rx, ry, hair, ink);

        if (hat)
            HatCrown(canvas, head, rx, ry);

        var shift = turn * rx * 0.2f;
        var eyeY = head.Y - ry * 0.02f;
        var gap = rx * 0.36f;
        var far = 1f - turn * 0.3f;
        Eye(canvas, head.X - gap + shift, eyeY, far, expression, expression == "wink", hair, ink);
        Eye(canvas, head.X + gap + shift, eyeY, 1f, expression, false, hair, ink);
        Brow(canvas, head.X - gap + shift, eyeY - ry * 0.28f, expression, hair, true, far);
        Brow(canvas, head.X + gap + shift, eyeY - ry * 0.28f, expression, hair, false, 1f);
        Nose(canvas, head.X + shift, eyeY + ry * 0.22f, ink, skin, turn);
        Mouth(canvas, head.X + shift * 0.4f, head.Y + ry * 0.4f, expression, ink);

        if (who.Mark == "freckle")
            Freckles(canvas, head, rx, ry);
        else if (who.Mark == "glasses")
            Glasses(canvas, head.X + shift, eyeY, gap, rx, ink);
    }

    private static SKPath FaceOval(P head, float rx, float ry, float turn)
    {
        var cx = head.X + turn * rx * 0.12f;
        var cy = head.Y;
        var cheek = rx * (1f - turn * 0.06f);
        var path = new SKPath();
        var top = cy - ry * 0.9f;
        var chin = cy + ry * 0.8f;
        path.MoveTo(cx, top);
        path.CubicTo(cx + cheek * 0.78f, top + 2, cx + cheek, cy - ry * 0.05f, cx + cheek * 0.9f, cy + ry * 0.22f);
        path.CubicTo(cx + cheek * 0.7f, cy + ry * 0.55f, cx + cheek * 0.32f, chin, cx, chin + 1);
        path.CubicTo(cx - cheek * 0.32f, chin, cx - cheek * 0.7f, cy + ry * 0.55f, cx - cheek * 0.9f, cy + ry * 0.22f);
        path.CubicTo(cx - cheek, cy - ry * 0.05f, cx - cheek * 0.78f, top + 2, cx, top);
        path.Close();
        return path;
    }

    private static void BobMass(SKCanvas canvas, P head, float rx, float ry, uint hair, uint ink)
    {
        var cx = head.X;
        var cy = head.Y;
        using var path = new SKPath();
        path.MoveTo(cx, cy - ry - 20);
        path.CubicTo(cx + rx + 30, cy - ry - 2, cx + rx + 22, cy + 36, cx + rx + 2, cy + ry * 1.2f);
        path.CubicTo(cx + rx * 0.4f, cy + ry * 0.42f, cx - rx * 0.4f, cy + ry * 0.42f, cx - rx - 2, cy + ry * 1.2f);
        path.CubicTo(cx - rx - 22, cy + 36, cx - rx - 30, cy - ry - 2, cx, cy - ry - 20);
        path.Close();
        Cloth(canvas, path, hair, ink);
        canvas.Save();
        canvas.ClipPath(path, SKClipOperation.Intersect, true);
        using var shine = Fill(Mix(hair, 0xFFFFFF, 0.34f));
        shine.Color = shine.Color.WithAlpha(150);
        canvas.DrawOval(new SKRect(cx - rx * 0.15f, cy - ry - 14, cx + rx * 0.95f, cy - ry * 0.05f), shine);
        using var dark = Fill(Mix(hair, 0x000000, 0.25f));
        dark.Color = dark.Color.WithAlpha(80);
        canvas.DrawOval(new SKRect(cx - rx - 10, cy + 8, cx - rx * 0.2f, cy + ry), dark);
        canvas.Restore();
    }

    private static void BobFront(SKCanvas canvas, P head, float rx, float ry, uint hair, uint ink)
    {
        var cx = head.X;
        var cy = head.Y;
        using var bangs = new SKPath();
        bangs.MoveTo(cx - rx * 0.88f, cy - ry * 0.42f);
        bangs.CubicTo(cx - rx * 0.2f, cy - ry - 22, cx + rx * 0.35f, cy - ry - 20, cx + rx * 0.86f, cy - ry * 0.46f);
        bangs.CubicTo(cx + rx * 0.2f, cy - ry * 0.22f, cx - rx * 0.15f, cy - ry * 0.18f, cx - rx * 0.88f, cy - ry * 0.42f);
        bangs.Close();
        Cloth(canvas, bangs, hair, ink);
        Lock(canvas, head, rx, ry, hair, ink, -1f, 1.05f);
        Lock(canvas, head, rx, ry, hair, ink, 1f, 0.92f);
    }

    private static void Lock(SKCanvas canvas, P head, float rx, float ry, uint hair, uint ink, float side, float drop)
    {
        var cx = head.X;
        var cy = head.Y;
        using var path = new SKPath();
        path.MoveTo(cx + side * rx * 0.78f, cy - ry * 0.28f);
        path.CubicTo(cx + side * (rx + 14), cy + ry * 0.02f, cx + side * (rx + 8), cy + ry * 0.32f, cx + side * (rx + 2), cy + ry * drop);
        path.CubicTo(cx + side * rx * 0.72f, cy + ry * 0.2f, cx + side * rx * 0.7f, cy - ry * 0.05f, cx + side * rx * 0.78f, cy - ry * 0.28f);
        path.Close();
        Cloth(canvas, path, Mix(hair, 0x000000, side < 0 ? 0.08f : 0f), ink);
    }

    private static void CropMass(SKCanvas canvas, P head, float rx, float ry, uint hair, uint ink)
    {
        using var path = new SKPath();
        path.AddOval(new SKRect(head.X - rx - 8, head.Y - ry - 10, head.X + rx + 8, head.Y - ry * 0.08f));
        Cloth(canvas, path, hair, ink);
    }

    private static void CropFront(SKCanvas canvas, P head, float rx, float ry, uint hair, uint ink)
    {
        using var path = new SKPath();
        path.MoveTo(head.X - rx * 0.8f, head.Y - ry * 0.15f);
        path.CubicTo(head.X - rx * 0.2f, head.Y - ry - 10, head.X + rx * 0.45f, head.Y - ry - 8, head.X + rx * 0.7f, head.Y - ry * 0.2f);
        path.CubicTo(head.X + rx * 0.1f, head.Y - ry * 0.42f, head.X - rx * 0.2f, head.Y - ry * 0.28f, head.X - rx * 0.8f, head.Y - ry * 0.15f);
        path.Close();
        Cloth(canvas, path, hair, ink);
    }

    private static void Ear(SKCanvas canvas, float x, float y, uint skin, uint ink, bool left)
    {
        FillOval(canvas, new SKRect(x - 8, y - 10, x + 8, y + 12), skin);
        StrokeArc(canvas, x, y, 7, 10, Mix(skin, ink, 0.35f), left ? 80 : 260, 160, 1.4f);
    }

    private static void Eye(SKCanvas canvas, float x, float y, float scale, string expression, bool wink, uint hair, uint ink)
    {
        if (wink)
        {
            StrokeArc(canvas, x, y + 2, 12 * scale, 7 * scale, ink, 200, 140, 2.2f);
            return;
        }

        var open = expression switch
        {
            "focus" => 0.72f,
            "surprise" => 1.22f,
            "happy" => 0.78f,
            "smile" => 0.88f,
            "concern" => 1.05f,
            _ => 1f,
        };
        var w = 15.5f * scale;
        var h = 11f * scale * open;
        FillOval(canvas, new SKRect(x - w, y - h, x + w, y + h), 0xFFFcf8);
        using (var lid = Line(ink, 2.1f))
            canvas.DrawArc(new SKRect(x - w, y - h, x + w, y + h), 200, 150, false, lid);
        var iris = (expression == "surprise" ? 7.2f : 6.1f) * scale;
        FillOval(canvas, new SKRect(x - iris, y - iris + 0.5f, x + iris, y + iris + 0.5f), 0x6B442C);
        FillOval(canvas, new SKRect(x - iris * 0.45f, y - iris * 0.2f, x + iris * 0.15f, y + iris * 0.55f), 0x3A2418);
        FillOval(canvas, new SKRect(x - iris * 0.42f, y - iris * 0.15f, x + iris * 0.42f, y + iris * 0.55f), 0x1A120E);
        FillOval(canvas, new SKRect(x - iris * 0.15f, y - iris * 0.85f, x + iris * 0.45f, y - iris * 0.2f), 0xFFFFFF);
        FillOval(canvas, new SKRect(x - iris * 0.7f, y + iris * 0.15f, x - iris * 0.25f, y + iris * 0.55f), 0xFFFFFF);
        if (expression is "smile" or "happy")
            StrokeArc(canvas, x, y - h * 0.15f, w, h * 0.9f, ink, 200, 140, 2.3f);
        StrokeLine(canvas, x - w - 1, y - h - 1, x - w + 5, y - h - 4, hair, 1.3f);
        StrokeLine(canvas, x + w - 6, y - h - 3, x + w + 1, y - h, hair, 1.3f);
    }

    private static void Brow(SKCanvas canvas, float x, float y, string expression, uint hair, bool left, float scale)
    {
        var lift = expression switch
        {
            "concern" => left ? -7f : 4f,
            "surprise" => -8f,
            "focus" => left ? 3f : 2f,
            "think" => left ? -6f : 2f,
            "wink" => left ? -5f : 0f,
            "happy" or "smile" => -2f,
            _ => 0f,
        };
        using var path = new SKPath();
        path.MoveTo(x - 14 * scale, y + 2 + lift);
        path.QuadTo(x, y - 5 + lift, x + 12 * scale, y + 1);
        using var stroke = Line(hair, 3.3f);
        canvas.DrawPath(path, stroke);
    }

    private static void Nose(SKCanvas canvas, float x, float y, uint ink, uint skin, float turn)
    {
        var tone = Mix(skin, ink, 0.28f);
        if (turn > 0.3f)
        {
            using var path = new SKPath();
            path.MoveTo(x - 1, y - 6);
            path.QuadTo(x + 7, y, x + 2, y + 7);
            using var stroke = Line(tone, 2f);
            canvas.DrawPath(path, stroke);
            return;
        }
        StrokeLine(canvas, x, y - 2, x + 1.5f, y + 7, tone, 1.8f);
        StrokeArc(canvas, x + 1, y + 8, 3.5f, 2.4f, tone, 10, 150, 1.5f);
    }

    private static void Mouth(SKCanvas canvas, float x, float y, string expression, uint ink)
    {
        const uint lip = 0xC45B62;
        if (expression is "smile" or "happy" or "wink")
        {
            var wide = expression == "happy" ? 18f : 15f;
            var open = expression == "happy" ? 14f : 11f;
            using var path = new SKPath();
            path.MoveTo(x - wide, y);
            path.QuadTo(x, y + open + 6, x + wide, y - (expression == "wink" ? 2 : 0));
            path.QuadTo(x, y + 3, x - wide, y);
            path.Close();
            using (var fill = Fill(lip))
                canvas.DrawPath(path, fill);
            FillRound(canvas, new SKRect(x - wide * 0.55f, y + 1, x + wide * 0.55f, y + 6), 0xFFF8F4, 2);
            StrokeLine(canvas, x, y + 1, x, y + 5, Mix(lip, 0xFFFFFF, 0.4f), 1f);
            using (var tongue = Fill(0xD56A6A))
                canvas.DrawOval(new SKRect(x - wide * 0.28f, y + open * 0.35f, x + wide * 0.28f, y + open * 0.85f), tongue);
            using var stroke = Line(ink, 1.8f);
            canvas.DrawPath(path, stroke);
            return;
        }
        if (expression == "surprise")
        {
            FillOval(canvas, new SKRect(x - 7, y - 2, x + 7, y + 12), 0x5C2E32);
            FillOval(canvas, new SKRect(x - 4, y, x + 4, y + 6), 0xF3D2D4);
            Stroke(canvas, new SKRect(x - 7, y - 2, x + 7, y + 12), lip, 1.8f);
            return;
        }
        if (expression is "concern")
        {
            StrokeArc(canvas, x, y + 10, 11, 6, lip, 200, 140, 2.4f);
            return;
        }
        if (expression is "focus" or "think")
        {
            StrokeLine(canvas, x - 8, y + 2, x + 9, y + 3, lip, 2.4f);
            return;
        }
        using var calm = new SKPath();
        calm.MoveTo(x - 11, y + 1);
        calm.QuadTo(x, y + 8, x + 11, y);
        using var calmStroke = Line(lip, 2.4f);
        canvas.DrawPath(calm, calmStroke);
    }

    private static void Freckles(SKCanvas canvas, P head, float rx, float ry)
    {
        var spot = 0xC47858u;
        Dot(canvas, head.X - rx * 0.42f, head.Y + ry * 0.12f, spot);
        Dot(canvas, head.X - rx * 0.3f, head.Y + ry * 0.22f, spot);
        Dot(canvas, head.X - rx * 0.5f, head.Y + ry * 0.2f, spot);
        Dot(canvas, head.X + rx * 0.38f, head.Y + ry * 0.14f, spot);
        Dot(canvas, head.X + rx * 0.5f, head.Y + ry * 0.24f, spot);
        Dot(canvas, head.X + rx * 0.28f, head.Y + ry * 0.22f, spot);
    }

    private static void Glasses(SKCanvas canvas, float x, float y, float gap, float rx, uint ink)
    {
        var frame = 0x1C1C1Cu;
        Stroke(canvas, new SKRect(x - gap - 16, y - 13, x - 2, y + 13), frame, 2.6f);
        Stroke(canvas, new SKRect(x + 2, y - 13, x + gap + 16, y + 13), frame, 2.6f);
        StrokeLine(canvas, x - 2, y - 1, x + 2, y - 1, frame, 2.4f);
        StrokeLine(canvas, x - gap - 16, y - 1, x - rx - 2, y + 8, frame, 2.2f);
        StrokeLine(canvas, x + gap + 16, y - 1, x + rx + 2, y + 8, frame, 2.2f);
    }

    private static void ProfileHead(SKCanvas canvas, P head, CharacterIdentity who, string expression, uint skin, uint ink)
    {
        var rx = (who.FaceShape == "oval" ? 36f : 40f) * who.HeadScale / 34f;
        var ry = (who.FaceShape == "oval" ? 50f : 48f) * who.HeadScale / 34f;
        var hair = who.HairColor;
        var cx = head.X;
        var cy = head.Y;
        using (var mass = new SKPath())
        {
            mass.MoveTo(cx - 6, cy - ry - 14);
            mass.CubicTo(cx - rx - 18, cy - ry, cx - rx - 16, cy + 10, cx - rx + 4, cy + ry * 0.55f);
            mass.CubicTo(cx - 8, cy + 16, cx + rx * 0.2f, cy + 6, cx + 8, cy - ry * 0.1f);
            mass.CubicTo(cx + rx * 0.2f, cy - ry, cx + 8, cy - ry - 8, cx - 6, cy - ry - 14);
            mass.Close();
            Cloth(canvas, mass, hair, ink);
        }
        FillOval(canvas, new SKRect(cx - rx * 0.15f, cy - 6, cx + 14, cy + 16), skin);
        using (var ear = Line(Mix(skin, ink, 0.35f), 1.5f))
            canvas.DrawArc(new SKRect(cx - 2, cy - 6, cx + 12, cy + 16), 40, 220, false, ear);
        using var face = new SKPath();
        face.MoveTo(cx - rx * 0.2f, cy - ry * 0.82f);
        face.CubicTo(cx + rx * 0.35f, cy - ry * 0.9f, cx + rx * 0.62f, cy - ry * 0.05f, cx + rx * 0.58f, cy + 6);
        face.CubicTo(cx + rx * 0.28f, cy + 10, cx + rx * 0.42f, cy + 16, cx + rx * 0.22f, cy + 18);
        face.CubicTo(cx + rx * 0.55f, cy + ry * 0.55f, cx - rx * 0.05f, cy + ry * 0.84f, cx - rx * 0.25f, cy + ry * 0.62f);
        face.CubicTo(cx - rx * 0.85f, cy + ry * 0.15f, cx - rx * 0.7f, cy - ry * 0.45f, cx - rx * 0.2f, cy - ry * 0.82f);
        face.Close();
        using (var fill = Fill(skin))
            canvas.DrawPath(face, fill);
        using (var stroke = Line(ink, 2.2f))
            canvas.DrawPath(face, stroke);
        using (var fringe = new SKPath())
        {
            fringe.MoveTo(cx - rx * 0.15f, cy - ry * 0.2f);
            fringe.CubicTo(cx + rx * 0.1f, cy - ry - 16, cx + rx * 0.7f, cy - ry * 0.7f, cx + rx * 0.35f, cy - ry * 0.15f);
            fringe.CubicTo(cx + rx * 0.1f, cy - ry * 0.45f, cx - rx * 0.05f, cy - ry * 0.35f, cx - rx * 0.15f, cy - ry * 0.2f);
            fringe.Close();
            Cloth(canvas, fringe, hair, ink);
        }
        Eye(canvas, cx + rx * 0.12f, cy - 2, 0.92f, expression, false, hair, ink);
        Brow(canvas, cx + rx * 0.12f, cy - ry * 0.28f, expression, hair, false, 0.9f);
        StrokeLine(canvas, cx + rx * 0.55f, cy + 6, cx + rx * 0.15f, cy + 16, Mix(skin, ink, 0.3f), 1.8f);
        Mouth(canvas, cx + rx * 0.18f, cy + ry * 0.22f, expression, ink);
        if (who.Mark == "freckle")
        {
            Dot(canvas, cx + rx * 0.15f, cy + 8, 0xC47858);
            Dot(canvas, cx + rx * 0.28f, cy + 12, 0xC47858);
        }
        else if (who.Mark == "glasses")
        {
            Stroke(canvas, new SKRect(cx - 2, cy - 14, cx + rx * 0.55f, cy + 10), 0x1C1C1C, 2.4f);
            StrokeLine(canvas, cx - 2, cy - 2, cx - rx * 0.35f, cy + 6, 0x1C1C1C, 2.2f);
        }
    }

    private static void BackHead(SKCanvas canvas, P head, CharacterIdentity who, uint ink)
    {
        var rx = (who.FaceShape == "oval" ? 40f : 46f) * who.HeadScale / 34f;
        var ry = (who.FaceShape == "oval" ? 50f : 48f) * who.HeadScale / 34f;
        var hair = who.HairColor;
        var drop = who.Hair == "bob" ? ry * 0.7f : ry * 0.15f;
        using var path = new SKPath();
        path.MoveTo(head.X, head.Y - ry - 16);
        path.CubicTo(head.X + rx + 18, head.Y - ry, head.X + rx + 8, head.Y + 10, head.X + rx * 0.4f, head.Y + drop);
        path.CubicTo(head.X, head.Y + drop + 12, head.X, head.Y + drop + 12, head.X - rx * 0.4f, head.Y + drop);
        path.CubicTo(head.X - rx - 8, head.Y + 10, head.X - rx - 18, head.Y - ry, head.X, head.Y - ry - 16);
        path.Close();
        Cloth(canvas, path, hair, ink);
        canvas.Save();
        canvas.ClipPath(path, SKClipOperation.Intersect, true);
        using var shine = Fill(Mix(hair, 0xFFFFFF, 0.3f));
        shine.Color = shine.Color.WithAlpha(140);
        canvas.DrawOval(new SKRect(head.X - 10, head.Y - ry - 8, head.X + rx, head.Y - 4), shine);
        canvas.Restore();
        Ear(canvas, head.X - rx, head.Y + 4, 0xF0C7A8, ink, true);
        Ear(canvas, head.X + rx, head.Y + 4, 0xF0C7A8, ink, false);
    }

    private static void HatBrim(SKCanvas canvas, P head, float rx, float ry)
    {
        var y = head.Y - ry * 0.35f;
        FillOval(canvas, new SKRect(head.X - rx - 28, y - 8, head.X + rx + 28, y + 16), 0xE6C27A);
        FillOval(canvas, new SKRect(head.X - rx - 18, y - 4, head.X + rx + 18, y + 10), 0xF3D7A1);
    }

    private static void HatCrown(SKCanvas canvas, P head, float rx, float ry)
    {
        var y = head.Y - ry * 0.72f;
        FillOval(canvas, new SKRect(head.X - rx * 0.72f, y - 8, head.X + rx * 0.72f, y + 22), 0xE6C27A);
        StrokeLine(canvas, head.X - rx * 0.55f, y + 12, head.X + rx * 0.55f, y + 12, 0xC4554A, 3f);
        using var stroke = Line(0x5C4630, 1.8f);
        canvas.DrawOval(new SKRect(head.X - rx * 0.72f, y - 8, head.X + rx * 0.72f, y + 22), stroke);
    }

    private static void HandOf(SKCanvas canvas, P elbow, P wrist, uint skin, uint ink, string kind)
    {
        var dx = wrist.X - elbow.X;
        var dy = wrist.Y - elbow.Y;
        var angle = MathF.Atan2(dy, dx) * 180f / MathF.PI;
        canvas.Save();
        canvas.Translate(wrist.X, wrist.Y);
        canvas.RotateDegrees(angle);
        using var path = new SKPath();
        if (kind == "point")
        {
            path.MoveTo(-2, -8);
            path.CubicTo(8, -10, 14, -6, 30, -3);
            path.CubicTo(34, 0, 30, 4, 16, 4);
            path.LineTo(14, 9);
            path.CubicTo(8, 14, 0, 12, -2, 7);
            path.CubicTo(-8, 2, -6, -4, -2, -8);
        }
        else
        {
            path.MoveTo(-2, -9);
            path.CubicTo(10, -14, 22, -12, 28, -4);
            path.CubicTo(31, 2, 26, 8, 18, 8);
            path.CubicTo(14, 15, 4, 16, -1, 10);
            path.CubicTo(-8, 4, -8, -3, -2, -9);
        }
        path.Close();
        using (var fill = Fill(skin))
            canvas.DrawPath(path, fill);
        using (var stroke = Line(ink, 1.8f))
            canvas.DrawPath(path, stroke);
        if (kind != "point")
        {
            StrokeLine(canvas, 14, -10, 15, 6, Mix(skin, ink, 0.45f), 1.15f);
            StrokeLine(canvas, 18, -11, 19, 6, Mix(skin, ink, 0.45f), 1.15f);
            StrokeLine(canvas, 22, -9, 22, 5, Mix(skin, ink, 0.45f), 1.15f);
        }
        canvas.Restore();
    }

    private static void PropOf(SKCanvas canvas, ShotVisualDemand demand, VisualBrandStyle brand, P elbowL, P handL, P elbowR, P handR)
    {
        if (demand.Pose is "reach" or "consult")
            Board(canvas, elbowR, handR, brand);
        else if (demand.Pose == "phone")
            Phone(canvas, elbowR, handR, brand);
        else if (demand.Pose == "write")
            Pen(canvas, elbowR, handR, brand);
        else if (brand.Motif == "tea-terrace" && demand.Pose is "stand" or "walk")
            Sprig(canvas, elbowR, handR, brand);
        else if (demand.Prop.Contains("bottle", StringComparison.OrdinalIgnoreCase))
            HeldBottle(canvas, elbowR, handR, brand);
        else if (demand.Prop.Contains("cup", StringComparison.OrdinalIgnoreCase))
            HeldCup(canvas, elbowR, handR, brand);
    }

    private static void Board(SKCanvas canvas, P elbow, P hand, VisualBrandStyle brand)
    {
        var p = Past(elbow, hand, 26f);
        FillRound(canvas, new SKRect(p.X - 18, p.Y - 26, p.X + 18, p.Y + 26), 0xFFFaf6, 4);
        FillRound(canvas, new SKRect(p.X - 10, p.Y - 30, p.X + 10, p.Y - 22), brand.Ink, 2);
        Stroke(canvas, new SKRect(p.X - 18, p.Y - 26, p.X + 18, p.Y + 26), brand.Ink, 1.8f);
        StrokeLine(canvas, p.X - 10, p.Y - 10, p.X + 10, p.Y - 10, Mix(brand.Ink, brand.Background, 0.35f), 1.6f);
        StrokeLine(canvas, p.X - 10, p.Y - 2, p.X + 8, p.Y - 2, Mix(brand.Ink, brand.Background, 0.35f), 1.6f);
        StrokeLine(canvas, p.X - 10, p.Y + 6, p.X + 6, p.Y + 6, Mix(brand.Ink, brand.Background, 0.35f), 1.6f);
    }

    private static void Phone(SKCanvas canvas, P elbow, P hand, VisualBrandStyle brand)
    {
        var p = Past(elbow, hand, 8f);
        FillRound(canvas, new SKRect(p.X - 8, p.Y - 16, p.X + 8, p.Y + 16), 0x1C2430, 4);
        FillRound(canvas, new SKRect(p.X - 5, p.Y - 12, p.X + 5, p.Y + 8), Mix(brand.Accent, 0xFFFFFF, 0.35f), 2);
    }

    private static void Pen(SKCanvas canvas, P elbow, P hand, VisualBrandStyle brand)
    {
        var p = Past(elbow, hand, 16f);
        StrokeLine(canvas, hand.X, hand.Y, p.X, p.Y, brand.Ink, 2.4f);
        FillOval(canvas, new SKRect(p.X - 3, p.Y - 3, p.X + 3, p.Y + 3), brand.Accent);
    }

    private static void Sprig(SKCanvas canvas, P elbow, P hand, VisualBrandStyle brand)
    {
        var p = Past(elbow, hand, 18f);
        StrokeLine(canvas, hand.X, hand.Y, p.X, p.Y - 16, 0x3E5A32, 2f);
        FillOval(canvas, new SKRect(p.X - 14, p.Y - 28, p.X + 2, p.Y - 12), brand.Accent);
        FillOval(canvas, new SKRect(p.X - 2, p.Y - 36, p.X + 16, p.Y - 18), Mix(brand.Accent, 0xFFFFFF, 0.2f));
        using var stroke = Line(0x2C4430, 1.4f);
        canvas.DrawOval(new SKRect(p.X - 14, p.Y - 28, p.X + 2, p.Y - 12), stroke);
        canvas.DrawOval(new SKRect(p.X - 2, p.Y - 36, p.X + 16, p.Y - 18), stroke);
    }

    private static void HeldBottle(SKCanvas canvas, P elbow, P hand, VisualBrandStyle brand)
    {
        var p = Past(elbow, hand, 10f);
        Bottle(canvas, p.X, p.Y - 20, 42, brand.Accent, brand.Ink);
    }

    private static void HeldCup(SKCanvas canvas, P elbow, P hand, VisualBrandStyle brand)
    {
        var p = Past(elbow, hand, 8f);
        Cup(canvas, p.X, p.Y - 10, brand);
    }

    private static P Past(P elbow, P hand, float distance)
    {
        var dx = hand.X - elbow.X;
        var dy = hand.Y - elbow.Y;
        var len = MathF.Max(1f, MathF.Sqrt(dx * dx + dy * dy));
        return new P(hand.X + dx / len * distance, hand.Y + dy / len * distance);
    }
}
