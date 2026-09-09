using KitPlatform.Packs.Sales;

namespace KitPlatform.Packs.Sales.Infrastructure;

internal static class NovixaPainDiscovery
{
    public const int LearnThreshold = 20;

    public static readonly string[] ResponseCodes =
    [
        "no_response",
        "seen_no_reply",
        "replied_not_interested",
        "objection",
        "interested",
        "phc_clicked",
        "phc_started",
        "phc_completed",
        "demo",
        "pilot",
    ];

    public static decimal ResponsePoints(string? code) => code switch
    {
        "seen_no_reply" => 5,
        "replied_not_interested" => -25,
        "objection" => 15,
        "interested" => 45,
        "phc_clicked" => 55,
        "phc_started" => 70,
        "phc_completed" => 85,
        "demo" => 90,
        "pilot" => 100,
        _ => 0,
    };

    public static bool IsPositive(string? code) => ResponsePoints(code) >= 45;

    public static bool IsConfirmed(string? code) => code is "interested" or "phc_clicked"
        or "phc_started" or "phc_completed" or "demo" or "pilot";

    public static bool RevealsSolution(string? code) => IsConfirmed(code) || code == "objection";

    public static decimal BlendConfidence(decimal current, decimal incoming) =>
        Math.Clamp(Math.Round(current * 0.65m + incoming * 0.35m, 2), -40, 100);

    public static string Mode(int marketTouches) =>
        marketTouches >= LearnThreshold ? "learn" : "explore";

    public static (NovixaPainDef Pain, string Mode, string Why) Pick(
        IReadOnlyList<LeadPainRow> leadRows,
        IReadOnlyList<KitSalesPainMarketBucketDto> market,
        int marketTouches,
        string? preferredCode = null)
    {
        if (!string.IsNullOrWhiteSpace(preferredCode))
        {
            var chosen = NovixaPainCatalog.Get(preferredCode);
            return (chosen, "staff", "Nhân viên chọn nỗi đau này.");
        }

        var mode = Mode(marketTouches);
        var recent = leadRows
            .Where(r => r.LastApproachedAt is { } at && at > DateTime.UtcNow.AddDays(-14))
            .Select(r => r.PainCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var exploit = leadRows
            .Where(r => r.Confidence >= 55 && !recent.Contains(r.PainCode))
            .OrderByDescending(r => r.Confidence)
            .FirstOrDefault();
        if (exploit is not null && mode == "learn")
        {
            var pain = NovixaPainCatalog.Get(exploit.PainCode);
            return (pain, "exploit",
                $"Lead này đã có tín hiệu {NovixaPainCatalog.CategoryLabel(pain.Category)} ({exploit.Confidence:0}).");
        }

        var shares = BlendShares(market, mode);
        var category = WeightedCategory(shares, leadRows, recent);
        var pool = NovixaPainCatalog.All
            .Where(p => p.Category == category && !recent.Contains(p.Code))
            .ToList();
        if (pool.Count == 0)
            pool = NovixaPainCatalog.All.Where(p => !recent.Contains(p.Code)).ToList();
        if (pool.Count == 0)
            pool = NovixaPainCatalog.All.ToList();

        var used = leadRows.Select(r => r.PainCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var fresh = pool.Where(p => !used.Contains(p.Code)).ToList();
        var pick = (fresh.Count > 0 ? fresh : pool)[Random.Shared.Next(fresh.Count > 0 ? fresh.Count : pool.Count)];
        var why = mode == "explore"
            ? $"Explore: thử nhóm {NovixaPainCatalog.CategoryLabel(category)} — chưa kết luận thị trường."
            : $"Learn: nhóm {NovixaPainCatalog.CategoryLabel(category)} đang được thăm dò theo tín hiệu thị trường.";
        return (pick, mode, why);
    }

    public static IReadOnlyDictionary<string, decimal> BlendShares(
        IReadOnlyList<KitSalesPainMarketBucketDto> market,
        string mode)
    {
        var explore = NovixaPainCatalog.ExploreShare;
        if (mode == "explore" || market.Sum(m => m.Approached) < LearnThreshold)
            return explore;

        var weights = new Dictionary<string, decimal>();
        foreach (var (category, share) in explore)
        {
            var row = market.FirstOrDefault(m => m.Category == category);
            var signal = row?.SignalRate ?? 0;
            weights[category] = share * 0.40m + (signal + 0.08m) * 0.60m;
        }

        var total = weights.Values.Sum();
        if (total <= 0)
            return explore;
        return weights.ToDictionary(kv => kv.Key, kv => kv.Value / total);
    }

    private static string WeightedCategory(
        IReadOnlyDictionary<string, decimal> shares,
        IReadOnlyList<LeadPainRow> leadRows,
        HashSet<string> recent)
    {
        var blocked = leadRows
            .Where(r => r.ResponseCode == "replied_not_interested" || recent.Contains(r.PainCode))
            .Select(r => r.Category)
            .GroupBy(c => c)
            .Where(g => g.Count() >= 3)
            .Select(g => g.Key)
            .ToHashSet();

        var live = shares
            .Where(kv => !blocked.Contains(kv.Key))
            .ToList();
        if (live.Count == 0)
            live = shares.ToList();

        var roll = (decimal)Random.Shared.NextDouble() * live.Sum(kv => kv.Value);
        var acc = 0m;
        foreach (var (category, share) in live)
        {
            acc += share;
            if (roll <= acc)
                return category;
        }

        return live[0].Key;
    }
}

internal sealed class LeadPainRow
{
    public string PainCode { get; init; } = "";
    public string Category { get; init; } = "";
    public decimal Confidence { get; init; }
    public string? ResponseCode { get; init; }
    public int ApproachedCount { get; init; }
    public decimal ResponseScore { get; init; }
    public decimal EngagementScore { get; init; }
    public bool Confirmed { get; init; }
    public DateTime? LastApproachedAt { get; init; }
    public string? Evidence { get; init; }
}
