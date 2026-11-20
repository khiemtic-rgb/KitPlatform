using System.Diagnostics;
using System.Globalization;
using System.Text;
using SkiaSharp;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Local MP4 for a marketing video job. FFmpeg is reached only through <see cref="IMarketingVideoFfmpegProcess"/>.
/// Scene text is drawn into frame files. It is never placed on the process argument list.
/// </summary>
internal sealed class MarketingLocalVideoRenderer
{
    public const string OutputFileName = "output.mp4";
    const string PartialFileName = "output.partial.mp4";
    const int MinimumOutputBytes = 1024;

    readonly IMarketingVideoFfmpegProcess _process;

    public MarketingLocalVideoRenderer(IMarketingVideoFfmpegProcess process) =>
        _process = process ?? throw new ArgumentNullException(nameof(process));

    public async Task<MarketingLocalVideoRenderResult> RenderAsync(
        MarketingLocalVideoRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            return MarketingLocalVideoRenderResult.Fail("Thiếu yêu cầu render.");
        if (string.IsNullOrWhiteSpace(request.JobDirectory))
            return MarketingLocalVideoRenderResult.Fail("Thiếu thư mục job.");

        var jobDir = Path.GetFullPath(request.JobDirectory);
        Directory.CreateDirectory(jobDir);
        var outputPath = Path.Combine(jobDir, OutputFileName);
        if (File.Exists(outputPath))
        {
            if (IsPlayableFile(outputPath))
                return MarketingLocalVideoRenderResult.Success(outputPath);
            return MarketingLocalVideoRenderResult.Fail(
                "output.mp4 đã có nhưng không phải video hợp lệ. Renderer không ghi đè.");
        }

        if (request.Scenes is null || request.Scenes.Count == 0)
            return MarketingLocalVideoRenderResult.Fail("Storyboard không có scene.");
        if (!TryFrameSize(request.AspectRatio, out var width, out var height))
            return MarketingLocalVideoRenderResult.Fail("Tỷ lệ khung không hỗ trợ cho render local: " + (request.AspectRatio ?? ""));

        if (!TryResolveInside(jobDir, request.VoicePath, out var voicePath))
            return MarketingLocalVideoRenderResult.Fail("File giọng nằm ngoài thư mục job.");
        if (!File.Exists(voicePath) || new FileInfo(voicePath).Length == 0)
            return MarketingLocalVideoRenderResult.Fail("Thiếu file giọng trong thư mục job.");

        var sequenced = request.FrameSequence is { Count: > 0 };
        var totalSec = 0;
        var imagePaths = new List<string>(request.Scenes.Count);
        foreach (var scene in request.Scenes)
        {
            if (scene is null || string.IsNullOrWhiteSpace(scene.Text))
                return MarketingLocalVideoRenderResult.Fail("Scene " + (scene?.Beat ?? "?") + " thiếu lời.");
            if (scene.EndSec <= scene.StartSec)
                return MarketingLocalVideoRenderResult.Fail("Scene " + scene.Beat + " không có thời lượng.");
            if (!sequenced)
            {
                if (!TryResolveInside(jobDir, scene.ImagePath, out var imagePath) || !File.Exists(imagePath))
                    return MarketingLocalVideoRenderResult.Fail("Scene " + scene.Beat + " thiếu file ảnh trong thư mục job.");
                imagePaths.Add(imagePath);
            }

            totalSec += scene.EndSec - scene.StartSec;
        }

        if (totalSec <= 0)
            return MarketingLocalVideoRenderResult.Fail("Storyboard không có thời lượng.");

        var partialPath = Path.Combine(jobDir, PartialFileName);
        var frames = new List<string>(request.Scenes.Count);
        try
        {
            if (File.Exists(partialPath))
                File.Delete(partialPath);

            var concatPath = Path.Combine(jobDir, "frames.txt");
            IReadOnlyList<string> args;
            TimeSpan timeout;
            if (sequenced)
            {
                var prepared = PrepareFrameSequence(jobDir, request.FrameSequence!, request.FrameDurationSec, totalSec);
                if (prepared.Error is not null)
                    return MarketingLocalVideoRenderResult.Fail(prepared.Error);
                frames = prepared.Frames!;
                await File.WriteAllTextAsync(
                    concatPath,
                    BuildFrameConcatScript(frames, request.FrameDurationSec),
                    cancellationToken);
                args = BuildMotionArguments(concatPath, voicePath, partialPath, totalSec);
                timeout = TimeSpan.FromSeconds(Math.Clamp(totalSec * 8, 180, 900));
            }
            else
            {
                for (var i = 0; i < request.Scenes.Count; i++)
                {
                    var scene = request.Scenes[i];
                    var framePath = Path.Combine(jobDir, "frame-" + (i + 1).ToString("00", CultureInfo.InvariantCulture) + ".png");
                    WriteFrame(imagePaths[i], scene.Beat, scene.Text, width, height, framePath);
                    frames.Add(framePath);
                }

                await File.WriteAllTextAsync(concatPath, BuildConcatScript(frames, request.Scenes), cancellationToken);
                args = BuildArguments(concatPath, voicePath, partialPath, totalSec);
                timeout = TimeSpan.FromSeconds(Math.Clamp(totalSec + 45, 60, 180));
            }
            var process = await _process.RunAsync(args, timeout, cancellationToken);
            if (process.TimedOut)
                return MarketingLocalVideoRenderResult.Fail("FFmpeg quá thời gian.");
            if (process.ExitCode != 0)
            {
                var detail = string.IsNullOrWhiteSpace(process.Stderr) ? "exit " + process.ExitCode : process.Stderr.Trim();
                return MarketingLocalVideoRenderResult.Fail("FFmpeg thất bại: " + detail);
            }

            if (!IsPlayableFile(partialPath))
                return MarketingLocalVideoRenderResult.Fail("FFmpeg xong nhưng không có file MP4 hợp lệ.");

            if (File.Exists(outputPath))
                return MarketingLocalVideoRenderResult.Fail("output.mp4 xuất hiện trong lúc render. Renderer không ghi đè.");

            File.Move(partialPath, outputPath, overwrite: false);
            if (!IsPlayableFile(outputPath))
                return MarketingLocalVideoRenderResult.Fail("output.mp4 sau khi ghi không phải video hợp lệ.");
            return MarketingLocalVideoRenderResult.Success(outputPath);
        }
        catch (InvalidOperationException ex)
        {
            return MarketingLocalVideoRenderResult.Fail(ex.Message);
        }
        finally
        {
            if (File.Exists(partialPath))
            {
                try { File.Delete(partialPath); } catch { /* leave the partial if the disk refuses */ }
            }
        }
    }

    public static bool IsPlayableFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;
        var info = new FileInfo(path);
        if (info.Length < MinimumOutputBytes)
            return false;
        Span<byte> header = stackalloc byte[12];
        using var stream = File.OpenRead(path);
        if (stream.Read(header) < 12)
            return false;
        if (IsImageHeader(header))
            return false;
        return header[4] == (byte)'f'
               && header[5] == (byte)'t'
               && header[6] == (byte)'y'
               && header[7] == (byte)'p';
    }

    internal static IReadOnlyList<string> BuildArguments(string concatPath, string voicePath, string partialPath, int totalSec) =>
    [
        "-hide_banner",
        "-loglevel", "error",
        "-y",
        "-f", "concat",
        "-safe", "0",
        "-i", concatPath,
        "-i", voicePath,
        "-map", "0:v:0",
        "-map", "1:a:0",
        "-c:v", "libx264",
        "-pix_fmt", "yuv420p",
        "-r", "30",
        "-c:a", "aac",
        "-b:a", "128k",
        "-t", totalSec.ToString(CultureInfo.InvariantCulture),
        "-movflags", "+faststart",
        partialPath,
    ];

    internal static IReadOnlyList<string> BuildMotionArguments(string concatPath, string voicePath, string partialPath, int totalSec)
    {
        var args = BuildArguments(concatPath, voicePath, partialPath, totalSec).ToList();
        var output = args[^1];
        args.RemoveAt(args.Count - 1);
        args.Add("-preset");
        args.Add("veryfast");
        args.Add("-crf");
        args.Add("18");
        args.Add(output);
        return args;
    }

    internal static bool TryFrameSize(string? aspectRatio, out int width, out int height)
    {
        width = 0;
        height = 0;
        switch ((aspectRatio ?? "").Trim())
        {
            case "9:16":
                width = 1080;
                height = 1920;
                return true;
            case "1:1":
                width = 1080;
                height = 1080;
                return true;
            case "16:9":
                width = 1920;
                height = 1080;
                return true;
            default:
                return false;
        }
    }

    static string BuildConcatScript(IReadOnlyList<string> frames, IReadOnlyList<MarketingLocalVideoScene> scenes)
    {
        var script = new StringBuilder();
        script.AppendLine("ffconcat version 1.0");
        for (var i = 0; i < frames.Count; i++)
        {
            var duration = scenes[i].EndSec - scenes[i].StartSec;
            script.Append("file '").Append(EscapeConcatPath(frames[i])).AppendLine("'");
            script.Append("duration ")
                .Append(duration.ToString(CultureInfo.InvariantCulture))
                .AppendLine(".000");
        }

        script.Append("file '").Append(EscapeConcatPath(frames[^1])).AppendLine("'");
        return script.ToString();
    }

    static string BuildFrameConcatScript(IReadOnlyList<string> frames, double frameDurationSec)
    {
        var duration = frameDurationSec.ToString("0.000000", CultureInfo.InvariantCulture);
        var script = new StringBuilder();
        script.AppendLine("ffconcat version 1.0");
        foreach (var frame in frames)
        {
            script.Append("file '").Append(EscapeConcatPath(frame)).AppendLine("'");
            script.Append("duration ").AppendLine(duration);
        }

        script.Append("file '").Append(EscapeConcatPath(frames[^1])).AppendLine("'");
        return script.ToString();
    }

    static (List<string>? Frames, string? Error) PrepareFrameSequence(
        string jobDir,
        IReadOnlyList<string> sequence,
        double frameDurationSec,
        int totalSec)
    {
        if (frameDurationSec < (1d / 60d) || frameDurationSec > 1d)
            return (null, "Thời lượng mỗi khung hình không hợp lệ.");
        if (Math.Abs(sequence.Count * frameDurationSec - totalSec) > frameDurationSec * 0.51)
            return (null, "Dãy khung không khớp thời lượng storyboard.");

        var frames = new List<string>(sequence.Count);
        foreach (var candidate in sequence)
        {
            if (!TryResolveInside(jobDir, candidate, out var full) || !File.Exists(full))
                return (null, "Khung hình nằm ngoài thư mục job hoặc không tồn tại.");
            if (!HasImageHeaderFile(full))
                return (null, "Khung hình không phải ảnh đã nhận dạng.");
            frames.Add(full);
        }

        return (frames, null);
    }

    static bool HasImageHeaderFile(string path)
    {
        Span<byte> header = stackalloc byte[12];
        using var stream = File.OpenRead(path);
        var read = stream.Read(header);
        return read >= 12 && IsImageHeader(header[..read]);
    }

    static string EscapeConcatPath(string path)
    {
        var normalized = Path.GetFullPath(path).Replace('\\', '/').Replace("'", @"'\''");
        return normalized;
    }

    static void WriteFrame(string imagePath, string beat, string text, int width, int height, string destPath)
    {
        using var bitmap = SKBitmap.Decode(imagePath);
        if (bitmap is null)
            throw new InvalidOperationException("Không đọc được ảnh scene " + beat + ".");

        using var surface = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using var canvas = new SKCanvas(surface);
        canvas.Clear(SKColors.Black);
        DrawCover(canvas, bitmap, width, height);
        using var bar = new SKPaint { Color = new SKColor(0, 0, 0, 170), IsAntialias = true };
        canvas.DrawRect(new SKRect(0, height - 340, width, height), bar);

        using var typeface = SKTypeface.FromFamilyName("Segoe UI") ?? SKTypeface.Default;
        if (typeface is null)
            throw new InvalidOperationException("Không có font để vẽ chữ scene " + beat + ".");
        using var font = new SKFont(typeface, 46);
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };

        var lines = new List<string> { beat };
        lines.AddRange(Wrap(text, 28).Take(4));
        var y = height - 280f;
        foreach (var line in lines)
        {
            canvas.DrawText(line, 64, y, font, paint);
            y += 58;
        }

        using var image = SKImage.FromBitmap(surface);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90)
                        ?? throw new InvalidOperationException("Không ghi được khung scene " + beat + ".");
        using var stream = File.Create(destPath);
        data.SaveTo(stream);
    }

    static void DrawCover(SKCanvas canvas, SKBitmap bitmap, int width, int height)
    {
        var scale = Math.Max(width / (float)bitmap.Width, height / (float)bitmap.Height);
        var w = bitmap.Width * scale;
        var h = bitmap.Height * scale;
        var x = (width - w) / 2f;
        var y = (height - h) / 2f;
        canvas.DrawBitmap(bitmap, new SKRect(x, y, x + w, y + h));
    }

    static IEnumerable<string> Wrap(string text, int width)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var line = new StringBuilder();
        foreach (var word in words)
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > width)
            {
                yield return line.ToString();
                line.Clear();
            }

            if (line.Length > 0)
                line.Append(' ');
            line.Append(word);
        }

        if (line.Length > 0)
            yield return line.ToString();
    }

    static bool TryResolveInside(string jobDir, string? candidate, out string fullPath)
    {
        fullPath = "";
        if (string.IsNullOrWhiteSpace(candidate))
            return false;
        fullPath = Path.GetFullPath(candidate);
        var prefix = Path.GetFullPath(jobDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                     + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsImageHeader(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return true;
        if (header.Length >= 4 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            return true;
        if (header.Length >= 4 && header[0] == (byte)'G' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'8')
            return true;
        return header.Length >= 12
               && header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F'
               && header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P';
    }
}

internal sealed class MarketingLocalVideoRenderRequest
{
    public required string JobDirectory { get; init; }
    public required string AspectRatio { get; init; }
    public required string VoicePath { get; init; }
    public required IReadOnlyList<MarketingLocalVideoScene> Scenes { get; init; }

    /// <summary>
    /// Optional already-composed frames inside the job directory.
    /// When set, the still-image caption path is not used.
    /// </summary>
    public IReadOnlyList<string>? FrameSequence { get; init; }

    public double FrameDurationSec { get; init; }
}

internal sealed class MarketingLocalVideoScene
{
    public required int Order { get; init; }
    public required string Beat { get; init; }
    public required string Text { get; init; }
    public required int StartSec { get; init; }
    public required int EndSec { get; init; }
    public required string ImagePath { get; init; }
}

internal sealed record MarketingLocalVideoRenderResult(bool Ok, string? OutputPath, string? Error)
{
    public static MarketingLocalVideoRenderResult Success(string outputPath) => new(true, outputPath, null);
    public static MarketingLocalVideoRenderResult Fail(string error) => new(false, null, error);
}

internal interface IMarketingVideoFfmpegProcess
{
    Task<MarketingVideoProcessResult> RunAsync(
        IReadOnlyList<string> argumentList,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

internal sealed record MarketingVideoProcessResult(int ExitCode, string Stderr, bool TimedOut);

internal sealed class ProcessMarketingVideoFfmpeg : IMarketingVideoFfmpegProcess
{
    public async Task<MarketingVideoProcessResult> RunAsync(
        IReadOnlyList<string> argumentList,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (argumentList is null || argumentList.Count == 0)
            return new MarketingVideoProcessResult(-1, "Thiếu đối số ffmpeg.", false);

        var start = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        foreach (var arg in argumentList)
        {
            if (string.IsNullOrEmpty(arg))
                return new MarketingVideoProcessResult(-1, "Đối số ffmpeg rỗng.", false);
            start.ArgumentList.Add(arg);
        }

        Process? process = null;
        try
        {
            process = new Process { StartInfo = start };
            if (!process.Start())
                return new MarketingVideoProcessResult(-1, "Không khởi động được ffmpeg.", false);

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
                var timed = Trim(await stderrTask);
                await stdoutTask;
                return new MarketingVideoProcessResult(-1, string.IsNullOrWhiteSpace(timed) ? "FFmpeg quá thời gian." : timed, true);
            }

            var stderr = Trim(await stderrTask);
            await stdoutTask;
            return new MarketingVideoProcessResult(process.ExitCode, stderr, false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return new MarketingVideoProcessResult(-1, "Không tìm thấy ffmpeg trên PATH.", false);
        }
        finally
        {
            process?.Dispose();
        }
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

    static string Trim(string? stderr)
    {
        var text = (stderr ?? "").Trim();
        return text.Length <= 600 ? text : text[..600];
    }
}
