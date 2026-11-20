using SkiaSharp;

namespace KitPlatform.Packs.Content.VisualIntelligence;

/// <summary>
/// Flat forward-facing Lan, ported from the v3 cut-out reference.
/// Screen-left arm waves. Screen-right arm points, then reaches.
/// </summary>
internal static class LanVector
{
    internal static bool TraceWrist;
    internal static SKPoint LastWrist;
    internal static SKPoint LeftWrist;
    internal static SKPoint RightWrist;
    internal static SKPoint LeftShoulder;
    internal static SKPoint RightShoulder;

    private const float Degrees = MathF.PI / 180f;
    private const float Film = 15f;

    private static readonly SKColor Skin = new(0xF1, 0xC4, 0xA3);
    private static readonly SKColor Hair = new(0x7A, 0x4A, 0x2B);
    private static readonly SKColor HairDeep = new(0x5E, 0x35, 0x19);
    private static readonly SKColor Torso = new(0x23, 0x3F, 0x7D);
    private static readonly SKColor Sleeve = new(0x1D, 0x36, 0x70);
    private static readonly SKColor Cuff = new(0x16, 0x29, 0x5C);
    private static readonly SKColor Collar = new(0x2A, 0x4A, 0x90);
    private static readonly SKColor Pants = new(0x5A, 0x6A, 0x37);
    private static readonly SKColor PantsDeep = new(0x46, 0x54, 0x2A);
    private static readonly SKColor Shoe = new(0x1E, 0x2F, 0x66);
    private static readonly SKColor Button = new(0x9F, 0xB4, 0xDE);

    // t, left shoulder, left elbow, wave, right shoulder, right elbow, x, head tilt
    private static readonly float[][] Keys =
    [
        [0f, 7f, 8f, 0f, 7f, 8f, 360f, 0f],
        [1.5f, 7f, 8f, 0f, 7f, 8f, 360f, 0f],
        [2.6f, 7f, 8f, 0f, 67f, 0f, 360f, -0.03f],
        [4.6f, 7f, 8f, 0f, 67f, 0f, 360f, -0.03f],
        [5.6f, 7f, 8f, 0f, 7f, 8f, 360f, 0f],
        [6.4f, 115f, 55f, 0f, 7f, 8f, 360f, 0.05f],
        [7f, 115f, 55f, 1f, 7f, 8f, 360f, 0.05f],
        [9f, 115f, 55f, 1f, 7f, 8f, 360f, 0.05f],
        [9.8f, 7f, 8f, 0f, 7f, 8f, 360f, 0f],
        [10f, 7f, 8f, 0f, 7f, 8f, 360f, 0f],
        [11.6f, 7f, 8f, 0f, 7f, 8f, 600f, 0f],
        [12.4f, 7f, 8f, 0f, 37f, 3f, 600f, 0.04f],
        [13.8f, 7f, 8f, 0f, 37f, 3f, 600f, 0.04f],
        [14.6f, 7f, 8f, 0f, 7f, 8f, 600f, 0f],
        [15f, 7f, 8f, 0f, 7f, 8f, 600f, 0f],
    ];

    private static readonly float[][] Zoom =
    [
        [0f, 1f], [1.5f, 1.05f], [5.5f, 1.05f], [6.4f, 1.32f], [9f, 1.32f],
        [10f, 1f], [11.6f, 1.05f], [12.4f, 1.3f], [14f, 1.3f], [15f, 1f],
    ];

    private static readonly float[][] Yaw =
    [
        [0f, 0f], [2.2f, 0f], [2.8f, 0.6f], [4.6f, 0.6f], [5.4f, 0f],
        [10.4f, 0f], [10.8f, 0.5f], [11.6f, 0.5f], [12.2f, 0.8f], [14f, 0.8f],
        [14.8f, 0f], [15f, 0f],
    ];

    internal static void Draw(SKCanvas canvas, float timeSeconds, float cueDuration, string clipId, float anchorX, float footY)
    {
        var time = cueDuration >= 8f
            ? Math.Clamp(timeSeconds, 0f, Film)
            : MapClip(clipId, timeSeconds, cueDuration);
        var pose = Sample(time);
        var zoom = LerpTable(Zoom, time);
        var yaw = LerpTable(Yaw, time);
        var speed = (Sample(time + 0.05f).X - Sample(time - 0.05f).X) / 0.1f;
        var walking = Math.Clamp(Math.Abs(speed) / 100f, 0f, 1f);
        var phase = time * 8f;
        var bob = walking > 0.2f ? -Math.Abs(MathF.Sin(phase)) * 8f : MathF.Sin(time * 2.2f) * -3f;
        var flutter = pose.Wave * MathF.Sin(time * 13f) * 24f;
        var pointing = time > 2.8f && time < 4.6f;

        canvas.Save();
        var fit = Math.Min(zoom, 1.08f);
        canvas.Translate(anchorX - 80f + (pose.X - 360f) * 0.42f, footY);
        canvas.Scale(0.44f * fit);
        canvas.Translate(-430f, -1080f);
        canvas.Translate(0, bob);

        var step = MathF.Sin(phase) * 0.13f * walking;
        Leg(canvas, 380f, step, 332f, 430f, 378f, 425f);
        Leg(canvas, 480f, -step, 430f, 528f, 482f, 512f);
        using (var waist = Paint(PantsDeep))
            canvas.DrawRect(new SKRect(356f, 445f, 504f, 469f), waist);
        using (var fly = Paint(Pants))
            canvas.DrawRect(new SKRect(425f, 447f, 435f, 469f), fly);

        using var torso = new SKPath();
        torso.MoveTo(345f, 246f);
        torso.QuadTo(430f, 224f, 522f, 246f);
        torso.LineTo(514f, 452f);
        torso.QuadTo(430f, 464f, 350f, 452f);
        torso.Close();
        using (var shirt = Paint(Torso))
            canvas.DrawPath(torso, shirt);
        Dot(canvas, 344f, 300f, 20f, Sleeve);
        Dot(canvas, 522f, 300f, 20f, Sleeve);

        using var neck = new SKPath();
        neck.MoveTo(400f, 232f);
        neck.LineTo(462f, 232f);
        neck.LineTo(431f, 306f);
        neck.Close();
        using (var skin = Paint(Skin))
            canvas.DrawPath(neck, skin);
        CollarFlap(canvas, 398f, 226f, 432f, 246f, 414f, 284f, 384f, 246f);
        CollarFlap(canvas, 464f, 226f, 430f, 246f, 448f, 284f, 478f, 246f);
        using (var seam = new SKPaint { Color = Cuff, Style = SKPaintStyle.Stroke, StrokeWidth = 3f, IsAntialias = true })
        {
            canvas.DrawLine(431f, 306f, 431f + yaw * 6f, 452f, seam);
        }

        foreach (var buttonY in new[] { 340f, 368f, 396f, 424f })
            Dot(canvas, 436f + yaw * 6f, buttonY, 3.5f, Button);

        Head(canvas, pose.Tilt, yaw, time);
        var left = Arm(canvas, -1, 330f, 266f, pose.LeftShoulder, pose.LeftElbow + flutter, false);
        var right = Arm(canvas, 1, 536f, 266f, pose.RightShoulder, pose.RightElbow, pointing);
        LeftShoulder = canvas.TotalMatrix.MapPoint(new SKPoint(330f, 266f));
        RightShoulder = canvas.TotalMatrix.MapPoint(new SKPoint(536f, 266f));
        LeftWrist = canvas.TotalMatrix.MapPoint(left);
        RightWrist = canvas.TotalMatrix.MapPoint(right);
        if (TraceWrist)
            LastWrist = RightWrist;
        canvas.Restore();
    }

    private static void Head(SKCanvas canvas, float tilt, float yaw, float time)
    {
        canvas.Save();
        canvas.Translate(430f, 215f);
        canvas.RotateRadians(tilt);
        canvas.Translate(-430f, -215f);
        using (var skin = Paint(Skin))
            canvas.DrawRect(new SKRect(412f, 200f, 450f, 244f), skin);

        using var hair = new SKPath();
        hair.MoveTo(335f, 218f);
        hair.CubicTo(318f, 120f, 352f, 52f, 430f, 50f);
        hair.CubicTo(510f, 52f, 528f, 130f, 516f, 222f);
        hair.QuadTo(494f, 240f, 476f, 206f);
        hair.LineTo(386f, 206f);
        hair.QuadTo(358f, 240f, 335f, 218f);
        hair.Close();
        using (var hairPaint = Paint(Hair))
            canvas.DrawPath(hair, hairPaint);
        Ell(canvas, 381f, 156f, 10f, 14f, Skin);
        Ell(canvas, 480f, 154f, 10f, 14f, Skin);
        Ell(canvas, 430f, 148f, 52f, 62f, Skin);

        using var bangs = new SKPath();
        bangs.MoveTo(374f, 150f);
        bangs.CubicTo(366f, 92f, 400f, 66f, 442f, 70f);
        bangs.CubicTo(488f, 74f, 494f, 112f, 486f, 150f);
        bangs.CubicTo(478f, 118f, 456f, 100f, 432f, 100f);
        bangs.CubicTo(404f, 108f, 388f, 128f, 374f, 150f);
        bangs.Close();
        using (var bangsPaint = Paint(Hair))
            canvas.DrawPath(bangs, bangsPaint);
        SideHair(canvas, true);
        SideHair(canvas, false);

        canvas.Save();
        canvas.Translate(430f + yaw * 14f, 0f);
        canvas.Scale(1f - 0.12f * Math.Abs(yaw), 1f);
        canvas.Translate(-430f, 0f);
        Ell(canvas, 396f, 150f, 11f, 10f, SKColors.White);
        Ell(canvas, 452f, 148f, 11f, 10f, SKColors.White);
        var blink = time % 3.2f < 0.12f ? 0.1f : 1f;
        var iris = new SKColor(0x4A, 0x2A, 0x18);
        Ell(canvas, 397f, 150f, 8f, 8f * blink, iris);
        Ell(canvas, 451f, 148f, 8f, 8f * blink, iris);
        Ell(canvas, 394f, 147f, 2.5f, 2.5f * blink, SKColors.White);
        Ell(canvas, 448f, 145f, 2.5f, 2.5f * blink, SKColors.White);
        using var lash = new SKPaint { Color = new SKColor(0x3A, 0x23, 0x14), Style = SKPaintStyle.Stroke, StrokeWidth = 3f, IsAntialias = true };
        using var lashPath = new SKPath();
        lashPath.AddArc(new SKRect(386f, 141f, 408f, 163f), 198f, 144f);
        canvas.DrawPath(lashPath, lash);
        using var lashPathRight = new SKPath();
        lashPathRight.AddArc(new SKRect(440f, 139f, 462f, 161f), 198f, 144f);
        canvas.DrawPath(lashPathRight, lash);
        canvas.DrawLine(new SKPoint(382f, 130f), new SKPoint(396f, 124f), lash);
        canvas.DrawLine(new SKPoint(396f, 124f), new SKPoint(410f, 128f), lash);
        canvas.DrawLine(new SKPoint(438f, 126f), new SKPoint(454f, 120f), lash);
        canvas.DrawLine(new SKPoint(454f, 120f), new SKPoint(468f, 128f), lash);
        canvas.Restore();

        var talking = (time > 2.6f && time < 4.8f) || (time > 6.6f && time < 9f) || (time > 12.4f && time < 14.4f);
        if (talking)
            Ell(canvas, 430f, 186f, 10f, 4f + 5f * Math.Abs(MathF.Sin(time * 14f)), new SKColor(0x8A, 0x33, 0x33));
        else
        {
            using var smile = new SKPaint { Color = new SKColor(0xB5, 0x54, 0x3F), Style = SKPaintStyle.Stroke, StrokeWidth = 3f, IsAntialias = true };
            using var mouth = new SKPath();
            mouth.AddArc(new SKRect(416f, 162f, 444f, 190f), 20f, 140f);
            canvas.DrawPath(mouth, smile);
        }

        canvas.Restore();
    }

    private static void SideHair(SKCanvas canvas, bool left)
    {
        using var path = new SKPath();
        if (left)
        {
            path.MoveTo(372f, 100f);
            path.CubicTo(352f, 140f, 356f, 190f, 364f, 210f);
            path.LineTo(378f, 200f);
            path.CubicTo(370f, 170f, 372f, 130f, 380f, 104f);
        }
        else
        {
            path.MoveTo(490f, 104f);
            path.CubicTo(508f, 140f, 506f, 190f, 498f, 210f);
            path.LineTo(486f, 200f);
            path.CubicTo(494f, 170f, 492f, 130f, 486f, 106f);
        }

        path.Close();
        using var paint = Paint(HairDeep);
        canvas.DrawPath(path, paint);
    }

    private static void Leg(SKCanvas canvas, float hipX, float swing, float x0, float x1, float ankle0, float ankle1)
    {
        canvas.Save();
        canvas.Translate(hipX, 470f);
        canvas.RotateRadians(swing);
        canvas.Translate(-hipX, -470f);
        Ell(canvas, ankle0, 1008f, 26f, 30f, Skin);
        using var cloth = new SKPath();
        cloth.MoveTo(x0, 462f);
        cloth.LineTo(x1, 462f);
        cloth.LineTo(ankle1, 985f);
        cloth.LineTo(ankle0 - 30f, 985f);
        cloth.Close();
        using (var pants = Paint(Pants))
            canvas.DrawPath(cloth, pants);
        using var crease = new SKPaint { Color = PantsDeep, Style = SKPaintStyle.Stroke, StrokeWidth = 3f, IsAntialias = true };
        canvas.DrawLine((x0 + x1) / 2f, 470f, ankle0 + 3f, 985f, crease);
        Ell(canvas, ankle0 - 4f, 1052f, 40f, 22f, Shoe, -0.12f);
        canvas.Restore();
    }

    private static SKPoint Arm(SKCanvas canvas, int side, float shoulderX, float shoulderY, float shoulderDeg, float elbowDeg, bool finger)
    {
        const float upper = 150f;
        const float fore = 135f;
        var shoulder = shoulderDeg * Degrees;
        var elbow = (shoulderDeg + elbowDeg) * Degrees;
        var upperTurn = -side * shoulder;
        var foreTurn = -side * elbow;
        var elbowX = shoulderX + side * MathF.Sin(shoulder) * upper;
        var elbowY = shoulderY + MathF.Cos(shoulder) * upper;
        var wristX = elbowX + side * MathF.Sin(elbow) * fore;
        var wristY = elbowY + MathF.Cos(elbow) * fore;

        canvas.Save();
        canvas.Translate(wristX, wristY);
        canvas.RotateRadians(foreTurn);
        Ell(canvas, 0f, 36f, 16f, 40f, Skin);
        Ell(canvas, -side * 13f, 22f, 6f, 16f, Skin, -side * 0.25f);
        if (finger)
            Seg(canvas, 0f, 40f, 0f, 8f, 38f, Skin, 4f);
        canvas.Restore();

        Seg(canvas, elbowX, elbowY, foreTurn, 44f, fore, Sleeve, 18f);
        canvas.Save();
        canvas.Translate(wristX, wristY);
        canvas.RotateRadians(foreTurn);
        using (var cuff = Paint(Cuff))
            canvas.DrawRoundRect(new SKRect(-24f, -20f, 24f, 2f), 6f, 6f, cuff);
        canvas.Restore();
        Seg(canvas, shoulderX, shoulderY, upperTurn, 52f, upper, Sleeve, 22f);
        Dot(canvas, elbowX, elbowY, 24f, Sleeve);
        Dot(canvas, shoulderX, shoulderY, 28f, Sleeve);
        return new SKPoint(wristX, wristY);
    }

    private static float MapClip(string clipId, float time, float duration)
    {
        var span = duration <= 0.05f ? 1f : duration;
        var unit = Math.Clamp(time / span, 0f, 1f);
        var (start, end) = clipId switch
        {
            "act-explain" => (1.5f, 4.6f),
            "act-return" => (4.6f, 5.6f),
            "act-wave" => (5.6f, 9f),
            "act-settle" => (9f, 9.8f),
            "act-walk" => (10f, 11.6f),
            "act-reach" => (11.6f, 13.8f),
            "act-return-reach" => (13.8f, 14.6f),
            "act-hold" => (14.6f, 15f),
            _ => (0f, 1.5f),
        };
        return start + (end - start) * unit;
    }

    private readonly record struct Pose(
        float LeftShoulder, float LeftElbow, float Wave,
        float RightShoulder, float RightElbow, float X, float Tilt);

    private static Pose Sample(float time)
    {
        time = Math.Clamp(time, 0f, Film);
        var index = 0;
        while (index < Keys.Length - 2 && time >= Keys[index + 1][0])
            index++;
        var a = Keys[index];
        var b = Keys[index + 1];
        var gap = b[0] - a[0];
        var unit = gap <= 0 ? 1f : Ease(Math.Clamp((time - a[0]) / gap, 0f, 1f));
        float Mix(int column) => a[column] + (b[column] - a[column]) * unit;
        return new Pose(Mix(1), Mix(2), Mix(3), Mix(4), Mix(5), Mix(6), Mix(7));
    }

    private static float LerpTable(float[][] table, float time)
    {
        time = Math.Clamp(time, 0f, Film);
        var index = 0;
        while (index < table.Length - 2 && time >= table[index + 1][0])
            index++;
        var a = table[index];
        var b = table[index + 1];
        var gap = b[0] - a[0];
        var unit = gap <= 0 ? 1f : Ease(Math.Clamp((time - a[0]) / gap, 0f, 1f));
        return a[1] + (b[1] - a[1]) * unit;
    }

    private static float Ease(float unit) =>
        unit < 0.5f
            ? 4f * unit * unit * unit
            : 1f - MathF.Pow(-2f * unit + 2f, 3f) / 2f;

    private static void CollarFlap(SKCanvas canvas, float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3)
    {
        using var path = new SKPath();
        path.MoveTo(x0, y0);
        path.LineTo(x1, y1);
        path.LineTo(x2, y2);
        path.LineTo(x3, y3);
        path.Close();
        using var paint = Paint(Collar);
        canvas.DrawPath(path, paint);
    }

    private static void Seg(SKCanvas canvas, float x, float y, float radians, float width, float length, SKColor color, float radius)
    {
        canvas.Save();
        canvas.Translate(x, y);
        canvas.RotateRadians(radians);
        using var paint = Paint(color);
        canvas.DrawRoundRect(new SKRect(-width / 2f, 0f, width / 2f, length), radius, radius, paint);
        canvas.Restore();
    }

    private static void Ell(SKCanvas canvas, float x, float y, float radiusX, float radiusY, SKColor color, float radians = 0f)
    {
        canvas.Save();
        canvas.Translate(x, y);
        if (Math.Abs(radians) > 0.0001f)
            canvas.RotateRadians(radians);
        using var paint = Paint(color);
        canvas.DrawOval(new SKRect(-radiusX, -radiusY, radiusX, radiusY), paint);
        canvas.Restore();
    }

    private static void Dot(SKCanvas canvas, float x, float y, float radius, SKColor color)
    {
        using var paint = Paint(color);
        canvas.DrawCircle(x, y, radius, paint);
    }

    private static SKPaint Paint(SKColor color) => new()
    {
        Color = color.WithAlpha(255),
        Style = SKPaintStyle.Fill,
        IsAntialias = true,
    };
}
