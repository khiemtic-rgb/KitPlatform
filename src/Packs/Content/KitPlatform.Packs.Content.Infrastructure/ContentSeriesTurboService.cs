using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentSeriesTurboService : IContentSeriesTurboService
{
    private readonly ContentRunwayClient _runway;
    private readonly ContentFalClient _fal;
    private readonly IContentSeriesTakeProxyService _takes;

    public ContentSeriesTurboService(
        ContentRunwayClient runway,
        ContentFalClient fal,
        IContentSeriesTakeProxyService takes)
    {
        _runway = runway;
        _fal = fal;
        _takes = takes;
    }

    public async Task<ContentSeriesTurboTaskDto> StartAsync(
        ContentSeriesTurboStartRequest request,
        CancellationToken cancellationToken = default)
    {
        var wan = IsWan(request.Engine);
        var seconds = request.Seconds >= 8 ? 10 : 5;
        var ratio = MapRatio(request.Ratio);
        var image = NormalizeImage(request.ImageDataUrl);
        var usedPlaceholder = string.IsNullOrWhiteSpace(request.ImageDataUrl)
                              || !LooksLikeImage(request.ImageDataUrl);
        if (usedPlaceholder)
            throw new InvalidOperationException("Thiếu KF cảnh — không gửi I2V (0 cr).");

        var prompt = BuildPrompt(request.Prompt);
        if (!wan)
            image = GuardRunwayDataUri(image, ratio);
        if (wan)
        {
            var wanId = await _fal.CreateImageToVideoAsync(
                image,
                prompt,
                request.NegativePrompt,
                seconds,
                request.Ratio,
                cancellationToken);
            return new ContentSeriesTurboTaskDto(
                wanId,
                "PENDING",
                null,
                null,
                usedPlaceholder,
                ContentFalClient.WanModel,
                seconds >= 8 ? 6 : 5);
        }

        var taskId = await _runway.CreateImageToVideoAsync(image, prompt, seconds, ratio, cancellationToken);
        return new ContentSeriesTurboTaskDto(
            taskId,
            "PENDING",
            null,
            null,
            usedPlaceholder,
            ContentRunwayClient.TurboModel,
            seconds);
    }

    public async Task<ContentSeriesTurboTaskDto> GetAsync(
        string taskId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new InvalidOperationException("Thiếu task id.");

        var id = taskId.Trim();
        if (ContentFalClient.IsFalTask(id))
        {
            var fal = await _fal.GetTaskAsync(id, cancellationToken);
            return new ContentSeriesTurboTaskDto(
                id,
                fal.Status,
                fal.VideoUrl,
                fal.Error,
                false,
                ContentFalClient.IsLipsyncTask(id) ? ContentFalClient.LipsyncModel : ContentFalClient.WanModel,
                0);
        }

        var (status, video, error, failureCode) = await _runway.GetTaskAsync(id, cancellationToken);
        long? bytes = null;
        string? mime = null;
        var verified = false;
        if (status == "SUCCEEDED" && !string.IsNullOrWhiteSpace(video))
        {
            var probe = await _takes.ProbeAsync(video, cancellationToken);
            bytes = probe.Bytes;
            mime = probe.Mime;
            verified = probe.Ok;
            if (!probe.Ok)
                error = string.IsNullOrWhiteSpace(error)
                    ? $"DOWNLOAD_FAILED: {probe.Error}"
                    : error;
        }
        return new ContentSeriesTurboTaskDto(
            id,
            status,
            video,
            error,
            false,
            ContentRunwayClient.TurboModel,
            0,
            failureCode,
            bytes,
            mime,
            verified);
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

        var audioBytes = DecodeAudio(request.AudioBase64);
        if (audioBytes.Length is < 32 or > 4_000_000)
            throw new InvalidOperationException("File thoại khớp môi không đọc được.");

        byte[] takeBytes;
        if (video.StartsWith("data:video/", StringComparison.OrdinalIgnoreCase))
        {
            takeBytes = DecodeAudio(video);
        }
        else
        {
            var take = await _takes.FetchAsync(video, cancellationToken);
            takeBytes = take.Bytes;
        }
        if (takeBytes.Length is < 800 or > 12_000_000)
            throw new InvalidOperationException("Take quá lớn hoặc trống — không gửi Fal.");

        var videoCdn = await _fal.UploadAsync(takeBytes, "video/mp4", $"{request.ClipId}.mp4", cancellationToken);
        var audioMime = string.IsNullOrWhiteSpace(request.Mime) ? "audio/mpeg" : request.Mime.Trim();
        var audioExt = audioMime.Contains("wav", StringComparison.OrdinalIgnoreCase) ? "wav" : "mp3";
        var audioCdn = await _fal.UploadAsync(audioBytes, audioMime, $"{request.ClipId}.{audioExt}", cancellationToken);

        var created = await _fal.CreateLipsyncAsync(videoCdn, audioCdn, request.SyncMode, cancellationToken);
        return new ContentSeriesTurboTaskDto(
            created.TaskId,
            created.Status,
            created.VideoUrl,
            null,
            false,
            ContentFalClient.LipsyncModel,
            0);
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

    private static bool IsWan(string? engine) =>
        string.Equals((engine ?? "").Trim(), "wan", StringComparison.OrdinalIgnoreCase);

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

    private static string BuildPrompt(string? prompt)
    {
        var raw = (prompt ?? "").Trim();
        const string safe =
            "Cinematic live-action. The photo is the first frame only. " +
            "Start motion right away: blink, breathe, small natural movement. Keep the same faces and clothes. No captions.";
        if (string.IsNullOrWhiteSpace(raw)) return safe;
        raw = System.Text.RegularExpressions.Regex.Replace(
            raw,
            @"\b(11-year-old|year-old|child|minor|crying|shouting)\b",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        raw = System.Text.RegularExpressions.Regex.Replace(raw, @"\s+", " ").Trim();
        return raw.Length <= 980 ? raw : raw[..980];
    }
}
