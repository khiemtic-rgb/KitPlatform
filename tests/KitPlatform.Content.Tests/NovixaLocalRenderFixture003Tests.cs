using System.Text.Json;
using KitPlatform.Packs.Content.Infrastructure;
using Xunit;

namespace KitPlatform.Content.Tests;

/// <summary>
/// Local FFmpeg smoke for <see cref="MarketingLocalVideoRenderer"/>.
/// Armed only when NOVIXA_LOCAL_RENDER_FIXTURE_003=1. No database and no external API.
/// </summary>
public sealed class NovixaLocalRenderFixture003Tests
{
    [Fact]
    public async Task Local_fixture_renders_mp4_from_files_inside_the_job_directory()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("NOVIXA_LOCAL_RENDER_FIXTURE_003"), "1", StringComparison.Ordinal))
            return;

        var root = FindFixtureRoot();
        var storyboard = JsonSerializer.Deserialize<FixtureStoryboard>(
            await File.ReadAllTextAsync(Path.Combine(root, "storyboard.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Storyboard rỗng.");

        Assert.Equal("9:16", storyboard.AspectRatio);
        Assert.Equal("voice.mp3", storyboard.VoiceFile);
        Assert.Equal(5, storyboard.Scenes.Count);

        var expectedStart = 0;
        for (var i = 0; i < storyboard.Scenes.Count; i++)
        {
            var scene = storyboard.Scenes[i];
            Assert.Equal(i + 1, scene.Order);
            Assert.False(string.IsNullOrWhiteSpace(scene.Beat));
            Assert.False(string.IsNullOrWhiteSpace(scene.Text));
            Assert.Equal(expectedStart, scene.StartSec);
            Assert.True(scene.EndSec > scene.StartSec);
            Assert.False(scene.ImageFile.Contains("://", StringComparison.Ordinal));
            Assert.True(File.Exists(Path.Combine(root, "inputs", scene.ImageFile)));
            expectedStart = scene.EndSec;
        }

        Assert.Equal(15, expectedStart);
        var voiceSource = Path.Combine(root, "inputs", storyboard.VoiceFile);
        Assert.True(File.Exists(voiceSource));
        Assert.True(new FileInfo(voiceSource).Length > 0);

        var jobDir = Path.Combine(root, "job");
        if (Directory.Exists(jobDir))
            throw new InvalidOperationException("Thư mục job đã có. Harness không ghi đè: " + jobDir);
        Directory.CreateDirectory(jobDir);

        var voicePath = Path.Combine(jobDir, storyboard.VoiceFile);
        File.Copy(voiceSource, voicePath, overwrite: false);
        var scenes = new List<MarketingLocalVideoScene>(storyboard.Scenes.Count);
        foreach (var scene in storyboard.Scenes)
        {
            var imagePath = Path.Combine(jobDir, scene.ImageFile);
            File.Copy(Path.Combine(root, "inputs", scene.ImageFile), imagePath, overwrite: false);
            scenes.Add(new MarketingLocalVideoScene
            {
                Order = scene.Order,
                Beat = scene.Beat,
                Text = scene.Text,
                StartSec = scene.StartSec,
                EndSec = scene.EndSec,
                ImagePath = imagePath,
            });
        }

        var renderer = new MarketingLocalVideoRenderer(new ProcessMarketingVideoFfmpeg());
        var result = await renderer.RenderAsync(new MarketingLocalVideoRenderRequest
        {
            JobDirectory = jobDir,
            AspectRatio = storyboard.AspectRatio,
            VoicePath = voicePath,
            Scenes = scenes,
        });

        Assert.True(result.Ok, result.Error);
        Assert.Equal(Path.Combine(jobDir, MarketingLocalVideoRenderer.OutputFileName), result.OutputPath);
        Assert.True(MarketingLocalVideoRenderer.IsPlayableFile(result.OutputPath!));
    }

    static string FindFixtureRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "fixtures", "novixa-local-render-003", "storyboard.json");
            if (File.Exists(candidate))
                return Path.GetDirectoryName(candidate)!;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Không thấy fixture novixa-local-render-003.");
    }

    sealed class FixtureStoryboard
    {
        public string AspectRatio { get; set; } = "";
        public string VoiceFile { get; set; } = "";
        public List<FixtureScene> Scenes { get; set; } = [];
    }

    sealed class FixtureScene
    {
        public int Order { get; set; }
        public string Beat { get; set; } = "";
        public string Text { get; set; } = "";
        public int StartSec { get; set; }
        public int EndSec { get; set; }
        public string ImageFile { get; set; } = "";
    }
}
