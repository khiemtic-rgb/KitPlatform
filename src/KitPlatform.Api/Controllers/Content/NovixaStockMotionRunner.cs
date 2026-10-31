using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KitPlatform.Api.Controllers.Content;

public sealed class NovixaMotionRequest
{
    public List<string> Lines { get; set; } = new();
    public List<NovixaMotionScreen> Screens { get; set; } = new();
    public List<NovixaStillFile> Stills { get; set; } = new();
    public List<NovixaStillFile> Keep { get; set; } = new();
    public bool AllowVoice { get; set; }
    public string? VoiceId { get; set; }
    public NovixaVoiceSettings? VoiceSettings { get; set; }
    public string? Phase { get; set; }
    public int? RedrawIndex { get; set; }
    public int Variant { get; set; }
    public string? Orientation { get; set; }
    public bool Force { get; set; }
    public string? ReuseFolder { get; set; }
    public List<NovixaCut> Cuts { get; set; } = new();
    public NovixaBrandProfile? Brand { get; set; }
}

public sealed class NovixaVoiceSettings
{
    public double? Stability { get; set; }
    public double? SimilarityBoost { get; set; }
    public double? Style { get; set; }
    public double? Speed { get; set; }
}

public sealed class NovixaCut
{
    public int From { get; set; }
    public int To { get; set; }
    public string? Title { get; set; }
}

public sealed record NovixaShortFile(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("from")] int From,
    [property: JsonPropertyName("to")] int To,
    [property: JsonPropertyName("seconds")] double Seconds,
    [property: JsonPropertyName("path")] string? Path);

public sealed class NovixaBrandProfile
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Colors { get; set; }
    public string? Style { get; set; }
    public string? ImageNotes { get; set; }
    public List<string> Products { get; set; } = new();
    public List<string> Avoid { get; set; } = new();
}

public sealed class NovixaStillFile
{
    public int Index { get; set; }
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public string Path { get; set; } = "";
    public string? BrandId { get; set; }
    public bool? BrandStale { get; set; }
    public string? Signature { get; set; }
    public string? Layout { get; set; }
    public string? Kind { get; set; }
    public string? Subject { get; set; }
    public int? Arrangement { get; set; }
    public int? Distance { get; set; }
    public int? Light { get; set; }
    public int? Shift { get; set; }
}

public sealed class NovixaMotionScreen
{
    public string Path { get; set; } = "";
    public string Label { get; set; } = "";
}

public sealed record NovixaMotionResult(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("folder")] string? Folder,
    [property: JsonPropertyName("videoPath")] string? VideoPath,
    [property: JsonPropertyName("stills")] List<NovixaStillFile>? Stills = null,
    [property: JsonPropertyName("phase")] string? Phase = null,
    [property: JsonPropertyName("taskId")] string? TaskId = null,
    [property: JsonPropertyName("shorts")] List<NovixaShortFile>? Shorts = null);

public static class NovixaStockMotionRunner
{
    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<NovixaMotionResult> RunAsync(Guid buildId, NovixaMotionRequest request, CancellationToken cancellationToken)
    {
        var repo = FindRepo();
        if (repo == null)
            return new NovixaMotionResult("failed", "Không thấy build_stock_motion.py.", null, null);
        var script = Path.Combine(repo, "proof", "kit-video-production-v1", "outputs", "novixa-pain-stock", "build_stock_motion.py");
        var jobDir = Path.Combine(repo, "proof", "kit-video-production-v1", "outputs", "novixa-studio", buildId.ToString("N"));
        Directory.CreateDirectory(jobDir);
        var jobFile = Path.Combine(jobDir, "job.json");
        var phase = string.IsNullOrWhiteSpace(request.Phase) ? "video" : request.Phase.Trim();
        var existing = phase == "video" && !request.Force ? ReadReady(buildId, request.Lines, request.VoiceId, request.Stills, VoiceKey(request.VoiceSettings)) : null;
        if (existing != null)
            return existing with { Message = "Video đã có. Không tạo lượt giọng mới." };
        var job = new
        {
            buildId = buildId.ToString("N"),
            phase,
            lines = request.Lines ?? new List<string>(),
            screens = (request.Screens ?? new List<NovixaMotionScreen>()).Select(screen => new { path = screen.Path, label = screen.Label }),
            stills = request.Stills ?? new List<NovixaStillFile>(),
            keep = request.Keep ?? new List<NovixaStillFile>(),
            redrawIndex = request.RedrawIndex,
            variant = request.Variant,
            orientation = request.Orientation == "wide" ? "wide" : "tall",
            force = request.Force,
            reuseFolder = request.ReuseFolder,
            cuts = (request.Cuts ?? new List<NovixaCut>()).Select(cut => new { from = cut.From, to = cut.To, title = cut.Title }),
            allowVoice = request.AllowVoice,
            voiceId = request.VoiceId,
            voiceSettings = request.VoiceSettings == null ? null : new
            {
                stability = request.VoiceSettings.Stability,
                similarityBoost = request.VoiceSettings.SimilarityBoost,
                style = request.VoiceSettings.Style,
                speed = request.VoiceSettings.Speed,
            },
            brand = request.Brand == null ? null : new
            {
                id = request.Brand.Id,
                name = request.Brand.Name,
                colors = request.Brand.Colors,
                style = request.Brand.Style,
                imageNotes = request.Brand.ImageNotes,
                products = request.Brand.Products,
                avoid = request.Brand.Avoid,
            },
        };
        await File.WriteAllTextAsync(jobFile, JsonSerializer.Serialize(job, Json), cancellationToken);
        var start = new ProcessStartInfo
        {
            FileName = "python",
            Arguments = $"\"{script}\" --job \"{jobFile}\"",
            WorkingDirectory = repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        Process? process;
        try
        {
            process = Process.Start(start);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new NovixaMotionResult("failed", "Không chạy được Python.", null, null);
        }
        if (process == null)
            return new NovixaMotionResult("failed", "Không chạy được Python.", null, null);
        using var running = process;
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await Task.WhenAll(stdoutTask, stderrTask);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        await process.WaitForExitAsync(cancellationToken);
        try
        {
            var parsed = JsonSerializer.Deserialize<NovixaMotionResult>(stdout, Json);
            if (parsed != null && !string.IsNullOrWhiteSpace(parsed.Status))
                return parsed;
        }
        catch (JsonException)
        {
            /* stdout was not the job result */
        }
        var detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
        return new NovixaMotionResult("failed", string.IsNullOrWhiteSpace(detail) ? "build_stock_motion không trả kết quả." : detail.Trim(), null, null);
    }

    public static NovixaMotionResult Read(Guid buildId)
    {
        var ready = ReadReady(buildId, null);
        var stills = ReadStills(buildId);
        if (ready != null)
            return stills?.Stills is { Count: > 0 } ? ready with { Stills = stills.Stills } : ready;
        var running = ReadTask(buildId);
        if (running != null) return running;
        if (stills != null) return stills;
        return new NovixaMotionResult("job_missing", "Chưa có tác vụ dựng.", null, null);
    }

    public static string? StillFile(Guid buildId, int index, string? folder, string? name = null)
    {
        if (index < 0 || index > 200 || string.IsNullOrWhiteSpace(folder)) return null;
        if (!System.Text.RegularExpressions.Regex.IsMatch(folder, @"^\d{8}T\d{6,12}Z-stills$")) return null;
        var fileName = string.IsNullOrWhiteSpace(name) ? $"u{index:00}.png" : Path.GetFileName(name);
        if (!System.Text.RegularExpressions.Regex.IsMatch(fileName, $@"^u{index:00}(-g\d{{6,12}})?\.(png|jpg)$")) return null;
        var root = BuildDir(buildId);
        if (root == null) return null;
        var file = Path.GetFullPath(Path.Combine(root, folder, fileName));
        if (!file.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase) || !File.Exists(file)) return null;
        return file;
    }

    public static NovixaStillFile? ReplaceStill(Guid buildId, int index, byte[] bytes, string extension)
    {
        if (index < 0 || index > 200 || bytes.Length < 32 || extension is not ("png" or "jpg")) return null;
        var current = ReadStills(buildId);
        if (current?.Folder == null || current.Stills == null || current.Stills.All(item => item.Index != index)) return null;
        var name = $"u{index:00}-g{DateTime.UtcNow:HHmmssfff}.{extension}";
        var full = Path.GetFullPath(Path.Combine(current.Folder, name));
        if (!full.StartsWith(Path.GetFullPath(current.Folder), StringComparison.OrdinalIgnoreCase)) return null;
        var jsonPath = Path.Combine(current.Folder, "stills.json");
        var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(jsonPath));
        var rows = node?["stills"]?.AsArray();
        if (rows == null) return null;
        var updated = false;
        foreach (var item in rows)
        {
            if (item?["index"]?.GetValue<int>() != index) continue;
            item["path"] = full;
            item["source"] = "gemini";
            updated = true;
        }
        if (!updated) return null;
        File.WriteAllBytes(full, bytes);
        File.WriteAllText(jsonPath, node!.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        var row = current.Stills.First(item => item.Index == index);
        row.Path = full;
        return row;
    }

    public static string? VideoFile(Guid buildId, string? folder)
    {
        var root = BuildDir(buildId);
        if (root == null || !Directory.Exists(root)) return null;
        if (!string.IsNullOrWhiteSpace(folder))
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(folder, @"^\d{8}T\d{6,12}Z-motion$")) return null;
            var file = Path.GetFullPath(Path.Combine(root, folder, "video.mp4"));
            return file.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase) && File.Exists(file) ? file : null;
        }
        var ready = ReadReady(buildId, null);
        return ready?.VideoPath != null && File.Exists(ready.VideoPath) ? ready.VideoPath : null;
    }

    public static NovixaMotionResult? ReadShorts(Guid buildId)
    {
        var root = BuildDir(buildId);
        if (root == null || !Directory.Exists(root)) return null;
        foreach (var dir in Directory.GetDirectories(root).Where(item => item.EndsWith("-shorts", StringComparison.OrdinalIgnoreCase)).OrderByDescending(item => item, StringComparer.Ordinal))
        {
            var file = Path.Combine(dir, "shorts.json");
            if (!File.Exists(file)) continue;
            try
            {
                var parsed = JsonSerializer.Deserialize<NovixaMotionResult>(File.ReadAllText(file), Json);
                var alive = parsed?.Shorts?.Where(item => !string.IsNullOrWhiteSpace(item.Path) && File.Exists(item.Path)).ToList();
                if (parsed?.Status == "ready" && alive is { Count: > 0 })
                    return parsed with { Folder = Path.GetFileName(dir), Shorts = alive };
            }
            catch (JsonException)
            {
                /* shorts.json không đọc được: thử lần cắt trước đó */
            }
        }
        return null;
    }

    public static string? ShortFile(Guid buildId, string? folder, int number)
    {
        var root = BuildDir(buildId);
        if (root == null || !Directory.Exists(root) || number < 1 || number > 99) return null;
        if (string.IsNullOrWhiteSpace(folder) || !System.Text.RegularExpressions.Regex.IsMatch(folder, @"^\d{8}T\d{6,12}Z-shorts$")) return null;
        var file = Path.GetFullPath(Path.Combine(root, folder, $"short_{number:00}.mp4"));
        return file.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase) && File.Exists(file) ? file : null;
    }

    public static string? PreviewFile(Guid buildId, string? folder)
    {
        var root = BuildDir(buildId);
        if (root == null || !Directory.Exists(root)) return null;
        if (!string.IsNullOrWhiteSpace(folder))
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(folder, @"^\d{8}T\d{6,12}Z-preview$")) return null;
            var file = Path.GetFullPath(Path.Combine(root, folder, "preview.mp3"));
            return file.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase) && File.Exists(file) ? file : null;
        }
        return Directory.GetDirectories(root).Where(dir => dir.EndsWith("-preview", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(dir => dir)
            .Select(dir => Path.Combine(dir, "preview.mp3"))
            .FirstOrDefault(File.Exists);
    }

    static NovixaMotionResult? ReadReady(Guid buildId, List<string>? lines, string? voiceId = null, List<NovixaStillFile>? stills = null, string? voiceKey = null)
    {
        var root = BuildDir(buildId);
        if (root == null || !Directory.Exists(root)) return null;
        NovixaMotionResult? found = null;
        foreach (var episode in Directory.GetDirectories(root).Where(dir => dir.EndsWith("-motion", StringComparison.OrdinalIgnoreCase)).OrderBy(dir => dir, StringComparer.Ordinal))
        {
            var file = Path.Combine(episode, "episode.json");
            if (!File.Exists(file)) continue;
            try
            {
                var text = File.ReadAllText(file);
                var parsed = JsonSerializer.Deserialize<NovixaMotionResult>(text, Json);
                if (parsed?.Status != "ready" || string.IsNullOrWhiteSpace(parsed.VideoPath) || !File.Exists(parsed.VideoPath)) continue;
                if (lines is { Count: > 0 })
                {
                    using var raw = JsonDocument.Parse(text);
                    if (!raw.RootElement.TryGetProperty("lines", out var stored) || stored.GetArrayLength() != lines.Count) continue;
                    var same = stored.EnumerateArray().Select((item, index) => item.GetString() == lines[index]).All(match => match);
                    if (!same || !SameTake(raw.RootElement, voiceId, stills)) continue;
                    if (voiceKey != null && (!raw.RootElement.TryGetProperty("voiceKey", out var key) || key.GetString() != voiceKey)) continue;
                }
                found = parsed;
            }
            catch (JsonException)
            {
                /* skip a partial file */
            }
        }
        return found;
    }

    /// <summary>Same key as build_stock_motion.py voice_key(): stability|similarity|style|speed with the script defaults.</summary>
    static string? VoiceKey(NovixaVoiceSettings? settings)
    {
        if (settings == null) return null;
        static string F(double value) => value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        var stability = Math.Clamp(settings.Stability ?? 0.5, 0, 1);
        var similarity = Math.Clamp(settings.SimilarityBoost ?? 0.85, 0, 1);
        var style = Math.Clamp(settings.Style ?? 0, 0, 1);
        var speed = Math.Clamp(settings.Speed ?? 1.0, 0.7, 1.2);
        return $"{F(stability)}|{F(similarity)}|{F(style)}|{F(speed)}";
    }

    static bool SameTake(JsonElement root, string? voiceId, List<NovixaStillFile>? stills)
    {
        var wantedVoice = (voiceId ?? "").Trim();
        if (wantedVoice.Length > 0)
        {
            var storedVoice = root.TryGetProperty("voiceId", out var voice) ? voice.GetString() ?? "" : "";
            if (!string.Equals(storedVoice, wantedVoice, StringComparison.Ordinal)) return false;
        }
        if (stills is not { Count: > 0 }) return true;
        if (!root.TryGetProperty("stills", out var stored) || stored.ValueKind != JsonValueKind.Array || stored.GetArrayLength() != stills.Count)
            return false;
        var index = 0;
        foreach (var item in stored.EnumerateArray())
        {
            var path = item.TryGetProperty("path", out var pathValue) ? pathValue.GetString() ?? "" : "";
            if (!string.Equals(path, stills[index].Path ?? "", StringComparison.OrdinalIgnoreCase)) return false;
            index++;
        }
        return true;
    }

    static NovixaMotionResult? ReadTask(Guid buildId)
    {
        var root = BuildDir(buildId);
        if (root == null || !Directory.Exists(root)) return null;
        foreach (var task in Directory.GetFiles(root, "task.json", SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc))
        {
            var folder = Path.GetDirectoryName(task);
            if (folder != null && File.Exists(Path.Combine(folder, "episode.json"))) continue;
            if (File.GetLastWriteTimeUtc(task) < DateTime.UtcNow.AddMinutes(-20)) continue;
            try
            {
                var parsed = JsonSerializer.Deserialize<NovixaMotionResult>(File.ReadAllText(task), Json);
                if (parsed?.Status == "running")
                    return parsed with { Folder = folder, Message = string.IsNullOrWhiteSpace(parsed.Message) ? "Đang dựng. Không tạo lượt mới." : parsed.Message };
            }
            catch (JsonException)
            {
                /* skip */
            }
        }
        return null;
    }

    static NovixaMotionResult? ReadStills(Guid buildId)
    {
        var root = BuildDir(buildId);
        if (root == null || !Directory.Exists(root)) return null;
        NovixaMotionResult? found = null;
        foreach (var dir in Directory.GetDirectories(root).Where(item => item.EndsWith("-stills", StringComparison.OrdinalIgnoreCase)).OrderBy(item => item, StringComparer.Ordinal))
        {
            var file = Path.Combine(dir, "stills.json");
            if (!File.Exists(file)) continue;
            try
            {
                var parsed = JsonSerializer.Deserialize<NovixaMotionResult>(File.ReadAllText(file), Json);
                if (parsed?.Stills is { Count: > 0 }) found = parsed with { Folder = dir };
            }
            catch (JsonException)
            {
                /* skip */
            }
        }
        return found;
    }

    static string? BuildDir(Guid buildId)
    {
        var repo = FindRepo();
        if (repo == null) return null;
        return Path.Combine(repo, "proof", "kit-video-production-v1", "outputs", "novixa-studio", buildId.ToString("N"));
    }

    static string? FindRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var script = Path.Combine(dir.FullName, "proof", "kit-video-production-v1", "outputs", "novixa-pain-stock", "build_stock_motion.py");
            if (File.Exists(script)) return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
