using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoPixelService : IKitVideoPixelService
{
    public const string FamixaStyle = "FAMIXA_VISUAL_STYLE_V1";

    private readonly KitVideoVisionRepository _repo;
    private readonly IVisualPromptCompiler _compiler;
    private readonly IImageGenerator _images;
    private readonly IImageVisionAnalyzer _vision;
    private readonly KitVideoArtifactStore _store;
    private readonly ContentOptions _options;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _env;
    private readonly ContentGeminiClient _gemini;

    public KitVideoPixelService(
        KitVideoVisionRepository repo,
        IVisualPromptCompiler compiler,
        IImageGenerator images,
        IImageVisionAnalyzer vision,
        KitVideoArtifactStore store,
        IOptions<ContentOptions> options,
        IConfiguration configuration,
        IHostEnvironment env,
        ContentGeminiClient gemini)
    {
        _repo = repo;
        _compiler = compiler;
        _images = images;
        _vision = vision;
        _store = store;
        _options = options.Value;
        _configuration = configuration;
        _env = env;
        _gemini = gemini;
    }

    public async Task<KitVideoProviderStatusDto> ProviderAsync(CancellationToken cancellationToken = default)
    {
        var resolved = await _gemini.ResolveConfigAsync(cancellationToken);
        var model = First(
            _options.KitVideoGeminiModel,
            _configuration["KIT_VIDEO_GEMINI_MODEL"],
            resolved.ImageModel,
            _options.ImageModel);
        return new KitVideoProviderStatusDto(
            First(_options.KitVideoImageProvider, _configuration["KIT_VIDEO_IMAGE_PROVIDER"], "gemini") ?? "gemini",
            model ?? "",
            resolved.ApiKeyConfigured,
            _vision.ProviderId,
            _images.ProviderId,
            false);
    }

    public async Task<KitVideoPixelGenerateDto> GenerateAsync(
        KitVideoPixelGenerateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.Confirmed)
            throw new InvalidOperationException("CREDIT_GATE: chưa USER CONFIRM — không gọi Gemini.");
        var shot = (request.ShotCode ?? "").Trim().ToUpperInvariant();
        if (shot.Length == 0) throw new InvalidOperationException("ShotCode bắt buộc.");
        var key = (request.IdempotencyKey ?? "").Trim();
        if (key.Length < 8)
            throw new InvalidOperationException("IdempotencyKey bắt buộc (≥ 8 ký tự).");

        var existing = await _repo.GetByIdempotencyAsync(key, cancellationToken);
        if (existing is not null)
            return await ToDto(existing, cancellationToken);

        var compiled = _compiler.Compile(request.Contract, request.ProjectStyle ?? _options.FamixaVisualStyle ?? FamixaStyle, request.Dialogue);
        if (compiled.HasDialogue)
            throw new InvalidOperationException("Prompt không chứa thoại.");

        var prior = await _repo.ListAttemptsAsync(request.ProductionId, shot, cancellationToken);
        if (prior.Count >= KitVideoVisionRules.MaxAutoAttempts)
            throw new InvalidOperationException("REVIEW_REQUIRED: MAX_AUTO_ATTEMPTS.");
        var last = prior.LastOrDefault();
        if (last is not null && last.Fingerprint == compiled.Fingerprint && !request.StrategyChanged)
            throw new InvalidOperationException("DO_NOT_BLIND_RETRY: Same generation input already attempted.");

        var refs = LoadReferences(compiled.References);
        var attemptNo = prior.Count + 1;
        var row = new KitVideoVisionRepository.AttemptRow
        {
            Id = Guid.NewGuid(),
            ProductionId = request.ProductionId,
            ShotCode = shot,
            AttemptNo = attemptNo,
            Status = "SUBMITTED",
            JobState = "SUBMITTED",
            Fingerprint = compiled.Fingerprint,
            IdempotencyKey = key,
            Provider = _images.ProviderId,
            QaJson = "{}",
            RepairJson = "{}",
            VisionJson = "{}",
            ArtifactJson = "{}",
            MetadataJson = JsonSerializer.Serialize(new
            {
                compiled.Prompt,
                compiled.Sections,
                refs = compiled.References.Select(r => r.Path),
                style = request.ProjectStyle ?? FamixaStyle,
            }),
        };

        var gen = await _images.GenerateAsync(
            new KitVideoImageGenerationRequest(
                compiled.Prompt,
                refs,
                "16:9",
                KitVideoArtifactRules.TargetWidth,
                KitVideoArtifactRules.TargetHeight),
            cancellationToken);

        row.Model = gen.Model;
        row.GenerationId = gen.GenerationId ?? "";
        row.CostUnknown = gen.CostUnknown;
        row.Provider = gen.Provider;
        if (!gen.Ok || gen.Bytes is null)
        {
            row.Status = "FAILED";
            row.JobState = "FAILED";
            row.FailureClass = string.IsNullOrWhiteSpace(gen.FailureClass) ? "PROVIDER_FAILED" : gen.FailureClass;
            await _repo.InsertAttemptAsync(row, JsonSerializer.Serialize(compiled), cancellationToken);
            return await ToDto(row, cancellationToken);
        }

        row.JobState = "PROCESSING";
        string path;
        byte[] jpeg;
        KitVideoArtifactValidation check;
        try
        {
            (path, jpeg, check) = _store.Persist(request.ProductionId, shot, attemptNo, gen.Bytes);
        }
        catch (Exception)
        {
            row.Status = "FAILED";
            row.JobState = "FAILED";
            row.FailureClass = "ARTIFACT_FAILED";
            await _repo.InsertAttemptAsync(row, JsonSerializer.Serialize(compiled), cancellationToken);
            return await ToDto(row, cancellationToken);
        }

        row.ImagePath = path;
        row.ArtifactSha256 = KitVideoIntegrityRules.Sha256Hex(jpeg);
        row.ArtifactJson = JsonSerializer.Serialize(check);
        if (!check.Ok)
        {
            row.Status = "FAILED";
            row.JobState = "FAILED";
            row.FailureClass = "ARTIFACT_FAILED";
            await _repo.InsertAttemptAsync(row, JsonSerializer.Serialize(compiled), cancellationToken);
            return await ToDto(row, cancellationToken);
        }

        row.Status = "VISION_PROCESSING";
        row.JobState = "VISION_PROCESSING";
        try
        {
            await _repo.InsertAttemptAsync(row, JsonSerializer.Serialize(compiled), cancellationToken);
            row.PersistStatus = "OK";
        }
        catch (Exception ex)
        {
            row.Status = "PERSISTENCE_FAILED";
            row.JobState = "PERSISTENCE_FAILED";
            row.PersistStatus = "PERSISTENCE_FAILED";
            row.FailureClass = "PERSISTENCE_FAILED";
            row.MetadataJson = JsonSerializer.Serialize(new { persistError = ex.Message });
            return await ToDto(row, cancellationToken);
        }

        var previous = prior.LastOrDefault(a => a.Status == "APPROVED");
        var previousBytes = previous is null ? null : _store.Read(previous.ImagePath);
        KitVideoVisionAnalysisResult analysis;
        try
        {
            analysis = await _vision.AnalyzeAsync(
                new KitVideoVisionAnalysisRequest(jpeg, "image/jpeg", request.Contract, refs, previousBytes),
                cancellationToken);
        }
        catch (Exception ex)
        {
            row.Status = "FAILED";
            row.JobState = "FAILED";
            row.FailureClass = "VISION_FAILED";
            row.MetadataJson = JsonSerializer.Serialize(new { visionError = ex.Message });
            await _repo.UpdateIntegrityAsync(row, cancellationToken);
            return await ToDto(row, cancellationToken);
        }

        ApplyVision(row, request.Contract, analysis, jpeg);
        try
        {
            await _repo.UpdateIntegrityAsync(row, cancellationToken);
            row.PersistStatus = "OK";
        }
        catch (Exception ex)
        {
            row.Status = "PERSISTENCE_FAILED";
            row.JobState = "PERSISTENCE_FAILED";
            row.PersistStatus = "PERSISTENCE_FAILED";
            row.FailureClass = "PERSISTENCE_FAILED";
            row.VisionJson = AsJsonb(analysis.RawJson);
            row.MetadataJson = JsonSerializer.Serialize(new { persistError = ex.Message });
            await _repo.UpdateIntegrityAsync(row, cancellationToken);
        }
        await _repo.UpsertContractAsync(
            request.ProductionId, shot, request.Contract.GetRawText(), JsonSerializer.Serialize(compiled), compiled.Fingerprint, cancellationToken);
        return await ToDto(row, cancellationToken);
    }

    public async Task<KitVideoPixelGenerateDto> AnalyzeBytesAsync(
        KitVideoPixelAnalyzeRequest request,
        CancellationToken cancellationToken = default)
    {
        var bytes = Convert.FromBase64String(request.ImageBase64);
        byte[] jpeg;
        try
        {
            jpeg = KitVideoArtifactStore.NormalizeJpeg(bytes);
        }
        catch (Exception)
        {
            return new KitVideoPixelGenerateDto(
                Guid.Empty, request.ProductionId, request.ShotCode, 0, "FAILED", "FAILED", "",
                null, null, null, null, _vision.ProviderId, "", true, "ARTIFACT_FAILED", null, false);
        }
        var check = KitVideoArtifactRules.Validate(jpeg, "image/jpeg");
        if (!check.Ok)
        {
            return new KitVideoPixelGenerateDto(
                Guid.Empty, request.ProductionId, request.ShotCode, 0, "FAILED", "FAILED", "",
                null, null, null, null, _vision.ProviderId, "", true, "ARTIFACT_FAILED", null, false);
        }
        var compiled = _compiler.Compile(request.Contract, FamixaStyle, null);
        var refs = LoadReferences(compiled.References);
        var analysis = await _vision.AnalyzeAsync(
            new KitVideoVisionAnalysisRequest(jpeg, "image/jpeg", request.Contract, refs, null),
            cancellationToken);
        var hash = KitVideoIntegrityRules.Sha256Hex(jpeg);
        var qa = AttachHash(KitVideoPixelVisionRules.Gate(request.Contract, analysis), hash, analysis);
        var status = qa.Status == "PASS" ? "VISION_PASS" : qa.Status == "REVIEW_REQUIRED" ? "REVIEW_REQUIRED" : "VISION_FAIL";
        var pack = KitVideoIntegrityRules.Gate(
            request.ShotCode, Guid.Empty.ToString(), status, qa.Status, qa.P0Fail, jpeg, hash, hash,
            qa.Status == "PASS" ? hash : "", "", qa.ImageType ?? analysis.ImageType);
        return new KitVideoPixelGenerateDto(
            Guid.Empty, request.ProductionId, request.ShotCode, 0,
            status, status,
            compiled.Fingerprint, qa,
            qa.Status is "FAIL" or "REVIEW_REQUIRED" ? KitVideoVisionRules.Diagnose(qa) : null,
            pack, null, _vision.ProviderId, "", true,
            qa.Status == "FAIL" ? "VISION_FAILED" : qa.Status == "REVIEW_REQUIRED" ? "VISION_UNCERTAIN" : "",
            analysis.RawJson, false, hash, qa.ImageType, "OK");
    }

    public async Task<KitVideoPixelGenerateDto> RevalidateAsync(
        KitVideoPixelRevalidateRequest request,
        CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetByIdAsync(request.AttemptId, cancellationToken)
            ?? throw new InvalidOperationException("Attempt không tồn tại.");
        var bytes = _store.Read(row.ImagePath);
        if (bytes is null || bytes.Length < 32)
            throw new InvalidOperationException("ARTIFACT_FAILED: không còn artifact để revalidate — không generate lại tự động.");
        var live = KitVideoIntegrityRules.Sha256Hex(bytes);
        if (!string.IsNullOrWhiteSpace(row.ArtifactSha256) && row.ArtifactSha256 != live)
        {
            row.Status = "NOT_READY";
            row.JobState = "NOT_READY";
            row.FailureClass = "ARTIFACT_HASH_MISMATCH";
            row.ApprovedArtifactSha256 = "";
            await _repo.UpdateIntegrityAsync(row, cancellationToken);
            return await ToDto(row, cancellationToken);
        }
        row.ArtifactSha256 = live;
        var compiled = _compiler.Compile(request.Contract, FamixaStyle, null);
        var refs = LoadReferences(compiled.References);
        var analysis = await _vision.AnalyzeAsync(
            new KitVideoVisionAnalysisRequest(bytes, "image/jpeg", request.Contract, refs, null),
            cancellationToken);
        ApplyVision(row, request.Contract, analysis, bytes);
        if (row.Status != "VISION_PASS")
            row.ApprovedArtifactSha256 = "";
        try
        {
            await _repo.UpdateIntegrityAsync(row, cancellationToken);
            row.PersistStatus = "OK";
        }
        catch (Exception ex)
        {
            row.Status = "PERSISTENCE_FAILED";
            row.JobState = "PERSISTENCE_FAILED";
            row.PersistStatus = "PERSISTENCE_FAILED";
            row.FailureClass = "PERSISTENCE_FAILED";
            row.MetadataJson = JsonSerializer.Serialize(new { persistError = ex.Message });
            row.VisionJson = AsJsonb(analysis.RawJson);
            await _repo.UpdateIntegrityAsync(row, cancellationToken);
        }
        return await ToDto(row, cancellationToken);
    }

    public async Task<KitVideoPixelGenerateDto> DecideAsync(
        KitVideoDirectorDecideRequest request,
        CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetByIdAsync(request.AttemptId, cancellationToken)
            ?? throw new InvalidOperationException("Attempt không tồn tại.");
        if (string.Equals(request.Decision, "APPROVE", StringComparison.OrdinalIgnoreCase))
        {
            var pack = await BuildI2vAsync(row, requireApproved: false, cancellationToken);
            if (pack.Blocked.Any(b => b.Contains("hash", StringComparison.OrdinalIgnoreCase)
                || b.Contains("Artifact", StringComparison.OrdinalIgnoreCase)
                || b.Contains("QA", StringComparison.OrdinalIgnoreCase)
                || b.Contains("P0", StringComparison.OrdinalIgnoreCase)
                || b.Contains("Image type", StringComparison.OrdinalIgnoreCase)
                || b.Contains("Vision", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("NOT_READY: " + string.Join(" | ", pack.Blocked.Where(b => !b.Contains("Director", StringComparison.OrdinalIgnoreCase))));
            }
            row.Status = "APPROVED";
            row.JobState = "APPROVED";
            row.ApprovedArtifactSha256 = row.ArtifactSha256;
            await _repo.UpdateIntegrityAsync(row, cancellationToken);
        }
        else
        {
            row.Status = "REJECTED";
            row.JobState = "REJECTED";
            row.ApprovedArtifactSha256 = "";
            if (!string.IsNullOrWhiteSpace(row.QaJson) && row.QaJson is not "{}")
            {
                var qa = JsonSerializer.Deserialize<KitVideoVisionQaDto>(row.QaJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (qa is not null)
                    row.QaJson = JsonSerializer.Serialize(qa with { AllowI2v = false, CanBeReference = false });
            }
            await _repo.UpdateIntegrityAsync(row, cancellationToken);
        }
        return await ToDto(row, cancellationToken);
    }

    public async Task<KitVideoI2vReadyPackageDto> I2vPackageAsync(Guid attemptId, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetByIdAsync(attemptId, cancellationToken)
            ?? throw new InvalidOperationException("Attempt không tồn tại.");
        return await BuildI2vAsync(row, requireApproved: true, cancellationToken);
    }

    public async Task<(byte[] Bytes, string Mime)?> ReadArtifactAsync(Guid attemptId, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetByIdAsync(attemptId, cancellationToken);
        if (row is null) return null;
        var bytes = _store.Read(row.ImagePath);
        if (bytes is null || bytes.Length == 0) return null;
        return (bytes, "image/jpeg");
    }

    private void ApplyVision(KitVideoVisionRepository.AttemptRow row, JsonElement contract, KitVideoVisionAnalysisResult analysis, byte[] jpeg)
    {
        var hash = KitVideoIntegrityRules.Sha256Hex(jpeg);
        var qa = AttachHash(KitVideoPixelVisionRules.Gate(contract, analysis), hash, analysis);
        var repair = qa.Status is "FAIL" or "REVIEW_REQUIRED" ? KitVideoVisionRules.Diagnose(qa) : null;
        row.QaJson = JsonSerializer.Serialize(qa);
        row.RepairJson = repair is null ? "{}" : JsonSerializer.Serialize(repair);
        row.VisionJson = AsJsonb(analysis.RawJson);
        row.ArtifactSha256 = hash;
        row.QaArtifactSha256 = hash;
        row.ImageType = qa.ImageType ?? analysis.ImageType;
        row.FailureClass = qa.Status == "FAIL" ? (qa.P0Fail.Any(x => x.StartsWith("IMAGE_TYPE", StringComparison.Ordinal)) ? "IMAGE_TYPE_FAIL" : "VISION_FAILED")
            : analysis.Uncertain ? "VISION_UNCERTAIN" : "";
        row.Status = qa.Status == "PASS" ? "VISION_PASS" : qa.Status == "REVIEW_REQUIRED" ? "REVIEW_REQUIRED" : "VISION_FAIL";
        row.JobState = row.Status;
        row.PersistStatus = "OK";
        if (row.Status != "VISION_PASS")
            row.ApprovedArtifactSha256 = "";
    }

    private static KitVideoVisionQaDto AttachHash(KitVideoVisionQaDto qa, string hash, KitVideoVisionAnalysisResult analysis) =>
        qa with
        {
            ArtifactHash = hash,
            ImageType = qa.ImageType ?? analysis.ImageType,
            AllowI2v = false,
            CanBeReference = qa.Status == "PASS" && (qa.ImageType ?? analysis.ImageType) == KitVideoIntegrityRules.ProductionStill,
        };

    private async Task<KitVideoI2vReadyPackageDto> BuildI2vAsync(
        KitVideoVisionRepository.AttemptRow row,
        bool requireApproved,
        CancellationToken ct)
    {
        _ = ct;
        var bytes = _store.Read(row.ImagePath);
        var live = bytes is { Length: > 32 } ? KitVideoIntegrityRules.Sha256Hex(bytes) : "";
        KitVideoVisionQaDto? qa = null;
        if (!string.IsNullOrWhiteSpace(row.QaJson) && row.QaJson is not "{}")
            qa = JsonSerializer.Deserialize<KitVideoVisionQaDto>(row.QaJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var status = requireApproved ? row.Status : row.Status == "APPROVED" ? "APPROVED" : "VISION_PASS";
        return KitVideoIntegrityRules.Gate(
            row.ShotCode,
            row.Id.ToString(),
            requireApproved ? row.Status : status,
            qa?.Status,
            qa?.P0Fail,
            bytes,
            live,
            row.ArtifactSha256,
            row.QaArtifactSha256,
            requireApproved ? row.ApprovedArtifactSha256 : live,
            string.IsNullOrWhiteSpace(row.ImageType) ? qa?.ImageType ?? "" : row.ImageType);
    }

    private async Task<KitVideoPixelGenerateDto> ToDto(KitVideoVisionRepository.AttemptRow row, CancellationToken ct)
    {
        KitVideoVisionQaDto? qa = null;
        if (!string.IsNullOrWhiteSpace(row.QaJson) && row.QaJson is not "{}")
            qa = JsonSerializer.Deserialize<KitVideoVisionQaDto>(row.QaJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        KitVideoRepairDto? repair = null;
        if (!string.IsNullOrWhiteSpace(row.RepairJson) && row.RepairJson is not "{}")
            repair = JsonSerializer.Deserialize<KitVideoRepairDto>(row.RepairJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var pack = await BuildI2vAsync(row, requireApproved: true, ct);
        return new KitVideoPixelGenerateDto(
            row.Id, row.ProductionId, row.ShotCode, row.AttemptNo, row.Status, row.JobState, row.Fingerprint,
            qa, repair, pack, row.ImagePath, row.Provider, row.Model, row.CostUnknown, row.FailureClass,
            row.VisionJson, false, row.ArtifactSha256, row.ImageType, row.PersistStatus);
    }

    private List<KitVideoPromptRefBytes> LoadReferences(IReadOnlyList<KitVideoPromptRefDto> refs)
    {
        var loaded = new List<KitVideoPromptRefBytes>();
        foreach (var r in refs)
        {
            var file = ResolveRefPath(r.Path, r.CharacterId);
            if (file is null || !File.Exists(file)) continue;
            var bytes = File.ReadAllBytes(file);
            var mime = KitVideoArtifactRules.DetectMime(bytes) ?? "image/png";
            loaded.Add(new KitVideoPromptRefBytes(r.Role, mime, bytes, $"{r.CharacterId} {r.Version} {r.Era}"));
        }
        return loaded.Take(4).ToList();
    }

    private string? ResolveRefPath(string? path, string? code)
    {
        var names = new List<string>();
        if (!string.IsNullOrWhiteSpace(path))
        {
            names.Add(path.Replace('/', Path.DirectorySeparatorChar).TrimStart('~', '/', '\\'));
            names.Add(Path.GetFileName(path));
        }
        if (!string.IsNullOrWhiteSpace(code))
        {
            names.Add($"{code}-minh-master.png");
            names.Add($"{code}-linh-master.png");
            names.Add($"{code}-nam-master.png");
            names.Add($"{code}-FRONT.png");
        }
        var roots = new List<string>();
        if (!string.IsNullOrWhiteSpace(_options.KitVideoRefRoot)) roots.Add(_options.KitVideoRefRoot);
        roots.Add(Path.Combine(_env.ContentRootPath, "wwwroot", "content", "famixa", "canon"));
        roots.Add(Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "..", "client", "admin", "public", "content", "famixa", "canon")));
        roots.Add(Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "..", "..", "client", "admin", "public", "content", "famixa", "canon")));
        foreach (var root in roots)
        {
            foreach (var name in names.Where(n => n.Length > 0).Distinct())
            {
                var full = Path.IsPathRooted(name) ? name : Path.Combine(root, Path.GetFileName(name));
                if (File.Exists(full)) return full;
                var nested = Path.Combine(root, name);
                if (File.Exists(nested)) return nested;
            }
        }
        return null;
    }

    private static string? First(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static string AsJsonb(string? raw)
    {
        var t = (raw ?? "").Trim();
        if (t.Length == 0) return "{}";
        try
        {
            using var _ = JsonDocument.Parse(t);
            return t;
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(new { raw = t });
        }
    }
}
