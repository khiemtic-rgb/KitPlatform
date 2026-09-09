using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoMasterReferenceService : IKitVideoMasterReferenceService
{
    private readonly KitVideoMasterReferenceRepository _repo;
    private readonly KitVideoMasterReviewRepository _reviews;
    private readonly IImageGenerator _images;
    private readonly IImageVisionAnalyzer _vision;
    private readonly KitVideoArtifactStore _store;

    public KitVideoMasterReferenceService(
        KitVideoMasterReferenceRepository repo,
        KitVideoMasterReviewRepository reviews,
        IImageGenerator images,
        IImageVisionAnalyzer vision,
        KitVideoArtifactStore store)
    {
        _repo = repo;
        _reviews = reviews;
        _images = images;
        _vision = vision;
        _store = store;
    }

    public async Task<KitVideoMasterReferenceDto?> GetAsync(
        string projectCode, string assetCode, CancellationToken cancellationToken = default)
    {
        KitVideoMasterReferenceRules.EnsureProjectAsset(projectCode, assetCode);
        var asset = await _repo.GetAssetAsync(projectCode.Trim().ToUpperInvariant(), assetCode.Trim().ToUpperInvariant(), cancellationToken);
        if (asset is null) return null;
        var candidates = await _repo.ListCandidatesAsync(asset.VersionId, cancellationToken);
        return ToDto(asset, candidates);
    }

    public async Task<KitVideoMasterReferenceDto> RegisterCandidateAsync(
        KitVideoMasterCandidateRequest request, CancellationToken cancellationToken = default)
    {
        KitVideoMasterReferenceRules.EnsureProjectAsset(request.ProjectCode, request.AssetCode);
        if (KitVideoMasterReferenceRules.IsLegacyOrGolden(request.ArtifactPath))
            throw new InvalidOperationException("LEGACY_OR_GOLDEN_NOT_MASTER");
        var asset = await RequireAsset(request.ProjectCode, request.AssetCode, cancellationToken);
        var existing = await _repo.ListCandidatesAsync(asset.VersionId, cancellationToken);
        if (existing.Any(c => string.Equals(c.Status, "LOCKED", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("MASTER_LOCKED: V1 không overwrite.");
        var code = KitVideoMasterReferenceRules.NextCandidateCode(existing.Select(c => c.CandidateCode).ToList());
        var row = new KitVideoMasterReferenceRepository.CandidateRow
        {
            Id = Guid.NewGuid(),
            VersionId = asset.VersionId,
            CandidateCode = code,
            Role = string.IsNullOrWhiteSpace(request.Role) ? "IDENTITY" : request.Role.Trim().ToUpperInvariant(),
            GenerationAttempt = existing.Count + 1,
            ArtifactPath = request.ArtifactPath ?? "",
            Sha256 = (request.Sha256 ?? "").Trim().ToLowerInvariant(),
            VisualStyleVersion = string.IsNullOrWhiteSpace(request.VisualStyleVersion) ? "V1" : request.VisualStyleVersion.Trim(),
            CharacterId = "CHAR-001",
            EraId = KitVideoMasterReferenceRules.Era01,
            ImageType = string.IsNullOrWhiteSpace(request.ImageType) ? "UNKNOWN" : request.ImageType.Trim().ToUpperInvariant(),
            Status = "DRAFT",
            QaStatus = "PENDING",
            QaJson = "{}",
            ExtraJson = "{}",
        };
        await _repo.InsertCandidateAsync(row, cancellationToken);
        return (await GetAsync(request.ProjectCode, request.AssetCode, cancellationToken))!;
    }

    public async Task<KitVideoMasterReferenceDto> ApplyQaAsync(
        Guid candidateId, KitVideoMasterQaRequest request, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetCandidateAsync(candidateId, cancellationToken)
            ?? throw new InvalidOperationException("Candidate không tồn tại.");
        if (row.Status.Equals("LOCKED", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("MASTER_LOCKED: V1 không overwrite.");
        var p0 = KitVideoMasterReferenceRules.EvaluateQa(new KitVideoMasterQaInput(
            request.ArtifactExists,
            request.Sha256 ?? row.Sha256,
            request.PersistedSha256 ?? row.Sha256,
            request.CharacterId ?? row.CharacterId,
            "CHAR-001",
            request.Age,
            11,
            request.IdentityMatch,
            request.StyleMatch,
            request.Photoreal,
            request.Anime,
            request.Corrupt,
            request.Watermark,
            request.ImageType ?? row.ImageType,
            request.AngleIdentityFail,
            request.ExpressionIdentityFail));
        var status = KitVideoMasterReferenceRules.QaStatus(p0);
        if (KitVideoMasterCreationRules.IsSheet(request.ImageType ?? row.ImageType))
        {
            p0 = p0.Concat(["IMAGE_TYPE_FAIL"]).Distinct().ToList();
            status = "FAIL";
        }
        await _repo.UpdateCandidateQaAsync(candidateId, status, JsonSerializer.Serialize(new { p0, status, usedBeautyScore = false }), cancellationToken);
        return (await GetAsync(KitVideoMasterReferenceRules.FamixaProject, KitVideoMasterReferenceRules.MinhAsset, cancellationToken))!;
    }

    public async Task<KitVideoMasterReferenceDto> AnalyzeAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetCandidateAsync(candidateId, cancellationToken)
            ?? throw new InvalidOperationException("Candidate không tồn tại.");
        var bytes = _store.Read(row.ArtifactPath);
        if (bytes is null)
        {
            await _repo.UpdateCandidateQaAsync(candidateId, "BLOCK", JsonSerializer.Serialize(new { p0 = new[] { "ARTIFACT_FAILED" } }), cancellationToken);
            return (await GetAsync(KitVideoMasterReferenceRules.FamixaProject, KitVideoMasterReferenceRules.MinhAsset, cancellationToken))!;
        }
        await RunVisionAndStore(row, bytes, cancellationToken);
        var asset = await RequireAsset(KitVideoMasterReferenceRules.FamixaProject, KitVideoMasterReferenceRules.MinhAsset, cancellationToken);
        await SafeEvent(asset.AssetId, asset.VersionId, row.Id, "CANDIDATE_ANALYZED", "system", row.ArtifactPath, row.Sha256, cancellationToken);
        return (await GetAsync(KitVideoMasterReferenceRules.FamixaProject, KitVideoMasterReferenceRules.MinhAsset, cancellationToken))!;
    }

    public async Task<KitVideoMasterGenerateDto> GenerateCandidateAsync(
        KitVideoMasterGenerateRequest request, CancellationToken cancellationToken = default)
    {
        KitVideoMasterReferenceRules.EnsureProjectAsset(request.ProjectCode, request.AssetCode);
        var asset = await RequireAsset(request.ProjectCode, request.AssetCode, cancellationToken);
        var existing = await _repo.ListCandidatesAsync(asset.VersionId, cancellationToken);
        var locked = existing.Any(c => c.Status.Equals("LOCKED", StringComparison.OrdinalIgnoreCase))
            || ToMasterStatus(asset).Equals("LOCKED", StringComparison.OrdinalIgnoreCase);
        var view = string.IsNullOrWhiteSpace(request.View) ? "FRONT" : request.View.Trim().ToUpperInvariant();
        var expression = string.IsNullOrWhiteSpace(request.Expression) ? "NEUTRAL" : request.Expression.Trim().ToUpperInvariant();
        var parent = request.ParentCandidateId is { } pid
            ? existing.FirstOrDefault(c => c.Id == pid)
            : null;
        if (request.ParentCandidateId is not null && parent is null)
            throw new InvalidOperationException("VARIATION: candidate gốc không tồn tại.");
        var parentDto = parent is null ? null : ToCandidate(parent);
        if (parent is not null)
        {
            var kids = existing.Count(c => c.ParentCandidateId == parent.Id || ParentIdFromExtra(c.ExtraJson) == parent.Id);
            KitVideoMasterSelectionRules.EnsureCanVary(locked, parentDto!.Lifecycle, parentDto.Recommendation, kids);
        }
        var parentCode = parent?.CandidateCode;
        var code = parent is null
            ? KitVideoMasterReferenceRules.NextCandidateCode(existing.Select(c => c.CandidateCode).ToList())
            : KitVideoMasterSelectionRules.NextVariationCode(parent.CandidateCode, existing.Select(c => c.CandidateCode).ToList());
        var prompt = KitVideoMasterCreationRules.CompilePrompt(view, expression, request.Diagnosis, parentCode);
        var fingerprint = KitVideoMasterCreationRules.Fingerprint(prompt, view, expression, request.Diagnosis, parent is null ? null : code);
        var attempts = await _repo.CountFingerprintAsync(asset.VersionId, fingerprint, cancellationToken);
        var rootCount = existing.Count(c => c.ParentCandidateId is null && ParentIdFromExtra(c.ExtraJson) is null);
        KitVideoMasterCreationRules.EnsureCanGenerate(
            request.Confirmed, parent is null ? rootCount : 0, attempts, attempts > 0, request.Diagnosis, locked);

        var dnaStatus = (await _repo.MinhDnaStatusAsync(cancellationToken) ?? "DRAFT").ToUpperInvariant();
        var canonEligible = dnaStatus is "APPROVED" or "REVIEW" && !request.ControlledTest;

        var gen = await _images.GenerateAsync(
            new KitVideoImageGenerationRequest(prompt, [], "3:4", 768, 1024),
            cancellationToken);
        if (!gen.Ok || gen.Bytes is null)
        {
            var failed = await InsertDraft(asset.VersionId, code, existing.Count + 1, fingerprint, request, view,
                "", "", "UNKNOWN", "FAIL", new { p0 = new[] { gen.FailureClass ?? "PROVIDER_FAILED" }, live = true },
                canonEligible, cancellationToken);
            return Result(failed, null, code, false, "FAIL", gen.Provider, true, canonEligible, gen.FailureClass, null);
        }

        string path;
        byte[] jpeg;
        KitVideoArtifactValidation check;
        try
        {
            (path, jpeg, check) = _store.PersistMaster("CHAR-001", "ERA-01", code, gen.Bytes);
        }
        catch (Exception)
        {
            var failed = await InsertDraft(asset.VersionId, code, existing.Count + 1, fingerprint, request, view,
                "", "", "UNKNOWN", "BLOCK", new { p0 = new[] { "ARTIFACT_FAILED" } }, canonEligible, cancellationToken);
            return Result(failed, null, code, false, "BLOCK", gen.Provider, true, canonEligible, "ARTIFACT_FAILED", null);
        }

        var sha = KitVideoIntegrityRules.Sha256Hex(jpeg);
        if (!check.Ok)
        {
            var failed = await InsertDraft(asset.VersionId, code, existing.Count + 1, fingerprint, request, view,
                path, sha, "UNKNOWN", "BLOCK", new { p0 = new[] { "ARTIFACT_FAILED" }, check }, canonEligible, cancellationToken);
            return Result(failed, null, code, false, "BLOCK", gen.Provider, true, canonEligible, "ARTIFACT_FAILED", sha);
        }

        var row = new KitVideoMasterReferenceRepository.CandidateRow
        {
            Id = Guid.NewGuid(),
            VersionId = asset.VersionId,
            CandidateCode = code,
            Role = "IDENTITY",
            GenerationAttempt = existing.Count + 1,
            ArtifactPath = path,
            Sha256 = sha,
            VisualStyleVersion = "V1",
            CharacterId = "CHAR-001",
            EraId = "ERA-01",
            ImageType = "UNKNOWN",
            Status = "DRAFT",
            QaStatus = "PENDING",
            QaJson = "{}",
            ExtraJson = JsonSerializer.Serialize(new
            {
                fingerprint,
                diagnosis = request.Diagnosis,
                canonEligible,
                view,
                expression,
                controlledTest = request.ControlledTest,
                parentCandidateId = parent?.Id,
                sourceCandidateId = parent?.Id,
                parentCode,
            }),
            Fingerprint = fingerprint,
            ParentCandidateId = parent?.Id,
        };
        await _repo.InsertCandidateAsync(row, cancellationToken);
        await SafeEvent(asset.AssetId, asset.VersionId, row.Id, parent is null ? "CANDIDATE_CREATED" : "CANDIDATE_VARIATION", "system", path, sha, cancellationToken);
        await RunVisionAndStore(row, jpeg, cancellationToken);
        var pkg = (await GetAsync(request.ProjectCode, request.AssetCode, cancellationToken))!;
        var saved = pkg.Candidates.FirstOrDefault(c => c.Id == row.Id);
        return new KitVideoMasterGenerateDto(
            pkg, row.Id, code, true, saved?.QaStatus ?? "PENDING", gen.Provider, true, canonEligible, null, sha);
    }

    public async Task<KitVideoMasterReferenceDto> ApproveAsync(
        string projectCode, string assetCode, Guid? candidateId, string decision, CancellationToken cancellationToken = default)
    {
        var row = await GetAsync(projectCode, assetCode, cancellationToken)
            ?? throw new InvalidOperationException("Master Reference package không tồn tại.");
        if (row.Status.Equals("LOCKED", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("MASTER_LOCKED: V1 không overwrite.");
        var pick = candidateId is { } id
            ? row.Candidates.FirstOrDefault(c => c.Id == id)
            : row.Candidates.FirstOrDefault(c => c.QaStatus == "PASS");
        if (string.Equals(decision, "REQUEST_REVISION", StringComparison.OrdinalIgnoreCase))
        {
            if (pick is not null)
            {
                await _repo.SetCandidateStatusAsync(pick.Id, "REVIEW", cancellationToken);
                await SafeEvent(row.AssetId, row.VersionId, pick.Id, "CANDIDATE_ANALYZED", "director", pick.ArtifactPath, pick.Sha256, cancellationToken);
            }
            return (await GetAsync(projectCode, assetCode, cancellationToken))!;
        }
        if (string.Equals(decision, "REJECT", StringComparison.OrdinalIgnoreCase))
        {
            if (pick is not null)
            {
                await _repo.SetCandidateStatusAsync(pick.Id, "REJECTED", cancellationToken);
                await SafeEvent(row.AssetId, row.VersionId, pick.Id, "CANDIDATE_REJECTED", "director", pick.ArtifactPath, pick.Sha256, cancellationToken);
            }
            return (await GetAsync(projectCode, assetCode, cancellationToken))!;
        }
        if (pick is null) throw new InvalidOperationException("MASTER_NOT_APPROVABLE: chưa chọn candidate.");
        return await SelectAsync(projectCode, assetCode, pick.Id, "director", cancellationToken);
    }

    public async Task<KitVideoMasterReferenceDto> LockAsync(
        string projectCode, string assetCode, CancellationToken cancellationToken = default)
    {
        var row = await GetAsync(projectCode, assetCode, cancellationToken)
            ?? throw new InvalidOperationException("Master Reference package không tồn tại.");
        KitVideoMasterReferenceRules.EnsureNotOverwrite(row.Status, row.Version, row.Version);
        if (!row.Status.Equals("APPROVED", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("MASTER_NOT_LOCKABLE: Director APPROVE + hash + QA PASS trước.");
        var pick = row.Candidates.FirstOrDefault(c => c.Status == "APPROVED" && c.QaStatus == "PASS" && c.CanonEligible);
        var bytes = pick is null ? null : _store.Read(pick.ArtifactPath);
        if (pick is null || string.IsNullOrWhiteSpace(pick.Sha256) || bytes is not { Length: > 32 })
            throw new InvalidOperationException("MASTER_NOT_LOCKABLE: HASH + artifact + QA PASS required.");
        var live = KitVideoIntegrityRules.Sha256Hex(bytes);
        if (!live.Equals(pick.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("HASH_MISMATCH");
        await _repo.SetMasterStatusAsync(row.AssetId, "LOCKED", cancellationToken);
        await SafeEvent(row.AssetId, row.VersionId, pick.Id, "MASTER_LOCKED", "director", pick.ArtifactPath, pick.Sha256, cancellationToken);
        return (await GetAsync(projectCode, assetCode, cancellationToken))!;
    }

    public Task<KitVideoMasterCompileDto> CompileAsync(string role, bool dnaApproved, bool styleReady)
    {
        if (!styleReady)
            return Task.FromResult(new KitVideoMasterCompileDto(false, "", "none", "Style System / Visual Style must be REVIEW/APPROVED first."));
        if (!dnaApproved)
            return Task.FromResult(new KitVideoMasterCompileDto(false, "", "none", "CHAR-001 Visual DNA V1 must be APPROVED before Canon Master. Controlled test vẫn dùng DNA nháp."));
        var prompt = KitVideoMasterCreationRules.CompilePrompt(role, "NEUTRAL");
        return Task.FromResult(new KitVideoMasterCompileDto(true, prompt, _images.ProviderId, null));
    }

    public async Task<(byte[] Bytes, string Mime)?> ReadCandidateImageAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetCandidateAsync(candidateId, cancellationToken);
        var bytes = row is null ? null : _store.Read(row.ArtifactPath);
        return bytes is { Length: > 32 } ? (bytes, "image/jpeg") : null;
    }

    public async Task<KitVideoMasterCompareDto> CompareAsync(
        string projectCode, string assetCode, CancellationToken cancellationToken = default)
    {
        var pkg = await GetAsync(projectCode, assetCode, cancellationToken)
            ?? throw new InvalidOperationException("Master Reference package không tồn tại.");
        var ranks = new List<KitVideoMasterRankDto>();
        foreach (var c in pkg.Candidates)
        {
            var rank = Rank(c);
            var keepRunner = c.FrontRunner || c.Lifecycle.Equals("FRONT_RUNNER", StringComparison.OrdinalIgnoreCase);
            await _repo.MergeCandidateExtraAsync(c.Id, JsonSerializer.Serialize(new
            {
                selection = new
                {
                    documentId = KitVideoMasterSelectionRules.DocumentId,
                    refinementId = KitVideoMasterSelectionRules.RefinementId,
                    lifecycle = keepRunner ? "FRONT_RUNNER" : rank.Lifecycle,
                    score = rank.Score,
                    recommendation = rank.Recommendation,
                    eligible = rank.Eligible,
                    p0 = rank.P0,
                    frontRunner = keepRunner,
                    autoSelected = false,
                    visualDnaVersion = "V1",
                }
            }), cancellationToken);
            ranks.Add(rank);
        }
        var eligible = ranks.Where(r => r.Eligible).OrderByDescending(r => r.Score).ToList();
        var recommended = eligible.FirstOrDefault();
        var ordered = eligible
            .Select((r, i) => r with { Recommendation = i == 0 ? "RECOMMENDED" : r.Score >= 70 ? "ALTERNATIVE" : "NOT_RECOMMENDED" })
            .Concat(ranks.Where(r => !r.Eligible).Select(r => r with { Recommendation = "INELIGIBLE" }))
            .ToList();
        var fresh = (await GetAsync(projectCode, assetCode, cancellationToken))!;
        return new KitVideoMasterCompareDto(fresh, ordered, recommended?.Id, false, "V1");
    }

    public async Task<KitVideoMasterReferenceDto> SelectAsync(
        string projectCode, string assetCode, Guid candidateId, string actor, CancellationToken cancellationToken = default)
    {
        var pkg = await GetAsync(projectCode, assetCode, cancellationToken)
            ?? throw new InvalidOperationException("Master Reference package không tồn tại.");
        if (pkg.Status.Equals("LOCKED", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("MASTER_LOCKED: V1 không overwrite.");
        var pick = pkg.Candidates.FirstOrDefault(c => c.Id == candidateId)
            ?? throw new InvalidOperationException("Candidate không tồn tại.");
        if (KitVideoMasterReviewRules.IsAllowedCandidate(pick.CandidateCode)
            && !await _reviews.HasDirectorPassAsync(pick.Id, cancellationToken))
            throw new InvalidOperationException("MASTER_REVIEW: Director PASS trên Master Review trước khi SELECT.");
        var rank = Rank(pick);
        var dna = (await _repo.MinhDnaStatusAsync(cancellationToken) ?? "DRAFT").ToUpperInvariant();
        var bytes = _store.Read(pick.ArtifactPath);
        var liveSha = bytes is { Length: > 32 } ? KitVideoIntegrityRules.Sha256Hex(bytes) : "";
        var qaSha = QaSha(pick);
        KitVideoMasterSelectionRules.EnsureCanSelect(new SelectionGate(
            bytes is { Length: > 32 },
            bytes is { Length: > 32 },
            pick.Sha256,
            liveSha,
            qaSha,
            pick.QaStatus.Equals("PASS", StringComparison.OrdinalIgnoreCase),
            pick.ImageType,
            rank.P0,
            rank.Eligible,
            dna is "APPROVED",
            pick.Lifecycle,
            false,
            pick.CanonEligible));
        await _repo.UpsertMasterReferenceAsync(pkg.VersionId, pick.ArtifactPath, pick.Sha256, pick.CandidateCode, cancellationToken);
        await _repo.SetCandidateStatusAsync(pick.Id, "APPROVED", cancellationToken);
        await _repo.SetMasterStatusAsync(pkg.AssetId, "APPROVED", cancellationToken);
        await _repo.MergeCandidateExtraAsync(pick.Id, JsonSerializer.Serialize(new
        {
            selection = new
            {
                documentId = KitVideoMasterSelectionRules.DocumentId,
                lifecycle = "MASTER_REFERENCE",
                score = rank.Score,
                recommendation = rank.Recommendation,
                eligible = true,
                sourceCandidate = pick.CandidateCode,
                copiedBytes = false,
            }
        }), cancellationToken);
        await SafeEvent(pkg.AssetId, pkg.VersionId, pick.Id, "CANDIDATE_SELECTED", actor, pick.ArtifactPath, pick.Sha256, cancellationToken);
        await SafeEvent(pkg.AssetId, pkg.VersionId, pick.Id, "MASTER_CREATED", actor, pick.ArtifactPath, pick.Sha256, cancellationToken);
        await SafeEvent(pkg.AssetId, pkg.VersionId, pick.Id, "MASTER_APPROVED", actor, pick.ArtifactPath, pick.Sha256, cancellationToken);
        return (await GetAsync(projectCode, assetCode, cancellationToken))!;
    }

    public async Task<KitVideoMasterReferenceDto> MarkFrontRunnerAsync(
        string projectCode, string assetCode, Guid candidateId, string actor, CancellationToken cancellationToken = default)
    {
        var pkg = await GetAsync(projectCode, assetCode, cancellationToken)
            ?? throw new InvalidOperationException("Master Reference package không tồn tại.");
        if (pkg.Status.Equals("LOCKED", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("MASTER_LOCKED: V1 không overwrite.");
        var pick = pkg.Candidates.FirstOrDefault(c => c.Id == candidateId)
            ?? throw new InvalidOperationException("Candidate không tồn tại.");
        var already = pick.FrontRunner || pick.Lifecycle.Equals("FRONT_RUNNER", StringComparison.OrdinalIgnoreCase);
        var count = pkg.Candidates.Count(c => c.FrontRunner || c.Lifecycle.Equals("FRONT_RUNNER", StringComparison.OrdinalIgnoreCase));
        KitVideoMasterSelectionRules.EnsureCanMarkFrontRunner(
            false, pick.Lifecycle, pick.Recommendation, pick.CanonEligible, !pick.CanonEligible, count, already);
        var rank = Rank(pick);
        await _repo.MergeCandidateExtraAsync(pick.Id, JsonSerializer.Serialize(new
        {
            selection = new
            {
                documentId = KitVideoMasterSelectionRules.DocumentId,
                refinementId = KitVideoMasterSelectionRules.RefinementId,
                lifecycle = "FRONT_RUNNER",
                score = rank.Score,
                recommendation = rank.Recommendation,
                eligible = rank.Eligible,
                p0 = rank.P0,
                frontRunner = true,
                autoSelected = false,
                visualDnaVersion = "V1",
            }
        }), cancellationToken);
        await SafeEvent(pkg.AssetId, pkg.VersionId, pick.Id, "CANDIDATE_FRONT_RUNNER", actor, pick.ArtifactPath, pick.Sha256, cancellationToken);
        return (await GetAsync(projectCode, assetCode, cancellationToken))!;
    }

    public async Task<KitVideoMasterResolveDto> ResolveAsync(
        string projectCode, string assetCode, CancellationToken cancellationToken = default)
    {
        var pkg = await GetAsync(projectCode, assetCode, cancellationToken);
        var pick = pkg?.Candidates.FirstOrDefault(c => c.Status is "APPROVED" or "LOCKED");
        var live = pick is null ? null : _store.Read(pick.ArtifactPath);
        var liveSha = live is { Length: > 32 } ? KitVideoIntegrityRules.Sha256Hex(live) : null;
        var ok = pkg is not null && pick is not null
            && KitVideoMasterSelectionRules.ProductionAccepts(pkg.Status, pick.ArtifactPath, pick.Sha256, liveSha);
        return new KitVideoMasterResolveDto(
            ok, ok ? null : "PRODUCTION_REQUIRES_LOCKED_MASTER",
            "CHAR-001", "ERA-01", pkg?.Version ?? "V1",
            pick?.CandidateCode, pick?.ArtifactPath, pick?.Sha256, pkg?.Status ?? "MISSING");
    }

    public async Task<IReadOnlyList<KitVideoMasterEventDto>> ListEventsAsync(
        string projectCode, string assetCode, CancellationToken cancellationToken = default)
    {
        var pkg = await GetAsync(projectCode, assetCode, cancellationToken)
            ?? throw new InvalidOperationException("Master Reference package không tồn tại.");
        var rows = await _repo.ListEventsAsync(pkg.AssetId, cancellationToken);
        return rows.Select(r => new KitVideoMasterEventDto(
            r.Id, r.EventType, r.Actor, r.CandidateId, r.ArtifactPath, r.Sha256, r.VisualDnaVersion, r.CreatedAt)).ToList();
    }

    private KitVideoMasterRankDto Rank(KitVideoMasterCandidateDto c)
    {
        var bytes = _store.Read(c.ArtifactPath);
        var liveSha = bytes is { Length: > 32 } ? KitVideoIntegrityRules.Sha256Hex(bytes) : "";
        var visionPass = c.QaStatus.Equals("PASS", StringComparison.OrdinalIgnoreCase);
        var faceOk = visionPass && !KitVideoMasterCreationRules.IsSheet(c.ImageType);
        int count = 1;
        try
        {
            if (c.Qa.ValueKind == JsonValueKind.Object && c.Qa.TryGetProperty("p0", out var p0el) && p0el.ValueKind == JsonValueKind.Array)
            {
                foreach (var x in p0el.EnumerateArray())
                    if (x.GetString() is "IDENTITY_FAIL" or "IMAGE_TYPE_FAIL") faceOk = false;
            }
        }
        catch (JsonException) { /* ignore */ }
        var p0 = KitVideoMasterSelectionRules.EvaluateP0(
            bytes is { Length: > 32 }, c.Sha256, liveSha, QaSha(c), c.ImageType, count, faceOk,
            KitVideoMasterReferenceRules.IsLegacyOrGolden(c.ArtifactPath), !c.CanonEligible);
        var eligible = p0.Count == 0 && visionPass;
        var score = eligible ? KitVideoMasterSelectionRules.Score(80, 78, 80, 76, 78, 72, 80) : KitVideoMasterSelectionRules.Score(40, 40, 40, 40, 40, 30, 40);
        if (!eligible && score >= 90) { /* P0 wins */ }
        return new KitVideoMasterRankDto(
            c.Id, c.CandidateCode, eligible, KitVideoMasterSelectionRules.Lifecycle(eligible, p0),
            score, KitVideoMasterSelectionRules.Recommend(eligible, score), p0);
    }

    private async Task SafeEvent(
        Guid assetId, Guid versionId, Guid? candidateId, string type, string? actor, string path, string sha, CancellationToken ct)
    {
        try
        {
            await _repo.InsertEventAsync(assetId, versionId, candidateId, type, actor, path, sha, "{}", ct);
        }
        catch
        {
            /* mig 346 may be pending — selection still works without audit row */
        }
    }

    private async Task RunVisionAndStore(
        KitVideoMasterReferenceRepository.CandidateRow row, byte[] jpeg, CancellationToken cancellationToken)
    {
        KitVideoVisionAnalysisResult vision;
        try
        {
            vision = await _vision.AnalyzeAsync(
                new KitVideoVisionAnalysisRequest(jpeg, "image/jpeg", KitVideoMasterCreationRules.VisionContract(), [], null),
                cancellationToken);
        }
        catch (Exception)
        {
            await _repo.UpdateCandidateQaAsync(row.Id, "FAIL", JsonSerializer.Serialize(new { p0 = new[] { "VISION_FAILED" }, status = "FAIL" }), cancellationToken);
            return;
        }
        using var raw = JsonDocument.Parse(string.IsNullOrWhiteSpace(vision.RawJson) ? "{}" : ExtractJson(vision.RawJson));
        var imageType = raw.RootElement.TryGetProperty("imageType", out var it) ? it.GetString() ?? "UNKNOWN" : "UNKNOWN";
        var p0 = new List<string>();
        if (KitVideoMasterCreationRules.IsSheet(imageType)) p0.Add("IMAGE_TYPE_FAIL");
        if (vision.DetectedCharacters != 1) p0.Add("IDENTITY_FAIL");
        if (!vision.DetectedCharacterIds.Contains("CHAR-001", StringComparer.OrdinalIgnoreCase) && vision.Overall != "PASS")
            p0.Add("IDENTITY_FAIL");
        if (vision.Overall is "FAIL" or "UNCERTAIN" && p0.Count == 0) p0.Add("IDENTITY_FAIL");
        var qaStatus = p0.Contains("IMAGE_TYPE_FAIL") ? "FAIL" : p0.Count == 0 && vision.Overall == "PASS" ? "PASS" : p0.Contains("ARTIFACT_FAILED") ? "BLOCK" : "FAIL";
        var qa = JsonSerializer.Serialize(new
        {
            p0,
            status = qaStatus,
            usedBeautyScore = false,
            vision = vision.Overall,
            imageType,
            sha256 = row.Sha256,
        });
        row.ImageType = KitVideoMasterCreationRules.IsSheet(imageType)
            ? "CHARACTER_SHEET"
            : imageType is "PRODUCTION_STILL" ? "PRODUCTION_STILL" : imageType;
        if (!new[] { "UNKNOWN", "PRODUCTION_STILL", "CHARACTER_CANDIDATE", "CHARACTER_SHEET", "COLLAGE", "MULTI_PANEL", "REFERENCE_BOARD" }.Contains(row.ImageType))
            row.ImageType = "UNKNOWN";
        await _repo.UpdateCandidateQaAsync(row.Id, qaStatus, qa, cancellationToken, row.ImageType);
    }

    private async Task<KitVideoMasterReferenceDto> InsertDraft(
        Guid versionId, string code, int attempt, string fingerprint, KitVideoMasterGenerateRequest request, string view,
        string path, string sha, string imageType, string qaStatus, object qa, bool canonEligible, CancellationToken ct)
    {
        var row = new KitVideoMasterReferenceRepository.CandidateRow
        {
            Id = Guid.NewGuid(),
            VersionId = versionId,
            CandidateCode = code,
            Role = "IDENTITY",
            GenerationAttempt = attempt,
            ArtifactPath = path,
            Sha256 = sha,
            VisualStyleVersion = "V1",
            CharacterId = "CHAR-001",
            EraId = "ERA-01",
            ImageType = imageType,
            Status = "DRAFT",
            QaStatus = qaStatus,
            QaJson = JsonSerializer.Serialize(qa),
            ExtraJson = JsonSerializer.Serialize(new { fingerprint, diagnosis = request.Diagnosis, canonEligible, view, controlledTest = request.ControlledTest }),
            Fingerprint = fingerprint,
        };
        await _repo.InsertCandidateAsync(row, ct);
        return (await GetAsync(request.ProjectCode, request.AssetCode, ct))!;
    }

    private KitVideoMasterGenerateDto Result(
        KitVideoMasterReferenceDto pkg, Guid? id, string code, bool artifact, string qa, string provider, bool live, bool canon, string? blocked, string? sha) =>
        new(pkg, id, code, artifact, qa, provider, live, canon, blocked, sha);

    private static string ToMasterStatus(KitVideoMasterReferenceRepository.AssetRow asset)
    {
        try
        {
            using var extra = JsonDocument.Parse(string.IsNullOrWhiteSpace(asset.ExtraJson) ? "{}" : asset.ExtraJson);
            if (extra.RootElement.TryGetProperty("masterReference", out var m) && m.TryGetProperty("status", out var s))
                return s.GetString() ?? "DRAFT";
        }
        catch (JsonException) { /* ignore */ }
        return "DRAFT";
    }

    private async Task<KitVideoMasterReferenceRepository.AssetRow> RequireAsset(
        string projectCode, string assetCode, CancellationToken cancellationToken) =>
        await _repo.GetAssetAsync(projectCode.Trim().ToUpperInvariant(), assetCode.Trim().ToUpperInvariant(), cancellationToken)
        ?? throw new InvalidOperationException("CHAR-001 video_asset không tồn tại. Không invent CHAR-005+.");

    private static KitVideoMasterReferenceDto ToDto(
        KitVideoMasterReferenceRepository.AssetRow asset,
        IReadOnlyList<KitVideoMasterReferenceRepository.CandidateRow> candidates)
    {
        using var extra = JsonDocument.Parse(string.IsNullOrWhiteSpace(asset.ExtraJson) ? "{}" : asset.ExtraJson);
        var master = extra.RootElement.TryGetProperty("masterReference", out var m) ? m : extra.RootElement;
        var status = master.ValueKind == JsonValueKind.Object && master.TryGetProperty("status", out var s)
            ? s.GetString() ?? "DRAFT"
            : "DRAFT";
        var spec = master.ValueKind == JsonValueKind.Undefined ? extra.RootElement.Clone() : master.Clone();
        return new KitVideoMasterReferenceDto(
            asset.AssetId,
            asset.VersionId,
            asset.ProjectCode,
            asset.AssetCode,
            asset.Lifecycle,
            KitVideoMasterReferenceRules.MinhDocumentId,
            status,
            string.IsNullOrWhiteSpace(asset.Era) ? KitVideoMasterReferenceRules.Era01 : asset.Era,
            11,
            asset.Version,
            true,
            candidates.Select(ToCandidate).ToList(),
            spec);
    }

    private static KitVideoMasterCandidateDto ToCandidate(KitVideoMasterReferenceRepository.CandidateRow row)
    {
        using var qa = JsonDocument.Parse(string.IsNullOrWhiteSpace(row.QaJson) ? "{}" : row.QaJson);
        var canon = false;
        var diagnosis = "";
        var score = 0;
        var recommendation = "";
        var lifecycle = "DRAFT";
        var eligible = false;
        var frontRunner = false;
        Guid? parentId = row.ParentCandidateId;
        Guid? sourceId = row.ParentCandidateId;
        try
        {
            using var extra = JsonDocument.Parse(string.IsNullOrWhiteSpace(row.ExtraJson) ? "{}" : row.ExtraJson);
            if (extra.RootElement.TryGetProperty("canonEligible", out var c) && c.ValueKind == JsonValueKind.True) canon = true;
            if (extra.RootElement.TryGetProperty("diagnosis", out var d)) diagnosis = d.GetString() ?? "";
            parentId ??= ReadGuid(extra.RootElement, "parentCandidateId");
            sourceId ??= ReadGuid(extra.RootElement, "sourceCandidateId") ?? parentId;
            if (extra.RootElement.TryGetProperty("selection", out var sel) && sel.ValueKind == JsonValueKind.Object)
            {
                if (sel.TryGetProperty("score", out var sc) && sc.TryGetInt32(out var n)) score = n;
                if (sel.TryGetProperty("recommendation", out var rec)) recommendation = rec.GetString() ?? "";
                if (sel.TryGetProperty("lifecycle", out var life)) lifecycle = life.GetString() ?? "DRAFT";
                if (sel.TryGetProperty("eligible", out var el) && el.ValueKind == JsonValueKind.True) eligible = true;
                if (sel.TryGetProperty("frontRunner", out var fr) && fr.ValueKind == JsonValueKind.True) frontRunner = true;
            }
        }
        catch (JsonException) { /* ignore */ }
        if (lifecycle.Equals("FRONT_RUNNER", StringComparison.OrdinalIgnoreCase)) frontRunner = true;
        return new KitVideoMasterCandidateDto(
            row.Id, row.CandidateCode, row.Role, row.GenerationAttempt, row.ArtifactPath, row.Sha256,
            row.VisualStyleVersion, row.CharacterId, row.EraId, row.ImageType, row.Status, row.QaStatus,
            qa.RootElement.Clone(), row.Fingerprint, diagnosis, canon, score, recommendation, lifecycle, eligible,
            frontRunner, parentId, sourceId);
    }

    private static string? QaSha(KitVideoMasterCandidateDto c)
    {
        try
        {
            if (c.Qa.ValueKind == JsonValueKind.Object && c.Qa.TryGetProperty("sha256", out var sha))
                return sha.GetString();
        }
        catch (JsonException) { /* ignore */ }
        return null;
    }

    private static Guid? ParentIdFromExtra(string extraJson)
    {
        try
        {
            using var extra = JsonDocument.Parse(string.IsNullOrWhiteSpace(extraJson) ? "{}" : extraJson);
            return ReadGuid(extra.RootElement, "parentCandidateId");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Guid? ReadGuid(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p)) return null;
        if (p.ValueKind == JsonValueKind.String && Guid.TryParse(p.GetString(), out var g)) return g;
        if (p.ValueKind == JsonValueKind.Null) return null;
        return p.TryGetGuid(out var id) ? id : null;
    }

    private static string ExtractJson(string raw)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        return start >= 0 && end > start ? raw[start..(end + 1)] : "{}";
    }
}
