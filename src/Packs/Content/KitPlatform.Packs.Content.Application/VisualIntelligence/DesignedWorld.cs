using SkiaSharp;

namespace KitPlatform.Packs.Content.VisualIntelligence;

public static partial class StudioScenePainter
{
    private static DesignedResult ComposeDesigned(
        SKCanvas canvas,
        VisualBrandStyle brand,
        ShotVisualDemand demand,
        CharacterIdentity? identity,
        bool schematic,
        List<string> parts)
    {
        var box = DrawDesignedSet(canvas, brand, demand, parts);
        if (schematic)
        {
            parts.Add("no-face");
            if (brand.Motif != "candle-axis")
            {
                parts.Add("chart");
                DrawInfoBoard(canvas, brand);
            }

            return new DesignedResult(box, false, 0, 0, 0, 0, 0, 0, "", "");
        }

        if (identity is null)
        {
            parts.Add(demand.Setting == "handoff-counter" ? "two-simplified-figures" : "simplified-figure");
            var staff = new CharacterIdentity("staff", brand.Code, "Staff", "round", "bob", 0x2C241C, brand.Accent, "none", 30);
            DrawActor(canvas, demand, staff, brand, 0);
            if (demand.Setting == "handoff-counter")
            {
                var partner = new CharacterIdentity("partner", brand.Code, "Partner", "oval", "crop", 0x1B2430, brand.Secondary, "none", 28);
                DrawActor(canvas, demand, partner, brand, -280f);
            }

            return new DesignedResult(box, false, 0, 0, 0, 0, 0, 0, "", "");
        }

        parts.Add("identity-figure");
        parts.Add(demand.Pose);
        parts.Add(demand.Camera);
        var stamp = DrawActor(canvas, demand, identity, brand, 0);
        return new DesignedResult(
            box, true, stamp.HairX, stamp.HairY, stamp.OutfitX, stamp.OutfitY,
            identity.HairColor, identity.OutfitColor, identity.FaceShape, identity.Mark);
    }

    private static Pack? DrawDesignedSet(SKCanvas canvas, VisualBrandStyle brand, ShotVisualDemand demand, List<string> parts)
    {
        if (brand.Motif != "candle-axis")
            Room(canvas, brand, brand.Motif is "home-threshold" or "tea-terrace");
        return brand.Motif switch
        {
            "home-threshold" => HomeSet(canvas, brand, demand.Setting, parts),
            "counter-grid" => PharmacySet(canvas, brand, demand.Setting, parts),
            "candle-axis" => TradeSet(canvas, brand, demand, parts),
            "tea-terrace" => TeaSet(canvas, brand, demand.Setting, parts),
            _ => RegisteredSet(canvas, brand, demand.Setting, parts),
        };
    }

    private static void Room(SKCanvas canvas, VisualBrandStyle brand, bool warm)
    {
        const float horizon = 332f;
        using (var wall = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 4),
                new SKPoint(0, horizon),
                [C(Mix(brand.Background, 0xFFFFFF, warm ? 0.2f : 0.08f)), C(brand.Background), C(Mix(brand.Background, brand.Ink, 0.06f))],
                [0f, 0.72f, 1f],
                SKShaderTileMode.Clamp),
        })
            canvas.DrawRect(new SKRect(0, 4, Width, horizon), wall);

        var floor = Mix(brand.Secondary, brand.Background, warm ? 0.38f : 0.14f);
        using (var paint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, horizon),
                new SKPoint(0, Height),
                [C(Mix(floor, 0xFFFFFF, 0.12f)), C(floor), C(Mix(floor, brand.Ink, 0.16f))],
                [0f, 0.4f, 1f],
                SKShaderTileMode.Clamp),
        })
            canvas.DrawRect(new SKRect(0, horizon, Width, Height), paint);
        FillRound(canvas, new SKRect(0, horizon - 8, Width, horizon + 4), Mix(brand.Prop, brand.Ink, 0.22f), 0);

        using var light = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(warm ? 280 : 180, 120),
                460,
                [C(0xFFFFFF).WithAlpha(warm ? (byte)78 : (byte)42), C(0xFFFFFF).WithAlpha(0)],
                SKShaderTileMode.Clamp),
        };
        canvas.DrawRect(new SKRect(0, 4, Width, Height), light);
    }

    private static Pack? HomeSet(SKCanvas canvas, VisualBrandStyle brand, string setting, List<string> parts)
    {
        switch (setting)
        {
            case "kitchen":
                parts.Add("window"); parts.Add("table"); parts.Add("bowl");
                PaneWindow(canvas, 360, 48, 300, 190, brand);
                LightCone(canvas, 420, 230, 760, 690, 40);
                FillRound(canvas, new SKRect(48, 70, 330, 230), Mix(brand.Prop, 0xFFFFFF, 0.2f), 6);
                Stroke(canvas, new SKRect(48, 70, 330, 230), brand.Ink, 1.4f);
                StrokeLine(canvas, 189, 82, 189, 218, brand.Ink, 1.3f);
                FillRound(canvas, new SKRect(168, 140, 180, 152), brand.Ink, 2);
                FillRound(canvas, new SKRect(198, 140, 210, 152), brand.Ink, 2);
                FillRound(canvas, new SKRect(690, 70, 980, 230), Mix(brand.Prop, 0xFFFFFF, 0.2f), 6);
                Stroke(canvas, new SKRect(690, 70, 980, 230), brand.Ink, 1.4f);
                StrokeLine(canvas, 835, 82, 835, 218, brand.Ink, 1.3f);
                FillRound(canvas, new SKRect(812, 140, 824, 152), brand.Ink, 2);
                FillRound(canvas, new SKRect(846, 140, 858, 152), brand.Ink, 2);
                FillRound(canvas, new SKRect(40, 430, 860, 520), Mix(brand.Background, 0xFFFFFF, 0.35f), 0);
                Slab(canvas, 40, 520, 820, 150, 28, Mix(brand.Prop, 0xFFFFFF, 0.08f), brand.Ink);
                for (var door = 0; door < 3; door++)
                {
                    var dx = 70 + door * 250;
                    Stroke(canvas, new SKRect(dx, 560, dx + 200, 650), brand.Ink, 1.5f);
                    FillRound(canvas, new SKRect(dx + 88, 598, dx + 112, 608), brand.Ink, 2);
                }
                ClayBowl(canvas, 180, 500, brand);
                Mug(canvas, 300, 492, brand);
                break;
            case "doorway":
                parts.Add("door"); parts.Add("plant"); parts.Add("shoes");
                Doorway(canvas, 520, 48, 280, 520, brand);
                LeafPlant(canvas, 160, 470, brand);
                ShoeAtWorld(canvas, 980, 640, brand.Ink);
                ShoeAtWorld(canvas, 1040, 646, brand.Ink);
                break;
            case "living-room":
                parts.Add("sofa"); parts.Add("lamp"); parts.Add("rug");
                Rug(canvas, 180, 560, 640, 120, brand);
                Sofa(canvas, 200, 470, brand);
                FloorLamp(canvas, 980, 300, brand);
                break;
            case "park":
                parts.Add("horizon"); parts.Add("tree"); parts.Add("path");
                Park(canvas, brand);
                break;
            case "classroom":
                parts.Add("board"); parts.Add("bench");
                FillRound(canvas, new SKRect(80, 70, 760, 280), Mix(brand.Prop, 0xFFFFFF, 0.45f), 6);
                Stroke(canvas, new SKRect(80, 70, 760, 280), brand.Ink, 2f);
                Slab(canvas, 140, 620, 760, 36, 22, brand.Prop, brand.Ink);
                break;
            case "night-window":
                parts.Add("moon"); parts.Add("window"); parts.Add("dark-wall");
                using (var night = Fill(Mix(brand.Ink, 0x0E1A2C, 0.35f)))
                    canvas.DrawRect(new SKRect(0, 4, Width, 332), night);
                FillOval(canvas, new SKRect(980, 60, 1060, 140), 0xF4E7C4);
                PaneWindow(canvas, 160, 70, 260, 180, brand);
                break;
            case "yard":
                parts.Add("fence"); parts.Add("ground");
                for (var post = 0; post < 8; post++)
                    FillRound(canvas, new SKRect(40 + post * 150, 250, 58 + post * 150, 430), Mix(brand.Prop, brand.Ink, 0.2f), 2);
                StrokeLine(canvas, 40, 300, 1180, 300, brand.Ink, 3f);
                StrokeLine(canvas, 40, 360, 1180, 360, brand.Ink, 3f);
                break;
            case "bedroom":
                parts.Add("bed"); parts.Add("pillow");
                Slab(canvas, 120, 560, 620, 90, 30, Mix(brand.Prop, 0xFFFFFF, 0.12f), brand.Ink);
                FillRound(canvas, new SKRect(150, 430, 680, 540), Mix(brand.Accent, 0xFFFFFF, 0.35f), 18);
                FillRound(canvas, new SKRect(170, 400, 340, 480), 0xF7F1E8, 16);
                Stroke(canvas, new SKRect(170, 400, 340, 480), brand.Ink, 1.5f);
                break;
            case "balcony":
                parts.Add("rail"); parts.Add("sky");
                using (var sky = Fill(Mix(brand.Secondary, 0xFFFFFF, 0.45f)))
                    canvas.DrawRect(new SKRect(0, 4, Width, 300), sky);
                for (var bar = 0; bar < 14; bar++)
                    FillRound(canvas, new SKRect(40 + bar * 86, 300, 52 + bar * 86, 470), brand.Ink, 2);
                StrokeLine(canvas, 30, 320, 1240, 320, brand.Ink, 4f);
                break;
            case "market-walk":
                parts.Add("awning"); parts.Add("stall");
                using (var awning = new SKPath())
                {
                    awning.MoveTo(80, 160);
                    awning.LineTo(760, 160);
                    awning.LineTo(720, 250);
                    awning.LineTo(40, 250);
                    awning.Close();
                    using var paint = Fill(brand.Accent);
                    canvas.DrawPath(awning, paint);
                }
                Slab(canvas, 120, 600, 560, 80, 26, brand.Prop, brand.Ink);
                break;
            case "school-gate":
                parts.Add("gate"); parts.Add("posts");
                FillRound(canvas, new SKRect(180, 80, 210, 520), brand.Ink, 3);
                FillRound(canvas, new SKRect(640, 80, 670, 520), brand.Ink, 3);
                StrokeLine(canvas, 180, 120, 670, 120, brand.Accent, 8f);
                StrokeLine(canvas, 210, 200, 640, 200, brand.Ink, 3f);
                break;
            case "rain-window":
                parts.Add("rain"); parts.Add("window");
                PaneWindow(canvas, 360, 60, 340, 220, brand);
                for (var drop = 0; drop < 18; drop++)
                    StrokeLine(canvas, 40 + drop * 70, 20 + (drop % 3) * 30, 20 + drop * 70, 120 + (drop % 3) * 30, Mix(brand.Ink, 0xFFFFFF, 0.4f), 1.4f);
                break;
            case "work-counter":
                parts.Add("low-table"); parts.Add("plant"); parts.Add("window");
                PaneWindow(canvas, 70, 48, 280, 180, brand);
                LightCone(canvas, 120, 220, 520, 700, 46);
                Slab(canvas, 160, 560, 460, 70, 36, Mix(brand.Prop, 0xFFFFFF, 0.1f), brand.Ink);
                Notebook(canvas, 220, 500, brand);
                Mug(canvas, 430, 512, brand);
                LeafPlant(canvas, 80, 430, brand);
                break;
            default:
                parts.Add("room");
                PaneWindow(canvas, 80, 70, 220, 150, brand);
                Slab(canvas, 360, 600, 280, 50, 20, brand.Prop, brand.Ink);
                break;
        }

        return null;
    }

    private static Pack? PharmacySet(SKCanvas canvas, VisualBrandStyle brand, string setting, List<string> parts)
    {
        Plus(canvas, 1188, 78, brand.Accent);
        Pack? box = null;
        switch (setting)
        {
            case "pharmacy-counter":
            case "work-counter":
                parts.Add("counter"); parts.Add("shelf"); parts.Add("blank-pack");
                PharmacyCounter(canvas, brand);
                box = new Pack(150, 500, 140, 90);
                break;
            case "shelf-aisle":
                parts.Add("tall-shelf"); parts.Add("gap");
                Bay(canvas, 40, brand);
                Bay(canvas, 430, brand);
                Bay(canvas, 860, brand);
                FillRound(canvas, new SKRect(700, 150, 840, 300), brand.Background, 8);
                break;
            case "stock-room":
                parts.Add("crate"); parts.Add("stack");
                Slab(canvas, 80, 620, 280, 120, 36, brand.Prop, brand.Ink);
                Slab(canvas, 140, 480, 240, 100, 30, brand.Secondary, brand.Ink);
                Slab(canvas, 620, 640, 360, 160, 40, brand.Accent, brand.Ink);
                break;
            case "delivery-door":
                parts.Add("door"); parts.Add("crate");
                Doorway(canvas, 640, 60, 300, 500, brand);
                Slab(canvas, 80, 620, 240, 110, 30, brand.Prop, brand.Ink);
                break;
            case "consult-corner":
                parts.Add("chair"); parts.Add("low-table");
                Slab(canvas, 360, 580, 300, 48, 24, brand.Prop, brand.Ink);
                FillRound(canvas, new SKRect(120, 500, 250, 560), Mix(brand.Prop, 0xFFFFFF, 0.2f), 10);
                FillRound(canvas, new SKRect(980, 500, 1110, 560), Mix(brand.Secondary, 0xFFFFFF, 0.2f), 10);
                break;
            case "cash-desk":
                parts.Add("drawer"); parts.Add("counter");
                Slab(canvas, 60, 600, 700, 100, 28, brand.Prop, brand.Ink);
                Stroke(canvas, new SKRect(120, 630, 280, 690), brand.Ink, 1.6f);
                FillRound(canvas, new SKRect(180, 652, 220, 664), brand.Ink, 2);
                break;
            case "night-close":
                parts.Add("shutter");
                FillRound(canvas, new SKRect(80, 60, 1160, 520), Mix(brand.Ink, brand.Background, 0.15f), 4);
                for (var y = 80; y < 500; y += 26)
                    FillRound(canvas, new SKRect(100, y, 1140, y + 8), Mix(brand.Background, brand.Ink, 0.2f), 2);
                break;
            case "cleaning":
                parts.Add("bucket"); parts.Add("floor");
                Slab(canvas, 220, 600, 110, 90, 24, brand.Accent, brand.Ink);
                FillOval(canvas, new SKRect(248, 500, 300, 530), Mix(brand.Ink, 0xFFFFFF, 0.3f));
                break;
            case "waiting-bench":
                parts.Add("bench"); parts.Add("clock-face");
                Slab(canvas, 120, 620, 720, 36, 22, brand.Prop, brand.Ink);
                ClockFace(canvas, 1040, 130, brand);
                break;
            case "handoff-counter":
                parts.Add("counter"); parts.Add("two-places");
                Slab(canvas, 40, 600, 1200, 70, 24, brand.Prop, brand.Ink);
                break;
            case "expiry-shelf":
                parts.Add("shelf"); parts.Add("blank-pack");
                Bay(canvas, 60, brand);
                Vial(canvas, 520, 250, 110, brand.Prop, brand.Ink);
                box = new Pack(180, 250, 140, 100);
                break;
            case "opening-gate":
                parts.Add("gate"); parts.Add("counter");
                FillRound(canvas, new SKRect(80, 80, 108, 520), brand.Ink, 3);
                FillRound(canvas, new SKRect(360, 80, 388, 520), brand.Ink, 3);
                StrokeLine(canvas, 80, 100, 388, 100, brand.Accent, 8f);
                Slab(canvas, 460, 620, 640, 50, 20, brand.Prop, brand.Ink);
                break;
            default:
                parts.Add("counter");
                Slab(canvas, 80, 620, 1000, 64, 22, brand.Prop, brand.Ink);
                break;
        }

        return box;
    }

    private static void PharmacyCounter(SKCanvas canvas, VisualBrandStyle brand)
    {
        var back = Mix(brand.Prop, brand.Ink, 0.08f);
        FillRound(canvas, new SKRect(36, 58, 800, 470), back, 6);
        Stroke(canvas, new SKRect(36, 58, 800, 470), brand.Ink, 1.6f);
        float[] shelves = [176, 292, 408];
        foreach (var y in shelves)
            ShelfBoard(canvas, 48, y, 736, Mix(brand.Prop, 0xFFFFFF, 0.12f), brand.Ink);

        Stock(canvas, 64, 176, brand, 0);
        Stock(canvas, 70, 292, brand, 1);
        Stock(canvas, 58, 408, brand, 2);

        Slab(canvas, 36, 548, 820, 150, 34, Mix(brand.Prop, 0xFFFFFF, 0.16f), brand.Ink);
        for (var drawer = 0; drawer < 3; drawer++)
        {
            var dx = 80 + drawer * 240;
            Stroke(canvas, new SKRect(dx, 600, dx + 190, 680), brand.Ink, 1.5f);
            FillRound(canvas, new SKRect(dx + 78, 634, dx + 112, 646), brand.Ink, 2);
        }

        Vial(canvas, 520, 430, 112, brand.Accent, brand.Ink);
        GlassJar(canvas, 620, 500, brand);
        Carton(canvas, 700, 540, 54, 62, brand.Secondary, brand.Ink);
        BlisterCard(canvas, 430, 500, brand.Accent, brand.Ink);
        using (var shade = Fill(0x16324F))
        {
            shade.Color = shade.Color.WithAlpha(28);
            canvas.DrawOval(new SKRect(48, 690, 840, 718), shade);
        }
    }

    private static void Stock(SKCanvas canvas, float x, float shelfY, VisualBrandStyle brand, int salt)
    {
        var y = shelfY - 4;
        Vial(canvas, x, y - 78, 78, Mix(brand.Accent, 0xFFFFFF, salt == 1 ? 0.25f : 0f), brand.Ink);
        Vial(canvas, x + 70, y - 96, 96, brand.Secondary, brand.Ink);
        Carton(canvas, x + 160, y, 48, 58, salt == 2 ? brand.Accent : brand.Secondary, brand.Ink);
        Carton(canvas, x + 230, y, 42, 44, Mix(brand.Prop, brand.Ink, 0.1f), brand.Ink);
        BlisterCard(canvas, x + 300, y - 36, brand.Accent, brand.Ink);
        Vial(canvas, x + 430, y - 70, 70, Mix(brand.Accent, brand.Ink, 0.15f), brand.Ink);
        Carton(canvas, x + 520, y, 56, 50, Mix(brand.Secondary, 0xFFFFFF, 0.2f), brand.Ink);
        if (salt != 1)
            Vial(canvas, x + 600, y - 84, 84, brand.Accent, brand.Ink);
    }

    private static void Bay(SKCanvas canvas, float x, VisualBrandStyle brand)
    {
        FillRound(canvas, new SKRect(x, 70, x + 280, 520), Mix(brand.Prop, brand.Ink, 0.06f), 4);
        Stroke(canvas, new SKRect(x, 70, x + 280, 520), brand.Ink, 1.5f);
        ShelfBoard(canvas, x + 10, 200, 250, brand.Prop, brand.Ink);
        ShelfBoard(canvas, x + 10, 330, 250, brand.Prop, brand.Ink);
        ShelfBoard(canvas, x + 10, 460, 250, brand.Prop, brand.Ink);
        Vial(canvas, x + 24, 112, 84, brand.Accent, brand.Ink);
        Carton(canvas, x + 100, 196, 46, 52, brand.Secondary, brand.Ink);
        Vial(canvas, x + 30, 250, 76, brand.Secondary, brand.Ink);
        BlisterCard(canvas, x + 120, 290, brand.Accent, brand.Ink);
        Carton(canvas, x + 40, 456, 50, 48, brand.Accent, brand.Ink);
    }

    private static Pack? TradeSet(SKCanvas canvas, VisualBrandStyle brand, ShotVisualDemand demand, List<string> parts)
    {
        var setting = demand.Setting;
        parts.Add("axis");
        using (var wash = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 4), new SKPoint(0, Height),
                [C(Mix(brand.Background, 0xFFFFFF, 0.06f)), C(brand.Background), C(0x10141A)],
                [0f, 0.45f, 1f],
                SKShaderTileMode.Clamp),
        })
            canvas.DrawRect(new SKRect(0, 4, Width, Height), wash);

        var panel = setting == "dual-screen" ? new SKRect(48, 48, 560, 430) : new SKRect(70, 46, 900, 470);
        Monitor(canvas, panel, brand);
        var candles = setting switch { "risk-board" => 5, "candle-cluster" => 16, "range-bars" => 8, "session-open" => 6, _ => 12 };
        CandleChart(canvas, panel, candles, brand);
        parts.Add(setting == "dual-screen" ? "two-monitors" : "candles");
        if (setting == "dual-screen")
        {
            var second = new SKRect(600, 70, 1220, 450);
            Monitor(canvas, second, brand);
            CandleChart(canvas, second, 7, brand);
        }
        else if (setting == "review-table")
        {
            parts.Add("blank-pages");
            Carton(canvas, 980, 430, 120, 150, brand.Prop, brand.Ink);
        }
        else
        {
            parts.Add("monitor");
        }

        if (setting is "trading-desk" or "work-counter" or "review-table" or "session-open" or "session-close")
        {
            PlaceMaster(canvas, demand.Pose is "sit" or "write" ? "sit" : "stand", 1040f, 640f, "close", 0xE1B12C);
        }

        Slab(canvas, 40, 600, 900, 90, 26, Mix(brand.Prop, brand.Background, 0.4f), brand.Ink);
        Laptop(canvas, 620, 560, brand);
        return null;
    }

    private static Pack? TeaSet(SKCanvas canvas, VisualBrandStyle brand, string setting, List<string> parts)
    {
        parts.Add("terrace");
        TeaLandscape(canvas, brand);
        if (setting == "tea-pour")
        {
            parts.Add("pot"); parts.Add("stream");
            HillTeaPot(canvas, 460, 560, brand);
            StrokeLine(canvas, 520, 520, 560, 590, Mix(brand.Secondary, 0xFFFFFF, 0.2f), 3f);
            TeaCup(canvas, 560, 600, brand);
        }
        else if (setting == "tea-path")
        {
            parts.Add("path"); parts.Add("bush");
            using var path = new SKPath();
            path.MoveTo(560, 300);
            path.LineTo(700, 700);
            path.LineTo(860, 700);
            path.LineTo(640, 300);
            path.Close();
            using var paint = Fill(Mix(brand.Prop, brand.Ink, 0.25f));
            canvas.DrawPath(path, paint);
            LeafPlant(canvas, 240, 520, brand);
        }
        else
        {
            parts.Add("cup"); parts.Add("round-table");
            RoundTable(canvas, 430, 560, brand);
            HillTeaPot(canvas, 390, 520, brand);
            TeaCup(canvas, 500, 530, brand);
            TeaCup(canvas, 560, 546, brand);
            for (var leaf = 0; leaf < 4; leaf++)
                FillOval(canvas, new SKRect(330 + leaf * 16, 500, 348 + leaf * 16, 518), brand.Accent);
        }

        return null;
    }

    private static Pack? RegisteredSet(SKCanvas canvas, VisualBrandStyle brand, string setting, List<string> parts)
    {
        parts.Add("registered-motif");
        var hash = 17;
        foreach (var ch in brand.Motif + "/" + setting)
            hash = hash * 31 + ch;
        var count = 3 + Math.Abs(hash) % 4;
        for (var i = 0; i < count; i++)
        {
            var x = 80 + ((hash >> (i * 3)) & 255) % 800;
            var y = 520 + i * 24;
            if (((hash >> i) & 1) == 0)
                LeafPlant(canvas, x, y - 80, brand);
            else
                Slab(canvas, x, y, 140, 70, 18, i % 2 == 0 ? brand.Accent : brand.Prop, brand.Ink);
        }

        parts.Add("n" + count);
        return null;
    }

    private static void Park(SKCanvas canvas, VisualBrandStyle brand)
    {
        using (var sky = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 4), new SKPoint(0, 300),
                [C(Mix(brand.Secondary, 0xFFFFFF, 0.55f)), C(Mix(brand.Background, 0xFFFFFF, 0.15f))],
                SKShaderTileMode.Clamp),
        })
            canvas.DrawRect(new SKRect(0, 4, Width, 320), sky);
        Canopy(canvas, 180, 250, 0.7f, Mix(brand.Secondary, brand.Ink, 0.25f), brand);
        Canopy(canvas, 420, 220, 0.85f, Mix(brand.Secondary, 0xFFFFFF, 0.05f), brand);
        Canopy(canvas, 1080, 240, 0.75f, brand.Secondary, brand);
        using var path = new SKPath();
        path.MoveTo(520, 300);
        path.LineTo(760, 300);
        path.LineTo(980, Height);
        path.LineTo(300, Height);
        path.Close();
        using (var paint = Fill(Mix(brand.Prop, brand.Ink, 0.18f)))
            canvas.DrawPath(path, paint);
        Slab(canvas, 860, 600, 220, 36, 16, Mix(brand.Prop, 0xFFFFFF, 0.08f), brand.Ink);
        Canopy(canvas, 140, 430, 1.15f, brand.Secondary, brand);
    }

    private static void TeaLandscape(SKCanvas canvas, VisualBrandStyle brand)
    {
        using (var sky = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 4), new SKPoint(0, 280),
                [C(0xC9D7C4), C(brand.Background)],
                SKShaderTileMode.Clamp),
        })
            canvas.DrawRect(new SKRect(0, 4, Width, 280), sky);
        Ridge(canvas, 210, Mix(brand.Ink, 0xFFFFFF, 0.62f), 0.9f);
        Ridge(canvas, 168, Mix(brand.Secondary, 0xFFFFFF, 0.35f), 1f);
        Ridge(canvas, 130, Mix(brand.Accent, brand.Ink, 0.25f), 1.15f);
        using (var mist = Fill(0xFFFFFF))
        {
            mist.Color = mist.Color.WithAlpha(90);
            canvas.DrawOval(new SKRect(-40, 150, 1320, 280), mist);
        }

        for (var row = 0; row < 6; row++)
        {
            var y0 = 250f + row * 28f + row * row * 6f;
            var depth = 70f + row * 18f;
            var lip = Mix(brand.Accent, 0x1C3420, 0.04f * row);
            using var band = new SKPath();
            band.MoveTo(80 - row * 10, y0);
            band.LineTo(Width - 80 + row * 10, y0);
            band.LineTo(Width - 20 + row * 16, y0 + depth * 0.45f);
            band.LineTo(20 - row * 16, y0 + depth * 0.45f);
            band.Close();
            using (var paint = Fill(lip))
                canvas.DrawPath(band, paint);
            using var face = new SKPath();
            var front = y0 + depth * 0.45f;
            face.MoveTo(20 - row * 16, front);
            face.LineTo(Width - 20 + row * 16, front);
            face.LineTo(Width - 10 + row * 16, front + 14 + row);
            face.LineTo(10 - row * 16, front + 14 + row);
            face.Close();
            using (var soil = Fill(Mix(brand.Prop, brand.Ink, 0.22f + row * 0.03f)))
                canvas.DrawPath(face, soil);
            var bushes = 6 + row;
            for (var b = 0; b < bushes; b++)
            {
                var bx = 70 + b * ((Width - 140f) / bushes) + row * 6f;
                var s = 8f + row * 2.4f;
                FillOval(canvas, new SKRect(bx, y0 - s * 0.2f, bx + s * 2.1f, y0 + s), Mix(brand.Accent, 0xFFFFFF, b % 2 == 0 ? 0.08f : 0f));
            }
        }

        using (var earth = Fill(Mix(brand.Prop, brand.Background, 0.25f)))
            canvas.DrawRect(new SKRect(0, 600, Width, Height), earth);
        using (var near = Fill(Mix(brand.Accent, brand.Prop, 0.35f)))
            canvas.DrawRect(new SKRect(0, 575, Width, 612), near);
    }

    private static void Ridge(SKCanvas canvas, float crown, uint color, float scale)
    {
        using var path = new SKPath();
        path.MoveTo(0, 300);
        path.CubicTo(180 * scale, crown + 40, 420, crown, 680, crown + 24);
        path.CubicTo(980, crown - 10, 1140, crown + 50, Width, crown + 30);
        path.LineTo(Width, 340);
        path.LineTo(0, 340);
        path.Close();
        using var paint = Fill(color);
        canvas.DrawPath(path, paint);
    }

    private static void Canopy(SKCanvas canvas, float x, float y, float scale, uint leaf, VisualBrandStyle brand)
    {
        var trunk = Mix(brand.Prop, brand.Ink, 0.35f);
        using var stem = new SKPath();
        stem.MoveTo(x - 7 * scale, y + 10);
        stem.CubicTo(x - 10 * scale, y + 60 * scale, x - 16 * scale, y + 100 * scale, x - 18 * scale, y + 130 * scale);
        stem.LineTo(x + 16 * scale, y + 126 * scale);
        stem.CubicTo(x + 12 * scale, y + 80 * scale, x + 8 * scale, y + 40 * scale, x + 6 * scale, y + 8);
        stem.Close();
        using (var paint = Fill(trunk))
            canvas.DrawPath(stem, paint);
        using var crown = new SKPath();
        crown.MoveTo(x, y - 78 * scale);
        crown.CubicTo(x + 70 * scale, y - 90 * scale, x + 108 * scale, y - 20 * scale, x + 72 * scale, y + 16 * scale);
        crown.CubicTo(x + 96 * scale, y + 36 * scale, x + 20 * scale, y + 58 * scale, x - 8 * scale, y + 28 * scale);
        crown.CubicTo(x - 70 * scale, y + 62 * scale, x - 112 * scale, y + 8 * scale, x - 78 * scale, y - 24 * scale);
        crown.CubicTo(x - 96 * scale, y - 70 * scale, x - 30 * scale, y - 96 * scale, x, y - 78 * scale);
        crown.Close();
        using (var paint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(x, y - 90 * scale), new SKPoint(x, y + 50 * scale),
                [C(Mix(leaf, 0xFFFFFF, 0.22f)), C(leaf), C(Mix(leaf, 0x000000, 0.22f))],
                [0f, 0.45f, 1f], SKShaderTileMode.Clamp),
        })
            canvas.DrawPath(crown, paint);
        using var outline = Line(Mix(brand.Ink, leaf, 0.45f), 1.4f);
        canvas.DrawPath(crown, outline);
    }

    private static void CandleChart(SKCanvas canvas, SKRect panel, int count, VisualBrandStyle brand)
    {
        var plot = new SKRect(panel.Left + 28, panel.Top + 24, panel.Right - 18, panel.Bottom - 36);
        for (var g = 1; g <= 4; g++)
        {
            var y = plot.Top + plot.Height * g / 5f;
            StrokeLine(canvas, plot.Left, y, plot.Right, y, Mix(brand.Ink, brand.Background, 0.55f), 1f);
        }

        var step = plot.Width / count;
        var level = 0.55f;
        using var trend = new SKPath();
        var started = false;
        for (var i = 0; i < count; i++)
        {
            var wobble = (((i * 37) % 11) - 5) / 28f;
            level = Math.Clamp(level + wobble, 0.18f, 0.88f);
            var body = 0.08f + ((i * 13) % 7) / 40f;
            var up = ((i * 5) % 3) != 0;
            var mid = plot.Bottom - plot.Height * level;
            var top = mid - plot.Height * body * (up ? 1f : 0.35f);
            var bot = mid + plot.Height * body * (up ? 0.35f : 1f);
            var x = plot.Left + 8 + i * step;
            var color = up ? brand.Accent : brand.Secondary;
            StrokeLine(canvas, x + step * 0.28f, top - 10 - (i % 3) * 4, x + step * 0.28f, bot + 8 + (i % 2) * 5, color, 1.4f);
            FillRound(canvas, new SKRect(x, MathF.Min(top, bot), x + step * 0.55f, MathF.Max(top, bot)), color, 1.5f);
            var ty = (top + bot) / 2f;
            if (!started)
            {
                trend.MoveTo(x, ty);
                started = true;
            }
            else
            {
                trend.LineTo(x + step * 0.28f, ty);
            }

            var vol = 8f + ((i * 17) % 9) * 3f;
            FillRound(canvas, new SKRect(x, plot.Bottom + 6, x + step * 0.4f, plot.Bottom + 6 + vol), Mix(color, brand.Background, 0.25f), 1);
        }

        using var line = Line(Mix(brand.Accent, 0xFFFFFF, 0.35f), 2f);
        canvas.DrawPath(trend, line);
    }

    private static void DrawInfoBoard(SKCanvas canvas, VisualBrandStyle brand)
    {
        var card = new SKRect(690, 70, 1230, 460);
        FillRound(canvas, card, Mix(brand.Background, 0xFFFFFF, 0.12f), 18);
        Stroke(canvas, card, brand.Ink, 2f);
        var plot = new SKRect(card.Left + 48, card.Top + 48, card.Right - 28, card.Bottom - 70);
        StrokeLine(canvas, plot.Left, plot.Top, plot.Left, plot.Bottom, brand.Ink, 1.6f);
        StrokeLine(canvas, plot.Left, plot.Bottom, plot.Right, plot.Bottom, brand.Ink, 1.6f);
        float[] bars = [0.38f, 0.55f, 0.46f, 0.72f, 0.64f, 0.9f];
        var gap = plot.Width / bars.Length;
        for (var i = 0; i < bars.Length; i++)
        {
            var h = (plot.Height - 12) * bars[i];
            var x = plot.Left + 12 + i * gap;
            var tone = i % 2 == 0 ? brand.Accent : brand.Secondary;
            FillRound(canvas, new SKRect(x, plot.Bottom - h, x + gap * 0.58f, plot.Bottom), tone, 4);
        }

        using var trend = new SKPath();
        trend.MoveTo(plot.Left + 16, plot.Bottom - plot.Height * 0.32f);
        trend.CubicTo(plot.Left + plot.Width * 0.3f, plot.Bottom - plot.Height * 0.7f, plot.Left + plot.Width * 0.62f, plot.Bottom - plot.Height * 0.42f, plot.Right - 10, plot.Top + 16);
        using var trendPaint = Line(brand.Ink, 2.6f);
        canvas.DrawPath(trend, trendPaint);
        FillOval(canvas, new SKRect(card.Left + 36, card.Bottom - 42, card.Left + 56, card.Bottom - 22), brand.Accent);
        FillRound(canvas, new SKRect(card.Left + 68, card.Bottom - 36, card.Left + 150, card.Bottom - 26), Mix(brand.Ink, brand.Background, 0.2f), 2);
        FillOval(canvas, new SKRect(card.Left + 170, card.Bottom - 42, card.Left + 190, card.Bottom - 22), brand.Secondary);
        FillRound(canvas, new SKRect(card.Left + 202, card.Bottom - 36, card.Left + 290, card.Bottom - 26), Mix(brand.Ink, brand.Background, 0.2f), 2);
        var donut = new SKRect(card.Right - 120, card.Top + 28, card.Right - 36, card.Top + 112);
        using (var arc = Line(brand.Accent, 10f))
            canvas.DrawArc(donut, 200, 220, false, arc);
        using (var rest = Line(Mix(brand.Ink, brand.Background, 0.3f), 10f))
            canvas.DrawArc(donut, 60, 120, false, rest);
    }

    private static void Slab(SKCanvas canvas, float x, float y, float w, float h, float depth, uint color, uint ink)
    {
        using (var shade = Fill(0x000000))
        {
            shade.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 10);
            shade.Color = shade.Color.WithAlpha(48);
            canvas.DrawOval(new SKRect(x + 8, y + h - 8, x + w + 28, y + h + 22), shade);
        }

        using var side = new SKPath();
        side.MoveTo(x + w, y);
        side.LineTo(x + w - depth * 0.45f, y - depth);
        side.LineTo(x + w - depth * 0.45f, y - depth + h);
        side.LineTo(x + w, y + h);
        side.Close();
        using (var paint = Fill(Mix(color, 0x000000, 0.16f)))
            canvas.DrawPath(side, paint);
        FillRound(canvas, new SKRect(x, y, x + w, y + h), color, 2);
        using var top = new SKPath();
        top.MoveTo(x, y);
        top.LineTo(x + w, y);
        top.LineTo(x + w - depth * 0.45f, y - depth);
        top.LineTo(x + depth * 0.18f, y - depth);
        top.Close();
        using (var paint = Fill(Mix(color, 0xFFFFFF, 0.24f)))
            canvas.DrawPath(top, paint);
        using var stroke = Line(ink, 1.5f);
        canvas.DrawPath(side, stroke);
        canvas.DrawPath(top, stroke);
        canvas.DrawRect(x, y, w, h, stroke);
    }

    private static void ShelfBoard(SKCanvas canvas, float x, float y, float w, uint color, uint ink)
    {
        using var top = new SKPath();
        top.MoveTo(x, y);
        top.LineTo(x + w, y);
        top.LineTo(x + w - 22, y - 18);
        top.LineTo(x + 12, y - 18);
        top.Close();
        using (var paint = Fill(Mix(color, 0xFFFFFF, 0.3f)))
            canvas.DrawPath(top, paint);
        FillRound(canvas, new SKRect(x, y, x + w, y + 12), Mix(color, 0x000000, 0.08f), 1);
        using var stroke = Line(ink, 1.3f);
        canvas.DrawPath(top, stroke);
        canvas.DrawRect(x, y, w, 12, stroke);
    }

    private static void Vial(SKCanvas canvas, float x, float y, float h, uint liquid, uint ink)
    {
        var w = Math.Max(22f, h * 0.4f);
        var bodyTop = y + h * 0.26f;
        var body = new SKRect(x, bodyTop, x + w, y + h);
        using (var glass = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(body.Left, body.Top), new SKPoint(body.Right, body.Bottom),
                [C(0xF4FBFF), C(Mix(0xE7F3FA, liquid, 0.25f)), C(Mix(liquid, 0xFFFFFF, 0.35f))],
                [0f, 0.35f, 1f],
                SKShaderTileMode.Clamp),
        })
        {
            canvas.DrawRoundRect(body, 7, 7, glass);
        }
        FillRound(canvas, new SKRect(x + 3, y + h * 0.5f, x + w - 3, y + h - 4), Mix(liquid, 0xFFFFFF, 0.12f), 5);
        FillRound(canvas, new SKRect(x + w * 0.3f, y + h * 0.14f, x + w * 0.7f, bodyTop + 3), Mix(0xFFFFFF, liquid, 0.08f), 2);
        FillRound(canvas, new SKRect(x + w * 0.24f, y, x + w * 0.76f, y + h * 0.15f), Mix(ink, 0xFFFFFF, 0.12f), 2);
        FillRound(canvas, new SKRect(x + 4, bodyTop + 6, x + 7.5f, y + h * 0.82f), 0xFFFFFF, 2);
        Stroke(canvas, body, ink, 1.4f);
    }

    private static void Carton(SKCanvas canvas, float x, float bottom, float w, float h, uint color, uint ink)
    {
        var y = bottom - h;
        var d = Math.Min(18f, h * 0.28f);
        using var side = new SKPath();
        side.MoveTo(x + w, y);
        side.LineTo(x + w - d * 0.5f, y - d);
        side.LineTo(x + w - d * 0.5f, bottom - d);
        side.LineTo(x + w, bottom);
        side.Close();
        using (var paint = Fill(Mix(color, 0x000000, 0.18f)))
            canvas.DrawPath(side, paint);
        FillRound(canvas, new SKRect(x, y, x + w, bottom), color, 2);
        using var top = new SKPath();
        top.MoveTo(x, y);
        top.LineTo(x + w, y);
        top.LineTo(x + w - d * 0.5f, y - d);
        top.LineTo(x + d * 0.15f, y - d);
        top.Close();
        using (var paint = Fill(Mix(color, 0xFFFFFF, 0.22f)))
            canvas.DrawPath(top, paint);
        FillRound(canvas, new SKRect(x + w * 0.14f, y + h * 0.28f, x + w * 0.86f, y + h * 0.62f), Mix(0xFFFFFF, color, 0.12f), 2);
        using var stroke = Line(ink, 1.35f);
        canvas.DrawPath(side, stroke);
        canvas.DrawPath(top, stroke);
        canvas.DrawRect(x, y, w, h, stroke);
    }

    private static void BlisterCard(SKCanvas canvas, float x, float y, uint pill, uint ink)
    {
        var card = new SKRect(x, y, x + 78, y + 48);
        FillRound(canvas, card, 0xF3F6F8, 4);
        Stroke(canvas, card, ink, 1.3f);
        for (var row = 0; row < 2; row++)
        for (var col = 0; col < 4; col++)
            FillOval(canvas, new SKRect(x + 8 + col * 18, y + 8 + row * 20, x + 20 + col * 18, y + 20 + row * 20), Mix(pill, 0xFFFFFF, 0.25f));
    }

    private static void GlassJar(SKCanvas canvas, float x, float y, VisualBrandStyle brand)
    {
        FillOval(canvas, new SKRect(x, y - 18, x + 54, y + 8), Mix(brand.Prop, 0xFFFFFF, 0.35f));
        FillRound(canvas, new SKRect(x + 6, y - 8, x + 48, y + 36), Mix(brand.Secondary, 0xFFFFFF, 0.2f), 8);
        FillRound(canvas, new SKRect(x + 14, y - 16, x + 40, y - 4), brand.Ink, 3);
        Stroke(canvas, new SKRect(x + 6, y - 8, x + 48, y + 36), brand.Ink, 1.3f);
    }

    private static void PaneWindow(SKCanvas canvas, float x, float y, float w, float h, VisualBrandStyle brand)
    {
        var frame = Mix(brand.Prop, 0xFFFFFF, 0.15f);
        FillRound(canvas, new SKRect(x, y, x + w, y + h), frame, 4);
        var glass = Mix(brand.Secondary, 0xFFFFFF, 0.62f);
        FillRound(canvas, new SKRect(x + 10, y + 10, x + w / 2f - 4, y + h / 2f - 4), glass, 2);
        FillRound(canvas, new SKRect(x + w / 2f + 4, y + 10, x + w - 10, y + h / 2f - 4), Mix(glass, 0xFFFFFF, 0.15f), 2);
        FillRound(canvas, new SKRect(x + 10, y + h / 2f + 4, x + w / 2f - 4, y + h - 10), Mix(glass, brand.Accent, 0.08f), 2);
        FillRound(canvas, new SKRect(x + w / 2f + 4, y + h / 2f + 4, x + w - 10, y + h - 10), glass, 2);
        Stroke(canvas, new SKRect(x, y, x + w, y + h), brand.Ink, 2f);
    }

    private static void LightCone(SKCanvas canvas, float x0, float y0, float x1, float y1, byte alpha)
    {
        using var path = new SKPath();
        path.MoveTo(x0, y0);
        path.LineTo(x0 + 80, y0);
        path.LineTo(x1, y1);
        path.LineTo(x1 - 220, y1);
        path.Close();
        using var paint = Fill(0xFFFFFF);
        paint.Color = paint.Color.WithAlpha(alpha);
        canvas.DrawPath(path, paint);
    }

    private static void Doorway(SKCanvas canvas, float x, float y, float w, float h, VisualBrandStyle brand)
    {
        FillRound(canvas, new SKRect(x - 16, y - 12, x + w + 16, y + h), Mix(brand.Prop, brand.Ink, 0.2f), 4);
        FillRound(canvas, new SKRect(x, y, x + w, y + h - 8), Mix(brand.Accent, brand.Background, 0.25f), 2);
        Stroke(canvas, new SKRect(x + 18, y + 18, x + w - 18, y + h * 0.48f), brand.Ink, 1.5f);
        Stroke(canvas, new SKRect(x + 18, y + h * 0.52f, x + w - 18, y + h - 28), brand.Ink, 1.5f);
        FillOval(canvas, new SKRect(x + w - 36, y + h * 0.48f, x + w - 24, y + h * 0.48f + 12), brand.Secondary);
    }

    private static void LeafPlant(SKCanvas canvas, float x, float y, VisualBrandStyle brand)
    {
        var pot = Mix(brand.Prop, brand.Ink, 0.12f);
        using var potPath = new SKPath();
        potPath.MoveTo(x + 8, y);
        potPath.LineTo(x + 40, y);
        potPath.LineTo(x + 34, y + 36);
        potPath.LineTo(x + 14, y + 36);
        potPath.Close();
        using (var paint = Fill(pot))
            canvas.DrawPath(potPath, paint);
        using var stroke = Line(brand.Ink, 1.4f);
        canvas.DrawPath(potPath, stroke);
        FillOval(canvas, new SKRect(x - 8, y - 46, x + 28, y - 8), brand.Secondary);
        FillOval(canvas, new SKRect(x + 16, y - 58, x + 52, y - 16), Mix(brand.Secondary, 0xFFFFFF, 0.15f));
        FillOval(canvas, new SKRect(x + 4, y - 70, x + 36, y - 28), Mix(brand.Accent, brand.Secondary, 0.4f));
    }

    private static void Mug(SKCanvas canvas, float x, float y, VisualBrandStyle brand)
    {
        FillRound(canvas, new SKRect(x, y, x + 36, y + 32), 0xF7F4EF, 6);
        Stroke(canvas, new SKRect(x, y, x + 36, y + 32), brand.Ink, 1.5f);
        using var handle = new SKPath();
        handle.MoveTo(x + 34, y + 8);
        handle.CubicTo(x + 56, y + 6, x + 56, y + 28, x + 34, y + 24);
        using var stroke = Line(brand.Ink, 2.2f);
        canvas.DrawPath(handle, stroke);
        FillOval(canvas, new SKRect(x + 6, y + 4, x + 28, y + 12), Mix(brand.Accent, 0xFFFFFF, 0.2f));
    }

    private static void ClayBowl(SKCanvas canvas, float x, float y, VisualBrandStyle brand)
    {
        using var path = new SKPath();
        path.MoveTo(x, y);
        path.CubicTo(x + 10, y + 36, x + 70, y + 36, x + 80, y);
        path.CubicTo(x + 60, y + 8, x + 20, y + 8, x, y);
        path.Close();
        Form(canvas, path, Mix(brand.Accent, 0xFFFFFF, 0.25f), brand.Ink, 1.5f);
        StrokeLine(canvas, x + 8, y + 2, x + 72, y + 2, Mix(brand.Ink, 0xFFFFFF, 0.4f), 1.4f);
    }

    private static void Notebook(SKCanvas canvas, float x, float y, VisualBrandStyle brand)
    {
        FillRound(canvas, new SKRect(x, y, x + 92, y + 58), 0xFBF7F0, 3);
        Stroke(canvas, new SKRect(x, y, x + 92, y + 58), brand.Ink, 1.4f);
        StrokeLine(canvas, x + 46, y + 4, x + 46, y + 54, Mix(brand.Accent, brand.Ink, 0.2f), 2f);
        for (var line = 0; line < 3; line++)
            StrokeLine(canvas, x + 8, y + 16 + line * 12, x + 40, y + 16 + line * 12, Mix(brand.Ink, 0xFFFFFF, 0.45f), 1f);
    }

    private static void RoundTable(SKCanvas canvas, float x, float y, VisualBrandStyle brand)
    {
        using (var shade = Fill(0x000000))
        {
            shade.Color = shade.Color.WithAlpha(40);
            canvas.DrawOval(new SKRect(x - 20, y + 28, x + 210, y + 70), shade);
        }
        FillOval(canvas, new SKRect(x, y, x + 190, y + 54), Mix(brand.Prop, brand.Ink, 0.12f));
        FillOval(canvas, new SKRect(x + 10, y + 6, x + 180, y + 40), Mix(brand.Prop, 0xFFFFFF, 0.18f));
        using var stroke = Line(brand.Ink, 1.6f);
        canvas.DrawOval(new SKRect(x, y, x + 190, y + 54), stroke);
        FillRound(canvas, new SKRect(x + 78, y + 40, x + 112, y + 78), Mix(brand.Prop, brand.Ink, 0.2f), 3);
    }

    private static void HillTeaPot(SKCanvas canvas, float x, float y, VisualBrandStyle brand)
    {
        var body = Mix(brand.Prop, 0xFFFFFF, 0.2f);
        FillOval(canvas, new SKRect(x, y, x + 78, y + 58), body);
        FillOval(canvas, new SKRect(x + 16, y - 16, x + 62, y + 10), Mix(body, 0xFFFFFF, 0.15f));
        FillOval(canvas, new SKRect(x + 32, y - 26, x + 46, y - 12), brand.Accent);
        using var spout = new SKPath();
        spout.MoveTo(x + 68, y + 18);
        spout.CubicTo(x + 108, y + 8, x + 112, y + 28, x + 96, y + 36);
        spout.CubicTo(x + 84, y + 32, x + 72, y + 30, x + 66, y + 26);
        spout.Close();
        using (var paint = Fill(body))
            canvas.DrawPath(spout, paint);
        using var handle = new SKPath();
        handle.MoveTo(x + 8, y + 16);
        handle.CubicTo(x - 28, y + 10, x - 24, y + 48, x + 10, y + 44);
        using var stroke = Line(brand.Ink, 3f);
        canvas.DrawPath(handle, stroke);
        using var outline = Line(brand.Ink, 1.4f);
        canvas.DrawOval(new SKRect(x, y, x + 78, y + 58), outline);
        canvas.DrawPath(spout, outline);
    }

    private static void TeaCup(SKCanvas canvas, float x, float y, VisualBrandStyle brand)
    {
        FillOval(canvas, new SKRect(x, y, x + 34, y + 16), Mix(brand.Prop, 0xFFFFFF, 0.3f));
        FillRound(canvas, new SKRect(x + 4, y + 6, x + 30, y + 28), 0xF7F4EF, 4);
        Stroke(canvas, new SKRect(x + 4, y + 6, x + 30, y + 28), brand.Ink, 1.3f);
    }

    private static void Sofa(SKCanvas canvas, float x, float y, VisualBrandStyle brand)
    {
        var cloth = Mix(brand.Accent, 0xFFFFFF, 0.18f);
        FillRound(canvas, new SKRect(x, y + 20, x + 460, y + 150), cloth, 28);
        FillRound(canvas, new SKRect(x + 16, y, x + 150, y + 90), Mix(cloth, 0xFFFFFF, 0.12f), 20);
        FillRound(canvas, new SKRect(x + 160, y + 10, x + 300, y + 96), Mix(cloth, 0xFFFFFF, 0.05f), 20);
        FillRound(canvas, new SKRect(x - 16, y + 30, x + 30, y + 150), Mix(cloth, brand.Ink, 0.08f), 16);
        FillRound(canvas, new SKRect(x + 430, y + 30, x + 480, y + 150), Mix(cloth, brand.Ink, 0.08f), 16);
        Stroke(canvas, new SKRect(x, y + 20, x + 460, y + 150), brand.Ink, 1.7f);
    }

    private static void FloorLamp(SKCanvas canvas, float x, float y, VisualBrandStyle brand)
    {
        using var shade = new SKPath();
        shade.MoveTo(x, y);
        shade.LineTo(x + 70, y);
        shade.LineTo(x + 56, y + 36);
        shade.LineTo(x + 14, y + 36);
        shade.Close();
        using (var paint = Fill(Mix(brand.Secondary, 0xFFFFFF, 0.25f)))
            canvas.DrawPath(shade, paint);
        using var stroke = Line(brand.Ink, 1.5f);
        canvas.DrawPath(shade, stroke);
        StrokeLine(canvas, x + 35, y + 36, x + 35, y + 150, brand.Ink, 2f);
        FillOval(canvas, new SKRect(x + 18, y + 146, x + 52, y + 162), brand.Prop);
    }

    private static void Rug(SKCanvas canvas, float x, float y, float w, float h, VisualBrandStyle brand)
    {
        FillOval(canvas, new SKRect(x, y, x + w, y + h), Mix(brand.Accent, brand.Background, 0.35f));
        using var stroke = Line(brand.Ink, 1.5f);
        canvas.DrawOval(new SKRect(x + 16, y + 12, x + w - 16, y + h - 12), stroke);
    }

    private static void ShoeAtWorld(SKCanvas canvas, float x, float y, uint ink)
    {
        FillOval(canvas, new SKRect(x, y, x + 46, y + 16), ink);
        FillOval(canvas, new SKRect(x + 4, y - 6, x + 28, y + 8), Mix(ink, 0xFFFFFF, 0.15f));
    }

    private static void ClockFace(SKCanvas canvas, float x, float y, VisualBrandStyle brand)
    {
        FillOval(canvas, new SKRect(x, y, x + 90, y + 90), 0xF7F4EF);
        Stroke(canvas, new SKRect(x, y, x + 90, y + 90), brand.Ink, 2f);
        StrokeLine(canvas, x + 45, y + 45, x + 45, y + 18, brand.Ink, 2.2f);
        StrokeLine(canvas, x + 45, y + 45, x + 68, y + 50, brand.Accent, 2.2f);
    }
}
