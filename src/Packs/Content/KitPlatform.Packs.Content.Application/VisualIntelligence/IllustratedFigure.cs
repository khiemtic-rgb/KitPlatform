using SkiaSharp;

namespace KitPlatform.Packs.Content.VisualIntelligence;

public static partial class StudioScenePainter
{
    private static void DrawIllustrated(
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
        DrawEditorial(
            canvas, demand, who, brand, hip, shoulder, head,
            shoulderL, elbowL, handL, shoulderR, elbowR, handR,
            hipL, kneeL, footL, hipR, kneeR, footR, pants);
    }

    private static void Sweater(SKCanvas canvas, P hip, P shoulder, P shoulderL, P elbowL, P handL, P shoulderR, P elbowR, P handR, uint color, uint ink)
    {
        var leftOut = OffsetChain([shoulderL, elbowL, handL], 16, 13, 11, 1);
        var leftIn = OffsetChain([shoulderL, elbowL, handL], 14, 11, 9, -1);
        var rightOut = OffsetChain([shoulderR, elbowR, handR], 16, 13, 11, -1);
        var rightIn = OffsetChain([shoulderR, elbowR, handR], 14, 11, 9, 1);
        var neckY = shoulder.Y - 22;
        var hemY = hip.Y + 22;
        using var path = new SKPath { FillType = SKPathFillType.Winding };
        path.MoveTo(leftOut[0].X, leftOut[0].Y);
        path.LineTo(leftOut[1].X, leftOut[1].Y);
        path.LineTo(leftOut[2].X, leftOut[2].Y);
        path.LineTo(leftIn[2].X, leftIn[2].Y);
        path.LineTo(leftIn[1].X, leftIn[1].Y);
        path.LineTo(leftIn[0].X, leftIn[0].Y);
        path.LineTo(hip.X - 24, hemY);
        path.QuadTo(hip.X, hemY + 8, hip.X + 24, hemY);
        path.LineTo(rightIn[0].X, rightIn[0].Y);
        path.LineTo(rightIn[1].X, rightIn[1].Y);
        path.LineTo(rightIn[2].X, rightIn[2].Y);
        path.LineTo(rightOut[2].X, rightOut[2].Y);
        path.LineTo(rightOut[1].X, rightOut[1].Y);
        path.LineTo(rightOut[0].X, rightOut[0].Y);
        path.CubicTo(shoulder.X + 28, neckY + 6, shoulder.X + 16, neckY, shoulder.X + 12, neckY);
        path.QuadTo(shoulder.X, neckY + 16, shoulder.X - 12, neckY);
        path.CubicTo(shoulder.X - 16, neckY, shoulder.X - 28, neckY + 6, leftOut[0].X, leftOut[0].Y);
        path.Close();
        Cloth(canvas, path, color, ink);
    }

    private static void LimbRibbon(SKCanvas canvas, P a, P b, P c, float r0, float r1, float r2, uint color, uint ink)
    {
        var outer = OffsetChain([a, b, c], r0, r1, r2, 1);
        var inner = OffsetChain([a, b, c], r0, r1, r2, -1);
        using var path = new SKPath { FillType = SKPathFillType.Winding };
        path.MoveTo(outer[0].X, outer[0].Y);
        path.QuadTo(outer[1].X, outer[1].Y, outer[2].X, outer[2].Y);
        path.LineTo(inner[2].X, inner[2].Y);
        path.QuadTo(inner[1].X, inner[1].Y, inner[0].X, inner[0].Y);
        path.Close();
        Cloth(canvas, path, color, ink);
    }

    private static P Along(P from, P to, float t) => new(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t);

    private static void Cloth(SKCanvas canvas, SKPath path, uint color, uint ink)
    {
        var bounds = path.Bounds;
        using var fill = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(bounds.Left, bounds.Top),
                new SKPoint(bounds.Right, bounds.Bottom),
                [C(Mix(color, 0xFFFFFF, 0.28f)), C(color), C(Mix(color, 0x000000, 0.2f))],
                [0f, 0.42f, 1f],
                SKShaderTileMode.Clamp),
        };
        canvas.DrawPath(path, fill);
        using var stroke = Line(Mix(ink, color, 0.38f), 1.5f);
        canvas.DrawPath(path, stroke);
    }

    private static void PantsBridge(SKCanvas canvas, P hip, uint pants)
    {
        using var path = new SKPath();
        path.MoveTo(hip.X - 34, hip.Y - 4);
        path.LineTo(hip.X + 34, hip.Y - 4);
        path.QuadTo(hip.X + 26, hip.Y + 30, hip.X + 6, hip.Y + 18);
        path.QuadTo(hip.X, hip.Y + 42, hip.X - 6, hip.Y + 18);
        path.QuadTo(hip.X - 26, hip.Y + 30, hip.X - 34, hip.Y - 4);
        path.Close();
        using var fill = Fill(pants);
        canvas.DrawPath(path, fill);
    }

    private static P[] OffsetChain(P[] pts, float w0, float w1, float w2, int sign)
    {
        float[] widths = [w0, w1, w2];
        var result = new P[pts.Length];
        for (var i = 0; i < pts.Length; i++)
        {
            var prev = pts[Math.Max(0, i - 1)];
            var next = pts[Math.Min(pts.Length - 1, i + 1)];
            var dx = next.X - prev.X;
            var dy = next.Y - prev.Y;
            var len = MathF.Max(1f, MathF.Sqrt(dx * dx + dy * dy));
            result[i] = new P(pts[i].X + -dy / len * widths[i] * sign, pts[i].Y + dx / len * widths[i] * sign);
        }
        return result;
    }

    private static void SolidBone(SKCanvas canvas, P a, P b, float r0, float r1, uint color)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var len = MathF.Max(1f, MathF.Sqrt(dx * dx + dy * dy));
        var nx = -dy / len;
        var ny = dx / len;
        using var path = new SKPath();
        path.MoveTo(a.X + nx * r0, a.Y + ny * r0);
        path.LineTo(b.X + nx * r1, b.Y + ny * r1);
        path.LineTo(b.X - nx * r1, b.Y - ny * r1);
        path.LineTo(a.X - nx * r0, a.Y - ny * r0);
        path.Close();
        using var fill = Fill(color);
        canvas.DrawPath(path, fill);
        canvas.DrawOval(new SKRect(a.X - r0, a.Y - r0, a.X + r0, a.Y + r0), fill);
        canvas.DrawOval(new SKRect(b.X - r1, b.Y - r1, b.X + r1, b.Y + r1), fill);
    }

    private static void HeldCard(SKCanvas canvas, P elbow, P hand, VisualBrandStyle brand)
    {
        var dx = hand.X - elbow.X;
        var dy = hand.Y - elbow.Y;
        var len = MathF.Max(1f, MathF.Sqrt(dx * dx + dy * dy));
        var x = hand.X + dx / len * 18f;
        var y = hand.Y + dy / len * 18f;
        FillRound(canvas, new SKRect(x - 16, y - 22, x + 16, y + 22), 0xFFFaf4, 4);
        Stroke(canvas, new SKRect(x - 16, y - 22, x + 16, y + 22), brand.Ink, 2f);
        StrokeLine(canvas, x - 8, y - 8, x + 8, y - 8, Mix(brand.Ink, brand.Background, 0.4f), 2f);
        StrokeLine(canvas, x - 8, y, x + 6, y, Mix(brand.Ink, brand.Background, 0.4f), 2f);
    }

    private static SKPath Unite(params SKPath[] parts)
    {
        var acc = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = new SKPath();
            if (!acc.Op(parts[i], SKPathOp.Union, next))
            {
                next.AddPath(acc);
                next.AddPath(parts[i]);
            }
            acc.Dispose();
            parts[i].Dispose();
            acc = next;
        }
        return acc;
    }

    private static void Garment(SKCanvas canvas, SKPath path, uint color, uint ink)
    {
        using (path)
        {
            using var fill = Fill(color);
            canvas.DrawPath(path, fill);
            using var stroke = Line(ink, 2.4f);
            canvas.DrawPath(path, stroke);
        }
    }

    private static SKPath Bone(P a, P b, float r0, float r1)
    {
        var path = new SKPath();
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var len = MathF.Max(1f, MathF.Sqrt(dx * dx + dy * dy));
        var nx = -dy / len;
        var ny = dx / len;
        path.MoveTo(a.X + nx * r0, a.Y + ny * r0);
        path.LineTo(b.X + nx * r1, b.Y + ny * r1);
        path.LineTo(b.X - nx * r1, b.Y - ny * r1);
        path.LineTo(a.X - nx * r0, a.Y - ny * r0);
        path.Close();
        path.AddOval(new SKRect(a.X - r0, a.Y - r0, a.X + r0, a.Y + r0));
        path.AddOval(new SKRect(b.X - r1, b.Y - r1, b.X + r1, b.Y + r1));
        return path;
    }

    private static SKPath TorsoPath(P hip, P shoulder, P shoulderL, P shoulderR)
    {
        var path = new SKPath();
        path.AddRoundRect(new SKRect(shoulder.X - 42, shoulder.Y + 6, shoulder.X + 42, hip.Y + 18), 18, 18);
        return path;
    }

    private static SKPath HipPath(P hip, P hipL, P hipR)
    {
        var path = new SKPath();
        path.MoveTo(hip.X - 32, hip.Y - 16);
        path.LineTo(hip.X + 32, hip.Y - 16);
        path.LineTo(hipR.X + 18, hipR.Y + 22);
        path.LineTo(hipL.X - 18, hipL.Y + 22);
        path.Close();
        return path;
    }

    private static P Toward(P from, P to, float distance)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var len = MathF.Max(1f, MathF.Sqrt(dx * dx + dy * dy));
        return new P(from.X + dx / len * distance, from.Y + dy / len * distance);
    }

    private static void Stool(SKCanvas canvas, P hip, VisualBrandStyle brand)
    {
        FillRound(canvas, new SKRect(hip.X - 70, hip.Y - 8, hip.X + 70, hip.Y + 14), brand.Prop, 8);
        Stroke(canvas, new SKRect(hip.X - 70, hip.Y - 8, hip.X + 70, hip.Y + 14), brand.Ink, 2.2f);
        FillRound(canvas, new SKRect(hip.X - 52, hip.Y + 8, hip.X - 36, 4), Mix(brand.Prop, brand.Ink, 0.35f), 4);
        FillRound(canvas, new SKRect(hip.X + 36, hip.Y + 8, hip.X + 52, 4), Mix(brand.Prop, brand.Ink, 0.35f), 4);
    }

    private static void Limb(SKCanvas canvas, P a, P b, P c, float wide, float narrow, uint color, uint ink)
    {
        Tapered(canvas, a, b, wide, (wide + narrow) * 0.46f, color, ink);
        Tapered(canvas, b, c, (wide + narrow) * 0.46f, narrow, color, ink);
        FillOval(canvas, new SKRect(b.X - narrow * 0.7f, b.Y - narrow * 0.7f, b.X + narrow * 0.7f, b.Y + narrow * 0.7f), color);
    }

    private static void Tapered(SKCanvas canvas, P a, P b, float r0, float r1, uint color, uint ink)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var len = MathF.Max(1f, MathF.Sqrt(dx * dx + dy * dy));
        var nx = -dy / len;
        var ny = dx / len;
        using var path = new SKPath();
        path.MoveTo(a.X + nx * r0, a.Y + ny * r0);
        path.LineTo(b.X + nx * r1, b.Y + ny * r1);
        path.LineTo(b.X - nx * r1, b.Y - ny * r1);
        path.LineTo(a.X - nx * r0, a.Y - ny * r0);
        path.Close();
        using (var fill = Fill(color))
            canvas.DrawPath(path, fill);
    }

    private static void Cuff(SKCanvas canvas, P elbow, P wrist, uint color, uint ink)
    {
        var cuff = Mix(color, ink, 0.22f);
        using var path = new SKPath();
        var dx = wrist.X - elbow.X;
        var dy = wrist.Y - elbow.Y;
        var len = MathF.Max(1f, MathF.Sqrt(dx * dx + dy * dy));
        path.MoveTo(wrist.X - dx / len * 10f, wrist.Y - dy / len * 10f);
        path.LineTo(wrist.X, wrist.Y);
        using var stroke = Line(cuff, 8f);
        canvas.DrawPath(path, stroke);
    }

    private static void Waistband(SKCanvas canvas, P hip, uint pants, uint ink)
    {
        FillRound(canvas, new SKRect(hip.X - 30, hip.Y - 8, hip.X + 30, hip.Y + 8), Mix(pants, ink, 0.28f), 3);
    }

    private static void Shirt(SKCanvas canvas, P hip, P shoulder, P shoulderL, P shoulderR, uint color, uint ink)
    {
        var hemY = hip.Y + 22;
        var neckY = shoulder.Y - 22;
        using var path = new SKPath();
        path.MoveTo(shoulder.X - 12, neckY);
        path.QuadTo(shoulder.X, neckY + 18, shoulder.X + 12, neckY);
        path.CubicTo(shoulder.X + 30, neckY + 2, shoulderR.X + 8, shoulder.Y - 18, shoulderR.X, shoulderR.Y + 16);
        path.QuadTo(hip.X + 32, (shoulder.Y + hip.Y) / 2f, hip.X + 26, hemY);
        path.QuadTo(hip.X, hemY + 10, hip.X - 26, hemY);
        path.QuadTo(hip.X - 32, (shoulder.Y + hip.Y) / 2f, shoulderL.X, shoulderL.Y + 16);
        path.CubicTo(shoulderL.X - 8, shoulder.Y - 18, shoulder.X - 30, neckY + 2, shoulder.X - 12, neckY);
        path.Close();
        Cloth(canvas, path, color, ink);
        StrokeLine(canvas, hip.X - 22, hemY - 2, hip.X + 22, hemY + 4, Mix(color, 0xFFFFFF, 0.35f), 2f);
        using var collar = new SKPath();
        collar.MoveTo(shoulder.X - 10, neckY + 2);
        collar.QuadTo(shoulder.X, neckY + 12, shoulder.X + 10, neckY + 2);
        using var collarStroke = Line(Mix(color, 0xFFFFFF, 0.35f), 3f);
        canvas.DrawPath(collar, collarStroke);
    }

    private static void FlatShoe(SKCanvas canvas, P knee, P foot, uint ink)
    {
        var dx = foot.X - knee.X;
        var dy = foot.Y - knee.Y;
        var len = MathF.Max(1f, MathF.Sqrt(dx * dx + dy * dy));
        var nx = -dy / len;
        var ny = dx / len;
        var toe = new P(foot.X + dx / len * 16f, foot.Y + dy / len * 8f);
        using var path = new SKPath();
        path.MoveTo(foot.X + nx * 8, foot.Y + ny * 8);
        path.LineTo(toe.X + nx * 5, toe.Y + ny * 5);
        path.LineTo(toe.X - nx * 5, toe.Y - ny * 5);
        path.LineTo(foot.X - nx * 8, foot.Y - ny * 8);
        path.Close();
        using (var fill = Fill(ink))
            canvas.DrawPath(path, fill);
    }

    private static void OpenHand(SKCanvas canvas, P elbow, P wrist, uint skin, uint ink)
    {
        var dx = wrist.X - elbow.X;
        var dy = wrist.Y - elbow.Y;
        canvas.Save();
        canvas.Translate(wrist.X, wrist.Y);
        canvas.RotateRadians(MathF.Atan2(dy, dx) - MathF.PI / 2f);
        using var path = new SKPath();
        path.MoveTo(-13, 0);
        path.LineTo(-13, 12);
        float x = -13f;
        float[] lengths = [15f, 18f, 16f, 13f];
        for (var i = 0; i < lengths.Length; i++)
        {
            path.LineTo(x, 12 + lengths[i]);
            path.CubicTo(x + 1.2f, 14 + lengths[i], x + 4.2f, 14 + lengths[i], x + 5.2f, 12 + lengths[i]);
            path.LineTo(x + 5.2f, 12);
            x += 6.6f;
        }
        path.LineTo(14, 10);
        path.LineTo(20, 4);
        path.CubicTo(22, -2, 14, -6, 10, 1);
        path.LineTo(-13, 0);
        path.Close();
        using (var fill = Fill(skin))
            canvas.DrawPath(path, fill);
        using (var stroke = Line(ink, 2.2f))
        {
            stroke.Style = SKPaintStyle.Stroke;
            stroke.StrokeJoin = SKStrokeJoin.Round;
            canvas.DrawPath(path, stroke);
        }
        canvas.Restore();
    }

    private static void Portrait(SKCanvas canvas, P head, CharacterIdentity who, string expression, VisualBrandStyle brand, uint ink)
    {
        var rx = who.FaceShape == "oval" ? 40f : 46f;
        var ry = who.FaceShape == "oval" ? 52f : 50f;
        var hair = who.HairColor;
        if (who.Hair == "bob")
            Bob(canvas, head, rx, ry, hair, ink);
        else
            Crop(canvas, head, rx, ry, hair, ink);

        FillOval(canvas, new SKRect(head.X - rx, head.Y - ry + 10, head.X + rx, head.Y + ry * 0.62f), brand.Skin);
        StrokeArc(canvas, head.X, head.Y - ry * 0.05f, rx, ry * 0.72f, ink, 200, 140, 2.4f);
        FillOval(canvas, new SKRect(head.X - rx - 5, head.Y - 2, head.X - rx + 9, head.Y + 16), brand.Skin);
        FillOval(canvas, new SKRect(head.X + rx - 9, head.Y - 2, head.X + rx + 5, head.Y + 16), brand.Skin);

        if (who.Hair == "bob")
            Bangs(canvas, head, rx, ry, hair, ink);
        else
            FillOval(canvas, new SKRect(head.X - rx + 2, head.Y - ry - 8, head.X + rx - 2, head.Y - ry * 0.28f), hair);

        Blush(canvas, head.X - rx * 0.52f, head.Y + ry * 0.22f);
        Blush(canvas, head.X + rx * 0.52f, head.Y + ry * 0.22f);

        var eyeY = head.Y + ry * 0.02f;
        var gap = rx * 0.36f;
        DesignedEye(canvas, head.X - gap, eyeY, expression, ink);
        DesignedEye(canvas, head.X + gap, eyeY, expression, ink);
        ArchedBrow(canvas, head.X - gap, eyeY - 18, expression, hair, true);
        ArchedBrow(canvas, head.X + gap, eyeY - 18, expression, hair, false);
        StrokeLine(canvas, head.X, eyeY + 12, head.X + 2, eyeY + 20, Mix(brand.Skin, ink, 0.35f), 2f);
        DesignedMouth(canvas, head.X, head.Y + ry * 0.42f, expression, ink);

        if (who.Mark == "freckle")
        {
            var spot = 0xC47858u;
            Dot(canvas, head.X + rx * 0.42f, head.Y + 6, spot);
            Dot(canvas, head.X + rx * 0.55f, head.Y + 12, spot);
            Dot(canvas, head.X + rx * 0.36f, head.Y + 14, spot);
            Dot(canvas, head.X - rx * 0.46f, head.Y + 8, spot);
            Dot(canvas, head.X - rx * 0.36f, head.Y + 14, spot);
        }
        else if (who.Mark == "glasses")
        {
            Stroke(canvas, new SKRect(head.X - rx * 0.78f, eyeY - 14, head.X - 4, eyeY + 14), 0x1A1A1A, 3f);
            Stroke(canvas, new SKRect(head.X + 4, eyeY - 14, head.X + rx * 0.78f, eyeY + 14), 0x1A1A1A, 3f);
            StrokeLine(canvas, head.X - 4, eyeY - 2, head.X + 4, eyeY - 2, 0x1A1A1A, 3f);
            StrokeLine(canvas, head.X - rx * 0.78f, eyeY - 2, head.X - rx - 2, eyeY + 8, 0x1A1A1A, 3f);
            StrokeLine(canvas, head.X + rx * 0.78f, eyeY - 2, head.X + rx + 2, eyeY + 8, 0x1A1A1A, 3f);
        }
    }

    private static void Bob(SKCanvas canvas, P head, float rx, float ry, uint hair, uint ink)
    {
        using var path = new SKPath();
        path.MoveTo(head.X, head.Y - ry - 16);
        path.CubicTo(head.X + rx + 28, head.Y - ry - 8, head.X + rx + 22, head.Y + 8, head.X + rx + 4, head.Y + ry * 0.42f);
        path.CubicTo(head.X + rx - 8, head.Y + ry * 0.08f, head.X + 16, head.Y - 8, head.X, head.Y - 10);
        path.CubicTo(head.X - 16, head.Y - 8, head.X - rx + 8, head.Y + ry * 0.08f, head.X - rx - 4, head.Y + ry * 0.42f);
        path.CubicTo(head.X - rx - 22, head.Y + 8, head.X - rx - 28, head.Y - ry - 8, head.X, head.Y - ry - 16);
        path.Close();
        using (var fill = Fill(hair))
            canvas.DrawPath(path, fill);
        using var stroke = Line(ink, 2.6f);
        stroke.Style = SKPaintStyle.Stroke;
        canvas.DrawPath(path, stroke);
    }

    private static void Bangs(SKCanvas canvas, P head, float rx, float ry, uint hair, uint ink)
    {
        using var path = new SKPath();
        path.MoveTo(head.X - rx * 0.92f, head.Y - ry * 0.15f);
        path.CubicTo(head.X - rx * 0.4f, head.Y - ry - 22, head.X + rx * 0.4f, head.Y - ry - 22, head.X + rx * 0.92f, head.Y - ry * 0.15f);
        path.CubicTo(head.X + rx * 0.45f, head.Y - ry * 0.02f, head.X + rx * 0.12f, head.Y - ry * 0.28f, head.X, head.Y - ry * 0.08f);
        path.CubicTo(head.X - rx * 0.12f, head.Y - ry * 0.28f, head.X - rx * 0.45f, head.Y - ry * 0.02f, head.X - rx * 0.92f, head.Y - ry * 0.15f);
        path.Close();
        using (var fill = Fill(hair))
            canvas.DrawPath(path, fill);
        using var stroke = Line(ink, 2.2f);
        stroke.Style = SKPaintStyle.Stroke;
        canvas.DrawPath(path, stroke);
    }

    private static void Crop(SKCanvas canvas, P head, float rx, float ry, uint hair, uint ink)
    {
        FillOval(canvas, new SKRect(head.X - rx - 4, head.Y - ry - 14, head.X + rx + 4, head.Y - ry * 0.05f), hair);
        StrokeArc(canvas, head.X, head.Y - ry * 0.35f, rx + 4, ry * 0.7f, ink, 200, 140, 2.4f);
    }

    private static void Blush(SKCanvas canvas, float x, float y)
    {
        using var paint = Fill(0xE09080);
        paint.Color = paint.Color.WithAlpha(70);
        canvas.DrawOval(new SKRect(x - 12, y - 7, x + 12, y + 7), paint);
    }

    private static void DesignedEye(SKCanvas canvas, float x, float y, string expression, uint ink)
    {
        var h = expression == "focus" ? 8f : expression == "concern" ? 12f : 11f;
        FillOval(canvas, new SKRect(x - 13, y - h, x + 13, y + h), 0xFFFFFF);
        StrokeArc(canvas, x, y - 1, 13, h, ink, 200, 170, 2.2f);
        FillOval(canvas, new SKRect(x - 6.5f, y - 6.5f, x + 6.5f, y + 6.5f), 0x6A4328);
        FillOval(canvas, new SKRect(x - 3.2f, y - 3.2f, x + 3.2f, y + 3.2f), 0x1A120E);
        FillOval(canvas, new SKRect(x + 1.5f, y - 4.5f, x + 5f, y - 1.2f), 0xFFFFFF);
    }

    private static void ArchedBrow(SKCanvas canvas, float x, float y, string expression, uint hair, bool left)
    {
        var lift = expression switch
        {
            "concern" => left ? -6f : 5f,
            "focus" => 3f,
            "smile" => -3f,
            _ => 0f,
        };
        using var path = new SKPath();
        path.MoveTo(x - 14, y + lift);
        path.QuadTo(x, y - 6 + lift, x + 12, y + 1);
        using var stroke = Line(hair, 3.4f);
        canvas.DrawPath(path, stroke);
    }

    private static void DesignedMouth(SKCanvas canvas, float x, float y, string expression, uint ink)
    {
        const uint lip = 0xC45B58;
        if (expression == "smile")
        {
            using var path = new SKPath();
            path.MoveTo(x - 16, y);
            path.QuadTo(x, y + 18, x + 16, y);
            path.QuadTo(x, y + 6, x - 16, y);
            path.Close();
            using (var fill = Fill(lip))
                canvas.DrawPath(path, fill);
            FillRound(canvas, new SKRect(x - 10, y + 1, x + 10, y + 7), 0xFFF8F2, 2);
            using var stroke = Line(ink, 2f);
            stroke.Style = SKPaintStyle.Stroke;
            canvas.DrawPath(path, stroke);
        }
        else if (expression == "concern")
        {
            StrokeArc(canvas, x, y + 12, 12, 7, lip, 200, 140, 2.6f);
        }
        else if (expression == "focus")
        {
            StrokeLine(canvas, x - 9, y + 2, x + 10, y + 2, lip, 2.6f);
        }
        else
        {
            StrokeArc(canvas, x, y, 12, 7, lip, 20, 140, 2.4f);
        }
    }

    private static SKPaint Line(uint color, float width) => new()
    {
        Color = C(color),
        Style = SKPaintStyle.Stroke,
        StrokeWidth = width,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
        IsAntialias = true,
    };
}
