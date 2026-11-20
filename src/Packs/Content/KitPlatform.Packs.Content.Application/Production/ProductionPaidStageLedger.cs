using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace KitPlatform.Packs.Content.Production;

public sealed record PaidStageAttemptRow(
    bool ReachedProvider,
    bool Open,
    string Fingerprint,
    bool Classified,
    decimal EstimatedUsd,
    string? Failure = null);

/// <summary>
/// Counts paid provider attempts for one shot and one stage.
/// A provider failure counts as an attempt. A failure before the provider does not.
/// </summary>
public static class ProductionPaidStageLedger
{
    public static (int Paid, bool InFlight, bool Classified, string? Fingerprint, decimal SpentUsd, bool BalanceRefusal) Read(
        IEnumerable<PaidStageAttemptRow> rows)
    {
        var paid = 0;
        var inFlight = false;
        var classified = false;
        string? fingerprint = null;
        var spent = 0m;
        var balanceRefusal = false;
        foreach (var row in rows)
        {
            if (row.Open)
                inFlight = true;
            if (!row.ReachedProvider)
                continue;
            paid++;
            spent += row.EstimatedUsd;
            fingerprint = row.Fingerprint;
            classified = row.Classified;
            balanceRefusal = IsProviderBalanceRefusal(row.Failure);
        }
        return (paid, inFlight, classified, fingerprint, spent, balanceRefusal);
    }

    /// <summary>The provider refused before generating because the account had no balance. Topping up is the remedy.</summary>
    public static bool IsProviderBalanceRefusal(string? failure)
    {
        var text = failure ?? "";
        return text.Contains("Exhausted balance", StringComparison.OrdinalIgnoreCase)
            || text.Contains("User is locked", StringComparison.OrdinalIgnoreCase);
    }

    public static string Gate(
        int paid,
        bool inFlight,
        bool classified,
        bool inputChanged,
        decimal spentUsd,
        decimal nextUsd,
        int maxAttempts = ProductionPaidStagePolicy.MaxPaidAttempts)
    {
        if (inFlight)
            return ProductionFailureReasons.InFlightReplay;
        var decision = ProductionPaidStagePolicy.Decide(
            paid,
            paid > 0 && classified,
            paid > 0 && inputChanged,
            spentUsd,
            nextUsd,
            maxAttempts);
        if (decision != ProductionPaidStagePolicy.Blocked)
            return decision;
        if (paid >= maxAttempts)
            return ProductionFailureReasons.PaidStageExhausted;
        if (paid > 0 && (!classified || !inputChanged))
            return ProductionFailureReasons.PaidStageUnchanged;
        return ProductionFailureReasons.OverBudget;
    }

    public static string ImageFingerprint(string? sizeToken, string? references, string? acting = null, string? visual = null) =>
        (sizeToken ?? "") + "|" + (references ?? "") + "|" + ActingStamp(acting) + "|" + ActingStamp(visual);

    public static string VisualSection(string? prompt)
    {
        var text = prompt ?? "";
        const string start = "VISUAL:";
        var at = text.IndexOf(start, StringComparison.Ordinal);
        if (at < 0) return "";
        at += start.Length;
        var end = text.IndexOf("\nMOTION:", at, StringComparison.Ordinal);
        if (end < 0) return text[at..].Trim();
        return text[at..end].Trim();
    }

    public static string ActingSection(string? prompt)
    {
        var text = prompt ?? "";
        const string start = "ACTING:";
        var at = text.IndexOf(start, StringComparison.Ordinal);
        if (at < 0) return "";
        at += start.Length;
        var end = text.IndexOf("\nVISUAL:", at, StringComparison.Ordinal);
        if (end < 0) return text[at..].Trim();
        return text[at..end].Trim();
    }

    static string ActingStamp(string? acting)
    {
        var text = (acting ?? "").Trim();
        if (text.Length == 0) return "";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
    }

    public static string VideoFingerprint(Guid? keyframeId, decimal durationSec, string? snapshotHash) =>
        (keyframeId?.ToString("N") ?? "") + "|"
        + durationSec.ToString(CultureInfo.InvariantCulture) + "|"
        + (snapshotHash ?? "");

    public static string HistoricalImageSize(string? idempotencyKey, string? failureReason)
    {
        var text = (failureReason ?? "") + "\n" + (idempotencyKey ?? "");
        if (text.Contains("image_size '0.5K'", StringComparison.Ordinal) || text.Contains(":0.5K:", StringComparison.Ordinal))
            return "0.5K";
        if (text.Contains("image_size '512'", StringComparison.Ordinal) || text.Contains(":512:", StringComparison.Ordinal))
            return "512";
        if (text.Contains("image_size '1K'", StringComparison.Ordinal) || text.Contains(":1K:", StringComparison.Ordinal))
            return "1K";
        if (text.Contains("image_size '2K'", StringComparison.Ordinal) || text.Contains(":2K:", StringComparison.Ordinal))
            return "2K";
        if (text.Contains("image_size '4K'", StringComparison.Ordinal) || text.Contains(":4K:", StringComparison.Ordinal))
            return "4K";
        return "";
    }

    public static bool IsProviderApiCall(string? providerId, string? modelId)
    {
        var provider = (providerId ?? "").Trim();
        var model = (modelId ?? "").Trim();
        if (provider.Length == 0)
            return false;
        if (provider.Equals("import", StringComparison.OrdinalIgnoreCase)
            || provider.Equals("fixture", StringComparison.OrdinalIgnoreCase))
            return false;
        if (model.StartsWith("existing-", StringComparison.OrdinalIgnoreCase)
            || model.StartsWith("fixture-", StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    public static bool CountsTowardCap(DateTimeOffset createdAt, string? providerId, string? modelId, bool reachedProvider) =>
        createdAt >= ProductionPaidStagePolicy.CountFrom
        && reachedProvider
        && IsProviderApiCall(providerId, modelId);

    public static decimal ImageEstimateUsd(string taskStatus, string? sizeToken)
    {
        if (!string.Equals(taskStatus, ProductionTaskStatuses.Succeeded, StringComparison.Ordinal))
            return 0m;
        return sizeToken switch
        {
            "512" or "0.5K" => 0.045m,
            "2K" => 0.101m,
            "4K" => 0.151m,
            _ => 0.067m,
        };
    }
}
