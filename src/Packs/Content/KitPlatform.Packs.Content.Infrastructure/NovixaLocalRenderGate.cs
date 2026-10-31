using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using SkiaSharp;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// NOVIXA tiktok_45s_hooks status and file rules. No database and no external provider.
/// </summary>
internal static class NovixaLocalRenderGate
{
    public const double FrameSeconds = 1d / 30d;

    public static bool IsGate(string? brandCode, string? templateCode, string? templateProvider) =>
        string.Equals(brandCode, "novixa", StringComparison.OrdinalIgnoreCase)
        && string.Equals(templateCode, "tiktok_45s_hooks", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(templateProvider, "creatomate", StringComparison.OrdinalIgnoreCase);

    public static string StatusAfterStoryboard(bool isGate) =>
        isGate ? "PreparingRender" : "Ready";

    public static bool ImageDecodes(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;
        try
        {
            using var bitmap = SKBitmap.Decode(path);
            return bitmap is not null && bitmap.Width > 0 && bitmap.Height > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static async Task<AttemptResult> TryRenderAsync(
        MarketingLocalVideoRenderer renderer,
        string jobDirectory,
        string aspectRatio,
        IReadOnlyList<SceneInput> scenes,
        string? voicePath,
        Guid jobId,
        string outputRoute,
        CancellationToken cancellationToken)
    {
        if (renderer is null)
            return AttemptResult.Fail("Thiếu renderer.");
        if (scenes is null || scenes.Count == 0)
            return AttemptResult.Fail("Storyboard không có scene.");

        var total = 0;
        var prepared = new List<MarketingLocalVideoScene>(scenes.Count);
        foreach (var scene in scenes)
        {
            if (scene is null || string.IsNullOrWhiteSpace(scene.Text))
                return AttemptResult.Fail("Scene " + (scene?.Beat ?? "?") + " thiếu lời.");
            if (scene.EndSec <= scene.StartSec)
                return AttemptResult.Fail("Scene " + scene.Beat + " không có thời lượng.");
            if (!Inside(jobDirectory, scene.ImagePath) || !ImageDecodes(scene.ImagePath))
                return AttemptResult.Fail("Scene " + scene.Beat + " thiếu ảnh đã giải mã.");
            total += scene.EndSec - scene.StartSec;
            prepared.Add(new MarketingLocalVideoScene
            {
                Order = prepared.Count + 1,
                Beat = scene.Beat,
                Text = scene.Text.Trim(),
                StartSec = scene.StartSec,
                EndSec = scene.EndSec,
                ImagePath = Path.GetFullPath(scene.ImagePath!),
            });
        }

        if (total <= 0)
            return AttemptResult.Fail("Storyboard không có thời lượng.");

        var voice = await ProbeAudioCoversAsync(jobDirectory, voicePath, total, cancellationToken);
        if (!voice.Ok)
            return AttemptResult.Fail(voice.Error ?? "Thiếu voice.mp3.");

        if (!MarketingLocalVideoRenderer.TryFrameSize(aspectRatio, out var width, out var height))
            return AttemptResult.Fail("Tỷ lệ khung không hỗ trợ cho render local: " + (aspectRatio ?? ""));

        var outputPath = Path.Combine(jobDirectory, MarketingLocalVideoRenderer.OutputFileName);
        try
        {
            RetireExistingOutput(jobDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return AttemptResult.Fail("Không đổi tên được output.mp4 cũ: " + ex.Message);
        }

        var rendered = await renderer.RenderAsync(new MarketingLocalVideoRenderRequest
        {
            JobDirectory = jobDirectory,
            AspectRatio = aspectRatio,
            VoicePath = voice.Path!,
            Scenes = prepared,
        }, cancellationToken);

        if (!rendered.Ok || string.IsNullOrWhiteSpace(rendered.OutputPath))
        {
            DeleteNewOutput(outputPath);
            return AttemptResult.Fail(string.IsNullOrWhiteSpace(rendered.Error)
                ? "Render local không tạo được MP4 hợp lệ."
                : rendered.Error);
        }

        var probed = await ProbeMp4Async(rendered.OutputPath, width, height, total, cancellationToken);
        if (!probed.Ok)
        {
            DeleteNewOutput(outputPath);
            return AttemptResult.Fail(probed.Error ?? "MP4 không giải mã được.");
        }

        if (!ContentVideoMediaPaths.IsJobOutputRoute(jobId, outputRoute))
        {
            DeleteNewOutput(outputPath);
            return AttemptResult.Fail("Đã có MP4 nhưng không gắn được vào route media của job.");
        }

        try
        {
            DeleteIfExists(Path.Combine(jobDirectory, "output.previous.mp4"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            /* the new MP4 is the published file; a locked previous copy is not advertised */
        }

        return AttemptResult.Success(outputRoute);
    }

    public static async Task<VoiceProbe> ProbeAudioCoversAsync(
        string jobDirectory,
        string? voicePath,
        int storyboardTotalSec,
        CancellationToken cancellationToken)
    {
        if (storyboardTotalSec <= 0)
            return VoiceProbe.Fail("Storyboard không có thời lượng.");
        if (!Inside(jobDirectory, voicePath))
            return VoiceProbe.Fail("Thiếu voice.mp3 trong thư mục job.");
        var full = Path.GetFullPath(voicePath!);
        var info = new FileInfo(full);
        if (!info.Exists || info.Length == 0)
            return VoiceProbe.Fail("Thiếu voice.mp3 trong thư mục job.");

        var probe = await RunProbeAsync(full, cancellationToken);
        if (!probe.Ok)
            return VoiceProbe.Fail(probe.Error ?? "voice.mp3 không có audio stream.");
        if (probe.AudioStreams < 1)
            return VoiceProbe.Fail("voice.mp3 không có audio stream.");
        if (probe.DurationSec + 0.001 < storyboardTotalSec)
            return VoiceProbe.Fail("voice.mp3 ngắn hơn storyboard.");
        return VoiceProbe.Ready(full, probe.DurationSec);
    }

    public static Task<Mp4Probe> ProbeMp4Async(
        string path,
        string aspectRatio,
        int storyboardTotalSec,
        CancellationToken cancellationToken)
    {
        if (!MarketingLocalVideoRenderer.TryFrameSize(aspectRatio, out var width, out var height))
            return Task.FromResult(Mp4Probe.Fail("Tỷ lệ khung không hỗ trợ cho render local: " + (aspectRatio ?? "")));
        return ProbeMp4Async(path, width, height, storyboardTotalSec, cancellationToken);
    }

    public static async Task<Mp4Probe> ProbeMp4Async(
        string path,
        int width,
        int height,
        int storyboardTotalSec,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return Mp4Probe.Fail(DecodeFailure(null));

        var probe = await RunProbeAsync(path, cancellationToken);
        if (!probe.Ok || probe.VideoStreams < 1 || probe.AudioStreams < 1)
            return Mp4Probe.Fail(DecodeFailure(probe.Error));
        if (!string.Equals(probe.VideoCodec, "h264", StringComparison.OrdinalIgnoreCase))
            return Mp4Probe.Fail("MP4 không phải H.264.");
        if (probe.Width != width || probe.Height != height)
            return Mp4Probe.Fail("MP4 không đúng kích thước " + width.ToString(CultureInfo.InvariantCulture)
                                 + "x" + height.ToString(CultureInfo.InvariantCulture) + ".");
        if (probe.DurationSec + FrameSeconds + 0.001 < storyboardTotalSec)
            return Mp4Probe.Fail("MP4 ngắn hơn storyboard.");

        var decode = await RunProcessAsync(
            "ffmpeg",
            ["-hide_banner", "-loglevel", "error", "-i", path, "-map", "0:v:0", "-map", "0:a:0", "-f", "null", "-"],
            TimeSpan.FromMinutes(2),
            cancellationToken);
        if (decode.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(decode.Stderr) ? "exit " + decode.ExitCode : decode.Stderr.Trim();
            return Mp4Probe.Fail(DecodeFailure(detail));
        }

        return Mp4Probe.Ready(probe.DurationSec);
    }

    /// <summary>
    /// True when the file can be a YouTube Short (vertical or square, at most 180 s).
    /// Null when ffprobe cannot read it, so the caller keeps its configured default.
    /// </summary>
    public static async Task<bool?> FitsYoutubeShortsAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        var probe = await RunProbeAsync(path, cancellationToken);
        if (!probe.Ok || probe.Width <= 0 || probe.Height <= 0) return null;
        return probe.Width <= probe.Height && (probe.DurationSec <= 0 || probe.DurationSec <= 180.5);
    }

    static async Task<ProbeFacts> RunProbeAsync(string path, CancellationToken cancellationToken)
    {
        var run = await RunProcessAsync(
            "ffprobe",
            [
                "-hide_banner", "-loglevel", "error",
                "-show_entries", "stream=codec_type,codec_name,width,height",
                "-show_entries", "format=duration",
                "-of", "json",
                path,
            ],
            TimeSpan.FromSeconds(30),
            cancellationToken);
        if (run.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(run.Stderr) ? "ffprobe exit " + run.ExitCode : run.Stderr.Trim();
            return ProbeFacts.Fail(detail);
        }

        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(run.Stdout) ? "{}" : run.Stdout);
            var video = 0;
            var audio = 0;
            string? videoCodec = null;
            var width = 0;
            var height = 0;
            if (doc.RootElement.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
            {
                foreach (var stream in streams.EnumerateArray())
                {
                    var type = stream.TryGetProperty("codec_type", out var typeEl) ? typeEl.GetString() : null;
                    if (string.Equals(type, "video", StringComparison.OrdinalIgnoreCase))
                    {
                        video++;
                        videoCodec ??= stream.TryGetProperty("codec_name", out var codec) ? codec.GetString() : null;
                        if (stream.TryGetProperty("width", out var w) && w.TryGetInt32(out var parsedW))
                            width = parsedW;
                        if (stream.TryGetProperty("height", out var h) && h.TryGetInt32(out var parsedH))
                            height = parsedH;
                    }
                    else if (string.Equals(type, "audio", StringComparison.OrdinalIgnoreCase))
                    {
                        audio++;
                    }
                }
            }

            var duration = 0d;
            if (doc.RootElement.TryGetProperty("format", out var format)
                && format.TryGetProperty("duration", out var durationEl)
                && double.TryParse(durationEl.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                duration = parsed;
            }

            return new ProbeFacts(true, null, video, audio, videoCodec, width, height, duration);
        }
        catch (JsonException ex)
        {
            return ProbeFacts.Fail("ffprobe không đọc được: " + ex.Message);
        }
    }

    static async Task<ProcessRun> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> args,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        foreach (var arg in args)
            start.ArgumentList.Add(arg);

        Process? process = null;
        try
        {
            process = new Process { StartInfo = start };
            if (!process.Start())
                return new ProcessRun(-1, "", "Không khởi động được " + fileName + ".");

            var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
            var stdoutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);
            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                TryKill(process);
                return new ProcessRun(-1, "", fileName + " quá thời gian.");
            }

            var stderr = await stderrTask;
            var stdout = await stdoutTask;
            return new ProcessRun(process.ExitCode, stdout ?? "", Trim(stderr));
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return new ProcessRun(-1, "", "Không tìm thấy " + fileName + " trên PATH.");
        }
        finally
        {
            process?.Dispose();
        }
    }

    static void RetireExistingOutput(string jobDirectory)
    {
        var output = Path.Combine(jobDirectory, MarketingLocalVideoRenderer.OutputFileName);
        if (!File.Exists(output))
            return;
        var previous = Path.Combine(jobDirectory, "output.previous.mp4");
        if (File.Exists(previous))
            File.Delete(previous);
        File.Move(output, previous);
    }

    static void DeleteNewOutput(string outputPath)
    {
        if (!File.Exists(outputPath))
            return;
        try
        {
            File.Delete(outputPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            /* the failure result already explains the render; a locked file is reported by the next attempt */
        }
    }

    static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    static bool Inside(string jobDirectory, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(jobDirectory))
            return false;
        var full = Path.GetFullPath(candidate);
        var prefix = Path.GetFullPath(jobDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                     + Path.DirectorySeparatorChar;
        return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    static void TryKill(Process? process)
    {
        try
        {
            if (process is { HasExited: false })
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            /* process already left */
        }
    }

    static string DecodeFailure(string? detail) =>
        string.IsNullOrWhiteSpace(detail) ? "MP4 không giải mã được." : "MP4 không giải mã được: " + detail.Trim();

    static string Trim(string? stderr)
    {
        var text = (stderr ?? "").Trim();
        return text.Length <= 600 ? text : text[..600];
    }

    internal sealed record SceneInput(string Beat, string Text, int StartSec, int EndSec, string? ImagePath);

    internal sealed record AttemptResult(bool Ready, string Status, string? OutputUrl, string? Error)
    {
        public static AttemptResult Success(string outputUrl) => new(true, "Ready", outputUrl, null);
        public static AttemptResult Fail(string error) => new(false, "Failed", null, error);
    }

    internal sealed record VoiceProbe(bool Ok, string? Path, double DurationSec, string? Error)
    {
        public static VoiceProbe Ready(string path, double duration) => new(true, path, duration, null);
        public static VoiceProbe Fail(string error) => new(false, null, 0, error);
    }

    internal sealed record Mp4Probe(bool Ok, double DurationSec, string? Error)
    {
        public static Mp4Probe Ready(double duration) => new(true, duration, null);
        public static Mp4Probe Fail(string error) => new(false, 0, error);
    }

    readonly record struct ProbeFacts(
        bool Ok,
        string? Error,
        int VideoStreams,
        int AudioStreams,
        string? VideoCodec,
        int Width,
        int Height,
        double DurationSec)
    {
        public static ProbeFacts Fail(string error) => new(false, error, 0, 0, null, 0, 0, 0);
    }

    readonly record struct ProcessRun(int ExitCode, string Stdout, string Stderr);
}
