using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using KitPlatform.Packs.Content;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentVideoService : IContentVideoService
{
    private static readonly string[] PreferredScriptKinds =
        ["tiktok_script", "web_long", "fb_page", "fb_short", "social_caption"];

    private readonly ContentRepository _repo;
    private readonly ContentCreatomateClient _creatomate;
    private readonly ContentElevenLabsClient _elevenLabs;
    private readonly IOptions<ContentOptions> _options;
    private readonly IConfiguration _configuration;

    public ContentVideoService(
        ContentRepository repo,
        ContentCreatomateClient creatomate,
        ContentElevenLabsClient elevenLabs,
        IOptions<ContentOptions> options,
        IConfiguration configuration)
    {
        _repo = repo;
        _creatomate = creatomate;
        _elevenLabs = elevenLabs;
        _options = options;
        _configuration = configuration;
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
                "Package chưa có bản viết (web / Facebook / tiktok_script) — Generate góc trước.");

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
                creatomateConfigured = await _creatomate.IsConfiguredAsync(cancellationToken),
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
        var script = job.ScriptBody;
        if (job.TopicId is Guid topicId)
        {
            var variants = await _repo.ListVariantsAsync(topicId, cancellationToken);
            var richer = PickScript(variants);
            if (!string.IsNullOrWhiteSpace(richer) && richer.Length > (script?.Length ?? 0) + 40)
                script = richer;
        }

        var storyboard = BuildStoryboard(script ?? "", beats, template.DurationSec);
        var storyboardJson = ContentRepository.ToJson(storyboard);

        await _repo.UpdateVideoJobAsync(
            id,
            scriptBody: script,
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

    public async Task<ContentVideoJobDto?> RunMvpPipelineAsync(
        Guid id,
        RunVideoMvpPipelineRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var opts = request ?? new RunVideoMvpPipelineRequest();
        var job = await _repo.GetVideoJobAsync(id, cancellationToken);
        if (job is null) return null;

        var template = await _repo.GetVideoTemplateAsync(job.TemplateId, cancellationToken)
                       ?? throw new InvalidOperationException("Template thiếu.");

        var pipelineNotes = new List<string>();
        var config = ParseConfigObject(job.ConfigJson);

        try
        {
            await SetStatusAsync(id, job, "GeneratingAssets", null, cancellationToken);

            var beats = ResolveBeatNames(template.ConfigJson);
            var storyboard = BuildStoryboardObjects(job.ScriptBody, beats, template.DurationSec);

            if (opts.GenerateImages)
            {
                for (var i = 0; i < storyboard.Count; i++)
                {
                    var scene = storyboard[i];
                    var prompt = string.IsNullOrWhiteSpace(scene.VisualPrompt)
                        ? $"{job.BrandName} · {scene.Beat}: {scene.Text}"
                        : scene.VisualPrompt!;
                    scene.ImageUrl = BuildPollinationsImageUrl(prompt);
                    storyboard[i] = scene;
                }
                pipelineNotes.Add($"images={storyboard.Count} (pollinations public URL)");
            }

            string? voicePublicUrl = null;
            string? voiceLocalPath = null;
            var videoCfg = await _elevenLabs.ResolveAsync(cancellationToken);
            if (opts.GenerateVoice && videoCfg.ElevenLabsConfigured)
            {
                await SetStatusAsync(id, job, "GeneratingVoice", null, cancellationToken);
                var narration = string.Join(". ", storyboard.Select(s => s.Text).Where(t => !string.IsNullOrWhiteSpace(t)));
                if (narration.Length < 20) narration = job.ScriptBody;
                var mp3 = await _elevenLabs.SynthesizeMp3Async(narration, cancellationToken);
                voiceLocalPath = await SaveVideoMediaAsync(id, "voice.mp3", mp3, cancellationToken);
                voicePublicUrl = BuildPublicMediaUrl(id, "voice.mp3", videoCfg.PublicMediaBaseUrl);
                if (voicePublicUrl is null)
                    pipelineNotes.Add("voice=local-only (set Public media URL trên Model AI / Video)");
                else
                    pipelineNotes.Add("voice=elevenlabs");
                config["voiceLocalPath"] = voiceLocalPath;
                if (voicePublicUrl is not null) config["voiceUrl"] = voicePublicUrl;
            }
            else if (opts.GenerateVoice)
            {
                pipelineNotes.Add("voice=skipped (no ElevenLabsApiKey)");
            }

            var storyboardJson = ContentRepository.ToJson(storyboard);
            config["mvpPipeline"] = new
            {
                at = DateTimeOffset.UtcNow,
                notes = pipelineNotes,
                creatomateConfigured = videoCfg.CreatomateConfigured,
                elevenLabsConfigured = videoCfg.ElevenLabsConfigured,
            };
            var configJson = ContentRepository.ToJson(config);

            await _repo.UpdateVideoJobAsync(
                id,
                null,
                "PreparingRender",
                job.ExternalRenderId,
                job.PreviewUrl,
                job.OutputUrl,
                null,
                storyboardJson,
                configJson,
                null,
                cancellationToken);

            if (!opts.Render)
            {
                var preview = FirstSceneImage(storyboard) ?? job.PreviewUrl;
                await _repo.UpdateVideoJobAsync(
                    id, null, "Ready", job.ExternalRenderId, preview, job.OutputUrl,
                    string.Join("; ", pipelineNotes),
                    storyboardJson, configJson, DateTimeOffset.UtcNow, cancellationToken);
                return await GetJobAsync(id, cancellationToken);
            }

            // Refresh job fields after updates
            job = (await _repo.GetVideoJobAsync(id, cancellationToken))!;
            return await QueueRenderWithStoryboardAsync(job, template, storyboard, storyboardJson, configJson, voicePublicUrl, cancellationToken);
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
                null,
                cancellationToken);
            return await GetJobAsync(id, cancellationToken);
        }
    }

    public async Task<ContentVideoJobDto?> ApplyCreatomateWebhookAsync(
        string renderId,
        string status,
        string? url,
        string? snapshotUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(renderId)) return null;
        var jobs = await _repo.ListVideoJobsAsync(null, null, cancellationToken);
        var job = jobs.FirstOrDefault(j =>
            string.Equals(j.ExternalRenderId, renderId, StringComparison.OrdinalIgnoreCase));
        if (job is null) return null;

        var mapped = MapCreatomateStatus(status);
        await _repo.UpdateVideoJobAsync(
            job.Id,
            null,
            mapped,
            renderId,
            snapshotUrl ?? url ?? job.PreviewUrl,
            mapped == "Ready" ? (url ?? job.OutputUrl) : job.OutputUrl,
            mapped == "Failed" ? "Creatomate webhook: failed" : null,
            job.StoryboardJson,
            job.ConfigJson,
            mapped == "Ready" ? DateTimeOffset.UtcNow : job.RenderedAt,
            cancellationToken);
        return await GetJobAsync(job.Id, cancellationToken);
    }

    public async Task<ContentVideoJobDto?> QueueRenderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _repo.GetVideoJobAsync(id, cancellationToken);
        if (job is null) return null;

        var template = await _repo.GetVideoTemplateAsync(job.TemplateId, cancellationToken)
                       ?? throw new InvalidOperationException("Template thiếu.");

        var videoCfg = await _creatomate.ResolveAsync(cancellationToken);
        var creatomateTemplateId = FirstNonEmpty(template.ExternalTemplateId, videoCfg.CreatomateTemplateId);

        // Local / no Creatomate → storyboard Ready
        if (template.Provider != "creatomate"
            || !videoCfg.CreatomateConfigured
            || string.IsNullOrWhiteSpace(creatomateTemplateId))
        {
            var prepared = await PrepareStoryboardAsync(id, cancellationToken);
            if (prepared is null) return null;
            if (template.Provider == "creatomate" && !videoCfg.CreatomateConfigured)
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
        var scenes = BuildStoryboardObjects(job.ScriptBody, beats, template.DurationSec);
        var storyboardJson = ContentRepository.ToJson(scenes);

        return await QueueRenderWithStoryboardAsync(
            job, template, scenes, storyboardJson, job.ConfigJson, voicePublicUrl: null, cancellationToken);
    }

    private async Task<ContentVideoJobDto?> QueueRenderWithStoryboardAsync(
        ContentRepository.VideoJobRow job,
        ContentRepository.VideoTemplateRow template,
        IReadOnlyList<SceneBeat> scenes,
        string storyboardJson,
        string configJson,
        string? voicePublicUrl,
        CancellationToken cancellationToken)
    {
        var videoCfg = await _creatomate.ResolveAsync(cancellationToken);
        var creatomateTemplateId = FirstNonEmpty(template.ExternalTemplateId, videoCfg.CreatomateTemplateId);
        if (template.Provider != "creatomate"
            || !videoCfg.CreatomateConfigured
            || string.IsNullOrWhiteSpace(creatomateTemplateId))
        {
            var msg = template.Provider == "creatomate" && !videoCfg.CreatomateConfigured
                ? "Chưa có CreatomateApiKey — storyboard/assets sẵn sàng (CapCut hoặc cấu hình key)."
                : template.Provider == "creatomate" && string.IsNullOrWhiteSpace(creatomateTemplateId)
                    ? "Template Creatomate thiếu UUID — điền trên Model AI / Video."
                    : "storyboard_local — xuất CapCut; bật Creatomate template để render MP4.";

            await _repo.UpdateVideoJobAsync(
                job.Id,
                null,
                "Ready",
                job.ExternalRenderId,
                FirstSceneImage(scenes) ?? job.PreviewUrl,
                job.OutputUrl,
                msg,
                storyboardJson,
                configJson,
                DateTimeOffset.UtcNow,
                cancellationToken);
            return await GetJobAsync(job.Id, cancellationToken);
        }

        await _repo.UpdateVideoJobAsync(
            job.Id, null, "Queued", job.ExternalRenderId, job.PreviewUrl, job.OutputUrl,
            null, storyboardJson, configJson, null, cancellationToken);

        try
        {
            var mods = BuildCreatomateModifications(
                template.ConfigJson, job.Title, job.ScriptBody, scenes, voicePublicUrl);
            var render = await _creatomate.CreateRenderAsync(
                creatomateTemplateId!,
                mods,
                cancellationToken);

            var status = MapCreatomateStatus(render.Status);
            await _repo.UpdateVideoJobAsync(
                job.Id,
                null,
                status,
                render.Id,
                render.SnapshotUrl ?? render.Url,
                status == "Ready" ? render.Url : null,
                null,
                storyboardJson,
                configJson,
                status == "Ready" ? DateTimeOffset.UtcNow : null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            await _repo.UpdateVideoJobAsync(
                job.Id, null, "Failed", null, null, null, ex.Message,
                storyboardJson, configJson, null, cancellationToken);
        }

        return await GetJobAsync(job.Id, cancellationToken);
    }

    public async Task<ContentVideoJobDto?> RefreshRenderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _repo.GetVideoJobAsync(id, cancellationToken);
        if (job is null) return null;
        if (string.IsNullOrWhiteSpace(job.ExternalRenderId) || !await _creatomate.IsConfiguredAsync(cancellationToken))
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
        var bodies = variants
            .Where(v => !string.IsNullOrWhiteSpace(v.BodyMarkdown))
            .Select(v => (v.Kind, Body: ForNarration(v.BodyMarkdown)))
            .Where(v => v.Body.Length > 0)
            .ToList();
        if (bodies.Count == 0) return null;

        var tiktok = bodies.FirstOrDefault(v =>
            v.Kind.Equals("tiktok_script", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(tiktok.Body)
            && (tiktok.Body.Length >= 280 || HasBeatLabels(tiktok.Body)))
            return tiktok.Body;

        foreach (var kind in PreferredScriptKinds)
        {
            var hit = bodies
                .Where(v => v.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(v => v.Body.Length)
                .FirstOrDefault();
            if (hit.Body is { Length: >= 200 }) return hit.Body;
        }

        return bodies.OrderByDescending(v => v.Body.Length).First().Body;
    }

    private static bool HasBeatLabels(string script) =>
        Regex.IsMatch(script, @"(?im)^\s*(?:#+\s*)?(HOOK|PROBLEM|INSIGHT|SOLUTION|CTA)\s*[:\-–]");

    private static string ForNarration(string markdown)
    {
        var t = markdown.Replace("\r\n", "\n");
        t = Regex.Replace(t, @"^#+\s*", "", RegexOptions.Multiline);
        t = Regex.Replace(t, @"\*\*|__", "");
        t = Regex.Replace(t, @"\[(.*?)\]\([^)]+\)", "$1");
        t = Regex.Replace(t, @"`+", "");
        return t.Trim();
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

    private static List<object> BuildStoryboard(string script, IReadOnlyList<string> beats, int durationSec) =>
        BuildStoryboardObjects(script, beats, durationSec).Cast<object>().ToList();

    private static List<SceneBeat> BuildStoryboardObjects(string script, IReadOnlyList<string> beats, int durationSec)
    {
        var sections = ParseLabeledSections(script, beats);
        var per = Math.Max(3, durationSec / Math.Max(1, beats.Count));
        var cursor = 0;
        var result = new List<SceneBeat>();
        for (var i = 0; i < beats.Count; i++)
        {
            var beat = beats[i];
            sections.TryGetValue(beat, out var text);
            if (string.IsNullOrWhiteSpace(text))
                text = ChunkFallback(script, beats.Count, i);

            var start = cursor;
            var end = i == beats.Count - 1 ? durationSec : cursor + per;
            cursor = end;
            var trimmed = text.Trim();
            result.Add(new SceneBeat
            {
                Order = i + 1,
                Beat = beat,
                Type = beat.ToLowerInvariant(),
                StartSec = start,
                EndSec = end,
                Text = trimmed,
                VisualHint = beat switch
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
                VisualPrompt =
                    $"Cinematic vertical 9:16 Vietnamese pharmacy / SaaS marketing still for beat {beat}: {trimmed}. No watermark, no readable tiny text.",
            });
        }

        return result;
    }

    private async Task SetStatusAsync(
        Guid id,
        ContentRepository.VideoJobRow job,
        string status,
        string? error,
        CancellationToken ct) =>
        await _repo.UpdateVideoJobAsync(
            id, null, status, job.ExternalRenderId, job.PreviewUrl, job.OutputUrl, error,
            job.StoryboardJson, job.ConfigJson, job.RenderedAt, ct);

    private static Dictionary<string, object?> ParseConfigObject(string json)
    {
        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return dict ?? new Dictionary<string, object?>();
        }
        catch
        {
            return new Dictionary<string, object?>();
        }
    }

    private static string BuildPollinationsImageUrl(string prompt)
    {
        var p = prompt.Length > 400 ? prompt[..400] : prompt;
        return "https://image.pollinations.ai/prompt/"
               + Uri.EscapeDataString(p)
               + "?width=1080&height=1920&nologo=true";
    }

    private string ResolveVideoAssetRoot()
    {
        var configured = string.IsNullOrWhiteSpace(_options.Value.VideoAssetRoot)
            ? "App_Data/content-video"
            : _options.Value.VideoAssetRoot;
        return Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), configured));
    }

    private async Task<string> SaveVideoMediaAsync(
        Guid jobId,
        string fileName,
        byte[] bytes,
        CancellationToken ct)
    {
        var dir = Path.Combine(ResolveVideoAssetRoot(), jobId.ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        await File.WriteAllBytesAsync(path, bytes, ct);
        return path;
    }

    private string? BuildPublicMediaUrl(Guid jobId, string fileName, string? publicBase = null)
    {
        var baseUrl = FirstNonEmpty(
            publicBase,
            _options.Value.PublicMediaBaseUrl,
            _configuration["Content:PublicMediaBaseUrl"],
            _configuration["Platform:ApiUrl"]);
        if (string.IsNullOrWhiteSpace(baseUrl)) return null;
        // Creatomate cannot reach localhost — still return URL for tunnel setups.
        return $"{baseUrl.TrimEnd('/')}/api/content/video/media/{jobId:D}/{Uri.EscapeDataString(fileName)}";
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static string? FirstSceneImage(IEnumerable<SceneBeat> scenes) =>
        scenes.Select(s => s.ImageUrl).FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));

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
        var parts = Regex.Split(script, @"(?<=[\.!\?…])\s+|\n{2,}")
            .Select(s => s.Trim())
            .Where(s => s.Length > 18 && !s.StartsWith('#') && !Regex.IsMatch(s, @"^#[\w]+"))
            .ToList();
        if (parts.Count == 0)
        {
            var lines = script.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length == 0) return script.Trim();
            var lineSize = Math.Max(1, (int)Math.Ceiling(lines.Length / (double)n));
            return string.Join("\n", lines.Skip(index * lineSize).Take(lineSize));
        }

        var size = Math.Max(1, (int)Math.Ceiling(parts.Count / (double)n));
        return string.Join(" ", parts.Skip(index * size).Take(size));
    }

    private static Dictionary<string, string> BuildCreatomateModifications(
        string templateConfigJson,
        string title,
        string script,
        IReadOnlyList<SceneBeat> storyboard,
        string? voicePublicUrl)
    {
        var mods = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Title"] = title,
            ["Title.text"] = title,
            ["Script"] = script,
            ["Script.text"] = script,
        };

        Dictionary<string, string>? textKeys = null;
        Dictionary<string, string>? imageKeys = null;
        string? voiceKey = null;
        try
        {
            using var cfg = JsonDocument.Parse(
                string.IsNullOrWhiteSpace(templateConfigJson) ? "{}" : templateConfigJson);
            if (cfg.RootElement.TryGetProperty("sceneTextKeys", out var tk) && tk.ValueKind == JsonValueKind.Object)
            {
                textKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in tk.EnumerateObject())
                    textKeys[p.Name] = p.Value.GetString() ?? p.Name;
            }
            if (cfg.RootElement.TryGetProperty("sceneImageKeys", out var ik) && ik.ValueKind == JsonValueKind.Object)
            {
                imageKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in ik.EnumerateObject())
                    imageKeys[p.Name] = p.Value.GetString() ?? p.Name;
            }
            if (cfg.RootElement.TryGetProperty("voiceKey", out var vk))
                voiceKey = vk.GetString();

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

        foreach (var scene in storyboard)
        {
            var beat = scene.Beat;
            mods[beat] = scene.Text;
            mods[$"{beat}.text"] = scene.Text;
            if (textKeys is not null && textKeys.TryGetValue(beat, out var textMod))
                mods[textMod] = scene.Text;
            if (!string.IsNullOrWhiteSpace(scene.ImageUrl))
            {
                mods[$"{beat}.image"] = scene.ImageUrl;
                if (imageKeys is not null && imageKeys.TryGetValue(beat, out var imageMod))
                    mods[imageMod] = scene.ImageUrl;
            }
        }

        if (!string.IsNullOrWhiteSpace(voicePublicUrl))
        {
            mods["Voice.source"] = voicePublicUrl;
            mods["Voice.audio"] = voicePublicUrl;
            if (!string.IsNullOrWhiteSpace(voiceKey))
                mods[voiceKey] = voicePublicUrl;
        }

        return mods;
    }

    private sealed class SceneBeat
    {
        [JsonPropertyName("order")]
        public int Order { get; set; }

        [JsonPropertyName("beat")]
        public string Beat { get; set; } = "";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("startSec")]
        public int StartSec { get; set; }

        [JsonPropertyName("endSec")]
        public int EndSec { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; } = "";

        [JsonPropertyName("visualHint")]
        public string? VisualHint { get; set; }

        [JsonPropertyName("visualPrompt")]
        public string? VisualPrompt { get; set; }

        [JsonPropertyName("imageUrl")]
        public string? ImageUrl { get; set; }
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
