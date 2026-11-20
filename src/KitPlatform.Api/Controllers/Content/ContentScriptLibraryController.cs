using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Infrastructure;

namespace KitPlatform.Api.Controllers.Content;

/// <summary>
/// Kịch bản đã viết sẵn của một kênh, lưu thành file trong repo:
/// docs/marketing/&lt;kênh&gt;/kich-ban/kenh.json ({"brandCode":"trading"}) và các file *.txt.
/// Đầu file có thể có dòng "# Mã: A2", "# Tiêu đề: …", "# Trạng thái: Đã duyệt", "# Cập nhật: 2026-10-31", "# Docs: url".
/// File &lt;id&gt;.dang.json cạnh kịch bản (nếu có) chứa tiêu đề, mô tả, caption để bước 5 Video Studio điền sẵn.
/// Chỉ đọc. Không gọi AI. Máy không có thư mục repo (VPS) trả danh sách rỗng.
/// </summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[RequirePlatformModule(PlatformModuleCodes.KitContent)]
[Route("api/content/script-library")]
public sealed class ContentScriptLibraryController : ControllerBase
{
    readonly INgvHinhPlanService _plans;

    public ContentScriptLibraryController(INgvHinhPlanService plans) => _plans = plans;

    static readonly Regex SafeId = new(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,79}$", RegexOptions.Compiled);
    static readonly Regex MetaLine = new(@"^#\s*(mã|tiêu đề|trạng thái|cập nhật|docs)\s*:\s*(.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex SceneLine = new(@"^(?:#{1,6}\s*)?(?:\*\*)?\s*(?:cảnh|canh|sc)\s*\d+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    const long MaxBytes = 200_000;

    [HttpGet]
    public ActionResult<ContentScriptLibraryListDto> List([FromQuery] string brandCode)
    {
        var folder = FolderFor(brandCode, out var note);
        if (folder == null) return Ok(new ContentScriptLibraryListDto(new List<ContentScriptLibraryItemDto>(), note));
        var items = Directory.GetFiles(folder, "*.txt")
            .Select(path => Read(path, includeBody: false))
            .Where(item => item != null)
            .Select(item => item!)
            .OrderBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Ok(new ContentScriptLibraryListDto(items, items.Count == 0 ? "Thư mục kịch bản của kênh chưa có file .txt." : null));
    }

    [HttpGet("{id}")]
    public ActionResult<ContentScriptLibraryItemDto> Get(string id, [FromQuery] string brandCode)
    {
        if (!SafeId.IsMatch(id ?? "")) return BadRequest(new { message = "Tên kịch bản không hợp lệ." });
        var folder = FolderFor(brandCode, out var note);
        if (folder == null) return NotFound(new { message = note });
        var path = Path.GetFullPath(Path.Combine(folder, id + ".txt"));
        if (!path.StartsWith(Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(path))
            return NotFound(new { message = "Không thấy kịch bản này." });
        var item = Read(path, includeBody: true);
        return item == null ? BadRequest(new { message = "Không đọc được file kịch bản." }) : Ok(item);
    }

    /// <summary>Kế hoạch hình cạnh kịch bản (<c>id.anh.json</c>). Không có file thì 404, prompt cũ vẫn dùng được.</summary>
    [HttpGet("{id}/shot-plan")]
    public ActionResult<ScriptShotPlanDto> ShotPlan(string id, [FromQuery] string brandCode)
    {
        if (!TryReadShotPlan(brandCode, id, out var json, out var error))
            return error == null
                ? NotFound(new { message = "Kịch bản này không có kế hoạch hình." })
                : BadRequest(new { message = error });
        try
        {
            var shots = NgvShotPlan.Parse(json!).Select(shot => new ScriptShotSpanDto(shot.From, shot.To, shot.First)).ToList();
            return Ok(new ScriptShotPlanDto(shots));
        }
        catch (JsonException)
        {
            return BadRequest(new { message = "Không đọc được kế hoạch hình." });
        }
    }

    /// <summary>Đã có file hình cạnh kịch bản hay chưa. Không trả nội dung file.</summary>
    [HttpGet("{id}/hinh")]
    public ActionResult<ScriptHinhStatusDto> Hinh(string id, [FromQuery] string brandCode)
    {
        if (!TryScriptPath(brandCode, id, ".hinh.json", out var path, out var error))
            return error == null ? NotFound(new { message = "Không thấy kịch bản này." }) : BadRequest(new { message = error });
        return Ok(new ScriptHinhStatusDto(System.IO.File.Exists(path)));
    }

    public sealed class HinhPlanBody
    {
        public bool Overwrite { get; set; }
    }

    /// <summary>Mô hình chữ viết .hinh.json, kiểm tra tối đa hai vòng sửa, đạt thì lưu.</summary>
    [HttpPost("{id}/hinh-plan")]
    public async Task<ActionResult<ScriptHinhPlanDto>> HinhPlan(string id, [FromQuery] string brandCode, [FromBody] HinhPlanBody? body, CancellationToken cancellationToken)
    {
        if (!TryScriptPath(brandCode, id, ".txt", out var scriptPath, out var error))
            return error == null ? NotFound(new { message = "Không thấy kịch bản này." }) : BadRequest(new { message = error });
        var dest = Path.ChangeExtension(scriptPath, ".hinh.json");
        var exists = System.IO.File.Exists(dest);
        if (exists && body?.Overwrite != true)
            return Conflict(new { message = "Đã có kế hoạch hình. Bấm lên lại nếu muốn ghi đè." });
        var channel = string.Equals(brandCode?.Trim(), "trading", StringComparison.OrdinalIgnoreCase) ? "ngv" : "";
        var draft = await _plans.ComposeAsync(scriptPath, channel, Examples(Path.GetDirectoryName(scriptPath)!), cancellationToken);
        if (!draft.Ok || string.IsNullOrWhiteSpace(draft.Json))
            return BadRequest(new { message = "Kế hoạch hình chưa đạt, chưa lưu.", errors = draft.Errors, tokens = draft.Tokens, rounds = draft.Repairs });
        if (exists)
        {
            var backup = Path.Combine(Path.GetDirectoryName(dest)!, "_ban-cu");
            Directory.CreateDirectory(backup);
            System.IO.File.Copy(dest, Path.Combine(backup, id + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".hinh.json"), overwrite: false);
        }
        await System.IO.File.WriteAllTextAsync(dest, draft.Json, new System.Text.UTF8Encoding(false), cancellationToken);
        return Ok(new ScriptHinhPlanDto(draft.Scenes, draft.Lines, draft.Tokens, draft.Repairs));
    }

    static string Examples(string folder)
    {
        var preferred = new[]
        {
            "DA1-vi-sao-phan-lon-nguoi-giao-dich-ca-nhan-thua-lo.hinh.json",
            "DA3-luong-20-trieu-mang-bao-nhieu-di-dau-tu.hinh.json",
        };
        var parts = new List<string>();
        foreach (var name in preferred)
        {
            var path = Path.Combine(folder, name);
            if (!System.IO.File.Exists(path)) continue;
            try
            {
                using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(path));
                if (!doc.RootElement.TryGetProperty("scenes", out var scenes) || scenes.ValueKind != JsonValueKind.Array) continue;
                var sample = scenes.EnumerateArray().Take(2);
                parts.Add(name + "\n" + JsonSerializer.Serialize(sample));
            }
            catch (JsonException)
            {
                /* mẫu hỏng thì bỏ, không chặn việc viết */
            }
        }
        return parts.Count == 0 ? "(Kênh chưa có file mẫu.)" : string.Join("\n\n", parts);
    }

    static bool TryScriptPath(string? brandCode, string? scriptId, string extension, out string path, out string? error)
    {
        path = "";
        error = null;
        var id = (scriptId ?? "").Trim();
        if (!SafeId.IsMatch(id))
        {
            error = "Tên kịch bản không hợp lệ.";
            return false;
        }
        var folder = FolderFor(brandCode, out var note);
        if (folder == null)
        {
            error = string.IsNullOrWhiteSpace(note) ? "Không thấy thư mục kịch bản." : note;
            return false;
        }
        path = Path.GetFullPath(Path.Combine(folder, id + extension));
        if (!path.StartsWith(Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase))
        {
            error = "Tên kịch bản không hợp lệ.";
            return false;
        }
        if (extension == ".txt" && !System.IO.File.Exists(path))
            return false;
        return true;
    }

    internal static bool TryReadShotPlan(string? brandCode, string? scriptId, out string? json, out string? error)
    {
        json = null;
        error = null;
        var id = (scriptId ?? "").Trim();
        if (id.Length == 0) return false;
        if (!SafeId.IsMatch(id))
        {
            error = "Tên kịch bản không hợp lệ.";
            return false;
        }
        var folder = FolderFor(brandCode, out var note);
        if (folder == null)
        {
            error = string.IsNullOrWhiteSpace(note) ? "Không thấy thư mục kịch bản." : note;
            return false;
        }
        var path = Path.GetFullPath(Path.Combine(folder, id + ".anh.json"));
        if (!path.StartsWith(Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(path))
            return false;
        if (new FileInfo(path).Length > 1_000_000)
        {
            error = "Kế hoạch hình quá lớn.";
            return false;
        }
        try
        {
            json = System.IO.File.ReadAllText(path);
            return true;
        }
        catch (IOException)
        {
            error = "Không đọc được kế hoạch hình.";
            return false;
        }
    }

    static ContentScriptLibraryItemDto? Read(string path, bool includeBody)
    {
        var info = new FileInfo(path);
        if (info.Length > MaxBytes) return null;
        string text;
        try { text = System.IO.File.ReadAllText(path); }
        catch (IOException) { return null; }
        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var body = new List<string>();
        var header = true;
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            var match = header ? MetaLine.Match(line.Trim()) : Match.Empty;
            if (match.Success)
            {
                meta[match.Groups[1].Value.ToLowerInvariant()] = match.Groups[2].Value.Trim();
                continue;
            }
            if (header && line.Trim().Length == 0 && body.Count == 0) continue;
            header = false;
            body.Add(line);
        }
        var bodyText = string.Join("\n", body).Trim();
        var lines = bodyText.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
        var scenes = lines.Count(line => SceneLine.IsMatch(line));
        var status = meta.GetValueOrDefault("trạng thái") ?? "";
        var id = Path.GetFileNameWithoutExtension(path);
        return new ContentScriptLibraryItemDto(
            id,
            meta.GetValueOrDefault("mã") ?? id,
            meta.GetValueOrDefault("tiêu đề") ?? id,
            string.IsNullOrWhiteSpace(status) ? "Chưa ghi" : status,
            status.Contains("đã duyệt", StringComparison.OrdinalIgnoreCase),
            meta.GetValueOrDefault("cập nhật"),
            meta.GetValueOrDefault("docs"),
            scenes,
            lines.Count - scenes,
            info.LastWriteTimeUtc,
            includeBody ? bodyText : null,
            includeBody ? ReadPublish(path) : null);
    }

    /// <summary>Nội dung đăng kèm (&lt;id&gt;.dang.json). Lỗi cú pháp thì bỏ qua, không chặn kịch bản.</summary>
    static JsonElement? ReadPublish(string scriptPath)
    {
        var sidecar = Path.Combine(Path.GetDirectoryName(scriptPath)!, Path.GetFileNameWithoutExtension(scriptPath) + ".dang.json");
        if (!System.IO.File.Exists(sidecar) || new FileInfo(sidecar).Length > MaxBytes) return null;
        try
        {
            using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(sidecar));
            return doc.RootElement.ValueKind == JsonValueKind.Object ? doc.RootElement.Clone() : null;
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Thư mục kich-ban có kenh.json trùng mã thương hiệu.</summary>
    static string? FolderFor(string? brandCode, out string note)
    {
        note = "";
        var code = (brandCode ?? "").Trim();
        if (code.Length == 0)
        {
            note = "Thiếu mã thương hiệu.";
            return null;
        }
        var root = FindMarketingRoot();
        if (root == null)
        {
            note = "Máy chủ này không có thư mục docs/marketing (chỉ dùng được trên máy có repo).";
            return null;
        }
        foreach (var channel in Directory.GetDirectories(root))
        {
            var folder = Path.Combine(channel, "kich-ban");
            var config = Path.Combine(folder, "kenh.json");
            if (!System.IO.File.Exists(config)) continue;
            try
            {
                var parsed = JsonSerializer.Deserialize<ChannelConfig>(System.IO.File.ReadAllText(config), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed?.BrandCode != null && parsed.BrandCode.Trim().Equals(code, StringComparison.OrdinalIgnoreCase))
                    return folder;
            }
            catch (JsonException)
            {
                /* kenh.json hỏng: bỏ qua kênh này */
            }
        }
        note = $"Chưa có thư mục kịch bản cho thương hiệu \"{code}\" (docs/marketing/<kênh>/kich-ban/kenh.json).";
        return null;
    }

    static string? FindMarketingRoot()
    {
        var configured = NovixaStockMotionRunner.ConfiguredVideoRepoRoot();
        if (configured != null)
        {
            var configuredMarketing = Path.Combine(configured, "docs", "marketing");
            return Directory.Exists(configuredMarketing) ? configuredMarketing : null;
        }
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var marketing = Path.Combine(dir.FullName, "docs", "marketing");
            if (Directory.Exists(marketing) && System.IO.File.Exists(Path.Combine(dir.FullName, "KitPlatform.slnx"))) return marketing;
            dir = dir.Parent;
        }
        return null;
    }

    sealed class ChannelConfig
    {
        public string? BrandCode { get; set; }
    }
}

public sealed record ContentScriptLibraryItemDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("approved")] bool Approved,
    [property: JsonPropertyName("updated")] string? Updated,
    [property: JsonPropertyName("docsUrl")] string? DocsUrl,
    [property: JsonPropertyName("scenes")] int Scenes,
    [property: JsonPropertyName("lines")] int Lines,
    [property: JsonPropertyName("modifiedUtc")] DateTime ModifiedUtc,
    [property: JsonPropertyName("body")] string? Body,
    [property: JsonPropertyName("publish")] JsonElement? Publish = null);

public sealed record ContentScriptLibraryListDto(
    [property: JsonPropertyName("items")] List<ContentScriptLibraryItemDto> Items,
    [property: JsonPropertyName("message")] string? Message);

public sealed record ScriptShotSpanDto(
    [property: JsonPropertyName("from")] int From,
    [property: JsonPropertyName("to")] int To,
    [property: JsonPropertyName("first")] string First);

public sealed record ScriptShotPlanDto(
    [property: JsonPropertyName("shots")] List<ScriptShotSpanDto> Shots);

public sealed record ScriptHinhStatusDto([property: JsonPropertyName("exists")] bool Exists);

public sealed record ScriptHinhPlanDto(
    [property: JsonPropertyName("scenes")] int Scenes,
    [property: JsonPropertyName("lines")] int Lines,
    [property: JsonPropertyName("tokens")] int Tokens,
    [property: JsonPropertyName("rounds")] int Rounds);
