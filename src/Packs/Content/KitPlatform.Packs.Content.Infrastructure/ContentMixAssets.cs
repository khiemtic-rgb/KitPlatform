using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>Procedural mix beds for Famixa assemble. Override with App_Data/content-mix/{id}.wav.</summary>
internal static class ContentMixAssets
{
    private static readonly string[] Allowed =
    {
        "room.night.dining",
        "music.bed.dim",
        "footstep",
        "paper",
        "breath",
        "phone-tap",
        "chair",
        "door",
    };

    public static string Resolve(string ffmpeg, string assetId, CancellationToken cancellationToken)
    {
        var id = (assetId ?? "").Trim().ToLowerInvariant();
        if (!Allowed.Contains(id))
            throw new InvalidOperationException($"Mix asset không hợp lệ: {assetId}");

        foreach (var root in OverrideRoots())
        {
            var custom = Path.Combine(root, id + ".wav");
            if (File.Exists(custom) && new FileInfo(custom).Length > 200) return custom;
        }

        var cache = Path.Combine(Path.GetTempPath(), "kit-famixa-mix-v1");
        Directory.CreateDirectory(cache);
        var dest = Path.Combine(cache, id + ".wav");
        if (File.Exists(dest) && new FileInfo(dest).Length > 200) return dest;
        Generate(ffmpeg, id, dest, cancellationToken);
        if (!File.Exists(dest) || new FileInfo(dest).Length < 200)
            throw new InvalidOperationException($"Không tạo được bed {id}.");
        return dest;
    }

    public static string MasterArgs(
        string cut,
        string dest,
        double totalSec,
        string? roomPath,
        string? musicPath,
        IReadOnlyList<(string Path, int DelayMs, double GainDb)> sfx,
        bool loudnorm)
    {
        var t = Math.Clamp(totalSec, 0.4, 180).ToString("0.###", CultureInfo.InvariantCulture);
        var inputs = new StringBuilder("-y -i \"").Append(cut).Append("\" ");
        var idx = 1;
        int? roomI = null;
        int? musI = null;
        if (!string.IsNullOrWhiteSpace(roomPath))
        {
            inputs.Append("-stream_loop -1 -i \"").Append(roomPath).Append("\" ");
            roomI = idx++;
        }
        if (!string.IsNullOrWhiteSpace(musicPath))
        {
            inputs.Append("-stream_loop -1 -i \"").Append(musicPath).Append("\" ");
            musI = idx++;
        }
        var sfxI = new List<int>();
        foreach (var s in sfx)
        {
            inputs.Append("-i \"").Append(s.Path).Append("\" ");
            sfxI.Add(idx++);
        }

        var fc = new StringBuilder();
        if (musI is not null)
            fc.Append("[0:a]aformat=sample_rates=48000:channel_layouts=stereo,asplit=2[voice][sc];");
        else
            fc.Append("[0:a]aformat=sample_rates=48000:channel_layouts=stereo[voice];");

        var mix = new List<string> { "[voice]" };
        if (roomI is int ri)
        {
            fc.Append('[').Append(ri).Append(":a]aformat=sample_rates=48000:channel_layouts=stereo,atrim=0:")
                .Append(t).Append(",asetpts=PTS-STARTPTS,volume=-20dB,afade=t=in:st=0:d=0.3[room];");
            mix.Add("[room]");
        }
        if (musI is int mi)
        {
            var fadeOut = Math.Max(0, totalSec - 0.4).ToString("0.###", CultureInfo.InvariantCulture);
            fc.Append('[').Append(mi).Append(":a]aformat=sample_rates=48000:channel_layouts=stereo,atrim=0:")
                .Append(t).Append(",asetpts=PTS-STARTPTS,volume=-22dB,afade=t=in:st=0:d=0.4,afade=t=out:st=")
                .Append(fadeOut).Append(":d=0.4[mus0];");
            fc.Append("[mus0][sc]sidechaincompress=threshold=0.025:ratio=8:attack=40:release=320:makeup=1[mus];");
            mix.Add("[mus]");
        }
        for (var i = 0; i < sfx.Count; i++)
        {
            var gain = sfx[i].GainDb.ToString("0.#", CultureInfo.InvariantCulture);
            fc.Append('[').Append(sfxI[i]).Append(":a]aformat=sample_rates=48000:channel_layouts=stereo,adelay=")
                .Append(sfx[i].DelayMs).Append('|').Append(sfx[i].DelayMs)
                .Append(",volume=").Append(gain).Append("dB[s").Append(i).Append("];");
            mix.Add("[s" + i + "]");
        }

        if (mix.Count == 1)
            fc.Append(loudnorm ? "[voice]loudnorm=I=-14:TP=-1.5:LRA=11[a]" : "[voice]anull[a]");
        else
        {
            foreach (var label in mix) fc.Append(label);
            fc.Append("amix=inputs=").Append(mix.Count).Append(":duration=first:dropout_transition=0:normalize=0[pre];");
            fc.Append(loudnorm ? "[pre]loudnorm=I=-14:TP=-1.5:LRA=11[a]" : "[pre]alimiter=limit=0.84[a]");
        }

        return $"{inputs}-filter_complex \"{fc}\" -map 0:v -map \"[a]\" -c:v copy -c:a aac -b:a 192k -ar 48000 -ac 2 -t {t} \"{dest}\"";
    }

    public static string MasterFallbackArgs(string cut, string dest, double totalSec)
    {
        var t = Math.Clamp(totalSec, 0.4, 180).ToString("0.###", CultureInfo.InvariantCulture);
        return $"-y -i \"{cut}\" -af \"loudnorm=I=-14:TP=-1.5:LRA=11\" -c:v copy -c:a aac -b:a 192k -ar 48000 -ac 2 -t {t} \"{dest}\"";
    }

    private static void Generate(string ffmpeg, string id, string dest, CancellationToken cancellationToken)
    {
        var args = id switch
        {
            "room.night.dining" =>
                $"-y -f lavfi -i anoisesrc=color=brown:amplitude=0.02:sample_rate=48000:duration=8 -af \"lowpass=f=380,highpass=f=40,volume=-20dB\" -ac 1 -ar 48000 \"{dest}\"",
            "music.bed.dim" =>
                $"-y -f lavfi -i sine=frequency=110:duration=24 -f lavfi -i sine=frequency=164.8:duration=24 -f lavfi -i sine=frequency=196:duration=24 -filter_complex \"[0][1][2]amix=inputs=3:duration=longest,lowpass=f=520,tremolo=f=0.12:d=0.18,volume=-24dB,afade=t=in:d=0.8,afade=t=out:st=23.2:d=0.8\" -ac 2 -ar 48000 \"{dest}\"",
            "footstep" =>
                $"-y -f lavfi -i anoisesrc=color=brown:amplitude=0.35:duration=0.12 -af \"lowpass=f=200,afade=t=in:d=0.01,afade=t=out:d=0.08,volume=-6dB\" -ac 1 -ar 48000 \"{dest}\"",
            "paper" =>
                $"-y -f lavfi -i anoisesrc=color=white:amplitude=0.22:duration=0.28 -af \"bandpass=f=3500:width_type=h:w=2500,afade=t=in:d=0.02,afade=t=out:d=0.12,volume=-10dB\" -ac 1 -ar 48000 \"{dest}\"",
            "breath" =>
                $"-y -f lavfi -i anoisesrc=color=pink:amplitude=0.1:duration=0.55 -af \"lowpass=f=700,afade=t=in:d=0.12,afade=t=out:d=0.25,volume=-16dB\" -ac 1 -ar 48000 \"{dest}\"",
            "phone-tap" =>
                $"-y -f lavfi -i sine=frequency=1400:duration=0.04 -af \"afade=t=out:d=0.03,volume=-8dB\" -ac 1 -ar 48000 \"{dest}\"",
            "chair" =>
                $"-y -f lavfi -i anoisesrc=color=brown:amplitude=0.28:duration=0.16 -af \"bandpass=f=220:width_type=h:w=80,afade=t=out:d=0.1,volume=-8dB\" -ac 1 -ar 48000 \"{dest}\"",
            "door" =>
                $"-y -f lavfi -i sine=frequency=70:duration=0.26 -af \"afade=t=in:d=0.02,afade=t=out:d=0.16,volume=-6dB\" -ac 1 -ar 48000 \"{dest}\"",
            _ => throw new InvalidOperationException($"Mix asset không hợp lệ: {id}"),
        };
        Run(ffmpeg, args, Path.GetDirectoryName(dest) ?? Path.GetTempPath(), cancellationToken);
    }

    private static IEnumerable<string> OverrideRoots()
    {
        yield return Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "content-mix");
        yield return Path.Combine(AppContext.BaseDirectory, "App_Data", "content-mix");
    }

    private static void Run(string bin, string args, string work, CancellationToken cancellationToken)
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
        var err = p.StandardError.ReadToEnd();
        p.WaitForExit(30_000);
        cancellationToken.ThrowIfCancellationRequested();
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"FFmpeg mix asset lỗi ({p.ExitCode}): {Trim(err)}");
    }

    private static string Trim(string? text)
    {
        var t = (text ?? "").Trim();
        return t.Length <= 220 ? t : t[^220..];
    }
}
