using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class CharacterReferenceGenerationService : ICharacterReferenceGenerationService
{
    private readonly CharacterReferencePackRepository _packs;
    private readonly KitVideoCharacterDnaRepository _dna;
    private readonly KitVideoMasterLockRepository _locks;
    private readonly KitVideoProductionReferencePackRepository _prp;
    private readonly KitVideoArtifactStore _store;
    private readonly ICharacterReferencePackService _crp;
    private readonly IImageGenerationProvider _images;

    public CharacterReferenceGenerationService(
        CharacterReferencePackRepository packs,
        KitVideoCharacterDnaRepository dna,
        KitVideoMasterLockRepository locks,
        KitVideoProductionReferencePackRepository prp,
        KitVideoArtifactStore store,
        ICharacterReferencePackService crp,
        IGeminiImageGenerationProvider images)
    {
        _packs = packs;
        _dna = dna;
        _locks = locks;
        _prp = prp;
        _store = store;
        _crp = crp;
        _images = images;
    }

    public IReadOnlyList<string> RunRegression() => CharacterReferenceGenerationV1Regression.Run();

    public Task<CharacterReferenceGenerationDto> GetAsync(
        string characterId, string? referenceType, string? provider, string eraId = "ERA-01",
        CancellationToken cancellationToken = default) =>
        PreviewAsync(characterId, new CharacterReferenceGenerationRequest(
            false, provider, eraId, referenceType), "director", persistPrepare: false, cancellationToken);

    public Task<CharacterReferenceGenerationDto> PrepareAsync(
        string characterId, CharacterReferenceGenerationRequest request, string actor,
        CancellationToken cancellationToken = default) =>
        PreviewAsync(characterId, request, actor, persistPrepare: true, cancellationToken);

    public async Task<CharacterReferenceGenerationDto> ExecuteAsync(
        string characterId, CharacterReferenceGenerationRequest request, string actor,
        CancellationToken cancellationToken = default)
    {
        var ctx = await LoadAsync(characterId, request, cancellationToken);
        var gate = CharacterReferenceGenerationRules.Evaluate(ctx.Input);
        if (!CharacterReferenceGenerationRules.MayCallProvider(gate) || ctx.Intent is null || ctx.Canonical is null)
            return ToDto(ctx, gate, generate: false, gemini: false);

        var executionId = Guid.NewGuid();
        var artifactId = Guid.NewGuid();
        if (ctx.Pack is not null)
        {
            await _packs.InsertEventAsync(ctx.Pack.Id, "REFERENCE_GENERATION_REQUESTED", new
            {
                character_id = ctx.CharacterId,
                reference_type = ctx.ReferenceType,
                intent_sha256 = ctx.IntentSha,
                fingerprint = ctx.Fingerprint,
                provider = ctx.Provider,
                confirm = true,
            }, actor, cancellationToken);
            await _packs.InsertEventAsync(ctx.Pack.Id, "GEMINI_REQUESTED", new
            {
                execution_id = executionId,
                generate = true,
                first_real_production = false,
            }, actor, cancellationToken);
        }

        var refs = LoadIdentityRefs(ctx);
        var compiled = CharacterReferenceGenerationRules.CompileProviderRequest(ctx.Canonical, refs, ctx.ReferenceType);
        var result = await _images.GenerateAsync(compiled, cancellationToken);
        var gemini = string.Equals(result.Provider, "GEMINI", StringComparison.OrdinalIgnoreCase);

        if (!result.Accepted || !result.Succeeded || result.Bytes is null || result.Bytes.Length < 32)
        {
            if (ctx.Pack is not null)
            {
                await SaveSlotAsync(ctx, new Slot(
                    executionId, "FAILED", ctx.ReferenceType, ctx.IntentSha, ctx.MasterSha, ctx.DnaSha, ctx.PrpSha,
                    ctx.Fingerprint, ctx.Provider, result.ProviderRequestId, null, null, null, DateTimeOffset.UtcNow),
                    cancellationToken);
                await _packs.InsertEventAsync(ctx.Pack.Id, "EXECUTION_FAILED", new
                {
                    execution_id = executionId,
                    generate = true,
                    auto_approve = false,
                }, actor, cancellationToken);
            }
            return ToDto(ctx, gate, generate: true, gemini,
                executionId: executionId,
                candidateStatus: "FAILED",
                staff: CharacterReferenceGenerationRules.StaffGenerateFail,
                providerRequestId: result.ProviderRequestId);
        }

        if (ctx.Pack is null)
            throw new InvalidOperationException("REFERENCE_GENERATION_BLOCKED: " + CharacterReferenceGenerationRules.StaffMasterBlock);

        var persisted = _store.PersistReferenceCandidate(
            ctx.CharacterId, ctx.EraId, ctx.Pack.Id, ctx.ReferenceType, executionId, result.Bytes);
        var artifactSha = KitVideoIntegrityRules.Sha256Hex(persisted.Bytes);
        await SaveSlotAsync(ctx, new Slot(
            executionId, CharacterReferenceGenerationRules.CandidateReady, ctx.ReferenceType, ctx.IntentSha,
            ctx.MasterSha, ctx.DnaSha, ctx.PrpSha, ctx.Fingerprint, ctx.Provider, result.ProviderRequestId,
            artifactId, persisted.Path, artifactSha, DateTimeOffset.UtcNow), cancellationToken);
        await _packs.InsertEventAsync(ctx.Pack.Id, "READY_FOR_DIRECTOR", new
        {
            execution_id = executionId,
            artifact_id = artifactId,
            artifact_sha256 = artifactSha,
            auto_approve = false,
            auto_lock = false,
            generate = true,
        }, actor, cancellationToken);

        return ToDto(ctx, gate, generate: true, gemini,
            executionId: executionId,
            artifactId: artifactId,
            artifactPath: persisted.Path,
            candidateStatus: CharacterReferenceGenerationRules.CandidateReady,
            review: "PENDING",
            staff: CharacterReferenceGenerationRules.StaffReady,
            next: CharacterReferenceGenerationRules.StaffWaitDirector,
            providerRequestId: result.ProviderRequestId);
    }

    public async Task<CharacterReferenceGenerationDto> GetExecutionAsync(
        string characterId, Guid executionId, CancellationToken cancellationToken = default)
    {
        var ctx = await LoadAsync(characterId, new CharacterReferenceGenerationRequest(), cancellationToken);
        var slot = ResolveSlot(ctx, executionId);
        if (slot is null)
            throw new InvalidOperationException("REFERENCE_GENERATION_BLOCKED: Chưa có lần tạo ảnh này.");
        return ToDto(ctx, CharacterReferenceGenerationRules.Evaluate(ctx.Input with { Confirm = false, DirectorProvider = slot.Provider }),
            generate: false, gemini: false,
            executionId: slot.ExecutionId, artifactId: slot.ArtifactId, artifactPath: slot.ArtifactPath,
            candidateStatus: slot.Status, review: ReviewOf(slot.Status),
            providerRequestId: slot.ProviderRequestId);
    }

    public async Task<CharacterReferenceGenerationDto> AcceptAsync(
        string characterId, Guid executionId, string actor, CancellationToken cancellationToken = default)
    {
        var ctx = await LoadAsync(characterId, new CharacterReferenceGenerationRequest(), cancellationToken);
        var slot = ResolveSlot(ctx, executionId)
            ?? throw new InvalidOperationException("REFERENCE_GENERATION_BLOCKED: Chưa có lần tạo ảnh này.");
        if (slot.Status is not ("READY_FOR_DIRECTOR" or "GENERATED" or "ACCEPTED"))
            throw new InvalidOperationException("REFERENCE_GENERATION_BLOCKED: Ảnh chưa sẵn sàng để chấp nhận.");
        var next = slot with { Status = "ACCEPTED" };
        await SaveSlotAsync(ctx, next, cancellationToken);
        if (ctx.Pack is not null)
            await _packs.InsertEventAsync(ctx.Pack.Id, "REFERENCE_CANDIDATE_ACCEPTED", new
            {
                execution_id = executionId,
                auto_approve = false,
                auto_lock = false,
                generate = false,
            }, actor, cancellationToken);
        return ToDto(ctx, CharacterReferenceGenerationRules.Evaluate(ctx.Input with { Confirm = false }),
            false, false, executionId, next.ArtifactId, next.ArtifactPath, "ACCEPTED", "ACCEPTED",
            "Đã chấp nhận. Có thể đưa vào bộ ảnh chuẩn.", "Đưa vào bộ ảnh chuẩn",
            next.ProviderRequestId);
    }

    public async Task<CharacterReferenceGenerationDto> RejectAsync(
        string characterId, Guid executionId, string actor, string? reason,
        CancellationToken cancellationToken = default)
    {
        if (!CharacterReferenceGenerationRules.RejectReasonValid(reason))
            throw new InvalidOperationException("REFERENCE_GENERATION_BLOCKED: " + CharacterReferenceGenerationRules.StaffReject);
        var ctx = await LoadAsync(characterId, new CharacterReferenceGenerationRequest(), cancellationToken);
        var slot = ResolveSlot(ctx, executionId)
            ?? throw new InvalidOperationException("REFERENCE_GENERATION_BLOCKED: Chưa có lần tạo ảnh này.");
        var next = slot with { Status = "REJECTED", RejectReason = reason!.Trim() };
        await SaveSlotAsync(ctx, next, cancellationToken);
        if (ctx.Pack is not null)
            await _packs.InsertEventAsync(ctx.Pack.Id, "REFERENCE_CANDIDATE_REJECTED", new
            {
                execution_id = executionId,
                reason = next.RejectReason,
                auto_retry = false,
                generate = false,
            }, actor, cancellationToken);
        return ToDto(ctx, CharacterReferenceGenerationRules.Evaluate(ctx.Input with { Confirm = false }),
            false, false, executionId, next.ArtifactId, next.ArtifactPath, "REJECTED", "REJECTED",
            "Đã từ chối ảnh này.", "Tạo ảnh tham chiếu",
            next.ProviderRequestId);
    }

    public async Task<CharacterReferenceGenerationDto> RegisterAsync(
        string characterId, Guid executionId, string actor, CancellationToken cancellationToken = default)
    {
        var ctx = await LoadAsync(characterId, new CharacterReferenceGenerationRequest(), cancellationToken);
        var slot = ResolveSlot(ctx, executionId)
            ?? throw new InvalidOperationException("REFERENCE_GENERATION_BLOCKED: Chưa có lần tạo ảnh này.");
        if (slot.Status is not ("ACCEPTED" or "REGISTERED"))
            throw new InvalidOperationException("REFERENCE_GENERATION_BLOCKED: Cần chấp nhận ảnh trước khi đưa vào bộ ảnh chuẩn.");
        if (ctx.Pack is null)
            throw new InvalidOperationException("REFERENCE_GENERATION_BLOCKED: " + CharacterReferenceGenerationRules.StaffMasterBlock);
        var bytes = string.IsNullOrWhiteSpace(slot.ArtifactPath) ? null : _store.Read(slot.ArtifactPath);
        if (bytes is null || bytes.Length < 32)
            throw new InvalidOperationException("REFERENCE_GENERATION_BLOCKED: " + CharacterReferenceGenerationRules.StaffGenerateFail);
        await AttachSlotAsync(ctx, slot, actor, cancellationToken);
        var next = slot with { Status = "REGISTERED" };
        await SaveSlotAsync(ctx, next, cancellationToken);
        await _packs.InsertEventAsync(ctx.Pack.Id, "REFERENCE_REGISTERED", new
        {
            execution_id = executionId,
            reference_type = slot.ReferenceType,
            auto_approve = false,
            generate = false,
        }, actor, cancellationToken);
        var fresh = await LoadAsync(characterId, new CharacterReferenceGenerationRequest(ReferenceType: slot.ReferenceType), cancellationToken);
        return ToDto(fresh, CharacterReferenceGenerationRules.Evaluate(fresh.Input with { Confirm = false }),
            false, false, executionId, next.ArtifactId, next.ArtifactPath, "REGISTERED", "ACCEPTED",
            "Đã đưa vào bộ ảnh chuẩn.", "Kiểm tra bộ ảnh",
            next.ProviderRequestId);
    }

    public async Task<CharacterReferenceGenerationDto> ValidatePackAsync(
        string characterId, Guid packId, string actor, Guid? executionId = null, CancellationToken cancellationToken = default)
    {
        var ctx = await LoadAsync(characterId, new CharacterReferenceGenerationRequest(), cancellationToken);
        if (executionId is Guid id && id != Guid.Empty)
            await AttachSlotAsync(ctx, ResolveSlot(ctx, id), actor, cancellationToken);
        await AttachLatestAcceptedAsync(ctx, actor, cancellationToken);
        CharacterReferencePackDto? pack = null;
        try
        {
            pack = await _crp.ValidateAsync(characterId, packId, actor, cancellationToken);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.StartsWith("REFERENCE_PACK_IDENTITY_CONFLICT", StringComparison.Ordinal)
            || ex.Message.StartsWith("REFERENCE_PACK_NOT_READY", StringComparison.Ordinal)
            || ex.Message.StartsWith("CRP_GATE_NOT_SATISFIED", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "REFERENCE_GENERATION_BLOCKED: Ảnh đã vào khung. Chưa kiểm tra xong bộ ảnh — xem lại 4 góc rồi thử lại.");
        }
        var fresh = await LoadAsync(characterId, new CharacterReferenceGenerationRequest(), cancellationToken);
        var already = pack?.Status is "VALIDATED" or "APPROVED" or "DIRECTOR_APPROVED" or "LOCKED";
        return ToDto(fresh, CharacterReferenceGenerationRules.Evaluate(fresh.Input with { Confirm = false }),
            false, false, executionId ?? fresh.Slot?.ExecutionId, fresh.Slot?.ArtifactId, fresh.Slot?.ArtifactPath,
            fresh.Slot?.Status ?? "REGISTERED", ReviewOf(fresh.Slot?.Status ?? "REGISTERED"),
            already ? "Bộ ảnh chuẩn đã kiểm tra. Đóng cửa sổ để xem 4 góc." : "Bộ ảnh chuẩn đã kiểm tra.",
            "Đóng để xem 4 góc");
    }

    public async Task<(byte[] Bytes, string Mime)?> ReadCandidateImageAsync(
        string characterId, Guid executionId, CancellationToken cancellationToken = default)
    {
        var ctx = await LoadAsync(characterId, new CharacterReferenceGenerationRequest(), cancellationToken);
        var slot = ResolveSlot(ctx, executionId);
        if (slot is null || string.IsNullOrWhiteSpace(slot.ArtifactPath)) return null;
        var bytes = _store.Read(slot.ArtifactPath);
        if (bytes is null || bytes.Length < 32) return null;
        return (bytes, KitVideoArtifactRules.DetectMime(bytes) ?? "image/jpeg");
    }

    private async Task<CharacterReferenceGenerationDto> PreviewAsync(
        string characterId, CharacterReferenceGenerationRequest request, string actor, bool persistPrepare,
        CancellationToken cancellationToken)
    {
        var ctx = await LoadAsync(characterId, request, cancellationToken);
        var gate = CharacterReferenceGenerationRules.Evaluate(ctx.Input);
        if (persistPrepare && ctx.Pack is not null)
        {
            await _packs.InsertEventAsync(ctx.Pack.Id, "REFERENCE_GENERATION_PREPARED", new
            {
                character_id = ctx.CharacterId,
                reference_type = ctx.ReferenceType,
                intent_sha256 = ctx.IntentSha,
                provider = ctx.Provider,
                generate = false,
            }, actor, cancellationToken);
        }
        return ToDto(ctx, gate, generate: false, gemini: false,
            executionId: ctx.Slot?.ExecutionId, artifactId: ctx.Slot?.ArtifactId, artifactPath: ctx.Slot?.ArtifactPath,
            candidateStatus: ctx.Slot?.Status, review: ReviewOf(ctx.Slot?.Status),
            providerRequestId: ctx.Slot?.ProviderRequestId);
    }

    private sealed record Slot(
        Guid ExecutionId,
        string Status,
        string ReferenceType,
        string? IntentSha256,
        string? MasterSha256,
        string? DnaSha256,
        string? PrpSha256,
        string? Fingerprint,
        string? Provider,
        string? ProviderRequestId,
        Guid? ArtifactId,
        string? ArtifactPath,
        string? ArtifactSha256,
        DateTimeOffset CreatedAt,
        string? RejectReason = null);

    private sealed record Ctx(
        string CharacterId,
        string CharacterName,
        string EraId,
        string ReferenceType,
        string? Provider,
        string MasterSha,
        string DnaSha,
        string PrpSha,
        CharacterReferencePackRepository.PackRow? Pack,
        CharacterReferenceGenerationRules.CharacterReferenceGenerationIntent? Intent,
        CharacterReferenceGenerationRules.CanonicalReferenceDescription? Canonical,
        string? IntentSha,
        string? Fingerprint,
        string? ExistingFingerprint,
        JsonObject Generation,
        Slot? Slot,
        CharacterReferenceGenerationRules.GateInput Input,
        int RequiredReady,
        int RequiredTotal,
        string? CrpStatus,
        bool CanUse,
        bool HasRegistered,
        string? CapabilityLevel,
        KitVideoMasterLockRepository.MasterRow? Master);

    private async Task<Ctx> LoadAsync(string characterId, CharacterReferenceGenerationRequest request, CancellationToken ct)
    {
        string id;
        try { id = CharacterReferencePackRules.NormalizeCharacterId(characterId); }
        catch (InvalidOperationException) { id = ""; }

        var era = string.IsNullOrWhiteSpace(request.EraId) ? "ERA-01" : request.EraId.Trim().ToUpperInvariant();
        var type = string.IsNullOrWhiteSpace(request.ReferenceType)
            ? "FULL_BODY"
            : CharacterReferencePackRules.NormalizeRefType(request.ReferenceType);
        var provider = string.IsNullOrWhiteSpace(request.Provider) ? null : request.Provider.Trim().ToUpperInvariant();

        var master = id.Length == 0 ? null : await _locks.GetByVersionAsync(id, era, "V1", ct);
        var dna = id.Length == 0 ? null : await _dna.GetByVersionAsync(id, era, "V1", ct);
        var prp = id.Length == 0 ? null : await _prp.GetByVersionAsync(id, era, "V1", ct);
        CharacterReferencePackGetDto? bundle = null;
        if (id.Length > 0)
        {
            try { bundle = await _crp.GetAsync(id, era, ct); }
            catch (InvalidOperationException) { bundle = null; }
        }
        var packs = id.Length == 0 ? [] : await _packs.ListByCharacterAsync(id, era, ct);
        var pack = packs.Where(r => r.Status != "SUPERSEDED")
            .OrderByDescending(r => CharacterReferencePackRules.VersionNumber(r.PackVersion))
            .ThenByDescending(r => r.CreatedAt)
            .FirstOrDefault();

        var masterLocked = master is not null && CharacterReferencePackRules.MasterLocked(master.Status);
        var dnaLocked = dna is not null && CharacterReferencePackRules.DnaLocked(dna.Status);
        var prpLocked = bundle?.PrpLocked == true
            || (prp is not null && string.Equals(prp.Status, "LOCKED", StringComparison.OrdinalIgnoreCase));
        var masterSha = master?.Sha256 ?? bundle?.Pack?.MasterSha256 ?? "";
        var dnaSha = !string.IsNullOrWhiteSpace(bundle?.Pack?.DnaSha256)
            ? bundle!.Pack!.DnaSha256
            : dna is null ? "" : KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(dna.SpecJson ?? "{}"));
        var prpSha = !string.IsNullOrWhiteSpace(bundle?.PrpSha256) ? bundle!.PrpSha256 : (prp?.PrpSha256 ?? "");
        var dnaMatch = dna is not null && CharacterReferencePackRules.SameSha(dna.MasterSha256, master?.Sha256 ?? masterSha);
        var identity = master is not null && dna is not null && masterLocked && dnaLocked && dnaMatch
            && CharacterReferencePackRules.SameTenant(id, master.CharacterId)
            && CharacterReferencePackRules.SameTenant(id, dna.CharacterId);
        var prpOk = prpLocked && CharacterReferencePackRules.ShaExists(prpSha);

        var items = pack is null ? [] : await _packs.ListItemsAsync(pack.Id, ct);
        var registered = items.Any(x =>
            string.Equals(x.RefType, type, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(x.ArtifactPath));
        var requiredReady = CharacterReferencePackRules.RequiredTypes.Count(t =>
            items.Any(x => string.Equals(x.RefType, t, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(x.ArtifactPath)));

        var generation = ReadGeneration(pack);
        MergeDiskSlots(generation, pack);
        var slot = LatestSlot(generation, type);
        var name = master?.CharacterName ?? dna?.CharacterName ?? "";

        CharacterReferenceGenerationRules.CharacterReferenceGenerationIntent? intent = null;
        CharacterReferenceGenerationRules.CanonicalReferenceDescription? canonical = null;
        string? intentSha = null;
        string? fingerprint = null;
        if (id.Length > 0 && CharacterReferencePackRules.ShaExists(masterSha)
            && CharacterReferencePackRules.ShaExists(dnaSha)
            && CharacterReferencePackRules.ShaExists(prpSha)
            && CharacterReferenceGenerationRules.IsSupportedType(type))
        {
            var dnaSpec = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(dna?.SpecJson) ? "{}" : dna!.SpecJson);
            var facts = string.Join(", ", new[]
            {
                CharacterReferencePackRules.DisplayAge(dnaSpec),
                CharacterReferencePackRules.DisplaySummary(dnaSpec),
            }.Where(x => x.Length > 0));
            intent = CharacterReferenceGenerationRules.BuildDefault(
                id, era, type, masterSha, dnaSha, prpSha,
                string.IsNullOrWhiteSpace(request.Style) ? null : request.Style,
                pack?.PackVersion);
            if (!string.IsNullOrWhiteSpace(request.Pose)) intent = intent with { Pose = request.Pose.Trim() };
            if (!string.IsNullOrWhiteSpace(request.Framing)) intent = intent with { Framing = request.Framing.Trim() };
            if (!string.IsNullOrWhiteSpace(request.Camera)) intent = intent with { Camera = request.Camera.Trim() };
            if (!string.IsNullOrWhiteSpace(request.Composition)) intent = intent with { Composition = request.Composition.Trim() };
            if (!string.IsNullOrWhiteSpace(request.Lighting)) intent = intent with { Lighting = request.Lighting.Trim() };
            if (!string.IsNullOrWhiteSpace(request.Background)) intent = intent with { Background = request.Background.Trim() };
            if (CharacterReferenceGenerationRules.IntentValid(intent))
            {
                canonical = CharacterReferenceGenerationRules.Canonical(intent, facts);
                intentSha = canonical.IntentSha256;
                if (!string.IsNullOrWhiteSpace(provider))
                {
                    var prior = pack is null
                        ? 0
                        : _store.ListReferenceCandidateMeta(pack.CharacterId, pack.EraId, pack.Id)
                            .Count(p => Path.GetFileName(p)
                                .StartsWith(type + "-", StringComparison.OrdinalIgnoreCase));
                    var attempt = request.Regenerate ? Math.Max(prior, 0) + 1 : Math.Max(prior, 1);
                    fingerprint = CharacterReferenceGenerationRules.ExecutionFingerprint(
                        intent, provider, CharacterReferenceGenerationRules.GenerationConfigForAttempt(attempt));
                }
            }
            else intent = null;
        }

        var profile = CharacterReferenceGenerationRules.ProfileOf(provider);
        var capabilityReady = provider is null
            || (profile is not null && CharacterReferenceGenerationRules.CapabilityAllowsDirector(profile));
        var capabilityLevel = profile is null ? null : CharacterReferenceGenerationRules.ReferenceCapability(profile);
        var existingFp = slot?.Fingerprint;
        var dup = fingerprint is not null
            && CharacterReferenceGenerationRules.DuplicatePolicy(fingerprint, existingFp) == "BLOCK_DUPLICATE";

        var input = new CharacterReferenceGenerationRules.GateInput(
            id.Length > 0 && (master is not null || dna is not null),
            identity,
            master is not null,
            masterLocked,
            dna is not null,
            dnaLocked,
            dnaMatch,
            prp is not null,
            prpOk,
            CharacterReferenceGenerationRules.IsSupportedType(type),
            intent is not null,
            canonical is not null,
            provider,
            capabilityReady,
            capabilityReady ? null : "PROVIDER_CAPABILITY_UNSUPPORTED",
            request.Confirm,
            dup);

        return new Ctx(
            id, name, era, type, provider, masterSha, dnaSha, prpSha, pack, intent, canonical, intentSha,
            fingerprint, existingFp, generation, slot, input, requiredReady, 4,
            bundle?.Pack?.Status ?? pack?.Status, bundle?.Pack?.CanUse ?? bundle?.CanUse ?? false,
            registered, capabilityLevel, master);
    }

    private IReadOnlyList<ImageGenerationReferenceBytes> LoadIdentityRefs(Ctx ctx)
    {
        var refs = new List<ImageGenerationReferenceBytes>();
        if (ctx.Master is not null && CharacterReferenceGenerationRules.MayUseAsIdentitySource(null, ctx.Master.ArtifactPath))
        {
            var bytes = _store.Read(ctx.Master.ArtifactPath);
            if (bytes is { Length: > 32 }
                && CharacterReferencePackRules.SameSha(KitVideoIntegrityRules.Sha256Hex(bytes), ctx.Master.Sha256))
            {
                refs.Add(new ImageGenerationReferenceBytes(
                    "MASTER_REFERENCE",
                    KitVideoArtifactRules.DetectMime(bytes) ?? "image/jpeg",
                    bytes,
                    ctx.Master.Sha256,
                    "MASTER_REFERENCE"));
            }
        }
        return refs;
    }

    private async Task SaveSlotAsync(Ctx ctx, Slot slot, CancellationToken ct)
    {
        if (ctx.Pack is null) return;
        var payload = new
        {
            executionId = slot.ExecutionId,
            status = slot.Status,
            referenceType = slot.ReferenceType,
            intentSha256 = slot.IntentSha256,
            masterSha256 = slot.MasterSha256,
            dnaSha256 = slot.DnaSha256,
            prpSha256 = slot.PrpSha256,
            fingerprint = slot.Fingerprint,
            provider = slot.Provider,
            providerRequestId = slot.ProviderRequestId,
            artifactId = slot.ArtifactId,
            artifactPath = slot.ArtifactPath,
            artifactSha256 = slot.ArtifactSha256,
            createdAt = slot.CreatedAt,
            rejectReason = slot.RejectReason,
        };
        _store.PersistReferenceCandidateMeta(
            ctx.CharacterId, ctx.EraId, ctx.Pack.Id, slot.ReferenceType, slot.ExecutionId,
            JsonSerializer.Serialize(payload));
        var root = ReadGeneration(ctx.Pack);
        var slots = root["slots"] as JsonObject ?? new JsonObject();
        slots[slot.ReferenceType] = JsonNode.Parse(JsonSerializer.Serialize(payload));
        root["slots"] = slots;
        var extra = MergeGeneration(ctx.Pack.ExtraJson, root);
        await _packs.SetExtraJsonAsync(ctx.Pack.Id, extra, ct);
        ctx.Pack.ExtraJson = extra;
    }

    private static JsonObject ReadGeneration(CharacterReferencePackRepository.PackRow? pack)
    {
        try
        {
            var extra = JsonNode.Parse(string.IsNullOrWhiteSpace(pack?.ExtraJson) ? "{}" : pack!.ExtraJson) as JsonObject
                ?? new JsonObject();
            return extra["reference_generation"] as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    private static string MergeGeneration(string? extraJson, JsonObject generation)
    {
        var extra = JsonNode.Parse(string.IsNullOrWhiteSpace(extraJson) ? "{}" : extraJson) as JsonObject
            ?? new JsonObject();
        extra["reference_generation"] = generation;
        return extra.ToJsonString();
    }

    private void MergeDiskSlots(JsonObject generation, CharacterReferencePackRepository.PackRow? pack)
    {
        if (pack is null) return;
        var slots = new JsonObject();
        foreach (var path in _store.ListReferenceCandidateMeta(pack.CharacterId, pack.EraId, pack.Id))
        {
            try
            {
                var node = JsonNode.Parse(File.ReadAllText(path));
                var type = node?["referenceType"]?.GetValue<string>();
                var id = node?["executionId"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(id)) continue;
                if (slots[type] is JsonObject)
                    continue;
                slots[type] = node;
            }
            catch (JsonException) { }
        }
        if (generation["slots"] is JsonObject existing)
        {
            foreach (var prop in existing)
            {
                if (slots[prop.Key] is null && prop.Value is not null)
                    slots[prop.Key] = prop.Value.DeepClone();
            }
        }
        generation["slots"] = slots;
    }

    private Slot? ResolveSlot(Ctx ctx, Guid executionId) =>
        FindSlot(ctx.Generation, executionId) ?? FindSlotOnDisk(ctx.Pack, executionId);

    private async Task AttachSlotAsync(Ctx ctx, Slot? slot, string actor, CancellationToken cancellationToken)
    {
        if (ctx.Pack is null || slot is null) return;
        if (slot.Status is not ("ACCEPTED" or "REGISTERED"))
            return;
        var bytes = string.IsNullOrWhiteSpace(slot.ArtifactPath) ? null : _store.Read(slot.ArtifactPath);
        if (bytes is null || bytes.Length < 32) return;
        await _crp.AttachItemAsync(ctx.CharacterId, ctx.Pack.Id, slot.ReferenceType, bytes,
            Path.GetFileName(slot.ArtifactPath), actor, cancellationToken);
    }

    private async Task AttachLatestAcceptedAsync(Ctx ctx, string actor, CancellationToken cancellationToken)
    {
        if (ctx.Generation["slots"] is not JsonObject slots) return;
        foreach (var prop in slots)
        {
            var slot = ReadSlot(prop.Value);
            if (slot is null || slot.Status is not ("ACCEPTED" or "REGISTERED")) continue;
            await AttachSlotAsync(ctx, slot, actor, cancellationToken);
        }
    }

    private static Slot? LatestSlot(JsonObject generation, string type)
    {
        if (generation["slots"] is not JsonObject slots) return null;
        if (!slots.TryGetPropertyValue(type, out var node) || node is null) return null;
        return ReadSlot(node);
    }

    private static Slot? FindSlot(JsonObject generation, Guid executionId)
    {
        if (generation["slots"] is not JsonObject slots) return null;
        foreach (var prop in slots)
        {
            var slot = ReadSlot(prop.Value);
            if (slot?.ExecutionId == executionId) return slot;
        }
        return null;
    }

    private Slot? FindSlotOnDisk(CharacterReferencePackRepository.PackRow? pack, Guid executionId)
    {
        if (pack is null) return null;
        foreach (var path in _store.ListReferenceCandidateMeta(pack.CharacterId, pack.EraId, pack.Id))
        {
            try
            {
                var slot = ReadSlot(JsonNode.Parse(File.ReadAllText(path)));
                if (slot?.ExecutionId == executionId) return slot;
            }
            catch (JsonException) { }
        }
        return null;
    }

    private static Slot? ReadSlot(JsonNode? node)
    {
        if (node is null) return null;
        try
        {
            var el = JsonSerializer.Deserialize<JsonElement>(node.ToJsonString());
            var id = ReadGuid(el, "executionId");
            if (id is null) return null;
            return new Slot(
                id.Value,
                Read(el, "status"),
                Read(el, "referenceType"),
                Read(el, "intentSha256"),
                Read(el, "masterSha256"),
                Read(el, "dnaSha256"),
                Read(el, "prpSha256"),
                Read(el, "fingerprint"),
                Read(el, "provider"),
                NullIfEmpty(Read(el, "providerRequestId")),
                ReadGuid(el, "artifactId"),
                NullIfEmpty(Read(el, "artifactPath")),
                NullIfEmpty(Read(el, "artifactSha256")),
                el.TryGetProperty("createdAt", out var at) && at.ValueKind == JsonValueKind.String
                    && DateTimeOffset.TryParse(at.GetString(), out var parsed) ? parsed : DateTimeOffset.UtcNow,
                NullIfEmpty(Read(el, "rejectReason")));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private CharacterReferenceGenerationDto ToDto(
        Ctx ctx,
        CharacterReferenceGenerationRules.Gate gate,
        bool generate,
        bool gemini,
        Guid? executionId = null,
        Guid? artifactId = null,
        string? artifactPath = null,
        string? candidateStatus = null,
        string? review = null,
        string? staff = null,
        string? next = null,
        string? providerRequestId = null)
    {
        var slotState = CharacterReferenceGenerationRules.SlotState(
            ctx.HasRegistered, candidateStatus ?? ctx.Slot?.Status, ctx.CrpStatus, ctx.RequiredReady >= 4);
        var technical = new CharacterReferenceGenerationTechnicalDto(
            ctx.CharacterId, ctx.EraId, ctx.ReferenceType, ctx.IntentSha, ctx.MasterSha, ctx.DnaSha, ctx.PrpSha,
            ctx.Provider ?? ctx.Slot?.Provider, providerRequestId ?? ctx.Slot?.ProviderRequestId,
            executionId ?? ctx.Slot?.ExecutionId, artifactId ?? ctx.Slot?.ArtifactId,
            ctx.Fingerprint ?? ctx.Slot?.Fingerprint, ctx.CapabilityLevel, gate.Status, gate.Code);
        return new CharacterReferenceGenerationDto(
            CharacterReferenceGenerationRules.DocumentId,
            ctx.CharacterId,
            ctx.CharacterName,
            ctx.EraId,
            ctx.ReferenceType,
            slotState,
            gate.Status,
            gate.Code,
            staff ?? (slotState == "READY_FOR_DIRECTOR"
                ? CharacterReferenceGenerationRules.StaffWaitDirector
                : gate.StaffMessage),
            next ?? (slotState == "READY_FOR_DIRECTOR"
                ? CharacterReferenceGenerationRules.StaffWaitDirector
                : gate.MayCallProvider
                    ? "Bắt đầu tạo"
                    : CharacterReferenceGenerationRules.StaffNextGenerate),
            gate.AuthorityValid,
            gate.ProviderSelected,
            gate.CapabilityReady,
            gate.GenerationAllowed,
            gate.MayCallProvider,
            gate.Code == "CONFIRM_REQUIRED",
            generate,
            gemini,
            false,
            false,
            false,
            false,
            ctx.Provider ?? ctx.Slot?.Provider,
            ctx.CapabilityLevel,
            ctx.IntentSha,
            ctx.MasterSha,
            ctx.DnaSha,
            ctx.PrpSha,
            ctx.Fingerprint ?? ctx.Slot?.Fingerprint,
            executionId ?? ctx.Slot?.ExecutionId,
            artifactId ?? ctx.Slot?.ArtifactId,
            artifactPath ?? ctx.Slot?.ArtifactPath,
            providerRequestId ?? ctx.Slot?.ProviderRequestId,
            candidateStatus ?? ctx.Slot?.Status,
            review,
            ctx.RequiredReady,
            ctx.RequiredTotal,
            ctx.CrpStatus,
            ctx.CanUse,
            false,
            false,
            technical);
    }

    private static string ReviewOf(string? status) => (status ?? "").ToUpperInvariant() switch
    {
        "READY_FOR_DIRECTOR" or "GENERATED" => "PENDING",
        "ACCEPTED" or "REGISTERED" => "ACCEPTED",
        "REJECTED" => "REJECTED",
        _ => status ?? "",
    };

    private string LiveSha(string path)
    {
        var bytes = _store.Read(path);
        return bytes is { Length: > 32 } ? KitVideoIntegrityRules.Sha256Hex(bytes) : "";
    }

    private static string Read(JsonElement el, string name) =>
        el.TryGetProperty(name, out var n) && n.ValueKind == JsonValueKind.String ? n.GetString()?.Trim() ?? "" : "";

    private static Guid? ReadGuid(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var n)) return null;
        if (n.ValueKind == JsonValueKind.String && Guid.TryParse(n.GetString(), out var g)) return g;
        return n.ValueKind == JsonValueKind.String ? null : n.TryGetGuid(out var id) ? id : null;
    }

    private static string? NullIfEmpty(string? v) => string.IsNullOrWhiteSpace(v) ? null : v;
}
