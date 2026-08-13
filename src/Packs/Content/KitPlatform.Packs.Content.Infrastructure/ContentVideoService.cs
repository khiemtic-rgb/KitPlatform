using System.Text.Json;
using System.Text.RegularExpressions;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentVideoService : IContentVideoService
{
    private static readonly string[] PreferredScriptKinds =
        ["tiktok_script", "social_caption", "fb_short", "fb_page", "web_long"];

    private readonly ContentRepository _repo;
    private readonly ContentCreatomateClient _creatomate;

    public ContentVideoService(ContentRepository repo, ContentCreatomateClient creatomate)
    {
        _repo = repo;
        _creatomate = creatomate;
    }

    public async Task<IReadOnlyList<ContentVideoTemplateDto>> ListTemplatesAsync(
        bool? activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListVideoTemplatesAsync(activeOnly, cancellationToken);
        return rows.Select(MapTemplate).ToList();
    }

    public async Task<IReadOnlyList<ContentVideoJobDto>> ListJobsAsync(
        Guid? brandId,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListVideoJobsAsync(brandId, status, cancellationToken);
        return rows.Select(MapJob).ToList();
    }

    public async Task<ContentVideoJobDto?> GetJobAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetVideoJobAsync(id, cancellationToken);
        return row is null ? null : MapJob(row);
    }

    public async Task<ContentVideoJobDto> CreateFromPackageAsync(
        CreateVideoJobFromPackageRequest request,
        CancellationToken cancellationToken = default)
    {
        var package = await _repo.GetPackageAsync(request.PackageId, cancellationToken)
                      ?? throw new InvalidOperationException("Package không tồn tại.");

        var template = await ResolveTemplateAsync(request, cancellationToken);
        if (!template.IsActive)
            throw new InvalidOperationException("Template đang tắt.");

        var variants = await _repo.ListVariantsAsync(package.TopicId, cancellationToken);
        var script = PickScript(variants);
        if (string.IsNullOrWhiteSpace(script))
            throw new InvalidOperationException(
                "Package chưa có bản tiktok_script / caption — Generate All trước.");

        var title = package.Title.Trim();
        if (title.Length > 480) title = title[..480];

        var id = await _repo.InsertVideoJobAsync(
            package.BrandId,
            package.Id,
            package.TopicId,
            template.Id,
            title,
            script.Trim(),
            "Draft",
            template.Provider,
            "[]",
            ContentRepository.ToJson(new
            {
                fromPackageStatus = package.Status,
                creatomateConfigured = _creatomate.IsConfigured,
            }),
            cancellationToken);

        return (await GetJobAsync(id, cancellationToken))!;
    }

    public async Task<ContentVideoJobDto?> UpdateScriptAsync(
        Guid id,
        UpdateVideoJobScriptRequest request,
        CancellationToken cancellationToken = default)
    {
        var job = await _repo.GetVideoJobAsync(id, cancellationToken);
        if (job is null) return null;
        if (job.Status is "Rendering" or "Queued")
            throw new InvalidOperationException("Job đang render — không sửa script.");

        var script = (request.ScriptBody ?? "").Trim();
        if (script.Length < 20)
            throw new InvalidOperationException("Script quá ngắn.");

        await _repo.UpdateVideoJobAsync(
            id,
            script,
            job.Status is "Ready" or "Approved" ? "Draft" : job.Status,
            job.ExternalRenderId,
            job.PreviewUrl,
            job.OutputUrl,
            null,
            "[]",
            job.ConfigJson,
            job.RenderedAt,
            cancellationToken);

        return await GetJobAsync(id, cancellationToken);
    }

    public async Task<ContentVideoJobDto?> PrepareStoryboardAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _repo.GetVideoJobAsync(id, cancellationToken);
        if (job is null) return null;

        var template = await _repo.GetVideoTemplateAsync(job.TemplateId, cancellationToken)
                       ?? throw new InvalidOperationException("Template thiếu.");

        var beats = ResolveBeatNames(template.ConfigJson);
        var storyboard = BuildStoryboard(job.ScriptBody, beats, template.DurationSec);
        var storyboardJson = ContentRepository.ToJson(storyboard);

        await _repo.UpdateVideoJobAsync(
            id,
            scriptBody: null,
            status: "Ready",
            externalRenderId: job.ExternalRenderId,
            previewUrl: job.PreviewUrl,
            outputUrl: job.OutputUrl,
            errorMessage: null,
            storyboardJson: storyboardJson,
            configJson: job.ConfigJson,
            renderedAt: DateTimeOffset.UtcNow,
            cancellationToken);

        return await GetJobAsync(id, cancellationToken);
    }

    public async Task<ContentVideoJobDto?> QueueRenderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _repo.GetVideoJobAsync(id, cancellationToken);
        if (job is null) return null;

        var template = await _repo.GetVideoTemplateAsync(job.TemplateId, cancellationToken)
                       ?? throw new InvalidOperationException("Template thiếu.");

        // Local / no Creatomate → storyboard Ready
        if (template.Provider != "creatomate"
            || !_creatomate.IsConfigured
            || string.IsNullOrWhiteSpace(template.ExternalTemplateId))
        {
            var prepared = await PrepareStoryboardAsync(id, cancellationToken);
            if (prepared is null) return null;
            if (template.Provider == "creatomate" && !_creatomate.IsConfigured)
            {
                await _repo.UpdateVideoJobAsync(
                    id,
                    null,
                    prepared.Status,
                    prepared.ExternalRenderId,
                    prepared.PreviewUrl,
                    prepared.OutputUrl,
                    "Chưa có CreatomateApiKey — đã chuẩn bị storyboard local để xuất CapCut.",
                    prepared.StoryboardJson,
                    prepared.ConfigJson,
                    prepared.RenderedAt,
                    cancellationToken);
                return await GetJobAsync(id, cancellationToken);
            }

            return prepared;
        }

        var beats = ResolveBeatNames(template.ConfigJson);
        var storyboard = BuildStoryboard(job.ScriptBody, beats, template.DurationSec);
        var storyboardJson = ContentRepository.ToJson(storyboard);

        await _repo.UpdateVideoJobAsync(
            id,
            null,
            "Queued",
            job.ExternalRenderId,
            job.PreviewUrl,
            job.OutputUrl,
            null,
            storyboardJson,
            job.ConfigJson,
            null,
            cancellationToken);

        try
        {
            var mods = BuildCreatomateModifications(template.ConfigJson, job.Title, job.ScriptBody, storyboard);
            var render = await _creatomate.CreateRenderAsync(
                template.ExternalTemplateId!,
                mods,
                cancellationToken);

            var status = MapCreatomateStatus(render.Status);
            await _repo.UpdateVideoJobAsync(
                id,
                null,
                status,
                render.Id,
                render.SnapshotUrl ?? render.Url,
                status == "Ready" ? render.Url : null,
                null,
                storyboardJson,
                job.ConfigJson,
                status == "Ready" ? DateTimeOffset.UtcNow : null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            await _repo.UpdateVideoJobAsync(
                id,
                null,
                "Failed",
                null,
                null,
                null,
                ex.Message,
                storyboardJson,
                job.ConfigJson,
                null,
                cancellationToken);
        }

        return await GetJobAsync(id, cancellationToken);
    }

    public async Task<ContentVideoJobDto?> RefreshRenderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _repo.GetVideoJobAsync(id, cancellationToken);
        if (job is null) return null;
        if (string.IsNullOrWhiteSpace(job.ExternalRenderId) || !_creatomate.IsConfigured)
            return await GetJobAsync(id, cancellationToken);

        try
        {
            var render = await _creatomate.GetRenderAsync(job.ExternalRenderId, cancellationToken);
            var status = MapCreatomateStatus(render.Status);
            await _repo.UpdateVideoJobAsync(
                id,
                null,
                status,
                render.Id,
                render.SnapshotUrl ?? render.Url ?? job.PreviewUrl,
                status == "Ready" ? render.Url : job.OutputUrl,
                status == "Failed" ? "Creatomate render failed" : null,
                job.StoryboardJson,
                job.ConfigJson,
                status == "Ready" ? DateTimeOffset.UtcNow : job.RenderedAt,
                cancellationToken);
        }
        catch (Exception ex)
        {
            await _repo.UpdateVideoJobAsync(
                id,
                null,
                "Failed",
                job.ExternalRenderId,
                job.PreviewUrl,
                job.OutputUrl,
                ex.Message,
                job.StoryboardJson,
                job.ConfigJson,
                job.RenderedAt,
                cancellationToken);
        }

        return await GetJobAsync(id, cancellationToken);
    }

    public async Task<ContentVideoJobDto?> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _repo.GetVideoJobAsync(id, cancellationToken);
        if (job is null) return null;
        if (job.Status is not ("Ready" or "Approved"))
            throw new InvalidOperationException("Chỉ duyệt job đã Ready (storyboard hoặc file render).");

        await _repo.UpdateVideoJobAsync(
            id,
            null,
            "Approved",
            job.ExternalRenderId,
            job.PreviewUrl,
            job.OutputUrl,
            null,
            job.StoryboardJson,
            job.ConfigJson,
            job.RenderedAt ?? DateTimeOffset.UtcNow,
            cancellationToken);

        return await GetJobAsync(id, cancellationToken);
    }

    private async Task<ContentRepository.VideoTemplateRow> ResolveTemplateAsync(
        CreateVideoJobFromPackageRequest request,
        CancellationToken ct)
    {
        if (request.TemplateId is { } tid && tid != Guid.Empty)
        {
            return await _repo.GetVideoTemplateAsync(tid, ct)
                   ?? throw new InvalidOperationException("Template không tồn tại.");
        }

        var code = string.IsNullOrWhiteSpace(request.TemplateCode)
            ? "tiktok_45s_hooks"
            : request.TemplateCode.Trim();
        return await _repo.GetVideoTemplateByCodeAsync(code, ct)
               ?? throw new InvalidOperationException($"Template code «{code}» không tồn tại — chạy mig 288.");
    }

    private static string? PickScript(IReadOnlyList<ContentRepository.VariantRow> variants)
    {
        foreach (var kind in PreferredScriptKinds)
        {
            var hit = variants.FirstOrDefault(v =>
                string.Equals(v.Kind, kind, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(v.BodyMarkdown));
            if (hit is not null) return hit.BodyMarkdown;
        }

        return variants.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v.BodyMarkdown))?.BodyMarkdown;
    }

    private static List<string> ResolveBeatNames(string configJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson);
            if (doc.RootElement.TryGetProperty("beats", out var beats) && beats.ValueKind == JsonValueKind.Array)
            {
                var list = beats.EnumerateArray()
                    .Select(x => x.GetString()?.Trim().ToUpperInvariant())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Cast<string>()
                    .ToList();
                if (list.Count > 0) return list;
            }
        }
        catch
        {
            /* fall through */
        }

        return ["HOOK", "PROBLEM", "INSIGHT", "SOLUTION", "CTA"];
    }

    private static List<object> BuildStoryboard(string script, IReadOnlyList<string> beats, int durationSec)
    {
        var sections = ParseLabeledSections(script, beats);
        var per = Math.Max(3, durationSec / Math.Max(1, beats.Count));
        var cursor = 0;
        var result = new List<object>();
        for (var i = 0; i < beats.Count; i++)
        {
            var beat = beats[i];
            sections.TryGetValue(beat, out var text);
            if (string.IsNullOrWhiteSpace(text))
            {
                // fallback: split whole script into N chunks
                text = ChunkFallback(script, beats.Count, i);
            }

            var start = cursor;
            var end = i == beats.Count - 1 ? durationSec : cursor + per;
            cursor = end;
            result.Add(new
            {
                beat,
                startSec = start,
                endSec = end,
                text = text.Trim(),
                visualHint = beat switch
                {
                    "HOOK" => "Cận mặt / text lớn 0–3s",
                    "PROBLEM" => "B-roll vấn đề",
                    "INSIGHT" => "Overlay insight",
                    "SOLUTION" => "Demo sản phẩm / UI",
                    "OFFER" => "Giá / ưu đãi",
                    "PROOF" => "Social proof",
                    "CTA" => "Logo + URL / QR",
                    _ => "Cut theo nhịp",
                },
            });
        }

        return result;
    }

    private static Dictionary<string, string> ParseLabeledSections(string script, IReadOnlyList<string> beats)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(script)) return map;

        var pattern = string.Join("|", beats.Select(Regex.Escape));
        var rx = new Regex(
            $@"(?:^|\n)\s*(?:#+\s*)?(?<beat>{pattern})\s*[:\-–]?\s*(?<body>.*?)(?=(?:\n\s*(?:#+\s*)?(?:{pattern})\s*[:\-–]?)|\z)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match m in rx.Matches(script))
        {
            var beat = m.Groups["beat"].Value.Trim().ToUpperInvariant();
            var body = m.Groups["body"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(body))
                map[beat] = body;
        }

        return map;
    }

    private static string ChunkFallback(string script, int n, int index)
    {
        var lines = script.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0) return script.Trim();
        var size = Math.Max(1, (int)Math.Ceiling(lines.Length / (double)n));
        var slice = lines.Skip(index * size).Take(size);
        return string.Join("\n", slice);
    }

    private static Dictionary<string, string> BuildCreatomateModifications(
        string templateConfigJson,
        string title,
        string script,
        IReadOnlyList<object> storyboard)
    {
        var mods = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Title"] = title,
            ["Script"] = script,
            ["Script.text"] = script,
        };

        foreach (var item in storyboard)
        {
            // anonymous → serialize
            var json = JsonSerializer.Serialize(item);
            using var doc = JsonDocument.Parse(json);
            var beat = doc.RootElement.GetProperty("beat").GetString() ?? "";
            var text = doc.RootElement.GetProperty("text").GetString() ?? "";
            if (!string.IsNullOrWhiteSpace(beat))
            {
                mods[beat] = text;
                mods[$"{beat}.text"] = text;
            }
        }

        try
        {
            using var cfg = JsonDocument.Parse(
                string.IsNullOrWhiteSpace(templateConfigJson) ? "{}" : templateConfigJson);
            if (cfg.RootElement.TryGetProperty("modifications", out var map)
                && map.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in map.EnumerateObject())
                {
                    var template = prop.Value.GetString() ?? "";
                    mods[prop.Name] = template
                        .Replace("{{script}}", script, StringComparison.OrdinalIgnoreCase)
                        .Replace("{{title}}", title, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        catch
        {
            /* ignore bad config */
        }

        return mods;
    }

    private static string MapCreatomateStatus(string status) =>
        status.Trim().ToLowerInvariant() switch
        {
            "succeeded" or "success" or "completed" => "Ready",
            "failed" or "error" => "Failed",
            "planned" or "waiting" or "queued" => "Queued",
            "rendering" or "transcribing" or "processing" => "Rendering",
            _ => "Rendering",
        };

    private static ContentVideoTemplateDto MapTemplate(ContentRepository.VideoTemplateRow r) =>
        new(
            r.Id, r.Code, r.Name, r.Provider, r.ExternalTemplateId, r.AspectRatio, r.DurationSec,
            r.Description, r.ConfigJson, r.IsActive, r.SortOrder);

    private static ContentVideoJobDto MapJob(ContentRepository.VideoJobRow r) =>
        new(
            r.Id, r.BrandId, r.BrandCode, r.BrandName, r.PackageId, r.TopicId, r.TemplateId,
            r.TemplateCode, r.TemplateName, r.Title, r.ScriptBody, r.Status, r.Provider,
            r.ExternalRenderId, r.PreviewUrl, r.OutputUrl, r.ErrorMessage, r.StoryboardJson,
            r.ConfigJson, r.CreatedAt, r.UpdatedAt, r.RenderedAt);
}
