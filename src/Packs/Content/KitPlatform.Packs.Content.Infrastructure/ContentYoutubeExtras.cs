using Microsoft.Extensions.Logging;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed record ContentYoutubeSeries(Guid Id, string Name, int? EpisodeNo = null);

internal sealed record ContentYoutubeCaptionFile(string Language, string Path);

internal sealed record ContentYoutubeThumbnail(string Path, string ContentType);

internal sealed record ContentYoutubeExtrasRequest(
    string AccessToken,
    string VideoId,
    Guid ChannelTargetId,
    ContentYoutubeSeries? Series,
    IReadOnlyList<ContentYoutubeCaptionFile> Captions,
    ContentYoutubeThumbnail? Thumbnail);

internal sealed class ContentYoutubeExtrasResult
{
    public string? PlaylistId { get; set; }
    public bool PlaylistCreated { get; set; }
    public List<string> Captions { get; } = [];
    public bool Thumbnail { get; set; }
    public List<string> Warnings { get; } = [];
}

/// <summary>Playlist id YouTube gave a series on one channel.</summary>
internal interface IContentYoutubePlaylistStore
{
    Task<string?> GetPlaylistAsync(Guid seriesId, Guid channelTargetId, CancellationToken ct);
    Task SavePlaylistAsync(Guid seriesId, Guid channelTargetId, string playlistId, CancellationToken ct);
}

/// <summary>
/// Steps after videos.insert succeeded. The video is already on YouTube, so a failed step is a warning and never fails the job.
/// </summary>
internal static class ContentYoutubeExtras
{
    public const long ThumbnailMaxBytes = 2 * 1024 * 1024;

    public static async Task<ContentYoutubeExtrasResult> RunAsync(
        IContentYoutubeApi api,
        IContentYoutubePlaylistStore store,
        ContentYoutubeExtrasRequest request,
        ILogger logger,
        CancellationToken ct)
    {
        var result = new ContentYoutubeExtrasResult();

        if (request.Series is { } series)
            await Step(result, logger, "Playlist", () => AddToSeriesPlaylistAsync(api, store, request, series, result, ct), ct);

        foreach (var caption in request.Captions)
        {
            await Step(result, logger, $"Phụ đề {caption.Language}", async () =>
            {
                await api.InsertCaptionAsync(request.AccessToken, request.VideoId, caption.Language,
                    caption.Language == "vi" ? "Tiếng Việt" : "English", caption.Path, ct);
                result.Captions.Add(caption.Language);
            }, ct);
        }

        if (request.Thumbnail is { } thumb)
        {
            await Step(result, logger, "Ảnh bìa", async () =>
            {
                var size = new FileInfo(thumb.Path).Length;
                if (size > ThumbnailMaxBytes)
                    throw new InvalidOperationException($"ảnh {size / 1024} KB, YouTube chỉ nhận tối đa 2 MB.");
                await api.SetThumbnailAsync(request.AccessToken, request.VideoId, thumb.Path, thumb.ContentType, ct);
                result.Thumbnail = true;
            }, ct);
        }

        return result;
    }

    static async Task AddToSeriesPlaylistAsync(
        IContentYoutubeApi api,
        IContentYoutubePlaylistStore store,
        ContentYoutubeExtrasRequest request,
        ContentYoutubeSeries series,
        ContentYoutubeExtrasResult result,
        CancellationToken ct)
    {
        var playlistId = await store.GetPlaylistAsync(series.Id, request.ChannelTargetId, ct);
        var created = false;
        if (string.IsNullOrWhiteSpace(playlistId))
        {
            playlistId = await CreateAsync(api, store, request, series, ct);
            created = true;
        }

        try
        {
            await api.AddToPlaylistAsync(request.AccessToken, playlistId, request.VideoId, ct);
        }
        catch (ContentYoutubeException ex) when (!created && ex.StatusCode == 404)
        {
            // The saved playlist was deleted on YouTube: make a new one for the series once.
            playlistId = await CreateAsync(api, store, request, series, ct);
            created = true;
            await api.AddToPlaylistAsync(request.AccessToken, playlistId, request.VideoId, ct);
        }

        result.PlaylistId = playlistId;
        result.PlaylistCreated = created;
    }

    static async Task<string> CreateAsync(
        IContentYoutubeApi api, IContentYoutubePlaylistStore store, ContentYoutubeExtrasRequest request,
        ContentYoutubeSeries series, CancellationToken ct)
    {
        var title = series.Name.Trim();
        if (title.Length > 150) title = title[..150].TrimEnd();
        var id = await api.CreatePlaylistAsync(request.AccessToken, title, "public", ct);
        await store.SavePlaylistAsync(series.Id, request.ChannelTargetId, id, ct);
        return id;
    }

    static async Task Step(ContentYoutubeExtrasResult result, ILogger logger, string name, Func<Task> run, CancellationToken ct)
    {
        try
        {
            await run();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"{name}: {ex.Message}");
            logger.LogWarning("YouTube bước phụ «{Step}» lỗi, video vẫn đã đăng: {Error}", name, ex.Message);
        }
    }
}
