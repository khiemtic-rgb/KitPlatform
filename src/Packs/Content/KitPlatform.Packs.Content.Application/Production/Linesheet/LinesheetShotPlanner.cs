namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Bảng shot nháp tự sinh, và kiểm tra bảng shot người dựng sửa tay.
/// Quy tắc chia: đổi bối cảnh → shot mới; khoảng lặng ≥ 3 giây đứng riêng một shot (cao trào im lặng);
/// shot đã dài ≥ <c>maxSec</c> thì đóng ở ranh giới hàng.
/// </summary>
public static class LinesheetShotPlanner
{
    public const double DefaultMaxShotSec = 7.0;
    public const int LongPauseMs = 3000;
    public const string PerformanceNote = "cảnh biểu diễn: chỗ ứng viên clip AI";
    public const string PreviewSetting = "preview";

    static readonly string[] TenseWords = ["gắt", "quát", "hoảng", "run", "cuống"];

    public static string SettingFor(LinesheetRow row, LinesheetSettings settings)
    {
        foreach (var rule in settings.RowSettingRules)
        {
            if (rule.Scene != row.Scene) continue;
            if (rule.Ids is { Count: > 0 } ids && ids.Contains(row.Id)) return rule.Setting;
            if (rule.Speaker is { Length: > 0 } speaker && row.Speaker == speaker) return rule.Setting;
            if (rule.CueContains is { Count: > 0 } keys && keys.Any(k => row.Cue.Contains(k, StringComparison.Ordinal)))
                return rule.Setting;
        }

        if (!settings.SceneToSetting.TryGetValue(row.Scene, out var setting))
            throw new LinesheetValidationException([new(0, row.Id, LinesheetErrorCodes.UnknownScene, "Cảnh chưa gắn bối cảnh: " + row.Scene)]);
        return setting;
    }

    /// <summary>Kiểm cấu hình bối cảnh khớp kịch bản trước khi sinh shot.</summary>
    public static IReadOnlyList<LinesheetValidationError> ValidateSettings(IReadOnlyList<LinesheetRow> rows, LinesheetSettings settings)
    {
        var errors = new List<LinesheetValidationError>();
        foreach (var scene in rows.Select(r => r.Scene).Distinct())
        {
            if (!settings.SceneToSetting.ContainsKey(scene))
                errors.Add(new(0, "", LinesheetErrorCodes.UnknownScene, "Cảnh chưa gắn bối cảnh: " + scene));
        }

        var referenced = settings.SceneToSetting.Values.Concat(settings.RowSettingRules.Select(r => r.Setting)).Distinct();
        foreach (var key in referenced.Where(k => !settings.Settings.ContainsKey(k)))
            errors.Add(new(0, "", LinesheetErrorCodes.UnknownSetting, "Bối cảnh chưa khai báo: " + key));
        foreach (var (key, def) in settings.Settings)
        {
            foreach (var cid in def.Cast.Where(c => !settings.Characters.ContainsKey(CharacterId(c))))
                errors.Add(new(0, "", LinesheetErrorCodes.UnknownCharacter, $"Bối cảnh {key}: nhân vật chưa khai báo {cid}"));
        }

        return errors;
    }

    /// <summary>"QUAN@0.3" → "QUAN".</summary>
    public static string CharacterId(string cast) => cast.Split('@')[0];

    public static IReadOnlyList<LinesheetShot> Draft(
        IReadOnlyList<LinesheetRow> rows,
        LinesheetTimeline timeline,
        LinesheetSettings settings,
        double maxSec = DefaultMaxShotSec)
    {
        var errors = ValidateSettings(rows, settings);
        if (errors.Count > 0)
            throw new LinesheetValidationException(errors);

        var times = timeline.Entries.ToDictionary(e => e.RowId);
        var setOf = rows.ToDictionary(r => r.Id, r => SettingFor(r, settings));
        var groups = new List<List<LinesheetRow>>();
        var cur = new List<LinesheetRow>();

        void Close()
        {
            if (cur.Count == 0) return;
            groups.Add(cur);
            cur = new List<LinesheetRow>();
        }

        foreach (var r in rows)
        {
            var longPause = r.IsPause && (r.DurationMs ?? 0) >= LongPauseMs;
            if (cur.Count > 0 && (setOf[r.Id] != setOf[cur[0].Id] || longPause))
                Close();
            cur.Add(r);
            var dur = (times[cur[^1].Id].EndMs - times[cur[0].Id].StartMs) / 1000.0;
            if (longPause || dur >= maxSec)
                Close();
        }

        Close();

        var prefix = EpisodePrefix(rows);
        var shots = new List<LinesheetShot>(groups.Count);
        string? prevSetting = null;
        for (var k = 0; k < groups.Count; k++)
        {
            var g = groups[k];
            var i = k + 1;
            var setting = setOf[g[0].Id];
            var cast = settings.Settings[setting].Cast;
            var castIds = cast.Select(CharacterId).ToList();
            var speakers = g.Where(r => r.IsLine && castIds.Contains(r.Speaker)).Select(r => r.Speaker).ToList();
            var focus = speakers.Count > 0 ? speakers[^1] : castIds.Count == 1 ? castIds[0] : "";
            var text = string.Join(" ", g.Where(r => r.IsLine).Select(r => r.Direction.ToLowerInvariant()));

            string preset;
            if (g.Any(r => r.IsPause && (r.DurationMs ?? 0) >= LongPauseMs))
                preset = LinesheetCameraPresets.SlowZoomIn;
            else if (TenseWords.Any(w => text.Contains(w, StringComparison.Ordinal)))
                preset = LinesheetCameraPresets.ShakeLight;
            else if (g.Any(r => r.IsMusic) && setting != PreviewSetting)
                preset = LinesheetCameraPresets.Parallax;
            else
                preset = LinesheetCameraPresets.DraftRotation[i % LinesheetCameraPresets.DraftRotation.Count];

            var expressions = PickExpressions(g, castIds, settings, setting);
            var notes = g.Any(r => r.IsMusic && r.Cue.StartsWith("song_", StringComparison.Ordinal)) ? PerformanceNote : "";
            shots.Add(new LinesheetShot(
                $"{prefix}_SH{i:000}",
                g[0].Scene,
                setting,
                g[0].Id,
                g[^1].Id,
                cast.ToList(),
                focus,
                preset,
                setting != prevSetting ? LinesheetTransitions.Fade : LinesheetTransitions.Cut,
                "",
                notes,
                expressions));
            prevSetting = setting;
        }

        return shots;
    }

    /// <summary>
    /// Nét mặt của từng nhân vật trong shot: lấy câu thoại cuối của người đó trong shot, so với expression_rules
    /// theo thứ tự (quy tắc đầu tiên khớp thì dùng). Người không nói trong shot giữ nét thường.
    /// </summary>
    public static string PickExpressions(IReadOnlyList<LinesheetRow> group, IReadOnlyList<string> castIds, LinesheetSettings settings, string setting = "")
    {
        if (settings.ExpressionRules.Count == 0) return "";
        var cues = group.Where(r => r.IsMusic || r.IsSfx).Select(r => r.Cue).ToList();
        var picks = new List<string>();
        foreach (var cid in castIds)
        {
            var last = group.LastOrDefault(r => r.IsLine && r.Speaker == cid);
            foreach (var rule in settings.ExpressionRules.Where(x => x.Character == cid && x.Expression.Length > 0))
            {
                if (rule.Settings is { Count: > 0 } only && !only.Contains(setting)) continue;
                var hit = (rule.SettingOnly && rule.Settings is { Count: > 0 })
                          || (rule.CuePrefix is { Count: > 0 } cp && cues.Any(c => cp.Any(p => c.StartsWith(p, StringComparison.Ordinal))))
                          || (last is not null && rule.DirectionContains is { Count: > 0 } dc
                              && dc.Any(k => last.Direction.Contains(k, StringComparison.OrdinalIgnoreCase)))
                          || (last is not null && rule.TextContains is { Count: > 0 } tc
                              && tc.Any(k => last.Vi.Contains(k, StringComparison.OrdinalIgnoreCase)));
                if (!hit) continue;
                picks.Add(cid + ":" + rule.Expression);
                break;
            }
        }

        return string.Join(";", picks);
    }

    /// <summary>"QUAN:hat;BO:gat" → nét của <paramref name="characterId"/>, hoặc "" nếu không có.</summary>
    public static string ExpressionFor(LinesheetShot shot, string characterId)
    {
        foreach (var part in (shot.Expressions ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var i = part.IndexOf(':');
            if (i > 0 && part[..i] == characterId) return FamixaLinesheetService.SafeName(part[(i + 1)..]);
        }

        return "";
    }

    /// <summary>"E01_001" → "E01". Hàng đầu không có dấu gạch dưới thì dùng "EP".</summary>
    public static string EpisodePrefix(IReadOnlyList<LinesheetRow> rows)
    {
        var first = rows.Count > 0 ? rows[0].Id : "";
        var i = first.IndexOf('_');
        return i > 0 ? first[..i] : "EP";
    }

    /// <summary>
    /// Bảng shot sửa tay phải phủ kín dòng thời gian, đúng thứ tự, không chồng nhau, bối cảnh và preset hợp lệ.
    /// </summary>
    public static IReadOnlyList<LinesheetValidationError> ValidateShots(
        IReadOnlyList<LinesheetRow> rows,
        IReadOnlyList<LinesheetShot> shots,
        LinesheetSettings settings)
    {
        var errors = new List<LinesheetValidationError>();
        var pos = rows.Select((r, i) => (r.Id, i)).ToDictionary(x => x.Id, x => x.i);
        if (shots.Count == 0)
        {
            errors.Add(new(0, "", LinesheetErrorCodes.NoShots, "Bảng shot trống."));
            return errors;
        }

        var expectedStart = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in shots)
        {
            if (!seen.Add(s.ShotId))
                errors.Add(new(0, s.ShotId, LinesheetErrorCodes.DuplicateId, "Trùng shot_id."));
            if (!pos.TryGetValue(s.StartRow, out var a) || !pos.TryGetValue(s.EndRow, out var b))
            {
                errors.Add(new(0, s.ShotId, LinesheetErrorCodes.ShotUnknownRow, "start_row/end_row không có trong kịch bản."));
                continue;
            }

            if (a != expectedStart)
                errors.Add(new(0, s.ShotId, a > expectedStart ? LinesheetErrorCodes.ShotGap : LinesheetErrorCodes.ShotOrder,
                    $"Shot phải bắt đầu ở {rows[Math.Min(expectedStart, rows.Count - 1)].Id}, đang là {s.StartRow}."));
            if (b < a)
                errors.Add(new(0, s.ShotId, LinesheetErrorCodes.ShotOrder, "end_row đứng trước start_row."));
            expectedStart = Math.Max(expectedStart, b + 1);

            if (!settings.Settings.ContainsKey(s.Setting))
                errors.Add(new(0, s.ShotId, LinesheetErrorCodes.UnknownSetting, "Bối cảnh chưa khai báo: " + s.Setting));
            if (!LinesheetCameraPresets.IsKnown(s.CameraPreset))
                errors.Add(new(0, s.ShotId, LinesheetErrorCodes.ShotBadPreset, "camera_preset không hợp lệ: " + s.CameraPreset));
            if (!LinesheetTransitions.IsKnown(s.TransitionIn))
                errors.Add(new(0, s.ShotId, LinesheetErrorCodes.ShotBadPreset, "transition_in không hợp lệ: " + s.TransitionIn));
            foreach (var c in s.Characters.Select(CharacterId).Where(c => !settings.Characters.ContainsKey(c)))
                errors.Add(new(0, s.ShotId, LinesheetErrorCodes.UnknownCharacter, "Nhân vật chưa khai báo: " + c));
            if (s.Focus.Length > 0 && !settings.Characters.ContainsKey(s.Focus))
                errors.Add(new(0, s.ShotId, LinesheetErrorCodes.UnknownCharacter, "focus chưa khai báo: " + s.Focus));
        }

        if (expectedStart < rows.Count)
            errors.Add(new(0, "", LinesheetErrorCodes.ShotGap, $"Các hàng từ {rows[expectedStart].Id} chưa thuộc shot nào."));
        return errors;
    }
}
