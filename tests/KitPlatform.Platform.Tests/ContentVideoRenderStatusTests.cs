using KitPlatform.Packs.Content;
using Xunit;

namespace KitPlatform.Platform.Tests;

public sealed class ContentVideoRenderStatusTests
{
    [Fact]
    public void Succeeded_with_mp4_is_ready()
    {
        Assert.Equal("Ready", ContentVideoRenderStatus.FromProvider("succeeded", "https://cdn.example/out.mp4"));
    }

    [Fact]
    public void Succeeded_without_file_is_failed()
    {
        Assert.Equal("Failed", ContentVideoRenderStatus.FromProvider("succeeded", null));
        Assert.Equal("Failed", ContentVideoRenderStatus.FromProvider("completed", "https://cdn.example/still.jpg"));
    }

    [Fact]
    public void Quota_and_keyframe_are_failed_not_rendering()
    {
        Assert.Equal("Failed", ContentVideoRenderStatus.FromProvider("quota_exceeded", null));
        Assert.Equal("Failed", ContentVideoRenderStatus.FromProvider("keyframe_rejected", "https://cdn.example/out.mp4"));
        Assert.Equal("Failed", ContentVideoRenderStatus.FromProvider("RESOURCE_EXHAUSTED", null));
    }

    [Fact]
    public void Known_in_progress_stays_open()
    {
        Assert.Equal("Rendering", ContentVideoRenderStatus.FromProvider("rendering", null));
        Assert.Equal("Queued", ContentVideoRenderStatus.FromProvider("queued", null));
    }

    [Fact]
    public void Unknown_status_is_failed()
    {
        Assert.Equal("Failed", ContentVideoRenderStatus.FromProvider("mystery", null));
    }
}
