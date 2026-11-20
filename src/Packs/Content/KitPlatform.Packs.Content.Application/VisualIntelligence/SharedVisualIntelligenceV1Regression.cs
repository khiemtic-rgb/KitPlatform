using System.Text;
using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.VisualIntelligence;

public static class SharedVisualIntelligenceV1Regression
{
    public const string SuiteId = "SHARED_VISUAL_INTELLIGENCE_V1";

    public static IReadOnlyList<string> Run(string? proofDirectory = null)
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var engine = new SharedVisualIntelligence();
        if (proofDirectory is not null) Directory.CreateDirectory(proofDirectory);
        var report = new Dictionary<string, object?>();

        CharacterConsistency(engine, proofDirectory, Ok, report);
        LibraryRange(engine, proofDirectory, Ok, report);
        DemandReuse(engine, proofDirectory, Ok, report);
        ChooseOrRefuse(engine, Ok, report);
        StyleLanes(engine, proofDirectory, Ok, report);
        Brands(engine, proofDirectory, Ok, report);
        Variety(engine, proofDirectory, Ok, report);
        Persistence(engine, proofDirectory, Ok, report);
        TextControl(engine, proofDirectory, Ok, report);
        CostAndRegression(engine, Ok, report);

        report["suite"] = SuiteId;
        report["fail"] = fail.Count;
        report["failures"] = fail;
        report["paidProviderCalls"] = 0;
        report["costUsd"] = 0;
        report["photoProvider"] = VisualDecisions.PhotoProvider;
        report["photoInvoked"] = false;
        report["illustratedRenderer"] = VisualDecisions.IllustratedRenderer;
        report["codeRenderer"] = VisualDecisions.CodeRenderer;
        if (proofDirectory is not null)
            File.WriteAllText(Path.Combine(proofDirectory, "acceptance.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return fail;
    }

    private static void CharacterConsistency(SharedVisualIntelligence engine, string? proof, Action<bool, string> ok, Dictionary<string, object?> report)
    {
        var session = engine.CreateEmptySession();
        var scenes = new (string Shot, string Pose, string Expression, string Setting, string Camera)[]
        {
            ("lan-stand", "stand", "calm", "kitchen", "medium"),
            ("lan-sit", "sit", "smile", "doorway", "close"),
            ("lan-reach", "reach", "concern", "park", "wide"),
            ("lan-walk", "walk", "focus", "living-room", "medium"),
        };
        var frames = new List<RenderedFrame>();
        foreach (var scene in scenes)
        {
            var demand = Person("lan-" + scene.Shot, "film-lan", "famixa", scene.Setting, scene.Pose, scene.Expression, scene.Camera, "CHAR-VIS-LAN");
            var resolved = engine.Resolve(session, demand, VisualResolveOptions.Studio);
            ok(resolved.Decision == VisualDecisions.GenerateLocal, "T1 " + scene.Shot + " generates a variant");
            ok(resolved.Frame is not null, "T1 " + scene.Shot + " has pixels");
            if (resolved.Frame is null) return;
            frames.Add(resolved.Frame);
            Write(proof, "t1-" + scene.Shot + ".png", resolved.Frame.Png);
        }

        var first = frames[0];
        ok(frames.All(f => f.SampledHair == BrandSeed.Lan.HairColor), "T1 hair color stays on Lan");
        ok(frames.All(f => f.SampledOutfit == BrandSeed.Lan.OutfitColor), "T1 outfit color stays on Lan");
        ok(frames.All(f => f.FaceShape == "round" && f.Mark == "freckle"), "T1 face and mark stay on Lan");
        ok(frames.Select(f => f.Sha256).Distinct().Count() == 4, "T1 four scenes are not the same pixels");
        ok(frames.Select(f => Convert.ToHexString(f.LumaGrid)).Distinct().Count() == 4, "T1 coarse layout changes with pose and place");

        var koi = engine.Resolve(session, Person("koi", "film-lan", "famixa", "bedroom", "stand", "calm", "medium", "CHAR-VIS-KOI"), VisualResolveOptions.Studio);
        ok(koi.Frame is not null && koi.Frame.SampledHair == BrandSeed.Koi.HairColor && koi.Frame.SampledHair != first.SampledHair, "T1 a second character does not inherit Lan");
        ok(koi.Frame is not null && koi.Frame.Mark == "glasses" && koi.Frame.FaceShape == "oval", "T1 Koi keeps glasses and face shape");
        if (koi.Frame is not null) Write(proof, "t1-koi-bedroom.png", koi.Frame.Png);

        var abroad = engine.Resolve(session, Person("lan-counter", "film-lan", "novixa", "pharmacy-counter", "stand", "calm", "medium", "CHAR-VIS-LAN"), VisualResolveOptions.Studio);
        ok(abroad.Frame is not null && abroad.Frame.SampledHair == BrandSeed.Lan.HairColor && abroad.Frame.SampledOutfit == BrandSeed.Lan.OutfitColor, "T1 Lan keeps hair and clothes in another brand's room");
        ok(abroad.Frame is not null && abroad.Frame.FaceShape == "round" && abroad.Frame.Mark == "freckle" && abroad.Frame.Motif == "counter-grid", "T1 Lan's face stays herself inside the Novixa world");
        if (abroad.Frame is not null) Write(proof, "t1-lan-novixa.png", abroad.Frame.Png);
        report["test1"] = new { character = "CHAR-VIS-LAN", scenes = scenes.Length, hair = first.SampledHair, outfit = first.SampledOutfit };
    }

    private static void LibraryRange(SharedVisualIntelligence engine, string? proof, Action<bool, string> ok, Dictionary<string, object?> report)
    {
        var session = engine.CreateEmptySession();
        string[] views = ["front", "three-quarter", "profile", "back"];
        var viewFrames = new List<RenderedFrame>();
        foreach (var view in views)
        {
            var tokens = view == "front" ? null : new[] { "view:" + view };
            var frame = RenderLibrary(engine, session, "lib-view-" + view, "famixa", "park", "stand", "calm", "medium", tokens);
            ok(frame is not null, "library view " + view + " renders");
            if (frame is null) return;
            viewFrames.Add(frame);
            Write(proof, "lib-view-" + view + ".png", frame.Png);
        }
        ok(viewFrames.Select(f => f.Sha256).Distinct().Count() == 1, "views without a designed turnaround reuse the front master");
        ok(viewFrames.All(f => f.SampledHair == BrandSeed.Lan.HairColor && f.Mark == "freckle"), "library turnaround keeps Lan");

        string[] expressions = ["calm", "smile", "happy", "concern", "focus", "wink", "surprise", "think"];
        var faces = new List<string>();
        foreach (var expression in expressions)
        {
            var frame = RenderLibrary(engine, session, "lib-face-" + expression, "famixa", "park", "stand", expression, "close", null);
            ok(frame is not null, "library expression " + expression + " renders");
            if (frame is null) return;
            faces.Add(frame.Sha256);
            Write(proof, "lib-face-" + expression + ".png", frame.Png);
        }
        ok(faces.Distinct().Count() == 1, "expressions without a designed face reuse the admitted master");

        string[] garments = ["shirt", "coat", "family", "sport", "outdoor", "apron"];
        var clothes = new List<string>();
        foreach (var garment in garments)
        {
            var tokens = garment == "shirt" ? null : new[] { "garment:" + garment };
            var frame = RenderLibrary(engine, session, "lib-wear-" + garment, "famixa", "park", "stand", "smile", "medium", tokens);
            ok(frame is not null, "library garment " + garment + " renders");
            if (frame is null) return;
            clothes.Add(frame.Sha256);
            Write(proof, "lib-wear-" + garment + ".png", frame.Png);
        }
        ok(clothes.Distinct().Count() == garments.Length, "library garments are different drawings");

        string[] actions = ["stand", "walk", "sit", "reach", "point", "phone", "write", "kneel"];
        var poses = new List<string>();
        foreach (var pose in actions)
        {
            var frame = RenderLibrary(engine, session, "lib-act-" + pose, "famixa", "park", pose, "calm", "medium", null);
            ok(frame is not null && frame.StructureCode.Contains(pose, StringComparison.Ordinal), "library action " + pose + " keeps its pose");
            if (frame is null) return;
            poses.Add(frame.Sha256);
            Write(proof, "lib-act-" + pose + ".png", frame.Png);
        }
        ok(poses.Distinct().Count() == actions.Length, "library actions are different drawings");

        var koi = RenderLibrary(engine, session, "lib-koi", "famixa", "park", "stand", "smile", "medium", null, "CHAR-VIS-KOI");
        ok(koi is not null && koi.Mark == "glasses" && koi.FaceShape == "oval" && koi.SampledHair == BrandSeed.Koi.HairColor, "library Koi stays a second identity");
        if (koi is not null) Write(proof, "lib-koi.png", koi.Png);

        var bottle = RenderLibrary(engine, session, "lib-bottle", "novixa", "consult-corner", "stand", "calm", "medium", null, "CHAR-VIS-LAN", "bottle");
        var chart = engine.Resolve(session, new ShotVisualDemand(
            "lib-chart", "film-lib", "trade", "chart-study", "compare", "trading-desk",
            "", "stand", "focus", "wide", "code"), VisualResolveOptions.Studio);
        ok(bottle is not null && chart.Frame is not null && bottle.Sha256 != chart.Frame.Sha256, "library object and chart are different pictures");
        if (bottle is not null) Write(proof, "lib-bottle.png", bottle.Png);
        if (chart.Frame is not null) Write(proof, "lib-chart.png", chart.Frame.Png);
        ok(chart.Frame is not null && chart.Frame.StructureCode.Contains("candles", StringComparison.Ordinal), "library chart keeps a candle structure");
        report["library"] = new { views = views.Length, expressions = expressions.Length, garments = garments.Length, actions = actions.Length };
    }

    private static RenderedFrame? RenderLibrary(
        SharedVisualIntelligence engine,
        VisualSession session,
        string shot,
        string brand,
        string setting,
        string pose,
        string expression,
        string camera,
        string[]? tokens,
        string character = "CHAR-VIS-LAN",
        string prop = "")
    {
        var demand = new ShotVisualDemand(
            shot, "film-lib", brand, shot, "show", setting, "", pose, expression, camera, "illustrated", character, prop, tokens);
        var resolved = engine.Resolve(session, demand, VisualResolveOptions.Studio);
        return resolved.Frame;
    }

    private static void DemandReuse(SharedVisualIntelligence engine, string? proof, Action<bool, string> ok, Dictionary<string, object?> report)
    {
        var session = engine.CreateSampleSession();
        var before = session.LocalRenders;
        var reuse = engine.Resolve(session, new ShotVisualDemand(
            "consult", "film-nx", "novixa", "pharmacy-consult", "explain", "pharmacy-counter",
            "Dược sĩ đang giải thích cho khách.", "stand", "calm", "medium", "illustrated"), VisualResolveOptions.Studio);
        ok(reuse.Decision == VisualDecisions.Reuse && reuse.AssetId == "nx-counter-consult", "T2 fitting library asset is reused");
        ok(session.LocalRenders == before && session.PaidProviderCalls == 0, "T2 reuse does not render or call a provider");

        var miss = engine.Resolve(session, Person("shoes", "film-fx", "famixa", "doorway", "kneel", "focus", "medium", "CHAR-VIS-LAN", "tie-shoes", "tie"), VisualResolveOptions.Studio);
        ok(miss.Decision == VisualDecisions.GenerateLocal && miss.AssetId != "nx-counter-consult", "T2 missing subject generates instead of borrowing a pharmacy still");
        ok(miss.Frame is not null && miss.Frame.StructureCode.Contains("doorway", StringComparison.Ordinal) && !miss.Frame.StructureCode.Contains("counter-grid", StringComparison.Ordinal), "T2 generated scene follows the requested place and brand");
        if (miss.Frame is not null) Write(proof, "t2-tie-shoes.png", miss.Frame.Png);
        if (session.Pixels.TryGetValue("nx-counter-consult", out var nx)) Write(proof, "t2-reused-counter.png", nx);

        var crossed = engine.Resolve(session, new ShotVisualDemand(
            "wrong-brand", "film-fx", "famixa", "pharmacy-consult", "explain", "pharmacy-counter",
            "", "stand", "calm", "medium", "illustrated"), VisualResolveOptions.Studio);
        ok(crossed.AssetId != "nx-counter-consult", "T2 Famixa does not receive the Novixa counter still");
        ok(crossed.Candidates.Any(c => c.AssetId == "nx-counter-consult" && c.ReasonCode == "BRAND"), "T2 the Novixa still is rejected for brand");
        report["test2"] = new { reused = reuse.AssetId, generated = miss.AssetId };
    }

    private static void ChooseOrRefuse(SharedVisualIntelligence engine, Action<bool, string> ok, Dictionary<string, object?> report)
    {
        var session = engine.CreateEmptySession();
        session.AddAsset(Plate("good", "novixa", "pharmacy-consult", "explain"));
        session.AddAsset(Plate("stale", "novixa", "pharmacy-consult", "explain"));
        session.AddAsset(Plate("close", "novixa", "pharmacy-consult", "ignore"));
        session.AddAsset(Plate("off", "famixa", "pharmacy-consult", "explain"));
        session.Usage.Add(new UsageMark("film-3", "past-1", "stale", "x"));
        session.Usage.Add(new UsageMark("film-3", "past-2", "stale", "x"));
        var demand = new ShotVisualDemand("pick", "film-3", "novixa", "pharmacy-consult", "explain", "pharmacy-counter", "", "stand", "calm", "medium", "illustrated");
        var chosen = engine.Resolve(session, demand, VisualResolveOptions.Studio);
        ok(chosen.AssetId == "good", "T3 best content match wins");
        ok(chosen.Candidates.Any(c => c.AssetId == "close" && c.ReasonCode == "BELOW_THRESHOLD" && !c.Eligible), "T3 a weak action match is not used");
        ok(chosen.Candidates.Any(c => c.AssetId == "off" && c.ReasonCode == "BRAND"), "T3 another brand is rejected");
        var goodScore = chosen.Candidates.First(c => c.AssetId == "good").Score;
        var staleScore = chosen.Candidates.First(c => c.AssetId == "stale").Score;
        ok(goodScore > staleScore, "T3 a less-used exact match ranks above a repeated one");

        var empty = engine.CreateEmptySession();
        var refused = engine.Resolve(empty, new ShotVisualDemand("none", "film-3", "novixa", "volcano-dance", "erupt", "crater"), new VisualResolveOptions(false, true, false, false, 72));
        ok(refused.Decision == VisualDecisions.NoFit && refused.AssetId is null && refused.Frame is null, "T3 no sufficient candidate is reported instead of filled");
        ok(refused.PaidProviderCalls == 0, "T3 refusal does not call a provider");
        report["test3"] = new { chosen = chosen.AssetId, refused = refused.Decision };
    }

    private static void StyleLanes(SharedVisualIntelligence engine, string? proof, Action<bool, string> ok, Dictionary<string, object?> report)
    {
        var session = engine.CreateEmptySession();
        var photo = engine.Resolve(session, new ShotVisualDemand(
            "photo", "film-style", "novixa", "customer-portrait", "wait", "waiting-bench",
            "Khách đang đợi lấy thuốc tại quầy.", "stand", "calm", "close", "photo"), VisualResolveOptions.Studio);
        ok(photo.Decision == VisualDecisions.GenerationRequired && photo.Frame is null, "T4 a photo demand is not filled with an illustration");
        ok(photo.Contract is not null && photo.Contract.ProviderId == VisualDecisions.PhotoProvider && !photo.Contract.ProviderCallAuthorized, "T4 photo stays a blocked Gemini contract");
        ok(photo.Contract is not null && !photo.Contract.ImagePrompt.Contains("Khách đang đợi", StringComparison.Ordinal), "T4 narration stays out of the photo prompt");
        ok(session.PaidProviderCalls == 0, "T4 photo contract performs no provider call");

        var illustrated = engine.Resolve(session, new ShotVisualDemand(
            "draw", "film-style", "novixa", "shelf-gap", "count", "shelf-aisle", "", "stand", "focus", "medium", "illustrated"), VisualResolveOptions.Studio);
        var code = engine.Resolve(session, new ShotVisualDemand(
            "chart", "film-style", "novixa", "stock-chart", "compare", "cash-desk", "", "stand", "calm", "wide", "code"), VisualResolveOptions.Studio);
        ok(illustrated.Frame is not null && illustrated.Frame.Motif == "counter-grid" && illustrated.Frame.StructureCode.Contains("simplified-figure", StringComparison.Ordinal), "T4 illustration stays in the Novixa counter world");
        ok(code.Frame is not null && code.Frame.RendererId == VisualDecisions.CodeRenderer && code.Frame.StructureCode.Contains("no-face", StringComparison.Ordinal), "T4 precise graphics use the code renderer");
        ok(illustrated.Frame is not null && code.Frame is not null && illustrated.Frame.Sha256 != code.Frame.Sha256, "T4 the two local methods are not the same picture");
        ok(illustrated.Frame is not null && code.Frame is not null && illustrated.Frame.GroundSample == BrandSeed.All[1].Background && code.Frame.GroundSample == BrandSeed.All[1].Background, "T4 both Novixa methods share the Novixa ground color");
        if (illustrated.Frame is not null) Write(proof, "t4-illustrated-shelf.png", illustrated.Frame.Png);
        if (code.Frame is not null) Write(proof, "t4-code-desk.png", code.Frame.Png);
        report["test4"] = new { photo = photo.Decision, illustrated = illustrated.Frame?.RendererId, code = code.Frame?.RendererId };
    }

    private static void Brands(SharedVisualIntelligence engine, string? proof, Action<bool, string> ok, Dictionary<string, object?> report)
    {
        var session = engine.CreateEmptySession();
        session.RegisterBrand(new VisualBrandStyle(
            "riverside-tea", "Riverside Tea", "illustrated-wash", "overcast-terrace", "ink-wash", 4, "tea-terrace",
            0xE7F0E4, 0x243028, 0x7A9E4A, 0xC4A574, 0xF3D2B5, 0xF4F1E6, "tea-hill",
            ["work-counter", "tea-pour", "tea-path"], ["pharmacy-shelf", "candle-axis"]));
        var frames = new List<RenderedFrame>();
        foreach (var brand in new[] { "famixa", "novixa", "trade", "riverside-tea" })
        {
            var resolved = engine.Resolve(session, new ShotVisualDemand(
                "plan-" + brand, "film-" + brand, brand, "plan-review", "explain", "work-counter",
                "", "stand", "calm", "medium", "auto"), VisualResolveOptions.Studio);
            ok(resolved.Decision == VisualDecisions.GenerateLocal && resolved.Frame is not null, "T5 " + brand + " produces its own picture");
            if (resolved.Frame is null) return;
            frames.Add(resolved.Frame);
            Write(proof, "t5-" + brand + ".png", resolved.Frame.Png);
        }

        ok(frames.Select(f => f.Motif).Distinct().Count() == 4, "T5 each brand keeps its own motif");
        ok(frames.Select(f => f.LineWeight).Distinct().Count() >= 3, "T5 line weight is part of the brand, not only the fill");
        ok(frames.Select(Primitives).Distinct().Count() == 4, "T5 the same brief does not become one template recolored");
        ok(frames[0].StructureCode.Contains("low-table", StringComparison.Ordinal) && frames[1].StructureCode.Contains("blank-pack", StringComparison.Ordinal) && frames[2].StructureCode.Contains("candles", StringComparison.Ordinal) && frames[3].StructureCode.Contains("cup", StringComparison.Ordinal), "T5 Famixa, Novixa, Trade, and a new brand draw different objects");

        var unknown = engine.Resolve(session, new ShotVisualDemand("missing", "film-x", "not-a-brand", "plan-review", "explain", "work-counter"), VisualResolveOptions.Studio);
        ok(unknown.Decision == VisualDecisions.NoFit && unknown.ReasonCodes.Contains("BRAND_UNKNOWN") && unknown.AssetId is null, "T5 an unknown brand is not silently mapped onto Famixa");
        report["test5"] = frames.Select(f => new { f.Motif, f.LineWeight, f.StructureCode }).ToArray();
    }

    private static void Variety(SharedVisualIntelligence engine, string? proof, Action<bool, string> ok, Dictionary<string, object?> report)
    {
        var famixa = engine.CreateEmptySession();
        var settings = BrandSeed.All[0].AllowedSettings.Where(s => s != "work-counter").Take(12).ToArray();
        var poses = new[] { "stand", "sit", "lean", "reach", "walk", "kneel" };
        var cameras = new[] { "wide", "medium", "close" };
        var expressions = new[] { "calm", "smile", "concern", "focus" };
        var dozen = new List<RenderedFrame>();
        for (var i = 0; i < settings.Length; i++)
        {
            var demand = Person("d" + i, "film-dozen", "famixa", settings[i], poses[i % poses.Length], expressions[i % expressions.Length], cameras[i % cameras.Length], "CHAR-VIS-LAN", settings[i], "live");
            var resolved = engine.Resolve(famixa, demand, VisualResolveOptions.Studio);
            ok(resolved.Decision == VisualDecisions.GenerateLocal && resolved.Frame is not null, "T6 scene " + i + " is created for its own demand");
            if (resolved.Frame is not null)
            {
                dozen.Add(resolved.Frame);
                Write(proof, "t6-scene-" + i.ToString("00") + ".png", resolved.Frame.Png);
            }
        }
        ok(dozen.Count == 12 && dozen.Select(f => f.Sha256).Distinct().Count() == 12, "T6 twelve scenes have no exact duplicate");
        ok(dozen.Select(f => f.StructureCode).Distinct().Count() >= 8, "T6 twelve scenes are not one layout recolored");
        var renders = famixa.LocalRenders;
        var again = engine.Resolve(famixa, Person("d0", "film-dozen", "famixa", settings[0], poses[0], expressions[0], cameras[0], "CHAR-VIS-LAN", settings[0], "live"), VisualResolveOptions.Studio);
        ok(again.Decision == VisualDecisions.Bound && famixa.LocalRenders == renders, "T6 opening the same shot does not create another image");

        var repeat = engine.Resolve(famixa, Person("d0-repeat", "film-dozen", "famixa", settings[0], poses[0], expressions[0], cameras[0], "CHAR-VIS-LAN", settings[0], "live"), VisualResolveOptions.Studio);
        ok(repeat.Decision == VisualDecisions.Reuse && repeat.AssetId == again.AssetId, "T6 an identical beat reuses the picture on purpose");

        var novixa = engine.CreateEmptySession();
        var places = BrandSeed.All[1].AllowedSettings.Where(s => s != "work-counter").ToArray();
        var hashes = new HashSet<string>();
        var coarse = new HashSet<string>();
        var structures = new HashSet<string>();
        var seen = new Dictionary<string, int>();
        var duplicates = new List<string>();
        for (var i = 0; i < 100; i++)
        {
            var setting = places[i % places.Length];
            var pose = poses[(i / places.Length) % poses.Length];
            var camera = cameras[(i / (places.Length * poses.Length)) % cameras.Length];
            var expression = expressions[(i / 3) % expressions.Length];
            var demand = new ShotVisualDemand(
                "s" + i.ToString("000"), "film-100", "novixa", "beat-" + i, "work", setting, "",
                pose, expression, camera, "illustrated");
            var resolved = engine.Resolve(novixa, demand, VisualResolveOptions.Studio);
            if (resolved.Frame is null)
            {
                ok(false, "T6 shot " + i + " missing");
                continue;
            }
            if (!seen.TryAdd(resolved.Frame.Sha256, i))
                duplicates.Add(seen[resolved.Frame.Sha256] + "~" + i);
            hashes.Add(resolved.Frame.Sha256);
            coarse.Add(Convert.ToHexString(resolved.Frame.LumaGrid));
            structures.Add(resolved.Frame.StructureCode);
            if (i is 0 or 33 or 66 or 99) Write(proof, "t6-ep-" + i.ToString("000") + ".png", resolved.Frame.Png);
        }
        ok(hashes.Count == 100, "T6 one hundred shots have no exact duplicate (" + hashes.Count + " " + string.Join(",", duplicates) + ")");
        ok(coarse.Count >= 90, "T6 one hundred shots stay apart on a coarse grid (" + coarse.Count + ")");
        var placeCount = structures.Select(SettingOf).Distinct().Count();
        ok(placeCount >= 10, "T6 the episode uses many places (" + placeCount + ")");
        ok(structures.Distinct().Count() >= 12, "T6 the episode is not a single composition");
        var busiest = novixa.Usage.GroupBy(u => u.AssetId).Max(g => g.Count());
        ok(busiest == 1, "T6 no still is repeated inside the hundred");
        ok(novixa.PaidProviderCalls == 0, "T6 the episode makes no paid call");
        report["test6"] = new { dozen = dozen.Count, episode = 100, uniqueHashes = hashes.Count, uniqueCoarse = coarse.Count, uniqueStructures = structures.Distinct().Count(), busiest };
    }

    private static void Persistence(SharedVisualIntelligence engine, string? proof, Action<bool, string> ok, Dictionary<string, object?> report)
    {
        var session = engine.CreateEmptySession();
        var demand = Person("keep", "film-keep", "famixa", "kitchen", "stand", "calm", "medium", "CHAR-VIS-LAN", "breakfast-talk", "talk");
        var created = engine.Resolve(session, demand, VisualResolveOptions.Studio);
        ok(created.AssetId is not null && created.Frame is not null, "T7 a chosen picture is stored");
        if (created.AssetId is null || created.Frame is null) return;
        var sha = created.Frame.Sha256;
        var renders = session.LocalRenders;
        session.Approve("film-keep", "keep");
        var reopened = engine.Resolve(session, demand with { BrandCode = "novixa", Narration = "Đổi brand không được đổi ảnh đã duyệt." }, VisualResolveOptions.Studio);
        ok(reopened.Decision == VisualDecisions.Bound && reopened.AssetId == created.AssetId, "T7 reload and a brand change keep the approved shot");
        ok(session.LocalRenders == renders && session.PaidProviderCalls == 0, "T7 reload creates nothing");
        ok(session.Pixels[created.AssetId].AsSpan().SequenceEqual(created.Frame.Png), "T7 approved pixels stay byte-for-byte");

        var variant = engine.Resolve(session, demand, VisualResolveOptions.Studio.AsRegenerate());
        ok(variant.Decision == VisualDecisions.VariantStored && variant.VariantAssetId is not null && variant.VariantAssetId != created.AssetId, "T7 regenerate stores a variant");
        ok(session.FindBinding("film-keep", "keep")?.AssetId == created.AssetId, "T7 the approved binding stays on the original");
        ok(session.FindAsset(created.AssetId)?.Approval == "approved" && VisualHashes.Sha256(session.Pixels[created.AssetId]) == sha, "T7 the approved asset is still in the library");

        var folder = Path.Combine(Path.GetTempPath(), "kit-visual-" + Guid.NewGuid().ToString("N"));
        engine.Save(folder, session);
        session.Pixels[created.AssetId][0] ^= 0x5A;
        engine.Save(folder, session);
        var loaded = engine.Load(folder);
        ok(VisualHashes.Sha256(loaded.Pixels[created.AssetId]) == sha, "T7 a later save does not replace the approved file");
        var fromDisk = engine.Resolve(loaded, demand with { BrandCode = "trade" }, VisualResolveOptions.Studio);
        ok(fromDisk.Decision == VisualDecisions.Bound && fromDisk.AssetId == created.AssetId && loaded.LocalRenders == 0, "T7 opening the saved film does not generate");
        if (variant.Frame is not null) Write(proof, "t7-variant.png", variant.Frame.Png);
        Write(proof, "t7-approved.png", created.Frame.Png);
        report["test7"] = new { approved = created.AssetId, variant = variant.VariantAssetId, sha };
    }

    private static void TextControl(SharedVisualIntelligence engine, string? proof, Action<bool, string> ok, Dictionary<string, object?> report)
    {
        var session = engine.CreateEmptySession();
        const string narration = "Thuốc này giúp hạ huyết áp sau 30 ngày, uống buổi sáng.";
        var demand = new ShotVisualDemand(
            "label", "film-text", "novixa", "blank-pack", "place", "pharmacy-counter", narration,
            "stand", "calm", "medium", "illustrated", null, "blank-box", null,
            [narration, "Amlodipine 5mg"]);
        var resolved = engine.Resolve(session, demand, VisualResolveOptions.Studio);
        ok(resolved.Frame is not null && resolved.Contract is not null, "T8 a box scene renders");
        if (resolved.Frame is null || resolved.Contract is null) return;
        ok(resolved.Frame.OverlayText == "Amlodipine 5mg", "T8 only the controlled label is drawn");
        ok(resolved.Contract.RejectedLabels.Contains(narration), "T8 narration is refused as a package label");
        ok(!resolved.Contract.ImagePrompt.Contains(narration, StringComparison.Ordinal) && !resolved.Contract.ImagePrompt.Contains("huyết áp", StringComparison.Ordinal), "T8 the image prompt does not repeat the narration");
        ok(!resolved.Frame.BoxHasInk && resolved.Frame.BoxBottom >= 0 && resolved.Frame.CaptionTop > resolved.Frame.BoxBottom, "T8 the box interior has no caption ink");
        ok(!Contains(resolved.Frame.Png, narration) && ContainsAscii(resolved.Frame.Png, "Amlodipine 5mg"), "T8 the file carries the controlled label and not the narration");
        const string vietnamese = "Thuốc hạ huyết áp";
        var undrawn = IllustratedSceneRenderer.UndrawableLetters(vietnamese);
        var marked = IllustratedSceneRenderer.MeasureCaption(vietnamese);
        var stripped = IllustratedSceneRenderer.MeasureCaption("Thuoc ha huyet ap");
        var dStroke = IllustratedSceneRenderer.MeasureCaption("Đ");
        var plainD = IllustratedSceneRenderer.MeasureCaption("D");
        ok(undrawn.Length == 0, "T8b Vietnamese diacritics have glyphs");
        ok(marked.Above > stripped.Above && marked.Below > stripped.Below, "T8b tone marks and the dot below add caption ink");
        ok(dStroke.Total > plainD.Total, "T8b đ keeps its stroke");
        ok(IllustratedSceneRenderer.MeasureCaption("ơ").Total > IllustratedSceneRenderer.MeasureCaption("o").Total, "T8b ơ keeps its horn");
        ok(IllustratedSceneRenderer.MeasureCaption("ư").Total > IllustratedSceneRenderer.MeasureCaption("u").Total, "T8b ư keeps its horn");
        ok(IllustratedSceneRenderer.UndrawableLetters("Amlodipine 5mg").Length == 0, "T8b the bitmap font still draws the ASCII label");
        var vi = engine.Resolve(engine.CreateEmptySession(), new ShotVisualDemand(
            "label-vi", "film-text-vi", "novixa", "blank-pack", "place", "pharmacy-counter", narration,
            "stand", "calm", "medium", "illustrated", null, "blank-box", null,
            ["Thuốc 5mg"]), VisualResolveOptions.Studio);
        ok(vi.Frame is not null && vi.Contract is not null && vi.Frame.OverlayText == "Thuốc 5mg" && !vi.Frame.BoxHasInk, "T8b the Vietnamese label stays in the caption");
        if (vi.Frame is not null)
        {
            ok(Contains(vi.Frame.Png, "Thuốc 5mg") && !Contains(vi.Frame.Png, narration), "T8b the file carries the Vietnamese label and not the narration");
            Write(proof, "t8-vi-caption.png", vi.Frame.Png);
        }
        Write(proof, "t8-blank-pack.png", resolved.Frame.Png);
        report["test8"] = new { overlay = resolved.Frame.OverlayText, rejected = resolved.Contract.RejectedLabels.Length };
        report["vietnameseDiacritics"] = new
        {
            supported = undrawn.Length == 0,
            sample = vietnamese,
            undrawable = undrawn,
            above = marked.Above,
            below = marked.Below,
            scope = "Controlled captions in kit-illustrated-v1 and kit-code-graphic-v1. Tone marks, horn, and đ are drawn. Narration still cannot become a package label.",
        };
    }

    private static void CostAndRegression(SharedVisualIntelligence engine, Action<bool, string> ok, Dictionary<string, object?> report)
    {
        var session = engine.CreateSampleSession();
        var sha = session.FindAsset("nx-counter-consult")!.Sha256;
        var bytes = session.Pixels["nx-counter-consult"].ToArray();
        var photo = engine.Resolve(session, new ShotVisualDemand("paid", "film-cost", "novixa", "portrait", "look", "waiting-bench", "", "stand", "calm", "close", "photo"), VisualResolveOptions.Studio with { AllowPaidProvider = true });
        ok(photo.Decision == VisualDecisions.GenerationRequired && session.PaidProviderCalls == 0 && photo.Frame is null, "T9 authorization still does not place a paid call");
        ok(session.Pixels["nx-counter-consult"].AsSpan().SequenceEqual(bytes) && session.FindAsset("nx-counter-consult")!.Sha256 == sha, "T9 an existing approved still is not rewritten");

        var bridge = engine.Resolve(engine.CreateEmptySession(), Person("bridge", "film-bridge", "famixa", "kitchen", "stand", "calm", "medium", "CHAR-VIS-LAN"), VisualResolveOptions.Studio);
        var dto = VisualStillBridge.ToDto(bridge);
        ok(dto.ImageDataUrl.StartsWith("data:image/png;base64,", StringComparison.Ordinal) && !dto.Model.Contains("gemini", StringComparison.OrdinalIgnoreCase), "T9 the still bridge returns local pixels");
        var blocked = false;
        try { VisualStillBridge.ToDto(photo); }
        catch (InvalidOperationException) { blocked = true; }
        ok(blocked, "T9 a photo requirement does not become a fake still");

        var prior = ImageGenerationExecutionV1Regression.Run();
        ok(prior.Count == 0, "T9 image execution regression still passes");
        report["test9"] = new { paidProviderCalls = session.PaidProviderCalls, costUsd = 0, priorFailures = prior.Count };
    }

    private static ShotVisualDemand Person(
        string shot, string film, string brand, string setting, string pose, string expression, string camera, string character,
        string? subject = null, string action = "live") => new(
        shot, film, brand, subject ?? setting, action, setting, "", pose, expression, camera, "illustrated", character);

    private static VisualAsset Plate(string id, string brand, string subject, string action) => new(
        id, "still", brand, brand, subject, action, "pharmacy-counter", "stand", "calm", "medium", "illustrated",
        null, "", [subject], "approved", "");

    private static string Primitives(RenderedFrame frame)
    {
        var cut = frame.StructureCode.IndexOf(':');
        return cut < 0 ? frame.StructureCode : frame.StructureCode[(cut + 1)..];
    }

    private static string SettingOf(string structure)
    {
        var slash = structure.IndexOf('/');
        var colon = structure.IndexOf(':');
        if (slash < 0 || colon < slash) return structure;
        return structure[(slash + 1)..colon];
    }

    private static void Write(string? directory, string name, byte[] png)
    {
        if (directory is null) return;
        File.WriteAllBytes(Path.Combine(directory, name), png);
    }

    private static bool ContainsAscii(byte[] data, string text) => data.AsSpan().IndexOf(Encoding.ASCII.GetBytes(text)) >= 0;

    private static bool Contains(byte[] data, string text) => data.AsSpan().IndexOf(Encoding.UTF8.GetBytes(text)) >= 0;
}
