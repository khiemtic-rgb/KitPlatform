using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content.Production;

/// <summary>Khoảng thời gian và số khung của một shot trong một lần dựng.</summary>
public sealed record LinesheetShotSpan(
    int ShotIndex,
    LinesheetShot Shot,
    int StartMs,
    int EndMs,
    int Frames,
    IReadOnlyList<LinesheetRow> Rows);

/// <summary>Một lớp chữ trên màn hình (tin nhắn, tiêu đề), thời gian tính trong shot.</summary>
public sealed record LinesheetTextLayer(string RowId, string Text, string Kind, double FromSec, double ToSec);

public static class LinesheetTextKinds
{
    public const string Title = "title";
    public const string Chat = "chat";
    public const string Caption = "caption";

    /// <summary>
    /// Chữ viết tay trên giấy (thêm 2026-11-07, EP02 "Trả cậu. Q."): chữ chỉ đồ vật mà nhân vật đang đọc, vẽ to giữa khung
    /// trên mảnh giấy. Trong kịch bản ghi cột screen bắt đầu bằng "✎".
    /// </summary>
    public const string Note = "note";

    public const string NotePrefix = "✎";
}

/// <summary>
/// Kế hoạch dựng: chia khung hình cho từng shot, chọn lớp chữ, dựng danh sách đối số FFmpeg.
/// Mọi lệnh là danh sách đối số (không qua shell), chữ không bao giờ nằm trên dòng lệnh: chữ được vẽ thành ảnh trước.
/// Logic chuyển từ bản dựng mẫu <c>reference/render_episode.py</c>.
/// </summary>
public static class LinesheetRenderPlan
{
    /// <summary>Tăng mỗi khi đổi cách dựng, để cache không trả clip cũ.</summary>
    public const string RenderVersion = "m1-kp-2"; // kp-2: vùng ghép môi mờ dần đầu/cuối câu

    /// <summary>
    /// Phiên bản giao diện điện thoại: đổi cách vẽ điện thoại (SkiaLinesheetLayerComposer.RenderPhone) thì tăng số này,
    /// không thì các shot có tin nhắn vẫn lấy bản cũ trong cache (bài học 2026-11-05).
    /// </summary>
    public const string PhoneUiVersion = "phone6"; // phone6: font Be Vietnam Pro
    public const string CaptionVersion = "card3"; // card3: font Be Vietnam Pro + logo trên tên phim (2026-11-05)

    /// <summary>Nền dư 25% so với khung để còn chỗ zoom và pan.</summary>
    public const double Over = 1.25;

    /// <summary>Dựng lớp ở 1,5 lần độ phân giải đầu ra để zoompan không rung.</summary>
    public const double Quality = 1.5;

    public const int DefaultFps = 24;
    public const string VideoPreset = "veryfast";
    public const string VideoCrf = "23";

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly Regex TrailingNote = new(@"\s*\([^)]*\)\s*$", RegexOptions.Compiled);

    // ---------- chia shot ----------

    public static IReadOnlyList<LinesheetShotSpan> Spans(
        IReadOnlyList<LinesheetRow> rows,
        LinesheetTimeline timeline,
        IReadOnlyList<LinesheetShot> shots,
        int fps,
        int rangeStartMs,
        int rangeEndMs)
    {
        if (rangeEndMs <= rangeStartMs)
            throw new InvalidOperationException(LinesheetErrorCodes.BadRange);
        var times = timeline.Entries.ToDictionary(e => e.RowId);
        var pos = rows.Select((r, i) => (r.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var spans = new List<LinesheetShotSpan>();
        for (var k = 0; k < shots.Count; k++)
        {
            var s = shots[k];
            var a = times[s.StartRow].StartMs;
            var b = k + 1 < shots.Count ? times[shots[k + 1].StartRow].StartMs : timeline.TotalMs;
            if (b <= rangeStartMs || a >= rangeEndMs) continue;
            a = Math.Max(a, rangeStartMs);
            b = Math.Min(b, rangeEndMs);
            var f0 = LinesheetTimelineBuilder.FrameAt(a - rangeStartMs, fps);
            var f1 = LinesheetTimelineBuilder.FrameAt(b - rangeStartMs, fps);
            if (f1 - f0 <= 0) continue;
            var i0 = pos[s.StartRow];
            var i1 = pos[s.EndRow];
            spans.Add(new LinesheetShotSpan(k, s, a, b, f1 - f0, rows.Skip(i0).Take(i1 - i0 + 1).ToList()));
        }

        return spans;
    }

    // ---------- lớp chữ ----------

    /// <summary>Bỏ ghi chú cho họa sĩ trong ngoặc ở cuối, ví dụ "Bệnh viện, 22:10 (Mai ngồi ở hành lang)".</summary>
    public static string CleanScreenText(string s) => TrailingNote.Replace(s ?? "", "").Trim();

    /// <summary>Tên phim giữ trên màn hình (thêm 2026-11-05, trước đó 3 giây).</summary>
    public const int TitleHoldMs = 5000;

    /// <summary>
    /// Thời gian đọc tối thiểu của chữ trên màn hình một hàng: 1 giây + 0,3 giây mỗi từ; tin nhắn ít nhất 2,5 giây,
    /// thẻ chữ khác ít nhất 3 giây, tối đa 8 giây; tên phim 5 giây. Lấy bản dài hơn giữa tiếng Việt và tiếng Anh.
    /// Không có chữ thì 0.
    /// </summary>
    public static int ReadingMs(LinesheetRow r)
    {
        var vi = CleanScreenText(r.ScreenVi);
        var en = CleanScreenText(r.ScreenEn);
        if (vi.Length == 0 && en.Length == 0) return 0;
        if (r.Cue == "title_sting") return TitleHoldMs;

        static int Need(string t)
        {
            if (t.Length == 0) return 0;
            var words = PhoneOwner(t).Body.Split([' ', '/'], StringSplitOptions.RemoveEmptyEntries).Length;
            return Math.Clamp(1000 + 300 * words, IsChat(t) ? 2500 : 3000, 8000);
        }

        return Math.Max(Need(vi), Need(en));
    }

    public static IReadOnlyList<LinesheetTextLayer> TextLayers(
        LinesheetShotSpan span,
        LinesheetTimeline timeline,
        string lang)
    {
        var times = timeline.Entries.ToDictionary(e => e.RowId);
        var shotSec = (span.EndMs - span.StartMs) / 1000.0;
        var layers = new List<LinesheetTextLayer>();
        foreach (var r in span.Rows)
        {
            var raw = r.Screen(lang).Trim();
            if (raw.Length == 0) continue;
            var note = raw.StartsWith(LinesheetTextKinds.NotePrefix, StringComparison.Ordinal);
            if (note) raw = raw[LinesheetTextKinds.NotePrefix.Length..].Trim();
            var text = CleanScreenText(raw);
            var preview = r.Cue.StartsWith("next_episode_preview", StringComparison.Ordinal);
            if (preview)
            {
                var c = text.IndexOf(':');
                if (c >= 0) text = text[..c] + " —" + text[(c + 1)..];
            }

            var kind = note ? LinesheetTextKinds.Note
                : r.Cue == "title_sting" || preview
                ? LinesheetTextKinds.Title
                : IsChat(text) ? LinesheetTextKinds.Chat : LinesheetTextKinds.Caption;
            var e = times[r.Id];
            var rowSec = (e.EndMs - e.StartMs) / 1000.0;
            var from = (e.StartMs - span.StartMs) / 1000.0;
            var need = LinesheetRenderPlan.ReadingMs(r) / 1000.0;
            var to = Math.Min(from + Math.Max(need, rowSec), shotSec);
            layers.Add(new LinesheetTextLayer(r.Id, text, kind, from, to));
        }

        return layers;
    }

    // ---------- dựng một shot ----------

    /// <summary>Biểu thức zoompan của từng preset. N = số khung − 1.</summary>
    public static string ZoompanExpr(string preset, int n)
    {
        const string center = "x='(iw-iw/zoom)/2':y='(ih-ih/zoom)/2'";
        return preset switch
        {
            LinesheetCameraPresets.SlowZoomIn => $"z='{D(Over)}*(1+0.10*on/{n})':{center}",
            LinesheetCameraPresets.SlowZoomOut => $"z='{D(Over)}*(1.10-0.10*on/{n})':{center}",
            LinesheetCameraPresets.PanRight => $"z='{D(Over * 0.95)}':x='(iw-iw/zoom)*(0.15+0.7*on/{n})':y='(ih-ih/zoom)/2'",
            LinesheetCameraPresets.PanLeft => $"z='{D(Over * 0.95)}':x='(iw-iw/zoom)*(0.85-0.7*on/{n})':y='(ih-ih/zoom)/2'",
            LinesheetCameraPresets.ShakeLight =>
                $"z='{D(Over * 1.03)}':x='(iw-iw/zoom)/2+iw/zoom*0.004*sin(on*1.3)':y='(ih-ih/zoom)/2+ih/zoom*0.004*cos(on*1.7)'",
            _ => $"z='{D(Over)}':{center}",
        };
    }

    public sealed record ShotImages(string? FlatPng, string? BackgroundPng, string? FrontPng)
    {
        public bool IsParallax => BackgroundPng is not null && FrontPng is not null;
    }

    /// <summary>Blur: làm mờ, tối khung hình phía sau trong lúc thẻ hiện (cảnh chèn điện thoại).</summary>
    public sealed record PlacedCard(string Png, int X, int Y, double FromSec, double ToSec, bool Blur = false);

    /// <summary>
    /// Tin nhắn hay không (thêm 2026-11-05): có "Người gửi: " (dấu hai chấm rồi dấu cách) trong 25 ký tự đầu, sau khi bỏ
    /// tiền tố chủ điện thoại "@Mẹ|". "Bệnh viện, 22:10" không phải tin nhắn vì sau dấu hai chấm không có dấu cách.
    /// </summary>
    public static bool IsChat(string text)
    {
        var t = PhoneOwner(text).Body;
        var i = t.IndexOf(": ", StringComparison.Ordinal);
        return i > 0 && i < 25;
    }

    /// <summary>Tiền tố chủ điện thoại "@Mẹ| Ngân hàng: …" → ("Mẹ", "Ngân hàng: …"). Không có tiền tố → ("", text).</summary>
    public static (string Owner, string Body) PhoneOwner(string text)
    {
        var t = text.TrimStart();
        if (t.StartsWith('@'))
        {
            var bar = t.IndexOf('|');
            if (bar > 1 && bar <= 20) return (t[1..bar].Trim(), t[(bar + 1)..].Trim());
        }

        return ("", text.Trim());
    }

    public static IReadOnlyList<string> ShotArgs(
        LinesheetShotSpan span,
        ShotImages images,
        IReadOnlyList<PlacedCard> cards,
        int width,
        int height,
        int fps,
        string outputPath)
    {
        var frames = span.Frames;
        var n = Math.Max(frames - 1, 1);
        var args = new List<string> { "-v", "error", "-y" };
        var graph = new StringBuilder();
        int next;
        if (!images.IsParallax)
        {
            args.AddRange(["-i", images.FlatPng ?? throw new InvalidOperationException(LinesheetErrorCodes.MissingLayer)]);
            graph.Append($"[0:v]zoompan={ZoompanExpr(span.Shot.CameraPreset, n)}:d={frames}:s={width}x{height}:fps={fps}[v0]");
            next = 1;
        }
        else
        {
            var t = frames / (double)fps;
            args.AddRange(["-i", images.BackgroundPng!, "-loop", "1", "-framerate", I(fps), "-i", images.FrontPng!]);
            graph.Append($"[0:v]zoompan=z='{D(Over * 0.97)}':x='(iw-iw/zoom)*(0.35+0.3*on/{n})':y='(ih-ih/zoom)/2'")
                 .Append($":d={frames}:s={width}x{height}:fps={fps}[b];")
                 .Append($"[1:v]scale=-1:{height}[f];")
                 .Append($"[b][f]overlay=x='-(overlay_w-{width})*(0.15+0.7*t/{D3(t)})':y=0:shortest=1[v0]");
            next = 2;
        }

        return FinishShotArgs(args, graph, "v0", next, span, cards, fps, outputPath);
    }

    /// <summary>Phần chung của mọi kiểu shot: phủ thẻ chữ, mờ vào, mã hóa đúng số khung.</summary>
    public static IReadOnlyList<string> FinishShotArgs(
        List<string> args, StringBuilder graph, string cur, int next,
        LinesheetShotSpan span, IReadOnlyList<PlacedCard> cards, int fps, string outputPath)
    {
        var frames = span.Frames;
        for (var i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            args.AddRange(["-loop", "1", "-framerate", I(fps), "-i", c.Png]);
            var on = $"enable='between(t,{D3(c.FromSec)},{D3(c.ToSec)})'";
            if (c.Blur)
            {
                // cảnh chèn điện thoại: khung hình phía sau mờ và tối đi, điện thoại hiện dần trong 0,2 giây
                graph.Append($";[{cur}]split[pa{i}][pb{i}];[pb{i}]boxblur=20:2,eq=brightness=-0.12[pk{i}];[pa{i}][pk{i}]overlay=x=0:y=0:{on}[pz{i}]");
                graph.Append($";[{next + i}:v]format=rgba,fade=t=in:st={D3(Math.Max(0, c.FromSec))}:d=0.2:alpha=1[pc{i}]");
                graph.Append($";[pz{i}][pc{i}]overlay=x={c.X}:y={c.Y}:shortest=1:{on}[t{i}]");
            }
            else
            {
                graph.Append($";[{cur}][{next + i}:v]overlay=x={c.X}:y={c.Y}:shortest=1:{on}[t{i}]");
            }

            cur = "t" + i;
        }

        if (span.Shot.TransitionIn == LinesheetTransitions.Fade)
        {
            graph.Append($";[{cur}]fade=t=in:st=0:d=0.3[vf]");
            cur = "vf";
        }

        args.AddRange(
        [
            "-filter_complex", graph.ToString(), "-map", $"[{cur}]", "-frames:v", I(frames),
            "-r", I(fps), "-c:v", "libx264", "-preset", VideoPreset, "-crf", VideoCrf, "-pix_fmt", "yuv420p",
            outputPath,
        ]);
        return args;
    }

    // ---------- ánh sáng và hướng nhân vật ----------

    /// <summary>
    /// Hệ số nhân R, G, B cho nhân vật trong một bối cảnh: trộn trắng với màu ánh sáng theo độ phủ,
    /// rồi nhân 0,6 nếu nhân vật không phải người đang nói.
    /// </summary>
    public static (double R, double G, double B) CharacterTint(LinesheetSettingDef? setting, bool dimmed)
    {
        double r = 1, g = 1, b = 1;
        if (setting?.Light is { Length: 7 } hex && hex[0] == '#' && setting.LightStrength > 0
            && int.TryParse(hex[1..], NumberStyles.HexNumber, Inv, out var rgb))
        {
            var k = Math.Clamp(setting.LightStrength, 0, 1);
            r = 1 - k + k * ((rgb >> 16) & 0xFF) / 255.0;
            g = 1 - k + k * ((rgb >> 8) & 0xFF) / 255.0;
            b = 1 - k + k * (rgb & 0xFF) / 255.0;
        }

        var d = dimmed ? 0.6 : 1.0;
        return (r * d, g * d, b * d);
    }

    /// <summary>Lật ảnh để nhân vật đứng lệch trái quay sang phải, đứng lệch phải quay sang trái. Đứng giữa giữ nguyên.</summary>
    public static bool ShouldMirror(string faces, double x) =>
        (x < 0.45 && faces == "left") || (x > 0.55 && faces != "left");

    // ---------- âm thanh ----------

    /// <summary>
    /// Chuẩn hóa một file giọng về WAV 48 kHz mono và cắt khoảng lặng thừa ở đầu, cuối (dịch vụ giọng thường tự đệm
    /// khoảng 1,2 giây mỗi câu; không cắt thì EP01 dài 10,1 phút thay vì 8,1 phút). Giữ 50 ms để không cụt hơi.
    /// </summary>
    public static IReadOnlyList<string> NormalizeVoiceArgs(string input, string outputWav, bool trim = true)
    {
        var args = new List<string> { "-v", "error", "-y", "-i", input };
        if (trim)
            args.AddRange(["-af", "silenceremove=start_periods=1:start_threshold=-45dB:start_silence=0.05,areverse,"
                                + "silenceremove=start_periods=1:start_threshold=-45dB:start_silence=0.05,areverse"]);
        args.AddRange(["-ar", "48000", "-ac", "1", outputWav]);
        return args;
    }

    // ---------- cân âm lượng giữa các giọng ----------

    /// <summary>
    /// ffprobe đo độ to tích phân (LUFS) của một câu. <paramref name="fileName"/> là tên tương đối trong thư mục làm việc
    /// (đường dẫn Windows có ':' làm hỏng cú pháp lavfi). Kết quả: mỗi khung một dòng, dòng cuối là độ to cả câu.
    /// </summary>
    public static IReadOnlyList<string> LoudnessProbeArgs(string fileName) =>
    [
        "-v", "error", "-f", "lavfi", "-i", "amovie=" + fileName + ",ebur128=metadata=1",
        "-show_entries", "frame_tags=lavfi.r128.I", "-of", "csv=p=0",
    ];

    /// <summary>Lấy độ to cả câu từ đầu ra của <see cref="LoudnessProbeArgs"/>; không đọc được thì null.</summary>
    public static double? ParseIntegratedLufs(string stdout)
    {
        foreach (var line in (stdout ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Reverse())
        {
            if (double.TryParse(line.Trim(','), NumberStyles.Float, Inv, out var v) && v > -70 && v < 0) return v;
        }

        return null;
    }

    /// <summary>
    /// Số dB cần cộng cho một câu: kéo về mức chung rồi cộng chỉnh riêng của giọng. Giới hạn ±12 dB để câu thì thầm
    /// không bị kéo to như câu nói thường quá mức. <paramref name="targetLufs"/> = 0 là tắt cân âm lượng.
    /// </summary>
    public static double VoiceGainDb(double? measuredLufs, double targetLufs, double voiceGainDb)
    {
        var level = targetLufs < 0 && measuredLufs is { } m ? Math.Clamp(targetLufs - m, -12, 12) : 0;
        return Math.Round(level + voiceGainDb, 1);
    }

    /// <summary>Đổi âm lượng, giữ đúng số mẫu (dòng thời gian không lệch); alimiter chặn vỡ tiếng khi kéo to.</summary>
    public static IReadOnlyList<string> GainArgs(string input, double gainDb, string outputWav) =>
    [
        "-v", "error", "-y", "-i", input,
        "-af", $"volume={D3(gainDb)}dB,alimiter=limit=0.95:level=disabled",
        "-ar", "48000", "-ac", "1", outputWav,
    ];

    /// <summary>
    /// Dấu của bộ nhạc và tiếng động đã tải lên. Tải nhạc hay tiếng động mới thì phải ghép tiếng lại, dù dòng thời gian
    /// không đổi (lỗi trước 2026-11-04: tiếng cũ được dùng lại vì khóa chỉ theo dòng thời gian).
    /// </summary>
    public static string CueSetTag(IEnumerable<LinesheetFileRecord> files) =>
        Sha256Hex("cues1|" + string.Join(";", files
            .Where(f => f.Kind is LinesheetFileKinds.CueMusic or LinesheetFileKinds.CueSfx)
            .Select(f => f.Kind + ":" + f.FileKey + "=" + f.Sha256)
            .OrderBy(x => x, StringComparer.Ordinal)))[..10];

    /// <summary>Dấu của cách cân âm lượng; đổi mức chung hoặc gain_db của một giọng thì ghép tiếng lại.</summary>
    public static string MixTag(LinesheetSettings settings) =>
        Sha256Hex("mix4|" + D3(settings.LoudnessLufs) + "|" + D3(settings.MasterLufs) + "|" + D3(settings.RoomToneDb) + "|" + string.Join(";", settings.Voices
            .OrderBy(k => k.Key, StringComparer.Ordinal)
            .Where(k => Math.Abs(k.Value.GainDb) > 0.001)
            .Select(k => k.Key + "=" + D3(k.Value.GainDb)))
            + "|beds:" + string.Join(";", settings.MusicBeds.Select(b => b.Cue + "@" + b.FromRow + "-" + b.ToRow + "/" + D3(b.GainDb)
                + "/" + D3(b.OffsetSec) + "/" + b.FadeInMs + "/" + b.FadeOutMs)))[..10];

    /// <summary>
    /// Trộn các lớp nhạc nền vào tiếng cả tập (2026-11-06). Mỗi lớp: cắt từ <c>offset</c>, lặp nếu ngắn, đủ dài
    /// <c>dur</c>, mờ vào/ra, hạ <c>gain</c>, dời tới <c>start</c>. Giữ đúng số mẫu của tiếng gốc (duration=first).
    /// </summary>
    public static IReadOnlyList<string> BedMixArgs(string input, IReadOnlyList<(string Path, double StartSec, double DurSec, double GainDb, double OffsetSec, double FadeInSec, double FadeOutSec)> beds, string outputWav)
    {
        var args = new List<string> { "-v", "error", "-y", "-i", input };
        var g = new StringBuilder();
        for (var i = 0; i < beds.Count; i++)
        {
            var b = beds[i];
            args.AddRange(["-stream_loop", "-1", "-i", b.Path]);
            var delay = (long)Math.Round(b.StartSec * 1000);
            g.Append($"[{i + 1}:a]aresample=48000,aformat=channel_layouts=mono,atrim=start={D3(b.OffsetSec)}:duration={D3(b.DurSec)},asetpts=PTS-STARTPTS,")
             .Append($"afade=t=in:st=0:d={D3(b.FadeInSec)},afade=t=out:st={D3(Math.Max(0, b.DurSec - b.FadeOutSec))}:d={D3(b.FadeOutSec)},")
             .Append($"volume={D3(b.GainDb)}dB,adelay={delay}:all=1[b{i}];");
        }

        g.Append("[0:a]");
        for (var i = 0; i < beds.Count; i++) g.Append($"[b{i}]");
        g.Append($"amix=inputs={beds.Count + 1}:duration=first:normalize=0[a]");
        args.AddRange(["-filter_complex", g.ToString(), "-map", "[a]", "-ar", "48000", "-ac", "1", outputWav]);
        return args;
    }

    /// <summary>
    /// Bước cuối của tiếng: trộn tiếng nền phòng (nhiễu nâu lọc thấp) rồi cộng <paramref name="gainDb"/> và chặn đỉnh khoảng −2 dB mẫu (sau khi nén AAC còn dưới −1 dBTP; bản 10 chặn 0,89 ra −0,1 dBTP).
    /// Giữ đúng số mẫu (amix duration=first). Mức nền tính sao cho sau khi cộng gain còn đúng <paramref name="roomToneDb"/>.
    /// </summary>
    public static IReadOnlyList<string> MasterArgs(string input, double gainDb, double roomToneDb, double durationSec, string outputWav)
    {
        string graph;
        if (roomToneDb < 0)
        {
            // nhiễu nâu a=0,01 lọc 900 Hz đo được khoảng −54,5 dBFS
            var amp = Math.Clamp(0.01 * Math.Pow(10, (roomToneDb - gainDb + 54.5) / 20), 0.00001, 0.5);
            graph = $"anoisesrc=d={D3(durationSec + 1)}:c=brown:r=48000:a={amp.ToString("0.000000", Inv)},lowpass=f=900[n];"
                    + $"[0:a][n]amix=inputs=2:duration=first:normalize=0,volume={D3(gainDb)}dB,alimiter=limit=0.68:attack=1:release=60:level=disabled[a]";
        }
        else
        {
            graph = $"[0:a]volume={D3(gainDb)}dB,alimiter=limit=0.68:attack=1:release=60:level=disabled[a]";
        }

        return ["-v", "error", "-y", "-i", input, "-filter_complex", graph, "-map", "[a]", "-ar", "48000", "-ac", "1", outputWav];
    }

    public static IReadOnlyList<string> SilenceArgs(int durationMs, string outputWav) =>
        ["-v", "error", "-y", "-f", "lavfi", "-i", "anullsrc=r=48000:cl=mono", "-t", D3(durationMs / 1000.0), outputWav];

    /// <summary>Nhạc hoặc hiệu ứng: cắt đúng thời lượng, mờ dần 0,3 giây ở cuối.</summary>
    public static IReadOnlyList<string> FitCueArgs(string input, int durationMs, string outputWav)
    {
        var d = durationMs / 1000.0;
        return
        [
            "-v", "error", "-y", "-i", input,
            "-af", $"apad,atrim=0:{D3(d)},afade=t=out:st={D3(Math.Max(d - 0.3, 0))}:d=0.3",
            "-ar", "48000", "-ac", "1", outputWav,
        ];
    }

    public static string ConcatList(IEnumerable<string> absolutePaths) =>
        string.Concat(absolutePaths.Select(p => "file '" + p.Replace('\\', '/').Replace("'", @"'\''") + "'\n"));

    /// <summary>
    /// Hình đoạn kết: hai ảnh tĩnh nối nhau, hiện dần và tắt dần; mã hóa đúng như shot (libx264, cùng preset, crf,
    /// pix_fmt, fps) để nối bằng concat chép thẳng.
    /// </summary>
    public static IReadOnlyList<string> OutroVideoArgs(string titlePng, string cardPng, double titleSec, double cardSec, int width, int height, int fps, string output)
    {
        var frames = (int)Math.Round((titleSec + cardSec) * fps, MidpointRounding.AwayFromZero);
        var graph = $"[0:v]scale={width}:{height},setsar=1,format=yuv420p,fade=t=in:st=0:d=0.6[a];"
                    + $"[1:v]scale={width}:{height},setsar=1,format=yuv420p,fade=t=in:st=0:d=0.6,fade=t=out:st={D3(Math.Max(0, cardSec - 1))}:d=1[b];"
                    + "[a][b]concat=n=2:v=1:a=0[v]";
        return
        [
            "-v", "error", "-y",
            "-loop", "1", "-framerate", I(fps), "-t", D3(titleSec), "-i", titlePng,
            "-loop", "1", "-framerate", I(fps), "-t", D3(cardSec), "-i", cardPng,
            "-filter_complex", graph, "-map", "[v]", "-frames:v", I(frames),
            "-r", I(fps), "-c:v", "libx264", "-preset", VideoPreset, "-crf", VideoCrf, "-pix_fmt", "yuv420p", output,
        ];
    }

    /// <summary>
    /// Tiếng đoạn kết (wav 48 kHz mono như tiếng cả tập): nhạc kết dưới "HẾT TẬP" ngân nhỏ dần, rồi nhạc nền khung kết
    /// hiện dần và tắt dần. Mỗi phần chuẩn hóa độ to riêng (nhạc nền nhỏ hơn lời thoại). Thiếu cue thì là im lặng.
    /// </summary>
    public static IReadOnlyList<string> OutroAudioArgs(string? stingPath, string? musicPath, double musicStartSec, double titleSec, double cardSec, string outputWav)
    {
        var args = new List<string> { "-v", "error", "-y" };
        string Src(string? path, double start, double dur, int idx)
        {
            if (path is null) return $"anullsrc=r=48000:cl=mono,atrim=0:{D3(dur)}";
            args.AddRange(["-ss", D3(start), "-t", D3(dur + 0.5), "-i", path]);
            return $"[{idx}:a]aresample=48000,aformat=channel_layouts=mono";
        }

        var n = 0;
        var a = Src(stingPath, 0, titleSec, stingPath is null ? -1 : n);
        if (stingPath is not null) n++;
        var b = Src(musicPath, musicStartSec, cardSec, musicPath is null ? -1 : n);
        var graph = $"{a},loudnorm=I=-16:TP=-2,aresample=48000,apad,atrim=0:{D3(titleSec)},afade=t=out:st={D3(Math.Max(0, titleSec - 0.9))}:d=0.9[s];"
                    + $"{b},loudnorm=I=-20:TP=-3,aresample=48000,apad,atrim=0:{D3(cardSec)},asetpts=PTS-STARTPTS,"
                    + $"afade=t=in:st=0:d=1.5,afade=t=out:st={D3(Math.Max(0, cardSec - 2.5))}:d=2.5[m];[s][m]concat=n=2:v=0:a=1[o]";
        args.AddRange(["-filter_complex", graph, "-map", "[o]", "-ar", "48000", "-ac", "1", "-c:a", "pcm_s16le", outputWav]);
        return args;
    }

    public static IReadOnlyList<string> ConcatArgs(string listFile, string output) =>
        ["-v", "error", "-y", "-f", "concat", "-safe", "0", "-i", listFile, "-c", "copy", output];

    // ---------- ghép tập ----------

    /// <summary>
    /// 16:9: hình (copy) + tiếng + các track phụ đề có nội dung. Độ dài đặt theo số khung, không dùng -shortest:
    /// track phụ đề kết thúc ở câu cuối, -shortest sẽ cắt mất đuôi tập.
    /// </summary>
    public static IReadOnlyList<string> MuxArgs(
        string video, string audio, int rangeStartMs, int rangeEndMs, int frames, int fps,
        IReadOnlyList<(string Path, string Language)> subtitleTracks, string output)
    {
        var args = new List<string>
        {
            "-v", "error", "-y", "-i", video,
            "-ss", D3(rangeStartMs / 1000.0), "-to", D3(rangeEndMs / 1000.0), "-i", audio,
        };
        foreach (var (path, _) in subtitleTracks) args.AddRange(["-i", path]);
        args.AddRange(["-map", "0:v", "-map", "1:a"]);
        for (var i = 0; i < subtitleTracks.Count; i++)
            args.AddRange(["-map", I(2 + i), $"-metadata:s:s:{i}", "language=" + subtitleTracks[i].Language]);
        args.AddRange(["-c:v", "copy", "-c:a", "aac", "-b:a", "128k"]);
        if (subtitleTracks.Count > 0) args.AddRange(["-c:s", "mov_text"]);
        args.AddRange(["-t", D3(frames / (double)fps), output]);
        return args;
    }

    /// <summary>
    /// 9:16: đốt phụ đề vào hình. <paramref name="subtitleFileName"/> và <paramref name="fontsDirName"/> là tên tương đối
    /// trong thư mục làm việc của tiến trình (đường dẫn Windows có dấu ':' sẽ làm hỏng cú pháp filter).
    /// </summary>
    public static IReadOnlyList<string> BurnArgs(
        string video, string audio, int rangeStartMs, int rangeEndMs, int frames, int fps,
        string? subtitleFileName, string fontsDirName, string fontFamily, bool vertical, string output)
    {
        var args = new List<string>
        {
            "-v", "error", "-y", "-i", video,
            "-ss", D3(rangeStartMs / 1000.0), "-to", D3(rangeEndMs / 1000.0), "-i", audio,
        };
        if (subtitleFileName is not null)
        {
            var style = vertical
                ? $"FontName={fontFamily},FontSize=11,Outline=1.5,Shadow=0,MarginV=120"
                : $"FontName={fontFamily},FontSize=18,Outline=1.5,Shadow=0,MarginV=24";
            args.AddRange(["-vf", $"subtitles={subtitleFileName}:fontsdir={fontsDirName}:force_style='{style}'"]);
        }

        args.AddRange(
        [
            "-map", "0:v", "-map", "1:a", "-c:v", "libx264", "-preset", VideoPreset, "-crf", VideoCrf,
            "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "128k", "-t", D3(frames / (double)fps), output,
        ]);
        return args;
    }

    public static IReadOnlyList<string> CountFramesProbeArgs(string path) =>
        ["-v", "error", "-count_frames", "-select_streams", "v:0", "-show_entries", "stream=nb_read_frames", "-of", "csv=p=0", path];

    public static IReadOnlyList<string> DurationProbeArgs(string path) =>
        ["-v", "error", "-show_entries", "format=duration", "-of", "default=nw=1:nk=1", path];

    // ---------- cache ----------

    /// <summary>
    /// Mã cache của một shot: đổi bất kỳ đầu vào nào (shot, khung, kích thước, chữ, mốc thời gian, ảnh) thì dựng lại.
    /// </summary>
    public static string ShotCacheKey(
        LinesheetShotSpan span,
        LinesheetTimeline timeline,
        int width, int height, int fps, string lang,
        IEnumerable<string> assetSha256)
    {
        var times = timeline.Entries.ToDictionary(e => e.RowId);
        var payload = JsonSerializer.Serialize(new
        {
            v = RenderVersion,
            shot = span.Shot,
            frames = span.Frames,
            w = width, h = height, fps, lang,
            a = span.StartMs, b = span.EndMs,
            rows = span.Rows.Select(r => new { r.Id, s = r.Screen(lang), times[r.Id].StartMs, times[r.Id].EndMs, r.Cue }),
            assets = assetSha256.ToList(),
        });
        // tin nhắn vẽ thành cảnh chèn điện thoại từ 2026-11-05: shot có tin nhắn phải dựng lại, shot khác giữ mã cũ
        if (span.Rows.Any(r => IsChat(CleanScreenText(r.Screen(lang))))) payload += "|" + PhoneUiVersion;
        // thẻ chữ và tên phim vẽ lại từ 2026-11-05 (to hơn, đặt theo loại chữ)
        if (span.Rows.Any(r => CleanScreenText(r.Screen(lang)) is { Length: > 0 } t && !IsChat(t))) payload += "|" + CaptionVersion;
        return Sha256Hex(payload)[..16];
    }

    public static string Sha256Hex(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    public static string Sha256Hex(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    // ---------- định dạng số ----------

    static string I(int v) => v.ToString(Inv);
    static string D(double v) => v.ToString("0.######", Inv);
    static string D3(double v) => v.ToString("0.000", Inv);
}
