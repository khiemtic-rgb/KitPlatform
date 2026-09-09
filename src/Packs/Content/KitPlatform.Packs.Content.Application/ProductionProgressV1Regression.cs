using System.Linq;

namespace KitPlatform.Packs.Content;

public static class ProductionProgressV1Regression
{
    public const string SuiteId = ProductionProgressRules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var minh = new ProductionProgressRules.CharacterFact("CHAR-001", "Minh", false, ["FULL_BODY"]);
        var linh = new ProductionProgressRules.CharacterFact("CHAR-003", "Linh", false, ["FRONT", "THREE_QUARTER", "SIDE", "FULL_BODY"]);
        var shots11 = Enumerable.Range(1, 11)
            .Select(n => new ProductionProgressRules.ShotFact($"S{n:00}", "SC01", n, ["CHAR-001", "CHAR-003"], false, false, false, false, false))
            .ToList();
        var scene = new ProductionProgressRules.SceneFact("SC01", "PHÒNG KHÁCH - TỐI", ["CHAR-001", "CHAR-003"], shots11);
        var live = new ProductionProgressRules.EpisodeFact(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "FAMIXA", "EP01", "Tập 01", true, [scene], [minh, linh], 11, 11);

        var resolved = ProductionProgressRules.Resolve(live);
        Ok(resolved.SceneCount == 1 && resolved.ShotCount == 11, "01 1 scene / 11 shots");
        Ok(resolved.StoryLine.Contains("1 cảnh", StringComparison.Ordinal)
            && resolved.StoryLine.Contains("11 shot", StringComparison.Ordinal)
            && !resolved.StoryLine.Contains("11 cảnh", StringComparison.Ordinal), "02 shots are not scenes");
        Ok(!minh.CanUse && minh.MissingTypes.Contains("FULL_BODY"), "03 CRP 3/4 character not ready");
        Ok(resolved.NextAction.Contains("Minh", StringComparison.Ordinal)
            && resolved.BlockingReason != null
            && resolved.BlockingReason.Contains("Toàn thân", StringComparison.Ordinal), "04 missing FULL_BODY blocks");
        Ok(resolved.ImageMade == 0 && resolved.ImageApproved == 0, "05 image 0/11 while CRP blocked");
        Ok(resolved.VideoMade == 0 && resolved.VideoApproved == 0, "06 video 0/11");
        Ok(resolved.Finalization == "Chưa sẵn sàng" && resolved.Publication == "Chưa sẵn sàng", "07 finish/publish not ready");

        var readyMinh = minh with { CanUse = true, MissingTypes = [] };
        var readyLinh = linh with { CanUse = true, MissingTypes = [] };
        var ready = ProductionProgressRules.Resolve(live with { Characters = [readyMinh, readyLinh] });
        Ok(ready.NextAction == "Tạo hình cho Shot 01", "08 CRP usable → image stage");

        var imageArtifact = shots11.Select((s, i) => s with { HasWorkflowImage = i == 0 }).ToList();
        var pendingReview = ProductionProgressRules.Resolve(live with
        {
            Characters = [readyMinh, readyLinh],
            Scenes = [scene with { Shots = imageArtifact }],
        });
        Ok(pendingReview.ImageMade == 1 && pendingReview.ImageApproved == 0, "09 artifact without director is not approval");
        Ok(pendingReview.NextAction.Contains("Tạo hình", StringComparison.Ordinal), "10 remaining shots still need image gen");
        var allImaged = shots11.Select(s => s with { HasWorkflowImage = true }).ToList();
        var reviewNext = ProductionProgressRules.Resolve(live with
        {
            Characters = [readyMinh, readyLinh],
            Scenes = [scene with { Shots = allImaged }],
        });
        Ok(reviewNext.NextAction.Contains("Duyệt hình", StringComparison.Ordinal), "10b all images made → review");

        var approved = shots11.Select((s, i) => s with { HasWorkflowImage = i == 0, ImageDirectorApproved = i == 0 }).ToList();
        var imageOk = ProductionProgressRules.Resolve(live with
        {
            Characters = [readyMinh, readyLinh],
            Scenes = [scene with { Shots = approved }],
        });
        Ok(imageOk.ImageMade == 1 && imageOk.ImageApproved == 1 && imageOk.VideoMade == 0, "11 image approved does not complete video");

        var legacyClip = shots11.Select(s => s with { HasWorkflowVideo = false, VideoDirectorApproved = false }).ToList();
        var clipOnly = ProductionProgressRules.Resolve(live with
        {
            LegacyKfCount = 11,
            LegacyVideoCount = 11,
            Scenes = [scene with { Shots = legacyClip }],
        });
        Ok(clipOnly.ImageMade == 0 && clipOnly.VideoMade == 0 && clipOnly.LegacyIgnored(live), "12 legacy counts ignored");
        Ok(clipOnly.ImageMade == 0 && clipOnly.VideoMade == 0, "13 kfCount/videoCount do not move progress");

        var videoReady = shots11.Select((s, i) => s with
        {
            HasWorkflowImage = true,
            ImageDirectorApproved = true,
            VideoContractApproved = true,
            HasWorkflowVideo = i == 0,
        }).ToList();
        var videoStage = ProductionProgressRules.Resolve(live with
        {
            Characters = [readyMinh, readyLinh],
            Scenes = [scene with { Shots = videoReady }],
        });
        Ok(videoStage.VideoMade == 1 && videoStage.VideoApproved == 0, "14 workflow video counts, not approved");
        Ok(videoStage.NextAction.Contains("Tạo video", StringComparison.Ordinal), "15 remaining shots still need video");
        var allVideo = shots11.Select(s => s with
        {
            HasWorkflowImage = true,
            ImageDirectorApproved = true,
            VideoContractApproved = true,
            HasWorkflowVideo = true,
        }).ToList();
        var videoReview = ProductionProgressRules.Resolve(live with
        {
            Characters = [readyMinh, readyLinh],
            Scenes = [scene with { Shots = allVideo }],
        });
        Ok(videoReview.NextAction.Contains("Duyệt video", StringComparison.Ordinal), "15b all videos made → review");

        var twoScenes = ProductionProgressRules.Resolve(live with
        {
            Scenes =
            [
                new ProductionProgressRules.SceneFact("SC01", "A", ["CHAR-001"], shots11.Take(6).ToList()),
                new ProductionProgressRules.SceneFact("SC02", "B", ["CHAR-003"], shots11.Skip(6).ToList()),
            ],
        });
        Ok(twoScenes.SceneCount == 2 && twoScenes.ShotCount == 11, "16 scene aggregate 2/11");
        Ok(twoScenes.Scenes.Sum(s => s.ShotCount) == 11, "17 episode aggregate from scenes");

        Ok(resolved.NextAction == "Hoàn thiện bộ ảnh chuẩn cho Minh", "18 one next action");
        Ok(resolved.BlockingReason != null && resolved.BlockingReason.Contains("Minh", StringComparison.Ordinal), "19 blocking reason deterministic");
        Ok(resolved.Stages.First(s => s.Id == "finish").Detail == "Chưa sẵn sàng", "20 no Hoàn thiện from still/video absence");
        Ok(resolved.Stages.First(s => s.Id == "publish").Detail == "Chưa sẵn sàng", "21 no Xuất bản");

        var again = ProductionProgressRules.Resolve(live);
        Ok(again.NextAction == resolved.NextAction
            && again.ImageMade == resolved.ImageMade
            && again.CompletedStages == resolved.CompletedStages, "22 reload same progress");
        Ok(resolved.CompletedStages == 2 && resolved.TotalStages == 9, "23 overall stages 2/9");
        Ok(resolved.Generate == false
            && !ProductionProgressRules.Generate
            && !ProductionProgressRules.GeminiCalled
            && !ProductionProgressRules.RunwayCalled
            && !ProductionProgressRules.VeoCalled, "24 generate=false no provider");

        var usableImage = ProductionProgressRules.Resolve(live with
        {
            Characters = [readyMinh, readyLinh],
            Scenes = [scene with { Shots = shots11 }],
            LegacyKfCount = 99,
            LegacyVideoCount = 99,
        });
        Ok(usableImage.NextAction == "Tạo hình cho Shot 01" && usableImage.ImageMade == 0, "25 legacy stills do not unlock image done");

        Ok(ProductionProgressRules.IsWorkflowImage("READY_FOR_DIRECTOR", "/a.jpg")
            && !ProductionProgressRules.IsImageDirectorApproved("PENDING")
            && !ProductionProgressRules.IsWorkflowVideo("SUCCEEDED", "/v.mp4"), "26 SUCCEEDED clip is not video complete");
        Ok(!ProductionProgressRules.IsWorkflowImage("READY_FOR_DIRECTOR", null)
            && ProductionProgressRules.StaffMissingLabel("FULL_BODY") == "Toàn thân", "27 no path = no image; staff label");

        return fail;
    }

    private static bool LegacyIgnored(this ProductionProgressRules.EpisodeProgress progress, ProductionProgressRules.EpisodeFact fact)
        => progress.ImageMade == 0 && progress.VideoMade == 0 && fact.LegacyKfCount == 11 && fact.LegacyVideoCount == 11;
}
