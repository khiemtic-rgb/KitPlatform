using System.Diagnostics;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Local speech-window trim for Fal lipsync input.
/// The stored take is never written. Fal receives only the approved interval.
/// </summary>
public static class ProductionLipsyncSpeechSource
{
    public static DirectorSpeechInterval Require(PerformanceSourceRef? source, decimal takeDurationSec)
    {
        if (takeDurationSec <= 0)
            throw new InvalidOperationException(ProductionFailureReasons.InvalidRange);
        var speech = ProductionDirectorTiming.ApprovedSpeech(source);
        if (speech is null)
            throw new InvalidOperationException(ProductionFailureReasons.MissingTimeline);
        if (speech.Value.StartSec < 0 || speech.Value.EndSec <= speech.Value.StartSec || speech.Value.EndSec > takeDurationSec)
            throw new InvalidOperationException(ProductionFailureReasons.InvalidRange);
        return speech.Value;
    }

    public static async Task<byte[]> TrimAsync(
        byte[] source,
        decimal startSec,
        decimal endSec,
        decimal takeDurationSec,
        CancellationToken ct)
    {
        if (source is not { Length: >= 32 })
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        if (takeDurationSec <= 0 || startSec < 0 || endSec <= startSec || endSec > takeDurationSec)
            throw new InvalidOperationException(ProductionFailureReasons.InvalidRange);
        if (startSec == 0m && endSec == takeDurationSec)
            return source;

        var ffmpeg = ResolveFfmpeg()
            ?? throw new InvalidOperationException(ProductionFailureReasons.ExecutionProcessFailed + ":ffmpeg-missing");
        var dir = Path.Combine(Path.GetTempPath(), "famixa-lipsync-src-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var input = Path.Combine(dir, "take.mp4");
            var output = Path.Combine(dir, "speech.mp4");
            await File.WriteAllBytesAsync(input, source, ct);
            var args = "-y -hide_banner -loglevel error -i \"" + input + "\" -ss "
                + ProductionMixContract.Dec(startSec) + " -to " + ProductionMixContract.Dec(endSec)
                + " -map 0:v:0 -map 0:a:0? -c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p"
                + " -c:a aac -b:a 128k -ar 48000 -ac 2 -movflags +faststart \"" + output + "\"";
            var (code, text) = await RunAsync(ffmpeg, args, dir, ct);
            if (code != 0 || !File.Exists(output))
                throw new InvalidOperationException(
                    ProductionFailureReasons.ExecutionProcessFailed + ":ffmpeg:" + OneLine(text));
            var trimmed = await File.ReadAllBytesAsync(output, ct);
            if (trimmed.Length < 32)
                throw new InvalidOperationException(ProductionFailureReasons.ExecutionProcessFailed + ":ffmpeg:empty");
            return trimmed;
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public static string? ResolveFfmpeg()
    {
        foreach (var name in new[] { "ffmpeg", "ffmpeg.exe" })
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = name,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                });
                if (p is null) continue;
                if (!p.WaitForExit(4000))
                {
                    try { p.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                    continue;
                }
                if (p.ExitCode == 0) return name;
            }
            catch (System.ComponentModel.Win32Exception) { }
        }

        foreach (var path in new[]
                 {
                     @"C:\ffmpeg\bin\ffmpeg.exe",
                     @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
                     "/usr/bin/ffmpeg",
                     "/usr/local/bin/ffmpeg",
                 })
        {
            if (File.Exists(path)) return path;
        }

        var winget = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Microsoft\WinGet\Packages");
        if (Directory.Exists(winget))
        {
            foreach (var hit in Directory.EnumerateFiles(winget, "ffmpeg.exe", SearchOption.AllDirectories))
                return hit;
        }
        return null;
    }

    public static async Task<(int Code, string Text)> RunAsync(string file, string args, string work, CancellationToken ct)
    {
        using var p = new Process();
        p.StartInfo = new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            WorkingDirectory = work,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        p.Start();
        var stdout = await p.StandardOutput.ReadToEndAsync(ct);
        var stderr = await p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        var text = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
        return (p.ExitCode, text ?? "");
    }

    static string OneLine(string? text)
    {
        var line = (text ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
        return line.Length <= 180 ? line : line[..180];
    }
}
