using System.Globalization;
using System.Text;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Đọc và ghi CSV (RFC 4180: dấu phẩy, ngoặc kép, xuống dòng trong ô) cho kịch bản bảng câu và bảng shot.
/// Kiểm toàn bộ trước khi trả về: lỗi ở bất kỳ hàng nào thì không nhập hàng nào.
/// </summary>
public static class LinesheetCsv
{
    public static readonly IReadOnlyList<string> ScriptColumns =
    [
        "id", "scene", "type", "speaker", "vi", "en", "direction",
        "duration_ms", "pause_after_ms", "cue", "screen_vi", "screen_en",
    ];

    /// <summary>Cột tùy chọn. Thiếu thì coi như rỗng.</summary>
    public const string TestFirstColumn = "test_first";

    /// <summary>Cột tùy chọn: thẻ cảm xúc ElevenLabs ghi tay cho câu (ví dụ "[whispers]"), thay cho thẻ tự suy từ direction.</summary>
    public const string TtsTagsColumn = "tts_tags";

    public static readonly IReadOnlyList<string> ShotColumns =
    [
        "shot_id", "scene", "setting", "start_row", "end_row", "characters",
        "focus", "camera_preset", "transition_in", "ai_clip_slot", "notes",
    ];

    /// <summary>Cột tùy chọn của bảng shot (M1.1): nét mặt, dạng "QUAN:hat;BO:gat".</summary>
    public const string ExpressionsColumn = "expressions";

    public static IReadOnlyList<LinesheetRow> ParseScript(string text)
    {
        var table = ParseTable(text);
        var errors = new List<LinesheetValidationError>();
        if (table.Count == 0)
            throw new LinesheetValidationException([new(0, "", LinesheetErrorCodes.Empty, "File trống.")]);

        var header = table[0].Fields.Select(h => h.Trim().TrimStart('﻿')).ToList();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < header.Count; i++)
            index.TryAdd(header[i], i);
        foreach (var col in ScriptColumns.Where(c => !index.ContainsKey(c)))
            errors.Add(new(1, "", LinesheetErrorCodes.MissingColumn, "Thiếu cột " + col));
        if (errors.Count > 0)
            throw new LinesheetValidationException(errors);

        var rows = new List<LinesheetRow>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ord = 0;
        foreach (var rec in table.Skip(1))
        {
            if (rec.Fields.All(string.IsNullOrWhiteSpace))
                continue;
            string F(string col)
            {
                var i = index.TryGetValue(col, out var k) ? k : -1;
                return i >= 0 && i < rec.Fields.Count ? rec.Fields[i] : "";
            }

            if (rec.Fields.Count != header.Count)
                errors.Add(new(rec.Line, F("id"), LinesheetErrorCodes.ColumnCount,
                    $"Hàng có {rec.Fields.Count} cột, tiêu đề có {header.Count} cột."));

            var id = F("id").Trim();
            var type = F("type").Trim();
            if (id.Length == 0)
                errors.Add(new(rec.Line, "", LinesheetErrorCodes.EmptyId, "Thiếu id."));
            else if (!seen.Add(id))
                errors.Add(new(rec.Line, id, LinesheetErrorCodes.DuplicateId, "Trùng id."));
            if (!LinesheetRowTypes.All.Contains(type))
                errors.Add(new(rec.Line, id, LinesheetErrorCodes.UnknownType, $"type không hợp lệ: '{type}'."));

            var duration = ParseOptionalInt(F("duration_ms"), rec.Line, id, "duration_ms", errors);
            var pause = ParseOptionalInt(F("pause_after_ms"), rec.Line, id, "pause_after_ms", errors) ?? 0;
            if (type == LinesheetRowTypes.Line && string.IsNullOrWhiteSpace(F("vi")))
                errors.Add(new(rec.Line, id, LinesheetErrorCodes.EmptyLine, "Câu thoại trống."));
            if (type is LinesheetRowTypes.Pause or LinesheetRowTypes.Music or LinesheetRowTypes.Sfx && duration is null)
                errors.Add(new(rec.Line, id, LinesheetErrorCodes.MissingDuration, "Thiếu duration_ms."));

            rows.Add(new LinesheetRow(
                ord++,
                id,
                F("scene").Trim(),
                type,
                F("speaker").Trim(),
                F("vi"),
                F("en"),
                F("direction"),
                duration,
                pause,
                F("cue").Trim(),
                F("screen_vi"),
                F("screen_en"),
                F(TestFirstColumn).Trim().Equals("x", StringComparison.OrdinalIgnoreCase),
                F(TtsTagsColumn).Trim()));
        }

        if (rows.Count == 0 && errors.Count == 0)
            errors.Add(new(0, "", LinesheetErrorCodes.Empty, "Không có hàng nào."));
        if (errors.Count > 0)
            throw new LinesheetValidationException(errors);
        return rows;
    }

    static int? ParseOptionalInt(string raw, int line, string id, string col, List<LinesheetValidationError> errors)
    {
        raw = (raw ?? "").Trim();
        if (raw.Length == 0) return null;
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v >= 0)
            return v;
        errors.Add(new(line, id, LinesheetErrorCodes.BadNumber, $"{col} không phải số nguyên không âm: '{raw}'."));
        return null;
    }

    public static IReadOnlyList<LinesheetShot> ParseShots(string text)
    {
        var table = ParseTable(text);
        var errors = new List<LinesheetValidationError>();
        if (table.Count == 0)
            throw new LinesheetValidationException([new(0, "", LinesheetErrorCodes.Empty, "File trống.")]);
        var header = table[0].Fields.Select(h => h.Trim().TrimStart('﻿')).ToList();
        var index = header.Select((h, i) => (h, i)).GroupBy(x => x.h).ToDictionary(g => g.Key, g => g.First().i);
        foreach (var col in ShotColumns.Where(c => !index.ContainsKey(c)))
            errors.Add(new(1, "", LinesheetErrorCodes.MissingColumn, "Thiếu cột " + col));
        if (errors.Count > 0)
            throw new LinesheetValidationException(errors);

        var shots = new List<LinesheetShot>();
        foreach (var rec in table.Skip(1))
        {
            if (rec.Fields.All(string.IsNullOrWhiteSpace)) continue;
            string F(string col) => index[col] < rec.Fields.Count ? rec.Fields[index[col]].Trim() : "";
            shots.Add(new LinesheetShot(
                F("shot_id"), F("scene"), F("setting"), F("start_row"), F("end_row"),
                F("characters").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                F("focus"), F("camera_preset"), F("transition_in"), F("ai_clip_slot"), F("notes"),
                index.ContainsKey(ExpressionsColumn) ? F(ExpressionsColumn) : ""));
        }

        return shots;
    }

    public static string WriteShots(IReadOnlyList<LinesheetShot> shots)
    {
        var sb = new StringBuilder();
        // cột nét mặt chỉ ghi khi có dùng, để bảng shot cũ (M1) ghi ra y nguyên
        var withExpr = shots.Any(s => !string.IsNullOrEmpty(s.Expressions));
        WriteRecord(sb, withExpr ? [.. ShotColumns, ExpressionsColumn] : ShotColumns);
        foreach (var s in shots)
        {
            List<string> f =
            [
                s.ShotId, s.Scene, s.Setting, s.StartRow, s.EndRow, string.Join(";", s.Characters),
                s.Focus, s.CameraPreset, s.TransitionIn, s.AiClipSlot, s.Notes,
            ];
            if (withExpr) f.Add(s.Expressions);
            WriteRecord(sb, f);
        }

        return sb.ToString();
    }

    public static string WriteTimeline(IReadOnlyList<LinesheetRow> rows, LinesheetTimeline timeline)
    {
        var sb = new StringBuilder();
        WriteRecord(sb, ["id", "scene", "type", "speaker", "start_ms", "end_ms", "vi", "en", "screen_vi", "screen_en", "cue"]);
        var map = timeline.Entries.ToDictionary(e => e.RowId);
        foreach (var r in rows)
        {
            var e = map[r.Id];
            WriteRecord(sb,
            [
                r.Id, r.Scene, r.Type, r.Speaker,
                e.StartMs.ToString(CultureInfo.InvariantCulture), e.EndMs.ToString(CultureInfo.InvariantCulture),
                r.Vi, r.En, r.ScreenVi, r.ScreenEn, r.Cue,
            ]);
        }

        return sb.ToString();
    }

    public static void WriteRecord(StringBuilder sb, IReadOnlyList<string> fields)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(Quote(fields[i] ?? ""));
        }

        sb.Append("\r\n");
    }

    static string Quote(string v) =>
        v.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;

    public sealed record Record(int Line, IReadOnlyList<string> Fields);

    /// <summary>Tách CSV thành các bản ghi. <see cref="Record.Line"/> là số dòng (1-based) nơi bản ghi bắt đầu.</summary>
    public static IReadOnlyList<Record> ParseTable(string text)
    {
        text ??= "";
        if (text.Length > 0 && text[0] == '﻿') text = text[1..];
        var records = new List<Record>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var line = 1;
        var recordStart = 1;
        var any = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    if (c == '\n') line++;
                    field.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    any = true;
                    break;
                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    any = true;
                    break;
                case '\r':
                    break;
                case '\n':
                    fields.Add(field.ToString());
                    field.Clear();
                    records.Add(new Record(recordStart, fields.ToList()));
                    fields.Clear();
                    any = false;
                    line++;
                    recordStart = line;
                    break;
                default:
                    field.Append(c);
                    any = true;
                    break;
            }
        }

        if (any || field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            records.Add(new Record(recordStart, fields.ToList()));
        }

        return records;
    }
}
