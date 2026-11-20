using KitPlatform.Packs.Content.Infrastructure;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class MarketingLocalVideoRendererTests
{
    const string SceneText = "FEFO-SENTINEL-KHONG-VAO-LENH";

    [Fact]
    public async Task Success_writes_playable_output_without_putting_scene_text_on_the_process_command()
    {
        var dir = NewJobDir();
        try
        {
            var fake = new FakeProcess { ExitCode = 0, WriteOutput = true };
            var renderer = new MarketingLocalVideoRenderer(fake);
            var result = await renderer.RenderAsync(Sample(dir));

            Assert.True(result.Ok, result.Error);
            Assert.True(MarketingLocalVideoRenderer.IsPlayableFile(result.OutputPath!));
            Assert.Equal(Path.Combine(dir, "output.mp4"), result.OutputPath);
            Assert.Equal(1, fake.Calls);
            Assert.NotNull(fake.Args);
            Assert.DoesNotContain(fake.Args, arg => arg.Contains(SceneText, StringComparison.Ordinal));
            Assert.Equal("-hide_banner", fake.Args[0]);
            Assert.EndsWith("output.partial.mp4", fake.Args[^1], StringComparison.OrdinalIgnoreCase);
            Assert.Contains(fake.Args, arg => arg.EndsWith("voice.mp3", StringComparison.OrdinalIgnoreCase));
            Assert.False(File.Exists(Path.Combine(dir, "output.partial.mp4")));
        }
        finally
        {
            TryDelete(dir);
        }
    }

    [Fact]
    public async Task Missing_output_is_failed_when_the_process_exits_cleanly()
    {
        var dir = NewJobDir();
        try
        {
            var fake = new FakeProcess { ExitCode = 0, WriteOutput = false };
            var renderer = new MarketingLocalVideoRenderer(fake);
            var result = await renderer.RenderAsync(Sample(dir));

            Assert.False(result.Ok);
            Assert.Null(result.OutputPath);
            Assert.Contains("không có file MP4", result.Error, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(dir, "output.mp4")));
            Assert.Equal(1, fake.Calls);
        }
        finally
        {
            TryDelete(dir);
        }
    }

    [Fact]
    public async Task Process_failure_is_failed_and_keeps_the_stderr_detail()
    {
        var dir = NewJobDir();
        try
        {
            var fake = new FakeProcess { ExitCode = 1, Stderr = "encoder failed", WriteOutput = false };
            var renderer = new MarketingLocalVideoRenderer(fake);
            var result = await renderer.RenderAsync(Sample(dir));

            Assert.False(result.Ok);
            Assert.Null(result.OutputPath);
            Assert.Contains("encoder failed", result.Error, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(dir, "output.mp4")));
            Assert.False(MarketingLocalVideoRenderer.IsPlayableFile(Path.Combine(dir, "output.mp4")));
        }
        finally
        {
            TryDelete(dir);
        }
    }

    [Fact]
    public async Task Existing_valid_output_is_kept_and_ffmpeg_is_not_called()
    {
        var dir = NewJobDir();
        try
        {
            var output = Path.Combine(dir, "output.mp4");
            File.WriteAllBytes(output, PlayableBytes());
            var fake = new FakeProcess { ExitCode = 0, WriteOutput = true };
            var renderer = new MarketingLocalVideoRenderer(fake);
            var result = await renderer.RenderAsync(Sample(dir));

            Assert.True(result.Ok, result.Error);
            Assert.Equal(output, result.OutputPath);
            Assert.Equal(0, fake.Calls);
            Assert.Equal(PlayableBytes(), File.ReadAllBytes(output));
        }
        finally
        {
            TryDelete(dir);
        }
    }

    [Fact]
    public async Task Existing_invalid_output_fails_without_overwrite()
    {
        var dir = NewJobDir();
        try
        {
            var output = Path.Combine(dir, "output.mp4");
            var original = new byte[1024];
            Array.Fill(original, (byte)'x');
            File.WriteAllBytes(output, original);
            var fake = new FakeProcess { ExitCode = 0, WriteOutput = true };
            var renderer = new MarketingLocalVideoRenderer(fake);
            var result = await renderer.RenderAsync(Sample(dir));

            Assert.False(result.Ok);
            Assert.Contains("không ghi đè", result.Error, StringComparison.Ordinal);
            Assert.Equal(0, fake.Calls);
            Assert.Equal(original, File.ReadAllBytes(output));
            Assert.False(MarketingLocalVideoRenderer.IsPlayableFile(output));
        }
        finally
        {
            TryDelete(dir);
        }
    }

    [Fact]
    public async Task Frame_sequence_muxes_prepared_frames_and_keeps_scene_text_off_the_command()
    {
        var dir = NewJobDir();
        try
        {
            var png = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
            var first = Path.Combine(dir, "motion-01.png");
            var second = Path.Combine(dir, "motion-02.png");
            File.WriteAllBytes(first, png);
            File.WriteAllBytes(second, png);
            var voice = Path.Combine(dir, "voice.mp3");
            File.WriteAllBytes(voice, [1, 2, 3, 4]);
            var fake = new FakeProcess { ExitCode = 0, WriteOutput = true };
            var renderer = new MarketingLocalVideoRenderer(fake);
            var result = await renderer.RenderAsync(new MarketingLocalVideoRenderRequest
            {
                JobDirectory = dir,
                AspectRatio = "9:16",
                VoicePath = voice,
                FrameDurationSec = 1,
                FrameSequence = [first, second],
                Scenes =
                [
                    new MarketingLocalVideoScene
                    {
                        Order = 1,
                        Beat = "HOOK",
                        Text = SceneText,
                        StartSec = 0,
                        EndSec = 2,
                        ImagePath = Path.Combine(dir, "missing-still.png"),
                    },
                ],
            });

            Assert.True(result.Ok, result.Error);
            Assert.Equal(1, fake.Calls);
            Assert.NotNull(fake.Args);
            Assert.DoesNotContain(fake.Args, arg => arg.Contains(SceneText, StringComparison.Ordinal));
            Assert.Contains(fake.Args, arg => arg == "veryfast");
            var concat = await File.ReadAllTextAsync(Path.Combine(dir, "frames.txt"));
            Assert.Contains("duration 1.000000", concat, StringComparison.Ordinal);
            Assert.DoesNotContain(SceneText, concat, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(dir);
        }
    }

    static MarketingLocalVideoRenderRequest Sample(string dir)
    {
        var image = Path.Combine(dir, "scene.png");
        File.WriteAllBytes(image, Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="));
        var voice = Path.Combine(dir, "voice.mp3");
        File.WriteAllBytes(voice, [1, 2, 3, 4]);
        return new MarketingLocalVideoRenderRequest
        {
            JobDirectory = dir,
            AspectRatio = "9:16",
            VoicePath = voice,
            Scenes =
            [
                new MarketingLocalVideoScene
                {
                    Order = 1,
                    Beat = "HOOK",
                    Text = SceneText,
                    StartSec = 0,
                    EndSec = 9,
                    ImagePath = image,
                },
            ],
        };
    }

    static string NewJobDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "kit-mkt-render-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    static void TryDelete(string dir)
    {
        try { Directory.Delete(dir, recursive: true); } catch { /* temp dir only */ }
    }

    static byte[] PlayableBytes()
    {
        var bytes = new byte[1024];
        bytes[4] = (byte)'f';
        bytes[5] = (byte)'t';
        bytes[6] = (byte)'y';
        bytes[7] = (byte)'p';
        return bytes;
    }

    sealed class FakeProcess : IMarketingVideoFfmpegProcess
    {
        public int ExitCode { get; init; }
        public string Stderr { get; init; } = "";
        public bool WriteOutput { get; init; }
        public int Calls { get; private set; }
        public List<string>? Args { get; private set; }

        public Task<MarketingVideoProcessResult> RunAsync(
            IReadOnlyList<string> argumentList,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            Calls++;
            Args = argumentList.ToList();
            Assert.True(timeout > TimeSpan.Zero);
            if (WriteOutput)
                File.WriteAllBytes(argumentList[^1], PlayableBytes());
            return Task.FromResult(new MarketingVideoProcessResult(ExitCode, Stderr, false));
        }
    }
}
