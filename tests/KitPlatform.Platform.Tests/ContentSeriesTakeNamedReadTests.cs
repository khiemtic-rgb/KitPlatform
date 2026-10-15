using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Infrastructure;
using Xunit;

namespace KitPlatform.Platform.Tests;

public sealed class ContentSeriesTakeNamedReadTests
{
    [Fact]
    public void ReadVeoTake_returns_bytes_for_a_named_file_inside_the_video_store()
    {
        using var fixture = new StoreFixture();
        var taskId = "veo_task_12345678";
        var fileId = FamixaVeoTakePlaybackV1.FileId(taskId);
        var payload = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        fixture.Write("veo", fileId, payload);

        var bytes = fixture.Proxy.ReadVeoTake(taskId);

        Assert.Equal(payload, bytes);
    }

    [Fact]
    public void ReadFalTake_returns_bytes_for_a_named_file_inside_the_video_store()
    {
        using var fixture = new StoreFixture();
        var taskId = "lipsync_task_12345678";
        var fileId = FamixaFalLipsyncPlaybackV1.FileId(taskId);
        var payload = new byte[] { 16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 };
        fixture.Write("fal", fileId, payload);

        var bytes = fixture.Proxy.ReadFalTake(
            "https://api.novixa.vn/api/content/series/fal-video?taskId=" + Uri.EscapeDataString(taskId));

        Assert.Equal(payload, bytes);
    }

    [Fact]
    public void Readers_return_no_result_for_paths_outside_the_video_store()
    {
        using var fixture = new StoreFixture();
        var sentinel = new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 };
        var outside = Path.Combine(fixture.Outside, "sentinel.mp4");
        File.WriteAllBytes(outside, sentinel);

        Assert.Null(fixture.Proxy.ReadVeoTake(outside));
        Assert.Null(fixture.Proxy.ReadVeoTake("../sentinel"));
        Assert.Null(fixture.Proxy.ReadVeoTake("..\\sentinel"));
        Assert.Null(fixture.Proxy.ReadVeoTake("/api/content/series/veo-takes/../../sentinel"));
        Assert.Null(fixture.Proxy.ReadFalTake(outside));
        Assert.Null(fixture.Proxy.ReadFalTake("https://v3b.fal.media/files/x.mp4"));
        Assert.Null(fixture.Proxy.ReadFalTake("/api/content/series/fal-video/../../sentinel"));
        Assert.Null(fixture.Store.TryReadNamed("veo", "../sentinel"));
        Assert.Null(fixture.Store.TryReadNamed("other", "abcdef12"));
        Assert.False(File.ReadAllBytes(outside).SequenceEqual(Array.Empty<byte>()));
    }

    private sealed class StoreFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "kpa-phase15-takes-" + Guid.NewGuid().ToString("N"));

        public StoreFixture()
        {
            Directory.CreateDirectory(_root);
            Outside = Path.Combine(Path.GetTempPath(), "kpa-phase15-outside-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Outside);
            var options = Options.Create(new ContentOptions { KitVideoVideoRoot = _root });
            Store = new KitVideoVideoStore(options, new FixedHost(Path.GetTempPath()));
            Proxy = new ContentSeriesTakeProxyService(new UnusedHttp(), Store);
        }

        public string Outside { get; }
        public KitVideoVideoStore Store { get; }
        public ContentSeriesTakeProxyService Proxy { get; }

        public void Write(string folder, string fileId, byte[] payload)
        {
            var dir = Path.Combine(_root, folder);
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, fileId + ".mp4"), payload);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
            if (Directory.Exists(Outside)) Directory.Delete(Outside, recursive: true);
        }
    }

    private sealed class FixedHost : IHostEnvironment
    {
        public FixedHost(string contentRoot) => ContentRootPath = contentRoot;
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "KitPlatform.Platform.Tests";
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class UnusedHttp : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            throw new InvalidOperationException("Named take reads do not open HTTP.");
    }
}
