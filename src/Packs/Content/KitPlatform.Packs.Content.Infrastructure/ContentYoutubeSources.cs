using System.Text.Json;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>Files on the server that go with a YouTube upload: subtitles and a custom thumbnail.</summary>
internal static class ContentYoutubeSources
{
    static readonly string[] Languages = ["vi", "en"];

    /// <summary>
    /// .srt per language (vi, en): a topic asset first (kind "subtitle" or a .srt file, language from meta or the name),
    /// then a file next to the video named "*.vi.srt", "*_en.srt", "vi.srt" and so on.
    /// </summary>
    public static IReadOnlyList<ContentYoutubeCaptionFile> Captions(
        IReadOnlyList<ContentRepository.AssetRow> assets, string assetRoot, string videoPath)
    {
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var a in assets.OrderByDescending(a => a.IsSelected).ThenByDescending(a => a.CreatedAt))
        {
            var isSubtitle = a.Kind == "subtitle" || a.StoragePath.EndsWith(".srt", StringComparison.OrdinalIgnoreCase);
            if (!isSubtitle) continue;
            var lang = MetaLanguage(a.MetaJson) ?? FileLanguage(a.FileName) ?? FileLanguage(a.StoragePath);
            if (lang is null || found.ContainsKey(lang)) continue;
            var path = Path.Combine(assetRoot, a.StoragePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) found[lang] = path;
        }

        var dir = Path.GetDirectoryName(videoPath);
        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.srt").OrderBy(f => f, StringComparer.Ordinal))
            {
                var lang = FileLanguage(file);
                if (lang is not null && !found.ContainsKey(lang)) found[lang] = file;
            }
        }

        return Languages.Where(found.ContainsKey).Select(l => new ContentYoutubeCaptionFile(l, found[l])).ToList();
    }

    internal static string? FileLanguage(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var stem = Path.GetFileNameWithoutExtension(name).ToLowerInvariant();
        foreach (var lang in Languages)
        {
            if (stem == lang || stem.EndsWith("." + lang, StringComparison.Ordinal)
                || stem.EndsWith("_" + lang, StringComparison.Ordinal) || stem.EndsWith("-" + lang, StringComparison.Ordinal))
                return lang;
        }
        return null;
    }

    static string? MetaLanguage(string? metaJson)
    {
        var value = MetaString(metaJson, "language") ?? MetaString(metaJson, "lang");
        var lang = value?.Trim().ToLowerInvariant();
        return lang is not null && Languages.Contains(lang) ? lang : null;
    }

    /// <summary>
    /// Image asset marked role "thumbnail" / "youtube_thumbnail", else the selected cover image. JPEG or PNG only.
    /// </summary>
    public static ContentYoutubeThumbnail? Thumbnail(IReadOnlyList<ContentRepository.AssetRow> assets, string assetRoot)
    {
        var images = assets.Where(a => a.Kind == "image" && a.ContentType is "image/jpeg" or "image/png").ToList();
        var pick = images
                       .Where(a => MetaString(a.MetaJson, "role") is "thumbnail" or "youtube_thumbnail")
                       .OrderByDescending(a => a.CreatedAt)
                       .FirstOrDefault()
                   ?? images.Where(a => a.IsSelected).OrderByDescending(a => a.CreatedAt).FirstOrDefault();
        if (pick is null) return null;
        var path = Path.Combine(assetRoot, pick.StoragePath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? new ContentYoutubeThumbnail(path, pick.ContentType) : null;
    }

    static string? MetaString(string? metaJson, string key)
    {
        if (string.IsNullOrWhiteSpace(metaJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(metaJson);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
