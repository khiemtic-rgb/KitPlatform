using System.Linq;

namespace KitPlatform.Packs.Content;

public static class ProductionWorkflowHardeningRules
{
    public const string DocumentId = "FAMIXA_PRODUCTION_WORKFLOW_HARDENING_V1";
    public const string SuiteId = "FAMIXA_PRODUCTION_WORKFLOW_HARDENING_V1_REGRESSION";

    public sealed record SceneShotGroup(string SceneId, int ShotCount);

    public sealed record ShotProgressInput(bool HasStill, bool HasClip, bool ImageApproved, bool VideoApproved);

    public sealed record ShotProgressCount(
        bool ImageMade,
        bool ImageApproved,
        bool VideoMade,
        bool VideoApproved,
        bool Complete);

    public sealed record ProgressTotals(
        int SceneCount,
        int ShotCount,
        int ImageMade,
        int ImageApproved,
        int VideoMade,
        int VideoApproved,
        int Complete);

    public sealed record SceneAssignResult(bool Ok, IReadOnlyList<string> CharacterIds, string? Error, string? CharacterId);

    public static bool Generate => false;
    public static bool GeminiCalled => false;
    public static bool RunwayCalled => false;
    public static bool VeoCalled => false;
    public static bool AutoApprove => false;
    public static bool AutoLock => false;

    public static (int SceneCount, int ShotCount) CountScenesAndShots(IEnumerable<SceneShotGroup> scenes)
    {
        var list = scenes.ToList();
        return (list.Count, list.Sum(x => x.ShotCount));
    }

    public static ShotProgressCount EvaluateShot(ShotProgressInput input)
    {
        var imageMade = input.HasStill;
        var imageApproved = imageMade && input.ImageApproved;
        var videoMade = imageMade && input.HasClip;
        var videoApproved = videoMade && input.VideoApproved;
        return new ShotProgressCount(imageMade, imageApproved, videoMade, videoApproved, imageApproved && videoApproved);
    }

    public static ProgressTotals EvaluateProgress(IEnumerable<SceneShotGroup> scenes, IEnumerable<ShotProgressInput> shots)
    {
        var counts = CountScenesAndShots(scenes);
        var rows = shots.Select(EvaluateShot).ToList();
        return new ProgressTotals(
            counts.SceneCount,
            counts.ShotCount,
            rows.Count(x => x.ImageMade),
            rows.Count(x => x.ImageApproved),
            rows.Count(x => x.VideoMade),
            rows.Count(x => x.VideoApproved),
            rows.Count(x => x.Complete));
    }

    public static SceneAssignResult AssignSceneCharacters(IEnumerable<string> requested, IEnumerable<string> known)
    {
        var allowed = known
            .Select(x => (x ?? "").Trim().ToUpperInvariant())
            .Where(x => x.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ids = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in requested)
        {
            var id = (raw ?? "").Trim();
            if (id.Length == 0) continue;
            if (!allowed.Contains(id))
                return new SceneAssignResult(false, [], "Không thể sử dụng nhân vật này", id);
            if (seen.Add(id.ToUpperInvariant()))
                ids.Add(id);
        }
        return new SceneAssignResult(true, ids, null, null);
    }

    public static string StaffStatus(string? raw)
    {
        var s = (raw ?? "").Trim().ToUpperInvariant();
        return s switch
        {
            "READY_FOR_DIRECTOR" => "CẦN DUYỆT",
            "IMAGE_APPROVED" or "DIRECTOR_APPROVED" or "APPROVED" => "ĐÃ DUYỆT",
            "BLOCKED" => "BỊ CHẶN",
            "DRAFT" => "BẢN NHÁP",
            "REFERENCE_MISSING" => "BỊ CHẶN",
            _ => s.Length == 0 ? "CHƯA BẮT ĐẦU" : "ĐANG THỰC HIỆN",
        };
    }

    public static string StaffBlockReason(string? raw)
    {
        var s = (raw ?? "").Trim().ToUpperInvariant();
        if (s.Contains("REFERENCE_MISSING") || s.Contains("CRP_DRAFT"))
            return "Bị chặn — Bộ ảnh nhân vật chưa hoàn tất";
        if (s.Contains("VIDEO_GENERATION_NOT_READY"))
            return "Chưa thể tạo video — cần duyệt hình trước";
        if (s.Contains("READY_FOR_DIRECTOR"))
            return "Đang chờ duyệt hình";
        return "Bước này chưa sẵn sàng.";
    }
}
