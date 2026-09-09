using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoIdentityStressService : IKitVideoIdentityStressService
{
    private readonly KitVideoIdentityStressRepository _repo;
    private readonly KitVideoIdentityTestRepository _identity;
    private readonly KitVideoMasterReferenceRepository _candidates;
    private readonly IImageGenerator _images;
    private readonly IImageVisionAnalyzer _vision;
    private readonly KitVideoArtifactStore _store;

    public KitVideoIdentityStressService(
        KitVideoIdentityStressRepository repo,
        KitVideoIdentityTestRepository identity,
        KitVideoMasterReferenceRepository candidates,
        IImageGenerator images,
        IImageVisionAnalyzer vision,
        KitVideoArtifactStore store)
    {
        _repo = repo;
        _identity = identity;
        _candidates = candidates;
        _images = images;
        _vision = vision;
        _store = store;
    }

    public async Task<IReadOnlyList<KitVideoIdentityStressDto>> ListAsync(Guid? candidateId, CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListTestsAsync(candidateId, cancellationToken);
        var list = new List<KitVideoIdentityStressDto>();
        foreach (var row in rows) list.Add(await ToDto(row, cancellationToken));
        return list;
    }

    public async Task<KitVideoIdentityStressDto> CreateAsync(KitVideoIdentityStressCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (!KitVideoIdentityStressRules.Accepts(request.ProjectCode, request.CharacterId, request.EraId))
            throw new InvalidOperationException("STRESS: FAMIXA / CHAR-001 / ERA-01 only.");
        var cand = await _candidates.GetCandidateAsync(request.CandidateId, cancellationToken)
            ?? throw new InvalidOperationException("Candidate không tồn tại.");
        if (!string.Equals(cand.CharacterId, "CHAR-001", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(cand.EraId, "ERA-01", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("STRESS: CHAR-001 / ERA-01 only.");
        var dna = (await _candidates.MinhDnaStatusAsync(cancellationToken) ?? "DRAFT").ToUpperInvariant();
        var life = LifecycleOf(cand.ExtraJson);
        var eligible = IsEligible(cand.ExtraJson, life, cand.QaStatus);
        var identityComplete = await _identity.HasCompleteAsync(cand.Id, cancellationToken);
        var identityId = await _identity.LatestCompleteIdAsync(cand.Id, cancellationToken);
        var bytes = _store.Read(cand.ArtifactPath);
        var artifactOk = cand.Sha256.Length > 8 && bytes is { Length: > 32 };
        var liveSha = bytes is { Length: > 32 } ? KitVideoIntegrityRules.Sha256Hex(bytes) : "";
        var locked = cand.Status.Equals("LOCKED", StringComparison.OrdinalIgnoreCase);
        if (!locked)
        {
            var asset = await _candidates.GetAssetAsync("FAMIXA", "CHAR-001", cancellationToken);
            locked = asset is not null && asset.Status.Equals("LOCKED", StringComparison.OrdinalIgnoreCase);
        }
        KitVideoIdentityStressRules.EnsureCanOpen(
            cand.CandidateCode, dna, eligible, identityComplete, artifactOk, artifactOk,
            cand.Sha256, liveSha, cand.QaStatus.Equals("PASS", StringComparison.OrdinalIgnoreCase), life, locked);
        var existing = (await _repo.ListTestsAsync(cand.Id, cancellationToken)).FirstOrDefault();
        if (existing is not null)
        {
            try
            {
                KitVideoIdentityStressRules.EnsureSourceStillValid(existing.SourceSha256, cand.Sha256, existing.SourceFingerprint, cand.Fingerprint);
                return await ToDto(existing, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                await _repo.SetStatusAsync(existing.Id, "BLOCKED", cancellationToken);
            }
        }
        var row = new KitVideoIdentityStressRepository.TestRow
        {
            Id = Guid.NewGuid(),
            CandidateId = cand.Id,
            CandidateCode = cand.CandidateCode,
            IdentityTestId = identityId,
            SourceSha256 = cand.Sha256,
            SourceFingerprint = cand.Fingerprint,
            Status = "PENDING",
            DocumentId = KitVideoIdentityStressRules.DocumentId,
        };
        await _repo.InsertTestAsync(row, cancellationToken);
        await _repo.InsertEventAsync(row.Id, "STRESS_TEST_CREATED", null, "system", cancellationToken);
        return await ToDto(row, cancellationToken);
    }

    public async Task<KitVideoIdentityStressDto> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await ToDto(await Require(id, cancellationToken), cancellationToken);

    public async Task<KitVideoIdentityStressDto> RunAsync(Guid id, KitVideoIdentityStressRunRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.Confirmed) throw new InvalidOperationException("CREDIT_GATE: chưa xác nhận — không gọi generator.");
        var test = await Require(id, cancellationToken);
        var dna = (await _candidates.MinhDnaStatusAsync(cancellationToken) ?? "DRAFT").ToUpperInvariant();
        if (!KitVideoIdentityStressRules.DnaAllows(dna))
            throw new InvalidOperationException("STRESS_TEST_BLOCKED: Visual DNA phải APPROVED.");
        var source = await _candidates.GetCandidateAsync(test.CandidateId, cancellationToken)
            ?? throw new InvalidOperationException("STRESS_TEST_BLOCKED: candidate không tồn tại.");
        KitVideoIdentityStressRules.EnsureSourceStillValid(test.SourceSha256, source.Sha256, test.SourceFingerprint, source.Fingerprint);
        var cases = string.IsNullOrWhiteSpace(request.CaseCode)
            ? KitVideoIdentityStressRules.RequiredCases
            : new[] { request.CaseCode.Trim().ToUpperInvariant() };
        if (request.Regenerate && cases.Length != 1)
            throw new InvalidOperationException("STRESS: chỉ regenerate case FAIL, không chạy lại cả 10.");
        await _repo.SetStatusAsync(test.Id, "RUNNING", cancellationToken);
        await _repo.InsertEventAsync(test.Id, "STRESS_CASE_STARTED", request.CaseCode, "system", cancellationToken);
        var sourceBytes = _store.Read(source.ArtifactPath);
        foreach (var code in cases)
        {
            if (KitVideoIdentityStressRules.CaseOf(code) is null)
                throw new InvalidOperationException($"STRESS: case {code} không hợp lệ.");
            var diagnosis = request.Regenerate ? request.Diagnosis : null;
            var fp = KitVideoIdentityStressRules.Fingerprint(test.CandidateId.ToString(), code, test.SourceFingerprint, diagnosis);
            var arts = await _repo.ListAttemptsAsync(test.Id, cancellationToken);
            var prior = arts.Where(a => a.TestCase == code).OrderByDescending(a => a.Attempt).FirstOrDefault();
            if (!request.Regenerate && prior is { } ok && ok.Fingerprint == fp && ok.Sha256.Length > 8 && _store.Read(ok.ArtifactPath) is { Length: > 32 })
                continue;
            if (prior is { } bad && bad.Fingerprint == fp && (string.IsNullOrWhiteSpace(bad.Sha256) || _store.Read(bad.ArtifactPath) is null)
                && string.IsNullOrWhiteSpace(request.Diagnosis))
                throw new InvalidOperationException("DO_NOT_BLIND_RETRY: ghi STRESS_DIAGNOSIS rồi mới repair.");
            if (request.Regenerate && string.IsNullOrWhiteSpace(request.Diagnosis))
                throw new InvalidOperationException("DO_NOT_BLIND_RETRY: repair cần diagnosis.");
            var attempt = await _repo.NextAttemptAsync(test.Id, code, cancellationToken);
            var prompt = KitVideoIdentityStressRules.CompilePrompt(code, test.CandidateCode, test.SourceSha256, diagnosis);
            var refs = new List<KitVideoPromptRefBytes>();
            if (sourceBytes is { Length: > 32 })
                refs.Add(new KitVideoPromptRefBytes("STRESS_SOURCE", "image/jpeg", sourceBytes, test.CandidateCode));
            var gen = await _images.GenerateAsync(new KitVideoImageGenerationRequest(prompt, refs, "3:4", 768, 1024), cancellationToken);
            if (!gen.Ok || gen.Bytes is null)
            {
                await _repo.InsertAttemptAsync(FailedAttempt(test.Id, code, attempt, fp, "GENERATION_FAILED", gen.Provider, gen.Model), cancellationToken);
                continue;
            }
            string path;
            byte[] jpeg;
            KitVideoArtifactValidation check;
            try
            {
                (path, jpeg, check) = _store.PersistStress("CHAR-001", "ERA-01", test.Id, code, attempt, gen.Bytes);
            }
            catch (Exception)
            {
                await _repo.InsertAttemptAsync(FailedAttempt(test.Id, code, attempt, fp, "ARTIFACT_FAILED", gen.Provider, gen.Model), cancellationToken);
                continue;
            }
            var sha = KitVideoIntegrityRules.Sha256Hex(jpeg);
            var row = new KitVideoIdentityStressRepository.AttemptRow
            {
                Id = Guid.NewGuid(),
                TestId = test.Id,
                TestGroup = KitVideoIdentityStressRules.GroupOf(code),
                TestCase = code,
                Attempt = attempt,
                ArtifactPath = path,
                Sha256 = sha,
                Fingerprint = fp,
                ImageType = KitVideoIdentityStressRules.Kind,
                QaStatus = check.Ok ? "PENDING" : "ARTIFACT_FAILED",
                QaJson = "{}",
                Provider = gen.Provider,
                Model = gen.Model,
            };
            KitVideoIdentityStressRules.EnsureNotCanonKind(row.ImageType);
            await _repo.InsertAttemptAsync(row, cancellationToken);
            if (check.Ok) await RunVision(row, jpeg, source, sourceBytes, cancellationToken);
        }
        return await RollupAsync(test, cancellationToken);
    }

    public async Task<KitVideoIdentityStressDto> RepairAsync(Guid id, KitVideoIdentityStressRepairRequest request, string actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Diagnosis))
            throw new InvalidOperationException("STRESS_DIAGNOSIS: repair cần diagnosis — không blind retry.");
        if (string.IsNullOrWhiteSpace(request.CaseCode))
            throw new InvalidOperationException("STRESS_DIAGNOSIS: cần mã case (ST-01…ST-10).");
        await DecideAsync(id, new KitVideoIdentityStressDecisionRequest("REPAIR", request.Diagnosis), actor, cancellationToken);
        return await RunAsync(id, new KitVideoIdentityStressRunRequest(request.CaseCode, request.Confirmed, true, request.Diagnosis), cancellationToken);
    }

    public async Task<KitVideoIdentityStressDto> AnalyzeAsync(Guid id, string? caseCode = null, CancellationToken cancellationToken = default)
    {
        var test = await Require(id, cancellationToken);
        var source = await _candidates.GetCandidateAsync(test.CandidateId, cancellationToken);
        var sourceBytes = source is null ? null : _store.Read(source.ArtifactPath);
        var latest = LatestByCase(await _repo.ListAttemptsAsync(id, cancellationToken));
        var ran = 0;
        foreach (var art in latest.Values)
        {
            if (!ShouldReanalyze(art, caseCode)) continue;
            var bytes = _store.Read(art.ArtifactPath);
            if (bytes is null)
                throw new InvalidOperationException($"STRESS_ANALYZE: không đọc được artifact {art.TestCase} a{art.Attempt:00}.");
            await RunVision(art, bytes, source, sourceBytes, cancellationToken);
            ran += 1;
        }
        if (ran == 0)
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(caseCode)
                    ? "STRESS_ANALYZE: không có case FAIL để chấm. Gõ ST-10 rồi bấm Phân tích lại."
                    : $"STRESS_ANALYZE: không có artifact {caseCode} để chấm.");
        return await RollupAsync(test, cancellationToken);
    }

    public async Task<KitVideoIdentityStressDto> DecideAsync(Guid id, KitVideoIdentityStressDecisionRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var test = await Require(id, cancellationToken);
        var decision = (request.Decision ?? "").Trim().ToUpperInvariant();
        if (!KitVideoIdentityStressRules.IsDirectorDecision(decision))
            throw new InvalidOperationException("STRESS: quyết định Director không hợp lệ.");
        if (decision == "PASS")
            KitVideoIdentityStressRules.EnsureDirectorApprove(test.Status);
        if (decision == "PROMOTE_TO_MASTER_REVIEW")
        {
            var dna = (await _candidates.MinhDnaStatusAsync(cancellationToken) ?? "DRAFT").ToUpperInvariant();
            var identityComplete = await _identity.HasCompleteAsync(test.CandidateId, cancellationToken);
            var cand = await _candidates.GetCandidateAsync(test.CandidateId, cancellationToken);
            var artifactOk = cand is not null && cand.Sha256.Length > 8 && _store.Read(cand.ArtifactPath) is { Length: > 32 };
            var directorPass = string.Equals(await _repo.LatestDecisionAsync(id, cancellationToken), "PASS", StringComparison.OrdinalIgnoreCase);
            KitVideoIdentityStressRules.EnsurePromote(
                identityComplete ? "COMPLETE" : "INCOMPLETE",
                test.Status,
                directorPass,
                artifactOk,
                KitVideoIdentityStressRules.DnaAllows(dna));
        }
        await _repo.InsertDecisionAsync(id, test.CandidateId, decision, request.Reason ?? "", actor, cancellationToken);
        await _repo.InsertEventAsync(id, decision == "REJECT" ? "DIRECTOR_REJECTED" : decision == "PASS" ? "DIRECTOR_APPROVED" : decision, null, actor, cancellationToken);
        if (decision is "PASS" or "REJECT")
            await _repo.SetStatusAsync(id, decision == "REJECT" ? "FAIL" : decision, cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<(byte[] Bytes, string Mime)?> ReadArtifactImageAsync(Guid artifactId, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetAttemptAsync(artifactId, cancellationToken);
        var bytes = row is null ? null : _store.Read(row.ArtifactPath);
        return bytes is { Length: > 32 } ? (bytes, "image/jpeg") : null;
    }

    private async Task<KitVideoIdentityStressDto> RollupAsync(KitVideoIdentityStressRepository.TestRow test, CancellationToken ct)
    {
        var latest = LatestByCase(await _repo.ListAttemptsAsync(test.Id, ct));
        var rows = latest.Values.Select(a =>
        {
            var qa = ParseQa(a.QaJson);
            return (Code: a.TestCase, Sha256: a.Sha256, P0: (IEnumerable<string>)qa.P0, QaStatus: a.QaStatus, IdentityScore: qa.Identity);
        }).ToList();
        var roll = KitVideoIdentityStressRules.Rollup(rows);
        var p0All = rows.SelectMany(r => r.P0).ToList();
        var status = roll.Status == "NOT_RUN" ? "PENDING" : roll.Status;
        var group = (string name) => GroupStatus(latest, name);
        var identity = p0All.Count > 0 ? "FAIL" : roll.Complete ? "PASS" : "PENDING";
        var age = latest.Values.Any(a => ParseQa(a.QaJson).P0.Contains("AGE_DRIFT")) ? "FAIL" : roll.Complete ? "PASS" : "PENDING";
        var hair = latest.Values.Any(a => ParseQa(a.QaJson).P0.Contains("HAIR_IDENTITY_BREAK")) ? "FAIL" : roll.Complete ? "PASS" : "PENDING";
        var face = latest.Values.Any(a => ParseQa(a.QaJson).P0.Any(x => x.Contains("FACE") || x.Contains("IDENTITY") || x.Contains("DEFORM")))
            ? "FAIL" : roll.Complete ? "PASS" : "PENDING";
        var avgIdentity = rows.Count == 0 ? 0 : (int)rows.Average(r => r.IdentityScore);
        await _repo.UpsertResultAsync(
            test.Id, group("CAMERA"), group("LIGHTING"), group("POSE"), group("EMOTION"), group("SCENE"),
            identity, age, hair, face, avgIdentity,
            JsonSerializer.Serialize(p0All),
            "[]",
            "[]",
            "[]",
            ct);
        await _repo.SetStatusAsync(test.Id, status, ct);
        if (roll.Complete) await _repo.InsertEventAsync(test.Id, "STRESS_TEST_COMPLETED", null, "system", ct);
        test.Status = status;
        return await ToDto(test, ct);
    }

    private async Task RunVision(
        KitVideoIdentityStressRepository.AttemptRow row,
        byte[] jpeg,
        KitVideoMasterReferenceRepository.CandidateRow? source,
        byte[]? sourceBytes,
        CancellationToken ct)
    {
        try
        {
            var refs = new List<KitVideoPromptRefBytes>();
            if (sourceBytes is { Length: > 32 })
                refs.Add(new KitVideoPromptRefBytes("IDENTITY_SOURCE", "image/jpeg", sourceBytes, source?.CandidateCode ?? "004-D"));
            var vision = await _vision.AnalyzeAsync(
                new KitVideoVisionAnalysisRequest(jpeg, "image/jpeg", KitVideoIdentityStressRules.VisionContract(row.TestCase), refs, null),
                ct);
            using var raw = JsonDocument.Parse(string.IsNullOrWhiteSpace(vision.RawJson) ? "{}" : ExtractJson(vision.RawJson));
            var imageType = raw.RootElement.TryGetProperty("imageType", out var it) ? it.GetString() ?? vision.ImageType : vision.ImageType;
            var sheet = KitVideoMasterCreationRules.IsSheet(imageType);
            var expected = row.TestCase.Equals("ST-10", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
            var (p0, provenance, identityScore) = KitVideoIdentityStressRules.EvaluateFromVision(
                new KitVideoIdentityStressRules.VisionSignals(
                    vision.Overall, vision.DetectedCharacters, expected, vision.DetectedCharacterIds,
                    vision.Requirements, imageType, sheet, row.TestCase));
            var qaStatus = p0.Count == 0 && identityScore >= KitVideoIdentityStressRules.ScoreThreshold
                ? "PASS" : sheet ? "FAIL" : p0.Count > 0 ? "FAIL" : identityScore < KitVideoIdentityStressRules.ScoreThreshold ? "FAIL" : "VISION_FAIL";
            var qa = JsonSerializer.Serialize(new
            {
                p0,
                p1 = Array.Empty<string>(),
                p2 = Array.Empty<string>(),
                status = qaStatus,
                vision = vision.Overall,
                imageType,
                sha256 = row.Sha256,
                kind = KitVideoIdentityStressRules.Kind,
                usedScoreAsOverride = false,
                identityScore,
                facialStructureScore = identityScore,
                hairScore = identityScore,
                eyeScore = identityScore,
                ageConsistencyScore = identityScore,
                styleConsistencyScore = identityScore,
                sceneComplianceScore = identityScore,
                diagnostic = new
                {
                    sourceCandidate = source?.CandidateCode ?? "",
                    sourceSha256 = source?.Sha256 ?? "",
                    sourceFingerprint = source?.Fingerprint ?? "",
                    sourcePath = source?.ArtifactPath ?? "",
                    referenceAttached = sourceBytes is { Length: > 32 },
                    visionTarget = "FULL_FRAME",
                    minhCrop = false,
                    contractPurpose = "IDENTITY_STRESS",
                    expectedCharacters = expected,
                    detectedCharacters = vision.DetectedCharacters,
                    detectedIds = vision.DetectedCharacterIds,
                    requirements = vision.Requirements.Select(r => new { r.Id, r.Status, r.Reason, r.Confidence }),
                    p0Provenance = provenance,
                    scoreMode = "FEATURE_FROM_VISION_SIGNALS",
                    collapsedCascade = KitVideoIdentityStressRules.IsCollapsedCascade(p0, identityScore, vision.Overall),
                    visionExtract = new
                    {
                        overall = vision.Overall,
                        expected = vision.ExpectedCharacters,
                        detected = vision.DetectedCharacters,
                        ids = vision.DetectedCharacterIds,
                        imageType,
                        uncertain = vision.Uncertain,
                    },
                },
            });
            var storedType = sheet ? "CHARACTER_SHEET" : KitVideoIdentityStressRules.Kind;
            KitVideoIdentityStressRules.EnsureNotCanonKind(storedType);
            await _repo.UpdateAttemptQaAsync(row.Id, qaStatus, qa, storedType, ct);
        }
        catch
        {
            await _repo.UpdateAttemptQaAsync(row.Id, "VISION_FAIL", JsonSerializer.Serialize(new { p0 = new[] { "VISION_FAILED" } }), null, ct);
        }
    }

    private async Task<KitVideoIdentityStressRepository.TestRow> Require(Guid id, CancellationToken ct) =>
        await _repo.GetTestAsync(id, ct) ?? throw new InvalidOperationException("Identity Stress Test không tồn tại.");

    private async Task<KitVideoIdentityStressDto> ToDto(KitVideoIdentityStressRepository.TestRow row, CancellationToken ct)
    {
        var arts = await _repo.ListAttemptsAsync(row.Id, ct);
        var latest = LatestByCase(arts);
        var result = await _repo.GetResultAsync(row.Id, ct);
        var decision = await _repo.LatestDecisionAsync(row.Id, ct);
        var p0 = CountJson(result?.P0);
        var p1 = CountJson(result?.P1);
        var p2 = CountJson(result?.P2);
        var scores = latest.Values.Select(a => ParseQa(a.QaJson)).ToList();
        int Avg(Func<QaParse, int> pick) => scores.Count == 0 ? 0 : (int)scores.Average(pick);
        var allow = row.Status == "PASS" && p0 == 0;
        return new KitVideoIdentityStressDto(
            row.Id, row.ProjectCode, row.CharacterId, row.EraId, row.CandidateId, row.CandidateCode,
            row.SourceSha256, row.Status, row.DocumentId, latest.Count, KitVideoIdentityStressRules.RequiredCount,
            latest.Count == KitVideoIdentityStressRules.RequiredCount, p0, p1, p2, false, false,
            latest.Values.Select(ToArtifact).ToList(),
            result?.Environment, result?.Lighting, result?.Camera, result?.Emotion, result?.Wardrobe,
            result?.Identity, result?.Age, result?.Hair, result?.Face, decision, null,
            Avg(s => s.Identity), Avg(s => s.Structure), Avg(s => s.Hair), Avg(s => s.Eye),
            Avg(s => s.Age), Avg(s => s.Style), Avg(s => s.Scene), allow);
    }

    private static KitVideoIdentityStressRepository.AttemptRow FailedAttempt(
        Guid testId, string code, int attempt, string fp, string status, string provider, string model) =>
        new()
        {
            Id = Guid.NewGuid(),
            TestId = testId,
            TestGroup = KitVideoIdentityStressRules.GroupOf(code),
            TestCase = code,
            Attempt = attempt,
            ImageType = KitVideoIdentityStressRules.Kind,
            QaStatus = status,
            QaJson = JsonSerializer.Serialize(new { p0 = new[] { status } }),
            Fingerprint = fp,
            Provider = provider,
            Model = model,
        };

    private static bool ShouldReanalyze(KitVideoIdentityStressRepository.AttemptRow art, string? caseCode)
    {
        if (!string.IsNullOrWhiteSpace(caseCode)
            && !art.TestCase.Equals(caseCode, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.IsNullOrWhiteSpace(caseCode)) return true;
        return art.QaStatus is "FAIL" or "VISION_FAIL" or "PENDING" or "ARTIFACT_FAILED";
    }

    private static Dictionary<string, KitVideoIdentityStressRepository.AttemptRow> LatestByCase(
        IReadOnlyList<KitVideoIdentityStressRepository.AttemptRow> arts) =>
        arts.GroupBy(a => a.TestCase).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Attempt).First());

    private static KitVideoIdentityStressArtifactDto ToArtifact(KitVideoIdentityStressRepository.AttemptRow row)
    {
        using var qa = JsonDocument.Parse(string.IsNullOrWhiteSpace(row.QaJson) ? "{}" : row.QaJson);
        return new KitVideoIdentityStressArtifactDto(
            row.Id, row.TestId, row.TestGroup, row.TestCase, row.Attempt, row.ArtifactPath, row.Sha256,
            row.Fingerprint, row.ImageType, row.QaStatus, qa.RootElement.Clone(), row.Provider, row.Model);
    }

    private static string GroupStatus(Dictionary<string, KitVideoIdentityStressRepository.AttemptRow> latest, string group)
    {
        var codes = KitVideoIdentityStressRules.Cases.Where(c => c.Group == group).Select(c => c.Code).ToList();
        var arts = codes.Select(c => latest.TryGetValue(c, out var a) ? a : null).ToList();
        if (arts.Any(a => a is null)) return "PENDING";
        if (arts.Any(a => a is { QaStatus: "FAIL" or "VISION_FAIL" or "ARTIFACT_FAILED" })) return "FAIL";
        if (arts.All(a => a?.QaStatus == "PASS")) return "PASS";
        return "PENDING";
    }

    private sealed record QaParse(List<string> P0, List<string> P1, int Identity, int Structure, int Hair, int Eye, int Age, int Style, int Scene);

    private static QaParse ParseQa(string qaJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(qaJson) ? "{}" : qaJson);
            var r = doc.RootElement;
            return new QaParse(
                ReadArr(r, "p0"), ReadArr(r, "p1"),
                ReadInt(r, "identityScore"), ReadInt(r, "facialStructureScore"), ReadInt(r, "hairScore"),
                ReadInt(r, "eyeScore"), ReadInt(r, "ageConsistencyScore"), ReadInt(r, "styleConsistencyScore"),
                ReadInt(r, "sceneComplianceScore"));
        }
        catch (JsonException)
        {
            return new QaParse([], [], 0, 0, 0, 0, 0, 0, 0);
        }
    }

    private static int ReadInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out var n) && n.TryGetInt32(out var v) ? v : 0;

    private static List<string> ReadArr(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array) return [];
        return arr.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s.Length > 0).ToList();
    }

    private static int CountJson(string? json)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
            return doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.GetArrayLength() : 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static string LifecycleOf(string? extraJson)
    {
        try
        {
            using var extra = JsonDocument.Parse(string.IsNullOrWhiteSpace(extraJson) ? "{}" : extraJson);
            if (extra.RootElement.TryGetProperty("selection", out var sel) && sel.ValueKind == JsonValueKind.Object
                && sel.TryGetProperty("lifecycle", out var life))
                return life.GetString() ?? "";
        }
        catch (JsonException) { /* ignore */ }
        return "";
    }

    private static bool IsEligible(string extraJson, string lifecycle, string qaStatus)
    {
        if (lifecycle is "ELIGIBLE" or "FRONT_RUNNER") return true;
        if (IsFrontRunner(extraJson, lifecycle)) return true;
        try
        {
            using var extra = JsonDocument.Parse(string.IsNullOrWhiteSpace(extraJson) ? "{}" : extraJson);
            if (extra.RootElement.TryGetProperty("selection", out var sel) && sel.ValueKind == JsonValueKind.Object
                && sel.TryGetProperty("eligible", out var el) && el.ValueKind == JsonValueKind.True)
                return true;
        }
        catch (JsonException) { /* ignore */ }
        return qaStatus.Equals("PASS", StringComparison.OrdinalIgnoreCase) && lifecycle is not ("INELIGIBLE" or "NOT_ELIGIBLE" or "REJECTED");
    }

    private static bool IsFrontRunner(string extraJson, string lifecycle)
    {
        if (lifecycle.Equals("FRONT_RUNNER", StringComparison.OrdinalIgnoreCase)) return true;
        try
        {
            using var extra = JsonDocument.Parse(string.IsNullOrWhiteSpace(extraJson) ? "{}" : extraJson);
            if (extra.RootElement.TryGetProperty("selection", out var sel) && sel.ValueKind == JsonValueKind.Object
                && sel.TryGetProperty("frontRunner", out var fr) && fr.ValueKind == JsonValueKind.True)
                return true;
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
