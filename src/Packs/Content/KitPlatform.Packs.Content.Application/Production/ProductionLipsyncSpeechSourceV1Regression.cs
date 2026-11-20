using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Fake Fal transport only. The timeline proof renders an isolated color fixture with local FFmpeg.
/// </summary>
public static class ProductionLipsyncSpeechSourceV1Regression
{
    public const string SuiteId = "FAMIXA_LIPSYNC_SPEECH_SOURCE_V1";
    public const decimal TakeSec = 8m;
    public const decimal SpeechStart = 2m;
    public const decimal SpeechEnd = 4.5m;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }
        try { RunAsync(Ok).GetAwaiter().GetResult(); }
        catch (Exception ex) { fail.Add("UNCAUGHT:" + ex.GetType().Name + ":" + ex.Message); }
        return fail;
    }

    static async Task RunAsync(Action<bool, string> Ok)
    {
        var ffmpeg = ProductionLipsyncSpeechSource.ResolveFfmpeg();
        Ok(ffmpeg is not null, "ffmpeg is on this machine");
        if (ffmpeg is null) return;

        var missing = await HostAsync(ffmpeg, ProductionTestMp4.Encode("no-speech", TakeSec), TakeSec, speech: null);
        var missingReason = await RejectAsync(() => missing.Svc.CreateLipsyncAsync(Cmd(missing)));
        Ok(missingReason == ProductionFailureReasons.MissingTimeline, "T1 missing speech rejected: " + missingReason);
        Ok(missing.Fal.StartCount == 0, "T1 provider not called");

        var open = await HostAsync(ffmpeg, ProductionTestMp4.Encode("open-end", TakeSec), TakeSec, speech: null, startOnly: 2m);
        var openReason = await RejectAsync(() => open.Svc.CreateLipsyncAsync(Cmd(open)));
        Ok(openReason == ProductionFailureReasons.MissingTimeline, "T1b a single speech end is not an interval: " + openReason);
        Ok(open.Fal.StartCount == 0, "T1b provider not called");

        var overflow = await HostAsync(
            ffmpeg, ProductionTestMp4.Encode("overflow", TakeSec), TakeSec, new DirectorSpeechInterval(6m, 9m));
        var overflowReason = await RejectAsync(() => overflow.Svc.CreateLipsyncAsync(Cmd(overflow)));
        Ok(overflowReason == ProductionFailureReasons.InvalidRange, "T1c speech outside the take rejected: " + overflowReason);
        Ok(overflow.Fal.StartCount == 0, "T1c provider not called");

        var undecodable = ProductionTestMp4.Encode("broken-sample", TakeSec);
        for (var i = undecodable.Length - 16; i < undecodable.Length; i++)
            undecodable[i] ^= 0xFF;
        var broken = await HostAsync(ffmpeg, undecodable, TakeSec, new DirectorSpeechInterval(SpeechStart, SpeechEnd));
        var brokenReason = await RejectAsync(() => broken.Svc.CreateLipsyncAsync(Cmd(broken)));
        Ok(brokenReason is not null
            && brokenReason.StartsWith(ProductionFailureReasons.ExecutionProcessFailed, StringComparison.Ordinal),
            "T4 trim failure rejected: " + brokenReason);
        Ok(broken.Fal.StartCount == 0, "T4 provider not called");

        var repoProof = ProofDirectory();
        Directory.CreateDirectory(repoProof);
        var takePath = Path.Combine(repoProof, "take.mp4");
        await RenderTakeAsync(ffmpeg, takePath);
        var takeBytes = await File.ReadAllBytesAsync(takePath);
        var sourceSha = KitVideoIntegrityRules.Sha256Hex(takeBytes);
        var ready = await HostAsync(ffmpeg, takeBytes, TakeSec, new DirectorSpeechInterval(SpeechStart, SpeechEnd));
        var voiceSha = KitVideoIntegrityRules.Sha256Hex(ready.VoiceBytes);
        var created = await ready.Svc.CreateLipsyncAsync(Cmd(ready));
        var sent = ready.Fal.LastRequest?.VideoBytes;
        var voiceSent = ready.Fal.LastRequest?.VoiceBytes;
        Ok(ready.Fal.StartCount == 1 && sent is { Length: >= 32 }, "T2 provider called once with video bytes");
        Ok(voiceSent is not null && KitVideoIntegrityRules.Sha256Hex(voiceSent) == voiceSha, "T2 voice bytes unchanged");
        Ok(ready.Fal.LastRequest?.SyncMode is null, "T2 sync_mode left to the existing adapter contract");
        var stored = ready.Files.Read(ready.Video.StorageKey);
        Ok(stored is not null && KitVideoIntegrityRules.Sha256Hex(stored) == sourceSha, "T3 source take hash unchanged");
        Ok(sent is not null && KitVideoIntegrityRules.Sha256Hex(sent) != sourceSha, "T2 trimmed video is not the whole take");

        var speechPath = Path.Combine(repoProof, "speech.mp4");
        if (sent is not null)
            await File.WriteAllBytesAsync(speechPath, sent);
        var speechDur = sent is null ? -1 : await ProbeDurationAsync(ffmpeg, speechPath);
        var head = await HueAsync(ffmpeg, takePath, 0.3);
        var speechOnTake = await HueAsync(ffmpeg, takePath, 2.3);
        var tail = await HueAsync(ffmpeg, takePath, 6.0);
        var speechHead = sent is null ? "missing" : await HueAsync(ffmpeg, speechPath, 0.2);
        Ok(Math.Abs(speechDur - (double)(SpeechEnd - SpeechStart)) <= 0.15, "T2 speech duration " + speechDur.ToString("0.###", CultureInfo.InvariantCulture));
        Ok(head == "red" && speechOnTake == "green" && tail == "blue",
            "fixture colors head=" + head + " speech=" + speechOnTake + " tail=" + tail);
        Ok(speechHead == "green" && speechHead != head, "T2 provider video starts at the speech, not the take head (" + speechHead + ")");

        var contract = ProductionMixContract.Build(
            ready.Video, created.Artifact,
            OfficialVeoVideoTakeAcceptanceV1.AcceptanceRole,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync),
            "fixture-timing",
            new DirectorSpeechInterval(SpeechStart, SpeechEnd));
        var placed = contract.PictureSegments.First(s => s.SourceRole == ProductionArtifactRoles.Lipsync);
        Ok(contract.OutputDurationSec == TakeSec
            && placed.DestStartSec == SpeechStart
            && placed.DestEndSec == SpeechEnd
            && placed.SourceStartSec == 0m
            && placed.SourceEndSec == SpeechEnd - SpeechStart,
            "T5 lipsync picture is the trimmed clip placed on the director window");
        Ok(contract.PictureSegments[0].SourceRole == ProductionArtifactRoles.VideoTake
            && contract.PictureSegments[0].DestStartSec == 0m
            && contract.PictureSegments[0].DestEndSec == SpeechStart
            && contract.PictureSegments[0].SourceStartSec == 0m,
            "T5 the head of the timeline is the take, not the lipsync");
        Ok(contract.AudioSegments.First(s => s.SourceRole == ProductionArtifactRoles.Lipsync).DestStartSec == SpeechStart,
            "T5 speech audio starts at the director mark");

        var mixPath = Path.Combine(repoProof, "mix.mp4");
        var command = FfmpegMixComposeGraph.CanonicalCommand(contract, [takePath, speechPath], mixPath);
        var args = command.StartsWith("ffmpeg ", StringComparison.Ordinal) ? command["ffmpeg ".Length..] : command;
        var mix = await ProductionLipsyncSpeechSource.RunAsync(ffmpeg, args, repoProof, CancellationToken.None);
        Ok(mix.Code == 0 && File.Exists(mixPath), "mix ffmpeg exit " + mix.Code + " " + One(mix.Text));
        var mixDur = File.Exists(mixPath) ? await ProbeDurationAsync(ffmpeg, mixPath) : -1;
        var mixHead = File.Exists(mixPath) ? await HueAsync(ffmpeg, mixPath, 0.3) : "missing";
        var mixSpeech = File.Exists(mixPath) ? await HueAsync(ffmpeg, mixPath, 2.3) : "missing";
        var mixTail = File.Exists(mixPath) ? await HueAsync(ffmpeg, mixPath, 6.0) : "missing";
        Ok(Math.Abs(mixDur - (double)TakeSec) <= 0.15, "mix duration " + mixDur.ToString("0.###", CultureInfo.InvariantCulture));
        Ok(mixHead == "red" && mixSpeech == "green" && mixTail == "blue",
            "mix colors head=" + mixHead + " speech=" + mixSpeech + " tail=" + mixTail);
        Ok(mixSpeech != "red", "speech slot is not the take head");

        var note = new StringBuilder();
        note.AppendLine("take=" + takePath);
        note.AppendLine("speech=" + speechPath);
        note.AppendLine("mix=" + mixPath);
        note.AppendLine("sourceSha=" + sourceSha);
        note.AppendLine("speechSha=" + (sent is null ? "" : KitVideoIntegrityRules.Sha256Hex(sent)));
        note.AppendLine("speechDurationSec=" + speechDur.ToString("0.###", CultureInfo.InvariantCulture));
        note.AppendLine("mixDurationSec=" + mixDur.ToString("0.###", CultureInfo.InvariantCulture));
        note.AppendLine("contractOutputSec=" + ProductionMixContract.Dec(contract.OutputDurationSec));
        note.AppendLine("takeHead=" + head + " takeSpeech=" + speechOnTake + " takeTail=" + tail);
        note.AppendLine("providerHead=" + speechHead);
        note.AppendLine("mixHead=" + mixHead + " mixSpeech=" + mixSpeech + " mixTail=" + mixTail);
        note.AppendLine("ffmpeg=" + ffmpeg);
        note.AppendLine("mixCommand=" + command);
        await File.WriteAllTextAsync(Path.Combine(repoProof, "proof.txt"), note.ToString());
    }

    static string ProofDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
            dir = dir.Parent;
        var root = dir?.FullName ?? Path.GetTempPath();
        return Path.Combine(root, ".dev", "famixa-lipsync-timeline-proof");
    }

    static async Task RenderTakeAsync(string ffmpeg, string takePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(takePath)!);
        var args = "-y -hide_banner -loglevel error"
            + " -f lavfi -i color=c=red:s=160x90:r=10:d=2"
            + " -f lavfi -i color=c=green:s=160x90:r=10:d=2.5"
            + " -f lavfi -i color=c=blue:s=160x90:r=10:d=3.5"
            + " -f lavfi -i anullsrc=channel_layout=stereo:sample_rate=48000"
            + " -filter_complex \"[0:v][1:v][2:v]concat=n=3:v=1:a=0[v]\""
            + " -map \"[v]\" -map 3:a -t 8"
            + " -c:v libx264 -pix_fmt yuv420p -c:a aac -b:a 128k"
            + " \"" + takePath + "\"";
        var run = await ProductionLipsyncSpeechSource.RunAsync(ffmpeg, args, Path.GetDirectoryName(takePath)!, CancellationToken.None);
        if (run.Code != 0 || !File.Exists(takePath))
            throw new InvalidOperationException("fixture ffmpeg " + run.Code + " " + One(run.Text));
    }

    static async Task<double> ProbeDurationAsync(string ffmpeg, string path)
    {
        var probe = Sibling(ffmpeg, "ffprobe") ?? Sibling(ffmpeg, "ffprobe.exe") ?? "ffprobe";
        var run = await ProductionLipsyncSpeechSource.RunAsync(
            probe,
            "-v error -show_entries format=duration -of default=nw=1:nk=1 \"" + path + "\"",
            Path.GetDirectoryName(path) ?? "",
            CancellationToken.None);
        return double.TryParse((run.Text ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : -1;
    }

    static async Task<string> HueAsync(string ffmpeg, string path, double sec)
    {
        using var p = new Process();
        p.StartInfo = new ProcessStartInfo
        {
            FileName = ffmpeg,
            Arguments = "-hide_banner -loglevel error -ss " + sec.ToString("0.0", CultureInfo.InvariantCulture)
                + " -i \"" + path + "\" -frames:v 1 -f rawvideo -pix_fmt rgb24 -",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        p.Start();
        var ms = new MemoryStream();
        await p.StandardOutput.BaseStream.CopyToAsync(ms);
        _ = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        var buf = ms.ToArray();
        const int w = 160;
        const int x = 80;
        const int y = 45;
        var i = (y * w + x) * 3;
        if (p.ExitCode != 0 || buf.Length < i + 3) return "unread";
        var r = buf[i];
        var g = buf[i + 1];
        var b = buf[i + 2];
        if (r > g + 30 && r > b + 30) return "red";
        if (g > r + 30 && g > b + 30) return "green";
        if (b > r + 30 && b > g + 30) return "blue";
        return "other:" + r.ToString(CultureInfo.InvariantCulture) + "," + g.ToString(CultureInfo.InvariantCulture) + "," + b.ToString(CultureInfo.InvariantCulture);
    }

    static string? Sibling(string ffmpeg, string name)
    {
        if (!Path.IsPathRooted(ffmpeg)) return null;
        var sibling = Path.Combine(Path.GetDirectoryName(ffmpeg) ?? "", name);
        return File.Exists(sibling) ? sibling : null;
    }

    static string One(string? text)
    {
        var line = (text ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
        return line.Length <= 240 ? line : line[..240];
    }

    static async Task<string?> RejectAsync(Func<Task> act)
    {
        try
        {
            await act();
            return null;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
    }

    static ProductionActionApiCommand Cmd(Host host) =>
        new(host.Shot, new ShotExecutionInput(
            "Recovered line", "beat", "visual", "", "", "8", "", "",
            null, "", "16:9", 8, ShotExecutionCompiler.Version, null,
            "line-SC01-CHAR-003-1", "CHAR-003", "", host.Video.ArtifactId, host.Voice.ArtifactId),
            "test", "speech-src-" + host.Shot.ShotCode + "-" + host.Nonce, AuthorizePaidNetwork: true);

    static async Task<Host> HostAsync(
        string ffmpeg,
        byte[] videoBytes,
        decimal duration,
        DirectorSpeechInterval? speech,
        decimal? startOnly = null)
    {
        _ = ffmpeg;
        var tenant = Guid.Parse("33333333-3333-4333-8333-333333333333");
        var caller = new Caller { Id = tenant };
        var store = new InMemoryProductionWorkStore(caller);
        var files = new InMemoryProductionArtifactFileStore(caller);
        var build = Guid.NewGuid();
        store.RegisterBuild(build, tenant);
        var shot = new ProductionScope(build, "EP01", "SC01", "EP01-SC01-SH04");
        var videoId = Guid.NewGuid();
        var voiceId = Guid.NewGuid();
        var voiceBytes = new byte[64];
        voiceBytes[0] = 0x49; voiceBytes[1] = 0x44; voiceBytes[2] = 0x33;
        var motion = new IdleMotion();
        var fal = new FakeFalLipsyncTransport();
        var key = new byte[ProductionCredentialSecretEnvelope.KeySize];
        RandomNumberGenerator.Fill(key);
        var ring = new MapProductionCredentialKeyRing("v1", new Dictionary<string, byte[]> { ["v1"] = key });
        var credentialStore = new InMemoryProductionCredentialStore(caller);
        var secrets = new InMemoryProductionCredentialSecretStore(caller, credentialStore, ring);
        var rotation = new InMemoryProductionCredentialRotation(credentialStore, secrets, ring);
        var registration = new ProductionCredentialRegistrationService(caller, credentialStore, rotation);
        var permits = new InMemoryProductionExecutionPermitStore();
        var snaps = new InMemoryPerformanceSnapshotStore();
        var svc = new ProductionCreateVideoService(
            new ProductionExecutionService(store, motion, credentials: new ProductionCredentialResolver(credentialStore, secrets, caller)),
            store, files, motion,
            officialPermits: permits,
            lipsync: new IdleLip(),
            officialLipsync: new FalLipSyncExecutionAdapter(fal),
            historicalVideoTakes: new OneVideo(new UnattributedVideoTake(
                videoId, ProductionArtifactRoles.VideoTake, ProductionTestMp4.Mime,
                KitVideoIntegrityRules.Sha256Hex(videoBytes), duration, "runway", "gen4_turbo", videoBytes)),
            historicalVoices: new OneVoice(new UnattributedVoice(
                voiceId, ProductionArtifactRoles.Voice, ProductionVoicePhysical.MpegMime,
                KitVideoIntegrityRules.Sha256Hex(voiceBytes), 2.5m, "elevenlabs", "eleven_v3",
                "Recovered line", "line-SC01-CHAR-003-1", voiceBytes)),
            targetShots: new Shots(build, shot.ShotCode),
            performanceSnapshots: snaps);
        var video = await svc.RegisterExistingVideoTakeAsync(new RegisterExistingVideoTakeCommand(shot, videoId, "test"));
        var voice = await svc.RegisterExistingVoiceAsync(new RegisterExistingVoiceCommand(shot, voiceId, "test"));
        if (speech is { } window || startOnly is not null)
        {
            var perf = new PerformanceContractService(snaps);
            var draft = perf.CreateDraft(ProductionTestPerformanceFixture.Source(build, shot.ShotCode));
            var source = new PerformanceSourceRef(
                true, [true], false, null,
                duration,
                ProductionDirectorTiming.Source,
                ProductionDirectorTiming.DecisionType,
                SpeechStartSec: speech?.StartSec ?? startOnly,
                SpeechEndSec: speech?.EndSec);
            await perf.FreezeAsync(draft, "regression", source);
        }
        var falId = ProductionLipsyncCatalog.FalProviderId;
        var fal3 = ProductionLipsyncCatalog.SyncLipsyncV3;
        await permits.IssueAsync(new IssueProductionExecutionPermitCommand(
            build, shot.ShotCode, "EP01", "SC01", ProductionActions.CreateLipsync, falId, fal3, "test",
            shot.ShotCode + ":" + Guid.NewGuid().ToString("N")));
        await registration.RegisterAsync(new RegisterProductionCredentialCommand(falId, fal3, "fal-test-secret", "fal-test"));
        return new Host(svc, fal, files, shot, video, voice, voiceBytes, Guid.NewGuid().ToString("N"));
    }

    sealed record Host(
        ProductionCreateVideoService Svc,
        FakeFalLipsyncTransport Fal,
        InMemoryProductionArtifactFileStore Files,
        ProductionScope Shot,
        ProductionArtifact Video,
        ProductionArtifact Voice,
        byte[] VoiceBytes,
        string Nonce);

    sealed class Caller : IProductionCallerTenant
    {
        public Guid Id { get; set; }
        public Guid RequireTenant() => ProductionTenantBoundary.Require(Id);
    }

    sealed class Shots(Guid buildId, string code) : IProductionTargetShotCheck
    {
        public Task RequireAsync(Guid build, string shotCode, CancellationToken ct = default)
        {
            if (build == buildId && !string.Equals(shotCode, code, StringComparison.Ordinal))
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
            return Task.CompletedTask;
        }
    }

    sealed class OneVideo(UnattributedVideoTake take) : IUnattributedVideoTakeSource
    {
        public Task<UnattributedVideoTake?> ReadAsync(Guid artifactId, CancellationToken ct = default) =>
            Task.FromResult<UnattributedVideoTake?>(take.ArtifactId == artifactId ? take : null);
    }

    sealed class OneVoice(UnattributedVoice voice) : IUnattributedVoiceSource
    {
        public Task<UnattributedVoice?> ReadAsync(Guid artifactId, CancellationToken ct = default) =>
            Task.FromResult<UnattributedVoice?>(voice.ArtifactId == artifactId ? voice : null);
    }

    sealed class IdleMotion : IMotionExecutionAdapter
    {
        public string ProviderId => "runway";
        public string ModelId => "gen4_turbo";
        public bool IsPaid => true;
        public bool RequiresNetwork => true;
        public Task<MotionStartResult> StartFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default) =>
            Task.FromResult(new MotionStartResult("no"));
        public Task<MotionPollResult> PollFrozenAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default) =>
            Task.FromResult(new MotionPollResult(MotionPollStatuses.Failed, providerJobId));
        public Task<MotionPollResult> RecoverFrozenAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default) =>
            Task.FromResult(new MotionPollResult(MotionPollStatuses.Failed, providerJobId));
        public Task<MotionAdapterResult> FetchOutputAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default) =>
            Task.FromResult(new MotionAdapterResult(providerJobId, [], "video/mp4", 0, "os/no"));
        public Task<MotionAdapterResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default) =>
            Task.FromResult(new MotionAdapterResult("no", [], "video/mp4", 0, "os/no"));
    }

    sealed class IdleLip : ILipSyncExecutionAdapter
    {
        public string ProviderId => "fixture";
        public string ModelId => "fixture-lipsync-v1";
        public bool IsPaid => false;
        public bool RequiresNetwork => false;
        public Task<ProductionMediaResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default) =>
            Task.FromResult(new ProductionMediaResult("no", [], "video/mp4", 0));
    }
}
