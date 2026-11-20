using System.Globalization;
using System.Text;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Dòng thời gian tập phim: mỗi hàng bắt đầu ở mốc kết thúc của hàng trước cộng pause_after_ms của hàng trước.
/// Thời lượng câu thoại lấy từ file giọng thật (đã cắt khoảng lặng thừa). Không có file giọng thì ước theo số ký tự,
/// và câu đó được liệt kê trong <see cref="LinesheetTimeline.EstimatedRowIds"/>.
/// </summary>
public static class LinesheetTimelineBuilder
{
    /// <summary>Ký tự mỗi giây khi ước lượng câu thiếu giọng (đo trên giọng Microsoft tiếng Việt: 9–12).</summary>
    public const double DefaultCharsPerSecond = 14.0;

    public static int EstimateLineMs(string text, double charsPerSecond = DefaultCharsPerSecond) =>
        (int)((text ?? "").Length / charsPerSecond * 1000) + 250;

    /// <summary>
    /// Có <paramref name="shots"/> thì giữ thêm cuối shot cho đủ thời gian đọc chữ trên màn hình
    /// (<see cref="LinesheetRenderPlan.ReadingMs"/>). Thêm 2026-11-05 sau soát EP01 bản 9: tin nhắn 0,8 giây, tên phim
    /// 3 giây, "Còn 10 ngày" 2 giây không kịp đọc. Phần giữ thêm nằm sau hàng cuối của shot, không chen vào giữa lời thoại.
    /// </summary>
    public static LinesheetTimeline Build(
        IReadOnlyList<LinesheetRow> rows,
        IReadOnlyDictionary<string, int> lineDurationsMs,
        bool allowMissing,
        double charsPerSecond = DefaultCharsPerSecond,
        IReadOnlyList<LinesheetShot>? shots = null)
    {
        var first = BuildCore(rows, lineDurationsMs, allowMissing, charsPerSecond, null);
        if (shots is null || shots.Count == 0) return first;
        var holds = ReadingHolds(rows, first, shots);
        return holds.Count == 0 ? first : BuildCore(rows, lineDurationsMs, allowMissing, charsPerSecond, holds);
    }

    /// <summary>Số ms giữ thêm sau hàng cuối của từng shot để chữ trong shot đủ thời gian đọc.</summary>
    public static Dictionary<string, int> ReadingHolds(
        IReadOnlyList<LinesheetRow> rows, LinesheetTimeline timeline, IReadOnlyList<LinesheetShot> shots)
    {
        var times = timeline.Entries.ToDictionary(e => e.RowId);
        var pos = rows.Select((r, i) => (r.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var holds = new Dictionary<string, int>();
        for (var k = 0; k < shots.Count; k++)
        {
            var s = shots[k];
            if (!pos.TryGetValue(s.StartRow, out var i0) || !pos.TryGetValue(s.EndRow, out var i1) || i1 < i0) continue;
            var end = k + 1 < shots.Count && times.TryGetValue(shots[k + 1].StartRow, out var next) ? next.StartMs : timeline.TotalMs;
            var deficit = 0;
            for (var i = i0; i <= i1; i++)
            {
                var need = LinesheetRenderPlan.ReadingMs(rows[i]);
                if (need > 0) deficit = Math.Max(deficit, need - (end - times[rows[i].Id].StartMs));
            }

            if (deficit > 0) holds[s.EndRow] = deficit;
        }

        return holds;
    }

    static LinesheetTimeline BuildCore(
        IReadOnlyList<LinesheetRow> rows,
        IReadOnlyDictionary<string, int> lineDurationsMs,
        bool allowMissing,
        double charsPerSecond,
        IReadOnlyDictionary<string, int>? holds)
    {
        var entries = new List<LinesheetTimelineEntry>(rows.Count);
        var estimated = new List<string>();
        var missing = new List<LinesheetValidationError>();
        var t = 0;
        foreach (var r in rows)
        {
            int dur;
            if (r.IsLine)
            {
                if (lineDurationsMs.TryGetValue(r.Id, out var measured) && measured > 0)
                {
                    dur = measured;
                }
                else
                {
                    if (!allowMissing)
                        missing.Add(new(0, r.Id, LinesheetErrorCodes.MissingAudio, "Thiếu file giọng."));
                    dur = EstimateLineMs(r.Vi, charsPerSecond);
                    estimated.Add(r.Id);
                }
            }
            else
            {
                dur = r.DurationMs ?? 0;
            }

            entries.Add(new LinesheetTimelineEntry(r.Id, t, t + dur));
            t += dur + r.PauseAfterMs + (holds is not null && holds.TryGetValue(r.Id, out var hold) ? hold : 0);
        }

        if (missing.Count > 0)
            throw new LinesheetValidationException(missing);
        return new LinesheetTimeline(entries, t, estimated);
    }

    public static string Srt(IReadOnlyList<LinesheetRow> rows, LinesheetTimeline timeline, string lang) =>
        SrtRange(rows, timeline, lang, 0, int.MaxValue);

    /// <summary>
    /// Phụ đề cho khoảng [fromMs, toMs), mốc tính lại từ fromMs. Khoảng không có câu nào thì trả chuỗi rỗng:
    /// FFmpeg không nhận file SRT rỗng, nên bên gọi phải bỏ track phụ đề trong trường hợp đó.
    /// </summary>
    public static string SrtRange(IReadOnlyList<LinesheetRow> rows, LinesheetTimeline timeline, string lang, int fromMs, int toMs)
    {
        var map = timeline.Entries.ToDictionary(e => e.RowId);
        var sb = new StringBuilder();
        var n = 0;
        foreach (var r in rows)
        {
            if (!r.IsLine) continue;
            var text = r.Text(lang).Trim();
            if (text.Length == 0) continue;
            var e = map[r.Id];
            if (e.EndMs <= fromMs || e.StartMs >= toMs) continue;
            n++;
            sb.Append(n.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append(SrtTime(Math.Max(e.StartMs, fromMs) - fromMs)).Append(" --> ")
              .Append(SrtTime(Math.Min(e.EndMs, toMs) - fromMs)).Append('\n');
            sb.Append(text).Append("\n\n");
        }

        return sb.ToString();
    }

    public static string SrtTime(int ms)
    {
        var h = ms / 3_600_000;
        var m = ms / 60_000 % 60;
        var s = ms / 1000 % 60;
        var x = ms % 1000;
        return string.Create(CultureInfo.InvariantCulture, $"{h:00}:{m:00}:{s:00},{x:000}");
    }

    /// <summary>
    /// Mốc ms sang chỉ số khung, làm tròn kiểu "chẵn gần nhất" (giống bản mẫu Python), tính trên mốc tích lũy
    /// để tổng số khung không trôi.
    /// </summary>
    public static int FrameAt(int ms, int fps) => (int)Math.Round(ms * (double)fps / 1000.0, MidpointRounding.ToEven);
}
