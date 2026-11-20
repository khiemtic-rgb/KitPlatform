using System.Globalization;
using System.Text.Json.Serialization;

namespace KitPlatform.Packs.Content.Production;

/// <summary>Khung chữ nhật trên khung hình (điểm ảnh).</summary>
public sealed record LinesheetLipRect(int X, int Y, int W, int H)
{
    public override string ToString() => $"{X},{Y},{W},{H}";
}

/// <summary>Một câu thoại có thể ghép môi: khuôn mặt người nói trong shot, khung đầu và cuối (tính trong shot).</summary>
public sealed record LinesheetLipItem(LinesheetRow Row, LinesheetFileRecord Voice, LinesheetLipRect Rect, int F0, int F1, int LeadMs, string KeyBase)
{
    public int Frames => F1 - F0;
}

/// <summary>Đoạn ghép môi đã có, đặt lên đoạn chuyển động khi dựng.</summary>
public sealed record LinesheetPlacedLip(string Mp4, int F0, int F1, LinesheetLipRect Rect);

/// <summary>Đoạn ghép môi đã tạo cho một câu (lưu trong lipsync/manifest.json, theo mã câu).</summary>
public sealed class LinesheetLipsyncEntry
{
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("storage_key")] public string StorageKey { get; set; } = "";
    [JsonPropertyName("shot_id")] public string ShotId { get; set; } = "";
    [JsonPropertyName("model")] public string Model { get; set; } = "";
    [JsonPropertyName("fps")] public int Fps { get; set; }
    [JsonPropertyName("seconds")] public double Seconds { get; set; }
    [JsonPropertyName("sha256")] public string Sha256 { get; set; } = "";
}

/// <summary>
/// Ghép môi theo lời (thêm 2026-11-04). Video AI không nghe được tiếng nên môi động lung tung. Cách làm: với mỗi câu,
/// cắt vùng mặt người nói từ đoạn chuyển động đã canh đúng khung của shot, gửi cùng tiếng của câu sang mô hình ghép môi,
/// rồi đặt vùng mặt đã ghép trở lại đúng chỗ, mép làm mờ dần. Chỉ một khuôn mặt trong mỗi đoạn gửi đi nên mô hình không
/// chọn nhầm người. Khuôn mặt lấy từ settings.faces theo ảnh cảnh.
/// </summary>
public static class LinesheetLipsyncPlan
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Cỡ khung khi tạo đoạn ghép môi (giống đoạn chuyển động). Bản dựng cỡ khác thì đổi tỉ lệ vùng mặt.</summary>
    public const int BaseWidth = 1280, BaseHeight = 720;

    /// <summary>Vùng mặt phóng lên cạnh này trước khi gửi (mô hình làm việc tốt ở khoảng 512 điểm ảnh).</summary>
    public const int SendSide = 512;

    /// <summary>Thêm vài khung trước và sau câu để môi kịp mở, kịp khép.</summary>
    public const int PadFrames = 3;

    /// <summary>Thời gian mờ dần vùng ghép môi khi bắt đầu và kết thúc câu (giây).</summary>
    public const double FadeSeconds = 0.16;

    /// <summary>Vùng cắt rộng gấp chừng này so với khung mặt (lấy cả cằm, má, một phần tóc).</summary>
    public const double Margin = 1.7;

    /// <summary>Cỡ ảnh PNG hoặc JPEG, đọc từ phần đầu file.</summary>
    public static (int W, int H)? ImageSize(string path)
    {
        try
        {
            using var f = File.OpenRead(path);
            var head = new byte[24];
            if (f.Read(head, 0, 24) < 24) return null;
            if (head[0] == 0x89 && head[1] == 0x50)
                return ((head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19],
                        (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23]);
            if (head[0] != 0xFF || head[1] != 0xD8) return null;
            f.Position = 2;
            while (f.Position < f.Length)
            {
                if (f.ReadByte() != 0xFF) continue;
                int marker;
                do marker = f.ReadByte(); while (marker == 0xFF);
                if (marker < 0) return null;
                var len = (f.ReadByte() << 8) | f.ReadByte();
                if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
                {
                    f.ReadByte();
                    var h = (f.ReadByte() << 8) | f.ReadByte();
                    var w = (f.ReadByte() << 8) | f.ReadByte();
                    return (w, h);
                }

                f.Position += len - 2;
            }
        }
        catch (IOException)
        {
        }

        return null;
    }

    static int Even(double v) => Math.Max(2, (int)Math.Round(v / 2) * 2);

    /// <summary>
    /// Khung mặt trên ảnh cảnh (điểm ảnh của ảnh) thành vùng cắt vuông trên khung hình ra: ảnh cảnh được phóng phủ kín rồi
    /// cắt giữa, giống <see cref="LinesheetMotionPlan.StillArgs"/>.
    /// </summary>
    public static LinesheetLipRect FaceRect(int[] box, int imageW, int imageH, int outW, int outH)
    {
        var scale = Math.Max(outW / (double)imageW, outH / (double)imageH);
        var ox = (imageW * scale - outW) / 2;
        var oy = (imageH * scale - outH) / 2;
        var cx = (box[0] + box[2] / 2.0) * scale - ox;
        var cy = (box[1] + box[3] / 2.0) * scale - oy;
        var side = Math.Min(Even(Math.Max(box[2], box[3]) * scale * Margin), Math.Min(outW, outH) / 2 * 2);
        var x = Math.Clamp(Even(cx - side / 2.0), 0, outW - side);
        var y = Math.Clamp(Even(cy - side / 2.0), 0, outH - side);
        return new LinesheetLipRect(x, y, side, side);
    }

    /// <summary>Vùng mặt ở cỡ tạo đoạn (1280×720) đổi sang cỡ bản dựng.</summary>
    public static LinesheetLipRect Scale(LinesheetLipRect r, int width, int height)
    {
        if (width == BaseWidth && height == BaseHeight) return r;
        double sx = width / (double)BaseWidth, sy = height / (double)BaseHeight;
        return new LinesheetLipRect(Even(r.X * sx), Even(r.Y * sy), Even(r.W * sx), Even(r.H * sy));
    }

    /// <summary>
    /// Các câu ghép môi được trong một shot có đoạn chuyển động và ảnh cảnh: câu có giọng thật, người nói có khuôn mặt
    /// trong settings.faces của ảnh cảnh đó.
    /// </summary>
    public static IReadOnlyList<LinesheetLipItem> Items(
        LinesheetShotSpan span, LinesheetTimeline timeline, LinesheetSettings settings, string? keyframeName,
        (int W, int H)? keyframeSize, IReadOnlyDictionary<string, LinesheetFileRecord> voices, string motionSha, int fps)
    {
        if (keyframeName is null || keyframeSize is not { } size
            || !settings.Faces.TryGetValue(keyframeName, out var faces) || faces.Count == 0) return [];
        var list = new List<LinesheetLipItem>();
        foreach (var r in span.Rows)
        {
            if (!r.IsLine || timeline.EstimatedRowIds.Contains(r.Id) || !voices.TryGetValue(r.Id, out var voice)) continue;
            if (!faces.TryGetValue(r.Speaker, out var box) || box is not { Length: 4 } || box[2] <= 0 || box[3] <= 0) continue;
            var e = timeline[r.Id];
            var startMs = e.StartMs - span.StartMs;
            var endMs = e.EndMs - span.StartMs;
            var f0 = Math.Max(0, (int)Math.Floor(startMs * fps / 1000.0) - PadFrames);
            // tính theo mili giây trong shot, không theo số khung của shot: bản dựng một đoạn và bản dựng cả tập có thể
            // lệch nhau một khung ở đầu shot, mã đoạn ghép môi vẫn phải giống nhau
            var f1 = Math.Min((int)Math.Ceiling((span.EndMs - span.StartMs) * fps / 1000.0), (int)Math.Ceiling(endMs * fps / 1000.0) + PadFrames);
            if (f1 - f0 < 6) continue;
            var lead = Math.Max(0, (int)Math.Round(startMs - f0 * 1000.0 / fps));
            var rect = FaceRect(box, size.W, size.H, BaseWidth, BaseHeight);
            var keyBase = "lip1|" + motionSha + "|" + voice.Sha256 + "|" + rect + "|" + f0.ToString(Inv) + "|" + f1.ToString(Inv)
                          + "|" + lead.ToString(Inv) + "|" + (span.EndMs - span.StartMs).ToString(Inv) + "|" + fps.ToString(Inv);
            list.Add(new LinesheetLipItem(r, voice, rect, f0, f1, lead, keyBase));
        }

        return list;
    }

    public static string LipKey(LinesheetLipItem item, string model) =>
        LinesheetRenderPlan.Sha256Hex(item.KeyBase + "|" + model)[..16];

    /// <summary>
    /// Đoạn gửi đi: dựng đoạn chuyển động đúng như khi dựng shot (cùng chuỗi lọc), lấy các khung của câu, cắt vùng mặt,
    /// phóng lên khoảng 512 điểm ảnh. Không tiếng.
    /// </summary>
    public static IReadOnlyList<string> SegmentArgs(
        LinesheetShotSpan span, string motionMp4, int clipSeconds, LinesheetLipItem item, int fps, string outputMp4)
    {
        var r = item.Rect;
        var side = Math.Max(SendSide, r.W);
        var graph = LinesheetMotionPlan.BaseChain(span.Frames, clipSeconds, BaseWidth, BaseHeight, fps)
                    + $";[v0]trim=start_frame={item.F0}:end_frame={item.F1},setpts=PTS-STARTPTS,crop={r.W}:{r.H}:{r.X}:{r.Y},"
                    + $"scale={side}:{side}:flags=lanczos,setsar=1[o]";
        return ["-v", "error", "-y", "-i", motionMp4, "-filter_complex", graph, "-map", "[o]", "-frames:v", item.Frames.ToString(Inv),
            "-r", fps.ToString(Inv), "-c:v", "libx264", "-preset", "veryfast", "-crf", "14", "-pix_fmt", "yuv420p", outputMp4];
    }

    /// <summary>Tiếng gửi đi: giọng của câu, thêm khoảng lặng phía trước cho khớp khung đầu, đủ dài bằng đoạn hình.</summary>
    public static IReadOnlyList<string> VoiceArgs(string voicePath, LinesheetLipItem item, int fps, string outputMp3)
    {
        var seconds = item.Frames / (double)fps;
        return ["-v", "error", "-y", "-i", voicePath, "-af", $"adelay={item.LeadMs}:all=1,apad", "-t", seconds.ToString("0.000", Inv),
            "-ac", "1", "-ar", "44100", "-c:a", "libmp3lame", "-b:a", "192k", outputMp3];
    }

    /// <summary>
    /// Lọc đặt một đoạn ghép môi lên hình: đưa về đúng cỡ vùng mặt và đúng nhịp khung, mép mờ dần (để chỗ nối không lộ),
    /// lùi thời gian tới khung đầu của câu.
    /// </summary>
    public static string OverlayGraph(LinesheetPlacedLip lip, int inputIndex, string cur, string output, int fps)
    {
        var r = lip.Rect;
        var n = lip.F1 - lip.F0;
        var t0 = (lip.F0 / (double)fps).ToString("0.0000", Inv);
        var t1 = ((lip.F1 - 0.5) / fps).ToString("0.0000", Inv);
        var m = Math.Max(4, r.W / 8 / 2 * 2);
        var dur = (n / (double)fps + 1).ToString("0.000", Inv);
        // 2026-11-09: mờ dần vùng ghép môi ở đầu và cuối (trước đây bật/tắt cứng trong 1 khung, mặt "giật" khi hết câu)
        var fadeSec = Math.Min(FadeSeconds, n / (double)fps / 4);
        var fd = fadeSec.ToString("0.000", Inv);
        var fo = Math.Max(0, n / (double)fps - fadeSec).ToString("0.000", Inv);
        var id = output;
        return $";[{inputIndex}:v]fps={fps},scale={r.W}:{r.H},setsar=1,trim=end_frame={n},setpts=PTS-STARTPTS,format=yuva420p[{id}s]"
               + $";color=c=black:s={r.W}x{r.H}:r={fps}:d={dur},format=yuv420p,drawbox=x={m}:y={m}:w={r.W - 2 * m}:h={r.H - 2 * m}:color=white:t=fill,"
               + $"gblur=sigma={m / 2},format=gray[{id}m]"
               + $";[{id}s][{id}m]alphamerge,fade=t=in:st=0:d={fd}:alpha=1,fade=t=out:st={fo}:d={fd}:alpha=1,setpts=PTS+{t0}/TB[{id}a]"
               + $";[{cur}][{id}a]overlay=x={r.X}:y={r.Y}:eof_action=pass:enable='between(t,{t0},{t1})'[{output}]";
    }
}
