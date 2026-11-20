using System.Text.Json;
using KitPlatform.Packs.Content.Infrastructure;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class NovixaVideoProof001Tests
{
    [Fact]
    public async Task Muxes_prepared_proof_frames_through_the_local_renderer()
    {
        var dir = Environment.GetEnvironmentVariable("NOVIXA_VIDEO_PROOF_DIR");
        if (string.IsNullOrWhiteSpace(dir))
            return;

        var job = Path.GetFullPath(dir);
        var storyPath = Path.Combine(job, "storyboard.json");
        var voice = Path.Combine(job, "voice.mp3");
        Assert.True(File.Exists(storyPath), "Thiếu storyboard.json");
        Assert.True(File.Exists(voice), "Thiếu voice.mp3");

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(storyPath));
        var root = document.RootElement;
        var fps = root.GetProperty("fps").GetInt32();
        var duration = root.GetProperty("durationSec").GetInt32();
        var scenes = new List<MarketingLocalVideoScene>();
        foreach (var scene in root.GetProperty("scenes").EnumerateArray())
        {
            scenes.Add(new MarketingLocalVideoScene
            {
                Order = scenes.Count + 1,
                Beat = scene.GetProperty("beat").GetString() ?? "",
                Text = scene.GetProperty("text").GetString() ?? "",
                StartSec = scene.GetProperty("start").GetInt32(),
                EndSec = scene.GetProperty("end").GetInt32(),
                ImagePath = voice,
            });
        }

        var frames = Directory.GetFiles(Path.Combine(job, "frames"), "f*.png").OrderBy(path => path, StringComparer.Ordinal).ToList();
        Assert.Equal(fps * duration, frames.Count);

        var renderer = new MarketingLocalVideoRenderer(new ProcessMarketingVideoFfmpeg());
        var result = await renderer.RenderAsync(new MarketingLocalVideoRenderRequest
        {
            JobDirectory = job,
            AspectRatio = "9:16",
            VoicePath = voice,
            FrameDurationSec = 1d / fps,
            FrameSequence = frames,
            Scenes = scenes,
        });

        Assert.True(result.Ok, result.Error);
        var probe = await NovixaLocalRenderGate.ProbeMp4Async(result.OutputPath!, "9:16", duration, CancellationToken.None);
        Assert.True(probe.Ok, probe.Error);
        Assert.True(probe.DurationSec + 0.05 >= duration);
    }
}
