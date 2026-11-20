using KitPlatform.Packs.Content.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KitPlatform.Content.Tests;

public class ContentYoutubeExtrasTests
{
    static readonly Guid SeriesId = Guid.Parse("01a18af7-0000-7000-8000-000000000001");
    static readonly Guid ChannelId = Guid.Parse("01a18d7e-0077-70c3-a174-c5eaafd35c3c");

    sealed class FakeStore : IContentYoutubePlaylistStore
    {
        public Dictionary<(Guid, Guid), string> Rows { get; } = [];
        public bool FailSave { get; init; }

        public Task<string?> GetPlaylistAsync(Guid seriesId, Guid channelTargetId, CancellationToken ct) =>
            Task.FromResult(Rows.TryGetValue((seriesId, channelTargetId), out var id) ? id : null);

        public Task SavePlaylistAsync(Guid seriesId, Guid channelTargetId, string playlistId, CancellationToken ct)
        {
            if (FailSave) throw new InvalidOperationException("db down");
            Rows[(seriesId, channelTargetId)] = playlistId;
            return Task.CompletedTask;
        }
    }

    sealed class FakeApi : IContentYoutubeApi
    {
        public List<string> Calls { get; } = [];
        public HashSet<string> MissingPlaylists { get; init; } = [];
        public bool FailCaptions { get; init; }
        public bool FailThumbnail { get; init; }
        public bool FailPlaylistCreate { get; init; }
        int _next;

        public Task<string> CreatePlaylistAsync(string accessToken, string title, string privacy, CancellationToken ct)
        {
            Calls.Add($"create:{title}:{privacy}");
            if (FailPlaylistCreate) throw new ContentYoutubeException("Insufficient Permission", 403, reason: "insufficientPermissions");
            return Task.FromResult($"PL{++_next}");
        }

        public Task AddToPlaylistAsync(string accessToken, string playlistId, string videoId, CancellationToken ct)
        {
            Calls.Add($"add:{playlistId}:{videoId}");
            if (MissingPlaylists.Contains(playlistId))
                throw new ContentYoutubeException("Playlist not found", 404, reason: "playlistNotFound");
            return Task.CompletedTask;
        }

        public Task InsertCaptionAsync(string accessToken, string videoId, string language, string name, string filePath, CancellationToken ct)
        {
            Calls.Add($"caption:{language}");
            if (FailCaptions) throw new ContentYoutubeException("Forbidden", 403);
            return Task.CompletedTask;
        }

        public Task SetThumbnailAsync(string accessToken, string videoId, string filePath, string contentType, CancellationToken ct)
        {
            Calls.Add("thumbnail");
            if (FailThumbnail) throw new ContentYoutubeException("The user does not have permission to upload custom thumbnails", 403);
            return Task.CompletedTask;
        }
    }

    static ContentYoutubeExtrasRequest Request(
        ContentYoutubeSeries? series = null,
        IReadOnlyList<ContentYoutubeCaptionFile>? captions = null,
        ContentYoutubeThumbnail? thumbnail = null) =>
        new("access", "vid123", ChannelId, series, captions ?? [], thumbnail);

    static Task<ContentYoutubeExtrasResult> Run(FakeApi api, FakeStore store, ContentYoutubeExtrasRequest request) =>
        ContentYoutubeExtras.RunAsync(api, store, request, NullLogger.Instance, CancellationToken.None);

    [Fact]
    public async Task Series_without_playlist_creates_a_public_one_and_saves_it()
    {
        var api = new FakeApi();
        var store = new FakeStore();
        var result = await Run(api, store, Request(new ContentYoutubeSeries(SeriesId, "Lộ trình giữ vốn")));

        Assert.Equal(new[] { "create:Lộ trình giữ vốn:public", "add:PL1:vid123" }, api.Calls);
        Assert.Equal("PL1", store.Rows[(SeriesId, ChannelId)]);
        Assert.Equal("PL1", result.PlaylistId);
        Assert.True(result.PlaylistCreated);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Saved_playlist_is_reused()
    {
        var api = new FakeApi();
        var store = new FakeStore();
        store.Rows[(SeriesId, ChannelId)] = "PLsaved";
        var result = await Run(api, store, Request(new ContentYoutubeSeries(SeriesId, "Lộ trình giữ vốn")));

        Assert.Equal(new[] { "add:PLsaved:vid123" }, api.Calls);
        Assert.False(result.PlaylistCreated);
        Assert.Equal("PLsaved", result.PlaylistId);
    }

    [Fact]
    public async Task Playlist_deleted_on_youtube_is_recreated_once()
    {
        var api = new FakeApi { MissingPlaylists = ["PLgone"] };
        var store = new FakeStore();
        store.Rows[(SeriesId, ChannelId)] = "PLgone";
        var result = await Run(api, store, Request(new ContentYoutubeSeries(SeriesId, "Lộ trình giữ vốn")));

        Assert.Equal(new[] { "add:PLgone:vid123", "create:Lộ trình giữ vốn:public", "add:PL1:vid123" }, api.Calls);
        Assert.Equal("PL1", store.Rows[(SeriesId, ChannelId)]);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task No_series_means_no_playlist_call()
    {
        var api = new FakeApi();
        var result = await Run(api, new FakeStore(), Request());
        Assert.Empty(api.Calls);
        Assert.Null(result.PlaylistId);
    }

    [Fact]
    public async Task Failing_steps_become_warnings_and_later_steps_still_run()
    {
        var thumb = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(thumb, new byte[1024]);
            var api = new FakeApi { FailPlaylistCreate = true, FailCaptions = true, FailThumbnail = true };
            var result = await Run(api, new FakeStore(), Request(
                new ContentYoutubeSeries(SeriesId, "Lộ trình giữ vốn"),
                [new("vi", "a.vi.srt"), new("en", "a.en.srt")],
                new ContentYoutubeThumbnail(thumb, "image/jpeg")));

            Assert.Equal(new[] { "create:Lộ trình giữ vốn:public", "caption:vi", "caption:en", "thumbnail" }, api.Calls);
            Assert.Equal(4, result.Warnings.Count);
            Assert.StartsWith("Playlist:", result.Warnings[0]);
            Assert.Null(result.PlaylistId);
            Assert.Empty(result.Captions);
            Assert.False(result.Thumbnail);
        }
        finally
        {
            File.Delete(thumb);
        }
    }

    [Fact]
    public async Task Store_failure_after_playlist_creation_is_a_warning()
    {
        var api = new FakeApi();
        var result = await Run(api, new FakeStore { FailSave = true }, Request(new ContentYoutubeSeries(SeriesId, "S")));
        Assert.Single(result.Warnings);
        Assert.Contains("db down", result.Warnings[0]);
    }

    [Fact]
    public async Task Oversized_thumbnail_is_skipped_with_warning()
    {
        var thumb = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(thumb, new byte[ContentYoutubeExtras.ThumbnailMaxBytes + 1]);
            var api = new FakeApi();
            var result = await Run(api, new FakeStore(), Request(thumbnail: new ContentYoutubeThumbnail(thumb, "image/png")));
            Assert.Empty(api.Calls);
            Assert.Single(result.Warnings);
        }
        finally
        {
            File.Delete(thumb);
        }
    }

    [Fact]
    public async Task Successful_captions_and_thumbnail_are_reported()
    {
        var thumb = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(thumb, new byte[10]);
            var result = await Run(new FakeApi(), new FakeStore(), Request(
                captions: [new("vi", "a.vi.srt")], thumbnail: new ContentYoutubeThumbnail(thumb, "image/jpeg")));
            Assert.Equal(new[] { "vi" }, result.Captions);
            Assert.True(result.Thumbnail);
            Assert.Empty(result.Warnings);
        }
        finally
        {
            File.Delete(thumb);
        }
    }
}
