using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Infrastructure;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class NovixaLocalRenderIntegrationTests
{
    [Fact]
    public async Task Complete_input_calls_the_renderer_and_becomes_ready_only_after_decode()
    {
        using var job = NewJob();
        CopyFixtureInputs(job.Directory);
        var calls = new CountingProcess(new ProcessMarketingVideoFfmpeg());
        var renderer = new MarketingLocalVideoRenderer(calls);
        var id = Guid.NewGuid();
        var route = ContentVideoMediaPaths.OutputRoute(id);

        var result = await NovixaLocalRenderGate.TryRenderAsync(
            renderer,
            job.Directory,
            "9:16",
            Scenes(job.Directory, 3),
            Voice(job.Directory),
            id,
            route,
            CancellationToken.None);

        Assert.True(result.Ready, result.Error);
        Assert.Equal("Ready", result.Status);
        Assert.Equal(route, result.OutputUrl);
        Assert.Null(result.Error);
        Assert.Equal(1, calls.Calls);
        Assert.True(File.Exists(Output(job.Directory)));
        var probe = await NovixaLocalRenderGate.ProbeMp4Async(Output(job.Directory), "9:16", 15, CancellationToken.None);
        Assert.True(probe.Ok, probe.Error);
        Assert.True(probe.DurationSec + NovixaLocalRenderGate.FrameSeconds + 0.001 >= 15);
    }

    [Fact]
    public async Task Missing_image_fails_before_the_renderer_and_names_the_beat()
    {
        using var job = NewJob();
        CopyFixtureInputs(job.Directory);
        File.Delete(Image(job.Directory, 3));
        var calls = new CountingProcess(new ProcessMarketingVideoFfmpeg());

        var result = await Render(job.Directory, calls, Scenes(job.Directory, 3));

        Assert.False(result.Ready);
        Assert.Equal("Failed", result.Status);
        Assert.Null(result.OutputUrl);
        Assert.Contains("PROOF", result.Error, StringComparison.Ordinal);
        Assert.Equal(0, calls.Calls);
    }

    [Fact]
    public async Task Missing_empty_or_short_voice_fails_before_the_renderer()
    {
        using var missing = NewJob();
        CopyFixtureInputs(missing.Directory);
        File.Delete(Voice(missing.Directory));
        var missingCalls = new CountingProcess(new ProcessMarketingVideoFfmpeg());
        var missingResult = await Render(missing.Directory, missingCalls, Scenes(missing.Directory, 3));
        Assert.False(missingResult.Ready);
        Assert.Null(missingResult.OutputUrl);
        Assert.Contains("voice", missingResult.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, missingCalls.Calls);

        using var empty = NewJob();
        CopyFixtureInputs(empty.Directory);
        File.WriteAllBytes(Voice(empty.Directory), []);
        var emptyCalls = new CountingProcess(new ProcessMarketingVideoFfmpeg());
        var emptyResult = await Render(empty.Directory, emptyCalls, Scenes(empty.Directory, 3));
        Assert.False(emptyResult.Ready);
        Assert.Null(emptyResult.OutputUrl);
        Assert.Contains("voice", emptyResult.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, emptyCalls.Calls);

        using var shortVoice = NewJob();
        CopyFixtureInputs(shortVoice.Directory);
        var shortCalls = new CountingProcess(new ProcessMarketingVideoFfmpeg());
        var shortResult = await Render(shortVoice.Directory, shortCalls, Scenes(shortVoice.Directory, 5));
        Assert.False(shortResult.Ready);
        Assert.Null(shortResult.OutputUrl);
        Assert.Contains("voice", shortResult.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ngắn hơn", shortResult.Error, StringComparison.Ordinal);
        Assert.Equal(0, shortCalls.Calls);
    }

    [Fact]
    public async Task Ffmpeg_failure_keeps_the_stderr_and_does_not_leave_a_blocking_output()
    {
        using var job = NewJob();
        CopyFixtureInputs(job.Directory);
        var blocking = new byte[2048];
        Array.Fill(blocking, (byte)'x');
        File.WriteAllBytes(Output(job.Directory), blocking);
        var fake = new FakeFfmpeg { ExitCode = 1, Stderr = "encoder failed" };

        var result = await Render(job.Directory, fake, Scenes(job.Directory, 3));

        Assert.False(result.Ready);
        Assert.Equal("Failed", result.Status);
        Assert.Null(result.OutputUrl);
        Assert.Contains("encoder failed", result.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(Output(job.Directory)));
        Assert.True(File.Exists(Previous(job.Directory)));
        Assert.Equal(1, fake.Calls);

        var missing = new FakeFfmpeg { ExitCode = -1, Stderr = "Không tìm thấy ffmpeg trên PATH." };
        var again = await Render(job.Directory, missing, Scenes(job.Directory, 3));
        Assert.False(again.Ready);
        Assert.Null(again.OutputUrl);
        Assert.Contains("Không tìm thấy ffmpeg", again.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(Output(job.Directory)));
    }

    [Fact]
    public async Task Ftyp_without_decodable_streams_is_failed_and_not_published()
    {
        using var job = NewJob();
        CopyFixtureInputs(job.Directory);
        var fake = new FakeFfmpeg { ExitCode = 0, WriteFtyp = true };

        var result = await Render(job.Directory, fake, Scenes(job.Directory, 3));

        Assert.False(result.Ready);
        Assert.Equal("Failed", result.Status);
        Assert.Null(result.OutputUrl);
        Assert.Contains("không giải mã", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Output(job.Directory)));
        Assert.Equal(1, fake.Calls);
    }

    [Fact]
    public async Task Retry_after_failure_renders_again_and_does_not_publish_the_previous_file()
    {
        using var job = NewJob();
        CopyFixtureInputs(job.Directory);
        var oldMp4 = FixtureFile("job", "output.mp4");
        File.Copy(oldMp4, Output(job.Directory));
        var oldLength = new FileInfo(oldMp4).Length;
        var failed = new FakeFfmpeg { ExitCode = 1, Stderr = "encoder failed" };

        var first = await Render(job.Directory, failed, Scenes(job.Directory, 3));

        Assert.False(first.Ready);
        Assert.Null(first.OutputUrl);
        Assert.False(File.Exists(Output(job.Directory)));
        Assert.Equal(oldLength, new FileInfo(Previous(job.Directory)).Length);
        Assert.Equal(1, failed.Calls);

        var renderer = new MarketingLocalVideoRenderer(new ProcessMarketingVideoFfmpeg());
        var id = Guid.NewGuid();
        var route = ContentVideoMediaPaths.OutputRoute(id);
        var second = await NovixaLocalRenderGate.TryRenderAsync(
            renderer,
            job.Directory,
            "9:16",
            Scenes(job.Directory, 3),
            Voice(job.Directory),
            id,
            route,
            CancellationToken.None);

        Assert.True(second.Ready, second.Error);
        Assert.Equal(route, second.OutputUrl);
        Assert.False(second.OutputUrl!.Contains("previous", StringComparison.OrdinalIgnoreCase));
        Assert.False(File.Exists(Previous(job.Directory)));
        var probe = await NovixaLocalRenderGate.ProbeMp4Async(Output(job.Directory), "9:16", 15, CancellationToken.None);
        Assert.True(probe.Ok, probe.Error);

        File.Copy(oldMp4, Output(job.Directory), overwrite: true);
        var failedAgain = new FakeFfmpeg { ExitCode = 1, Stderr = "encoder failed" };
        var third = await Render(job.Directory, failedAgain, Scenes(job.Directory, 3), id);
        Assert.False(third.Ready);
        Assert.Null(third.OutputUrl);
        Assert.True(File.Exists(Previous(job.Directory)));
        Assert.False(File.Exists(Output(job.Directory)));
    }

    [Fact]
    public void Outside_the_gate_keeps_storyboard_ready_and_does_not_use_the_local_renderer()
    {
        Assert.True(NovixaLocalRenderGate.IsGate("novixa", "tiktok_45s_hooks", "storyboard_local"));
        Assert.False(NovixaLocalRenderGate.IsGate("famixa", "tiktok_45s_hooks", "storyboard_local"));
        Assert.False(NovixaLocalRenderGate.IsGate("NOVIXA", "reel_square_promo", "storyboard_local"));
        Assert.False(NovixaLocalRenderGate.IsGate("novixa", "tiktok_45s_hooks", "creatomate"));
        Assert.Equal("PreparingRender", NovixaLocalRenderGate.StatusAfterStoryboard(true));
        Assert.Equal("Ready", NovixaLocalRenderGate.StatusAfterStoryboard(false));
        Assert.Equal("Ready", ContentVideoRenderStatus.FromProvider("succeeded", "https://cdn.example/out.mp4"));
        Assert.Equal("Failed", ContentVideoRenderStatus.FromProvider("succeeded", "https://cdn.example/still.jpg"));
    }

    [Fact]
    public void Media_route_and_renderer_directory_are_the_same_path()
    {
        var root = Path.Combine(Path.GetTempPath(), "kit-novixa-media-" + Guid.NewGuid().ToString("N"));
        var jobId = Guid.Parse("22222222-2222-4222-8222-222222222222");
        try
        {
            var configured = ContentVideoMediaPaths.ResolveRoot(root, Path.GetTempPath());
            var jobDir = ContentVideoMediaPaths.JobDirectory(configured, jobId);
            var output = ContentVideoMediaPaths.File(configured, jobId, ContentVideoMediaPaths.OutputFileName);
            Directory.CreateDirectory(jobDir);
            File.WriteAllBytes(output, [1, 2, 3, 4]);

            Assert.Equal(MarketingLocalVideoRenderer.OutputFileName, ContentVideoMediaPaths.OutputFileName);
            Assert.Equal(Path.Combine(jobDir, ContentVideoMediaPaths.OutputFileName), output);
            Assert.StartsWith(Path.GetFullPath(root), output, StringComparison.OrdinalIgnoreCase);
            var route = ContentVideoMediaPaths.OutputRoute(jobId);
            Assert.EndsWith("/api/content/video/media/" + jobId.ToString("D") + "/output.mp4", route, StringComparison.OrdinalIgnoreCase);
            Assert.True(ContentVideoMediaPaths.IsJobOutputRoute(jobId, route));
            Assert.False(ContentVideoMediaPaths.IsJobOutputRoute(Guid.NewGuid(), route));
            Assert.False(ContentVideoMediaPaths.IsJobOutputRoute(jobId, "https://cdn.example/still.jpg"));

            var relativeRoot = ContentVideoMediaPaths.ResolveRoot("App_Data/content-video", root);
            Assert.Equal(Path.Combine(Path.GetFullPath(root), "App_Data", "content-video"), relativeRoot);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* temp */ }
        }
    }

    static async Task<NovixaLocalRenderGate.AttemptResult> Render(
        string directory,
        IMarketingVideoFfmpegProcess process,
        IReadOnlyList<NovixaLocalRenderGate.SceneInput> scenes,
        Guid? jobId = null)
    {
        var id = jobId ?? Guid.NewGuid();
        return await NovixaLocalRenderGate.TryRenderAsync(
            new MarketingLocalVideoRenderer(process),
            directory,
            "9:16",
            scenes,
            Voice(directory),
            id,
            ContentVideoMediaPaths.OutputRoute(id),
            CancellationToken.None);
    }

    static List<NovixaLocalRenderGate.SceneInput> Scenes(string directory, int secondsEach)
    {
        string[] beats = ["HOOK", "PROBLEM", "PROOF", "OFFER", "CTA"];
        var scenes = new List<NovixaLocalRenderGate.SceneInput>(beats.Length);
        for (var i = 0; i < beats.Length; i++)
        {
            scenes.Add(new NovixaLocalRenderGate.SceneInput(
                beats[i],
                "Lời scene " + beats[i] + " cho fixture local.",
                i * secondsEach,
                (i + 1) * secondsEach,
                Image(directory, i + 1)));
        }

        return scenes;
    }

    static void CopyFixtureInputs(string directory)
    {
        var inputs = FixtureRoot("inputs");
        for (var i = 1; i <= 5; i++)
            File.Copy(Path.Combine(inputs, "scene-" + i.ToString("00") + ".png"), Image(directory, i));
        File.Copy(Path.Combine(inputs, "voice.mp3"), Voice(directory));
    }

    static string FixtureFile(string folder, string name) => Path.Combine(FixtureRoot(folder), name);

    static string FixtureRoot(string folder)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "fixtures", "novixa-local-render-003", folder);
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Không thấy fixture novixa-local-render-003.");
    }

    static string Image(string directory, int order) =>
        Path.Combine(directory, "scene-" + order.ToString("00") + "-source.bin");

    static string Voice(string directory) => Path.Combine(directory, "voice.mp3");
    static string Output(string directory) => Path.Combine(directory, "output.mp4");
    static string Previous(string directory) => Path.Combine(directory, "output.previous.mp4");

    static TempJob NewJob()
    {
        var directory = Path.Combine(Path.GetTempPath(), "kit-novixa-render-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return new TempJob(directory);
    }

    sealed class TempJob : IDisposable
    {
        public TempJob(string directory) => Directory = directory;
        public string Directory { get; }
        public void Dispose()
        {
            try { System.IO.Directory.Delete(Directory, recursive: true); } catch { /* temp */ }
        }
    }

    sealed class CountingProcess : IMarketingVideoFfmpegProcess
    {
        readonly IMarketingVideoFfmpegProcess _inner;
        public CountingProcess(IMarketingVideoFfmpegProcess inner) => _inner = inner;
        public int Calls { get; private set; }

        public Task<MarketingVideoProcessResult> RunAsync(
            IReadOnlyList<string> argumentList,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            Calls++;
            return _inner.RunAsync(argumentList, timeout, cancellationToken);
        }
    }

    sealed class FakeFfmpeg : IMarketingVideoFfmpegProcess
    {
        public int ExitCode { get; init; }
        public string Stderr { get; init; } = "";
        public bool WriteFtyp { get; init; }
        public int Calls { get; private set; }

        public Task<MarketingVideoProcessResult> RunAsync(
            IReadOnlyList<string> argumentList,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            Calls++;
            if (WriteFtyp)
            {
                var bytes = new byte[2048];
                bytes[4] = (byte)'f';
                bytes[5] = (byte)'t';
                bytes[6] = (byte)'y';
                bytes[7] = (byte)'p';
                File.WriteAllBytes(argumentList[^1], bytes);
            }

            return Task.FromResult(new MarketingVideoProcessResult(ExitCode, Stderr, false));
        }
    }
}
