using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Proves the hybrid episode concat on the tenant EP01 ASSEMBLY files.
/// Does not create Production rows and does not call ASSEMBLE_EPISODE.
/// </summary>
public static class ProductionEpisodeConcatHybridV1Regression
{
    public const string Sh01File = "6d4058f21f324ff7bf4aaa4cf91407cd.mp4";
    public const string Sh02File = "309602be95d04c7ca46b752402b28da7.mp4";

    public static async Task<ProductionEpisodeConcatHybridV1Result> RunAsync()
    {
        var failures = new List<string>();
        var proof = new List<string>();
        var before = TempDirs();
        try
        {
            var sh01 = await File.ReadAllBytesAsync(Find(Sh01File));
            var sh02 = await File.ReadAllBytesAsync(Find(Sh02File));
            var contract = ContractFor(sh01, sh02);
            var again = ContractFor(sh01, sh02);
            if (!string.Equals(contract.ContractHash, again.ContractHash, StringComparison.Ordinal)
                || contract.DurationToleranceSec != 0
                || contract.CompositionMode != ProductionEpisodeAssemblyContract.ConcatCopy
                || ProductionMixContract.Dec(contract.OutputDurationSec) != "10")
                failures.Add("contract hash, CONCAT_COPY, tolerance 0, or output 10 changed");

            var audit = new ProductionFfmpegMixAudit();
            var transport = new ProcessFfmpegMixTransport(audit);
            var request = new FfmpegEpisodeConcatRequest(
                Guid.Parse("c1c1c1c1-c1c1-c1c1-c1c1-c1c1c1c1c101"),
                Guid.Parse("c69e4bcb-c73a-47a2-8dc7-41583c2d3b5f"),
                contract,
                new[] { sh01, sh02 },
                "hybrid-proof");
            var first = await transport.ConcatCopyOnceAsync(request);
            var second = await transport.ConcatCopyOnceAsync(request);
            var sha1 = Sha(first.Bytes);
            var sha2 = Sha(second.Bytes);
            proof.Add("sha1=" + sha1);
            proof.Add("sha2=" + sha2);
            proof.Add("bytes=" + first.Bytes.Length + "/" + second.Bytes.Length);
            if (!string.Equals(sha1, sha2, StringComparison.OrdinalIgnoreCase) || !first.Bytes.AsSpan().SequenceEqual(second.Bytes))
                failures.Add("DETERMINISM file bytes differ");
            if (!ProductionTestMp4.TryReadDuration(first.Bytes, out var duration))
                failures.Add("TryReadDuration failed");
            else
            {
                proof.Add("tryRead=" + duration.ToString(CultureInfo.InvariantCulture));
                proof.Add("dec=" + ProductionMixContract.Dec(duration));
                proof.Add("decSum=" + ProductionMixContract.Dec(contract.OutputDurationSec));
                if (ProductionMixContract.Dec(duration) != ProductionMixContract.Dec(contract.OutputDurationSec)
                    || ProductionMixContract.Dec(duration) != "10")
                    failures.Add("official duration is not 10");
            }
            proof.Add("mvhd=" + ReadMvhd(first.Bytes));
            if (ReadMvhd(first.Bytes) != "10000/1000")
                failures.Add("mvhd is not 10000/1000");

            var probe = ProbeFile(first.Bytes);
            proof.Add(probe.Text);
            failures.AddRange(probe.Failures);

            var hashA = StreamHash(first.Bytes, "0:a");
            var hashB = StreamHash(second.Bytes, "0:a");
            var hashV1 = StreamHash(first.Bytes, "0:v");
            var hashV2 = StreamHash(second.Bytes, "0:v");
            proof.Add("audioHash=" + hashA + " / " + hashB);
            proof.Add("videoHash=" + hashV1 + " / " + hashV2);
            if (!string.Equals(hashA, hashB, StringComparison.Ordinal) || !string.Equals(hashV1, hashV2, StringComparison.Ordinal))
                failures.Add("DETERMINISM stream hash differs");

            await AssertFailureCleansUpAsync(transport, failures);
        }
        catch (Exception ex)
        {
            failures.Add(ex.GetType().Name + ": " + ex.Message);
        }

        var after = TempDirs();
        var leaked = after.Except(before, StringComparer.OrdinalIgnoreCase).ToArray();
        if (leaked.Length > 0)
            failures.Add("temp leftover " + string.Join(",", leaked));
        proof.Add("tempLeak=" + leaked.Length);
        return new ProductionEpisodeConcatHybridV1Result(failures, string.Join('\n', proof));
    }

    static async Task AssertFailureCleansUpAsync(ProcessFfmpegMixTransport transport, List<string> failures)
    {
        var junk = new byte[64];
        junk[0] = 0x01;
        var contract = ContractFor(junk, junk);
        var before = TempDirs();
        var threw = false;
        try
        {
            await transport.ConcatCopyOnceAsync(new FfmpegEpisodeConcatRequest(
                Guid.NewGuid(), Guid.NewGuid(), contract, new[] { junk, junk }, "hybrid-fail"));
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }
        if (!threw)
            failures.Add("invalid media did not fail closed");
        var leaked = TempDirs().Except(before, StringComparer.OrdinalIgnoreCase).ToArray();
        if (leaked.Length > 0)
            failures.Add("failure temp leftover " + string.Join(",", leaked));
    }

    static ProductionEpisodeAssemblyContractV1 ContractFor(byte[] sh01, byte[] sh02)
    {
        return ProductionEpisodeAssemblyContract.Build(
            Guid.Parse("c1c1c1c1-c1c1-c1c1-c1c1-c1c1c1c1c101"),
            "EP01",
            new[]
            {
                new ProductionEpisodeAssemblyInput(
                    0, "EP01-SC01-SH01", ProductionArtifactRoles.Assembly,
                    Guid.Parse("c69e4bcb-c73a-47a2-8dc7-41583c2d3b5f"),
                    KitVideoIntegrityRules.Sha256Hex(sh01), "os/sh01.mp4", 4, 0, 0),
                new ProductionEpisodeAssemblyInput(
                    1, "EP01-SC01-SH02", ProductionArtifactRoles.Assembly,
                    Guid.Parse("4d114dda-488a-4f24-9c4d-ef86d4e13cc7"),
                    KitVideoIntegrityRules.Sha256Hex(sh02), "os/sh02.mp4", 6, 0, 0),
            });
    }

    static (IReadOnlyList<string> Failures, string Text) ProbeFile(byte[] bytes)
    {
        var failures = new List<string>();
        var work = Path.Combine(Path.GetTempPath(), "famixa-hybrid-proof-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var path = Path.Combine(work, "out.mp4");
        try
        {
            File.WriteAllBytes(path, bytes);
            var ffprobe = ResolveTool("ffprobe") ?? ResolveTool("ffprobe.exe");
            if (ffprobe is null)
            {
                failures.Add("ffprobe missing");
                return (failures, "probe-missing");
            }
            var format = Run(ffprobe, "-v error -show_entries format=duration -of csv=p=0 \"" + path + "\"");
            var video = Run(ffprobe, "-v error -select_streams v:0 -show_entries stream=codec_name,time_base,r_frame_rate,start_pts,duration,duration_ts,nb_frames -of csv=p=0 \"" + path + "\"");
            var audio = Run(ffprobe, "-v error -select_streams a:0 -show_entries stream=codec_name,profile,sample_rate,channels,time_base,start_time,duration,duration_ts,nb_frames -of csv=p=0 \"" + path + "\"");
            var packets = Run(ffprobe, "-v error -select_streams a:0 -show_packets -show_entries packet=pts,duration -of csv=p=0 \"" + path + "\"");
            var editText = ReadEditLists(bytes);
            var text = "format=" + format.Trim() + " video=" + OneLine(video) + " audio=" + OneLine(audio);
            if (format.Trim() != "10.000000")
                failures.Add("ffprobe duration " + format.Trim());
            if (!video.Contains("1/12288", StringComparison.Ordinal) || !video.Contains("24/1", StringComparison.Ordinal)
                || !video.StartsWith("h264,", StringComparison.Ordinal) || !video.Contains(",0,", StringComparison.Ordinal)
                || !video.Contains("10.000000", StringComparison.Ordinal) || !video.Contains("122880", StringComparison.Ordinal)
                || !video.TrimEnd().EndsWith("240", StringComparison.Ordinal))
                failures.Add("video clock " + OneLine(video));
            if (!audio.Contains("aac", StringComparison.OrdinalIgnoreCase) || !audio.Contains("LC", StringComparison.Ordinal)
                || !audio.Contains("48000", StringComparison.Ordinal) || !audio.Contains(",2,", StringComparison.Ordinal)
                || !audio.Contains("480000", StringComparison.Ordinal))
                failures.Add("audio stream " + OneLine(audio));
            text += " edit=" + editText;
            var presentedEdits = editText.Split("1024/10000", StringSplitOptions.None).Length - 1;
            if (presentedEdits < 2)
                failures.Add("edit list " + editText);
            var parsed = new List<(long Pts, long Dur)>();
            foreach (var line in packets.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split(',');
                if (parts.Length >= 2 && long.TryParse(parts[0], out var pts) && long.TryParse(parts[1], out var dur))
                    parsed.Add((pts, dur));
            }
            var negative = parsed.Count(p => p.Pts < 0);
            var ones = parsed.Count(p => p.Dur == 1);
            var junction = parsed.FirstOrDefault(p => p.Pts <= 192000 && p.Pts + p.Dur > 192000);
            text += " audioPackets=" + parsed.Count + " negativePts=" + negative + " dur1=" + ones
                + " junction=" + junction.Pts + ":" + junction.Dur;
            if (negative != 1 || ones != 0 || junction.Dur != 1024)
                failures.Add("priming junction negative=" + negative + " dur1=" + ones + " junction=" + junction.Pts + ":" + junction.Dur);
            return (failures, text);
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { /* temp */ }
        }
    }

    static string ReadEditLists(byte[] bytes)
    {
        var found = new List<string>();
        for (var i = 0; i <= bytes.Length - 24; i++)
        {
            if (bytes[i] != (byte)'e' || bytes[i + 1] != (byte)'l' || bytes[i + 2] != (byte)'s' || bytes[i + 3] != (byte)'t')
                continue;
            if (bytes[i + 4] != 0) continue;
            var count = ReadU32(bytes, i + 8);
            var cursor = i + 12;
            for (var n = 0; n < count && cursor + 8 <= bytes.Length; n++)
            {
                var duration = ReadU32(bytes, cursor);
                var media = ReadU32(bytes, cursor + 4);
                found.Add(media.ToString(CultureInfo.InvariantCulture) + "/" + duration.ToString(CultureInfo.InvariantCulture));
                cursor += 12;
            }
        }
        return string.Join(",", found);
    }

    static string ReadMvhd(byte[] bytes)
    {
        for (var i = 0; i <= bytes.Length - 28; i++)
        {
            if (bytes[i] == (byte)'m' && bytes[i + 1] == (byte)'v' && bytes[i + 2] == (byte)'h' && bytes[i + 3] == (byte)'d' && bytes[i + 4] == 0)
            {
                var ts = ReadU32(bytes, i + 16);
                var dur = ReadU32(bytes, i + 20);
                return dur.ToString(CultureInfo.InvariantCulture) + "/" + ts.ToString(CultureInfo.InvariantCulture);
            }
        }
        return "none";
    }

    static uint ReadU32(byte[] bytes, int offset) =>
        ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) | ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];

    static string StreamHash(byte[] bytes, string map)
    {
        var work = Path.Combine(Path.GetTempPath(), "famixa-hybrid-hash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var path = Path.Combine(work, "out.mp4");
        try
        {
            File.WriteAllBytes(path, bytes);
            var ffmpeg = ResolveTool("ffmpeg") ?? ResolveTool("ffmpeg.exe");
            if (ffmpeg is null) return "missing";
            return OneLine(Run(ffmpeg, "-v error -i \"" + path + "\" -map " + map + " -f streamhash -hash md5 -"));
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { /* temp */ }
        }
    }

    static string Run(string file, string args)
    {
        using var p = new Process();
        p.StartInfo = new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        p.Start();
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        return string.IsNullOrWhiteSpace(stdout) ? stderr : stdout + stderr;
    }

    static string OneLine(string raw) => raw.Replace('\r', ' ').Replace('\n', ' ').Trim();

    static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    static string[] TempDirs() =>
        Directory.GetDirectories(Path.GetTempPath(), "famixa-episode-concat-*");

    static string Find(string name)
    {
        var rel = Path.Combine(
            "src", "KitPlatform.Api", "App_Data", "kit-video-i2v",
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa1", "os", name);
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var full = Path.Combine(dir.FullName, rel);
            if (File.Exists(full)) return full;
        }
        throw new FileNotFoundException(name);
    }

    static string? ResolveTool(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            try
            {
                var candidate = Path.Combine(dir, name);
                if (File.Exists(candidate)) return candidate;
            }
            catch { /* next */ }
        }
        var winget = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages");
        if (Directory.Exists(winget))
        {
            var hit = Directory.EnumerateFiles(winget, name, SearchOption.AllDirectories).FirstOrDefault();
            if (hit is not null) return hit;
        }
        return File.Exists(name) ? name : null;
    }
}

public sealed record ProductionEpisodeConcatHybridV1Result(
    IReadOnlyList<string> Failures,
    string Proof);
