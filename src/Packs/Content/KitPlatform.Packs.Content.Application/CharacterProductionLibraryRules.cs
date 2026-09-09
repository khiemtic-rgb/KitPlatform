using System.Linq;

namespace KitPlatform.Packs.Content;

public static class CharacterProductionLibraryRules
{
    public const string DocumentId = "CHARACTER_PRODUCTION_LIBRARY_V1";
    public const string SuiteId = "CHARACTER_PRODUCTION_LIBRARY_V1_REGRESSION";

    public static readonly string[] Filters = ["all", "ready", "in_progress", "review", "problem"];
    public static readonly string[] Sorts = ["series", "name", "scenes", "status"];

    public sealed record AuthoritySnapshot(
        bool MasterExists,
        bool MasterLocked,
        bool DnaExists,
        bool DnaLocked,
        bool PrpExists,
        bool PrpLocked,
        string? CrpStatus,
        string? CrpVersion,
        bool CoverageReady,
        bool IdentityPass,
        int RequiredReady,
        int RequiredTotal,
        int SceneCount,
        IReadOnlyList<string>? MissingTypes = null);

    public sealed record ReadinessView(
        string Code,
        string Bucket,
        string Label,
        string Reason,
        string NextAction,
        bool CanUse);

    public static bool RequireCharacterId(string? characterId) =>
        !string.IsNullOrWhiteSpace(characterId);

    public static string NormalizeCharacterId(string? raw)
    {
        var v = (raw ?? "").Trim().ToUpperInvariant();
        if (v.Length == 0)
            throw new InvalidOperationException("LIBRARY_INVALID: characterId bắt buộc. Không mặc định nhân vật.");
        return v;
    }

    public static bool SameTenant(string? requested, string? owned) =>
        !string.IsNullOrWhiteSpace(requested)
        && !string.IsNullOrWhiteSpace(owned)
        && string.Equals(requested.Trim(), owned.Trim(), StringComparison.OrdinalIgnoreCase);

    public static bool IsLocked(string? status) =>
        string.Equals(status, "LOCKED", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "MASTER_REFERENCE_LOCKED", StringComparison.OrdinalIgnoreCase);

    public static bool IsImmutable(string? crpStatus) => IsLocked(crpStatus);

    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool AutoSelect() => false;
    public static bool CreatesPixels(string? action) =>
        action is not null && action.Contains("GENERAT", StringComparison.OrdinalIgnoreCase);

    public static ReadinessView EvaluateReadiness(AuthoritySnapshot snap)
    {
        if (!snap.IdentityPass)
            return new ReadinessView("IDENTITY_CONFLICT", "problem", "Có vấn đề về nhận diện", "Ảnh chuẩn đang khác nhận diện đã khóa.", "Khắc phục vấn đề nhận diện", false);
        if (!snap.MasterExists)
            return new ReadinessView("MASTER_MISSING", "blocked", "Chưa hoàn thiện hồ sơ", "Chưa có hồ sơ nhân vật đã khóa.", "Hoàn thiện hồ sơ nhân vật", false);
        if (!snap.MasterLocked)
            return new ReadinessView("MASTER_REVIEW_REQUIRED", "blocked", "Chưa hoàn thiện hồ sơ", "Hồ sơ nhân vật chưa khóa.", "Hoàn thiện hồ sơ nhân vật", false);
        if (!snap.DnaExists)
            return new ReadinessView("DNA_MISSING", "blocked", "Thiếu quy tắc nhận diện", "Chưa có quy tắc nhận diện đã khóa.", "Hoàn thiện quy tắc nhận diện", false);
        if (!snap.DnaLocked)
            return new ReadinessView("DNA_MISSING", "blocked", "Thiếu quy tắc nhận diện", "Quy tắc nhận diện chưa khóa.", "Hoàn thiện quy tắc nhận diện", false);
        if (!snap.PrpExists || !snap.PrpLocked)
            return new ReadinessView("PRP_MISSING", "blocked", "Chưa thể sử dụng", "Ảnh tham chiếu sản xuất chưa khóa.", "Hoàn thiện ảnh tham chiếu sản xuất", false);

        var crp = (snap.CrpStatus ?? "").ToUpperInvariant();
        if (crp.Length == 0)
            return new ReadinessView("CRP_MISSING", "in_progress", "Thiếu ảnh chuẩn", "Chưa có bộ ảnh chuẩn.", "Hoàn thiện bộ ảnh chuẩn", false);
        if (crp is "DRAFT" or "REJECTED")
            return new ReadinessView(
                snap.CoverageReady ? "CRP_DRAFT" : "REFERENCE_MISSING",
                "in_progress",
                snap.CoverageReady ? "Đang hoàn thiện ảnh chuẩn" : "Cần bổ sung",
                snap.CoverageReady ? "Bộ ảnh chuẩn chưa được kiểm tra." : MissingReason(snap),
                "Hoàn thiện bộ ảnh chuẩn",
                false);
        if (!snap.CoverageReady)
            return new ReadinessView("REFERENCE_MISSING", "in_progress", "Cần bổ sung", MissingReason(snap), "Hoàn thiện bộ ảnh chuẩn", false);
        if (crp is "VALIDATED" or "REVIEW")
            return new ReadinessView("CRP_VALIDATED", "review", "Bộ ảnh chuẩn đã hoàn tất", "Có thể chuyển sang bước tạo hình.", "Sẵn sàng tạo hình", true);
        if (crp is "APPROVED" or "DIRECTOR_APPROVED")
            return new ReadinessView("CRP_APPROVED", "review", "Đã duyệt — chờ khóa", "Bộ ảnh chuẩn đã duyệt, chưa khóa.", "Chờ khóa bộ ảnh", true);
        if (crp == "LOCKED" && snap.MasterLocked && snap.DnaLocked && snap.PrpLocked)
            return new ReadinessView("CRP_LOCKED", "ready", "Sẵn sàng sản xuất", "Nhân vật đã khóa và sẵn sàng dùng trong cảnh.", "Sẵn sàng sử dụng", true);
        return new ReadinessView("BLOCKED", "blocked", "Chưa thể sử dụng", "Nhân vật chưa đủ điều kiện sản xuất.", "Khắc phục hồ sơ nhân vật", false);
    }

    public static bool MatchesSearch(string query, string characterId, string name, string role)
    {
        var q = (query ?? "").Trim();
        if (q.Length == 0) return true;
        return Contains(characterId, q) || Contains(name, q) || Contains(role, q);
    }

    public static bool MatchesFilter(string filter, ReadinessView readiness)
    {
        var f = (filter ?? "all").Trim().ToLowerInvariant();
        if (f is "" or "all") return true;
        if (f == "ready") return readiness.Bucket == "ready";
        if (f == "in_progress") return readiness.Bucket == "in_progress";
        if (f == "review") return readiness.Bucket == "review";
        if (f == "problem") return readiness.Bucket is "problem" or "blocked";
        return true;
    }

    public static IReadOnlyList<T> SortRows<T>(
        IEnumerable<T> rows, string sort, Func<T, string> name, Func<T, int> scenes, Func<T, string> status, Func<T, int> seriesOrder)
    {
        var key = (sort ?? "series").Trim().ToLowerInvariant();
        return key switch
        {
            "name" => rows.OrderBy(name, StringComparer.OrdinalIgnoreCase).ToList(),
            "scenes" => rows.OrderByDescending(scenes).ThenBy(name, StringComparer.OrdinalIgnoreCase).ToList(),
            "status" => rows.OrderBy(status, StringComparer.OrdinalIgnoreCase).ThenBy(name, StringComparer.OrdinalIgnoreCase).ToList(),
            _ => rows.OrderBy(seriesOrder).ToList(),
        };
    }

    public static bool PickerAllows(ReadinessView readiness) => readiness.CanUse;

    public static object TechnicalSnapshot(AuthoritySnapshot snap, ReadinessView view) => new
    {
        master = snap.MasterLocked ? "LOCKED" : snap.MasterExists ? "UNLOCKED" : "MISSING",
        dna = snap.DnaLocked ? "LOCKED" : snap.DnaExists ? "UNLOCKED" : "MISSING",
        production_reference = snap.PrpLocked ? "LOCKED" : snap.PrpExists ? "UNLOCKED" : "MISSING",
        reference_pack = snap.CrpStatus ?? "MISSING",
        reference_pack_version = snap.CrpVersion ?? "",
        readiness = view.Code,
        generate = false,
    };

    public static string MissingReason(AuthoritySnapshot snap)
    {
        var missing = (snap.MissingTypes ?? [])
            .Select(StaffViewLabel)
            .Where(x => x.Length > 0)
            .ToList();
        return missing.Count > 0 ? $"Thiếu: {string.Join(", ", missing)}" : "Bộ ảnh nhân vật chưa hoàn tất.";
    }

    public static string StaffViewLabel(string? type) => (type ?? "").Trim().ToUpperInvariant() switch
    {
        "FRONT" => "Trước mặt",
        "THREE_QUARTER" => "3/4",
        "SIDE" => "Nghiêng",
        "FULL_BODY" => "Toàn thân",
        _ => (type ?? "").Trim(),
    };

    private static bool Contains(string value, string query) =>
        (value ?? "").Contains(query, StringComparison.OrdinalIgnoreCase);
}
