using System.Diagnostics;
using System.Globalization;
using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>Normalize takes + mix voice on a master timeline via FFmpeg. Does not invent cuts.</summary>
internal sealed class ContentSeriesAssembleService : IContentSeriesAssembleService
{
    private const int MaxClips = 40;
    private const int MaxVoiceBytes = 8_000_000;
    private const int MaxStillBytes = 8_000_000;
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
                var still = DecodeStill(clip.StillBase64);
                var fromStill = still.Length > 0 && string.IsNullOrWhiteSpace(clip.VideoUrl);
                if (!fromStill && string.IsNullOrWhiteSpace(clip.VideoUrl))
                    throw new InvalidOperationException($"{clip.Code}: thiếu take và thiếu KF — không bỏ shot, tạo KF rồi ghép.");
                var stillExt = still.Length > 2 && still[0] == 0xFF && still[1] == 0xD8 ? "jpg" : "png";
                var src = Path.Combine(work, fromStill ? $"src{i:00}.{stillExt}" : $"src{i:00}.mp4");
                if (fromStill)
                    await File.WriteAllBytesAsync(src, still, cancellationToken);
                else
                {
                    var take = await _takes.FetchAsync(clip.VideoUrl!, cancellationToken);
                    await File.WriteAllBytesAsync(src, take.Bytes, cancellationToken);
                }
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
                if (!fromStill)
                {
                    var takeDur = ProbeDurationSec(ffmpeg, src);
                    if (takeDur > 0 && takeDur + 0.001 < dur - 0.12)
                        throw new InvalidOperationException(
                            $"{clip.Code}: take {takeDur.ToString("0.##", CultureInfo.InvariantCulture)}s ngắn hơn production {dur.ToString("0.##", CultureInfo.InvariantCulture)}s — không clone frame.");
                }
                var outPart = Path.Combine(work, $"part{i:00}.mp4");
                var keepAudio = !fromStill && clip.UseVideoAudio && HasAudibleAudio(ffmpeg, src);
                if (!keepAudio && voices.Count == 0 && clip.RequireVoice)
                    throw new InvalidOperationException($"{clip.Code}: thiếu thoại — không ghép file câm.");
                await RunFfmpeg(
                    ffmpeg,
                    MixArgs(
                        src,
                        voices,
                        delays,
                        clip.UsableStart,
                        dur,
                        outPart,
                        request.Aspect,
                        keepAudio,
                        fromStill,
                        request.Mix?.Grade == true,
                        request.Mix?.Interpolate == true,
                        request.Mix?.ColorMatch == true && i > 0),
                    work,
                    cancellationToken);
                parts.Add(outPart);
            }

            var list = Path.Combine(work, "list.txt");
            await File.WriteAllTextAsync(
                list,
                string.Join('\n', parts.Select(p => $"file '{p.Replace('\\', '/')}'")),
                cancellationToken);
            var dest = Path.Combine(work, "cut.mp4");
            try
            {
                await RunFfmpeg(
                    ffmpeg,
                    $"-y -fflags +genpts -f concat -safe 0 -i \"{list}\" -c copy \"{dest}\"",
                    work,
                    cancellationToken);
            }
            catch (InvalidOperationException)
            {
                await RunFfmpeg(
                    ffmpeg,
                    $"-y -fflags +genpts -f concat -safe 0 -i \"{list}\" -c:v libx264 -preset veryfast -crf 20 -c:a aac -b:a 160k -ar 48000 -ac 2 -fps_mode cfr \"{dest}\"",
                    work,
                    cancellationToken);
            }
            dest = await ApplyCanonicalMix(ffmpeg, dest, work, clips, request.Mix, cancellationToken);
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

    private static string MixArgs(
        string video,
        List<string> voices,
        List<int> delays,
        double ss,
        double dur,
        string dest,
        string? aspect,
        bool useVideoAudio,
        bool fromStill = false,
        bool grade = false,
        bool interpolate = false,
        bool colorMatch = false)
    {
        var t = $"-t {dur.ToString("0.###", CultureInfo.InvariantCulture)}";
        var keepTakeAudio = useVideoAudio && !fromStill;
        var inputs = new StringBuilder();
        if (fromStill)
            inputs.Append("-loop 1 ").Append(t).Append(" -i \"").Append(video).Append("\" ");
        else
        {
            var start = ss > 0.05 ? $"-ss {ss.ToString("0.###", CultureInfo.InvariantCulture)} " : "";
            inputs.Append(start).Append("-i \"").Append(video).Append("\" ");
        }
        if (!keepTakeAudio)
        {
            foreach (var v in voices)
                inputs.Append("-i \"").Append(v).Append("\" ");
            if (voices.Count == 0)
                inputs.Append("-f lavfi -i anullsrc=channel_layout=stereo:sample_rate=48000 ");
        }

        var portrait = string.Equals(aspect?.Trim(), "9:16", StringComparison.OrdinalIgnoreCase);
        var vw = portrait ? 1080 : 1920;
        var vh = portrait ? 1920 : 1080;
        var fit = portrait
            ? $"scale={vw}:{vh}:force_original_aspect_ratio=increase,crop={vw}:{vh}"
            : $"scale={vw}:{vh}:force_original_aspect_ratio=decrease,pad={vw}:{vh}:(ow-iw)/2:(oh-ih)/2";
        var fc = new StringBuilder();
        var polish = new StringBuilder();
        if (grade)
            polish.Append(",eq=contrast=1.04:saturation=0.92:brightness=0.01,colorbalance=rs=0.02:gs=-0.01:bs=-0.02");
        if (colorMatch)
            polish.Append(",eq=saturation=0.94:gamma=1.01");
        if (interpolate)
            polish.Append(",minterpolate=fps=30:mi_mode=mci:mc_mode=aobmc:vsbmc=1");
        fc.Append("[0:v]").Append(fit).Append(polish).Append(",fps=30,setsar=1,format=yuv420p[v];");
        if (keepTakeAudio)
        {
            fc.Append("[0:a]aformat=sample_rates=48000:channel_layouts=stereo,atrim=0:")
                .Append(dur.ToString("0.###", CultureInfo.InvariantCulture))
                .Append(",apad=pad_dur=0.05[a]");
        }
        else if (voices.Count == 0)
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

            var gain = VideoAudioLipsyncPipelineV1Rules.DialogueLinearGain.ToString("0.###", CultureInfo.InvariantCulture);
            if (voices.Count == 1)
                fc.Append("[a0]volume=").Append(gain).Append(",apad=pad_dur=0.05[a]");
            else
            {
                for (var i = 0; i < voices.Count; i++) fc.Append("[a").Append(i).Append(']');
                fc.Append("amix=inputs=").Append(voices.Count).Append(":normalize=0:dropout_transition=0,volume=")
                    .Append(gain).Append(",apad=pad_dur=0.05[a]");
            }
        }

        return $"-y {inputs}-filter_complex \"{fc}\" -map \"[v]\" -map \"[a]\" {t} -c:v libx264 -preset veryfast -crf 20 -c:a aac -b:a 160k -ar 48000 -ac 2 \"{dest}\"";
    }

    private static async Task<string> ApplyCanonicalMix(
        string ffmpeg,
        string cut,
        string work,
        IReadOnlyList<ContentSeriesAssembleClipDto> clips,
        ContentSeriesAssembleMixDto? mix,
        CancellationToken cancellationToken)
    {
        var dest = Path.Combine(work, "mixed.mp4");
        var total = clips.Sum(c => Math.Clamp(c.Seconds, 0.4, 20));
        if (mix is null || (!mix.Room && !mix.Music && (mix.Sfx is null || mix.Sfx.Count == 0)))
        {
            await RunFfmpeg(ffmpeg, ContentMixAssets.MasterFallbackArgs(cut, dest, total), work, cancellationToken);
            return dest;
        }

        string? room = null;
        string? music = null;
        if (mix.Room)
            room = ContentMixAssets.Resolve(ffmpeg, mix.RoomId ?? "room.night.dining", cancellationToken);
        if (mix.Music)
            music = ContentMixAssets.Resolve(ffmpeg, mix.MusicId ?? "music.bed.dim", cancellationToken);
        var sfx = new List<(string Path, int DelayMs, double GainDb)>();
        foreach (var cue in mix.Sfx ?? Array.Empty<ContentSeriesAssembleMixSfxDto>())
        {
            var path = ContentMixAssets.Resolve(ffmpeg, cue.AssetId, cancellationToken);
            sfx.Add((path, Math.Max(0, (int)Math.Round(cue.StartSec * 1000)), cue.GainDb));
        }

        await RunFfmpeg(
            ffmpeg,
            ContentMixAssets.MasterArgs(cut, dest, total, room, music, sfx, mix.Loudnorm),
            work,
            cancellationToken);
        return dest;
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

    private static double ProbeDurationSec(string ffmpeg, string src)
    {
        try
        {
            using var p = new Process();
            p.StartInfo = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = $"-hide_banner -i \"{src}\"",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            if (!p.Start()) return 0;
            var err = p.StandardError.ReadToEnd();
            p.WaitForExit(20_000);
            var m = System.Text.RegularExpressions.Regex.Match(
                err,
                @"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!m.Success) return 0;
            var h = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            var min = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            var sec = double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            return h * 3600 + min * 60 + sec;
        }
        catch
        {
            return 0;
        }
    }

    private static bool HasAudibleAudio(string ffmpeg, string src)
    {
        try
        {
            using var p = new Process();
            p.StartInfo = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = $"-hide_banner -i \"{src}\" -af volumedetect -f null -",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            if (!p.Start()) return false;
            var err = p.StandardError.ReadToEnd();
            p.WaitForExit(20_000);
            var m = System.Text.RegularExpressions.Regex.Match(
                err,
                @"max_volume:\s*(-inf|[-\d.]+)\s*dB",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!m.Success) return false;
            if (string.Equals(m.Groups[1].Value, "-inf", StringComparison.OrdinalIgnoreCase)) return false;
            return double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var db)
                   && db > -32;
        }
        catch
        {
            return false;
        }
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

    private static byte[] DecodeStill(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<byte>();
        try
        {
            var bytes = DecodeAudio(raw);
            return bytes.Length is >= 32 and <= MaxStillBytes ? bytes : Array.Empty<byte>();
        }
        catch
        {
            return Array.Empty<byte>();
        }
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
