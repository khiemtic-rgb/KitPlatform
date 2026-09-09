using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class CharacterReferenceAutoGenerationV2Service : ICharacterReferenceAutoGenerationV2Service
{
    private readonly CharacterReferencePackRepository _packs;
    private readonly KitVideoCharacterDnaRepository _dna;
    private readonly KitVideoMasterLockRepository _locks;
    private readonly KitVideoProductionReferencePackRepository _prp;
    private readonly KitVideoArtifactStore _store;
    private readonly ICharacterReferencePackService _crp;
    private readonly ICharacterReferenceGenerationProvider _provider;
    private readonly CharacterAuthorityStore _authority;

    public CharacterReferenceAutoGenerationV2Service(
        CharacterReferencePackRepository packs,
        KitVideoCharacterDnaRepository dna,
        KitVideoMasterLockRepository locks,
        KitVideoProductionReferencePackRepository prp,
        KitVideoArtifactStore store,
        ICharacterReferencePackService crp,
        ICharacterReferenceGenerationProvider provider,
        CharacterAuthorityStore authority)
    {
        _packs = packs;
        _dna = dna;
        _locks = locks;
        _prp = prp;
        _store = store;
        _crp = crp;
        _provider = provider;
        _authority = authority;
    }

    public IReadOnlyList<string> RunRegression() => CharacterReferenceAutoGenerationV2Regression.Run();

    public Task<CharacterReferenceSetDto> GetAsync(
        string characterId, string? provider, string eraId = "ERA-01",
        CancellationToken cancellationToken = default) =>
        PreviewAsync(characterId, new CharacterReferenceSetRequestDto(false, provider, eraId), "director", false, cancellationToken);

    public Task<CharacterReferenceSetDto> PrepareAsync(
        string characterId, CharacterReferenceSetRequestDto request, string actor,
        CancellationToken cancellationToken = default) =>
        PreviewAsync(characterId, request with { Confirm = false }, actor, true, cancellationToken);

    public async Task<CharacterReferenceSetDto> ExecuteAsync(
        string characterId, CharacterReferenceSetRequestDto request, string actor,
        CancellationToken cancellationToken = default)
    {
        var ctx = await LoadAsync(characterId, request, cancellationToken);
        if (CharacterReferenceRegenerationV1Rules.IsRejected(ctx.CrpStatus)
            || CharacterReferenceRegenerationV1Rules.IsGenerating(ctx.CrpStatus))
        {
            return ToDto(ctx, new CharacterReferenceAutoGenerationV2Rules.Gate(
                "BLOCKED", "CRP_NOT_REJECTED", CharacterReferenceRegenerationV1Rules.StaffNotRejected,
                true, false, false, false, true, false), generate: false, called: false);
        }
        var gate = CharacterReferenceAutoGenerationV2Rules.Evaluate(ctx.Input);
        if (!CharacterReferenceAutoGenerationV2Rules.MayCallProvider(gate))
            return ToDto(ctx, gate, generate: false, called: false);

        var officialMaster = ctx.CharacterId.Length == 0
            ? null
            : await _locks.GetByVersionAsync(ctx.CharacterId, ctx.EraId, "V1", cancellationToken);
        if (officialMaster is null)
            return await ExecuteWorkspaceAsync(ctx, gate, actor, cancellationToken);

        var pack = ctx.Pack;
        if (pack is null)
        {
            var created = await _crp.CreateAsync(ctx.CharacterId, actor, ctx.EraId, cancellationToken);
            pack = await _packs.GetByIdAsync(created.Id, cancellationToken)
                ?? throw new InvalidOperationException("REFERENCE_GENERATION_BLOCKED: " + CharacterReferenceAutoGenerationV2Rules.StaffMaster);
        }

        var setId = Guid.NewGuid();
        await _packs.InsertEventAsync(pack.Id, "REFERENCE_SET_REQUESTED", new
        {
            document = CharacterReferenceAutoGenerationV2Rules.DocumentId,
            character_id = ctx.CharacterId,
            fingerprint = ctx.Fingerprint,
            provider = ctx.Provider,
            confirm = true,
            first_real_production = false,
            generate_shot = false,
            generate_video = false,
        }, actor, cancellationToken);

        var refs = LoadIdentityRefs(ctx);
        var views = CharacterReferenceAutoGenerationV2Rules.RequiredTypes.Select(type =>
        {
            var intent = CharacterReferenceGenerationRules.BuildDefault(
                ctx.CharacterId, ctx.EraId, type, ctx.MasterSha, ctx.DnaSha, ctx.PrpSha, packVersion: pack.PackVersion);
            var canonical = CharacterReferenceGenerationRules.Canonical(intent, ctx.IdentityFacts);
            return CharacterReferenceGenerationRules.CompileProviderRequest(canonical, refs, type) is var compiled
                ? new CharacterReferenceViewRequest(type, compiled.Prompt, compiled.AspectRatio, compiled.References)
                : new CharacterReferenceViewRequest(type, canonical.Text, type == "FULL_BODY" ? "3:4" : "1:1", refs);
        }).ToList();

        var result = await _provider.GenerateSetAsync(
            new CharacterReferenceSetRequest(ctx.CharacterId, ctx.EraId, ctx.MasterSha, ctx.DnaSha, ctx.PrpSha, views),
            cancellationToken);
        var gemini = string.Equals(result.ProviderCalled ? _provider.ProviderId : "", "GEMINI", StringComparison.OrdinalIgnoreCase);

        if (!result.Succeeded && result.Views.All(v => !v.Succeeded))
        {
            await SaveSetAsync(pack, setId, ctx, result, "FAILED", actor, cancellationToken);
            return ToDto(ctx with { Pack = pack }, gate, generate: false, called: true, gemini,
                setId, 0, CharacterReferenceAutoGenerationV2Rules.RequiredTypes.ToList(),
                CharacterReferenceAutoGenerationV2Rules.StaffFail);
        }

        var attached = 0;
        foreach (var view in result.Views.Where(v => v.Succeeded && v.Bytes is { Length: > 32 }))
        {
            if (CharacterReferenceAutoGenerationV2Rules.RejectHistoricalStill(null, view.ReferenceType))
                continue;
            await _crp.AttachItemAsync(ctx.CharacterId, pack.Id, view.ReferenceType, view.Bytes!, $"{view.ReferenceType}.png", actor, cancellationToken);
            attached++;
        }

        var items = await _packs.ListItemsAsync(pack.Id, cancellationToken);
        var present = items
            .Where(i => !string.IsNullOrWhiteSpace(i.ArtifactPath))
            .Select(i => i.RefType)
            .ToList();
        var coverage = CharacterReferenceAutoGenerationV2Rules.RequiredTypes.Count(t =>
            present.Contains(t, StringComparer.OrdinalIgnoreCase));
        var missing = CharacterReferenceAutoGenerationV2Rules.RequiredTypes
            .Where(t => !present.Contains(t, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var complete = coverage == 4 && missing.Count == 0;
        await SaveSetAsync(pack, setId, ctx, result, complete ? "READY_FOR_DIRECTOR" : "INCOMPLETE", actor, cancellationToken);
        await _packs.InsertEventAsync(pack.Id, complete ? "REFERENCE_SET_READY_FOR_DIRECTOR" : "REFERENCE_SET_INCOMPLETE", new
        {
            set_id = setId,
            coverage,
            auto_approve = false,
            auto_lock = false,
            generate = true,
            first_real_production = false,
        }, actor, cancellationToken);

        var fresh = await LoadAsync(ctx.CharacterId, request with { Confirm = false }, cancellationToken);
        return ToDto(fresh, gate, generate: complete, called: true, gemini, setId, coverage, missing,
            complete ? CharacterReferenceAutoGenerationV2Rules.StaffPending : CharacterReferenceAutoGenerationV2Rules.StaffIncomplete);
    }

    private async Task<CharacterReferenceSetDto> PreviewAsync(
        string characterId, CharacterReferenceSetRequestDto request, string actor, bool persistPrepare,
        CancellationToken ct)
    {
        var ctx = await LoadAsync(characterId, request, ct);
        var gate = CharacterReferenceAutoGenerationV2Rules.Evaluate(ctx.Input);
        if (persistPrepare && ctx.Pack is not null)
        {
            await _packs.InsertEventAsync(ctx.Pack.Id, "REFERENCE_SET_PREPARED", new
            {
                document = CharacterReferenceAutoGenerationV2Rules.DocumentId,
                confirm = false,
                generate = false,
            }, actor, ct);
        }
        return ToDto(ctx, gate, generate: false, called: false);
    }

    private sealed record Ctx(
        string CharacterId,
        string CharacterName,
        string EraId,
        string? Provider,
        string MasterSha,
        string DnaSha,
        string PrpSha,
        string IdentityFacts,
        CharacterReferencePackRepository.PackRow? Pack,
        string? Fingerprint,
        CharacterReferenceAutoGenerationV2Rules.GateInput Input,
        int Coverage,
        IReadOnlyList<string> Missing,
        string? CrpStatus,
        bool CanUse,
        bool CrpLockedComplete,
        KitVideoMasterLockRepository.MasterRow? Master);

    private async Task<Ctx> LoadAsync(string characterId, CharacterReferenceSetRequestDto request, CancellationToken ct)
    {
        string id;
        try { id = CharacterReferencePackRules.NormalizeCharacterId(characterId); }
        catch (InvalidOperationException) { id = ""; }

        var era = string.IsNullOrWhiteSpace(request.EraId) ? "ERA-01" : request.EraId.Trim().ToUpperInvariant();
        var provider = string.IsNullOrWhiteSpace(request.Provider) ? null : request.Provider.Trim().ToUpperInvariant();

        var master = id.Length == 0 ? null : await _locks.GetByVersionAsync(id, era, "V1", ct);
        var dna = id.Length == 0 ? null : await _dna.GetByVersionAsync(id, era, "V1", ct);
        var prp = id.Length == 0 ? null : await _prp.GetByVersionAsync(id, era, "V1", ct);
        if (master is null && dna is null && prp is null && id.Length > 0)
        {
            var snap = await _authority.ReadAsync(id, era, ct);
            master = _authority.ToOfficialMaster(snap);
            dna = _authority.ToOfficialDna(snap);
            prp = _authority.ToOfficialPrp(snap);
        }
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
        var prpMaster = !CharacterReferencePackRules.ShaExists(prpSha) || !CharacterReferencePackRules.ShaExists(masterSha)
            || CharacterReferencePackRules.SameSha(prp?.MasterSha256 ?? masterSha, masterSha);
        var prpDna = !CharacterReferencePackRules.ShaExists(prpSha) || !CharacterReferencePackRules.ShaExists(dnaSha)
            || CharacterReferencePackRules.SameSha(prp?.DnaSha256 ?? dnaSha, dnaSha);

        var items = pack is null ? [] : await _packs.ListItemsAsync(pack.Id, ct);
        var present = items.Where(x => !string.IsNullOrWhiteSpace(x.ArtifactPath)).Select(x => x.RefType).ToList();
        var workspaceCrp = pack is null && id.Length > 0
            ? await _authority.ReadCrpAsync(id, era, ct)
            : null;
        if (present.Count == 0 && workspaceCrp is { Items.Count: > 0 })
            present = workspaceCrp.Items.Select(i => i.Type).ToList();
        var coverage = CharacterReferenceAutoGenerationV2Rules.RequiredTypes.Count(t =>
            present.Contains(t, StringComparer.OrdinalIgnoreCase));
        var missing = CharacterReferenceAutoGenerationV2Rules.RequiredTypes
            .Where(t => !present.Contains(t, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var crpStatus = bundle?.Pack?.Status ?? pack?.Status ?? workspaceCrp?.Status;
        var canUse = bundle?.Pack?.CanUse ?? bundle?.CanUse ?? workspaceCrp?.CanUse ?? false;
        var lockedComplete = CharacterReferenceAutoGenerationV2Rules.CrpAlreadyComplete(crpStatus, canUse, coverage);

        var fingerprint = CharacterReferencePackRules.ShaExists(masterSha)
            && CharacterReferencePackRules.ShaExists(dnaSha)
            && CharacterReferencePackRules.ShaExists(prpSha)
            && id.Length > 0
            ? CharacterReferenceAutoGenerationV2Rules.ExecutionFingerprint(id, era, masterSha, dnaSha, prpSha)
            : null;
        var existing = ReadSetFingerprint(pack);
        var dup = fingerprint is not null
            && CharacterReferenceAutoGenerationV2Rules.DuplicatePolicy(fingerprint, existing) == "BLOCK_DUPLICATE";

        var capabilityReady = provider is null || CharacterReferenceAutoGenerationV2Rules.CapabilityAllows(provider);
        var dnaSpec = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(dna?.SpecJson) ? "{}" : dna!.SpecJson);
        var facts = string.Join(", ", new[]
        {
            CharacterReferencePackRules.DisplayAge(dnaSpec),
            CharacterReferencePackRules.DisplaySummary(dnaSpec),
        }.Where(x => x.Length > 0));

        var input = new CharacterReferenceAutoGenerationV2Rules.GateInput(
            id.Length > 0,
            master is not null,
            masterLocked,
            dna is not null,
            dnaLocked,
            prp is not null || prpLocked,
            prpLocked,
            CharacterReferencePackRules.ShaExists(masterSha),
            CharacterReferencePackRules.ShaExists(dnaSha),
            CharacterReferencePackRules.ShaExists(prpSha),
            dnaMatch,
            prpMaster,
            prpDna,
            lockedComplete,
            provider,
            capabilityReady,
            capabilityReady ? null : "PROVIDER_CAPABILITY_UNSUPPORTED",
            request.Confirm,
            dup,
            false);

        return new Ctx(
            id, master?.CharacterName ?? dna?.CharacterName ?? "", era, provider,
            masterSha, dnaSha, prpSha, facts, pack, fingerprint, input, coverage, missing,
            crpStatus, canUse, lockedComplete, master);
    }

    private async Task<CharacterReferenceSetDto> ExecuteWorkspaceAsync(
        Ctx ctx, CharacterReferenceAutoGenerationV2Rules.Gate gate, string actor, CancellationToken cancellationToken)
    {
        var snap = await _authority.ReadAsync(ctx.CharacterId, ctx.EraId, cancellationToken)
            ?? throw new InvalidOperationException("REFERENCE_GENERATION_BLOCKED: " + CharacterReferenceAutoGenerationV2Rules.StaffMaster);
        var setId = Guid.NewGuid();
        var refs = LoadIdentityRefs(ctx);
        var views = CharacterReferenceAutoGenerationV2Rules.RequiredTypes.Select(type =>
        {
            var intent = CharacterReferenceGenerationRules.BuildDefault(
                ctx.CharacterId, ctx.EraId, type, ctx.MasterSha, ctx.DnaSha, ctx.PrpSha, packVersion: "V1");
            var canonical = CharacterReferenceGenerationRules.Canonical(intent, ctx.IdentityFacts);
            return CharacterReferenceGenerationRules.CompileProviderRequest(canonical, refs, type) is var compiled
                ? new CharacterReferenceViewRequest(type, compiled.Prompt, compiled.AspectRatio, compiled.References)
                : new CharacterReferenceViewRequest(type, canonical.Text, type == "FULL_BODY" ? "3:4" : "1:1", refs);
        }).ToList();

        var result = await _provider.GenerateSetAsync(
            new CharacterReferenceSetRequest(ctx.CharacterId, ctx.EraId, ctx.MasterSha, ctx.DnaSha, ctx.PrpSha, views),
            cancellationToken);
        var gemini = string.Equals(result.ProviderCalled ? _provider.ProviderId : "", "GEMINI", StringComparison.OrdinalIgnoreCase);

        var stored = new List<CharacterAuthorityStore.CrpItem>();
        foreach (var view in result.Views.Where(v => v.Succeeded && v.Bytes is { Length: > 32 }))
        {
            if (CharacterReferenceAutoGenerationV2Rules.RejectHistoricalStill(null, view.ReferenceType))
                continue;
            var persist = _store.PersistReference(ctx.CharacterId, ctx.EraId, snap.AssetId, view.ReferenceType, view.Bytes!);
            stored.Add(new CharacterAuthorityStore.CrpItem(
                view.ReferenceType,
                persist.Path,
                KitVideoIntegrityRules.Sha256Hex(persist.Bytes)));
        }

        var coverage = CharacterReferenceAutoGenerationV2Rules.RequiredTypes.Count(t =>
            stored.Any(i => string.Equals(i.Type, t, StringComparison.OrdinalIgnoreCase)));
        var missing = CharacterReferenceAutoGenerationV2Rules.RequiredTypes
            .Where(t => !stored.Any(i => string.Equals(i.Type, t, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var complete = coverage == 4 && missing.Count == 0;
        var crpSha = stored.Count == 0
            ? null
            : KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(
                string.Join("|", stored.OrderBy(i => i.Type).Select(i => $"{i.Type}:{i.Sha256}"))));
        var prior = await _authority.ReadCrpAsync(ctx.CharacterId, ctx.EraId, cancellationToken);
        await _authority.WriteCrpAsync(snap.AssetId, new CharacterAuthorityStore.CrpSnapshot(
            ctx.CharacterId, ctx.EraId,
            complete ? "READY_FOR_DIRECTOR" : "INCOMPLETE",
            coverage, false, crpSha, stored,
            prior?.Version ?? "V1",
            setId.ToString(),
            History: prior?.History), cancellationToken);

        return ToDto(
            ctx with
            {
                Coverage = coverage,
                Missing = missing,
                CrpStatus = complete ? "READY_FOR_DIRECTOR" : "INCOMPLETE",
                CanUse = false,
            },
            gate, generate: false, called: true, gemini, setId, coverage, missing,
            complete ? CharacterReferenceAutoGenerationV2Rules.PendingReview : CharacterReferenceAutoGenerationV2Rules.StaffFail);
    }

    private IReadOnlyList<ImageGenerationReferenceBytes> LoadIdentityRefs(Ctx ctx)
    {
        var refs = new List<ImageGenerationReferenceBytes>();
        if (ctx.Master is null) return refs;
        if (CharacterReferenceAutoGenerationV2Rules.RejectHistoricalStill(ctx.Master.Id.ToString(), ctx.Master.ArtifactPath))
            return refs;
        if (!CharacterReferenceGenerationRules.MayUseAsIdentitySource(null, ctx.Master.ArtifactPath))
            return refs;
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
        return refs;
    }

    private async Task SaveSetAsync(
        CharacterReferencePackRepository.PackRow pack,
        Guid setId,
        Ctx ctx,
        CharacterReferenceSetResult result,
        string status,
        string actor,
        CancellationToken ct)
    {
        var extra = JsonNode.Parse(string.IsNullOrWhiteSpace(pack.ExtraJson) ? "{}" : pack.ExtraJson) as JsonObject
            ?? new JsonObject();
        var gen = extra["reference_generation"] as JsonObject ?? new JsonObject();
        gen["set"] = JsonNode.Parse(JsonSerializer.Serialize(new
        {
            document = CharacterReferenceAutoGenerationV2Rules.DocumentId,
            setId,
            status,
            fingerprint = ctx.Fingerprint,
            provider = ctx.Provider,
            providerRequestId = result.ProviderRequestId,
            coverage = result.Views.Count(v => v.Succeeded),
            directorReview = CharacterReferenceAutoGenerationV2Rules.PendingReview,
            approved = false,
            locked = false,
            actor,
        }));
        extra["reference_generation"] = gen;
        var json = extra.ToJsonString();
        await _packs.SetExtraJsonAsync(pack.Id, json, ct);
        pack.ExtraJson = json;
    }

    private static string? ReadSetFingerprint(CharacterReferencePackRepository.PackRow? pack)
    {
        try
        {
            var extra = JsonNode.Parse(string.IsNullOrWhiteSpace(pack?.ExtraJson) ? "{}" : pack!.ExtraJson) as JsonObject;
            var set = extra?["reference_generation"]?["set"] as JsonObject;
            var status = set?["status"]?.GetValue<string>();
            if (status is not ("READY_FOR_DIRECTOR" or "SUCCEEDED")) return null;
            return set?["fingerprint"]?.GetValue<string>();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private CharacterReferenceSetDto ToDto(
        Ctx ctx,
        CharacterReferenceAutoGenerationV2Rules.Gate gate,
        bool generate,
        bool called,
        bool gemini = false,
        Guid? setId = null,
        int? coverage = null,
        IReadOnlyList<string>? missing = null,
        string? staff = null)
    {
        var ready = coverage ?? ctx.Coverage;
        var miss = missing ?? ctx.Missing;
        var assets = CharacterReferenceAutoGenerationV2Rules.RequiredTypes
            .Select(t => new CharacterReferenceSetAssetDto(t, null, null, null, !miss.Contains(t, StringComparer.OrdinalIgnoreCase)))
            .ToList();
        var consistency = CharacterReferenceAutoGenerationV2Rules.ConsistencyPass(
            ctx.CharacterId, assets.Where(a => a.Present).Select(a => a.Type).ToList(), ready == 4, ready == 4);
        return new CharacterReferenceSetDto(
            CharacterReferenceAutoGenerationV2Rules.DocumentId,
            ctx.CharacterId,
            ctx.CharacterName,
            ctx.EraId,
            gate.Status,
            gate.Code,
            staff ?? gate.StaffMessage,
            ctx.CrpLockedComplete
                ? CharacterReferenceAutoGenerationV2Rules.StaffComplete
                : generate || ready == 4
                    ? "Duyệt bộ ảnh"
                    : CharacterReferenceAutoGenerationV2Rules.StaffCreate,
            gate.AuthorityValid,
            gate.ProviderSelected,
            gate.CapabilityReady,
            gate.GenerationAllowed,
            gate.MayCallProvider,
            gate.Code == "CONFIRMATION_REQUIRED" || (!requestConfirm(ctx) && gate.Status != "READY"),
            generate,
            called,
            gemini,
            false,
            false,
            false,
            false,
            ctx.Provider,
            ctx.Provider is null ? null : CharacterReferenceAutoGenerationV2Rules.SetCapability(ctx.Provider),
            setId,
            ctx.Pack?.Id,
            ctx.CrpStatus,
            ready,
            4,
            miss,
            assets,
            CharacterReferenceAutoGenerationV2Rules.PendingReview,
            false,
            string.Equals(ctx.CrpStatus, "LOCKED", StringComparison.OrdinalIgnoreCase),
            ctx.CanUse && string.Equals(ctx.CrpStatus, "LOCKED", StringComparison.OrdinalIgnoreCase),
            consistency,
            false,
            false,
            ctx.Fingerprint,
            ctx.MasterSha,
            ctx.DnaSha,
            ctx.PrpSha);
    }

    private static bool requestConfirm(Ctx ctx) => ctx.Input.Confirm;
}
