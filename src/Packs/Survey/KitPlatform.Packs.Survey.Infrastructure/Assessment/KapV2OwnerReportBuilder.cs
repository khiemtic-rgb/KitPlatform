using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using KitPlatform.Packs.Survey;

namespace KitPlatform.Packs.Survey.Infrastructure;

public sealed record KapV2CategoryInput(string Code, double ScorePct, bool InsufficientData);

public sealed record KapV2OwnerLeak(string Code, int Amount, string Title, string Said, string Body);

public sealed record KapV2OwnerConcern(string Label, string Text);

public sealed record KapV2OwnerAction(
    string Code,
    string Area,
    string Finding,
    string Said,
    string Why,
    string Title,
    IReadOnlyList<string> Steps,
    string Who,
    string Time,
    string Check,
    string? Tool);

public sealed record KapV2OwnerArea(string Code, string Name, int? Pct, string? Note);

public sealed record KapV2OwnerPreview(string Title, string Body);

public sealed record KapV2OwnerReport(
    string Headline,
    IReadOnlyList<KapV2OwnerLeak> Leaks,
    KapV2OwnerConcern? Concern,
    IReadOnlyList<KapV2OwnerAction> Actions,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Watch,
    IReadOnlyList<KapV2OwnerArea> Areas,
    bool HasSoftware,
    IReadOnlyList<KapV2OwnerPreview> Preview);

public sealed class KapV2TemplateIndex
{
    private readonly Dictionary<(string Question, string Option), (int? Score, string Label)> _options = new();
    private readonly Dictionary<string, double> _impact = new(StringComparer.Ordinal);
    private readonly List<(string Category, string Code, bool Scorable)> _questions = [];

    public int? Score(string code, string? option)
    {
        if (option is null) return null;
        return _options.TryGetValue((code, option), out var row) ? row.Score : null;
    }

    public string? Label(string code, string? option)
    {
        if (option is null) return null;
        return _options.TryGetValue((code, option), out var row) ? row.Label : null;
    }

    public double Impact(string code) => _impact.TryGetValue(code, out var n) ? n : 0.8;

    /// <summary>Cùng quy tắc chấm “chưa đủ câu” với AssessmentScoringEngine: ít nhất nửa câu có điểm bị bỏ trống.</summary>
    public bool CategoryInsufficient(string categoryCode, IReadOnlyDictionary<string, string> answers)
    {
        var questions = _questions.Where(q => q.Category == categoryCode && q.Scorable).ToList();
        if (questions.Count == 0) return false;
        var blankScores = questions.Count(q =>
            answers.TryGetValue(q.Code, out var option)
            && _options.TryGetValue((q.Code, option), out var row)
            && row.Score is null);
        return blankScores * 2 >= questions.Count;
    }

    public static KapV2TemplateIndex FromJson(JsonElement template)
    {
        var index = new KapV2TemplateIndex();
        foreach (var cat in template.GetProperty("categories").EnumerateArray())
        foreach (var dim in cat.GetProperty("dimensions").EnumerateArray())
        foreach (var q in dim.GetProperty("questions").EnumerateArray())
        {
            var code = q.GetProperty("code").GetString()!;
            var scorable = !q.TryGetProperty("scorable", out var scorableEl) || scorableEl.ValueKind != JsonValueKind.False;
            index._questions.Add((cat.GetProperty("code").GetString()!, code, scorable));
            if (q.TryGetProperty("metadata", out var meta)
                && meta.ValueKind == JsonValueKind.Object
                && meta.TryGetProperty("impact", out var impact)
                && impact.ValueKind == JsonValueKind.Number)
                index._impact[code] = impact.GetDouble();
            foreach (var opt in q.GetProperty("options").EnumerateArray())
            {
                int? score = opt.TryGetProperty("score", out var s) && s.ValueKind == JsonValueKind.Number
                    ? s.GetInt32()
                    : null;
                index._options[(code, opt.GetProperty("code").GetString()!)] =
                    (score, opt.GetProperty("label").GetString() ?? "");
            }
        }
        return index;
    }

    public static KapV2TemplateIndex FromTemplate(AssessmentTemplateDto template)
    {
        var index = new KapV2TemplateIndex();
        foreach (var cat in template.Categories)
        foreach (var dim in cat.Dimensions)
        foreach (var q in dim.Questions)
        {
            index._questions.Add((cat.Code, q.Code, q.Scorable));
            if (q.Metadata is not null && q.Metadata.TryGetValue("impact", out var raw))
            {
                var n = AsDouble(raw);
                if (n is not null) index._impact[q.Code] = n.Value;
            }
            foreach (var opt in q.Options)
                index._options[(q.Code, opt.Code)] = (opt.Score, opt.Label);
        }
        return index;
    }

    private static double? AsDouble(object raw) => raw switch
    {
        JsonElement el when el.ValueKind == JsonValueKind.Number => el.GetDouble(),
        double d => d,
        float f => f,
        decimal m => (double)m,
        int i => i,
        long l => l,
        _ => null,
    };
}

/// <summary>
/// Bản C# của owner-report.ts. Chữ nằm trong KapV2OwnerCopy.json, tách nguyên từ file đó.
/// </summary>
public static class KapV2OwnerReportBuilder
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly Catalog Data = Load();
    private static readonly HashSet<string> StaffOnly = new(Data.StaffOnly, StringComparer.Ordinal);
    private static readonly string[] AreaOrder = ["CUSTOMER", "INVENTORY", "OPERATIONS", "BUSINESS", "TECH", "GROWTH"];
    private static readonly Dictionary<string, string[]> ConcernQuestions = new(StringComparer.Ordinal)
    {
        ["WHY_STOCKOUT"] = ["I3", "I4"],
        ["WHY_FORGET"] = ["C3", "C4"],
        ["NEAR_WAIT"] = ["I1"],
        ["NEAR_NONE"] = ["I1"],
        ["NEAR_RETURN"] = ["I1"],
        ["STAFF_TURNOVER"] = ["O2"],
        ["STAFF_ADVICE"] = ["O3"],
        ["STAFF_PASSIVE"] = ["O1"],
        ["STAFF_CONTROL"] = ["O4", "I5"],
        ["NUM_MARGIN"] = ["B1"],
        ["NUM_STOCK"] = ["I3", "I4"],
        ["NUM_CUSTOMER"] = ["C5"],
        ["NUM_STAFF"] = ["O4"],
        ["INSP_RECORDS"] = ["O5"],
        ["INSP_STORAGE"] = ["O5"],
        ["INSP_NONE_YET"] = ["O5"],
        ["OPEN_6M"] = ["G2", "O1"],
        ["OPEN_12M"] = ["G2"],
    };

    public static KapV2OwnerReport Build(
        IReadOnlyDictionary<string, string> answers,
        IReadOnlyList<KapV2CategoryInput> categoryScores,
        double overallPct,
        KapV2TemplateIndex template)
    {
        var solo = Get(answers, "O3") == "UNKNOWN" || Get(answers, "O2") == "UNKNOWN";
        var hasSoftware = Get(answers, "T1") is "OPT3" or "OPT4";
        var painQs = new HashSet<string>(StringComparer.Ordinal);
        if (Data.Pain.TryGetValue(Get(answers, "P3") ?? "", out var painList))
            foreach (var q in painList) painQs.Add(q);
        Data.Priority.TryGetValue(Get(answers, "G5") ?? "", out var priorityArea);
        var concernQs = new HashSet<string>(StringComparer.Ordinal);
        if (ConcernBranch(answers) is { } branch && ConcernQuestions.TryGetValue(branch, out var concernList))
            foreach (var q in concernList) concernQs.Add(q);

        bool PainLeak(LeakWork l) =>
            painQs.Contains(LeakQuestion(l.Code)) || priorityArea == "INVENTORY";

        var leaks = BuildLeaks(answers)
            .OrderByDescending(PainLeak)
            .ThenByDescending(l => l.Amount)
            .ToList();
        var bigLeak = new HashSet<string>(
            leaks.Where(l => l.Amount >= 20).Select(l => l.Code == "STOCKOUT" ? "I3" : "I1"),
            StringComparer.Ordinal);

        var ranked = new List<Ranked>();
        for (var order = 0; order < Data.Copy.Count; order++)
        {
            var copy = Data.Copy[order];
            var opt = Get(answers, copy.Code);
            var score = template.Score(copy.Code, opt);
            if (score is null || score > 2) continue;
            if (solo && StaffOnly.Contains(copy.Code)) continue;
            var p = template.Impact(copy.Code) * (4 - score.Value) / 3d;
            if (painQs.Contains(copy.Code)) p *= 1.6;
            if (copy.Area == priorityArea) p *= 1.25;
            if (bigLeak.Contains(copy.Code)) p *= 1.3;
            if (concernQs.Contains(copy.Code)) p *= 1.5;
            ranked.Add(new Ranked(copy, opt!, p, order));
        }
        ranked.Sort((x, y) =>
        {
            var c = y.P.CompareTo(x.P);
            return c != 0 ? c : x.Order.CompareTo(y.Order);
        });

        var perArea = new Dictionary<string, int>(StringComparer.Ordinal);
        var chosen = new List<Ranked>();
        foreach (var r in ranked)
        {
            perArea.TryGetValue(r.Copy.Area, out var n);
            if (n >= 2) continue;
            chosen.Add(r);
            perArea[r.Copy.Area] = n + 1;
            if (chosen.Count == 3) break;
        }

        var actions = chosen.Select(r => ToAction(r.Copy, r.Opt, solo, hasSoftware, answers)).ToList();

        var strengths = new List<string>();
        foreach (var want in new[] { 4, 3 })
        {
            foreach (var copy in Data.Copy)
            {
                var opt = Get(answers, copy.Code);
                if (template.Score(copy.Code, opt) != want) continue;
                if (solo && StaffOnly.Contains(copy.Code)) continue;
                if (copy.Strength is not null && opt is not null && copy.Strength.TryGetValue(opt, out var s) && !strengths.Contains(s))
                    strengths.Add(s);
            }
        }
        var fromInputs = StrengthInputs(answers).Where(x => x is not null).Cast<string>().ToList();
        strengths.InsertRange(0, fromInputs);

        var concernText = ConcernText(answers);
        var concernLabel = template.Label("P3", Get(answers, "P3"));
        KapV2OwnerConcern? concern = concernText is not null && concernLabel is not null
            ? new KapV2OwnerConcern(concernLabel, concernText)
            : null;

        var watch = WatchLines(answers).Where(x => x is not null).Cast<string>().Take(2).ToList();

        var areas = AreaOrder.Select(code =>
        {
            if (!Data.Areas.TryGetValue(code, out var name))
                throw new InvalidOperationException($"missing {code} in [{string.Join("|", Data.Areas.Keys)}] count={Data.Areas.Count}");
            if (code == "GROWTH" && Get(answers, "G2") == "UNKNOWN")
                return new KapV2OwnerArea(code, name, null, "Chưa áp dụng: anh/chị chưa có ý định mở thêm");
            var row = categoryScores.FirstOrDefault(c => c.Code == code);
            if (row is null || row.InsufficientData)
                return new KapV2OwnerArea(code, name, null, "Chưa đủ câu trả lời để chấm");
            return new KapV2OwnerArea(code, name, (int)Math.Round(row.ScorePct, MidpointRounding.AwayFromZero), null);
        }).ToList();

        var score100 = (int)Math.Round(overallPct, MidpointRounding.AwayFromZero);
        var topLeak = leaks.FirstOrDefault(PainLeak);
        string? problem = topLeak is not null
            ? topLeak.Code == "STOCKOUT" ? "đang mất đơn vì hết hàng" : "đang mất tiền vì hàng hết hạn"
            : chosen.Count > 0
                ? (solo && chosen[0].Copy.Solo?.Problem is { Length: > 0 } sp ? sp : chosen[0].Copy.Problem)
                : null;
        var strong = strengths.Count > 0 ? strengths[0] : null;
        string line;
        if (problem is not null && strong is not null) line = $"{strong}, nhưng {problem}.";
        else if (problem is not null) line = $"Nên bắt đầu từ chỗ {problem}.";
        else if (strong is not null) line = $"{strong}. Nhà thuốc đang làm tốt ở hầu hết các phần.";
        else line = "Nhà thuốc đang làm tốt ở hầu hết các phần.";
        var headline = $"Nhà thuốc của anh/chị đạt {score100}/100. {Sentence(line)}";

        var preview = new List<KapV2OwnerPreview>();
        var shownLeaks = new HashSet<string>(StringComparer.Ordinal);
        KapV2OwnerPreview LeakItem(LeakWork l)
        {
            shownLeaks.Add(LeakQuestion(l.Code));
            return new KapV2OwnerPreview(l.Title, $"{l.Said} {l.Body}");
        }
        if (topLeak is not null) preview.Add(LeakItem(topLeak));
        foreach (var act in actions)
        {
            if (preview.Count >= 2) break;
            if (shownLeaks.Contains(act.Code)) continue;
            preview.Add(new KapV2OwnerPreview(act.Finding, $"{act.Said} {act.Why}"));
        }
        var restLeak = leaks.FirstOrDefault(l => !shownLeaks.Contains(LeakQuestion(l.Code)) && l.Amount >= 20);
        if (restLeak is not null && preview.Count == 2 && topLeak is null) preview[1] = LeakItem(restLeak);
        else if (restLeak is not null && preview.Count < 2) preview.Add(LeakItem(restLeak));

        return new KapV2OwnerReport(
            headline,
            leaks.Take(2).Select(l => new KapV2OwnerLeak(l.Code, l.Amount, l.Title, l.Said, l.Body)).ToList(),
            concern,
            actions,
            strengths.Take(3).ToList(),
            watch,
            areas,
            hasSoftware,
            preview);
    }

    public static string ScoreLabel(int score100) => score100 switch
    {
        >= 80 => "Xuất sắc",
        >= 60 => "Khá tốt",
        >= 40 => "Cơ bản",
        _ => "Cần cải thiện",
    };

    private static KapV2OwnerAction ToAction(CopyItem copy, string opt, bool solo, bool hasSoftware, IReadOnlyDictionary<string, string> answers)
    {
        var useSolo = solo && copy.Solo is not null;
        var soloItem = useSolo ? copy.Solo : null;
        var finding = soloItem?.Finding ?? copy.Finding;
        var why = soloItem?.Why ?? WhyOf(copy, opt, answers);
        var title = soloItem?.Title ?? copy.Title!;
        var steps = soloItem?.Steps ?? StepsOf(copy, opt, solo);
        var who = WhoOf(copy, solo, soloItem);
        var time = soloItem?.Time ?? copy.Time;
        var check = soloItem?.Check ?? copy.Check!;
        var app = soloItem?.App ?? copy.App;
        var saidRaw = copy.Said[opt];
        if (saidRaw.StartsWith("anh/chị ", StringComparison.Ordinal))
            saidRaw = saidRaw["anh/chị ".Length..];
        var tool = hasSoftware
            ? app
            : copy.Novixa is not null ? $"Khi muốn làm tự động: {copy.Novixa} (Novixa có sẵn)." : null;
        return new KapV2OwnerAction(
            copy.Code,
            copy.Area,
            finding,
            $"Anh/chị trả lời là {saidRaw}.",
            why,
            title,
            steps,
            who,
            time,
            check,
            tool);
    }

    private static string WhyOf(CopyItem copy, string opt, IReadOnlyDictionary<string, string> answers)
    {
        if (copy.WhyKind == "I1")
        {
            var baseText = opt == "OPT1"
                ? "Thấy tình cờ thì thường đã quá gần ngày hết hạn, khó bán kịp hay trả lại."
                : "Kiểm kê vài tháng mới làm một lần, nên nhiều lô tới lúc thấy thì đã quá gần ngày hết hạn.";
            return Get(answers, "I7") == "NEAR_WAIT"
                ? baseText + " Hiện hàng cận hạn đang để tới khi hết hạn rồi hủy, nên mỗi lô thấy muộn là mất trắng."
                : baseText;
        }
        if (copy.WhyKind == "T1")
            return opt == "OPT1"
                ? "Ghi tay hoặc không ghi thì không tra lại được khách mua gì, thuốc nào bán chạy, lãi bao nhiêu."
                : "Excel ghi được nhưng mỗi người ghi một kiểu, khó cộng số và khó tra lại khách mua gì.";
        return copy.Why ?? "";
    }

    private static string[] StepsOf(CopyItem copy, string opt, bool solo)
    {
        if (copy.StepsKind == "T1")
            return opt == "OPT1"
                ? [
                    "Mỗi đơn bán ghi 4 cột: ngày, tên thuốc, số lượng, tiền. Có số điện thoại khách thì ghi thêm.",
                    "Cuối tuần cộng lại để biết thuốc nào bán nhiều nhất.",
                ]
                : [
                    "Thống nhất một mẫu Excel: ngày, tên thuốc, số lượng, giá bán, giá nhập, số điện thoại khách.",
                    "Mọi người ghi chung một file. Cuối ngày anh/chị xem file đã đủ đơn chưa.",
                ];
        if (copy.StepsKind == "O5")
            return [
                "Viết ra các sổ phải ghi hằng ngày, hằng tuần: nhiệt độ, độ ẩm, vệ sinh, sổ thuốc kê đơn…",
                solo
                    ? "Gắn mỗi sổ với một giờ cố định, ví dụ ghi nhiệt độ, độ ẩm lúc mở cửa và trước khi đóng cửa. Dán bảng giờ cạnh nhiệt ẩm kế."
                    : "Mỗi sổ ghi rõ ai ghi và ghi lúc mấy giờ. Dán bảng này cạnh nhiệt ẩm kế.",
            ];
        return copy.Steps ?? [];
    }

    private static string WhoOf(CopyItem copy, bool solo, SoloItem? soloItem)
    {
        if (copy.WhoKind == "O5" && soloItem?.Who is null)
            return solo ? "Anh/chị" : "Người được giao từng sổ";
        return copy.Who ?? "";
    }

    private static List<LeakWork> BuildLeaks(IReadOnlyDictionary<string, string> answers)
    {
        var leaks = new List<LeakWork>();
        var i2 = Get(answers, "I2");
        if (i2 is not null && Data.ExpiredMonth.TryGetValue(i2, out var exp))
        {
            var yearly = exp * 12d;
            var amount = i2 == "EXP_GT7M" ? 84 : Millions(yearly);
            var text = i2 == "EXP_GT7M" ? "trên 84 triệu" : $"khoảng {amount} triệu";
            leaks.Add(new LeakWork(
                "EXPIRED",
                amount,
                $"Hàng hết hạn: {text} mỗi năm",
                $"Anh/chị cho biết tháng trước hàng hết hạn phải hủy hoặc trả lại khoảng {Data.ExpiredLabel[i2]}.",
                $"Nhân lên 12 tháng là {text} mỗi năm. Phần trả lại được nhà cung cấp chưa trừ ra, nhưng phần phải hủy là tiền mất hẳn."));
        }
        var i3 = Get(answers, "I3");
        var p5 = Get(answers, "P5");
        if (i3 is not null && p5 is not null
            && Data.StockoutPerWeek.TryGetValue(i3, out var perWeek)
            && Data.AovMid.TryGetValue(p5, out var aov))
        {
            var amount = Millions(perWeek * 52d * aov);
            leaks.Add(new LeakWork(
                "STOCKOUT",
                amount,
                $"Hết hàng: có thể mất tới {amount} triệu tiền bán mỗi năm",
                $"Anh/chị cho biết tuần trước có {Data.StockoutLabel[i3]} khách hỏi mà quầy không có thuốc, mỗi đơn trung bình {Data.AovLabel[p5]}.",
                $"Nếu mỗi lần như vậy khách sang chỗ khác mua, một năm nhà thuốc có thể mất tới khoảng {amount} triệu tiền bán. Thực tế thấp hơn vì có khách chịu chờ hoặc đổi thuốc khác."));
        }
        return leaks;
    }

    private static string? ConcernBranch(IReadOnlyDictionary<string, string> answers) =>
        Get(answers, "P3") switch
        {
            "PAIN_CUSTOMER" => Get(answers, "C6"),
            "PAIN_INVENTORY" => Get(answers, "I7"),
            "PAIN_STAFF" => Get(answers, "O7"),
            "PAIN_NUMBERS" => Get(answers, "B5"),
            "PAIN_COMPLIANCE" => Get(answers, "O6"),
            "PAIN_GROWTH" => Get(answers, "G3"),
            _ => null,
        };

    private static string? ConcernText(IReadOnlyDictionary<string, string> a)
    {
        var pain = Get(a, "P3");
        if (pain == "PAIN_COMPLIANCE")
        {
            Data.Concern.ComplianceByO6.TryGetValue(Get(a, "O6") ?? "", out var baseText);
            var t4 = Get(a, "T4") == "OPT3" ? Data.Concern.ComplianceT4 : "";
            if (!string.IsNullOrEmpty(baseText)) return baseText + t4;
            var trimmed = (t4 ?? "").Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }
        if (pain is null || !Data.Concern.Maps.TryGetValue(pain, out var map)) return null;
        var key = pain switch
        {
            "PAIN_CUSTOMER" => Get(a, "C6"),
            "PAIN_INVENTORY" => Get(a, "I7"),
            "PAIN_STAFF" => Get(a, "O7"),
            "PAIN_NUMBERS" => Get(a, "B5"),
            "PAIN_GROWTH" => Get(a, "G3"),
            _ => null,
        };
        if (key is null || !map.TryGetValue(key, out var text)) return null;
        return text;
    }

    private static IEnumerable<string?> StrengthInputs(IReadOnlyDictionary<string, string> a)
    {
        yield return Get(a, "C1") switch
        {
            "REG_9_10" => "Gần như khách nào cũng là khách quen",
            "REG_6_8" => "6–8 trong 10 khách là khách quen",
            _ => null,
        };
        yield return Get(a, "I2") == "EXP_0" ? "Gần như không có hàng hết hạn phải hủy" : null;
        yield return Get(a, "I7") == "NEAR_PUSH" ? "Hàng cận hạn được giảm giá, bán trước" : null;
    }

    private static IEnumerable<string?> WatchLines(IReadOnlyDictionary<string, string> a)
    {
        yield return Get(a, "I2") == "UNKNOWN" ? "Chưa rõ tiền hàng hết hạn mỗi tháng: từ tháng này, mỗi lần hủy hay trả hàng thì ghi lại số tiền." : null;
        yield return Get(a, "I3") == "UNKNOWN" ? "Chưa để ý mỗi tuần khách hỏi mà hết hàng bao nhiêu lần: thử ghi lại trong 1 tuần." : null;
        yield return Get(a, "P5") == "UNKNOWN" ? "Chưa rõ mỗi đơn trung bình bao nhiêu tiền: lấy tiền bán 1 ngày chia cho số đơn." : null;
        yield return Get(a, "C1") == "UNKNOWN" ? "Chưa để ý trong 10 khách có mấy khách quen: thử đếm trong 1 ngày." : null;
        yield return Get(a, "C4") == "UNKNOWN" ? "Chưa theo dõi việc hỏi thăm khách sau khi bán: ghi lại mỗi lần nhắn hỏi thăm." : null;
        yield return Get(a, "T4") == "UNKNOWN" ? "Chưa rõ yêu cầu liên thông dữ liệu bán thuốc: hỏi bên bán phần mềm hoặc Sở Y tế xem nhà thuốc cần làm gì." : null;
    }

    private static int Millions(double v)
    {
        var m = v / 1_000_000d;
        var n = m >= 10
            ? Math.Round(m / 5d, MidpointRounding.AwayFromZero) * 5d
            : Math.Round(m, MidpointRounding.AwayFromZero);
        return (int)n;
    }

    private static string Sentence(string s) =>
        s.Length == 0 ? s : s[..1].ToUpper(Vi) + s[1..];

    private static string LeakQuestion(string code) => code == "STOCKOUT" ? "I3" : "I1";

    private static string? Get(IReadOnlyDictionary<string, string> answers, string code) =>
        answers.TryGetValue(code, out var v) ? v : null;

    private static CopyItem ReadCopy(JsonElement el) => new()
    {
        Code = Str(el, "code") ?? "",
        Area = Str(el, "area") ?? "",
        Finding = Str(el, "finding") ?? "",
        Problem = Str(el, "problem") ?? "",
        Said = ReadStringMap(el, "said"),
        Why = Str(el, "why"),
        WhyKind = Str(el, "whyKind"),
        Title = Str(el, "title"),
        Steps = Strings(el, "steps"),
        StepsKind = Str(el, "stepsKind"),
        Who = Str(el, "who"),
        WhoKind = Str(el, "whoKind"),
        Time = Str(el, "time") ?? "",
        Check = Str(el, "check"),
        App = Str(el, "app"),
        Novixa = Str(el, "novixa"),
        Strength = el.TryGetProperty("strength", out var strength) && strength.ValueKind == JsonValueKind.Object
            ? ReadStringMap(el, "strength")
            : null,
        Solo = ReadSolo(el),
    };

    private static SoloItem? ReadSolo(JsonElement el)
    {
        if (!el.TryGetProperty("solo", out var solo) || solo.ValueKind != JsonValueKind.Object) return null;
        return new SoloItem
        {
            Finding = Str(solo, "finding"),
            Problem = Str(solo, "problem"),
            Why = Str(solo, "why"),
            Title = Str(solo, "title"),
            Steps = Strings(solo, "steps"),
            Check = Str(solo, "check"),
            Time = Str(solo, "time"),
            App = Str(solo, "app"),
            Who = Str(solo, "who"),
        };
    }

    private static string? Str(JsonElement el, string name) =>
        el.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string[]? Strings(JsonElement el, string name) =>
        el.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(x => x.GetString()!).ToArray()
            : null;

    private static Dictionary<string, string> ReadStringMap(JsonElement parent, string name)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!parent.TryGetProperty(name, out var el)) return map;
        foreach (var p in el.EnumerateObject())
            if (p.Value.ValueKind == JsonValueKind.String)
                map[p.Name] = p.Value.GetString()!;
        return map;
    }

    private static Dictionary<string, string?> ReadNullableStringMap(JsonElement parent, string name)
    {
        var map = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (!parent.TryGetProperty(name, out var el)) return map;
        foreach (var p in el.EnumerateObject())
            map[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : null;
        return map;
    }

    private static Dictionary<string, double> ReadDoubleMap(JsonElement parent, string name)
    {
        var map = new Dictionary<string, double>(StringComparer.Ordinal);
        if (!parent.TryGetProperty(name, out var el)) return map;
        foreach (var p in el.EnumerateObject())
            map[p.Name] = p.Value.GetDouble();
        return map;
    }

    private static Dictionary<string, string[]> ReadStringArrayMap(JsonElement parent, string name)
    {
        var map = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (!parent.TryGetProperty(name, out var el)) return map;
        foreach (var p in el.EnumerateObject())
            map[p.Name] = p.Value.EnumerateArray().Select(x => x.GetString()!).ToArray();
        return map;
    }

    private static Catalog Load()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("KapV2OwnerCopy.json", StringComparison.Ordinal));
        using var stream = name is null ? null : asm.GetManifestResourceStream(name);
        if (stream is null)
            throw new InvalidOperationException("Thiếu KapV2OwnerCopy.json. Có: " + string.Join(" | ", asm.GetManifestResourceNames()));
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;
        var catalog = new Catalog
        {
            Copy = root.GetProperty("copy").EnumerateArray().Select(ReadCopy).ToList(),
            StaffOnly = root.GetProperty("staffOnly").EnumerateArray().Select(x => x.GetString()!).ToList(),
            Areas = ReadStringMap(root, "areas"),
            AovLabel = ReadStringMap(root, "aovLabel"),
            StockoutLabel = ReadStringMap(root, "stockoutLabel"),
            ExpiredLabel = ReadStringMap(root, "expiredLabel"),
            AovMid = ReadDoubleMap(root, "aovMid"),
            StockoutPerWeek = ReadDoubleMap(root, "stockoutPerWeek"),
            ExpiredMonth = ReadDoubleMap(root, "expiredMonth"),
            Pain = ReadStringArrayMap(root, "pain"),
            Priority = ReadNullableStringMap(root, "priority"),
        };
        if (root.TryGetProperty("concern", out var concernEl))
        {
            catalog.Concern.PainCustomer = ReadStringMap(concernEl, "PAIN_CUSTOMER");
            catalog.Concern.PainInventory = ReadStringMap(concernEl, "PAIN_INVENTORY");
            catalog.Concern.PainStaff = ReadStringMap(concernEl, "PAIN_STAFF");
            catalog.Concern.PainNumbers = ReadStringMap(concernEl, "PAIN_NUMBERS");
            catalog.Concern.PainGrowth = ReadStringMap(concernEl, "PAIN_GROWTH");
            catalog.Concern.ComplianceByO6 = ReadStringMap(concernEl, "complianceByO6");
            catalog.Concern.ComplianceT4 = concernEl.GetProperty("complianceT4").GetString() ?? "";
        }
        return catalog;
    }

    private sealed record Ranked(CopyItem Copy, string Opt, double P, int Order);

    private sealed record LeakWork(string Code, int Amount, string Title, string Said, string Body);

    private sealed class Catalog
    {
        public List<CopyItem> Copy { get; set; } = [];
        public Dictionary<string, string[]> Pain { get; set; } = new();
        public Dictionary<string, string?> Priority { get; set; } = new();
        public List<string> StaffOnly { get; set; } = [];
        public Dictionary<string, string> Areas { get; set; } = new();
        public Dictionary<string, double> AovMid { get; set; } = new();
        public Dictionary<string, string> AovLabel { get; set; } = new();
        public Dictionary<string, double> StockoutPerWeek { get; set; } = new();
        public Dictionary<string, string> StockoutLabel { get; set; } = new();
        public Dictionary<string, double> ExpiredMonth { get; set; } = new();
        public Dictionary<string, string> ExpiredLabel { get; set; } = new();
        public ConcernCatalog Concern { get; set; } = new();
    }

    private sealed class ConcernCatalog
    {
        [JsonPropertyName("PAIN_CUSTOMER")]
        public Dictionary<string, string> PainCustomer { get; set; } = new();
        [JsonPropertyName("PAIN_INVENTORY")]
        public Dictionary<string, string> PainInventory { get; set; } = new();
        [JsonPropertyName("PAIN_STAFF")]
        public Dictionary<string, string> PainStaff { get; set; } = new();
        [JsonPropertyName("PAIN_NUMBERS")]
        public Dictionary<string, string> PainNumbers { get; set; } = new();
        [JsonPropertyName("PAIN_GROWTH")]
        public Dictionary<string, string> PainGrowth { get; set; } = new();
        public Dictionary<string, string> ComplianceByO6 { get; set; } = new();
        public string ComplianceT4 { get; set; } = "";

        [JsonIgnore]
        public IReadOnlyDictionary<string, Dictionary<string, string>> Maps => new Dictionary<string, Dictionary<string, string>>
        {
            ["PAIN_CUSTOMER"] = PainCustomer,
            ["PAIN_INVENTORY"] = PainInventory,
            ["PAIN_STAFF"] = PainStaff,
            ["PAIN_NUMBERS"] = PainNumbers,
            ["PAIN_GROWTH"] = PainGrowth,
        };
    }

    private sealed class CopyItem
    {
        public string Code { get; set; } = "";
        public string Area { get; set; } = "";
        public string Finding { get; set; } = "";
        public string Problem { get; set; } = "";
        public Dictionary<string, string> Said { get; set; } = new();
        public string? Why { get; set; }
        public string? WhyKind { get; set; }
        public string? Title { get; set; }
        public string[]? Steps { get; set; }
        public string? StepsKind { get; set; }
        public string? Who { get; set; }
        public string? WhoKind { get; set; }
        public string Time { get; set; } = "";
        public string? Check { get; set; }
        public string? App { get; set; }
        public string? Novixa { get; set; }
        public Dictionary<string, string>? Strength { get; set; }
        public SoloItem? Solo { get; set; }
    }

    private sealed class SoloItem
    {
        public string? Finding { get; set; }
        public string? Problem { get; set; }
        public string? Why { get; set; }
        public string? Title { get; set; }
        public string[]? Steps { get; set; }
        public string? Check { get; set; }
        public string? Time { get; set; }
        public string? App { get; set; }
        public string? Who { get; set; }
    }
}
