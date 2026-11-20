using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace KitPlatform.Packs.Content.Infrastructure;

public interface INgvHinhPlanService
{
    Task<NgvHinhDraft> ComposeAsync(string scriptPath, string channel, string examples, CancellationToken ct);
}

/// <summary>Asks the configured text model for a .hinh.json, then keeps it only when the checker passes.</summary>
internal sealed class NgvHinhPlanService : INgvHinhPlanService
{
    readonly ContentGeminiClient _gemini;

    public NgvHinhPlanService(ContentGeminiClient gemini) => _gemini = gemini;

    public async Task<NgvHinhDraft> ComposeAsync(string scriptPath, string channel, string examples, CancellationToken ct)
    {
        var lines = await RunCheckerAsync(scriptPath, null, channel, dumpLines: true, ct);
        if (lines.Lines.Count == 0)
            return NgvHinhDraft.Fail("Kịch bản không có câu đọc.");
        var numbered = string.Join("\n", lines.Lines.Select((row, index) =>
            $"{index + 1}. {(row.Short ? "[ngắn] " : "")}{row.Text}"));
        var system = SystemPrompt(channel);
        var ask = "Kịch bản, câu đã đánh số đúng thứ tự đọc:\n" + numbered
            + "\n\nMẫu cùng kênh, học cách đặt cảnh và chữ ngắn, không chép nội dung:\n" + examples
            + "\n\nTrả về đúng một JSON .hinh.json, khoảng 10 đến 16 cảnh. Cảnh đầu bắt đầu ở câu 1. first chép đúng đầu câu đọc, không chép chữ [ngắn]. Câu [ngắn] từ câu 2 trở đi không được là first. Chữ trên hình tối đa 5 từ. Khung ngang dễ tràn: edu_steps tối đa 3 bước, edu_rules tối đa 4 dòng, không đặt câu dài vào title.";
        var tokens = 0;
        string? last = null;
        var errors = new List<string>();
        for (var round = 0; round < 3; round++)
        {
            var prior = last != null && last.TrimStart().StartsWith('{') ? "\n\nBản vừa rồi:\n" + (last.Length > 24000 ? last[..24000] : last) : "";
            var user = round == 0
                ? ask
                : ask + prior + "\n\nSửa đúng các lỗi này và trả lại toàn bộ JSON, không thêm lời giải thích:\n- " + string.Join("\n- ", errors.Take(30));
            string raw;
            try
            {
                raw = await _gemini.GenerateJsonAsync(system, user, ct, maxOutputTokens: 32768, disableThinking: true);
            }
            catch (InvalidOperationException ex)
            {
                return NgvHinhDraft.Fail(ex.Message);
            }
            tokens += Math.Max(1, (system.Length + user.Length + raw.Length) / 4);
            var json = SliceJson(raw);
            if (json == null)
            {
                var preview = raw.Trim().Replace("\r", " ").Replace("\n", " ");
                if (preview.Length > 160) preview = preview[..160];
                errors = ["Kết quả không phải JSON. " + preview];
                last = raw;
                continue;
            }
            var planPath = Path.Combine(Path.GetTempPath(), "hinh-draft-" + Guid.NewGuid().ToString("N") + ".json");
            await File.WriteAllTextAsync(planPath, json, new UTF8Encoding(false), ct);
            try
            {
                var check = await RunCheckerAsync(scriptPath, planPath, channel, dumpLines: false, ct);
                if (check.Ok)
                    return new NgvHinhDraft(true, Pretty(json), tokens, round, check.Scenes, check.LineCount, []);
                errors = check.Errors.Count > 0 ? check.Errors : ["Kế hoạch chưa đạt."];
                last = json;
            }
            finally
            {
                File.Delete(planPath);
            }
        }
        return new NgvHinhDraft(false, last, tokens, 2, 0, lines.Lines.Count, errors);
    }

    static string SystemPrompt(string channel)
    {
        var ngv = channel == "ngv";
        return """
            Bạn viết file .hinh.json cho Video Studio. Chỉ trả JSON, không markdown.
            Schema: {"palette":{"background":[16,30,62],"paper":[26,44,84],"ink":[245,240,228],"muted":[170,184,208],"accent":[222,176,84],"good":[84,196,140],"warn":[236,160,70],"danger":[232,100,100]},"scenes":[{"first":"đúng đầu câu đọc","template":"...","params":{}}]}
            Mẫu được phép và tham số:
            - edu_title: title, subtitle, tag, icon
            - edu_bignumber: number, label, source, chain (mảng chữ ngắn), icon, kicker
            - edu_steps: title, steps (2 đến 4 phần tử {head, text, icon})
            - edu_contrast: title, left {head, text, items, icon, big}, right cùng dạng
            - edu_rules: title, rules (mảng chữ), icons (mảng tên icon)
            - nv_board: items. Mỗi item: art, x, y, s (tâm và cỡ theo % vùng an toàn), w (tuỳ chọn), args, text (chỉ khi art là note), tone.
            Chuyển động sẵn có, không cần khoá push (máy đẩy chậm tự chạy): line (câu thứ mấy trong cảnh, 0 là câu mở), d (giây), from (pop|rise|drop|left|right|fade|flip|none), move ([{line,x,y}]), hide ({line}), bob (true/false). Mẫu edu dùng focus: mảng, mỗi phần tử là chỉ số mục hiện khi tới câu đó.
            Mỗi cảnh nv_board kể chuyện nên có ít nhất hai hình hiện theo câu (line khác nhau) và một move hoặc hide.
            Câu đánh dấu [ngắn] không được là first, trừ khi đó là câu 1 của cả kịch bản. Mọi câu [ngắn] phía sau nằm trong cảnh đứng trước: first của cảnh sau là câu đọc thường ngay sau câu ngắn đó.
            Số trong number, text, tag, note chỉ được là số có trong câu đọc của chính cảnh đó.
            Icon lấy từ: coin, coins, wallet, wallet_empty, lock, shield, check, cross, warn, clock, scale, flame, line_down, line_up, candles, candles_down, gauge, phone, hop, ke, so_tay, ket, khach, nhan_vien, danh_sach, lo_thuoc.
            """ + (ngv
            ? """

            Luật kênh Người Giữ Vốn: hình nv_board chỉ được keeper, keeper_desk, wrist, watch, jar, cup, keys và đồ vật note, coin, coins, bill, wallet, clock, lock, mark, arrow, card, phone, receipt, book, scale, tag, calendar, bag, mag, bubble, report.
            Cấm person, customer, box, bottle, blister, store, shelf, screen. Không mã chứng khoán, không tiền số, không tên sàn, không phép tính bằng đô la.
            """
            : "");
    }

    static async Task<CheckerResult> RunCheckerAsync(string scriptPath, string? planPath, string channel, bool dumpLines, CancellationToken ct)
    {
        var repo = FindRepo();
        var checker = repo == null ? null : Path.Combine(repo, "tools", "kit-video-library", "ngv-edu", "hinh_check.py");
        if (checker == null || !File.Exists(checker))
            return CheckerResult.Broken("Không thấy bộ kiểm tra kế hoạch hình.");
        var python = Environment.GetEnvironmentVariable("Content__PythonPath")
            ?? Environment.GetEnvironmentVariable("CONTENT_PYTHON_PATH")
            ?? "python";
        var args = dumpLines
            ? $"\"{checker}\" --script \"{scriptPath}\" --lines"
            : $"\"{checker}\" --script \"{scriptPath}\" --plan \"{planPath}\" --channel \"{channel}\"";
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = python,
                Arguments = args,
                WorkingDirectory = repo,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
            },
        };
        process.StartInfo.Environment["PYTHONIOENCODING"] = "utf-8";
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(8));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (Exception) { /* already gone */ }
            return CheckerResult.Broken("Kiểm tra kế hoạch hình quá lâu.");
        }
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (string.IsNullOrWhiteSpace(stdout))
            return CheckerResult.Broken(string.IsNullOrWhiteSpace(stderr) ? "Bộ kiểm tra không trả kết quả." : stderr.Trim());
        try
        {
            using var doc = JsonDocument.Parse(stdout);
            var root = doc.RootElement;
            if (dumpLines && root.ValueKind == JsonValueKind.Array)
            {
                var rows = root.EnumerateArray().Select(row => new SpokenRow(
                    row.TryGetProperty("text", out var text) ? text.GetString() ?? "" : "",
                    row.TryGetProperty("short", out var flag) && flag.ValueKind == JsonValueKind.True)).ToList();
                return new CheckerResult(true, [], rows, rows.Count, 0);
            }
            var errors = root.TryGetProperty("errors", out var list)
                ? list.EnumerateArray().Select(item => item.GetString() ?? "").Where(item => item.Length > 0).ToList()
                : new List<string>();
            var scenes = root.TryGetProperty("scenes", out var sceneCount) && sceneCount.TryGetInt32(out var n) ? n : 0;
            var lineCount = root.TryGetProperty("lines", out var lineCountEl) && lineCountEl.TryGetInt32(out var m) ? m : 0;
            return new CheckerResult(root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True, errors, [], lineCount, scenes);
        }
        catch (JsonException)
        {
            return CheckerResult.Broken("Bộ kiểm tra trả kết quả không đọc được.");
        }
    }

    static string? SliceJson(string raw)
    {
        var text = raw.Trim();
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        var slice = text[start..(end + 1)];
        try
        {
            using var doc = JsonDocument.Parse(slice);
            return doc.RootElement.ValueKind == JsonValueKind.Object ? slice : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static string Pretty(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    static string? FindRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "KitPlatform.slnx"))) return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    sealed record SpokenRow(string Text, bool Short);

    sealed record CheckerResult(bool Ok, List<string> Errors, List<SpokenRow> Lines, int LineCount, int Scenes)
    {
        public static CheckerResult Broken(string message) => new(false, [message], [], 0, 0);
    }
}

public sealed record NgvHinhDraft(
    bool Ok,
    string? Json,
    int Tokens,
    int Repairs,
    int Scenes,
    int Lines,
    IReadOnlyList<string> Errors)
{
    public static NgvHinhDraft Fail(string message) => new(false, null, 0, 0, 0, 0, [message]);
}
