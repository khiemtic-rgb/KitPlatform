using System.Diagnostics;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Chạy ffmpeg/ffprobe theo mẫu <c>ProcessMarketingVideoFfmpeg</c>: ArgumentList (không qua shell), giới hạn thời gian,
/// hủy thì giết cả cây tiến trình. Khác ở chỗ có thư mục làm việc (bộ lọc subtitles cần đường dẫn tương đối) và trả stdout của ffprobe.
/// </summary>
internal sealed class ProcessLinesheetMediaTools : ILinesheetMediaTools
{
    readonly FamixaLinesheetOptions _options;

    public ProcessLinesheetMediaTools(IOptions<FamixaLinesheetOptions> options) => _options = options.Value;

    public async Task RunFfmpegAsync(IReadOnlyList<string> args, string workingDirectory, TimeSpan timeout, CancellationToken ct)
    {
        var (code, _, stderr, timedOut) = await RunAsync(_options.FfmpegPath, args, workingDirectory, timeout, ct);
        if (timedOut) throw new InvalidOperationException("LINESHEET_FFMPEG_TIMEOUT: " + stderr);
        if (code != 0) throw new InvalidOperationException($"LINESHEET_FFMPEG_FAILED ({code}): {stderr}");
    }

    public async Task<string> RunFfprobeAsync(IReadOnlyList<string> args, string workingDirectory, TimeSpan timeout, CancellationToken ct)
    {
        var (code, stdout, stderr, timedOut) = await RunAsync(_options.FfprobePath, args, workingDirectory, timeout, ct);
        if (timedOut) throw new InvalidOperationException("LINESHEET_FFPROBE_TIMEOUT");
        if (code != 0) throw new InvalidOperationException($"LINESHEET_FFPROBE_FAILED ({code}): {stderr}");
        return stdout;
    }

    internal static async Task<(int Code, string Stdout, string Stderr, bool TimedOut)> RunAsync(
        string fileName, IReadOnlyList<string> args, string workingDirectory, TimeSpan timeout, CancellationToken ct)
    {
        if (args is null || args.Count == 0 || args.Any(string.IsNullOrEmpty))
            throw new InvalidOperationException("LINESHEET_MEDIA_ARGS: thiếu hoặc rỗng đối số.");
        var start = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        foreach (var a in args) start.ArgumentList.Add(a);

        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start()) throw new InvalidOperationException("LINESHEET_MEDIA_START: không khởi động được " + fileName);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new InvalidOperationException($"LINESHEET_MEDIA_MISSING: không tìm thấy {fileName} trên PATH.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(limit.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            var err = Tail(await stderrTask);
            await stdoutTask;
            if (ct.IsCancellationRequested) throw;
            return (-1, "", err, true);
        }

        return (process.ExitCode, await stdoutTask, Tail(await stderrTask), false);
    }

    static void TryKill(Process p)
    {
        try
        {
            if (!p.HasExited) p.Kill(entireProcessTree: true);
        }
        catch
        {
            /* đã thoát */
        }
    }

    static string Tail(string? s)
    {
        s = (s ?? "").Trim();
        return s.Length > 600 ? s[^600..] : s;
    }
}
