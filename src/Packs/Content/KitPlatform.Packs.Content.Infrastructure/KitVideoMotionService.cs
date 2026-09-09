using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoMotionService : IKitVideoMotionService
{
    private readonly KitVideoMotionRepository _repo;
    private readonly IKitVideoPixelService _pixels;
    private readonly KitVideoArtifactStore _images;
    private readonly KitVideoVideoStore _videos;
    private readonly ContentRunwayClient _runway;
    private readonly IContentSeriesTakeProxyService _takes;
    private readonly ContentGeminiClient _gemini;
    private readonly KitVideoVisionRepository _keyframes;
    private readonly ContentRepository _productions;
    private readonly IRunwayRequestCompiler _compiler;
    private readonly IVideoOutputQA _videoQa;
    private readonly IVisualUniverseSnapshotResolver _snapshots;
    private readonly IUnifiedVisualCompiler _visualCompiler;

    public KitVideoMotionService(
        KitVideoMotionRepository repo,
        IKitVideoPixelService pixels,
        KitVideoArtifactStore images,
        KitVideoVideoStore videos,
        ContentRunwayClient runway,
        IContentSeriesTakeProxyService takes,
        ContentGeminiClient gemini,
        KitVideoVisionRepository keyframes,
        ContentRepository productions,
        IRunwayRequestCompiler compiler,
        IVideoOutputQA videoQa,
        IVisualUniverseSnapshotResolver snapshots,
        IUnifiedVisualCompiler visualCompiler)
    {
        _repo = repo;
        _pixels = pixels;
        _images = images;
        _videos = videos;
        _runway = runway;
        _takes = takes;
        _gemini = gemini;
        _keyframes = keyframes;
        _productions = productions;
        _compiler = compiler;
        _videoQa = videoQa;
        _snapshots = snapshots;
        _visualCompiler = visualCompiler;
    }

    public async Task<KitVideoMotionTakeDto> PreflightAsync(
        KitVideoMotionSubmitRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var compiled = KitVideoMotionRules.Compile(request.MotionContract);
            var built = await BuildPreflightAsync(request.KeyframeAttemptId, compiled, cancellationToken);
            var status = built.Preflight.Ok ? "READY" : "BLOCKED";
            return new KitVideoMotionTakeDto(
                Guid.Empty, built.ProductionId, compiled.ShotCode, request.KeyframeAttemptId, 0, status,
                compiled.Model, compiled.DurationSec, compiled.Prompt, built.Fingerprint, built.SourceHash,
                null, null, null, null, built.Preflight.Ok ? "" : "RUNWAY_NOT_CALLED", "", "", "NONE",
                built.Preflight, null, built.Preflight.Ok ? "" : string.Join(" | ", built.Preflight.Blocked),
                false, false, false);
        }
        catch (InvalidOperationException ex)
        {
            return Blocked(request.KeyframeAttemptId, ex.Message);
        }
    }

    public async Task<KitVideoMotionTakeDto> SubmitAsync(
        KitVideoMotionSubmitRequest request,
        CancellationToken cancellationToken = default)
    {
        var key = (request.IdempotencyKey ?? "").Trim();
        if (key.Length < 8)
            throw new InvalidOperationException("IdempotencyKey bắt buộc (≥ 8 ký tự).");
        var existing = await _repo.GetByIdempotencyAsync(key, cancellationToken);
        if (existing is not null)
        {
            if (request.Confirmed && existing.Status == "READY" && !existing.Confirmed)
                return await CallRunwayAsync(existing, cancellationToken);
            return await ToDto(existing, cancellationToken);
        }

        KitVideoMotionRules.EnsureRetryReason(request.RetryReason);
        KitVideoMotionCompileDto compiled;
        try
        {
            compiled = KitVideoMotionRules.Compile(request.MotionContract);
        }
        catch (InvalidOperationException ex)
        {
            return Blocked(request.KeyframeAttemptId, ex.Message);
        }
        var built = await BuildPreflightAsync(request.KeyframeAttemptId, compiled, cancellationToken);
        if (!built.Preflight.Ok)
            return Blocked(request.KeyframeAttemptId, string.Join(" | ", built.Preflight.Blocked), compiled, built);

        var prior = await _repo.ListByKeyframeAsync(request.KeyframeAttemptId, cancellationToken);
        if (prior.Count >= KitVideoMotionRules.MaxAttempts)
            throw new InvalidOperationException("REVIEW_REQUIRED: MAX_AUTO_ATTEMPTS.");
        var lastFail = prior.LastOrDefault(t => t.Status is "FAILED" or "DIAGNOSE" or "VIDEO_QA_FAIL");
        if (lastFail is not null)
            KitVideoMotionRules.EnsureNotBlindRetry(lastFail.Fingerprint, built.Fingerprint, request.RetryReason);

        var row = new KitVideoMotionRepository.TakeRow
        {
            Id = Guid.NewGuid(),
            ProductionId = built.ProductionId,
            ShotCode = KitVideoMotionRules.GoldenShot,
            KeyframeAttemptId = request.KeyframeAttemptId,
            AttemptNo = prior.Count + 1,
            Status = "READY",
            Model = compiled.Model,
            DurationSec = compiled.DurationSec,
            Ratio = compiled.Ratio,
            MotionPrompt = compiled.Prompt,
            Fingerprint = built.Fingerprint,
            SourceArtifactSha256 = built.SourceHash,
            RetryReason = request.RetryReason ?? "",
            CreditState = "NONE",
            IdempotencyKey = key,
            MotionJson = request.MotionContract.ValueKind == JsonValueKind.Undefined
                ? JsonSerializer.Serialize(compiled)
                : request.MotionContract.GetRawText(),
            MetadataJson = "{}",
            ProviderJson = "{}",
            QaJson = "{}",
            Confirmed = false,
        };
        await _repo.InsertAsync(row, cancellationToken);
        if (!request.Confirmed)
            return await ToDto(row, built.Preflight, cancellationToken);
        return await CallRunwayAsync(row, cancellationToken);
    }

    public async Task<KitVideoMotionTakeDto> PollAsync(Guid takeId, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetByIdAsync(takeId, cancellationToken)
            ?? throw new InvalidOperationException("Take không tồn tại.");
        if (row.Status is "VIDEO_READY" or "READY_FOR_DIRECTOR" or "APPROVED_TAKE" or "VIDEO_QA_FAIL" or "DIAGNOSE" or "REJECTED" or "INVALIDATED" or "BLOCKED")
            return await ToDto(row, cancellationToken);
        if (string.IsNullOrWhiteSpace(row.RunwayTaskId))
            return await ToDto(row, cancellationToken);

        if (row.Status == "SUBMITTED")
            Advance(row, "PROCESSING");
        (string Status, string? VideoUrl, string? Error, string? FailureCode) task;
        try
        {
            task = await _runway.GetTaskAsync(row.RunwayTaskId, cancellationToken);
        }
        catch (Exception ex)
        {
            row.MetadataJson = JsonSerializer.Serialize(new { diagnose = ex.Message });
            await _repo.UpdateAsync(row, cancellationToken);
            return await ToDto(row, cancellationToken);
        }
        await _repo.InsertProviderTaskAsync(
            row.Id, row.RunwayTaskId, row.Fingerprint, row.SourceArtifactSha256,
            task.Status, task.FailureCode ?? "", task.Error ?? "", task.VideoUrl, cancellationToken);
        row.ProviderJson = JsonSerializer.Serialize(new { task.Status, task.VideoUrl, task.Error, task.FailureCode });
        if (task.Status is "PENDING" or "RUNNING" or "THROTTLED")
        {
            await _repo.UpdateAsync(row, cancellationToken);
            return await ToDto(row, cancellationToken);
        }
        if (task.Status != "SUCCEEDED" || string.IsNullOrWhiteSpace(task.VideoUrl))
        {
            row.FailureCode = task.FailureCode ?? "";
            row.FailureClass = KitVideoMotionRules.ClassifyFailure(null, task.FailureCode, task.Error);
            row.CreditState = "REFUND_PENDING";
            row.MetadataJson = JsonSerializer.Serialize(new { diagnose = KitVideoMotionRules.Diagnose(task.FailureCode, task.Error) });
            Advance(row, "FAILED");
            Advance(row, "DIAGNOSE");
            await _repo.UpdateAsync(row, cancellationToken);
            return await ToDto(row, cancellationToken);
        }

        Advance(row, "SUCCEEDED");
        row.OutputUrl = task.VideoUrl;
        row.CreditState = "CHARGED";
        Advance(row, "DOWNLOADING");
        byte[] video;
        try
        {
            var fetched = await _takes.FetchAsync(task.VideoUrl, cancellationToken);
            video = fetched.Bytes;
        }
        catch (Exception ex)
        {
            row.Status = "FAILED";
            row.FailureClass = "VIDEO_DOWNLOAD_FAILED";
            row.MetadataJson = JsonSerializer.Serialize(new { diagnose = ex.Message });
            await _repo.UpdateAsync(row, cancellationToken);
            return await ToDto(row, cancellationToken);
        }

        row.VideoPath = _videos.Persist(row.ProductionId, row.ShotCode, row.AttemptNo, video);
        row.VideoSha256 = KitVideoIntegrityRules.Sha256Hex(video);
        Advance(row, "ARTIFACT_VERIFY");
        var tech = KitVideoMotionRules.EvaluateTechnical(
            video, "SUCCEEDED", task.VideoUrl, row.DurationSec, row.VideoSha256, row.VideoSha256, row.SourceArtifactSha256);
        Advance(row, "VIDEO_QA");
        var visual = await AnalyzeVideoAsync(row, video, cancellationToken);
        var qa = MergeQa(tech, visual);
        row.QaJson = JsonSerializer.Serialize(qa);
        if (tech.Status != "PASS")
        {
            row.FailureClass = "VIDEO_VERIFY_FAIL";
            Advance(row, "FAILED");
        }
        else if (qa.Status == "FAIL")
        {
            row.FailureClass = "VIDEO_QA_FAIL";
            Advance(row, "VIDEO_QA_FAIL");
        }
        else
        {
            Advance(row, "READY_FOR_DIRECTOR");
        }
        await _repo.UpdateAsync(row, cancellationToken);
        return await ToDto(row, cancellationToken);
    }

    public async Task<KitVideoMotionTakeDto> GetAsync(Guid takeId, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetByIdAsync(takeId, cancellationToken)
            ?? throw new InvalidOperationException("Take không tồn tại.");
        return await ToDto(row, cancellationToken);
    }

    public async Task<KitVideoMotionTakeDto?> GetLatestByKeyframeAsync(
        Guid keyframeAttemptId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListByKeyframeAsync(keyframeAttemptId, cancellationToken);
        var row = rows.LastOrDefault();
        return row is null ? null : await ToDto(row, cancellationToken);
    }

    public async Task<KitVideoMotionTakeDto> DecideAsync(Guid takeId, string decision, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetByIdAsync(takeId, cancellationToken)
            ?? throw new InvalidOperationException("Take không tồn tại.");
        if (row.Status != "READY_FOR_DIRECTOR")
            throw new InvalidOperationException("NOT_READY: Director chỉ approve/reject khi READY_FOR_DIRECTOR.");
        var live = _videos.Read(row.VideoPath);
        if (live is null || KitVideoIntegrityRules.Sha256Hex(live) != row.VideoSha256)
        {
            row.Status = "INVALIDATED";
            row.FailureClass = "VIDEO_FINGERPRINT_MISMATCH";
            await _repo.UpdateAsync(row, cancellationToken);
            throw new InvalidOperationException("INVALIDATED: video bytes đã đổi sau VIDEO_READY.");
        }
        if (string.Equals(decision, "APPROVE", StringComparison.OrdinalIgnoreCase))
        {
            KitVideoVideoQaDto? qa = null;
            if (!string.IsNullOrWhiteSpace(row.QaJson) && row.QaJson is not "{}")
                qa = JsonSerializer.Deserialize<KitVideoVideoQaDto>(row.QaJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (qa?.Status == "FAIL" || qa?.P0Fail is { Count: > 0 })
                throw new InvalidOperationException("NOT_READY: Video QA FAIL — không approve.");
            Advance(row, "APPROVED_TAKE");
        }
        else
        {
            Advance(row, "REJECTED");
        }
        await _repo.UpdateAsync(row, cancellationToken);
        return await ToDto(row, cancellationToken);
    }

    public async Task<(byte[] Bytes, string Mime)?> ReadVideoAsync(Guid takeId, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetByIdAsync(takeId, cancellationToken);
        if (row is null) return null;
        var bytes = _videos.Read(row.VideoPath);
        return bytes is null ? null : (bytes, "video/mp4");
    }

    private async Task<KitVideoMotionTakeDto> CallRunwayAsync(
        KitVideoMotionRepository.TakeRow row,
        CancellationToken ct)
    {
        var kf = await _keyframes.GetByIdAsync(row.KeyframeAttemptId, ct)
            ?? throw new InvalidOperationException("Approved keyframe không tồn tại.");
        var jpeg = _images.Read(kf.ImagePath);
        if (jpeg is null) throw new InvalidOperationException("ARTIFACT_FAILED: keyframe không đọc được.");
        var pack = await _pixels.I2vPackageAsync(row.KeyframeAttemptId, ct);
        using var motionDoc = JsonDocument.Parse(row.MotionJson == "{}"
            ? JsonSerializer.Serialize(new { shotCode = row.ShotCode, action = row.MotionPrompt, camera = "", motion = row.MotionPrompt, durationSec = row.DurationSec, model = row.Model })
            : row.MotionJson);
        var compiledReq = _compiler.Compile(pack, motionDoc.RootElement);
        if (compiledReq.InputImage != "APPROVED_KEYFRAME")
            throw new InvalidOperationException("PREFLIGHT: inputImage phải là APPROVED_KEYFRAME.");
        var prompt = await CompileBoundPromptAsync(
            compiledReq.PromptText, compiledReq.Duration, ct);
        KitVideoEngineRules.EnsureCreditGate(true);
        Advance(row, "SUBMITTING");
        row.Confirmed = true;
        row.CreditState = "PENDING";
        await _repo.UpdateAsync(row, ct);
        try
        {
            var dataUri = "data:image/jpeg;base64," + Convert.ToBase64String(jpeg);
            row.RunwayTaskId = await _runway.CreateImageToVideoAsync(
                dataUri, prompt, compiledReq.Duration, compiledReq.Ratio, ct);
            Advance(row, "SUBMITTED");
            row.CreditState = "ESTIMATED";
            await _repo.InsertProviderTaskAsync(
                row.Id, row.RunwayTaskId, row.Fingerprint, row.SourceArtifactSha256,
                "RUNWAY_ACCEPTED", "", "", null, ct);
            await _repo.UpdateAsync(row, ct);
        }
        catch (Exception ex)
        {
            row.Status = "FAILED";
            row.FailureClass = "SUBMIT_FAILED";
            row.CreditState = "REFUND_PENDING";
            row.MetadataJson = JsonSerializer.Serialize(new { diagnose = ex.Message });
            await _repo.UpdateAsync(row, ct);
        }
        return await ToDto(row, ct);
    }

    private async Task<string> CompileBoundPromptAsync(
        string motionLayer,
        int duration,
        CancellationToken cancellationToken)
    {
        var (snapshot, snapGate) = await VideoVisualIngressV1Rules.ResolveSnapshotAsync(
            _snapshots, FamixaVisualUniverseAuthorityV1Rules.ProjectId, cancellationToken);
        if (snapGate is not null || snapshot is null)
            throw new InvalidOperationException(snapGate ?? VideoVisualIngressV1Rules.GateSnapshot);
        var compiled = VideoVisualIngressV1Rules.CompileVideo(
            snapshot,
            new SeriesStillVisualIngressV1Rules.SceneVisualContract(
                ProjectId: FamixaVisualUniverseAuthorityV1Rules.ProjectId,
                Motion: motionLayer,
                Duration: duration > 0 ? duration.ToString() : null,
                Framing: VideoVisualIngressV1Rules.VideoView),
            _visualCompiler);
        if (compiled.Gate is not null || compiled.Contract is null
            || !VideoVisualIngressV1Rules.ProviderMayCall(compiled.Contract))
            throw new InvalidOperationException(compiled.Gate ?? VideoVisualIngressV1Rules.GateCompile);
        return VideoVisualIngressV1Rules.RunwayI2vPrompt(
            VideoVisualIngressV1Rules.ComposeMotionLayer(
                new SeriesStillVisualIngressV1Rules.SceneVisualContract(
                    ProjectId: FamixaVisualUniverseAuthorityV1Rules.ProjectId,
                    Motion: motionLayer,
                    Duration: duration > 0 ? duration.ToString() : null,
                    Framing: VideoVisualIngressV1Rules.VideoView)));
    }

    private async Task<(Guid ProductionId, string SourceHash, string Fingerprint, KitVideoMotionPreflightDto Preflight)>
        BuildPreflightAsync(Guid keyframeAttemptId, KitVideoMotionCompileDto compiled, CancellationToken ct)
    {
        var pack = await _pixels.I2vPackageAsync(keyframeAttemptId, ct);
        var file = await _pixels.ReadArtifactAsync(keyframeAttemptId, ct);
        var jpeg = file?.Bytes;
        var check = jpeg is null ? null : KitVideoArtifactRules.Validate(jpeg, "image/jpeg");
        var live = jpeg is null ? "" : KitVideoIntegrityRules.Sha256Hex(jpeg);
        var resolved = await _runway.ResolveAsync(ct);
        var pre = KitVideoMotionRules.Preflight(
            compiled.ShotCode,
            pack.Ready,
            pack.Blocked,
            pack.ImageType,
            live,
            pack.SourceArtifactHash ?? "",
            pack.SourceArtifactHash ?? "",
            pack.SourceArtifactHash ?? "",
            jpeg,
            check,
            compiled,
            resolved.RunwayConfigured);
        var fingerprint = KitVideoMotionRules.Fingerprint(live, compiled.Prompt, compiled.Model, compiled.DurationSec);
        var kf = await _keyframes.GetByIdAsync(keyframeAttemptId, ct)
            ?? throw new InvalidOperationException("Keyframe attempt không tồn tại.");
        var production = await _productions.GetVideoProductionAsync(kf.ProductionId, ct)
            ?? throw new InvalidOperationException("Production không tồn tại.");
        try
        {
            KitVideoMotionRules.EnsureGoldenLock(production.ProjectCode, compiled.ShotCode);
        }
        catch (InvalidOperationException ex)
        {
            pre = new KitVideoMotionPreflightDto(false, pre.Blocked.Append(ex.Message).ToArray(), compiled);
        }
        return (kf.ProductionId, live, fingerprint, pre);
    }

    private async Task<KitVideoVideoQaDto?> AnalyzeVideoAsync(
        KitVideoMotionRepository.TakeRow row,
        byte[] video,
        CancellationToken ct)
    {
        try
        {
            var kf = await _keyframes.GetByIdAsync(row.KeyframeAttemptId, ct);
            var jpeg = kf is null ? null : _images.Read(kf.ImagePath);
            var images = new List<(string Mime, string Base64, string Label)>();
            if (jpeg is { Length: > 32 })
                images.Add(("image/jpeg", Convert.ToBase64String(jpeg), "APPROVED KEYFRAME — identity / wardrobe / location / props must persist."));
            if (video.Length is > 32 and < 12_000_000)
                images.Add(("video/mp4", Convert.ToBase64String(video), "CANDIDATE TAKE — judge motion against the keyframe. Do not invent people."));
            if (images.Count < 2) return null;
            var raw = await _gemini.GenerateJsonWithImagesAsync(
                """
                You are a Video QA judge. Return ONLY JSON.
                Compare the take to the approved keyframe.
                P0 FAIL if: extra/missing person, face melt, wardrobe change, lost prop, location change, no action, severe deformation, temporal artifact.
                """,
                """
                Return {"overall":"PASS|FAIL|UNCERTAIN","p0Fail":[],"actionOccurred":true,"deformation":false,"unwantedAction":false}
                """,
                images,
                ct);
            using var doc = JsonDocument.Parse(ExtractJson(raw));
            var root = doc.RootElement;
            var overall = root.TryGetProperty("overall", out var ov) ? ov.GetString() ?? "FAIL" : "FAIL";
            var p0 = new List<string>();
            if (root.TryGetProperty("p0Fail", out var arr) && arr.ValueKind == JsonValueKind.Array)
                p0.AddRange(arr.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s.Length > 0));
            if (root.TryGetProperty("actionOccurred", out var act) && act.ValueKind == JsonValueKind.False)
                p0.Add("Action did not occur");
            if (root.TryGetProperty("deformation", out var def) && def.ValueKind == JsonValueKind.True)
                p0.Add("Severe deformation");
            if (root.TryGetProperty("unwantedAction", out var un) && un.ValueKind == JsonValueKind.True)
                p0.Add("Unwanted action");
            if (overall.Equals("UNCERTAIN", StringComparison.OrdinalIgnoreCase) && p0.Count == 0)
                return new KitVideoVideoQaDto("REVIEW_REQUIRED", new Dictionary<string, int> { ["visual"] = 50 }, [], ["P0 UNCERTAIN"], [], false, row.VideoSha256, row.SourceArtifactSha256);
            var status = p0.Count > 0 || overall.Equals("FAIL", StringComparison.OrdinalIgnoreCase) ? "FAIL" : "PASS";
            return new KitVideoVideoQaDto(status, new Dictionary<string, int> { ["visual"] = status == "PASS" ? 100 : 0 }, p0, [], p0, status == "PASS", row.VideoSha256, row.SourceArtifactSha256);
        }
        catch
        {
            return null;
        }
    }

    private static KitVideoVideoQaDto MergeQa(KitVideoVideoQaDto tech, KitVideoVideoQaDto? visual)
    {
        if (visual is null)
            return tech with { Status = tech.Status == "PASS" ? "REVIEW_REQUIRED" : tech.Status, AllowApprove = false, Warnings = tech.Warnings.Append("Visual Video QA not attached — not auto APPROVED_TAKE.").ToArray() };
        var p0 = tech.P0Fail.Concat(visual.P0Fail).ToArray();
        var status = p0.Length > 0 ? "FAIL" : visual.Status == "REVIEW_REQUIRED" ? "REVIEW_REQUIRED" : tech.Status == "PASS" && visual.Status == "PASS" ? "PASS" : "FAIL";
        var scores = new Dictionary<string, int>(tech.Scores);
        foreach (var kv in visual.Scores) scores[kv.Key] = kv.Value;
        return new KitVideoVideoQaDto(status, scores, p0, visual.Warnings, p0, status == "PASS", tech.VideoHash, tech.SourceArtifactHash);
    }

    private static void Advance(KitVideoMotionRepository.TakeRow row, string to)
    {
        if (!KitVideoMotionRules.CanAdvance(row.Status, to))
            throw new InvalidOperationException($"NOT_READY: illegal take jump {row.Status} → {to}.");
        row.Status = to;
    }

    private async Task<KitVideoMotionTakeDto> ToDto(
        KitVideoMotionRepository.TakeRow row,
        CancellationToken ct) =>
        await ToDto(row, null, ct);

    private async Task<KitVideoMotionTakeDto> ToDto(
        KitVideoMotionRepository.TakeRow row,
        KitVideoMotionPreflightDto? preflight,
        CancellationToken ct)
    {
        KitVideoVideoQaDto? qa = null;
        if (!string.IsNullOrWhiteSpace(row.QaJson) && row.QaJson is not "{}")
            qa = JsonSerializer.Deserialize<KitVideoVideoQaDto>(row.QaJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var diagnose = "";
        if (!string.IsNullOrWhiteSpace(row.MetadataJson) && row.MetadataJson is not "{}")
        {
            try
            {
                using var doc = JsonDocument.Parse(row.MetadataJson);
                if (doc.RootElement.TryGetProperty("diagnose", out var d))
                    diagnose = d.GetString() ?? "";
            }
            catch (JsonException) { /* ignore */ }
        }
        if (string.IsNullOrWhiteSpace(diagnose) && row.Status is "FAILED" or "DIAGNOSE")
            diagnose = KitVideoMotionRules.Diagnose(row.FailureCode, row.FailureClass);
        _ = ct;
        return new KitVideoMotionTakeDto(
            row.Id, row.ProductionId, row.ShotCode, row.KeyframeAttemptId, row.AttemptNo, row.Status,
            row.Model, row.DurationSec, row.MotionPrompt, row.Fingerprint, row.SourceArtifactSha256,
            string.IsNullOrWhiteSpace(row.VideoSha256) ? null : row.VideoSha256,
            string.IsNullOrWhiteSpace(row.RunwayTaskId) ? null : row.RunwayTaskId,
            row.OutputUrl, row.VideoPath, row.FailureClass, row.FailureCode, row.RetryReason, row.CreditState,
            preflight, qa, diagnose,
            row.Status is not ("READY" or "BLOCKED"),
            row.Status is "VIDEO_READY" or "READY_FOR_DIRECTOR" or "APPROVED_TAKE",
            !string.IsNullOrWhiteSpace(row.RunwayTaskId),
            row.CreditState is "ESTIMATED" or "PENDING" ? "UNKNOWN" : "UNKNOWN",
            row.CreditState is "CHARGED" or "ACTUAL" ? "UNKNOWN" : "UNKNOWN");
    }

    private static KitVideoMotionTakeDto Blocked(
        Guid keyframeAttemptId,
        string reason,
        KitVideoMotionCompileDto? compiled = null,
        (Guid ProductionId, string SourceHash, string Fingerprint, KitVideoMotionPreflightDto Preflight)? built = null)
    {
        var blocked = new[] { reason };
        var pre = built?.Preflight ?? new KitVideoMotionPreflightDto(false, blocked, compiled ?? new KitVideoMotionCompileDto(KitVideoMotionRules.GoldenShot, "", KitVideoMotionRules.Model, 5, KitVideoMotionRules.Ratio, KitVideoMotionRules.Version));
        return new KitVideoMotionTakeDto(
            Guid.Empty, built?.ProductionId ?? Guid.Empty, KitVideoMotionRules.GoldenShot, keyframeAttemptId, 0, "BLOCKED",
            compiled?.Model ?? KitVideoMotionRules.Model, compiled?.DurationSec ?? 5, compiled?.Prompt ?? "",
            built?.Fingerprint ?? "", built?.SourceHash ?? "",
            null, null, null, null, "RUNWAY_NOT_CALLED", "", "", "NONE",
            pre, null, reason, false, false, false);
    }

    private static string ExtractJson(string raw)
    {
        var t = (raw ?? "").Trim();
        var start = t.IndexOf('{');
        var end = t.LastIndexOf('}');
        if (start >= 0 && end > start) return t[start..(end + 1)];
        return "{}";
    }
}
