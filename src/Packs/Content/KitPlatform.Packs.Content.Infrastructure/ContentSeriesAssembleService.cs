using System.Diagnostics;
using System.Globalization;
using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>Normalize takes + mix voice on a master timeline via FFmpeg. Does not invent cuts.</summary>
internal sealed class ContentSeriesAssembleService : IContentSeriesAssembleService
{
    private const int MaxClips = 40;
    private const int MaxVoiceBytes = 4_000_000;
    private readonly IContentSeriesTakeProxyService _takes;

    public ContentSeriesAssembleService(IContentSeriesTakeProxyService takes)
    {
        _takes = takes;
    }

    public async Task<(byte[] Bytes, string ContentType, string FileName)> AssembleAsync(
        ContentSeriesAssembleRequest request,
        CancellationToken cancellationToken = default)
    {
        var clips = request.Clips ?? Array.Empty<ContentSeriesAssembleClipDto>();
        if (clips.Count is < 1 or > MaxClips)
            throw new InvalidOperationException($"Ghép 1–{MaxClips} Short.");

        var ffmpeg = ResolveFfmpeg();
        if (string.IsNullOrWhiteSpace(ffmpeg))
            throw new InvalidOperationException("Chưa có FFmpeg trên máy API. Cài ffmpeg và thêm vào PATH rồi ghép lại.");

        var work = Path.Combine(Path.GetTempPath(), "kit-famixa-assemble", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var parts = new List<string>();
            for (var i = 0; i < clips.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var clip = clips[i];
                var take = await _takes.FetchAsync(clip.VideoUrl, cancellationToken);
                var src = Path.Combine(work, $"src{i:00}.mp4");
                await File.WriteAllBytesAsync(src, take.Bytes, cancellationToken);
                var voices = new List<string>();
                var delays = new List<int>();
                foreach (var v in clip.Voices ?? Array.Empty<ContentSeriesAssembleVoiceDto>())
                {
                    var raw = DecodeAudio(v.AudioBase64);
                    if (raw.Length < 32 || raw.Length > MaxVoiceBytes) continue;
                    var vp = Path.Combine(work, $"v{i:00}_{voices.Count}.mp3");
                    await File.WriteAllBytesAsync(vp, raw, cancellationToken);
                    voices.Add(vp);
                    delays.Add(Math.Max(0, (int)Math.Round(v.StartSec * 1000)));
                }

                var dur = Math.Clamp(clip.Seconds, 0.4, 20);
                if (clip.UsableEnd is > 0) dur = Math.Min(dur, Math.Max(0.4, clip.UsableEnd.Value - clip.UsableStart));
                var outPart = Path.Combine(work, $"part{i:00}.mp4");
                await RunFfmpeg(ffmpeg, MixArgs(src, voices, delays, clip.UsableStart, dur, outPart, request.Aspect), work, cancellationToken);
                parts.Add(outPart);
            }

            var list = Path.Combine(work, "list.txt");
            await File.WriteAllTextAsync(
                list,
                string.Join('\n', parts.Select(p => $"file '{p.Replace('\\', '/')}'")),
                cancellationToken);
            var dest = Path.Combine(work, "cut.mp4");
            await RunFfmpeg(ffmpeg, $"-y -f concat -safe 0 -i \"{list}\" -c copy \"{dest}\"", work, cancellationToken);
            var bytes = await File.ReadAllBytesAsync(dest, cancellationToken);
            if (bytes.Length < 1000) throw new InvalidOperationException("FFmpeg xong nhưng file trống.");
            var stem = Sanitize(request.FileStem);
            return (bytes, "video/mp4", $"{stem}.mp4");
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { /* temp */ }
        }
    }

    private static string MixArgs(string video, List<string> voices, List<int> delays, double ss, double dur, string dest, string? aspect)
    {
        var start = ss > 0.05 ? $"-ss {ss.ToString("0.###", CultureInfo.InvariantCulture)} " : "";
        var t = $"-t {dur.ToString("0.###", CultureInfo.InvariantCulture)}";
        var inputs = new StringBuilder();
        inputs.Append(start).Append("-i \"").Append(video).Append("\" ");
        foreach (var v in voices)
            inputs.Append("-i \"").Append(v).Append("\" ");
        if (voices.Count == 0)
            inputs.Append("-f lavfi -i anullsrc=channel_layout=stereo:sample_rate=48000 ");

        var portrait = string.Equals(aspect?.Trim(), "9:16", StringComparison.OrdinalIgnoreCase);
        var vw = portrait ? 1080 : 1920;
        var vh = portrait ? 1920 : 1080;
        var fit = portrait
            ? $"scale={vw}:{vh}:force_original_aspect_ratio=increase,crop={vw}:{vh}"
            : $"scale={vw}:{vh}:force_original_aspect_ratio=decrease,pad={vw}:{vh}:(ow-iw)/2:(oh-ih)/2";
        var fc = new StringBuilder();
        fc.Append("[0:v]").Append(fit).Append(",fps=30,setsar=1,format=yuv420p,tpad=stop_mode=clone:stop_duration=8[v];");
        if (voices.Count == 0)
        {
            fc.Append("[1:a]aformat=sample_rates=48000:channel_layouts=stereo,atrim=0:").Append(dur.ToString("0.###", CultureInfo.InvariantCulture)).Append("[a]");
        }
        else
        {
            for (var i = 0; i < voices.Count; i++)
            {
                var ms = delays[i];
                fc.Append('[').Append(i + 1).Append(":a]adelay=").Append(ms).Append('|').Append(ms)
                    .Append(",aformat=sample_rates=48000:channel_layouts=stereo[a").Append(i).Append("];");
            }

            if (voices.Count == 1)
                fc.Append("[a0]volume=2,apad[a]");
            else
            {
                for (var i = 0; i < voices.Count; i++) fc.Append("[a").Append(i).Append(']');
                fc.Append("amix=inputs=").Append(voices.Count).Append(":normalize=0:dropout_transition=0,volume=2,apad[a]");
            }
        }

        return $"-y {inputs}-filter_complex \"{fc}\" -map \"[v]\" -map \"[a]\" {t} -c:v libx264 -preset veryfast -crf 20 -c:a aac -b:a 160k -ar 48000 -ac 2 \"{dest}\"";
    }

    private static async Task RunFfmpeg(string bin, string args, string work, CancellationToken cancellationToken)
    {
        using var p = new Process();
        p.StartInfo = new ProcessStartInfo
        {
            FileName = bin,
            Arguments = args,
            WorkingDirectory = work,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        p.Start();
        var err = await p.StandardError.ReadToEndAsync(cancellationToken);
        await p.WaitForExitAsync(cancellationToken);
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"FFmpeg lỗi ({p.ExitCode}): {Trim(err)}");
    }

    private static string? ResolveFfmpeg()
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
                p.WaitForExit(4000);
                if (p.ExitCode == 0) return name;
            }
            catch { /* next */ }
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

        return null;
    }

    private static byte[] DecodeAudio(string raw)
    {
        var s = (raw ?? "").Trim();
        var comma = s.IndexOf(',');
        if (s.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
            s = s[(comma + 1)..];
        try { return Convert.FromBase64String(s); }
        catch { throw new InvalidOperationException("File thoại không đọc được."); }
    }

    private static string Sanitize(string? stem)
    {
        var t = string.Join("-", (stem ?? "famixa-cut").Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(t) ? "famixa-cut" : t.Trim()[..Math.Min(t.Trim().Length, 60)];
    }

    private static string Trim(string? text)
    {
        var t = (text ?? "").Trim();
        return t.Length <= 280 ? t : t[^280..];
    }
}
