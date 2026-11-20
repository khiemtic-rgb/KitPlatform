using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using SkiaSharp;

namespace KitPlatform.Packs.Content.VisualIntelligence;

/// <summary>
/// Turns performance frames into a local mp4. No paid video provider.
/// </summary>
public static class PerformanceFilm
{
    public const int Fps = 24;

    public static byte[] Encode(IPerformanceEngine engine, ShotTimeline timeline, ActionClip clip, float duration)
    {
        if (duration < 0.4f || duration > 4f)
            throw new ArgumentOutOfRangeException(nameof(duration), "Thời lượng phim nằm trong khoảng 0.4–4 giây.");

        var dir = Path.Combine(Path.GetTempPath(), "kit-performance-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var frames = Math.Max(2, (int)Math.Round(duration * Fps));
            for (var i = 0; i < frames; i++)
            {
                var time = frames == 1 ? 0 : duration * i / (frames - 1);
                var shot = timeline with { Cue = new TimelineCue(clip.Id, 0, duration) };
                var png = engine.Render(PerformanceCatalog.Request(shot, clip, time));
                File.WriteAllBytes(Path.Combine(dir, $"frame-{i:00}.png"), png);
            }

            var output = Path.Combine(dir, "film.mp4");
            var error = RunFfmpeg(dir, output, "frame-%02d.png", 60_000);
            if (!File.Exists(output))
                throw new InvalidOperationException("ffmpeg không tạo được phim. " + error);
            return File.ReadAllBytes(output);
        }
        finally
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch (IOException)
            {
            }
        }
    }

    public const string NovixaShortId = "film-novixa-short";

    public static byte[] EncodeSequence(IPerformanceEngine engine, ShotTimeline timeline, IReadOnlyList<FilmBeat> beats, float totalSeconds)
    {
        if (beats.Count == 0)
            throw new InvalidOperationException("Phim không có nhịp.");
        if (totalSeconds < 2f || totalSeconds > 15f)
            throw new ArgumentOutOfRangeException(nameof(totalSeconds), "Phim ngắn nằm trong khoảng 2–15 giây.");
        var natural = beats[^1].Start + beats[^1].Duration;
        if (natural <= 0)
            throw new InvalidOperationException("Nhịp phim không có thời lượng.");
        var scale = totalSeconds / natural;
        var dir = Path.Combine(Path.GetTempPath(), "kit-performance-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var frames = Math.Max(2, (int)Math.Round(totalSeconds * Fps));
            for (var i = 0; i < frames; i++)
            {
                var time = frames == 1 ? 0 : totalSeconds * i / (frames - 1);
                var clock = Math.Abs(scale - 1f) < 0.001f
                    ? Math.Min(natural, i / (float)Fps)
                    : Math.Min(natural, time / scale);
                var beat = beats[0];
                foreach (var candidate in beats)
                {
                    if (clock + 0.001f >= candidate.Start)
                        beat = candidate;
                }
                var clip = PerformanceCatalog.FindClip(beat.ClipId);
                var shot = timeline with { Cue = new TimelineCue(clip.Id, 0, natural) };
                var png = engine.Render(PerformanceCatalog.Request(shot, clip, clock));
                File.WriteAllBytes(Path.Combine(dir, $"frame-{i:000}.png"), Caption(png, beat.Line));
            }

            var output = Path.Combine(dir, "film.mp4");
            var error = RunFfmpeg(dir, output, "frame-%03d.png", 120_000);
            if (!File.Exists(output))
                throw new InvalidOperationException("ffmpeg không tạo được phim. " + error);
            TryAddVoice(dir, output, beats, scale);
            return File.ReadAllBytes(output);
        }
        finally
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static byte[] Caption(byte[] png, string line)
    {
        using var bitmap = SKBitmap.Decode(png);
        using var canvas = new SKCanvas(bitmap);
        using var bar = new SKPaint { Color = new SKColor(16, 28, 40, 176) };
        canvas.DrawRect(new SKRect(0, bitmap.Height - 68, bitmap.Width, bitmap.Height), bar);
        using var typeface = SKTypeface.FromFamilyName("Segoe UI") ?? SKTypeface.Default;
        using var font = new SKFont(typeface, 28);
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        canvas.DrawText(line, 40, bitmap.Height - 26, font, paint);
        canvas.Flush();
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }

    private static bool VoiceUnavailable;

    private static void TryAddVoice(string dir, string video, IReadOnlyList<FilmBeat> beats, float scale)
    {
        if (VoiceUnavailable)
            return;
        try
        {
            var clips = new List<string>();
            for (var i = 0; i < beats.Count; i++)
            {
                var line = beats[i].Line?.Trim();
                if (string.IsNullOrEmpty(line))
                    continue;
                var clip = Path.Combine(dir, "line-" + i + ".mp3");
                if (!Speak(line, clip))
                {
                    VoiceUnavailable = true;
                    return;
                }
                clips.Add(clip);
            }

            if (clips.Count == 0)
                return;
            var voice = Path.Combine(dir, "voice.m4a");
            if (!MixVoice(dir, beats, scale, voice))
                return;
            var muxed = Path.Combine(dir, "film-voice.mp4");
            var error = RunRaw(
                dir,
                60_000,
                "ffmpeg", "-y", "-i", video, "-i", voice,
                "-c:v", "copy", "-c:a", "aac", "-movflags", "+faststart", muxed);
            if (error == "" && new FileInfo(muxed).Length > 1000)
                File.Copy(muxed, video, true);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
    }

    private static bool Speak(string line, string destination)
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "kit-voice-cache");
        Directory.CreateDirectory(cacheDir);
        var cache = Path.Combine(cacheDir, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("vi-VN-HoaiMyNeural|" + line))) + ".mp3");
        if (!File.Exists(cache) || new FileInfo(cache).Length < 1000)
        {
            if (File.Exists(cache))
                File.Delete(cache);
            var error = RunRaw(
                cacheDir,
                12_000,
                "python", "-m", "edge_tts",
                "--voice", "vi-VN-HoaiMyNeural",
                "--text", line,
                "--write-media", cache);
            if (error != "" || !File.Exists(cache) || new FileInfo(cache).Length < 1000)
                return false;
        }

        File.Copy(cache, destination, true);
        return true;
    }

    private static bool MixVoice(string dir, IReadOnlyList<FilmBeat> beats, float scale, string voice)
    {
        var args = new List<string> { "-y" };
        var filters = new StringBuilder();
        var mix = new StringBuilder();
        var count = 0;
        for (var i = 0; i < beats.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(beats[i].Line))
                continue;
            args.Add("-i");
            args.Add("line-" + i + ".mp3");
            var delay = Math.Max(0, (int)Math.Round(beats[i].Start * scale * 1000));
            filters.Append('[').Append(count).Append(":a]adelay=").Append(delay).Append('|').Append(delay).Append(",apad[a").Append(count).Append("];");
            mix.Append("[a").Append(count).Append(']');
            count++;
        }

        if (count == 0)
            return false;
        args.Insert(0, "ffmpeg");
        filters.Append(mix).Append("amix=inputs=").Append(count).Append(":duration=longest:normalize=0[out]");
        args.Add("-filter_complex");
        args.Add(filters.ToString());
        args.Add("-map");
        args.Add("[out]");
        args.Add("-c:a");
        args.Add("aac");
        args.Add(voice);
        return RunRaw(dir, 40_000, args.ToArray()) == "" && File.Exists(voice);
    }

    private static string RunRaw(string dir, int timeoutMs, params string[] args)
    {
        if (args.Length == 0)
            return "Không có lệnh.";
        var start = new ProcessStartInfo
        {
            FileName = args[0],
            WorkingDirectory = dir,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        for (var i = 1; i < args.Length; i++)
            start.ArgumentList.Add(args[i]);
        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start())
                return "Không khởi động được " + args[0] + ".";
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return "Không tìm thấy " + args[0] + ".";
        }

        var stderrTask = process.StandardError.ReadToEndAsync();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        if (!process.WaitForExit(timeoutMs))
        {
            try
            {
                process.Kill(true);
            }
            catch (InvalidOperationException)
            {
            }

            return "Hết thời gian.";
        }

        var stderr = stderrTask.Wait(2_000) ? stderrTask.Result : "";
        _ = stdoutTask;
        return process.ExitCode == 0 ? "" : stderr.Length > 400 ? stderr[^400..] : stderr;
    }

    private static string RunFfmpeg(string dir, string output, string pattern, int timeoutMs)
    {
        var start = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            WorkingDirectory = dir,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("-y");
        start.ArgumentList.Add("-framerate");
        start.ArgumentList.Add(Fps.ToString());
        start.ArgumentList.Add("-i");
        start.ArgumentList.Add(pattern);
        start.ArgumentList.Add("-c:v");
        start.ArgumentList.Add("libx264");
        start.ArgumentList.Add("-pix_fmt");
        start.ArgumentList.Add("yuv420p");
        start.ArgumentList.Add("-movflags");
        start.ArgumentList.Add("+faststart");
        start.ArgumentList.Add(output);
        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start())
                return "Không khởi động được ffmpeg.";
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return "Không tìm thấy ffmpeg trên PATH.";
        }

        var stderrTask = process.StandardError.ReadToEndAsync();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        if (!process.WaitForExit(timeoutMs))
        {
            try
            {
                process.Kill(true);
            }
            catch (InvalidOperationException)
            {
            }

            return "ffmpeg hết thời gian.";
        }

        var stderr = stderrTask.Wait(2_000) ? stderrTask.Result : "";
        _ = stdoutTask;
        if (process.ExitCode != 0)
            return stderr.Length > 500 ? stderr[^500..] : stderr;
        return "";
    }
}
