using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace KitPlatform.Packs.Content.VisualIntelligence;

public static class IllustratedSceneRenderer
{
    public const int Width = 1280;
    public const int Height = 720;

    /// <summary>
    /// Caption glyphs are a Latin bitmap plus Vietnamese tone marks, horns, and đ.
    /// Unknown letters are skipped. Narration is never substituted for a missing glyph.
    /// </summary>
    public static string UndrawableLetters(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var seen = new HashSet<char>();
        var buffer = new StringBuilder();
        foreach (var ch in text)
        {
            if (Glyphs.CanDraw(ch) || !seen.Add(ch)) continue;
            buffer.Append(ch);
        }
        return buffer.ToString();
    }

    public readonly record struct CaptionInk(int Above, int Below, int Total);

    /// <summary>Counts caption ink. Above is tone marks. Below is the dot of ạ/ệ. Total includes the letter body.</summary>
    public static CaptionInk MeasureCaption(string? text)
    {
        const int baseline = 12;
        var canvas = new Canvas(280, 28, 0xFFFFFF);
        Glyphs.Draw(canvas, 4, baseline, text ?? "", 0x111111);
        var above = 0;
        var below = 0;
        var total = 0;
        for (var y = 0; y < canvas.H; y++)
        {
            for (var x = 0; x < canvas.W; x++)
            {
                if (canvas.Get(x, y) == 0xFFFFFF) continue;
                total++;
                if (y < baseline) above++;
                if (y >= baseline + 7) below++;
            }
        }
        return new CaptionInk(above, below, total);
    }

    public static RenderedFrame Render(
        VisualSession session,
        ShotVisualDemand demand,
        VisualBrandStyle brand,
        GenerationContract contract)
    {
        var identity = session.FindCharacter(demand.CharacterId) ?? session.FindCharacter(contract.CharacterId);
        var code = contract.Medium == "code" || brand.Motif == "candle-axis";
        var painted = StudioScenePainter.Paint(brand, demand, identity, code);
        var canvas = new Canvas(Width, Height, brand.Background);
        canvas.Blit(painted.Rgb);
        canvas.Set(0, 0, brand.Background);
        canvas.Set(1, 0, brand.Background);
        canvas.Set(0, 1, brand.Background);
        canvas.Set(1, 1, brand.Background);
        var parts = painted.Parts;
        SceneBox? box = null;
        if (painted.Box is { } pack)
            box = canvas.BlankBox(pack.X, pack.Y, pack.W, pack.H, brand.Background, brand.Ink, brand.LineWeight, parts);
        uint hair = 0;
        uint outfit = 0;
        var face = "";
        var mark = "";
        if (painted.Identity)
        {
            canvas.Set(painted.HairX, painted.HairY, painted.Hair);
            canvas.Set(painted.OutfitX, painted.OutfitY, painted.Outfit);
            hair = painted.Hair;
            outfit = painted.Outfit;
            face = painted.Face;
            mark = painted.Mark;
        }

        var boxBottom = -1;
        var boxInk = false;
        if (box is { } drawn)
        {
            boxBottom = drawn.Bottom;
            boxInk = canvas.InteriorHas(drawn, brand.Ink, brand.LineWeight + 2);
        }

        var overlay = contract.ControlledOverlay.FirstOrDefault() ?? "";
        var captionTop = -1;
        if (overlay.Length > 0)
        {
            captionTop = canvas.H - 72;
            canvas.Rect(0, captionTop, canvas.W, 72, Mix(brand.Background, brand.Ink, 0.08f));
            canvas.Rect(0, captionTop, canvas.W, Math.Max(2, brand.LineWeight), brand.Accent);
            Glyphs.Draw(canvas, 28, captionTop + 18, overlay, brand.Ink, 3);
        }

        var structure = brand.Code + "/" + demand.Setting + ":" + string.Join(",", parts);
        var png = canvas.ToPng(overlay);
        return new RenderedFrame
        {
            Png = png,
            Sha256 = VisualHashes.Sha256(png),
            SampledHair = hair,
            SampledOutfit = outfit,
            FaceShape = face,
            Mark = mark,
            LineWeight = brand.LineWeight,
            Motif = brand.Motif,
            Medium = contract.Medium,
            StructureCode = structure,
            OverlayText = overlay,
            BoxHasInk = boxInk,
            BoxBottom = boxBottom,
            CaptionTop = captionTop,
            LumaGrid = canvas.Luma(32, 18),
            RendererId = contract.RendererId,
            GroundSample = canvas.Get(1, 1),
        };
    }

    private static SceneBox? DrawWorld(Canvas canvas, VisualBrandStyle brand, ShotVisualDemand demand, List<string> parts)
    {
        var prop = brand.ForbiddenMotifs.Contains(demand.Prop, StringComparer.OrdinalIgnoreCase) ? "" : demand.Prop;
        SceneBox? box = null;
        switch (brand.Motif)
        {
            case "home-threshold":
                DrawHome(canvas, brand, demand.Setting, parts);
                break;
            case "counter-grid":
                box = DrawPharmacy(canvas, brand, demand.Setting, prop, parts);
                break;
            case "candle-axis":
                DrawTrade(canvas, brand, demand.Setting, parts);
                break;
            case "tea-terrace":
                DrawTea(canvas, brand, demand.Setting, parts);
                break;
            default:
                DrawRegistered(canvas, brand, demand.Setting, parts);
                break;
        }
        if (prop == "blank-box" && box is null && brand.Motif != "candle-axis")
            box = canvas.BlankBox(48, 168, 128, 92, brand.Prop, brand.Ink, brand.LineWeight, parts);
        return box;
    }

    private static void DrawHome(Canvas canvas, VisualBrandStyle brand, string setting, List<string> parts)
    {
        canvas.Rect(0, 250, canvas.W, 110, Mix(brand.Secondary, brand.Background, 0.45f));
        switch (setting)
        {
            case "kitchen":
                parts.Add("window"); parts.Add("table"); parts.Add("bowl");
                canvas.Rect(36, 36, 120, 90, Mix(brand.Accent, 0xFFFFFF, 0.55f));
                canvas.Rect(48, 48, 40, 60, brand.Secondary);
                canvas.Rect(150, 210, 280, 18, brand.Prop);
                canvas.FillEllipse(250, 200, 28, 12, brand.Accent);
                break;
            case "doorway":
                parts.Add("door"); parts.Add("plant"); parts.Add("shoes");
                canvas.Rect(250, 40, 130, 220, brand.Prop);
                canvas.Rect(262, 54, 106, 190, Mix(brand.Accent, brand.Background, 0.35f));
                canvas.FillEllipse(470, 200, 28, 40, brand.Secondary);
                canvas.FillEllipse(150, 268, 22, 8, brand.Ink);
                canvas.FillEllipse(190, 270, 22, 8, brand.Ink);
                break;
            case "living-room":
                parts.Add("sofa"); parts.Add("lamp"); parts.Add("rug");
                canvas.Rect(80, 190, 300, 70, brand.Accent);
                canvas.Rect(70, 170, 40, 30, brand.Accent);
                canvas.Rect(500, 120, 14, 110, brand.Prop);
                canvas.FillEllipse(507, 108, 26, 18, brand.Secondary);
                canvas.FillEllipse(280, 280, 160, 28, brand.Secondary);
                break;
            case "park":
                parts.Add("horizon"); parts.Add("tree"); parts.Add("path");
                canvas.Rect(0, 200, canvas.W, 160, brand.Secondary);
                canvas.Rect(0, 0, canvas.W, 200, Mix(brand.Accent, 0xFFFFFF, 0.72f));
                canvas.Rect(90, 120, 18, 90, brand.Prop);
                canvas.FillEllipse(99, 100, 46, 40, brand.Secondary);
                canvas.FillEllipse(420, 130, 16, 50, Mix(brand.Prop, brand.Ink, 0.2f));
                break;
            case "classroom":
                parts.Add("board"); parts.Add("bench");
                canvas.Rect(160, 40, 300, 120, Mix(brand.Secondary, 0xFFFFFF, 0.35f));
                canvas.StrokeRect(160, 40, 300, 120, brand.Ink, brand.LineWeight);
                canvas.Rect(80, 230, 460, 16, brand.Prop);
                canvas.Rect(80, 270, 460, 16, brand.Prop);
                break;
            case "night-window":
                parts.Add("moon"); parts.Add("window"); parts.Add("dark-wall");
                canvas.Rect(0, 0, canvas.W, canvas.H, Mix(brand.Ink, brand.Background, 0.55f));
                canvas.FillEllipse(500, 70, 28, 28, brand.Prop);
                canvas.Rect(60, 50, 140, 120, Mix(brand.Accent, brand.Background, 0.4f));
                canvas.StrokeRect(60, 50, 140, 120, brand.Ink, brand.LineWeight);
                break;
            case "yard":
                parts.Add("fence"); parts.Add("ground");
                canvas.Rect(0, 220, canvas.W, 140, brand.Secondary);
                for (var x = 20; x < 620; x += 36)
                    canvas.Rect(x, 150, brand.LineWeight + 2, 80, brand.Prop);
                canvas.Rect(20, 150, 600, brand.LineWeight + 2, brand.Prop);
                break;
            case "bedroom":
                parts.Add("bed"); parts.Add("pillow");
                canvas.Rect(120, 180, 360, 90, brand.Prop);
                canvas.Rect(140, 160, 120, 36, 0xFFFFFF);
                canvas.Rect(120, 250, 360, 18, brand.Accent);
                break;
            case "balcony":
                parts.Add("rail"); parts.Add("sky");
                canvas.Rect(0, 0, canvas.W, 180, Mix(brand.Accent, 0xFFFFFF, 0.75f));
                for (var x = 30; x < 610; x += 28)
                    canvas.Rect(x, 180, brand.LineWeight + 1, 70, brand.Ink);
                canvas.Rect(20, 176, 600, brand.LineWeight + 2, brand.Ink);
                break;
            case "market-walk":
                parts.Add("awning"); parts.Add("stall");
                canvas.Rect(40, 150, 160, 100, brand.Prop);
                canvas.FillTriangle(40, 150, 120, 80, 200, 150, brand.Accent);
                canvas.Rect(250, 160, 160, 90, brand.Prop);
                canvas.FillTriangle(250, 160, 330, 96, 410, 160, brand.Secondary);
                break;
            case "school-gate":
                parts.Add("gate"); parts.Add("posts");
                canvas.Rect(180, 80, 18, 180, brand.Ink);
                canvas.Rect(420, 80, 18, 180, brand.Ink);
                canvas.Rect(180, 80, 258, 12, brand.Ink);
                canvas.Rect(198, 120, 222, brand.LineWeight, brand.Prop);
                break;
            case "rain-window":
                parts.Add("rain"); parts.Add("window");
                canvas.Rect(200, 40, 200, 140, Mix(brand.Accent, 0xFFFFFF, 0.5f));
                canvas.StrokeRect(200, 40, 200, 140, brand.Ink, brand.LineWeight);
                for (var i = 0; i < 18; i++)
                    canvas.Line(40 + i * 32, 20, 20 + i * 32, 200, brand.Ink, 1);
                break;
            case "work-counter":
                parts.Add("low-table"); parts.Add("plant"); parts.Add("window");
                canvas.Rect(48, 48, 110, 80, Mix(brand.Accent, 0xFFFFFF, 0.45f));
                canvas.FillEllipse(520, 210, 30, 36, brand.Secondary);
                canvas.Rect(180, 214, 240, 16, brand.Prop);
                break;
            default:
                parts.Add("room");
                canvas.Rect(40, 40, 80, 80, brand.Accent);
                canvas.Rect(80, 220, 200, 16, brand.Prop);
                break;
        }
    }

    private static SceneBox? DrawPharmacy(Canvas canvas, VisualBrandStyle brand, string setting, string prop, List<string> parts)
    {
        canvas.Rect(0, 240, canvas.W, 120, Mix(brand.Secondary, brand.Background, 0.25f));
        SceneBox? box = null;
        switch (setting)
        {
            case "pharmacy-counter":
            case "work-counter":
                parts.Add("counter"); parts.Add("shelf"); parts.Add("blank-pack");
                canvas.Rect(0, 200, canvas.W, 28, brand.Prop);
                canvas.Rect(40, 40, 560, 16, brand.Ink);
                for (var i = 0; i < 5; i++)
                    canvas.Rect(60 + i * 100, 70, 70, 90, i % 2 == 0 ? brand.Accent : brand.Secondary);
                box = canvas.BlankBox(80, 150, 70, 48, brand.Background, brand.Ink, brand.LineWeight, parts);
                break;
            case "shelf-aisle":
                parts.Add("tall-shelf"); parts.Add("gap");
                for (var col = 0; col < 3; col++)
                    canvas.Rect(70 + col * 180, 30, 140, 200, Mix(brand.Prop, brand.Ink, 0.08f));
                canvas.Rect(250, 90, 100, 40, brand.Background);
                break;
            case "stock-room":
                parts.Add("crate"); parts.Add("stack");
                canvas.Rect(80, 160, 120, 80, brand.Prop);
                canvas.Rect(110, 100, 120, 80, brand.Secondary);
                canvas.Rect(360, 140, 140, 100, brand.Accent);
                break;
            case "delivery-door":
                parts.Add("door"); parts.Add("crate");
                canvas.Rect(300, 30, 160, 210, brand.Ink);
                canvas.Rect(318, 48, 124, 176, brand.Background);
                canvas.Rect(60, 180, 100, 70, brand.Prop);
                break;
            case "consult-corner":
                parts.Add("chair"); parts.Add("low-table");
                canvas.FillEllipse(160, 210, 40, 16, brand.Accent);
                canvas.FillEllipse(460, 210, 40, 16, brand.Secondary);
                canvas.Rect(270, 200, 100, 14, brand.Prop);
                break;
            case "cash-desk":
                parts.Add("drawer"); parts.Add("counter");
                canvas.Rect(80, 160, 420, 80, brand.Prop);
                canvas.Rect(110, 180, 90, 28, brand.Background);
                canvas.StrokeRect(110, 180, 90, 28, brand.Ink, brand.LineWeight);
                break;
            case "night-close":
                parts.Add("shutter");
                canvas.Rect(80, 40, 480, 200, brand.Ink);
                for (var y = 50; y < 230; y += 18)
                    canvas.Rect(90, y, 460, 4, brand.Background);
                break;
            case "cleaning":
                parts.Add("bucket"); parts.Add("floor");
                canvas.FillEllipse(180, 230, 36, 18, brand.Accent);
                canvas.Rect(160, 180, 40, 50, brand.Accent);
                canvas.Rect(0, 250, canvas.W, 20, Mix(brand.Ink, brand.Background, 0.15f));
                break;
            case "waiting-bench":
                parts.Add("bench"); parts.Add("clock-face");
                canvas.Rect(80, 200, 360, 18, brand.Prop);
                canvas.Rect(100, 218, 12, 40, brand.Ink);
                canvas.Rect(400, 218, 12, 40, brand.Ink);
                canvas.FillEllipse(520, 80, 28, 28, brand.Background);
                canvas.StrokeEllipse(520, 80, 28, 28, brand.Ink, brand.LineWeight);
                break;
            case "handoff-counter":
                parts.Add("counter"); parts.Add("two-places");
                canvas.Rect(40, 210, 560, 24, brand.Prop);
                break;
            case "expiry-shelf":
                parts.Add("shelf"); parts.Add("blank-pack");
                canvas.Rect(60, 80, 500, 12, brand.Ink);
                canvas.Rect(60, 150, 500, 12, brand.Ink);
                box = canvas.BlankBox(90, 96, 64, 48, brand.Background, brand.Ink, brand.LineWeight, parts);
                canvas.Rect(200, 100, 64, 44, brand.Prop);
                break;
            case "opening-gate":
                parts.Add("gate"); parts.Add("counter");
                canvas.Rect(40, 60, 16, 180, brand.Ink);
                canvas.Rect(200, 60, 16, 180, brand.Ink);
                canvas.Rect(40, 60, 176, 10, brand.Accent);
                canvas.Rect(260, 190, 300, 22, brand.Prop);
                break;
            default:
                parts.Add("counter");
                canvas.Rect(0, 200, canvas.W, 24, brand.Prop);
                break;
        }
        return box;
    }

    private static void DrawTrade(Canvas canvas, VisualBrandStyle brand, string setting, List<string> parts)
    {
        canvas.Rect(0, 0, canvas.W, canvas.H, brand.Background);
        parts.Add("axis");
        var left = setting == "session-close" ? 280 : 70;
        var width = setting == "dual-screen" ? 220 : 500;
        canvas.Line(left, 260, left + width, 260, brand.Ink, 1);
        canvas.Line(left, 60, left, 260, brand.Ink, 1);
        var candles = setting switch
        {
            "risk-board" => 4,
            "candle-cluster" => 14,
            "range-bars" => 8,
            "session-open" => 5,
            _ => 9,
        };
        for (var i = 0; i < candles; i++)
        {
            var x = left + 24 + i * (width / (candles + 1));
            var body = 28 + ((i * 37) % 70);
            var up = i % 3 != 0;
            var color = up ? brand.Accent : brand.Secondary;
            canvas.Line(x, 80 + (i * 13) % 40, x, 250, brand.Ink, 1);
            canvas.Rect(x - 6, 110 + (i % 4) * 12, 12, body, color);
        }
        parts.Add(setting == "dual-screen" ? "two-monitors" : "candles");
        if (setting == "dual-screen")
        {
            canvas.StrokeRect(360, 40, 240, 200, brand.Ink, brand.LineWeight);
            canvas.Line(400, 210, 560, 210, brand.Ink, 1);
            canvas.Line(400, 80, 400, 210, brand.Ink, 1);
        }
        else if (setting == "review-table")
        {
            parts.Add("blank-pages");
            canvas.Rect(400, 70, 90, 120, brand.Prop);
            canvas.Rect(500, 90, 80, 110, Mix(brand.Prop, brand.Ink, 0.2f));
        }
        else
        {
            parts.Add("monitor");
            canvas.StrokeRect(40, 28, 180, 28, brand.Prop, 1);
        }
    }

    private static void DrawTea(Canvas canvas, VisualBrandStyle brand, string setting, List<string> parts)
    {
        canvas.Rect(0, 0, canvas.W, 160, Mix(brand.Accent, 0xFFFFFF, 0.55f));
        canvas.Rect(0, 200, canvas.W, 160, brand.Secondary);
        parts.Add("terrace");
        for (var i = 0; i < 4; i++)
            canvas.Rect(0, 120 + i * 28, canvas.W, 8, Mix(brand.Secondary, brand.Background, 0.3f));
        if (setting == "tea-pour")
        {
            parts.Add("pot"); parts.Add("stream");
            canvas.FillEllipse(300, 150, 30, 18, brand.Prop);
            canvas.Line(330, 150, 380, 190, brand.Ink, brand.LineWeight);
            canvas.FillEllipse(400, 200, 24, 10, brand.Accent);
        }
        else if (setting == "tea-path")
        {
            parts.Add("path"); parts.Add("bush");
            canvas.FillEllipse(320, 250, 40, 16, Mix(brand.Prop, brand.Background, 0.2f));
            canvas.FillEllipse(120, 180, 26, 16, brand.Accent);
            canvas.FillEllipse(500, 170, 30, 18, brand.Accent);
        }
        else
        {
            parts.Add("cup"); parts.Add("round-table");
            canvas.FillEllipse(320, 230, 70, 18, brand.Prop);
            canvas.FillEllipse(320, 200, 16, 12, brand.Background);
            canvas.StrokeEllipse(320, 200, 16, 12, brand.Ink, brand.LineWeight);
        }
    }

    private static void DrawRegistered(Canvas canvas, VisualBrandStyle brand, string setting, List<string> parts)
    {
        parts.Add("registered-motif");
        var hash = 17;
        foreach (var ch in brand.Motif + "/" + setting)
            hash = hash * 31 + ch;
        var count = 3 + Math.Abs(hash) % 4;
        for (var i = 0; i < count; i++)
        {
            var x = 40 + ((hash >> (i * 3)) & 255) % 480;
            var y = 40 + ((hash >> (i + 2)) & 127) % 180;
            if (((hash >> i) & 1) == 0) canvas.FillEllipse(x, y, 20 + i * 4, 14, i % 2 == 0 ? brand.Accent : brand.Secondary);
            else canvas.Rect(x, y, 36 + i * 8, 18, i % 2 == 0 ? brand.Accent : brand.Prop);
        }
        parts.Add("n" + count);
    }

    private static void DrawSimplified(Canvas canvas, VisualBrandStyle brand, ShotVisualDemand demand, List<string> parts)
    {
        var layout = Figure(demand, 22);
        parts.Add(demand.Setting == "handoff-counter" ? "two-simplified-figures" : "simplified-figure");
        DrawBody(canvas, layout, brand.Ink, brand.Prop, brand.Skin, brand.LineWeight, demand.Pose, false);
        DrawFace(canvas, layout, brand, new CharacterIdentity("", "", "", "round", "", 0, 0, "none", layout.Head), demand.Expression);
        if (demand.Setting == "handoff-counter")
        {
            var other = layout with { Ax = layout.Ax - 160 };
            DrawBody(canvas, other, brand.Ink, brand.Secondary, brand.Skin, brand.LineWeight, "stand", false);
        }
    }

    private static (uint Hair, uint Outfit, string Face, string Mark) DrawIdentity(
        Canvas canvas, VisualBrandStyle brand, ShotVisualDemand demand, CharacterIdentity identity, List<string> parts)
    {
        var layout = Figure(demand, identity.HeadScale);
        parts.Add("identity-figure");
        parts.Add(demand.Pose);
        parts.Add(demand.Camera);
        DrawBody(canvas, layout, identity.HairColor, identity.OutfitColor, brand.Skin, brand.LineWeight, demand.Pose, true);
        DrawFace(canvas, layout, brand, identity, demand.Expression);
        var hair = canvas.Get(layout.HairX, layout.HairY);
        var outfit = canvas.Get(layout.OutfitX, layout.OutfitY);
        return (hair, outfit, identity.FaceShape, identity.Mark);
    }

    private static void DrawBody(
        Canvas canvas, FigureLayout layout, uint hair, uint outfit, uint skin, int line, string pose, bool identity)
    {
        var head = layout.Head;
        var ax = layout.Ax;
        var ay = layout.Ay;
        var torsoH = pose == "kneel" ? head : (int)(head * 2.1);
        var torsoW = (int)(head * 1.7);
        if (pose == "lean") ax += head / 2;
        canvas.Rect(ax - torsoW / 2, ay, torsoW, torsoH, outfit);
        if (pose == "sit")
            canvas.Rect(ax - torsoW / 2, ay + torsoH - 4, (int)(head * 2.4), head / 2, outfit);
        else if (pose == "walk")
        {
            canvas.Rect(ax - head / 2, ay + torsoH, head / 3, head, outfit);
            canvas.Rect(ax + head / 5, ay + torsoH, head / 3, (int)(head * 1.3), outfit);
        }
        else if (pose == "kneel")
            canvas.Rect(ax - head, ay + torsoH, head * 2, head / 2, outfit);
        else
        {
            canvas.Rect(ax - head / 3, ay + torsoH, head / 3, (int)(head * 1.4), outfit);
            canvas.Rect(ax + 2, ay + torsoH, head / 3, (int)(head * 1.4), outfit);
        }

        if (pose == "reach")
            canvas.Rect(ax + torsoW / 2, ay + head / 3, head * 2, Math.Max(4, line + 2), outfit);
        else if (pose == "lean")
            canvas.Line(ax - torsoW / 2, ay + head / 2, ax - head * 2, ay + head, outfit, line + 2);
        else
        {
            canvas.Line(ax - torsoW / 2, ay + head / 2, ax - torsoW, ay + head, outfit, line + 1);
            canvas.Line(ax + torsoW / 2, ay + head / 2, ax + torsoW, ay + head, outfit, line + 1);
        }

        canvas.FillEllipse(ax, ay - head / 6, head, (int)(head * 1.05), skin);
        if (identity)
        {
            canvas.FillEllipse(layout.HairX, layout.HairY, (int)(head * 1.05), (int)(head * 0.72), hair);
            canvas.Set(layout.HairX, layout.HairY, hair);
            canvas.Set(layout.OutfitX, layout.OutfitY, outfit);
        }
    }

    private static void DrawFace(Canvas canvas, FigureLayout layout, VisualBrandStyle brand, CharacterIdentity identity, string expression)
    {
        var ax = layout.Ax;
        var ay = layout.Ay - layout.Head / 6;
        var eye = Math.Max(2, layout.Head / 10);
        var y = ay - layout.Head / 8;
        if (expression == "focus") eye = Math.Max(2, eye - 1);
        canvas.FillEllipse(ax - layout.Head / 3, y, eye, eye, brand.Ink);
        canvas.FillEllipse(ax + layout.Head / 3, y, eye, eye, brand.Ink);
        if (expression == "concern")
        {
            canvas.Line(ax - layout.Head / 2, y - eye * 3, ax - layout.Head / 5, y - eye, brand.Ink, 2);
            canvas.Line(ax + layout.Head / 2, y - eye * 3, ax + layout.Head / 5, y - eye, brand.Ink, 2);
        }
        var mouthY = ay + layout.Head / 3;
        if (expression == "smile")
            canvas.Line(ax - eye * 2, mouthY, ax + eye * 2, mouthY - 2, brand.Ink, 2);
        else if (expression == "concern")
            canvas.Line(ax - eye * 2, mouthY, ax + eye * 2, mouthY + 3, brand.Ink, 2);
        else
            canvas.Line(ax - eye * 2, mouthY, ax + eye, mouthY, brand.Ink, 2);

        if (identity.Mark == "freckle")
        {
            canvas.Set(ax + layout.Head / 2, ay + 2, brand.Secondary);
            canvas.Set(ax + layout.Head / 2 + 3, ay + 4, brand.Secondary);
            canvas.Set(ax + layout.Head / 2 + 1, ay + 7, brand.Secondary);
        }
        else if (identity.Mark == "glasses")
        {
            canvas.StrokeEllipse(ax - layout.Head / 3, y, eye + 4, eye + 3, brand.Ink, 2);
            canvas.StrokeEllipse(ax + layout.Head / 3, y, eye + 4, eye + 3, brand.Ink, 2);
            canvas.Line(ax - layout.Head / 3 + eye + 4, y, ax + layout.Head / 3 - eye - 4, y, brand.Ink, 2);
        }
    }

    private static FigureLayout Figure(ShotVisualDemand demand, int headScale)
    {
        var scale = demand.Camera switch { "close" => 146, "wide" => 70, _ => 108 };
        var head = Math.Max(16, headScale * scale / 100);
        var ax = demand.Pose switch
        {
            "sit" => 130,
            "lean" => 240,
            "reach" => 470,
            "walk" => 560,
            "kneel" => 70,
            _ => 340,
        };
        var ay = demand.Pose switch
        {
            "sit" => 246,
            "kneel" => 268,
            "lean" => 176,
            "reach" => 188,
            _ => 198,
        };
        if (demand.Camera == "close") ay += 24;
        if (demand.Camera == "wide") ay -= 16;
        var hairX = ax;
        var hairY = ay - head / 6 - (int)(head * 0.55);
        var outfitX = ax;
        var outfitY = ay + head / 2;
        return new FigureLayout(ax, ay, head, hairX, hairY, outfitX, outfitY);
    }

    private readonly record struct FigureLayout(int Ax, int Ay, int Head, int HairX, int HairY, int OutfitX, int OutfitY);

    private readonly record struct SceneBox(int X, int Y, int W, int H)
    {
        public int Bottom => Y + H;
    }

    private static uint Mix(uint a, uint b, float t)
    {
        byte MixCh(int shift)
        {
            var av = (a >> shift) & 0xFF;
            var bv = (b >> shift) & 0xFF;
            return (byte)(av + (bv - av) * t);
        }
        return ((uint)MixCh(16) << 16) | ((uint)MixCh(8) << 8) | MixCh(0);
    }

    private sealed class Canvas
    {
        public readonly int W;
        public readonly int H;
        private readonly byte[] _rgb;

        public Canvas(int w, int h, uint background)
        {
            W = w;
            H = h;
            _rgb = new byte[w * h * 3];
            Clear(background);
        }

        public void Clear(uint color)
        {
            for (var i = 0; i < _rgb.Length; i += 3)
            {
                _rgb[i] = (byte)((color >> 16) & 0xFF);
                _rgb[i + 1] = (byte)((color >> 8) & 0xFF);
                _rgb[i + 2] = (byte)(color & 0xFF);
            }
        }

        public void Blit(byte[] rgb)
        {
            if (rgb.Length < _rgb.Length) return;
            Buffer.BlockCopy(rgb, 0, _rgb, 0, _rgb.Length);
        }

        public void Set(int x, int y, uint color)
        {
            if ((uint)x >= (uint)W || (uint)y >= (uint)H) return;
            var i = (y * W + x) * 3;
            _rgb[i] = (byte)((color >> 16) & 0xFF);
            _rgb[i + 1] = (byte)((color >> 8) & 0xFF);
            _rgb[i + 2] = (byte)(color & 0xFF);
        }

        public uint Get(int x, int y)
        {
            if ((uint)x >= (uint)W || (uint)y >= (uint)H) return 0;
            var i = (y * W + x) * 3;
            return ((uint)_rgb[i] << 16) | ((uint)_rgb[i + 1] << 8) | _rgb[i + 2];
        }

        public void Rect(int x, int y, int w, int h, uint color)
        {
            for (var yy = y; yy < y + h; yy++)
                for (var xx = x; xx < x + w; xx++)
                    Set(xx, yy, color);
        }

        public void StrokeRect(int x, int y, int w, int h, uint color, int weight)
        {
            Rect(x, y, w, weight, color);
            Rect(x, y + h - weight, w, weight, color);
            Rect(x, y, weight, h, color);
            Rect(x + w - weight, y, weight, h, color);
        }

        public void FillEllipse(int cx, int cy, int rx, int ry, uint color)
        {
            if (rx <= 0 || ry <= 0) return;
            for (var y = cy - ry; y <= cy + ry; y++)
            {
                for (var x = cx - rx; x <= cx + rx; x++)
                {
                    var dx = (x - cx) / (double)rx;
                    var dy = (y - cy) / (double)ry;
                    if (dx * dx + dy * dy <= 1) Set(x, y, color);
                }
            }
        }

        public void StrokeEllipse(int cx, int cy, int rx, int ry, uint color, int weight)
        {
            var inner = Get(cx, cy);
            FillEllipse(cx, cy, rx, ry, color);
            FillEllipse(cx, cy, Math.Max(1, rx - weight), Math.Max(1, ry - weight), inner);
        }

        public void FillTriangle(int x0, int y0, int x1, int y1, int x2, int y2, uint color)
        {
            var minY = Math.Max(0, Math.Min(y0, Math.Min(y1, y2)));
            var maxY = Math.Min(H - 1, Math.Max(y0, Math.Max(y1, y2)));
            for (var y = minY; y <= maxY; y++)
            {
                var nodes = new List<int>(3);
                Edge(x0, y0, x1, y1, y, nodes);
                Edge(x1, y1, x2, y2, y, nodes);
                Edge(x2, y2, x0, y0, y, nodes);
                if (nodes.Count < 2) continue;
                nodes.Sort();
                for (var i = 0; i + 1 < nodes.Count; i += 2)
                    for (var x = nodes[i]; x <= nodes[i + 1]; x++)
                        Set(x, y, color);
            }
        }

        public void Line(int x0, int y0, int x1, int y1, uint color, int weight)
        {
            var dx = Math.Abs(x1 - x0);
            var dy = Math.Abs(y1 - y0);
            var sx = x0 < x1 ? 1 : -1;
            var sy = y0 < y1 ? 1 : -1;
            var err = dx - dy;
            var half = Math.Max(0, weight / 2);
            while (true)
            {
                for (var oy = -half; oy <= half; oy++)
                    for (var ox = -half; ox <= half; ox++)
                        Set(x0 + ox, y0 + oy, color);
                if (x0 == x1 && y0 == y1) break;
                var e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }
            }
        }

        public SceneBox BlankBox(int x, int y, int w, int h, uint fill, uint ink, int weight, List<string> parts)
        {
            Rect(x, y, w, h, fill);
            StrokeRect(x, y, w, h, ink, Math.Max(1, weight));
            parts.Add("blank-box");
            return new SceneBox(x, y, w, h);
        }

        public bool InteriorHas(SceneBox box, uint ink, int inset)
        {
            var x0 = box.X + inset;
            var y0 = box.Y + inset;
            var x1 = box.X + box.W - inset;
            var y1 = box.Y + box.H - inset;
            for (var y = y0; y < y1; y++)
                for (var x = x0; x < x1; x++)
                    if (Get(x, y) == ink) return true;
            return false;
        }

        public byte[] Luma(int cols, int rows)
        {
            var grid = new byte[cols * rows];
            var cw = W / cols;
            var ch = H / rows;
            for (var row = 0; row < rows; row++)
            {
                for (var col = 0; col < cols; col++)
                {
                    long sum = 0;
                    var count = 0;
                    for (var y = row * ch; y < (row + 1) * ch; y += 2)
                    {
                        for (var x = col * cw; x < (col + 1) * cw; x += 2)
                        {
                            var i = (y * W + x) * 3;
                            sum += _rgb[i] + _rgb[i + 1] + _rgb[i + 2];
                            count++;
                        }
                    }
                    grid[row * cols + col] = (byte)(count == 0 ? 0 : (sum / count / 3) / 8);
                }
            }
            return grid;
        }

        public byte[] ToPng(string label)
        {
            var raw = new byte[(W * 3 + 1) * H];
            var stride = W * 3;
            for (var y = 0; y < H; y++)
            {
                raw[y * (stride + 1)] = 0;
                Buffer.BlockCopy(_rgb, y * stride, raw, y * (stride + 1) + 1, stride);
            }
            using var compressed = new MemoryStream();
            using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
                zlib.Write(raw);
            var idat = compressed.ToArray();
            using var png = new MemoryStream();
            png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            var ihdr = new byte[13];
            Be(ihdr, 0, W);
            Be(ihdr, 4, H);
            ihdr[8] = 8;
            ihdr[9] = 2;
            Chunk(png, "IHDR"u8, ihdr);
            if (label.Length > 0 && label.Any(ch => ch > 127))
            {
                var text = new List<byte>();
                text.AddRange(Encoding.Latin1.GetBytes("Label"));
                text.Add(0);
                text.Add(0);
                text.Add(0);
                text.Add(0);
                text.Add(0);
                text.AddRange(Encoding.UTF8.GetBytes(label));
                Chunk(png, "iTXt"u8, text.ToArray());
            }
            else if (label.Length > 0)
            {
                var text = Encoding.ASCII.GetBytes("Label\0" + label);
                Chunk(png, "tEXt"u8, text);
            }
            Chunk(png, "IDAT"u8, idat);
            Chunk(png, "IEND"u8, []);
            return png.ToArray();
        }

        private static void Edge(int x0, int y0, int x1, int y1, int y, List<int> nodes)
        {
            if (y0 == y1 || y < Math.Min(y0, y1) || y >= Math.Max(y0, y1)) return;
            var x = x0 + (y - y0) * (x1 - x0) / (y1 - y0);
            nodes.Add(x);
        }
    }

    private static void Be(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static void Chunk(Stream stream, ReadOnlySpan<byte> type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        length[0] = (byte)(data.Length >> 24);
        length[1] = (byte)(data.Length >> 16);
        length[2] = (byte)(data.Length >> 8);
        length[3] = (byte)data.Length;
        stream.Write(length);
        stream.Write(type);
        stream.Write(data);
        var crcSource = new byte[type.Length + data.Length];
        type.CopyTo(crcSource);
        data.CopyTo(crcSource.AsSpan(type.Length));
        var crc = Crc(crcSource);
        Span<byte> crcBytes = stackalloc byte[4];
        crcBytes[0] = (byte)(crc >> 24);
        crcBytes[1] = (byte)(crc >> 16);
        crcBytes[2] = (byte)(crc >> 8);
        crcBytes[3] = (byte)crc;
        stream.Write(crcBytes);
    }

    private static uint Crc(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return crc ^ 0xFFFFFFFFu;
    }

    private static class Glyphs
    {
        private static readonly Dictionary<char, string> Map = new()
        {
            ['A'] = "01110/10001/10001/11111/10001/10001/10001",
            ['B'] = "11110/10001/10001/11110/10001/10001/11110",
            ['C'] = "01110/10001/10000/10000/10000/10001/01110",
            ['D'] = "11110/10001/10001/10001/10001/10001/11110",
            ['E'] = "11111/10000/10000/11110/10000/10000/11111",
            ['F'] = "11111/10000/10000/11110/10000/10000/10000",
            ['G'] = "01110/10001/10000/10111/10001/10001/01111",
            ['H'] = "10001/10001/10001/11111/10001/10001/10001",
            ['I'] = "01110/00100/00100/00100/00100/00100/01110",
            ['J'] = "00111/00010/00010/00010/00010/10010/01100",
            ['K'] = "10001/10010/10100/11000/10100/10010/10001",
            ['L'] = "10000/10000/10000/10000/10000/10000/11111",
            ['M'] = "10001/11011/10101/10101/10001/10001/10001",
            ['N'] = "10001/11001/10101/10011/10001/10001/10001",
            ['O'] = "01110/10001/10001/10001/10001/10001/01110",
            ['P'] = "11110/10001/10001/11110/10000/10000/10000",
            ['Q'] = "01110/10001/10001/10001/10101/10010/01101",
            ['R'] = "11110/10001/10001/11110/10100/10010/10001",
            ['S'] = "01111/10000/10000/01110/00001/00001/11110",
            ['T'] = "11111/00100/00100/00100/00100/00100/00100",
            ['U'] = "10001/10001/10001/10001/10001/10001/01110",
            ['V'] = "10001/10001/10001/10001/10001/01010/00100",
            ['W'] = "10001/10001/10001/10101/10101/10101/01010",
            ['X'] = "10001/10001/01010/00100/01010/10001/10001",
            ['Y'] = "10001/10001/01010/00100/00100/00100/00100",
            ['Z'] = "11111/00001/00010/00100/01000/10000/11111",
            ['0'] = "01110/10001/10011/10101/11001/10001/01110",
            ['1'] = "00100/01100/00100/00100/00100/00100/01110",
            ['2'] = "01110/10001/00001/00010/00100/01000/11111",
            ['3'] = "11110/00001/00001/01110/00001/00001/11110",
            ['4'] = "00010/00110/01010/10010/11111/00010/00010",
            ['5'] = "11111/10000/11110/00001/00001/10001/01110",
            ['6'] = "00110/01000/10000/11110/10001/10001/01110",
            ['7'] = "11111/00001/00010/00100/01000/01000/01000",
            ['8'] = "01110/10001/10001/01110/10001/10001/01110",
            ['9'] = "01110/10001/10001/01111/00001/00010/01100",
            ['a'] = "00000/00000/01110/00001/01111/10001/01111",
            ['d'] = "00001/00001/01101/10011/10001/10011/01101",
            ['e'] = "00000/00000/01110/10001/11111/10000/01110",
            ['g'] = "00000/01111/10001/01111/00001/10001/01110",
            ['i'] = "00100/00000/01100/00100/00100/00100/01110",
            ['l'] = "01100/00100/00100/00100/00100/00100/01110",
            ['m'] = "00000/00000/11010/10101/10101/10001/10001",
            ['n'] = "00000/00000/11110/10001/10001/10001/10001",
            ['o'] = "00000/00000/01110/10001/10001/10001/01110",
            ['p'] = "00000/11110/10001/11110/10000/10000/10000",
        };

        public static bool CanDraw(char ch)
        {
            if (ch == ' ') return true;
            foreach (var part in ch.ToString().Normalize(NormalizationForm.FormD))
            {
                if (part is 'đ' or 'Đ') continue;
                if (IsMark(part)) continue;
                if (CharUnicodeInfo.GetUnicodeCategory(part) == UnicodeCategory.NonSpacingMark) return false;
                if (BaseKey(part) is null) return false;
            }
            return true;
        }

        public static void Draw(Canvas canvas, int x, int y, string text, uint ink, int scale = 1)
        {
            var form = text.Normalize(NormalizationForm.FormD);
            for (var i = 0; i < form.Length;)
            {
                var ch = form[i];
                if (ch == ' ') { x += 4 * scale; i++; continue; }
                if (IsMark(ch) || CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                {
                    i++;
                    continue;
                }
                var end = i + 1;
                while (end < form.Length && (IsMark(form[end]) || CharUnicodeInfo.GetUnicodeCategory(form[end]) == UnicodeCategory.NonSpacingMark))
                    end++;
                var key = BaseKey(ch);
                if (key is null || !Map.TryGetValue(key.Value, out var rows)) { x += 6 * scale; i = end; continue; }
                Paint(canvas, x, y, rows, ink, scale);
                if (ch is 'đ' or 'Đ')
                {
                    for (var dx = -2; dx <= 0; dx++)
                        for (var sy = 0; sy < scale; sy++)
                            for (var sx = 0; sx < scale; sx++)
                                canvas.Set(x + dx * scale + sx, y + 3 * scale + sy, ink);
                }
                PaintMarks(canvas, x, y, form.AsSpan(i + 1, end - i - 1), ink, scale);
                x += 6 * scale;
                i = end;
            }
        }

        private static char? BaseKey(char ch)
        {
            if (ch is 'đ' or 'Đ') return 'D';
            if (Map.ContainsKey(ch)) return ch;
            var upper = char.ToUpperInvariant(ch);
            return Map.ContainsKey(upper) ? upper : null;
        }

        private static bool IsMark(char ch) => ch is '\u0300' or '\u0301' or '\u0302' or '\u0303' or '\u0306' or '\u0309' or '\u031B' or '\u0323';

        private static void Paint(Canvas canvas, int x, int y, string rows, uint ink, int scale)
        {
            var lines = rows.Split('/');
            for (var row = 0; row < lines.Length; row++)
                for (var col = 0; col < lines[row].Length; col++)
                    if (lines[row][col] == '1')
                        for (var sy = 0; sy < scale; sy++)
                            for (var sx = 0; sx < scale; sx++)
                                canvas.Set(x + col * scale + sx, y + row * scale + sy, ink);
        }

        private static void PaintMarks(Canvas canvas, int x, int y, ReadOnlySpan<char> marks, uint ink, int scale)
        {
            void Dot(int px, int py)
            {
                for (var sy = 0; sy < scale; sy++)
                    for (var sx = 0; sx < scale; sx++)
                        canvas.Set(x + px * scale + sx, y + py * scale + sy, ink);
            }

            var stacked = 0;
            foreach (var mark in marks)
            {
                if (mark == '\u0323')
                {
                    Dot(2, 8);
                    continue;
                }
                if (mark == '\u031B')
                {
                    Dot(5, 0);
                    Dot(6, 1);
                    Dot(5, 2);
                    continue;
                }
                if (!IsMark(mark)) continue;
                var dy = -4 - stacked * 3;
                stacked++;
                switch (mark)
                {
                    case '\u0301':
                        Dot(3, dy);
                        Dot(2, dy + 1);
                        Dot(1, dy + 2);
                        break;
                    case '\u0300':
                        Dot(1, dy);
                        Dot(2, dy + 1);
                        Dot(3, dy + 2);
                        break;
                    case '\u0309':
                        Dot(1, dy);
                        Dot(2, dy + 1);
                        Dot(3, dy);
                        Dot(2, dy + 2);
                        break;
                    case '\u0303':
                        Dot(0, dy + 1);
                        Dot(1, dy);
                        Dot(2, dy + 1);
                        Dot(3, dy);
                        Dot(4, dy + 1);
                        break;
                    case '\u0302':
                        Dot(2, dy);
                        Dot(1, dy + 1);
                        Dot(3, dy + 1);
                        Dot(0, dy + 2);
                        Dot(4, dy + 2);
                        break;
                    case '\u0306':
                        Dot(0, dy);
                        Dot(4, dy);
                        Dot(1, dy + 1);
                        Dot(2, dy + 2);
                        Dot(3, dy + 1);
                        break;
                }
            }
        }
    }
}
