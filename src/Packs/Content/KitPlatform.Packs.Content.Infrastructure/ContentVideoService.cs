using System.Security.Cryptography;
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

    private const int MaxSceneImageBytes = 8 * 1024 * 1024;
    private readonly ContentRepository _repo;
    private readonly ContentCreatomateClient _creatomate;
    private readonly ContentElevenLabsClient _elevenLabs;
    private readonly IOptions<ContentOptions> _options;
    private readonly IConfiguration _configuration;
    private readonly MarketingLocalVideoRenderer _localRenderer = new(new ProcessMarketingVideoFfmpeg());

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
        await NovixaE2eIsolation.AssertAsync(_repo, _options.Value.VideoAssetRoot, cancellationToken);
        var package = await _repo.GetPackageAsync(request.PackageId, cancellationToken)
                      ?? throw new InvalidOperationException("Package không tồn tại.");

        var template = await ResolveTemplateAsync(request, cancellationToken);
        if (!template.IsActive)
            throw new InvalidOperationException("Template đang tắt.");

        var variants = await _repo.ListVariantsAsync(ContentPackageScope.RequireTopic(package), cancellationToken);
        var script = PickScript(variants);
        if (string.IsNullOrWhiteSpace(script))
            throw new InvalidOperationException(
                "Package chưa có bản viết (web / Facebook / tiktok_script) — Generate góc trước.");

        var title = package.Title.Trim();
        if (title.Length > 480) title = title[..480];

        var id = await _repo.InsertVideoJobAsync(
            ContentPackageScope.RequireBrand(package),
            package.Id,
            ContentPackageScope.RequireTopic(package),
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
        await NovixaE2eIsolation.AssertAsync(_repo, _options.Value.VideoAssetRoot, cancellationToken);
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

        var novixa = IsNovixaTiktokHooks(job, template);
        await _repo.UpdateVideoJobAsync(
            id,
            scriptBody: script,
            status: NovixaLocalRenderGate.StatusAfterStoryboard(novixa),
            externalRenderId: job.ExternalRenderId,
            previewUrl: job.PreviewUrl,
            outputUrl: novixa ? null : job.OutputUrl,
            errorMessage: null,
            storyboardJson: storyboardJson,
            configJson: job.ConfigJson,
            renderedAt: novixa ? null : DateTimeOffset.UtcNow,
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
        var storyboardJson = job.StoryboardJson;
        var configJson = job.ConfigJson;
        var novixa = IsNovixaTiktokHooks(job, template);

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

            storyboardJson = ContentRepository.ToJson(storyboard);
            config["mvpPipeline"] = new
            {
                at = DateTimeOffset.UtcNow,
                notes = pipelineNotes,
                creatomateConfigured = videoCfg.CreatomateConfigured,
                elevenLabsConfigured = videoCfg.ElevenLabsConfigured,
            };
            configJson = ContentRepository.ToJson(config);

            await _repo.UpdateVideoJobAsync(
                id,
                null,
                "PreparingRender",
                job.ExternalRenderId,
                job.PreviewUrl,
                novixa ? null : job.OutputUrl,
                null,
                storyboardJson,
                configJson,
                null,
                cancellationToken);

            if (!opts.Render)
            {
                var preview = FirstSceneImage(storyboard) ?? job.PreviewUrl;
                if (novixa)
                {
                    var note = string.Join("; ", pipelineNotes);
                    await _repo.UpdateVideoJobAsync(
                        id, null, "PreparingRender", job.ExternalRenderId, preview, null,
                        string.IsNullOrWhiteSpace(note) ? null : note,
                        storyboardJson, configJson, null, cancellationToken);
                    return await GetJobAsync(id, cancellationToken);
                }

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
                novixa ? null : job.OutputUrl,
                ex.Message,
                storyboardJson,
                configJson,
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

        var mapped = ContentVideoRenderStatus.FromProvider(status, url);
        await _repo.UpdateVideoJobAsync(
            job.Id,
            null,
            mapped,
            renderId,
            snapshotUrl ?? url ?? job.PreviewUrl,
            mapped == "Ready" ? (url ?? job.OutputUrl) : job.OutputUrl,
            mapped == "Failed" ? ContentVideoRenderStatus.FailureNote(status, url) : null,
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

        if (IsNovixaTiktokHooks(job, template))
            return await RenderNovixaLocalFromJobAsync(job, template, cancellationToken);

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
        if (IsNovixaTiktokHooks(job, template))
            return await RenderNovixaLocalAsync(job, template, scenes, storyboardJson, configJson, cancellationToken);

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

            var status = ContentVideoRenderStatus.FromProvider(render.Status, render.Url);
            await _repo.UpdateVideoJobAsync(
                job.Id,
                null,
                status,
                render.Id,
                render.SnapshotUrl ?? render.Url,
                status == "Ready" ? render.Url : null,
                status == "Failed" ? ContentVideoRenderStatus.FailureNote(render.Status, render.Url) : null,
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
            var status = ContentVideoRenderStatus.FromProvider(render.Status, render.Url);
            await _repo.UpdateVideoJobAsync(
                id,
                null,
                status,
                render.Id,
                render.SnapshotUrl ?? render.Url ?? job.PreviewUrl,
                status == "Ready" ? render.Url : job.OutputUrl,
                status == "Failed" ? ContentVideoRenderStatus.FailureNote(render.Status, render.Url) : null,
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

        var template = await _repo.GetVideoTemplateAsync(job.TemplateId, cancellationToken)
                       ?? throw new InvalidOperationException("Template thiếu.");
        if (IsNovixaTiktokHooks(job, template))
        {
            if (!ContentVideoMediaPaths.IsJobOutputRoute(job.Id, job.OutputUrl))
                throw new InvalidOperationException("Chỉ duyệt khi MP4 đã gắn đúng route media của job.");
            var mediaPath = ResolveLocalMediaPath(job.Id, ContentVideoMediaPaths.OutputFileName);
            if (mediaPath is null)
                throw new InvalidOperationException("Không thấy output.mp4 của job để duyệt.");
            var total = StoryboardDuration(ReadStoryboard(job.StoryboardJson));
            var probe = await NovixaLocalRenderGate.ProbeMp4Async(
                mediaPath, template.AspectRatio, total, cancellationToken);
            if (!probe.Ok)
                throw new InvalidOperationException(probe.Error ?? "MP4 không giải mã được.");
        }

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

    private string ResolveVideoAssetRoot() =>
        ContentVideoMediaPaths.ResolveRoot(_options.Value.VideoAssetRoot, Directory.GetCurrentDirectory());

    public string? ResolveLocalMediaPath(Guid jobId, string fileName)
    {
        if (!ContentVideoMediaPaths.IsSafeFileName(fileName))
            return null;
        var root = ResolveVideoAssetRoot();
        var path = Path.GetFullPath(ContentVideoMediaPaths.File(root, jobId, fileName));
        var prefix = ContentVideoMediaPaths.JobDirectory(root, jobId)
                     .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                     + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            return null;
        return path;
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

    private static bool IsNovixaTiktokHooks(
        ContentRepository.VideoJobRow job,
        ContentRepository.VideoTemplateRow template) =>
        NovixaLocalRenderGate.IsGate(job.BrandCode, template.Code, template.Provider);

    private const long MaxImportedVideoBytes = 300L * 1024 * 1024;

    public async Task<ContentVideoJobDto> ImportFinishedAsync(
        ImportFinishedVideoRequest request,
        Stream mp4,
        CancellationToken cancellationToken = default)
    {
        await NovixaE2eIsolation.AssertAsync(_repo, _options.Value.VideoAssetRoot, cancellationToken);
        var brand = await _repo.GetBrandAsync(request.BrandId, cancellationToken)
                    ?? throw new InvalidOperationException("Brand không tồn tại.");
        var title = (request.Title ?? "").Trim();
        if (title.Length == 0)
            throw new InvalidOperationException("Thiếu tiêu đề video.");
        if (title.Length > 480)
            title = title[..480];
        var templateCode = string.IsNullOrWhiteSpace(request.TemplateCode)
            ? "tiktok_45s_hooks"
            : request.TemplateCode.Trim();
        var template = await _repo.GetVideoTemplateByCodeAsync(templateCode, cancellationToken)
                       ?? throw new InvalidOperationException("Template không tồn tại: " + templateCode);
        if (!template.IsActive)
            throw new InvalidOperationException("Template đang tắt.");
        if (!MarketingLocalVideoRenderer.TryFrameSize(template.AspectRatio, out _, out _))
            throw new InvalidOperationException("Tỷ lệ khung của template không hỗ trợ nhập: " + template.AspectRatio);

        var id = await _repo.InsertVideoJobAsync(
            brand.Id,
            null,
            null,
            template.Id,
            title,
            (request.ScriptBody ?? "").Trim(),
            "Draft",
            "storyboard_local",
            "[]",
            ContentRepository.ToJson(new { imported = true }),
            cancellationToken);

        var jobDir = ContentVideoMediaPaths.JobDirectory(ResolveVideoAssetRoot(), id);
        Directory.CreateDirectory(jobDir);
        var tempPath = Path.Combine(jobDir, "import.tmp.mp4");
        var outputPath = Path.Combine(jobDir, ContentVideoMediaPaths.OutputFileName);
        try
        {
            long written = 0;
            await using (var target = File.Create(tempPath))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await mp4.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    written += read;
                    if (written > MaxImportedVideoBytes)
                        throw new InvalidOperationException("File MP4 quá lớn (tối đa 300 MB).");
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }

            if (written == 0)
                throw new InvalidOperationException("File MP4 rỗng.");

            var probe = await NovixaLocalRenderGate.ProbeMp4Async(tempPath, template.AspectRatio, 0, cancellationToken);
            if (!probe.Ok)
                throw new InvalidOperationException(probe.Error ?? "MP4 không giải mã được.");
            var seconds = (int)Math.Floor(probe.DurationSec);
            if (seconds < 1)
                throw new InvalidOperationException("MP4 ngắn hơn 1 giây.");

            string sha256;
            await using (var hashStream = File.OpenRead(tempPath))
                sha256 = Convert.ToHexString(await SHA256.HashDataAsync(hashStream, cancellationToken)).ToLowerInvariant();

            File.Move(tempPath, outputPath, overwrite: true);
            var storyboard = JsonSerializer.Serialize(new[]
            {
                new SceneBeat { Order = 1, Beat = "IMPORT", Type = "imported", StartSec = 0, EndSec = seconds, Text = title },
            });
            var config = ContentRepository.ToJson(new
            {
                imported = true,
                sourceFileName = request.SourceFileName,
                sha256,
                bytes = written,
                durationSeconds = Math.Round(probe.DurationSec, 2),
                importedAt = DateTimeOffset.UtcNow,
            });
            await _repo.UpdateVideoJobAsync(
                id,
                null,
                "Ready",
                null,
                null,
                ContentVideoMediaPaths.OutputRoute(id),
                null,
                storyboard,
                config,
                DateTimeOffset.UtcNow,
                cancellationToken);
            return (await GetJobAsync(id, cancellationToken))!;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch (IOException)
            {
                /* the failure below is the answer; a locked temp file is not advertised */
            }

            await _repo.UpdateVideoJobAsync(
                id,
                null,
                "Failed",
                null,
                null,
                null,
                ex.Message,
                "[]",
                ContentRepository.ToJson(new { imported = true, sourceFileName = request.SourceFileName }),
                null,
                cancellationToken);
            throw ex is InvalidOperationException ? ex : new InvalidOperationException("Không ghi được file MP4: " + ex.Message);
        }
    }

    private async Task<ContentVideoJobDto?> RenderNovixaLocalFromJobAsync(
        ContentRepository.VideoJobRow job,
        ContentRepository.VideoTemplateRow template,
        CancellationToken cancellationToken)
    {
        var scenes = ReadStoryboard(job.StoryboardJson);
        if (scenes.Count == 0)
        {
            var trimmed = (job.StoryboardJson ?? "").Trim();
            var reason = trimmed is "" or "[]" or "null"
                ? "Chưa có storyboard NOVIXA. Chạy MVP (ảnh + giọng) trước khi render."
                : "storyboard_json không đọc được.";
            return await FailNovixaAsync(job, job.StoryboardJson ?? "[]", job.ConfigJson ?? "{}", reason, cancellationToken);
        }

        return await RenderNovixaLocalAsync(
            job, template, scenes, job.StoryboardJson ?? "[]", job.ConfigJson ?? "{}", cancellationToken);
    }

    private async Task<ContentVideoJobDto?> RenderNovixaLocalAsync(
        ContentRepository.VideoJobRow job,
        ContentRepository.VideoTemplateRow template,
        IReadOnlyList<SceneBeat> scenes,
        string storyboardJson,
        string configJson,
        CancellationToken cancellationToken)
    {
        try
        {
            var jobDir = ContentVideoMediaPaths.JobDirectory(ResolveVideoAssetRoot(), job.Id);
            Directory.CreateDirectory(jobDir);
            var prepared = new List<NovixaLocalRenderGate.SceneInput>(scenes.Count);
            for (var i = 0; i < scenes.Count; i++)
            {
                var scene = scenes[i];
                if (string.IsNullOrWhiteSpace(scene.Text))
                    return await FailNovixaAsync(job, storyboardJson, configJson, "Scene " + scene.Beat + " thiếu lời.", cancellationToken);
                if (scene.EndSec <= scene.StartSec)
                    return await FailNovixaAsync(job, storyboardJson, configJson, "Scene " + scene.Beat + " không có thời lượng.", cancellationToken);
                var imagePath = await MaterializeSceneImageAsync(jobDir, scene, i, cancellationToken);
                prepared.Add(new NovixaLocalRenderGate.SceneInput(
                    scene.Beat, scene.Text.Trim(), scene.StartSec, scene.EndSec, imagePath));
            }

            var total = StoryboardDuration(scenes);
            string voicePath;
            try
            {
                voicePath = await ResolveJobVoicePathAsync(jobDir, configJson, total, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return await FailNovixaAsync(job, storyboardJson, configJson, ex.Message, cancellationToken);
            }

            await _repo.UpdateVideoJobAsync(
                job.Id, null, "Rendering", job.ExternalRenderId, job.PreviewUrl, null, null,
                storyboardJson, configJson, null, cancellationToken);

            var outputUrl = BuildPublicMediaUrl(job.Id, ContentVideoMediaPaths.OutputFileName)
                            ?? ContentVideoMediaPaths.OutputRoute(job.Id);
            var attempt = await NovixaLocalRenderGate.TryRenderAsync(
                _localRenderer,
                jobDir,
                template.AspectRatio,
                prepared,
                voicePath,
                job.Id,
                outputUrl,
                cancellationToken);
            if (!attempt.Ready || !ContentVideoMediaPaths.IsJobOutputRoute(job.Id, attempt.OutputUrl))
            {
                return await FailNovixaAsync(
                    job, storyboardJson, configJson,
                    attempt.Error ?? "Render local không tạo được MP4 hợp lệ.",
                    cancellationToken);
            }

            var published = ResolveLocalMediaPath(job.Id, ContentVideoMediaPaths.OutputFileName);
            if (published is null)
            {
                return await FailNovixaAsync(
                    job, storyboardJson, configJson,
                    "Đã có MP4 nhưng không gắn được vào route media của job.",
                    cancellationToken);
            }

            await _repo.UpdateVideoJobAsync(
                job.Id,
                null,
                "Ready",
                job.ExternalRenderId,
                FirstSceneImage(scenes) ?? job.PreviewUrl,
                attempt.OutputUrl,
                null,
                storyboardJson,
                configJson,
                DateTimeOffset.UtcNow,
                cancellationToken);
            return await GetJobAsync(job.Id, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FailNovixaAsync(job, storyboardJson, configJson, ex.Message, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await FailNovixaAsync(
                job, storyboardJson, configJson,
                "Render local thất bại: " + ex.GetType().Name, cancellationToken);
        }
    }

    private async Task<ContentVideoJobDto?> FailNovixaAsync(
        ContentRepository.VideoJobRow job,
        string storyboardJson,
        string configJson,
        string reason,
        CancellationToken cancellationToken)
    {
        await _repo.UpdateVideoJobAsync(
            job.Id, null, "Failed", job.ExternalRenderId, job.PreviewUrl, null, reason,
            storyboardJson, configJson, null, cancellationToken);
        return await GetJobAsync(job.Id, cancellationToken);
    }

    private static async Task<string> ResolveJobVoicePathAsync(
        string jobDir,
        string configJson,
        int storyboardTotalSec,
        CancellationToken cancellationToken)
    {
        var configured = ReadConfigString(configJson, "voiceLocalPath");
        var candidate = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(jobDir, "voice.mp3")
            : configured;
        var voice = await NovixaLocalRenderGate.ProbeAudioCoversAsync(
            jobDir, candidate, storyboardTotalSec, cancellationToken);
        if (!voice.Ok || string.IsNullOrWhiteSpace(voice.Path))
            throw new InvalidOperationException(voice.Error ?? "Thiếu voice.mp3 trong thư mục job.");
        return voice.Path;
    }

    private static int StoryboardDuration(IReadOnlyList<SceneBeat> scenes)
    {
        var total = 0;
        foreach (var scene in scenes)
        {
            if (scene.EndSec > scene.StartSec)
                total += scene.EndSec - scene.StartSec;
        }

        return total;
    }

    private async Task<string> MaterializeSceneImageAsync(
        string jobDir,
        SceneBeat scene,
        int index,
        CancellationToken cancellationToken)
    {
        var dest = Path.Combine(jobDir, "scene-" + (index + 1).ToString("00") + "-source.bin");
        if (NovixaLocalRenderGate.ImageDecodes(dest))
            return dest;
        if (File.Exists(dest))
            File.Delete(dest);

        NovixaE2eIsolation.ThrowIfExternalCall("Pollinations");
        var raw = scene.ImageUrl?.Trim();
        if (string.IsNullOrWhiteSpace(raw)
            || !Uri.TryCreate(raw, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !uri.Host.Equals("image.pollinations.ai", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Scene " + scene.Beat + " thiếu ảnh Pollinations đã lưu trên storyboard. Renderer không tự tạo ảnh.");
        }

        try
        {
            using var http = new HttpClient(new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 3,
            })
            {
                Timeout = TimeSpan.FromSeconds(30),
            };
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("Scene " + scene.Beat + ": tải ảnh thất bại (" + (int)response.StatusCode + ").");
            var finalUri = response.RequestMessage?.RequestUri;
            if (finalUri is null
                || finalUri.Scheme != Uri.UriSchemeHttps
                || !finalUri.Host.Equals("image.pollinations.ai", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Scene " + scene.Beat + ": ảnh không còn ở image.pollinations.ai.");
            }

            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = File.Create(dest))
            {
                var buffer = new byte[81920];
                var total = 0;
                while (true)
                {
                    var read = await input.ReadAsync(buffer.AsMemory(), cancellationToken);
                    if (read == 0)
                        break;
                    total += read;
                    if (total > MaxSceneImageBytes)
                        throw new InvalidOperationException("Scene " + scene.Beat + ": ảnh lớn hơn 8 MB.");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }

                if (total < 32)
                    throw new InvalidOperationException("Scene " + scene.Beat + ": ảnh rỗng.");
            }
        }
        catch
        {
            if (File.Exists(dest))
                File.Delete(dest);
            throw;
        }

        if (!HasImageFileHeader(dest) || !NovixaLocalRenderGate.ImageDecodes(dest))
        {
            File.Delete(dest);
            throw new InvalidOperationException("Scene " + scene.Beat + ": file tải về không phải ảnh đã giải mã.");
        }

        return dest;
    }

    private static bool HasImageFileHeader(string path)
    {
        Span<byte> header = stackalloc byte[12];
        using var stream = File.OpenRead(path);
        var read = stream.Read(header);
        return read >= 12 && MarketingLocalVideoRenderer.IsImageHeader(header[..read]);
    }

    private static string? ReadConfigString(string json, string key)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (!doc.RootElement.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.String)
                return null;
            return value.GetString();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<SceneBeat> ReadStoryboard(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<SceneBeat>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
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
