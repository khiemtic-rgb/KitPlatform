using System.Diagnostics;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Server FFmpeg MIX transport. RemuxOnceAsync copies one LIPSYNC input (SH01).
/// ComposeSegmentsOnceAsync executes a frozen MIX_CONTRACT_V1. Does not call any AI provider. No retry.
/// </summary>
internal sealed class ProcessFfmpegMixTransport : IFfmpegMixTransport
{
    public const string CanonicalCommand =
        "ffmpeg -y -hide_banner -loglevel error -i input.mp4 -map 0:v:0 -map 0:a:0 -c copy -movflags +faststart output.mp4";

    readonly ProductionFfmpegMixAudit _audit;

    public ProcessFfmpegMixTransport(ProductionFfmpegMixAudit audit) => _audit = audit;

    public bool RequiresNetwork => false;
    public int ExecutionCount { get; private set; }
    public int ComposeExecutionCount { get; private set; }
    public int ConcatExecutionCount { get; private set; }
    public string? LastCommand { get; private set; }
    public string? LastComposeCommand { get; private set; }
    public string? LastConcatCommand { get; private set; }

    public async Task<FfmpegMixTransportResult> RemuxOnceAsync(
        FfmpegMixTransportRequest request, CancellationToken ct = default)
    {
        if (request.LipsyncBytes is not { Length: >= 32 })
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        var ffmpeg = ResolveFfmpeg()
            ?? throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity + ":ffmpeg-missing");

        var work = Path.Combine(Path.GetTempPath(), "famixa-mix-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var input = Path.Combine(work, "input.mp4");
        var output = Path.Combine(work, "output.mp4");
        try
        {
            await File.WriteAllBytesAsync(input, request.LipsyncBytes, ct);
            ExecutionCount++;
            LastCommand = CanonicalCommand;
            _audit.ExecutionCount++;
            _audit.LastCommand = CanonicalCommand;

            var args = "-y -hide_banner -loglevel error -i \"" + input + "\" -map 0:v:0 -map 0:a:0 -c copy -movflags +faststart \"" + output + "\"";
            var (code, err) = await RunAsync(ffmpeg, args, work, ct);
            if (code != 0 || !File.Exists(output))
                throw new InvalidOperationException(
                    ProductionFailureReasons.FailedIdentity + ":ffmpeg:" + Truncate(err));

            var bytes = await File.ReadAllBytesAsync(output, ct);
            if (bytes.Length < 32)
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
            if (!await HasVideoAndAudioAsync(ffmpeg, output, ct))
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity + ":missing-av-stream");

            var job = "ffmpeg-" + request.AttemptId.ToString("N");
            return new FfmpegMixTransportResult(job, bytes, ProductionTestMp4.Mime, CanonicalCommand);
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { /* temp */ }
        }
    }

    public async Task<FfmpegMixTransportResult> ComposeSegmentsOnceAsync(
        FfmpegMixComposeRequest request, CancellationToken ct = default)
    {
        var err = ProductionMixContract.Validate(request.Contract);
        if (err is not null)
            throw new InvalidOperationException(err);
        if (request.ParentBytes.Count != request.Contract.Parents.Count)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        for (var i = 0; i < request.Contract.Parents.Count; i++)
        {
            var bytes = request.ParentBytes[i];
            if (bytes is not { Length: >= 32 })
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
            if (ProductionMixContract.NormSha(KitVideoIntegrityRules.Sha256Hex(bytes))
                != ProductionMixContract.NormSha(request.Contract.Parents[i].Sha256))
                throw new InvalidOperationException(ProductionFailureReasons.HashChanged);
        }
        if (string.Equals(request.Contract.CompositionMode, ProductionMixContract.VoiceOverStill, StringComparison.Ordinal))
            return await HoldVoiceOverStillAsync(request, ct);

        var ffmpeg = ResolveFfmpeg()
            ?? throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity + ":ffmpeg-missing");
        var work = Path.Combine(Path.GetTempPath(), "famixa-mix-compose-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var output = Path.Combine(work, "output.mp4");
        try
        {
            var inputPaths = new List<string>();
            for (var i = 0; i < request.ParentBytes.Count; i++)
            {
                var path = Path.Combine(work, "in" + i + ".mp4");
                await File.WriteAllBytesAsync(path, request.ParentBytes[i], ct);
                inputPaths.Add(path);
            }
            var command = FfmpegMixComposeGraph.CanonicalCommand(request.Contract, inputPaths, output);
            ComposeExecutionCount++;
            LastComposeCommand = command;
            _audit.ComposeExecutionCount++;
            _audit.LastComposeCommand = command;

            var args = command.StartsWith("ffmpeg ", StringComparison.Ordinal)
                ? command["ffmpeg ".Length..]
                : command;
            var (code, stderr) = await RunAsync(ffmpeg, args, work, ct);
            if (code != 0 || !File.Exists(output))
                throw new InvalidOperationException(
                    ProductionFailureReasons.FailedIdentity + ":ffmpeg-compose:" + Truncate(stderr));

            var bytes = await File.ReadAllBytesAsync(output, ct);
            if (bytes.Length < 32)
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
            if (!await HasVideoAndAudioAsync(ffmpeg, output, ct))
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity + ":missing-av-stream");
            if (!ProductionTestMp4.TryReadDuration(bytes, out var duration)
                || ProductionMixContract.Dec(duration) != ProductionMixContract.Dec(request.Contract.OutputDurationSec))
                throw new InvalidOperationException(ProductionFailureReasons.DurationMismatch);

            var job = "ffmpeg-compose-" + request.AttemptId.ToString("N");
            return new FfmpegMixTransportResult(job, bytes, ProductionTestMp4.Mime, command);
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { /* temp */ }
        }
    }

    async Task<FfmpegMixTransportResult> HoldVoiceOverStillAsync(
        FfmpegMixComposeRequest request, CancellationToken ct)
    {
        if (request.ParentBytes.Count != 2)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        var ffmpeg = ResolveFfmpeg()
            ?? throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity + ":ffmpeg-missing");
        var work = Path.Combine(Path.GetTempPath(), "famixa-mix-still-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var still = Path.Combine(work, "still");
        var voice = Path.Combine(work, "voice");
        var output = Path.Combine(work, "output.mp4");
        try
        {
            await File.WriteAllBytesAsync(still, request.ParentBytes[0], ct);
            await File.WriteAllBytesAsync(voice, request.ParentBytes[1], ct);
            var command = FfmpegMixComposeGraph.CanonicalCommand(request.Contract, [still, voice], output);
            ComposeExecutionCount++;
            LastComposeCommand = command;
            _audit.ComposeExecutionCount++;
            _audit.LastComposeCommand = command;
            var args = command.StartsWith("ffmpeg ", StringComparison.Ordinal)
                ? command["ffmpeg ".Length..]
                : command;
            var (code, stderr) = await RunAsync(ffmpeg, args, work, ct);
            if (code != 0 || !File.Exists(output))
                throw new InvalidOperationException(
                    ProductionFailureReasons.FailedIdentity + ":ffmpeg-still:" + Truncate(stderr));
            var bytes = await File.ReadAllBytesAsync(output, ct);
            if (bytes.Length < 32)
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
            if (!await HasVideoAndAudioAsync(ffmpeg, output, ct))
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity + ":missing-av-stream");
            if (!ProductionTestMp4.TryReadDuration(bytes, out var duration)
                || ProductionMixContract.Dec(duration) != ProductionMixContract.Dec(request.Contract.OutputDurationSec))
                throw new InvalidOperationException(ProductionFailureReasons.DurationMismatch);
            var job = "ffmpeg-still-" + request.AttemptId.ToString("N");
            return new FfmpegMixTransportResult(job, bytes, ProductionTestMp4.Mime, command);
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { /* temp */ }
        }
    }

    public const string ConcatCanonicalCommand =
        "ffmpeg -y -hide_banner -loglevel error -copyts -f concat -safe 0 -i video-list.txt -an -c:v copy video.mp4"
        + " && ffmpeg -y -hide_banner -loglevel error -i inN.mp4 -filter_complex atrim=end=PRESENTED,asetpts=PTS-STARTPTS,concat -map [a] -c:a aac -b:a 192k -ar 48000 -ac 2 audio.m4a"
        + " && ffmpeg -y -hide_banner -loglevel error -i video.mp4 -i audio.m4a -map 0:v:0 -map 1:a:0 -c copy -movflags +faststart output.mp4";

    public Task<FfmpegMixTransportResult> ConcatCopyOnceAsync(
        FfmpegEpisodeConcatRequest request, CancellationToken ct = default)
        => ConcatEpisodeHybridOnceAsync(request, ct);

    async Task<FfmpegMixTransportResult> ConcatEpisodeHybridOnceAsync(
        FfmpegEpisodeConcatRequest request, CancellationToken ct)
    {
        var err = ProductionEpisodeAssemblyContract.Validate(request.Contract);
        if (err is not null)
            throw new InvalidOperationException(err);
        if (request.ParentBytes.Count != request.Contract.OrderedInputs.Count)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        for (var i = 0; i < request.Contract.OrderedInputs.Count; i++)
        {
            var bytes = request.ParentBytes[i];
            if (bytes is not { Length: >= 32 })
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
            if (ProductionMixContract.NormSha(KitVideoIntegrityRules.Sha256Hex(bytes))
                != ProductionMixContract.NormSha(request.Contract.OrderedInputs[i].Sha256))
                throw new InvalidOperationException(ProductionFailureReasons.HashChanged);
        }

        var ffmpeg = ResolveFfmpeg()
            ?? throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity + ":ffmpeg-missing");
        var work = Path.Combine(Path.GetTempPath(), "famixa-episode-concat-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var list = Path.Combine(work, "video-list.txt");
        var videoOut = Path.Combine(work, "video.mp4");
        var audioOut = Path.Combine(work, "audio.m4a");
        var output = Path.Combine(work, "output.mp4");
        try
        {
            var lines = new List<string>();
            var inputs = new System.Text.StringBuilder();
            for (var i = 0; i < request.ParentBytes.Count; i++)
            {
                var path = Path.Combine(work, "in" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".mp4");
                await File.WriteAllBytesAsync(path, request.ParentBytes[i], ct);
                lines.Add("file '" + path.Replace("'", "'\\''") + "'");
                inputs.Append("-i \"").Append(path).Append("\" ");
            }
            await File.WriteAllLinesAsync(list, lines, ct);
            ConcatExecutionCount++;
            LastConcatCommand = ConcatCanonicalCommand;
            _audit.ConcatExecutionCount++;
            _audit.LastConcatCommand = ConcatCanonicalCommand;

            var videoArgs = "-y -hide_banner -loglevel error -copyts -f concat -safe 0 -i \"" + list
                + "\" -an -c:v copy \"" + videoOut + "\"";
            var (videoCode, videoErr) = await RunAsync(ffmpeg, videoArgs, work, ct);
            if (videoCode != 0 || !File.Exists(videoOut))
                throw new InvalidOperationException(
                    ProductionFailureReasons.FailedIdentity + ":ffmpeg-video:" + Truncate(videoErr));

            var audioArgs = "-y -hide_banner -loglevel error " + inputs
                + "-filter_complex \"" + PresentedAudioFilter(request.Contract) + "\" -map \"[a]\" -c:a aac -b:a 192k -ar 48000 -ac 2 \""
                + audioOut + "\"";
            var (audioCode, audioErr) = await RunAsync(ffmpeg, audioArgs, work, ct);
            if (audioCode != 0 || !File.Exists(audioOut))
                throw new InvalidOperationException(
                    ProductionFailureReasons.FailedIdentity + ":ffmpeg-audio:" + Truncate(audioErr));

            var muxArgs = "-y -hide_banner -loglevel error -i \"" + videoOut + "\" -i \"" + audioOut
                + "\" -map 0:v:0 -map 1:a:0 -c copy -movflags +faststart \"" + output + "\"";
            var (muxCode, muxErr) = await RunAsync(ffmpeg, muxArgs, work, ct);
            if (muxCode != 0 || !File.Exists(output))
                throw new InvalidOperationException(
                    ProductionFailureReasons.FailedIdentity + ":ffmpeg-mux:" + Truncate(muxErr));

            var bytesOut = await File.ReadAllBytesAsync(output, ct);
            if (bytesOut.Length < 32)
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
            if (!await HasVideoAndAudioAsync(ffmpeg, output, ct))
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity + ":missing-av-stream");
            if (!ProductionTestMp4.TryReadDuration(bytesOut, out var duration)
                || ProductionMixContract.Dec(duration) != ProductionMixContract.Dec(request.Contract.OutputDurationSec))
                throw new InvalidOperationException(ProductionFailureReasons.DurationMismatch);
            if (!await VideoStartsAtZeroAsync(ffmpeg, output, ct))
                throw new InvalidOperationException(ProductionFailureReasons.DurationMismatch);

            var job = "ffmpeg-concat-" + request.AttemptId.ToString("N");
            return new FfmpegMixTransportResult(job, bytesOut, ProductionTestMp4.Mime, ConcatCanonicalCommand);
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { /* temp */ }
        }
    }

    static string PresentedAudioFilter(ProductionEpisodeAssemblyContractV1 contract)
    {
        var parts = new List<string>();
        var labels = new System.Text.StringBuilder();
        for (var i = 0; i < contract.OrderedInputs.Count; i++)
        {
            var label = "a" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            parts.Add("[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ":a]atrim=end=" + ProductionMixContract.Dec(contract.OrderedInputs[i].DurationSec)
                + ",asetpts=PTS-STARTPTS[" + label + "]");
            labels.Append('[').Append(label).Append(']');
        }
        parts.Add(labels + "concat=n="
            + contract.OrderedInputs.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + ":v=0:a=1[a]");
        return string.Join(';', parts);
    }

    static async Task<bool> VideoStartsAtZeroAsync(string ffmpeg, string path, CancellationToken ct)
    {
        var probe = ResolveSibling(ffmpeg, "ffprobe") ?? ResolveSibling(ffmpeg, "ffprobe.exe");
        if (probe is null) return true;
        var dir = Path.GetDirectoryName(path) ?? "";
        var start = await RunAsync(probe,
            "-v error -select_streams v:0 -show_entries stream=start_pts -of csv=p=0 \"" + path + "\"", dir, ct);
        return start.Code == 0 && string.Equals((start.Text ?? "").Trim(), "0", StringComparison.Ordinal);
    }

    static async Task<bool> HasVideoAndAudioAsync(string ffmpeg, string path, CancellationToken ct)
    {
        var probe = ResolveSibling(ffmpeg, "ffprobe") ?? ResolveSibling(ffmpeg, "ffprobe.exe");
        if (probe is null) return ProductionTestMp4.IsDecodable(await File.ReadAllBytesAsync(path, ct), out _);
        var dir = Path.GetDirectoryName(path) ?? "";
        var v = await RunAsync(probe, "-v error -select_streams v:0 -show_entries stream=codec_type -of csv=p=0 \"" + path + "\"", dir, ct);
        var a = await RunAsync(probe, "-v error -select_streams a:0 -show_entries stream=codec_type -of csv=p=0 \"" + path + "\"", dir, ct);
        return v.Code == 0 && (v.Text ?? "").Contains("video", StringComparison.OrdinalIgnoreCase)
            && a.Code == 0 && (a.Text ?? "").Contains("audio", StringComparison.OrdinalIgnoreCase);
    }

    static async Task<(int Code, string Text)> RunAsync(string file, string args, string work, CancellationToken ct)
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
        return (p.ExitCode, string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
    }

    static string? ResolveFfmpeg()
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

    static string? ResolveSibling(string ffmpeg, string name)
    {
        if (Path.IsPathRooted(ffmpeg))
        {
            var sibling = Path.Combine(Path.GetDirectoryName(ffmpeg) ?? "", name);
            if (File.Exists(sibling)) return sibling;
        }
        return File.Exists(name) ? name : null;
    }

    static string Truncate(string? raw)
    {
        var t = (raw ?? "").Trim().Replace('\r', ' ').Replace('\n', ' ');
        return t.Length <= 180 ? t : t[..180];
    }
}
