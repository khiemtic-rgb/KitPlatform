using System.Linq;
using System.Text;
using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class CharacterReferenceRegenerationV1Service : ICharacterReferenceRegenerationV1Service
{
    private readonly ICharacterAuthorityInitializationV1Service _init;
    private readonly ICharacterReferencePackService _crp;
    private readonly CharacterAuthorityStore _authority;
    private readonly KitVideoArtifactStore _store;
    private readonly ICharacterReferenceGenerationProvider _provider;

    public CharacterReferenceRegenerationV1Service(
        ICharacterAuthorityInitializationV1Service init,
        ICharacterReferencePackService crp,
        CharacterAuthorityStore authority,
        KitVideoArtifactStore store,
        ICharacterReferenceGenerationProvider provider)
    {
        _init = init;
        _crp = crp;
        _authority = authority;
        _store = store;
        _provider = provider;
    }

    public IReadOnlyList<string> RunRegression() => CharacterReferenceRegenerationV1Regression.Run();

    public Task<CharacterReferenceRegenerationDto> GetAsync(
        string characterId, string? provider = null, string eraId = "ERA-01",
        CancellationToken cancellationToken = default) =>
        PreviewAsync(characterId, new CharacterReferenceRegenerateRequestDto(false, provider, eraId), "GET", cancellationToken);

    public Task<CharacterReferenceRegenerationDto> PrepareAsync(
        string characterId, CharacterReferenceRegenerateRequestDto request, string actor,
        CancellationToken cancellationToken = default) =>
        PreviewAsync(characterId, request with { Confirm = false }, "REGENERATE", cancellationToken);

    public Task<CharacterReferenceRegenerationDto> HistoryAsync(
        string characterId, string eraId = "ERA-01",
        CancellationToken cancellationToken = default) =>
        GetAsync(characterId, null, eraId, cancellationToken);

    public async Task<CharacterReferenceRegenerationDto> RejectAsync(
        string characterId, CharacterReferenceRejectRequestDto request, string actor,
        CancellationToken cancellationToken = default)
    {
        var era = string.IsNullOrWhiteSpace(request.EraId) ? "ERA-01" : request.EraId!;
        var ctx = await LoadAsync(characterId, era, null, cancellationToken);
        var reason = (request.RejectReasonText ?? "").Trim();
        if (string.IsNullOrWhiteSpace(reason) && !string.IsNullOrWhiteSpace(request.RejectReasonCode))
            reason = CharacterReferenceRegenerationV1Rules.RejectReasonLabel(request.RejectReasonCode);
        var gate = CharacterReferenceRegenerationV1Rules.Evaluate(ctx.Input with
        {
            Action = "REJECT",
            RejectReasonOk = CharacterReferenceRegenerationV1Rules.RejectReasonValid(reason),
        });
        if (gate.Code is not null)
            throw new InvalidOperationException($"{gate.Code}: {gate.StaffMessage}");
        if (ctx.OfficialLocked)
            throw new InvalidOperationException("CRP_LOCKED: " + CharacterReferenceRegenerationV1Rules.StaffLocked);
        if (ctx.AssetId == Guid.Empty || ctx.Crp is null)
            throw new InvalidOperationException("CRP_NOT_FOUND: " + CharacterReferenceRegenerationV1Rules.StaffMissing);

        var now = DateTimeOffset.UtcNow.ToString("o");
        await _authority.WriteCrpAsync(ctx.AssetId, ctx.Crp with
        {
            Status = CharacterReferenceRegenerationV1Rules.Rejected,
            CanUse = false,
            RejectReasonCode = string.IsNullOrWhiteSpace(request.RejectReasonCode) ? "OTHER" : request.RejectReasonCode!.Trim().ToUpperInvariant(),
            RejectReasonText = reason,
            RejectedBy = actor,
            RejectedAt = now,
            MasterSha = ctx.MasterSha,
            DnaSha = ctx.DnaSha,
            PrpSha = ctx.PrpSha,
        }, cancellationToken);
        var verify = await _authority.ReadCrpAsync(ctx.CharacterId, era, cancellationToken);
        if (!CharacterReferenceRegenerationV1Rules.IsRejected(verify?.Status)
            || !string.Equals(verify?.Version, ctx.Crp.Version, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("CRP_NOT_REJECTED: Reject không ghi được trạng thái hiện tại.");
        return await GetAsync(ctx.CharacterId, null, era, cancellationToken);
    }

    public async Task<CharacterReferenceRegenerationDto> ExecuteAsync(
        string characterId, CharacterReferenceRegenerateRequestDto request, string actor,
        CancellationToken cancellationToken = default)
    {
        var era = string.IsNullOrWhiteSpace(request.EraId) ? "ERA-01" : request.EraId!;
        var ctx = await LoadAsync(characterId, era, request.Provider, cancellationToken);
        if (ctx.OfficialLocked)
            throw new InvalidOperationException("CRP_LOCKED: " + CharacterReferenceRegenerationV1Rules.StaffLocked);

        var nextVersion = CharacterReferenceRegenerationV1Rules.NextVersion(ctx.Crp?.Version);
        var fingerprint = CharacterReferenceRegenerationV1Rules.ExecutionFingerprint(
            ctx.CharacterId, era, ctx.MasterSha, ctx.DnaSha, ctx.PrpSha, nextVersion, request.Provider);
        var successHits = (ctx.Crp?.History ?? [])
            .Select(h => h.Fingerprint)
            .Append(ctx.Crp?.Fingerprint)
            .Where(f => !string.IsNullOrWhiteSpace(f));
        var dup = successHits.Any(f =>
            CharacterReferenceRegenerationV1Rules.DuplicatePolicy(fingerprint, f) == "BLOCK_DUPLICATE"
            && (ctx.Crp?.Status == CharacterReferenceRegenerationV1Rules.PendingReview
                || (ctx.Crp?.History ?? []).Any(h =>
                    h.Fingerprint == f && h.Status == CharacterReferenceRegenerationV1Rules.PendingReview)));
        var gate = CharacterReferenceRegenerationV1Rules.Evaluate(ctx.Input with
        {
            Action = "EXECUTE",
            DirectorProvider = request.Provider,
            Confirm = request.Confirm,
            DuplicateSuccess = CharacterReferenceRegenerationV1Rules.DuplicatePolicy(fingerprint, ctx.Crp?.Fingerprint) == "BLOCK_DUPLICATE"
                && ctx.Crp?.Version == nextVersion,
        });
        if (!CharacterReferenceRegenerationV1Rules.MayCallProvider(gate))
            return ToDto(ctx, gate, request.Provider, false, false);

        var refs = await LoadMasterRefAsync(ctx, cancellationToken);
        var views = CharacterReferenceRegenerationV1Rules.RequiredTypes.Select(type =>
        {
            var intent = CharacterReferenceGenerationRules.BuildDefault(
                ctx.CharacterId, era, type, ctx.MasterSha, ctx.DnaSha, ctx.PrpSha, packVersion: nextVersion);
            var canonical = CharacterReferenceGenerationRules.Canonical(intent, ctx.IdentityFacts);
            return CharacterReferenceGenerationRules.CompileProviderRequest(canonical, refs, type) is var compiled
                ? new CharacterReferenceViewRequest(type, compiled.Prompt, compiled.AspectRatio, compiled.References)
                : new CharacterReferenceViewRequest(type, canonical.Text, type == "FULL_BODY" ? "3:4" : "1:1", refs);
        }).ToList();

        var result = await _provider.GenerateSetAsync(
            new CharacterReferenceSetRequest(ctx.CharacterId, era, ctx.MasterSha, ctx.DnaSha, ctx.PrpSha, views),
            cancellationToken);
        var gemini = string.Equals(result.ProviderCalled ? _provider.ProviderId : "", "GEMINI", StringComparison.OrdinalIgnoreCase);
        if (!gemini || string.Equals(request.Provider, "RUNWAY", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.Provider, "VEO", StringComparison.OrdinalIgnoreCase))
        {
            return ToDto(ctx, gate with { Code = "PROVIDER_CAPABILITY_UNSUPPORTED" }, request.Provider, false, false);
        }

        var setId = Guid.NewGuid();
        var stored = new List<CharacterAuthorityStore.CrpItem>();
        foreach (var view in result.Views.Where(v => v.Succeeded && v.Bytes is { Length: > 32 }))
        {
            if (CharacterReferenceAutoGenerationV2Rules.RejectHistoricalStill(null, view.ReferenceType))
                continue;
            var persist = _store.PersistReferenceCandidate(ctx.CharacterId, era, ctx.AssetId, view.ReferenceType, setId, view.Bytes!);
            stored.Add(new CharacterAuthorityStore.CrpItem(
                view.ReferenceType, persist.Path, KitVideoIntegrityRules.Sha256Hex(persist.Bytes)));
        }

        var coverage = CharacterReferenceRegenerationV1Rules.RequiredTypes.Count(t =>
            stored.Any(i => string.Equals(i.Type, t, StringComparison.OrdinalIgnoreCase)));
        if (coverage != 4)
        {
            return ToDto(ctx, gate with
            {
                Status = "FAILED",
                Code = "REFERENCE_SET_INCOMPLETE",
                StaffMessage = CharacterReferenceRegenerationV1Rules.StaffIncomplete,
                MayCallProvider = false,
            }, request.Provider, false, true, gemini, setId);
        }

        var crpSha = KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(
            string.Join("|", stored.OrderBy(i => i.Type).Select(i => $"{i.Type}:{i.Sha256}"))));
        var history = (ctx.Crp?.History ?? []).ToList();
        history.Add(new CharacterAuthorityStore.CrpHistoryEntry(
            ctx.Crp!.Version,
            ctx.Crp.Status,
            ctx.Crp.Sha256,
            ctx.Crp.Items,
            ctx.Crp.RejectReasonCode,
            ctx.Crp.RejectReasonText,
            ctx.Crp.RejectedBy,
            ctx.Crp.RejectedAt,
            ctx.Crp.Provider,
            ctx.Crp.SetId,
            ctx.Crp.Fingerprint));

        await _authority.WriteCrpAsync(ctx.AssetId, new CharacterAuthorityStore.CrpSnapshot(
            ctx.CharacterId, era, CharacterReferenceRegenerationV1Rules.PendingReview,
            coverage, false, crpSha, stored, nextVersion, setId.ToString(), request.Provider,
            fingerprint, ctx.MasterSha, ctx.DnaSha, ctx.PrpSha,
            History: history), cancellationToken);

        _ = actor;
        _ = dup;
        var created = await GetAsync(ctx.CharacterId, request.Provider, era, cancellationToken);
        return created with
        {
            Generate = false,
            ProviderCalled = true,
            GeminiCalled = gemini,
            RunwayCalled = false,
            VeoCalled = false,
            ReferenceSetId = setId,
            Fingerprint = fingerprint,
        };
    }

    private async Task<CharacterReferenceRegenerationDto> PreviewAsync(
        string characterId, CharacterReferenceRegenerateRequestDto request, string action, CancellationToken ct)
    {
        var era = string.IsNullOrWhiteSpace(request.EraId) ? "ERA-01" : request.EraId!;
        var ctx = await LoadAsync(characterId, era, request.Provider, ct);
        var gate = CharacterReferenceRegenerationV1Rules.Evaluate(ctx.Input with
        {
            Action = action,
            DirectorProvider = request.Provider,
            Confirm = request.Confirm,
        });
        return ToDto(ctx, gate, request.Provider, false, false);
    }

    private sealed record Ctx(
        string CharacterId,
        string CharacterName,
        Guid AssetId,
        bool OfficialLocked,
        string MasterSha,
        string DnaSha,
        string PrpSha,
        string IdentityFacts,
        CharacterAuthorityStore.CrpSnapshot? Crp,
        CharacterReferenceRegenerationV1Rules.GateInput Input);

    private async Task<Ctx> LoadAsync(string characterId, string eraId, string? provider, CancellationToken ct)
    {
        string id;
        try { id = CharacterReferencePackRules.NormalizeCharacterId(characterId); }
        catch (InvalidOperationException) { id = ""; }

        CharacterReferencePackGetDto? pack = null;
        if (id.Length > 0)
        {
            try { pack = await _crp.GetAsync(id, eraId, ct); }
            catch (InvalidOperationException) { pack = null; }
        }

        var init = id.Length == 0
            ? null
            : await _init.GetAsync(id, provider, eraId, ct);
        var snap = id.Length == 0 ? null : await _authority.ReadAsync(id, eraId, ct);
        var crp = id.Length == 0 ? null : await _authority.ReadCrpAsync(id, eraId, ct);
        var officialLocked = string.Equals(pack?.Pack?.Status, "LOCKED", StringComparison.OrdinalIgnoreCase)
            && pack?.Pack?.CanUse == true;
        var status = officialLocked ? "LOCKED" : crp?.Status ?? pack?.Pack?.Status;
        var masterSha = init?.Master.Sha256 ?? crp?.MasterSha ?? pack?.Pack?.MasterSha256 ?? "";
        var dnaSha = init?.Dna.Sha256 ?? crp?.DnaSha ?? pack?.Pack?.DnaSha256 ?? "";
        var prpSha = init?.Prp.Sha256 ?? crp?.PrpSha ?? pack?.PrpSha256 ?? "";
        var dnaSpec = JsonSerializer.Deserialize<JsonElement>(
            string.IsNullOrWhiteSpace(snap is null ? null : null) ? "{}" : "{}");
        var facts = string.Join(", ", new[]
        {
            CharacterReferencePackRules.DisplayAge(dnaSpec),
            CharacterReferencePackRules.DisplaySummary(dnaSpec),
        }.Where(x => x.Length > 0));
        if (init is { } ready && !string.IsNullOrWhiteSpace(ready.CharacterName))
            facts = ready.CharacterName;

        var input = new CharacterReferenceRegenerationV1Rules.GateInput(
            id.Length > 0,
            CharacterAuthorityInitializationV1Rules.IsLocked(init?.Master.Status) || pack?.MasterLocked == true,
            CharacterAuthorityInitializationV1Rules.IsLocked(init?.Dna.Status) || pack?.DnaLocked == true,
            CharacterAuthorityInitializationV1Rules.IsLocked(init?.Prp.Status) || pack?.PrpLocked == true,
            crp is not null || pack?.Pack is not null,
            officialLocked ? "LOCKED" : status,
            provider,
            false,
            false,
            true,
            "GET");

        return new Ctx(
            id,
            init?.CharacterName ?? pack?.CharacterName ?? "",
            snap?.AssetId ?? Guid.Empty,
            officialLocked,
            masterSha,
            dnaSha,
            prpSha,
            facts,
            crp,
            input);
    }

    private async Task<IReadOnlyList<ImageGenerationReferenceBytes>> LoadMasterRefAsync(Ctx ctx, CancellationToken ct)
    {
        var snap = await _authority.ReadAsync(ctx.CharacterId, "ERA-01", ct);
        var master = _authority.ToOfficialMaster(snap);
        if (master is null) return [];
        if (CharacterReferenceAutoGenerationV2Rules.RejectHistoricalStill(master.Id.ToString(), master.ArtifactPath))
            return [];
        if (!CharacterReferenceGenerationRules.MayUseAsIdentitySource(null, master.ArtifactPath))
            return [];
        var bytes = _store.Read(master.ArtifactPath);
        if (bytes is not { Length: > 32 }) return [];
        return
        [
            new ImageGenerationReferenceBytes(
                "MASTER_REFERENCE",
                KitVideoArtifactRules.DetectMime(bytes) ?? "image/jpeg",
                bytes,
                master.Sha256,
                "MASTER_REFERENCE"),
        ];
    }

    private static CharacterReferenceRegenerationDto ToDto(
        Ctx ctx,
        CharacterReferenceRegenerationV1Rules.Gate gate,
        string? provider,
        bool generate,
        bool called,
        bool gemini = false,
        Guid? setId = null)
    {
        var types = ctx.Crp?.Items.Select(i => i.Type).ToList() ?? [];
        var missing = CharacterReferenceRegenerationV1Rules.RequiredTypes
            .Where(t => !types.Contains(t, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var history = (ctx.Crp?.History ?? []).Select(h => new CharacterReferenceHistoryItemDto(
            h.Version,
            h.Status,
            CharacterReferenceRegenerationV1Rules.IsRejected(h.Status)
                ? CharacterReferenceRegenerationV1Rules.StaffRejected
                : CharacterReferencePackRules.StatusVi(h.Status),
            h.Items.Count,
            false,
            h.Sha256,
            h.RejectReasonCode,
            h.RejectReasonText,
            h.RejectedBy,
            h.RejectedAt,
            h.Provider,
            h.SetId)).ToList();
        var status = ctx.OfficialLocked ? "LOCKED" : ctx.Crp?.Status ?? "DRAFT";
        return new CharacterReferenceRegenerationDto(
            CharacterReferenceRegenerationV1Rules.DocumentId,
            ctx.CharacterId,
            ctx.CharacterName,
            ctx.Crp?.EraId ?? "ERA-01",
            ctx.Crp?.Version ?? "V1",
            status,
            CharacterReferenceRegenerationV1Rules.IsRejected(status)
                ? CharacterReferenceRegenerationV1Rules.StaffRejected
                : CharacterReferencePackRules.StatusVi(status),
            gate.StaffMessage,
            gate.MayRegenerate
                ? CharacterReferenceRegenerationV1Rules.StaffRegenerate
                : gate.MayReject
                    ? CharacterReferenceRegenerationV1Rules.StaffReject
                    : gate.StaffMessage,
            gate.Code,
            gate.MayReject && !ctx.OfficialLocked,
            gate.MayRegenerate && !ctx.OfficialLocked,
            gate.MayCallProvider,
            gate.Code == "CONFIRMATION_REQUIRED" || gate.Code == "PROVIDER_REQUIRED",
            generate,
            called,
            gemini,
            false,
            false,
            false,
            false,
            false,
            false,
            ctx.Crp?.CanUse == true && CharacterReferenceRegenerationV1Rules.IsLocked(status),
            CharacterReferenceRegenerationV1Rules.IsApproved(status),
            CharacterReferenceRegenerationV1Rules.IsLocked(status),
            ctx.Crp?.Coverage ?? types.Count,
            4,
            types,
            missing,
            ctx.Crp?.RejectReasonCode,
            ctx.Crp?.RejectReasonText,
            ctx.Crp?.RejectedBy,
            ctx.Crp?.RejectedAt,
            provider ?? ctx.Crp?.Provider,
            Guid.TryParse(ctx.Crp?.SetId, out var parsed) ? parsed : setId,
            ctx.Crp?.Fingerprint,
            ctx.MasterSha,
            ctx.DnaSha,
            ctx.PrpSha,
            ctx.Crp?.Sha256,
            history);
    }
}
