namespace KitPlatform.Packs.Content;

/// <summary>
/// One directory rule for the local renderer and GET video/media.
/// </summary>
public static class ContentVideoMediaPaths
{
    public const string DefaultRoot = "App_Data/content-video";
    public const string OutputFileName = "output.mp4";

    public static string ResolveRoot(string? configured, string contentRoot)
    {
        var root = string.IsNullOrWhiteSpace(configured) ? DefaultRoot : configured.Trim();
        return Path.IsPathRooted(root)
            ? Path.GetFullPath(root)
            : Path.GetFullPath(Path.Combine(contentRoot, root));
    }

    public static string JobDirectory(string videoAssetRoot, Guid jobId) =>
        Path.Combine(Path.GetFullPath(videoAssetRoot), jobId.ToString("N"));

    public static string File(string videoAssetRoot, Guid jobId, string fileName) =>
        Path.Combine(JobDirectory(videoAssetRoot, jobId), fileName);

    public static string OutputRoute(Guid jobId) =>
        "/api/content/video/media/" + jobId.ToString("D") + "/" + OutputFileName;

    public static bool IsSafeFileName(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && fileName.IndexOfAny(['/', '\\']) < 0
        && !fileName.Contains("..", StringComparison.Ordinal);

    public static bool IsJobOutputRoute(Guid jobId, string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;
        var suffix = OutputRoute(jobId);
        if (url.Equals(suffix, StringComparison.OrdinalIgnoreCase))
            return true;
        return ContentVideoRenderStatus.IsPlayableVideo(url)
               && url.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
    }
}
