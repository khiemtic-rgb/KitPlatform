using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentSeriesTurboService : IContentSeriesTurboService
{
    private readonly ContentFalClient _fal;
    private readonly IFamixaProviderRegistry _providers;
    private readonly IFamixaProviderSelectionService _selector;
    private readonly IContentSeriesTakeProxyService _takes;
    private readonly IVisualUniverseSnapshotResolver _snapshots;
    private readonly IUnifiedVisualCompiler _compiler;

    public ContentSeriesTurboService(
        ContentFalClient fal,
        IFamixaProviderRegistry providers,
        IFamixaProviderSelectionService selector,
        IContentSeriesTakeProxyService takes,
        IVisualUniverseSnapshotResolver snapshots,
        IUnifiedVisualCompiler compiler)
    {
        _fal = fal;
        _providers = providers;
        _selector = selector;
        _takes = takes;
        _snapshots = snapshots;
        _compiler = compiler;
    }

    public async Task<ContentSeriesTurboTaskDto> StartAsync(
        ContentSeriesTurboStartRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.Confirm)
            throw new InvalidOperationException(VideoVisualIngressV1Rules.GateConfirm);

        var decision = _selector.Select(FamixaRuntimeMaxCost.MotionRequirements(request));
        var wan = decision.ProviderId == FamixaProviderIds.Wan;
        var seconds = request.Seconds >= 8 ? 10 : 5;
        var ratio = MapRatio(request.Ratio);
        var image = NormalizeImage(request.ImageDataUrl);
        var usedPlaceholder = string.IsNullOrWhiteSpace(request.ImageDataUrl)
                              || !LooksLikeImage(request.ImageDataUrl);
        if (usedPlaceholder)
            throw new InvalidOperationException("Thiếu KF cảnh — không gửi I2V (0 cr).");

        var prompt = await CompileBoundPromptAsync(request, cancellationToken);
        if (!wan)
            image = GuardRunwayDataUri(image, ratio);

        string? lastImage = null;
        if (!wan && !string.IsNullOrWhiteSpace(request.LastFrameFromUrl))
            lastImage = await ExtractLastFrameDataUriAsync(request.LastFrameFromUrl, cancellationToken);

        var started = await _providers.GetMotion(decision.ProviderId).StartAsync(
            new FamixaMotionStartRequest(
                image,
                prompt,
                request.NegativePrompt,
                seconds,
                wan ? request.Ratio : ratio,
                lastImage),
            cancellationToken);
        var taskId = started.ProviderRequestId
                     ?? throw new InvalidOperationException("Provider I2V không trả task id.");
        return FamixaExecutionProvenanceRules.StampTurbo(
            new ContentSeriesTurboTaskDto(
                taskId,
                "PENDING",
                null,
                null,
                usedPlaceholder,
                started.ModelId ?? decision.ModelId ?? "",
                wan ? (seconds >= 8 ? 6 : 5) : seconds),
            decision);
    }

    public async Task<ContentSeriesTurboTaskDto> GetAsync(
        string taskId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new InvalidOperationException("Thiếu task id.");

        var raw = taskId.Trim();
        var identity = _providers.DescribeTask(raw);
        var polled = identity.Capability == FamixaProviderCapability.LipSync
            ? await _providers.GetLipSync(identity.ProviderId).GetAsync(raw, cancellationToken)
            : await _providers.GetMotion(identity.ProviderId).GetAsync(raw, cancellationToken);
        return await ToTurboDtoAsync(polled, identity, probe: identity.ProviderId == FamixaProviderIds.Runway, cancellationToken);
    }

    public async Task<ContentSeriesTurboTaskDto> StartLipsyncAsync(
        ContentSeriesLipsyncStartRequest request,
        CancellationToken cancellationToken = default)
    {
        var video = (request.VideoUrl ?? "").Trim();
        if (string.IsNullOrWhiteSpace(video))
            throw new InvalidOperationException("Thiếu URL take để khớp môi.");
        if (!(video.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
              || video.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
              || video.StartsWith("data:video/", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Take khớp môi phải là HTTPS hoặc data video.");

        var voiceRows = (request.Voices ?? Array.Empty<ContentSeriesLipsyncVoiceDto>())
            .Where(v => !string.IsNullOrWhiteSpace(v.AudioBase64))
            .ToList();
        if (voiceRows.Count == 0)
            voiceRows.Add(new ContentSeriesLipsyncVoiceDto(request.AudioBase64, 0, request.Mime));
        var padToPerformance = request.PerformanceDurationSec is > 0
            ? request.PerformanceDurationSec
            : request.ProductionDurationSec;
        var audioBytes = voiceRows.Count == 1 && padToPerformance is not > 0
            ? DecodeAudio(voiceRows[0].AudioBase64)
            : MergeLipsyncVoices(voiceRows, cancellationToken, padToPerformance);
        if (audioBytes.Length is < 32 or > 8_000_000)
            throw new InvalidOperationException("File thoại khớp môi không đọc được.");

        var takeBytes = await FetchTakeForLipsyncAsync(video, request.TakeTaskId, cancellationToken);
        if (takeBytes.Length is < 800 or > 12_000_000)
            throw new InvalidOperationException("Take quá lớn hoặc trống — không gửi Fal.");
        // V3: Fal receives the full performance take. Editorial trim is assemble-only.

        var videoCdn = await _fal.UploadAsync(takeBytes, "video/mp4", $"{request.ClipId}.mp4", cancellationToken);
        var audioMime = string.IsNullOrWhiteSpace(request.Mime) ? "audio/mpeg" : request.Mime.Trim();
        var audioExt = audioMime.Contains("wav", StringComparison.OrdinalIgnoreCase) ? "wav" : "mp3";
        var audioCdn = await _fal.UploadAsync(audioBytes, audioMime, $"{request.ClipId}.{audioExt}", cancellationToken);

        var decision = _selector.Select(new FamixaProviderSelectionRequirements(
            FamixaProviderCapability.LipSync,
            DurationSec: request.PerformanceDurationSec ?? request.ProductionDurationSec,
            LipsyncModel: request.Model));
        var created = await _providers.GetLipSync(decision.ProviderId).StartAsync(
            new FamixaLipSyncStartRequest(videoCdn, audioCdn, request.SyncMode, decision.ModelId),
            cancellationToken);
        return FamixaExecutionProvenanceRules.StampTurbo(
            new ContentSeriesTurboTaskDto(
                created.ProviderRequestId ?? "",
                created.Status == FamixaProviderStatus.Succeeded ? "SUCCEEDED" : "PENDING",
                created.OutputUrl,
                created.Error,
                false,
                created.ModelId ?? decision.ModelId ?? "",
                0),
            decision);
    }

    private async Task<byte[]> FetchTakeForLipsyncAsync(string video, string? takeTaskId, CancellationToken cancellationToken)
    {
        if (video.StartsWith("data:video/", StringComparison.OrdinalIgnoreCase))
            return DecodeAudio(video);

        try
        {
            return (await _takes.FetchAsync(video, cancellationToken)).Bytes;
        }
        catch (InvalidOperationException ex) when (IsExpiredTakeLink(ex.Message) && !string.IsNullOrWhiteSpace(takeTaskId))
        {
            var identity = _providers.DescribeTask(takeTaskId, capability: FamixaProviderCapability.Motion);
            var recovered = identity.Capability == FamixaProviderCapability.LipSync
                ? await _providers.GetLipSync(identity.ProviderId).RecoverAsync(takeTaskId, cancellationToken)
                : await _providers.GetMotion(identity.ProviderId).RecoverAsync(takeTaskId, cancellationToken);
            var fresh = (recovered.OutputUrl ?? "").Trim();
            if (recovered.Status != FamixaProviderStatus.Succeeded || string.IsNullOrWhiteSpace(fresh))
                throw new InvalidOperationException(
                    "Link take hết hạn. Hỏi lại task cũ không lấy được file. Không tạo video mới trừ khi Director Confirm Tạo lại.");
            return (await _takes.FetchAsync(fresh, cancellationToken)).Bytes;
        }
    }

    private async Task<ContentSeriesTurboTaskDto> ToTurboDtoAsync(
        FamixaProviderResult polled,
        FamixaProviderTaskRef identity,
        bool probe,
        CancellationToken cancellationToken)
    {
        var status = FamixaProviderTaskIdentity.ToTurboStatus(polled.Status);
        var video = polled.OutputUrl;
        var error = polled.Error;
        long? bytes = null;
        string? mime = polled.MimeType;
        var verified = false;
        if (probe && status == "SUCCEEDED" && !string.IsNullOrWhiteSpace(video))
        {
            var checkedTake = await _takes.ProbeAsync(video, cancellationToken);
            bytes = checkedTake.Bytes;
            mime = checkedTake.Mime;
            verified = checkedTake.Ok;
            if (!checkedTake.Ok)
                error = string.IsNullOrWhiteSpace(error)
                    ? $"DOWNLOAD_FAILED: {checkedTake.Error}"
                    : error;
        }
        return new ContentSeriesTurboTaskDto(
            polled.ProviderRequestId ?? "",
            status,
            video,
            error,
            false,
            polled.ModelId ?? identity.ModelId ?? "",
            0,
            polled.ErrorCode,
            bytes,
            mime,
            verified);
    }

    private static bool IsExpiredTakeLink(string? message)
    {
        var m = message ?? "";
        return m.Contains("401", StringComparison.Ordinal)
               || m.Contains("403", StringComparison.Ordinal)
               || m.Contains("hết hạn", StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] DecodeAudio(string? raw)
    {
        var s = (raw ?? "").Trim();
        if (s.Length < 32)
            throw new InvalidOperationException("Thiếu file thoại để khớp môi.");
        var comma = s.IndexOf(',');
        if (s.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
            s = s[(comma + 1)..];
        try { return Convert.FromBase64String(s); }
        catch { throw new InvalidOperationException("File thoại không đọc được."); }
    }

    private static byte[] TrimVideoToDuration(byte[] takeBytes, double durationSec, CancellationToken cancellationToken)
    {
        var t = Math.Clamp(durationSec, 0.4, 20);
        var work = Path.Combine(Path.GetTempPath(), "kit-famixa-lipsync-trim", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var src = Path.Combine(work, "take.mp4");
            var dest = Path.Combine(work, "trim.mp4");
            File.WriteAllBytes(src, takeBytes);
            cancellationToken.ThrowIfCancellationRequested();
            RunFfmpegOrThrow(
                $"-y -hide_banner -i \"{src}\" -t {t.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)} -c:v libx264 -preset veryfast -crf 20 -an \"{dest}\"",
                dest,
                "Không cắt được take về production duration trước Fal.");
            return File.ReadAllBytes(dest);
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { /* temp */ }
        }
    }

    private static byte[] MergeLipsyncVoices(
        IReadOnlyList<ContentSeriesLipsyncVoiceDto> voices,
        CancellationToken cancellationToken,
        double? productionDurationSec = null)
    {
        var work = Path.Combine(Path.GetTempPath(), "kit-famixa-lipsync-merge", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var inputs = new System.Text.StringBuilder("-y ");
            var fc = new System.Text.StringBuilder();
            for (var i = 0; i < voices.Count; i++)
            {
                var raw = DecodeAudio(voices[i].AudioBase64);
                var path = Path.Combine(work, $"v{i:00}.mp3");
                File.WriteAllBytes(path, raw);
                inputs.Append("-i \"").Append(path).Append("\" ");
                var ms = Math.Max(0, (int)Math.Round(voices[i].StartSec * 1000));
                fc.Append('[').Append(i).Append(":a]adelay=").Append(ms).Append('|').Append(ms)
                    .Append(",aformat=sample_rates=48000:channel_layouts=stereo[a").Append(i).Append("];");
            }
            for (var i = 0; i < voices.Count; i++) fc.Append("[a").Append(i).Append(']');
            var prod = productionDurationSec is > 0 ? Math.Clamp(productionDurationSec.Value, 0.4, 20) : 0;
            var prodTxt = prod.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            fc.Append("amix=inputs=").Append(voices.Count).Append(":normalize=0:dropout_transition=0");
            if (prod > 0)
                fc.Append(",atrim=0:").Append(prodTxt).Append(",apad=pad_dur=0.05[a]");
            else
                fc.Append(",apad[a]");
            var dest = Path.Combine(work, "merged.mp3");
            cancellationToken.ThrowIfCancellationRequested();
            RunFfmpegOrThrow(
                prod > 0
                    ? $"{inputs}-hide_banner -filter_complex \"{fc}\" -map \"[a]\" -t {prodTxt} -c:a libmp3lame -b:a 160k -ar 48000 -ac 2 \"{dest}\""
                    : $"{inputs}-hide_banner -filter_complex \"{fc}\" -map \"[a]\" -c:a libmp3lame -b:a 160k -ar 48000 -ac 2 \"{dest}\"",
                dest,
                "Không ghép được nhiều câu thoại trước Fal. Không bỏ câu.");
            return File.ReadAllBytes(dest);
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { /* temp */ }
        }
    }

    private static void RunFfmpegOrThrow(string arguments, string dest, string failMessage)
    {
        using var p = new System.Diagnostics.Process();
        p.StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        if (!p.Start())
            throw new InvalidOperationException(failMessage);
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(60_000))
        {
            try { p.Kill(entireProcessTree: true); } catch { /* ignore */ }
            throw new InvalidOperationException(failMessage);
        }
        try { Task.WaitAll(new Task[] { stdout, stderr }, 5_000); } catch { /* ignore */ }
        if (!p.HasExited || p.ExitCode != 0 || !File.Exists(dest))
            throw new InvalidOperationException(failMessage);
    }

    private static string MapRatio(string? raw)
    {
        var r = (raw ?? "").Trim().ToLowerInvariant();
        if (r is "9:16" or "9x16" or "720:1280") return "720:1280";
        return "1280:720";
    }

    private static bool LooksLikeImage(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var t = raw.Trim();
        return t.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)
               || t.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
               || t.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeImage(string? raw)
    {
        var t = (raw ?? "").Trim();
        return t;
    }

    /** Same path as the takes that already succeeded: data-URI (or public https). No Fal CDN — Runway often cannot HEAD fal.media. */
    private static string GuardRunwayDataUri(string image, string ratio)
    {
        if (image.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || image.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Runway chỉ nhận JPEG data-URI 1280×720 — không gửi URL/PNG gốc (0 cr).");
        }
        var bytes = DecodeDataUrl(image, out var mime);
        if (bytes.Length < 800)
            throw new InvalidOperationException("KF trống — không gửi Runway (0 cr).");
        if (bytes.Length > 5_000_000)
            throw new InvalidOperationException(
                $"KF {bytes.Length} byte — quá 5MB data-URI. F5 rồi gửi; client đã nén JPEG đúng pixel Runway.");
        var jpeg = mime.StartsWith("image/jpeg", StringComparison.OrdinalIgnoreCase)
                   || mime.StartsWith("image/jpg", StringComparison.OrdinalIgnoreCase);
        if (!jpeg || bytes[0] != 0xFF || bytes[1] != 0xD8)
            throw new InvalidOperationException("KF phải là JPEG — không gửi PNG Gemini vào Runway (0 cr).");
        if (IsTinyPngPlaceholder(bytes))
            throw new InvalidOperationException("KF placeholder 1×1 — không gửi Runway (0 cr).");
        var size = ReadImageSize(bytes);
        var (tw, th) = TargetPixels(ratio);
        if (size is not { } dim)
            throw new InvalidOperationException("Không đọc được pixel JPEG — không gửi Runway (0 cr).");
        if (dim.W != tw || dim.H != th)
            throw new InvalidOperationException(
                $"KF {dim.W}×{dim.H} — Runway cần đúng {tw}×{th} JPEG. F5 Admin — KIT normalize trước khi trừ cr.");
        return image;
    }

    private static bool IsPrivateHttpUrl(string raw)
    {
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var u)) return true;
        var host = u.Host;
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || host is "127.0.0.1" or "::1")
            return true;
        if (System.Net.IPAddress.TryParse(host, out var ip))
        {
            var b = ip.GetAddressBytes();
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && b.Length >= 4)
            {
                if (b[0] == 10) return true;
                if (b[0] == 192 && b[1] == 168) return true;
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
            }
        }
        return false;
    }

    private static (int W, int H) TargetPixels(string ratio) =>
        ratio is "720:1280" ? (720, 1280) : (1280, 720);

    private static bool AspectMatches(int w, int h, int tw, int th)
    {
        if (w <= 0 || h <= 0) return false;
        var a = w / (double)h;
        var t = tw / (double)th;
        return Math.Abs(a - t) / t <= 0.04;
    }

    private static (int W, int H)? ReadImageSize(byte[] bytes)
    {
        if (bytes.Length >= 24 && bytes[0] == 0x89 && bytes[1] == 0x50)
        {
            var w = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
            var h = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
            if (w is > 0 and <= 16_000 && h is > 0 and <= 16_000) return (w, h);
        }
        if (bytes.Length > 8 && bytes[0] == 0xFF && bytes[1] == 0xD8)
            return ReadJpegSize(bytes);
        return null;
    }

    private static (int W, int H)? ReadJpegSize(byte[] bytes)
    {
        var i = 2;
        while (i + 8 < bytes.Length)
        {
            if (bytes[i] != 0xFF)
            {
                i++;
                continue;
            }
            var marker = bytes[i + 1];
            if (marker is 0xD8 or 0xD9)
            {
                i += 2;
                continue;
            }
            if (i + 3 >= bytes.Length) break;
            var len = (bytes[i + 2] << 8) | bytes[i + 3];
            if (len < 2) break;
            if (marker is 0xC0 or 0xC1 or 0xC2)
            {
                if (i + 8 >= bytes.Length) return null;
                var h = (bytes[i + 5] << 8) | bytes[i + 6];
                var w = (bytes[i + 7] << 8) | bytes[i + 8];
                if (w > 0 && h > 0) return (w, h);
                return null;
            }
            i += 2 + len;
        }
        return null;
    }

    private static bool LooksLikeImageBytes(byte[] bytes, string mime)
    {
        if (bytes.Length < 12) return false;
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return true;
        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return true;
        if (bytes[0] is (byte)'<' or (byte)'{') return false;
        return mime.StartsWith("image/jpeg", StringComparison.OrdinalIgnoreCase)
               || mime.StartsWith("image/png", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTinyPngPlaceholder(byte[] bytes)
    {
        if (bytes.Length < 24) return false;
        if (!(bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)) return false;
        var w = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
        var h = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
        return w <= 1 || h <= 1;
    }

    private static byte[] DecodeDataUrl(string raw, out string mime)
    {
        var s = raw.Trim();
        mime = "image/jpeg";
        var comma = s.IndexOf(',');
        if (s.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
        {
            var header = s[5..comma];
            var semi = header.IndexOf(';');
            var type = (semi > 0 ? header[..semi] : header).Trim();
            if (type.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                mime = type;
            s = s[(comma + 1)..];
        }
        try { return Convert.FromBase64String(s); }
        catch { throw new InvalidOperationException("KF không đọc được — không gửi Runway (0 cr)."); }
    }

    private async Task<string> ExtractLastFrameDataUriAsync(string takeUrl, CancellationToken cancellationToken)
    {
        var take = await _takes.FetchAsync(takeUrl, cancellationToken);
        if (take.Bytes.Length < 1000)
            throw new InvalidOperationException("Last-frame: take trước trống — không gửi Runway (0 cr).");
        var ffmpeg = ResolveFfmpeg();
        if (string.IsNullOrWhiteSpace(ffmpeg))
            throw new InvalidOperationException("Last-frame cần FFmpeg trên máy API — không gửi Runway (0 cr).");
        var work = Path.Combine(Path.GetTempPath(), "kit-famixa-lastframe", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var src = Path.Combine(work, "take.mp4");
            var dest = Path.Combine(work, "last.jpg");
            await File.WriteAllBytesAsync(src, take.Bytes, cancellationToken);
            using var p = new System.Diagnostics.Process();
            p.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = $"-y -sseof -0.05 -i \"{src}\" -frames:v 1 -q:v 3 \"{dest}\"",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            p.Start();
            await p.WaitForExitAsync(cancellationToken);
            if (p.ExitCode != 0 || !File.Exists(dest))
                throw new InvalidOperationException("Không lấy được frame cuối take trước — không gửi Runway (0 cr).");
            var bytes = await File.ReadAllBytesAsync(dest, cancellationToken);
            if (bytes.Length < 32)
                throw new InvalidOperationException("Frame cuối trống — không gửi Runway (0 cr).");
            return "data:image/jpeg;base64," + Convert.ToBase64String(bytes);
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { /* temp */ }
        }
    }

    private static string? ResolveFfmpeg()
    {
        foreach (var name in new[] { "ffmpeg", "ffmpeg.exe" })
        {
            try
            {
                using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
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

    private async Task<string> CompileBoundPromptAsync(
        ContentSeriesTurboStartRequest request,
        CancellationToken cancellationToken)
    {
        var (snapshot, snapGate) = await VideoVisualIngressV1Rules.ResolveSnapshotAsync(
            _snapshots, FamixaVisualUniverseAuthorityV1Rules.ProjectId, cancellationToken);
        if (snapGate is not null || snapshot is null)
            throw new InvalidOperationException(snapGate ?? VideoVisualIngressV1Rules.GateSnapshot);
        var compiled = VideoVisualIngressV1Rules.CompileVideo(
            snapshot, VideoVisualIngressV1Rules.FromTurbo(request), _compiler);
        if (compiled.Gate is not null || compiled.Contract is null
            || !VideoVisualIngressV1Rules.ProviderMayCall(compiled.Contract))
            throw new InvalidOperationException(compiled.Gate ?? VideoVisualIngressV1Rules.GateCompile);
        return VideoVisualIngressV1Rules.RunwayI2vPrompt(
            VideoVisualIngressV1Rules.ComposeMotionLayer(VideoVisualIngressV1Rules.FromTurbo(request)));
    }

    private static string BuildPrompt(string? prompt)
    {
        var raw = (prompt ?? "").Trim();
        const string motionOnly =
            "Subtle body movement, blink and breathe. Camera remains steady.";
        if (string.IsNullOrWhiteSpace(raw)) return motionOnly;
        raw = System.Text.RegularExpressions.Regex.Replace(
            raw,
            @"\b(11-year-old|year-old|child|minor|crying|shouting)\b",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        raw = System.Text.RegularExpressions.Regex.Replace(
            raw,
            @"(?i)\b(display the text|show the words|write a prompt|on-?screen text)\b",
            " ");
        raw = System.Text.RegularExpressions.Regex.Replace(raw, @"\s+", " ").Trim();
        if (LooksLikeForbiddenI2vPrompt(raw))
            throw new InvalidOperationException(
                "I2V law — chỉ motion + camera. Không gửi mô tả lại KF / câu phủ định (0 cr).");
        if (string.IsNullOrWhiteSpace(raw)) return motionOnly;
        return raw.Length <= 900 ? raw : raw[..900];
    }

    private static bool LooksLikeForbiddenI2vPrompt(string raw) =>
        System.Text.RegularExpressions.Regex.IsMatch(
            raw,
            @"\b(no text|no captions?|no logo|watermark|do not |don't |mute take|preserve the (characters|wardrobe|room)|visual contract|fail conditions|same wardrobe|vietnamese family drama|stand in )\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
}
