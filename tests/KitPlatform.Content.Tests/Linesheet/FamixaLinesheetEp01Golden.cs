using System.Globalization;
using System.Text;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Content.Tests.Linesheet;

/// <summary>
/// So lõi C# với bản mẫu Python trên EP01 "Đêm tuyệt nhất" (162 hàng, giọng Edge đã cắt lặng):
/// cùng dòng thời gian, cùng 64 shot, cùng phụ đề, 11.665 khung ở 24 fps.
/// </summary>
public static class FamixaLinesheetEp01Golden
{
    public const int ExpectedTotalMs = 486_060;
    public const int ExpectedFrames24 = 11_665;
    public const int ExpectedShots = 64;

    public static IReadOnlyList<string> Check(string fixturesDir)
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        string Read(string name) => File.ReadAllText(Path.Combine(fixturesDir, name), Encoding.UTF8);

        var rows = LinesheetCsv.ParseScript(Read("ep01_script.csv"));
        Ok(rows.Count == 162 && rows.Count(r => r.IsLine) == 114, "G1 script 162 rows / 114 lines");

        // thời lượng giọng đo được = end_ms - start_ms của bản mẫu
        var golden = LinesheetCsv.ParseTable(Read("ep01_timings.csv"));
        var head = golden[0].Fields;
        int Col(string n) => head.ToList().IndexOf(n);
        var (cId, cType, cStart, cEnd) = (Col("id"), Col("type"), Col("start_ms"), Col("end_ms"));
        var expect = golden.Skip(1).ToDictionary(
            r => r.Fields[cId],
            r => (Start: int.Parse(r.Fields[cStart], CultureInfo.InvariantCulture), End: int.Parse(r.Fields[cEnd], CultureInfo.InvariantCulture), Type: r.Fields[cType]));
        var durations = expect.Where(kv => kv.Value.Type == "line").ToDictionary(kv => kv.Key, kv => kv.Value.End - kv.Value.Start);

        var tl = LinesheetTimelineBuilder.Build(rows, durations, allowMissing: false);
        Ok(tl.TotalMs == ExpectedTotalMs, $"G2 total {tl.TotalMs} == {ExpectedTotalMs}");
        var drift = tl.Entries.Where(e => expect[e.RowId].Start != e.StartMs || expect[e.RowId].End != e.EndMs).Select(e => e.RowId).ToList();
        Ok(drift.Count == 0, "G3 row timings match reference: " + string.Join(",", drift.Take(5)));
        Ok(tl.EstimatedRowIds.Count == 0, "G4 no estimated lines");

        var settings = FamixaLinesheetService.ParseSettings(Read("ep01_settings.json"));
        Ok(LinesheetShotPlanner.ValidateSettings(rows, settings).Count == 0, "G5 settings valid");
        var shots = LinesheetShotPlanner.Draft(rows, tl, settings);
        var reference = LinesheetCsv.ParseShots(Read("ep01_shots.csv"));
        Ok(shots.Count == ExpectedShots && reference.Count == ExpectedShots, $"G6 shot count {shots.Count}");
        for (var i = 0; i < Math.Min(shots.Count, reference.Count); i++)
        {
            var (a, b) = (shots[i], reference[i]);
            var same = a.ShotId == b.ShotId && a.Scene == b.Scene && a.Setting == b.Setting && a.StartRow == b.StartRow
                       && a.EndRow == b.EndRow && a.Characters.SequenceEqual(b.Characters) && a.Focus == b.Focus
                       && a.CameraPreset == b.CameraPreset && a.TransitionIn == b.TransitionIn && a.Notes == b.Notes;
            Ok(same, $"G7 shot {b.ShotId} matches reference (C#: {a.Setting}/{a.StartRow}-{a.EndRow}/{a.CameraPreset}/{a.Focus})");
        }

        Ok(LinesheetShotPlanner.ValidateShots(rows, reference, settings).Count == 0, "G8 reference shots validate");
        Ok(LinesheetCsv.WriteShots(LinesheetCsv.ParseShots(Read("ep01_shots.csv"))).Replace("\r\n", "\n").TrimEnd()
           == Read("ep01_shots.csv").Replace("\r\n", "\n").TrimEnd(), "G9 shots csv byte round trip");

        static string Norm(string s) => s.Replace("\r\n", "\n").TrimEnd();
        Ok(Norm(LinesheetTimelineBuilder.Srt(rows, tl, "vi")) == Norm(Read("ep01_subtitles_vi.srt")), "G10 srt vi identical");
        Ok(Norm(LinesheetTimelineBuilder.Srt(rows, tl, "en")) == Norm(Read("ep01_subtitles_en.srt")), "G11 srt en identical");

        var spans = LinesheetRenderPlan.Spans(rows, tl, shots, 24, 0, tl.TotalMs);
        Ok(spans.Count == ExpectedShots && spans.Sum(s => s.Frames) == ExpectedFrames24, $"G12 frames {spans.Sum(s => s.Frames)} == {ExpectedFrames24}");
        var v = LinesheetRenderPlan.Spans(rows, tl, shots, 24, 0, 45_000);
        Ok(v.Sum(s => s.Frames) == 1080, "G13 45 s clip = 1080 frames");
        return fail;
    }
}
