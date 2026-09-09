using System.Linq;

namespace KitPlatform.Packs.Content;

public static class ProductionProgressRules
{
    public const string DocumentId = "FAMIXA_PRODUCTION_PROGRESS_SOURCE_OF_TRUTH_V1";
    public const string SuiteId = "FAMIXA_PRODUCTION_PROGRESS_SOURCE_OF_TRUTH_V1_REGRESSION";

    public const int TotalStages = 9;

    public static bool Generate => false;
    public static bool GeminiCalled => false;
    public static bool RunwayCalled => false;
    public static bool VeoCalled => false;

    public sealed record CharacterFact(string CharacterId, string Name, bool CanUse, IReadOnlyList<string> MissingTypes);

    public sealed record ShotFact(
        string ShotId,
        string SceneId,
        int ShotNumber,
        IReadOnlyList<string> CharacterIds,
        bool HasWorkflowImage,
        bool ImageDirectorApproved,
        bool VideoContractApproved,
        bool HasWorkflowVideo,
        bool VideoDirectorApproved,
        bool Finalized = false,
        bool Published = false);

    public sealed record SceneFact(
        string SceneId,
        string SceneName,
        IReadOnlyList<string> CharacterIds,
        IReadOnlyList<ShotFact> Shots);

    public sealed record EpisodeFact(
        Guid BuildId,
        string SeriesCode,
        string EpisodeCode,
        string Title,
        bool ScriptLocked,
        IReadOnlyList<SceneFact> Scenes,
        IReadOnlyList<CharacterFact> Characters,
        int LegacyKfCount = 0,
        int LegacyVideoCount = 0,
        bool GraphPublished = false);

    public sealed record ShotProgress(
        string ShotId,
        string SceneId,
        int ShotNumber,
        IReadOnlyList<string> CharacterIds,
        IReadOnlyList<string> CharacterNames,
        string CurrentStep,
        string NextAction,
        string StaffStatus,
        string? BlockingReason,
        bool ImageMade,
        bool ImageApproved,
        bool VideoMade,
        bool VideoApproved,
        bool Finalized,
        bool Published);

    public sealed record StageNode(string Id, string Label, bool Done, string Detail);

    public sealed record SceneProgress(
        string SceneId,
        string SceneName,
        int ShotCount,
        int CharacterCount,
        IReadOnlyList<string> CharacterNames,
        int ImageMade,
        int ImageApproved,
        int VideoMade,
        int VideoApproved,
        string NextAction,
        string? BlockingReason,
        IReadOnlyList<ShotProgress> Shots);

    public sealed record EpisodeProgress(
        Guid BuildId,
        string SeriesCode,
        string EpisodeCode,
        string Title,
        int SceneCount,
        int ShotCount,
        int CharacterCount,
        int ImageMade,
        int ImageApproved,
        int VideoMade,
        int VideoApproved,
        string Finalization,
        string Publication,
        string CurrentStep,
        string NextAction,
        string? BlockingReason,
        string StoryLine,
        int CompletedStages,
        int TotalStages,
        string Tone,
        IReadOnlyList<StageNode> Stages,
        IReadOnlyList<SceneProgress> Scenes,
        bool Generate = false);

    public static bool IsWorkflowImage(string? status, string? artifactPath)
    {
        if (string.IsNullOrWhiteSpace(artifactPath)) return false;
        var s = (status ?? "").Trim().ToUpperInvariant();
        return s is "READY_FOR_DIRECTOR" or "APPROVED" or "IMAGE_APPROVED";
    }

    public static bool IsImageDirectorApproved(string? directorApproval)
        => (directorApproval ?? "").Trim().Equals("APPROVED", StringComparison.OrdinalIgnoreCase);

    public static bool IsVideoContractApproved(string? status)
    {
        var s = (status ?? "").Trim().ToUpperInvariant();
        return s is "APPROVED" or "DIRECTOR_APPROVED";
    }

    public static bool IsWorkflowVideo(string? status, string? artifactPath)
    {
        if (string.IsNullOrWhiteSpace(artifactPath)) return false;
        var s = (status ?? "").Trim().ToUpperInvariant();
        return s is "READY_FOR_DIRECTOR" or "DIRECTOR_APPROVED";
    }

    public static bool IsVideoDirectorApproved(string? status, DateTimeOffset? approvedAt)
        => approvedAt.HasValue
           || (status ?? "").Trim().Equals("DIRECTOR_APPROVED", StringComparison.OrdinalIgnoreCase);

    public static (int SceneCount, int ShotCount) CountScenesAndShots(IEnumerable<SceneFact> scenes)
    {
        var list = scenes.ToList();
        return (list.Count, list.Sum(s => s.Shots.Count));
    }

    public static EpisodeProgress Resolve(EpisodeFact fact)
    {
        _ = fact.LegacyKfCount;
        _ = fact.LegacyVideoCount;

        var counts = CountScenesAndShots(fact.Scenes);
        var characters = fact.Characters
            .GroupBy(c => c.CharacterId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
        var blocked = FirstBlockedCharacter(fact, characters);

        var scenes = fact.Scenes.Select(scene => ResolveScene(scene, characters, blocked, fact.ScriptLocked, counts.SceneCount)).ToList();
        var imageMade = scenes.Sum(s => s.ImageMade);
        var imageApproved = scenes.Sum(s => s.ImageApproved);
        var videoMade = scenes.Sum(s => s.VideoMade);
        var videoApproved = scenes.Sum(s => s.VideoApproved);
        var complete = scenes.SelectMany(s => s.Shots).Count(s => s.Finalized);
        var published = fact.GraphPublished && complete >= counts.ShotCount && counts.ShotCount > 0;
        var finishReady = counts.ShotCount > 0 && complete >= counts.ShotCount;

        var stages = new List<StageNode>
        {
            new("script", "Kịch bản", fact.ScriptLocked, fact.ScriptLocked ? "Hoàn thành" : "Chưa khóa"),
            new("scenes", "Chia cảnh", counts.SceneCount > 0,
                counts.SceneCount > 0 ? $"{counts.SceneCount} cảnh · {counts.ShotCount} shot" : "Chưa chia"),
            new("cast", "Nhân vật", blocked is null && UniqueCharacterIds(fact).Count > 0,
                blocked is null && UniqueCharacterIds(fact).Count > 0 ? "Hoàn thành" : "Cần hoàn thiện"),
            new("image", "Tạo hình", counts.ShotCount > 0 && imageMade >= counts.ShotCount,
                counts.ShotCount > 0 ? $"{imageMade}/{counts.ShotCount}" : "0"),
            new("imageReview", "Duyệt hình", counts.ShotCount > 0 && imageApproved >= counts.ShotCount,
                counts.ShotCount > 0 ? $"{imageApproved}/{counts.ShotCount}" : "0"),
            new("video", "Tạo video", counts.ShotCount > 0 && videoMade >= counts.ShotCount,
                counts.ShotCount > 0 ? $"{videoMade}/{counts.ShotCount}" : "0"),
            new("videoReview", "Duyệt video", counts.ShotCount > 0 && videoApproved >= counts.ShotCount,
                counts.ShotCount > 0 ? $"{videoApproved}/{counts.ShotCount}" : "0"),
            new("finish", "Hoàn thiện", finishReady, finishReady ? $"{complete}/{counts.ShotCount}" : "Chưa sẵn sàng"),
            new("publish", "Xuất bản", published, published ? "Đã xuất bản" : "Chưa sẵn sàng"),
        };
        var current = stages.FirstOrDefault(s => !s.Done)?.Id ?? "publish";
        var next = EpisodeNextAction(fact, characters, blocked, counts, imageMade, imageApproved, videoMade, videoApproved, complete, published);
        return new EpisodeProgress(
            fact.BuildId,
            fact.SeriesCode,
            fact.EpisodeCode,
            fact.Title,
            counts.SceneCount,
            counts.ShotCount,
            UniqueCharacterIds(fact).Count,
            imageMade,
            imageApproved,
            videoMade,
            videoApproved,
            finishReady ? $"{complete}/{counts.ShotCount}" : "Chưa sẵn sàng",
            published ? "Đã xuất bản" : "Chưa sẵn sàng",
            current,
            next.Action,
            next.Reason,
            $"{counts.SceneCount} cảnh · {counts.ShotCount} shot · {UniqueCharacterIds(fact).Count} nhân vật",
            stages.Count(s => s.Done),
            TotalStages,
            published ? "done" : counts.SceneCount > 0 || fact.ScriptLocked ? "work" : "wait",
            stages,
            scenes);
    }

    public static string StaffMissingLabel(string? type) => (type ?? "").Trim().ToUpperInvariant() switch
    {
        "FRONT" => "Trước mặt",
        "THREE_QUARTER" => "3/4",
        "SIDE" => "Nghiêng",
        "FULL_BODY" => "Toàn thân",
        _ => type ?? "",
    };

    private static SceneProgress ResolveScene(
        SceneFact scene,
        IReadOnlyList<CharacterFact> characters,
        CharacterFact? blocked,
        bool scriptLocked,
        int sceneCount)
    {
        var shots = scene.Shots
            .Select(shot => ResolveShot(shot, characters, blocked, scriptLocked, sceneCount))
            .ToList();
        var names = scene.CharacterIds
            .Select(id => characters.FirstOrDefault(c => SameId(c.CharacterId, id))?.Name ?? id)
            .Where(n => n.Length > 0)
            .ToList();
        var next = shots.Select(s => (s.NextAction, s.BlockingReason)).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.NextAction));
        if (blocked is not null)
            next = (CharacterNextAction(blocked), CharacterBlockReason(blocked));
        else if (string.IsNullOrWhiteSpace(next.NextAction) && shots.Count > 0)
            next = (shots[0].NextAction, shots[0].BlockingReason);
        return new SceneProgress(
            scene.SceneId,
            scene.SceneName,
            shots.Count,
            scene.CharacterIds.Count,
            names,
            shots.Count(s => s.ImageMade),
            shots.Count(s => s.ImageApproved),
            shots.Count(s => s.VideoMade),
            shots.Count(s => s.VideoApproved),
            next.NextAction,
            next.BlockingReason,
            shots);
    }

    private static ShotProgress ResolveShot(
        ShotFact shot,
        IReadOnlyList<CharacterFact> characters,
        CharacterFact? episodeBlocked,
        bool scriptLocked,
        int sceneCount)
    {
        var names = shot.CharacterIds
            .Select(id => characters.FirstOrDefault(c => SameId(c.CharacterId, id))?.Name ?? id)
            .ToList();
        var shotBlocked = episodeBlocked
            ?? shot.CharacterIds
                .Select(id => characters.FirstOrDefault(c => SameId(c.CharacterId, id)))
                .FirstOrDefault(c => c is { CanUse: false });
        var imageMade = shot.HasWorkflowImage;
        var imageApproved = imageMade && shot.ImageDirectorApproved;
        var videoMade = imageApproved && shot.HasWorkflowVideo;
        var videoApproved = videoMade && shot.VideoDirectorApproved;
        var finalized = imageApproved && videoApproved && shot.Finalized;
        var next = ShotNext(scriptLocked, sceneCount, shotBlocked, imageMade, imageApproved, shot.VideoContractApproved, videoMade, videoApproved, finalized, shot.Published, shot.ShotNumber);
        return new ShotProgress(
            shot.ShotId,
            shot.SceneId,
            shot.ShotNumber,
            shot.CharacterIds,
            names,
            next.Step,
            next.Action,
            next.Staff,
            next.Reason,
            imageMade,
            imageApproved,
            videoMade,
            videoApproved,
            finalized,
            shot.Published && finalized);
    }

    private static (string Step, string Action, string Staff, string? Reason) ShotNext(
        bool scriptLocked,
        int sceneCount,
        CharacterFact? blocked,
        bool imageMade,
        bool imageApproved,
        bool videoContractApproved,
        bool videoMade,
        bool videoApproved,
        bool finalized,
        bool published,
        int shotNumber)
    {
        var shot = Pad(Math.Max(shotNumber, 1));
        if (!scriptLocked)
            return ("script", "Hoàn thiện kịch bản", "Chưa sẵn sàng", "Kịch bản chưa khóa.");
        if (sceneCount <= 0)
            return ("scenes", "Chia video thành các cảnh", "Chưa sẵn sàng", "Chưa chia cảnh.");
        if (blocked is not null)
            return ("cast", CharacterNextAction(blocked), "Cần bổ sung", CharacterBlockReason(blocked));
        if (!imageMade)
            return ("image", $"Tạo hình cho Shot {shot}", "Chờ tạo hình", null);
        if (!imageApproved)
            return ("imageReview", $"Duyệt hình Shot {shot}", "Chờ duyệt hình", "Hình đã tạo xong, đang chờ duyệt.");
        if (!videoContractApproved)
            return ("video", "Chưa có hợp đồng video", "Chưa có hợp đồng video", "Chưa chuẩn bị nội dung tạo video.");
        if (!videoMade)
            return ("video", $"Tạo video cho Shot {shot}", "Chờ tạo video", null);
        if (!videoApproved)
            return ("videoReview", $"Duyệt video Shot {shot}", "Chờ duyệt video", "Video đã tạo xong, đang chờ duyệt.");
        if (!finalized)
            return ("finish", "Hoàn thiện", "Đã duyệt", null);
        if (!published)
            return ("publish", "Xuất bản", "Đã hoàn thiện", null);
        return ("publish", "", "Đã xuất bản", null);
    }

    private static (string Action, string? Reason) EpisodeNextAction(
        EpisodeFact fact,
        IReadOnlyList<CharacterFact> characters,
        CharacterFact? blocked,
        (int SceneCount, int ShotCount) counts,
        int imageMade,
        int imageApproved,
        int videoMade,
        int videoApproved,
        int complete,
        bool published)
    {
        if (!fact.ScriptLocked)
            return ("Hoàn thiện kịch bản", "Kịch bản chưa khóa.");
        if (counts.SceneCount <= 0)
            return ("Chia video thành các cảnh", "Chưa chia cảnh.");
        if (blocked is not null)
            return (CharacterNextAction(blocked), CharacterBlockReason(blocked));
        if (counts.ShotCount > 0 && imageMade < counts.ShotCount)
        {
            var first = FirstOpen(fact, s => !s.HasWorkflowImage);
            return ($"Tạo hình cho Shot {Pad(first)}", null);
        }
        if (counts.ShotCount > 0 && imageApproved < counts.ShotCount)
        {
            var first = FirstOpen(fact, s => !(s.HasWorkflowImage && s.ImageDirectorApproved));
            return ($"Duyệt hình Shot {Pad(first)}", "Hình đã tạo xong, đang chờ duyệt.");
        }
        if (counts.ShotCount > 0 && videoMade < counts.ShotCount)
        {
            var needContract = fact.Scenes.SelectMany(s => s.Shots)
                .FirstOrDefault(s => s.HasWorkflowImage && s.ImageDirectorApproved && !s.VideoContractApproved);
            if (needContract is not null)
                return ("Chưa có hợp đồng video", "Chưa chuẩn bị nội dung tạo video.");
            var first = FirstOpen(fact, s => !(s.HasWorkflowImage && s.ImageDirectorApproved && s.HasWorkflowVideo));
            return ($"Tạo video cho Shot {Pad(first)}", null);
        }
        if (counts.ShotCount > 0 && videoApproved < counts.ShotCount)
        {
            var first = FirstOpen(fact, s => !s.VideoDirectorApproved);
            return ($"Duyệt video Shot {Pad(first)}", "Video đã tạo xong, đang chờ duyệt.");
        }
        if (counts.ShotCount > 0 && complete < counts.ShotCount)
            return ("Hoàn thiện tập", null);
        if (!published)
            return ("Xuất bản video", null);
        return ("", null);
    }

    private static CharacterFact? FirstBlockedCharacter(EpisodeFact fact, IReadOnlyList<CharacterFact> characters)
    {
        foreach (var id in UniqueCharacterIds(fact))
        {
            var row = characters.FirstOrDefault(c => SameId(c.CharacterId, id));
            if (row is null)
                return new CharacterFact(id, id, false, []);
            if (!row.CanUse)
                return row;
        }
        return UniqueCharacterIds(fact).Count == 0 && fact.Scenes.Count > 0
            ? new CharacterFact("", "nhân vật", false, [])
            : null;
    }

    private static string CharacterNextAction(CharacterFact row)
        => string.IsNullOrWhiteSpace(row.Name) || row.Name == "nhân vật"
            ? "Hoàn thiện bộ ảnh chuẩn"
            : $"Hoàn thiện bộ ảnh chuẩn cho {row.Name}";

    private static string CharacterBlockReason(CharacterFact row)
    {
        var missing = row.MissingTypes.Select(StaffMissingLabel).Where(x => x.Length > 0).ToList();
        if (missing.Count > 0)
            return $"Bộ ảnh chuẩn của {Display(row)} chưa hoàn tất. Thiếu: {string.Join(", ", missing)}.";
        if (string.IsNullOrWhiteSpace(row.CharacterId))
            return "Cảnh chưa gắn nhân vật.";
        return $"Bộ ảnh chuẩn của {Display(row)} chưa hoàn tất.";
    }

    private static string Display(CharacterFact row)
        => string.IsNullOrWhiteSpace(row.Name) || row.Name == "nhân vật" ? "nhân vật" : row.Name;

    private static IReadOnlyList<string> UniqueCharacterIds(EpisodeFact fact)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ids = new List<string>();
        foreach (var scene in fact.Scenes)
        {
            foreach (var id in scene.CharacterIds)
            {
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) continue;
                ids.Add(id);
            }
        }
        return ids;
    }

    private static int FirstOpen(EpisodeFact fact, Func<ShotFact, bool> pred)
    {
        foreach (var scene in fact.Scenes)
        {
            foreach (var shot in scene.Shots)
            {
                if (pred(shot)) return shot.ShotNumber;
            }
        }
        return 1;
    }

    private static string Pad(int n) => n.ToString("00");

    private static bool SameId(string a, string b)
        => string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
}
