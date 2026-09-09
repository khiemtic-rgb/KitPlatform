using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoIdentityTestService : IKitVideoIdentityTestService
{
    private readonly KitVideoIdentityTestRepository _repo;
    private readonly KitVideoMasterReferenceRepository _candidates;
    private readonly IImageGenerator _images;
    private readonly IImageVisionAnalyzer _vision;
    private readonly KitVideoArtifactStore _store;

    public KitVideoIdentityTestService(
        KitVideoIdentityTestRepository repo,
        KitVideoMasterReferenceRepository candidates,
        IImageGenerator images,
        IImageVisionAnalyzer vision,
        KitVideoArtifactStore store)
    {
        _repo = repo;
        _candidates = candidates;
        _images = images;
        _vision = vision;
        _store = store;
    }

    public async Task<IReadOnlyList<KitVideoIdentityTestDto>> ListAsync(Guid? candidateId, CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListTestsAsync(candidateId, cancellationToken);
        var list = new List<KitVideoIdentityTestDto>();
        foreach (var row in rows) list.Add(await ToDto(row, cancellationToken));
        return list;
    }

    public async Task<KitVideoIdentityTestDto> CreateAsync(KitVideoIdentityTestCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (!KitVideoIdentityTestRules.Accepts(request.ProjectCode, request.CharacterId, request.EraId))
            throw new InvalidOperationException("IDENTITY_TEST: FAMIXA / CHAR-001 / ERA-01 only.");
        var cand = await _candidates.GetCandidateAsync(request.CandidateId, cancellationToken)
            ?? throw new InvalidOperationException("Candidate không tồn tại.");
        var dna = (await _candidates.MinhDnaStatusAsync(cancellationToken) ?? "DRAFT").ToUpperInvariant();
        var front = IsFrontRunner(cand.ExtraJson);
        KitVideoIdentityTestRules.EnsureCanOpen(dna, cand.CandidateCode, front, request.Designated);
        var existing = (await _repo.ListTestsAsync(cand.Id, cancellationToken)).FirstOrDefault();
        if (existing is not null) return await ToDto(existing, cancellationToken);
        var row = new KitVideoIdentityTestRepository.TestRow
        {
            Id = Guid.NewGuid(),
            CandidateId = cand.Id,
            CandidateCode = cand.CandidateCode,
            SourceSha256 = cand.Sha256,
            SourceFingerprint = cand.Fingerprint,
            Status = "PENDING",
        };
        await _repo.InsertTestAsync(row, cancellationToken);
        return await ToDto(row, cancellationToken);
    }

    public async Task<KitVideoIdentityTestDto> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await ToDto(await Require(id, cancellationToken), cancellationToken);

    public async Task<IReadOnlyList<KitVideoIdentityTestArtifactDto>> ListArtifactsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await Require(id, cancellationToken);
        var arts = await _repo.ListArtifactsAsync(id, cancellationToken);
        return arts.Select(ToArtifact).ToList();
    }

    public async Task<KitVideoIdentityTestDto> RunAsync(Guid id, KitVideoIdentityTestRunRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.Confirmed) throw new InvalidOperationException("CREDIT_GATE: chưa xác nhận — không gọi generator.");
        var test = await Require(id, cancellationToken);
        var dna = (await _candidates.MinhDnaStatusAsync(cancellationToken) ?? "DRAFT").ToUpperInvariant();
        if (!KitVideoIdentityTestRules.DnaAllows(dna))
            throw new InvalidOperationException("IDENTITY_TEST_BLOCKED: Visual DNA phải APPROVED.");
        var variants = string.IsNullOrWhiteSpace(request.Variant)
            ? KitVideoIdentityTestRules.RequiredVariants
            : new[] { request.Variant.Trim().ToUpperInvariant() };
        await _repo.SetStatusAsync(test.Id, "RUNNING", cancellationToken);
        var source = await _candidates.GetCandidateAsync(test.CandidateId, cancellationToken);
        var sourceBytes = source is null ? null : _store.Read(source.ArtifactPath);
        foreach (var variant in variants)
        {
            if (!KitVideoIdentityTestRules.RequiredVariants.Contains(variant))
                throw new InvalidOperationException($"IDENTITY_TEST: variant {variant} không hợp lệ.");
            var type = KitVideoIdentityTestRules.TestTypeOf(variant);
            var fp = KitVideoIdentityTestRules.Fingerprint(test.CandidateId.ToString(), type, variant, test.SourceSha256, test.DnaVersion);
            var arts = await _repo.ListArtifactsAsync(test.Id, cancellationToken);
            var prior = arts.Where(a => a.TestVariant == variant).OrderByDescending(a => a.Attempt).FirstOrDefault();
            if (!request.Regenerate && prior is { } ok && ok.Fingerprint == fp && ok.Sha256.Length > 8 && _store.Read(ok.ArtifactPath) is { Length: > 32 })
                continue;
            if (!request.Regenerate && prior is { } bad && bad.Fingerprint == fp && (string.IsNullOrWhiteSpace(bad.Sha256) || _store.Read(bad.ArtifactPath) is null))
                throw new InvalidOperationException("DO_NOT_BLIND_RETRY: artifact cũ hỏng — ghi chỗ sai rồi regenerate.");
            var attempt = await _repo.NextAttemptAsync(test.Id, variant, cancellationToken);
            var prompt = KitVideoIdentityTestRules.CompilePrompt(variant, test.CandidateCode, test.SourceSha256);
            var refs = new List<KitVideoPromptRefBytes>();
            if (sourceBytes is { Length: > 32 })
                refs.Add(new KitVideoPromptRefBytes("IDENTITY", "image/jpeg", sourceBytes, test.CandidateCode));
            var gen = await _images.GenerateAsync(new KitVideoImageGenerationRequest(prompt, refs, "3:4", 768, 1024), cancellationToken);
            if (!gen.Ok || gen.Bytes is null)
            {
                await _repo.InsertArtifactAsync(new KitVideoIdentityTestRepository.ArtifactRow
                {
                    Id = Guid.NewGuid(),
                    TestId = test.Id,
                    TestType = type,
                    TestVariant = variant,
                    Attempt = attempt,
                    ImageType = KitVideoIdentityTestRules.Kind,
                    QaStatus = "GENERATION_FAILED",
                    QaJson = JsonSerializer.Serialize(new { p0 = new[] { "GENERATION_FAILED" } }),
                    Fingerprint = fp,
                    Provider = gen.Provider,
                    Model = gen.Model,
                }, cancellationToken);
                continue;
            }
            string path;
            byte[] jpeg;
            KitVideoArtifactValidation check;
            try
            {
                (path, jpeg, check) = _store.PersistIdentity("CHAR-001", "ERA-01", test.Id, variant, attempt, gen.Bytes);
            }
            catch (Exception)
            {
                await _repo.InsertArtifactAsync(new KitVideoIdentityTestRepository.ArtifactRow
                {
                    Id = Guid.NewGuid(),
                    TestId = test.Id,
                    TestType = type,
                    TestVariant = variant,
                    Attempt = attempt,
                    ImageType = KitVideoIdentityTestRules.Kind,
                    QaStatus = "ARTIFACT_FAILED",
                    QaJson = JsonSerializer.Serialize(new { p0 = new[] { "ARTIFACT_FAILED" } }),
                    Fingerprint = fp,
                    Provider = gen.Provider,
                    Model = gen.Model,
                }, cancellationToken);
                continue;
            }
            var sha = KitVideoIntegrityRules.Sha256Hex(jpeg);
            var row = new KitVideoIdentityTestRepository.ArtifactRow
            {
                Id = Guid.NewGuid(),
                TestId = test.Id,
                TestType = type,
                TestVariant = variant,
                Attempt = attempt,
                ArtifactPath = path,
                Sha256 = sha,
                Fingerprint = fp,
                ImageType = KitVideoIdentityTestRules.Kind,
                QaStatus = check.Ok ? "PENDING" : "ARTIFACT_FAILED",
                QaJson = "{}",
                Provider = gen.Provider,
                Model = gen.Model,
            };
            KitVideoIdentityTestRules.EnsureNotCanonKind(row.ImageType);
            await _repo.InsertArtifactAsync(row, cancellationToken);
            if (check.Ok) await RunVision(row, jpeg, variant, cancellationToken);
        }
        return await GetAsync(id, cancellationToken);
    }

    public async Task<KitVideoIdentityTestDto> AnalyzeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var test = await Require(id, cancellationToken);
        var arts = await _repo.ListArtifactsAsync(id, cancellationToken);
        foreach (var art in arts)
        {
            var bytes = _store.Read(art.ArtifactPath);
            if (bytes is null) continue;
            await RunVision(art, bytes, art.TestVariant, cancellationToken);
        }
        return await ToDto(test, cancellationToken);
    }

    public async Task<KitVideoIdentityTestDto> CompareAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var test = await Require(id, cancellationToken);
        var arts = LatestByVariant(await _repo.ListArtifactsAsync(id, cancellationToken));
        var viewFail = KitVideoIdentityTestRules.ViewVariants.Any(v => arts.TryGetValue(v, out var a) && a.QaStatus is "FAIL" or "VISION_FAIL");
        var emoFail = KitVideoIdentityTestRules.EmotionVariants.Any(v => arts.TryGetValue(v, out var a) && a.QaStatus is "FAIL" or "VISION_FAIL");
        var anyP0 = arts.Values.Any(a => a.QaStatus is "FAIL" or "VISION_FAIL" or "ARTIFACT_FAILED");
        var complete = KitVideoIdentityTestRules.Completeness(arts.Where(kv => kv.Value.Sha256.Length > 8 && kv.Value.QaStatus is "PASS" or "FAIL" or "VISION_FAIL").Select(kv => kv.Key));
        var status = !complete ? "INCOMPLETE" : anyP0 ? "FAIL" : "PASS";
        await _repo.UpsertResultAsync(
            id,
            viewFail ? "FAIL" : complete ? "PASS" : "PENDING",
            emoFail ? "FAIL" : complete ? "PASS" : "PENDING",
            anyP0 ? "FAIL" : complete ? "PASS" : "PENDING",
            anyP0 ? 40 : 80,
            JsonSerializer.Serialize(arts.Values.Select(a => a.QaStatus).ToArray()),
            "[]",
            cancellationToken);
        await _repo.SetStatusAsync(id, status, cancellationToken);
        test.Status = status;
        return await ToDto(test, cancellationToken);
    }

    public async Task<KitVideoIdentityTestDto> DecideAsync(Guid id, KitVideoIdentityTestDecisionRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var test = await Require(id, cancellationToken);
        var decision = (request.Decision ?? "").Trim().ToUpperInvariant();
        if (!KitVideoIdentityTestRules.IsDirectorDecision(decision))
            throw new InvalidOperationException("IDENTITY_TEST: quyết định Director không hợp lệ.");
        await _repo.InsertDecisionAsync(id, test.CandidateId, decision, request.Reason ?? "", actor, cancellationToken);
        if (decision is "PASS" or "CONDITIONAL" or "FAIL" or "REJECT")
            await _repo.SetStatusAsync(id, decision == "REJECT" ? "FAIL" : decision, cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public Task<KitVideoIdentityTestDto> SummaryAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync(id, cancellationToken);

    public async Task<(byte[] Bytes, string Mime)?> ReadArtifactImageAsync(Guid artifactId, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetArtifactAsync(artifactId, cancellationToken);
        var bytes = row is null ? null : _store.Read(row.ArtifactPath);
        return bytes is { Length: > 32 } ? (bytes, "image/jpeg") : null;
    }

    private async Task RunVision(KitVideoIdentityTestRepository.ArtifactRow row, byte[] jpeg, string variant, CancellationToken ct)
    {
        try
        {
            var vision = await _vision.AnalyzeAsync(
                new KitVideoVisionAnalysisRequest(jpeg, "image/jpeg", KitVideoMasterCreationRules.VisionContract(), [], null),
                ct);
            using var raw = JsonDocument.Parse(string.IsNullOrWhiteSpace(vision.RawJson) ? "{}" : ExtractJson(vision.RawJson));
            var imageType = raw.RootElement.TryGetProperty("imageType", out var it) ? it.GetString() ?? "UNKNOWN" : "UNKNOWN";
            var sheet = KitVideoMasterCreationRules.IsSheet(imageType);
            var minh = vision.DetectedCharacterIds.Contains("CHAR-001", StringComparer.OrdinalIgnoreCase) || vision.Overall == "PASS";
            var p0 = KitVideoIdentityTestRules.EvaluateP0(
                true, row.Sha256, row.Sha256, sheet, vision.DetectedCharacters, minh,
                vision.Overall != "FAIL", vision.Overall != "FAIL", true, variant, variant);
            var qaStatus = p0.Count == 0 && vision.Overall == "PASS" ? "PASS" : sheet ? "FAIL" : "VISION_FAIL";
            var qa = JsonSerializer.Serialize(new
            {
                p0,
                status = qaStatus,
                vision = vision.Overall,
                imageType,
                sha256 = row.Sha256,
                kind = KitVideoIdentityTestRules.Kind,
                usedBeautyScore = false,
            });
            var storedType = sheet ? "CHARACTER_SHEET" : KitVideoIdentityTestRules.Kind;
            KitVideoIdentityTestRules.EnsureNotCanonKind(storedType);
            await _repo.UpdateArtifactQaAsync(row.Id, qaStatus, qa, storedType, ct);
        }
        catch
        {
            await _repo.UpdateArtifactQaAsync(row.Id, "VISION_FAIL", JsonSerializer.Serialize(new { p0 = new[] { "VISION_FAILED" } }), null, ct);
        }
    }

    private async Task<KitVideoIdentityTestRepository.TestRow> Require(Guid id, CancellationToken ct) =>
        await _repo.GetTestAsync(id, ct) ?? throw new InvalidOperationException("Identity Test không tồn tại.");

    private async Task<KitVideoIdentityTestDto> ToDto(KitVideoIdentityTestRepository.TestRow row, CancellationToken ct)
    {
        var arts = await _repo.ListArtifactsAsync(row.Id, ct);
        var latest = LatestByVariant(arts);
        var complete = KitVideoIdentityTestRules.Completeness(
            latest.Where(kv => kv.Value.Sha256.Length > 8 && !string.IsNullOrWhiteSpace(kv.Value.Fingerprint) && kv.Value.QaStatus is not "PENDING" and not "GENERATION_FAILED")
                .Select(kv => kv.Key));
        var result = await _repo.GetResultAsync(row.Id, ct);
        var decision = await _repo.LatestDecisionAsync(row.Id, ct);
        return new KitVideoIdentityTestDto(
            row.Id, row.ProjectCode, row.CharacterId, row.EraId, row.CandidateId, row.CandidateCode,
            row.SourceSha256, row.Status, row.DocumentId, latest.Count, KitVideoIdentityTestRules.RequiredVariants.Length,
            complete, false, false, latest.Values.Select(ToArtifact).ToList(),
            result?.View, result?.Emotion, result?.Identity, decision, null);
    }

    private static Dictionary<string, KitVideoIdentityTestRepository.ArtifactRow> LatestByVariant(
        IReadOnlyList<KitVideoIdentityTestRepository.ArtifactRow> arts) =>
        arts.GroupBy(a => a.TestVariant).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Attempt).First());

    private static KitVideoIdentityTestArtifactDto ToArtifact(KitVideoIdentityTestRepository.ArtifactRow row)
    {
        using var qa = JsonDocument.Parse(string.IsNullOrWhiteSpace(row.QaJson) ? "{}" : row.QaJson);
        return new KitVideoIdentityTestArtifactDto(
            row.Id, row.TestId, row.TestType, row.TestVariant, row.Attempt, row.ArtifactPath, row.Sha256,
            row.Fingerprint, row.ImageType, row.QaStatus, qa.RootElement.Clone(), row.Provider, row.Model);
    }

    private static bool IsFrontRunner(string extraJson)
    {
        try
        {
            using var extra = JsonDocument.Parse(string.IsNullOrWhiteSpace(extraJson) ? "{}" : extraJson);
            if (extra.RootElement.TryGetProperty("selection", out var sel) && sel.ValueKind == JsonValueKind.Object)
            {
                if (sel.TryGetProperty("frontRunner", out var fr) && fr.ValueKind == JsonValueKind.True) return true;
                if (sel.TryGetProperty("lifecycle", out var life) && life.GetString() == "FRONT_RUNNER") return true;
            }
        }
        catch (JsonException) { /* ignore */ }
        return false;
    }

    private static string ExtractJson(string raw)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        return start >= 0 && end > start ? raw[start..(end + 1)] : "{}";
    }
}
