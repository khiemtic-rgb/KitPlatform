namespace KitPlatform.Packs.Content;

/// <summary>KIT Video Engine Phase 01 — credit, media, retry, run status. Engine ≠ Project.</summary>
public static class KitVideoEngineRules
{
    public static readonly string[] RunStatuses =
    [
        "READY", "RUNNING", "SUCCEEDED", "FAILED", "BLOCKED", "CANCELLED", "PARTIALLY_COMPLETE",
    ];

    public static readonly string[] JobStatuses =
    [
        "PRECHECK", "READY", "CONFIRM_REQUIRED", "SUBMITTED", "RUNNING",
        "SUCCEEDED", "FAILED", "REFUND_PENDING", "BLOCKED",
    ];

    public static bool CanSubmitProvider(string shotState)
    {
        var s = (shotState ?? "").Trim().ToUpperInvariant();
        return s is not ("HOLD" or "DRAFT" or "");
    }

    public static string HoldIfNoAction(bool hasAction) => hasAction ? "READY" : "HOLD";

    public static string CreditGateNext(string status, bool confirmed)
    {
        var s = (status ?? "").Trim().ToUpperInvariant();
        if (s is "PRECHECK" or "")
            return confirmed ? "READY" : "PRECHECK";
        if (s == "READY")
            return confirmed ? "SUBMITTED" : "CONFIRM_REQUIRED";
        if (s == "CONFIRM_REQUIRED")
            return confirmed ? "SUBMITTED" : "CONFIRM_REQUIRED";
        return s;
    }

    public static void EnsureCreditGate(bool confirmed)
    {
        if (!confirmed)
            throw new InvalidOperationException("CREDIT_GATE: chưa USER CONFIRM — không gọi provider.");
    }

    public static void EnsureNotHold(string shotState)
    {
        if (!CanSubmitProvider(shotState))
            throw new InvalidOperationException("HOLD: shot không có Action / HOLD — không gửi Image Generation hoặc I2V.");
    }

    public static string ClassifyRetry(string? failureCode, string? error)
    {
        var code = (failureCode ?? "").ToUpperInvariant();
        var err = (error ?? "").ToUpperInvariant();
        if (code.Contains("BAD_OUTPUT") || err.Contains("BAD_OUTPUT"))
            return "REPAIR_REQUIRED";
        if (code.Contains("INVALID") || err.Contains("INVALID INPUT") || err.Contains("INVALID_INPUT"))
            return "NON_RETRYABLE";
        if (err.Contains("TIMEOUT") || err.Contains("NETWORK") || code.Contains("TIMEOUT") || code.Contains("UNAVAILABLE"))
            return "RETRYABLE";
        if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(err))
            return "UNKNOWN";
        return "UNKNOWN";
    }

    public static string RunStatusAfterShots(IReadOnlyList<(string State, bool Failed)> shots)
    {
        if (shots.Count == 0) return "READY";
        var failed = shots.Count(s =>
            string.Equals(s.State, "FAILED", StringComparison.OrdinalIgnoreCase) || s.Failed);
        var videoOk = shots.Count(s => s.State is "VIDEO_READY" or "LIPSYNC_READY" or "FINAL_SELECTED");
        var generating = shots.Any(s => s.State is "KF_GENERATING" or "I2V_GENERATING");
        if (generating) return "RUNNING";
        if (failed > 0 && failed < shots.Count) return "PARTIALLY_COMPLETE";
        if (failed == shots.Count) return "FAILED";
        if (videoOk == shots.Count) return "SUCCEEDED";
        return "READY";
    }

    public static KitVideoMediaValidation EvaluateMedia(KitVideoMediaSnapshot snap)
    {
        var provider = (snap.ProviderStatus ?? "").Trim().ToUpperInvariant();
        var url = (snap.OutputUrl ?? "").Trim();
        var httpOk = snap.HttpStatus is 200 or 201;
        var reasons = new List<string>();

        if (httpOk && provider is not "SUCCEEDED")
            reasons.Add("HTTP 200 không phải SUCCESS.");
        if (provider is not "SUCCEEDED")
            reasons.Add($"Provider status {provider} ≠ SUCCEEDED.");
        if (url.Length == 0)
            reasons.Add("SUCCEEDED nhưng không có Output URL.");
        if (snap.FileExists == false)
            reasons.Add("File không tồn tại.");
        if (snap.Readable == false)
            reasons.Add("File không đọc được.");
        if (snap.ProbeOk == false)
            reasons.Add(string.IsNullOrWhiteSpace(snap.ProbeError) ? "Download/probe lỗi." : snap.ProbeError!);
        if (snap.ContainerOk == false)
            reasons.Add("Media container không hợp lệ.");
        if (snap.DurationSec is <= 0)
            reasons.Add("Duration không hợp lệ.");
        if (snap.Width is < 16 || snap.Height is < 16)
            reasons.Add("Resolution không hợp lệ.");

        var ok = provider == "SUCCEEDED"
                 && url.Length > 0
                 && snap.FileExists != false
                 && snap.Readable != false
                 && snap.ProbeOk != false
                 && snap.ContainerOk != false
                 && snap.DurationSec is not <= 0
                 && snap.Width is not < 16
                 && snap.Height is not < 16;
        return new KitVideoMediaValidation(ok, ok ? "VIDEO_READY" : "FAILED", reasons);
    }
}

public sealed record KitVideoMediaSnapshot(
    string? ProviderStatus,
    string? OutputUrl,
    int? HttpStatus = null,
    bool? FileExists = null,
    bool? Readable = null,
    bool? ContainerOk = null,
    bool? ProbeOk = null,
    string? ProbeError = null,
    double? DurationSec = null,
    int? Width = null,
    int? Height = null);

public sealed record KitVideoMediaValidation(bool Ok, string ShotOutcome, IReadOnlyList<string> Reasons);

public interface IMediaValidator
{
    KitVideoMediaValidation Validate(KitVideoMediaSnapshot snapshot);
    Task<KitVideoMediaValidation> ValidateUrlAsync(
        KitVideoMediaSnapshot snapshot,
        CancellationToken cancellationToken = default);
}
