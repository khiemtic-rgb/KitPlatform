namespace KitPlatform.Packs.Content;

/// <summary>
/// Maps a provider render report onto a job status.
/// A success word without a video URL is a failure, not a finished video.
/// Quota, keyframe, and other failure words are never left in Rendering.
/// </summary>
public static class ContentVideoRenderStatus
{
    public static string FromProvider(string? providerStatus, string? outputUrl)
    {
        var status = (providerStatus ?? "").Trim().ToLowerInvariant();
        if (IsFailure(status))
            return "Failed";
        if (IsSuccess(status))
            return IsPlayableVideo(outputUrl) ? "Ready" : "Failed";
        if (status is "planned" or "waiting" or "queued" or "pending" or "submitted")
            return "Queued";
        if (status is "rendering" or "transcribing" or "processing" or "running" or "")
            return "Rendering";
        return "Failed";
    }

    public static string FailureNote(string? providerStatus, string? outputUrl)
    {
        var status = (providerStatus ?? "").Trim();
        if (IsSuccess(status) && !IsPlayableVideo(outputUrl))
            return "Provider báo xong nhưng không có file video.";
        if (string.IsNullOrWhiteSpace(status))
            return "Provider không trả file video.";
        return "Provider không trả file video: " + status;
    }

    public static bool IsPlayableVideo(string? outputUrl)
    {
        var value = (outputUrl ?? "").Trim();
        if (value.Length < 8)
            return false;
        if (value.StartsWith("data:video", StringComparison.OrdinalIgnoreCase))
            return true;
        if (!value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            return false;
        var path = value.Split('?', 2)[0];
        if (path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    private static bool IsSuccess(string status) =>
        status is "succeeded" or "success" or "completed";

    private static bool IsFailure(string status) =>
        status.Contains("fail", StringComparison.Ordinal)
        || status.Contains("error", StringComparison.Ordinal)
        || status.Contains("quota", StringComparison.Ordinal)
        || status.Contains("keyframe", StringComparison.Ordinal)
        || status.Contains("cancel", StringComparison.Ordinal)
        || status.Contains("429", StringComparison.Ordinal)
        || status.Contains("exhausted", StringComparison.Ordinal);
}
