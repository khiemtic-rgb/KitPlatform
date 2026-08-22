namespace KitPlatform.Api.LocalOs;

/// <summary>KIT_MKT cover files for pack_local listings. Lives under App_Data — do not rsync-wipe.</summary>
internal static class LocalOsCoverStore
{
    public static string DirectoryPath(IHostEnvironment env) =>
        Path.Combine(env.ContentRootPath, "App_Data", "local-os-covers");

    public static string PublicPath(Guid listingId) =>
        $"/api/public/local-os/listings/{listingId:D}/cover";

    public static string Save(
        IHostEnvironment env,
        Guid listingId,
        byte[] bytes,
        string? contentType,
        string? fileName)
    {
        var dir = DirectoryPath(env);
        Directory.CreateDirectory(dir);
        foreach (var old in Directory.GetFiles(dir, listingId.ToString("N") + ".*"))
            File.Delete(old);
        var path = Path.Combine(dir, listingId.ToString("N") + ExtFrom(contentType, fileName));
        File.WriteAllBytes(path, bytes);
        return PublicPath(listingId);
    }

    public static (string Path, string ContentType)? Find(IHostEnvironment env, Guid listingId)
    {
        var dir = DirectoryPath(env);
        foreach (var ext in new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" })
        {
            var path = Path.Combine(dir, listingId.ToString("N") + ext);
            if (File.Exists(path))
                return (path, Mime(ext));
        }

        return null;
    }

    private static string ExtFrom(string? contentType, string? fileName)
    {
        var ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        if (ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif")
            return ext;
        return (contentType ?? "").Trim().ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".jpg",
        };
    }

    private static string Mime(string ext) => ext switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => "image/jpeg",
    };
}
