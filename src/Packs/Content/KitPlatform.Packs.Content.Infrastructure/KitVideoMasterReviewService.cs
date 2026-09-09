using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoMasterReviewService : IKitVideoMasterReviewService
{
    private readonly KitVideoMasterReviewRepository _repo;
    private readonly KitVideoMasterReferenceRepository _candidates;
    private readonly KitVideoIdentityTestRepository _identity;
    private readonly KitVideoIdentityStressRepository _stress;
    private readonly KitVideoMasterLockRepository _locks;
    private readonly KitVideoArtifactStore _store;

    public KitVideoMasterReviewService(
        KitVideoMasterReviewRepository repo,
        KitVideoMasterReferenceRepository candidates,
        KitVideoIdentityTestRepository identity,
        KitVideoIdentityStressRepository stress,
        KitVideoMasterLockRepository locks,
        KitVideoArtifactStore store)
    {
        _repo = repo;
        _candidates = candidates;
        _identity = identity;
        _stress = stress;
        _locks = locks;
        _store = store;
    }

    public async Task<IReadOnlyList<KitVideoMasterReviewDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListAsync(cancellationToken);
        var list = new List<KitVideoMasterReviewDto>();
        foreach (var row in rows) list.Add(await ToDto(row, cancellationToken));
        return list;
    }

    public async Task<KitVideoMasterReviewDto> OpenAsync(KitVideoMasterReviewOpenRequest request, CancellationToken cancellationToken = default)
    {
        if (!KitVideoMasterReviewRules.Accepts(request.ProjectCode, request.CharacterId, request.EraId))
            throw new InvalidOperationException("MASTER_REVIEW: FAMIXA / CHAR-001 / ERA-01 only.");
        var cand = await ResolveCandidate(request.CandidateId, cancellationToken);
        var existing = await _repo.GetByCandidateAsync(cand.Id, cancellationToken);
        if (existing is not null) return await ToDto(existing, cancellationToken);
        var snap = await SnapshotAsync(cand, cancellationToken);
        KitVideoMasterReviewRules.EnsureCanOpen(cand.CandidateCode, snap.Gate);
        var identityId = await _identity.LatestCompleteIdAsync(cand.Id, cancellationToken);
        var stress = (await _stress.ListTestsAsync(cand.Id, cancellationToken)).FirstOrDefault(t => t.Status == "PASS");
        var row = new KitVideoMasterReviewRepository.ReviewRow
        {
            Id = Guid.NewGuid(),
            CandidateId = cand.Id,
            CandidateCode = cand.CandidateCode,
            IdentityTestId = identityId,
            StressTestId = stress?.Id,
            SourceSha256 = cand.Sha256,
            SourceFingerprint = cand.Fingerprint,
            Status = "PENDING",
        };
        await _repo.InsertAsync(row, cancellationToken);
        await _repo.InsertEventAsync(row.Id, "MASTER_REVIEW_STARTED", null, "system", cancellationToken);
        await _repo.InsertEventAsync(row.Id, "MASTER_REVIEW_OPENED", null, "system", cancellationToken);
        return await ToDto(row, cancellationToken);
    }

    public async Task<KitVideoMasterReviewDto> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await ToDto(await Require(id, cancellationToken), cancellationToken);

    public async Task<KitVideoMasterReviewDto> GetByCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetByCandidateAsync(candidateId, cancellationToken)
            ?? throw new InvalidOperationException("Master Review không tồn tại.");
        return await ToDto(row, cancellationToken);
    }

    public async Task<KitVideoMasterReviewDto> DecideAsync(Guid id, KitVideoMasterReviewDecisionRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var row = await Require(id, cancellationToken);
        var decision = (request.Decision ?? "").Trim().ToUpperInvariant();
        if (!KitVideoMasterReviewRules.IsDirectorDecision(decision))
            throw new InvalidOperationException("MASTER_REVIEW: quyết định Director không hợp lệ.");
        if (decision == "PASS")
        {
            row.Note = request.Note ?? row.Note;
            return await ApproveAndLockAsync(row, actor, cancellationToken);
        }
        KitVideoMasterReviewRules.EnsureNotLocked(false, row.Status);
        var cand = await _candidates.GetCandidateAsync(row.CandidateId, cancellationToken)
            ?? throw new InvalidOperationException("Candidate không tồn tại.");
        var next = decision == "CONDITIONAL" ? "CONDITIONAL" : "REJECTED";
        await _repo.InsertDecisionAsync(id, row.CandidateId, decision, request.Note ?? "", actor, cancellationToken);
        await _repo.SetStatusAsync(id, next, request.Note, cancellationToken);
        await _repo.InsertEventAsync(id, "DIRECTOR_" + decision, null, actor, cancellationToken);
        await _candidates.MergeCandidateExtraAsync(cand.Id, JsonSerializer.Serialize(new
        {
            masterReview = new
            {
                documentId = KitVideoMasterReviewRules.DocumentId,
                status = next,
                autoSelected = false,
            }
        }), cancellationToken);
        row.Status = next;
        row.Note = request.Note ?? row.Note;
        return await ToDto(row, cancellationToken);
    }

    public async Task<KitVideoMasterReviewDto> SelectMasterAsync(Guid id, string actor, CancellationToken cancellationToken = default)
    {
        var row = await Require(id, cancellationToken);
        var cand = await _candidates.GetCandidateAsync(row.CandidateId, cancellationToken)
            ?? throw new InvalidOperationException("Candidate không tồn tại.");
        var snap = await SnapshotAsync(cand, cancellationToken);
        KitVideoMasterReviewRules.EnsureCanSelect(row.Status, cand.CandidateCode, snap.Gate);
        if (KitVideoMasterReviewRules.IsGoldenPath(cand.ArtifactPath))
            throw new InvalidOperationException("MASTER_REVIEW: không đụng Golden SH01-01.");
        var asset = await _candidates.GetAssetAsync("FAMIXA", "CHAR-001", cancellationToken)
            ?? throw new InvalidOperationException("Master Reference package không tồn tại.");
        if (asset.Status.Equals("LOCKED", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("MASTER_LOCKED: V1 không overwrite.");
        await _candidates.UpsertMasterReferenceAsync(asset.VersionId, cand.ArtifactPath, cand.Sha256, cand.CandidateCode, cancellationToken);
        await _candidates.MergeCandidateExtraAsync(cand.Id, JsonSerializer.Serialize(new
        {
            masterReview = new
            {
                documentId = KitVideoMasterReviewRules.DocumentId,
                status = "SELECTED",
                masterRefCode = KitVideoMasterReviewRules.MasterRefCode,
                copiedBytes = false,
                autoSelected = false,
            }
        }), cancellationToken);
        await _candidates.InsertEventAsync(asset.AssetId, asset.VersionId, cand.Id, "MASTER_REVIEW_SELECTED", actor, cand.ArtifactPath, cand.Sha256, "{}", cancellationToken);
        await _repo.InsertDecisionAsync(id, cand.Id, "SELECT", row.Note, actor, cancellationToken);
        await _repo.SetStatusAsync(id, "SELECTED", null, cancellationToken);
        await _repo.InsertEventAsync(id, "MASTER_SELECTED", null, actor, cancellationToken);
        row.Status = "SELECTED";
        return await ToDto(row, cancellationToken);
    }

    public async Task<KitVideoMasterReviewDto> ApproveAsync(Guid id, string actor, CancellationToken cancellationToken = default)
    {
        var row = await Require(id, cancellationToken);
        var cand = await _candidates.GetCandidateAsync(row.CandidateId, cancellationToken)
            ?? throw new InvalidOperationException("Candidate không tồn tại.");
        var snap = await SnapshotAsync(cand, cancellationToken);
        KitVideoMasterReviewRules.EnsureCanApprove(row.Status, cand.CandidateCode, snap.Gate);
        var asset = await _candidates.GetAssetAsync("FAMIXA", "CHAR-001", cancellationToken)
            ?? throw new InvalidOperationException("Master Reference package không tồn tại.");
        await _candidates.SetCandidateStatusAsync(cand.Id, "APPROVED", cancellationToken);
        await _candidates.SetMasterStatusAsync(asset.AssetId, "APPROVED", cancellationToken);
        await _candidates.InsertEventAsync(asset.AssetId, asset.VersionId, cand.Id, "MASTER_APPROVED", actor, cand.ArtifactPath, cand.Sha256, "{}", cancellationToken);
        await _repo.InsertDecisionAsync(id, cand.Id, "APPROVE", row.Note, actor, cancellationToken);
        await _repo.SetStatusAsync(id, "APPROVED", null, cancellationToken);
        await _repo.InsertEventAsync(id, "MASTER_APPROVED", null, actor, cancellationToken);
        row.Status = "APPROVED";
        return await ToDto(row, cancellationToken);
    }

    public async Task<KitVideoMasterReviewDto> LockAsync(Guid id, string actor, CancellationToken cancellationToken = default) =>
        await ApproveAndLockAsync(await Require(id, cancellationToken), actor, cancellationToken);

    private async Task<KitVideoMasterReviewDto> ApproveAndLockAsync(
        KitVideoMasterReviewRepository.ReviewRow row, string actor, CancellationToken cancellationToken)
    {
        KitVideoMasterLockRules.EnsureDirector(actor);
        var cand = await _candidates.GetCandidateAsync(row.CandidateId, cancellationToken)
            ?? throw new InvalidOperationException("Candidate không tồn tại.");
        if (KitVideoMasterReviewRules.IsGoldenPath(cand.ArtifactPath))
            throw new InvalidOperationException("MASTER_LOCK: không đụng Golden SH01-01.");
        var existing = await _locks.GetByVersionAsync("CHAR-001", "ERA-01", "V1", cancellationToken);
        if (existing is not null)
        {
            if (!KitVideoMasterLockRules.SameSource(existing.SourceCandidateCode, cand.CandidateCode))
                throw new InvalidOperationException("MASTER_LOCK_CONFLICT: Canon active với source khác. Không overwrite V1.");
            row.Status = "LOCKED";
            return await ToDto(row, cancellationToken);
        }
        if (row.Status is "LOCKED")
            return await ToDto(row, cancellationToken);
        var snap = await SnapshotAsync(cand, cancellationToken);
        KitVideoMasterReviewRules.EnsureDirectorPass(cand.CandidateCode, snap.Gate);
        await _repo.MarkEligibleOnPassAsync(cand.Id, cancellationToken);
        cand = await _candidates.GetCandidateAsync(row.CandidateId, cancellationToken) ?? cand;
        var dnaVersion = await _candidates.MinhDnaVersionAsync(cancellationToken);
        var asset = await _candidates.GetAssetAsync("FAMIXA", "CHAR-001", cancellationToken)
            ?? throw new InvalidOperationException("Master Reference package không tồn tại.");
        var gateJson = JsonSerializer.Serialize(new
        {
            vision = "PASS",
            p0 = 0,
            dna = "APPROVED",
            identity = $"{snap.IdentityHave}/7 PASS",
            stress = $"{snap.StressHave}/10 PASS",
            director = "PASS",
            hashValid = snap.Gate.HashValid,
            productionStill = snap.Gate.ProductionStill,
        });
        await _locks.LockAtomicAsync(new KitVideoMasterLockRepository.LockWrite(
            Guid.NewGuid(), Guid.NewGuid(), row.Id, cand.Id, cand.ParentCandidateId, row.IdentityTestId, row.StressTestId,
            asset.AssetId, asset.VersionId, cand.CandidateCode, cand.ArtifactPath, cand.Sha256, cand.Fingerprint, dnaVersion,
            $"{snap.IdentityHave}/7 PASS", $"{snap.StressHave}/10 PASS", gateJson, actor, row.Note), cancellationToken);
        row.Status = "LOCKED";
        return await ToDto(row, cancellationToken);
    }

    public async Task<KitVideoMasterLockDto?> ResolveCanonAsync(string characterId, string eraId, CancellationToken cancellationToken = default)
    {
        var row = await _locks.GetActiveCanonAsync(characterId, eraId, cancellationToken);
        return row is null ? null : ToLockDto(row);
    }

    public Task RejectMasterMutationAsync(Guid masterId, string action, CancellationToken cancellationToken = default)
    {
        _ = masterId;
        _ = cancellationToken;
        KitVideoMasterLockRules.EnsureImmutable(action);
        throw new InvalidOperationException("MASTER_LOCKED: V1 không overwrite. Dùng MASTER_CHANGE_REQUEST / V2.");
    }

    private async Task<KitVideoMasterReferenceRepository.CandidateRow> ResolveCandidate(Guid? candidateId, CancellationToken ct)
    {
        if (candidateId is { } id)
        {
            return await _candidates.GetCandidateAsync(id, ct)
                ?? throw new InvalidOperationException("Candidate không tồn tại.");
        }
        var asset = await _candidates.GetAssetAsync("FAMIXA", "CHAR-001", ct)
            ?? throw new InvalidOperationException("Master Reference package không tồn tại.");
        var list = await _candidates.ListCandidatesAsync(asset.VersionId, ct);
        return list.FirstOrDefault(c => KitVideoMasterReviewRules.IsAllowedCandidate(c.CandidateCode))
            ?? throw new InvalidOperationException("MASTER_REVIEW_BLOCKED: chỉ MINH-E01-CANDIDATE-004-D.");
    }

    private async Task<KitVideoMasterReviewRepository.ReviewRow> Require(Guid id, CancellationToken ct) =>
        await _repo.GetAsync(id, ct) ?? throw new InvalidOperationException("Master Review không tồn tại.");

    private async Task<KitVideoMasterReviewDto> ToDto(KitVideoMasterReviewRepository.ReviewRow row, CancellationToken ct)
    {
        var cand = await _candidates.GetCandidateAsync(row.CandidateId, ct)
            ?? throw new InvalidOperationException("Candidate không tồn tại.");
        var snap = await SnapshotAsync(cand, ct);
        var decision = await _repo.LatestDecisionAsync(row.Id, ct);
        var parent = KitVideoMasterReviewRules.ParentCandidate;
        if (cand.ParentCandidateId is { } pid)
        {
            var p = await _candidates.GetCandidateAsync(pid, ct);
            if (p is not null) parent = p.CandidateCode;
        }
        string? blocked = null;
        try { KitVideoMasterReviewRules.EnsureCanOpen(cand.CandidateCode, snap.Gate); }
        catch (InvalidOperationException ex) { blocked = ex.Message; }
        var canPass = blocked is null && row.Status is "PENDING" or "CONDITIONAL";
        var canSelect = row.Status == "PASS" && blocked is null;
        var canApprove = row.Status == "SELECTED" && blocked is null;
        var canLock = blocked is null && row.Status is "PASS" or "SELECTED" or "APPROVED";
        var locked = await _locks.GetByVersionAsync("CHAR-001", "ERA-01", "V1", ct);
        return new KitVideoMasterReviewDto(
            row.Id, row.ProjectCode, row.CharacterId, row.EraId, row.CandidateId, row.CandidateCode,
            KitVideoMasterReviewRules.Variation, parent, row.SourceSha256, row.Status, row.DocumentId,
            row.MasterRefCode, row.Note, row.IdentityTestId, row.StressTestId,
            snap.IdentityHave, KitVideoMasterReviewRules.IdentityRequired, snap.Gate.IdentityP0, snap.Gate.IdentityPass,
            snap.StressHave, KitVideoMasterReviewRules.StressRequired, snap.Gate.StressP0, snap.Gate.StressPass,
            snap.Gate.St10Pass, snap.Gate.DnaApproved, canPass, canSelect, canApprove, canLock,
            false, row.Status == "LOCKED" || locked is not null, decision, blocked, snap.IdentityShots, snap.StressShots,
            locked is null ? null : ToLockDto(locked));
    }

    private sealed record Snap(
        KitVideoMasterReviewRules.Gate Gate,
        int IdentityHave,
        int StressHave,
        IReadOnlyList<KitVideoMasterReviewShotDto> IdentityShots,
        IReadOnlyList<KitVideoMasterReviewShotDto> StressShots);

    private async Task<Snap> SnapshotAsync(KitVideoMasterReferenceRepository.CandidateRow cand, CancellationToken ct)
    {
        var bytes = _store.Read(cand.ArtifactPath);
        var readable = bytes is { Length: > 32 };
        var liveSha = readable ? KitVideoIntegrityRules.Sha256Hex(bytes!) : "";
        var hashValid = readable && liveSha.Equals(cand.Sha256, StringComparison.OrdinalIgnoreCase);
        var dna = (await _candidates.MinhDnaStatusAsync(ct) ?? "DRAFT").ToUpperInvariant();
        var asset = await _candidates.GetAssetAsync("FAMIXA", "CHAR-001", ct);
        var locked = cand.Status.Equals("LOCKED", StringComparison.OrdinalIgnoreCase)
            || MasterStatusOf(asset) == "LOCKED";
        var life = LifecycleOf(cand.ExtraJson);
        var eligible = KitVideoMasterReviewRules.IsReviewEligible(
            life, SelectionEligible(cand.ExtraJson), FrontRunner(cand.ExtraJson),
            cand.QaStatus.Equals("PASS", StringComparison.OrdinalIgnoreCase));
        var candP0 = ReadP0(cand.QaJson).Count;
        var identityId = await _identity.LatestCompleteIdAsync(cand.Id, ct);
        var identityShots = new List<KitVideoMasterReviewShotDto>();
        var identityP0 = 0;
        var identityPassCount = 0;
        if (identityId is { } iid)
        {
            var arts = (await _identity.ListArtifactsAsync(iid, ct))
                .GroupBy(a => a.TestVariant)
                .Select(g => g.OrderByDescending(x => x.Attempt).First())
                .ToList();
            foreach (var v in KitVideoMasterReviewRules.IdentityVariants)
            {
                var art = arts.FirstOrDefault(a => a.TestVariant.Equals(v, StringComparison.OrdinalIgnoreCase));
                var p0 = art is null ? 1 : ReadP0(art.QaJson).Count;
                var pass = art is { QaStatus: "PASS" } && p0 == 0 && art.Sha256.Length > 8;
                if (pass) identityPassCount += 1;
                identityP0 += p0;
                identityShots.Add(new KitVideoMasterReviewShotDto(
                    art?.Id ?? Guid.Empty, v, IdentityLabel(v), "IDENTITY_TEST",
                    art?.Attempt ?? 0, art?.QaStatus ?? "NOT_RUN", p0, ReadScore(art?.QaJson), art?.Sha256 ?? ""));
            }
        }
        var identityPass = identityPassCount == KitVideoMasterReviewRules.IdentityRequired && identityP0 == 0;

        var stressTests = await _stress.ListTestsAsync(cand.Id, ct);
        var stress = stressTests.FirstOrDefault(t => t.Status == "PASS") ?? stressTests.FirstOrDefault();
        var stressShots = new List<KitVideoMasterReviewShotDto>();
        var stressP0 = 0;
        var stressPassCount = 0;
        var st10 = false;
        if (stress is not null)
        {
            var arts = (await _stress.ListAttemptsAsync(stress.Id, ct))
                .GroupBy(a => a.TestCase)
                .Select(g => g.OrderByDescending(x => x.Attempt).First())
                .ToList();
            foreach (var code in KitVideoMasterReviewRules.StressCases)
            {
                var art = arts.FirstOrDefault(a => a.TestCase.Equals(code, StringComparison.OrdinalIgnoreCase));
                var p0 = art is null ? 1 : ReadP0(art.QaJson).Count;
                var pass = art is { QaStatus: "PASS" } && p0 == 0 && art.Sha256.Length > 8;
                if (pass) stressPassCount += 1;
                if (code == "ST-10") st10 = pass;
                stressP0 += p0;
                stressShots.Add(new KitVideoMasterReviewShotDto(
                    art?.Id ?? Guid.Empty, code, StressLabel(code), "IDENTITY_STRESS",
                    art?.Attempt ?? 0, art?.QaStatus ?? "NOT_RUN", p0, ReadScore(art?.QaJson), art?.Sha256 ?? ""));
            }
        }
        var stressPass = stressPassCount == KitVideoMasterReviewRules.StressRequired && stressP0 == 0 && st10;

        var gate = new KitVideoMasterReviewRules.Gate(
            readable, hashValid,
            cand.QaStatus.Equals("PASS", StringComparison.OrdinalIgnoreCase),
            cand.ImageType.Equals("PRODUCTION_STILL", StringComparison.OrdinalIgnoreCase),
            candP0, eligible, KitVideoMasterReviewRules.DnaAllows(dna),
            identityPass, identityPassCount, identityP0,
            stressPass, stressPassCount, stressP0, st10, locked);
        return new Snap(gate, identityPassCount, stressPassCount, identityShots, stressShots);
    }

    private static KitVideoMasterLockRules.LockGate ToLockGate(
        KitVideoMasterReviewRules.Gate g,
        KitVideoMasterReviewRepository.ReviewRow row,
        KitVideoMasterReferenceRepository.CandidateRow cand,
        string actor,
        string lifecycle)
    {
        var review = (row.Status ?? "").ToUpperInvariant();
        var life = (lifecycle ?? "").ToUpperInvariant();
        var rejected = cand.Status.Equals("REJECTED", StringComparison.OrdinalIgnoreCase) || review == "REJECTED";
        var directorPass = review is "PASS" or "SELECTED" or "APPROVED" or "LOCKED";
        var draftBlocked = cand.Status.Equals("DRAFT", StringComparison.OrdinalIgnoreCase) && !directorPass;
        var eligibleBlocked = life is not ("ELIGIBLE" or "FRONT_RUNNER" or "MASTER_REFERENCE") && !directorPass;
        return new KitVideoMasterLockRules.LockGate(
            g.Readable, g.HashValid, g.VisionPass, g.ProductionStill, g.CandidateP0, g.Eligible, g.DnaApproved,
            g.IdentityPass, g.IdentityHave, g.IdentityP0, g.StressPass, g.StressHave, g.StressP0, g.St10Pass,
            directorPass, rejected, draftBlocked, eligibleBlocked, g.LockedMaster, cand.CandidateCode, row.Status, actor);
    }

    private static KitVideoMasterLockDto ToLockDto(KitVideoMasterLockRepository.MasterRow m) =>
        new(
            m.Id, m.MasterCode, "CHAR-001", m.CharacterName, m.EraId, m.SourceCandidateId, m.SourceCandidateCode,
            m.SourceVariation, m.ArtifactPath, m.Sha256, m.VisionFingerprint, m.DnaVersion, m.IdentityTestResult,
            m.StressTestResult, m.MasterReviewResult, m.LockedBy, m.LockedAt, m.LockReason, m.Version, m.Status,
            m.CanonPointerId, true);

    private static string IdentityLabel(string v) => v switch
    {
        "THREE_QUARTER_LEFT" => "3/4 LEFT",
        "THREE_QUARTER_RIGHT" => "3/4 RIGHT",
        "LIGHT_SMILE" => "LIGHT SMILE",
        _ => v,
    };

    private static string StressLabel(string code) =>
        KitVideoIdentityStressRules.CaseOf(code)?.Label ?? code;

    private static string MasterStatusOf(KitVideoMasterReferenceRepository.AssetRow? asset)
    {
        if (asset is null) return "";
        try
        {
            using var extra = JsonDocument.Parse(string.IsNullOrWhiteSpace(asset.ExtraJson) ? "{}" : asset.ExtraJson);
            if (extra.RootElement.TryGetProperty("masterReference", out var m) && m.TryGetProperty("status", out var s))
                return (s.GetString() ?? "").ToUpperInvariant();
        }
        catch (JsonException) { /* ignore */ }
        return (asset.Status ?? "").ToUpperInvariant();
    }

    private static string LifecycleOf(string extraJson)
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

    private static bool SelectionEligible(string extraJson)
    {
        try
        {
            using var extra = JsonDocument.Parse(string.IsNullOrWhiteSpace(extraJson) ? "{}" : extraJson);
            if (extra.RootElement.TryGetProperty("selection", out var sel) && sel.ValueKind == JsonValueKind.Object
                && sel.TryGetProperty("eligible", out var el) && el.ValueKind == JsonValueKind.True)
                return true;
        }
        catch (JsonException) { /* ignore */ }
        return false;
    }

    private static bool FrontRunner(string extraJson)
    {
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

    private static List<string> ReadP0(string? qaJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(qaJson) ? "{}" : qaJson);
            if (!doc.RootElement.TryGetProperty("p0", out var arr) || arr.ValueKind != JsonValueKind.Array) return [];
            return arr.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s.Length > 0).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static int ReadScore(string? qaJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(qaJson) ? "{}" : qaJson);
            if (doc.RootElement.TryGetProperty("identityScore", out var n) && n.TryGetInt32(out var v)) return v;
        }
        catch (JsonException) { /* ignore */ }
        return 0;
    }
}
