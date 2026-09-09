using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class CharacterAuthorityPipelineV1Service : ICharacterAuthorityPipelineV1Service
{
    private readonly ICharacterAuthorityInitializationV1Service _init;
    private readonly ICharacterReferenceAutoGenerationV2Service _crpSet;
    private readonly ICharacterReferencePackService _crp;
    private readonly CharacterAuthorityStore _authority;

    public CharacterAuthorityPipelineV1Service(
        ICharacterAuthorityInitializationV1Service init,
        ICharacterReferenceAutoGenerationV2Service crpSet,
        ICharacterReferencePackService crp,
        CharacterAuthorityStore authority)
    {
        _init = init;
        _crpSet = crpSet;
        _crp = crp;
        _authority = authority;
    }

    public IReadOnlyList<string> RunRegression() => CharacterAuthorityPipelineV1Regression.Run();

    public async Task<CharacterAuthorityPipelineDto> GetAsync(
        string characterId, string? provider = null, string eraId = "ERA-01",
        CancellationToken cancellationToken = default)
    {
        var init = await _init.GetAsync(characterId, provider, eraId, cancellationToken);
        CharacterReferencePackGetDto? pack = null;
        CharacterReferenceSetDto? set = null;
        if (!string.IsNullOrWhiteSpace(init.CharacterId))
        {
            try { pack = await _crp.GetAsync(init.CharacterId, eraId, cancellationToken); }
            catch (InvalidOperationException) { pack = null; }
            try { set = await _crpSet.GetAsync(init.CharacterId, provider, eraId, cancellationToken); }
            catch (InvalidOperationException) { set = null; }
        }

        var photo = CharacterAuthorityPipelineV1Rules.RejectPhotorealisticExternal(init.MasterImageUrl)
                    || CharacterAuthorityPipelineV1Rules.RejectPhotorealisticExternal(init.Master.Sha256);
        var historical = CharacterAuthorityInitializationV1Rules.RejectHistoricalStill(
            init.Master.ArtifactId?.ToString());
        var snap = new CharacterAuthorityPipelineV1Rules.Snapshot(
            init.IdentityReady || init.CharacterId.Length > 0,
            init.IdentityReady,
            init.IdentityReady,
            init.AuthorityLocked,
            init.Master.Status,
            init.Dna.Status,
            init.Prp.Status,
            pack?.Pack?.Status ?? set?.CrpStatus,
            set?.Coverage ?? pack?.Pack?.RequiredReady ?? 0,
            pack?.Pack?.CoverageReady == true || (set?.Coverage ?? 0) >= 4,
            pack?.Pack?.Status is "APPROVED" or "DIRECTOR_APPROVED" or "LOCKED",
            string.Equals(pack?.Pack?.Status, "LOCKED", StringComparison.OrdinalIgnoreCase),
            pack?.Pack?.CanUse == true || pack?.CanUse == true,
            photo && !init.AuthorityLocked,
            historical && !init.AuthorityLocked,
            false,
            provider,
            false,
            false);
        var gate = CharacterAuthorityPipelineV1Rules.Evaluate(snap);
        var crpSha = pack?.Pack?.PackSha256;
        return new CharacterAuthorityPipelineDto(
            CharacterAuthorityPipelineV1Rules.DocumentId,
            init.CharacterId,
            init.CharacterName,
            init.Role,
            init.EraId,
            gate.PipelineState,
            gate.Code,
            gate.StaffMessage,
            gate.NextAction,
            gate.WorkspaceCreated,
            init.IdentityReady,
            gate.CharacterReady,
            gate.ProductionReady,
            init.AuthorityLocked,
            photo,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            init.Master,
            init.Dna,
            init.Prp,
            pack?.Pack?.Status ?? set?.CrpStatus,
            set?.Coverage ?? pack?.Pack?.RequiredReady ?? 0,
            pack?.Pack?.CanUse == true || pack?.CanUse == true,
            new CharacterAuthorityObjectDto(
                init.CharacterId,
                init.EraId,
                init.IdentityVersion,
                init.Master.Sha256,
                init.Dna.Sha256,
                init.Prp.Sha256,
                crpSha,
                gate.PipelineState,
                gate.CharacterReady),
            new
            {
                visualStyle = CharacterAuthorityPipelineV1Rules.VisualStyleDocument,
                visualStyleVersion = CharacterAuthorityPipelineV1Rules.VisualStyleVersion,
                pipeline = gate.PipelineState,
                authorityLocked = init.AuthorityLocked,
            });
    }

    public async Task<CharacterAuthorityPipelineDto> ApproveCrpAsync(
        string characterId, string actor, string eraId = "ERA-01", CancellationToken cancellationToken = default)
    {
        var pack = await TryPack(characterId, eraId, cancellationToken);
        if (pack?.Pack is { } official && official.Status is "DRAFT" or "REVIEW" or "VALIDATED" or "READY_FOR_DIRECTOR")
            await _crp.ApproveAsync(characterId, official.Id, actor, null, cancellationToken);
        await TryAdvanceWorkspaceCrp(characterId, eraId, "DIRECTOR_APPROVED", canUse: false, cancellationToken);
        return await GetAsync(characterId, null, eraId, cancellationToken);
    }

    public async Task<CharacterAuthorityPipelineDto> LockCrpAsync(
        string characterId, string actor, string eraId = "ERA-01", CancellationToken cancellationToken = default)
    {
        var pack = await TryPack(characterId, eraId, cancellationToken);
        if (pack?.Pack is { } official)
        {
            if (official.Status is "DRAFT" or "REVIEW" or "VALIDATED" or "READY_FOR_DIRECTOR")
            {
                await _crp.ApproveAsync(characterId, official.Id, actor, null, cancellationToken);
                pack = await TryPack(characterId, eraId, cancellationToken);
                official = pack?.Pack ?? official;
            }
            if (official.Status is "APPROVED" or "DIRECTOR_APPROVED")
                await _crp.LockAsync(characterId, official.Id, actor, cancellationToken);
        }
        await TryAdvanceWorkspaceCrp(characterId, eraId, "DIRECTOR_APPROVED", canUse: false, cancellationToken);
        await TryAdvanceWorkspaceCrp(characterId, eraId, "LOCKED", canUse: true, cancellationToken);
        return await GetAsync(characterId, null, eraId, cancellationToken);
    }

    private async Task<CharacterReferencePackGetDto?> TryPack(string characterId, string eraId, CancellationToken ct)
    {
        try { return await _crp.GetAsync(characterId, eraId, ct); }
        catch (InvalidOperationException) { return null; }
    }

    private async Task TryAdvanceWorkspaceCrp(
        string characterId, string eraId, string status, bool canUse, CancellationToken ct)
    {
        try
        {
            await AdvanceWorkspaceCrp(characterId, eraId, status, canUse, ct);
        }
        catch (InvalidOperationException ex) when (
            ex.Message is "CRP_NOT_READY" or "CRP_NOT_APPROVED")
        {
        }
    }

    private async Task AdvanceWorkspaceCrp(
        string characterId, string eraId, string status, bool canUse, CancellationToken ct)
    {
        var snap = await _authority.ReadAsync(characterId, eraId, ct)
            ?? throw new InvalidOperationException("CRP_NOT_READY");
        var crp = await _authority.ReadCrpAsync(characterId, eraId, ct)
            ?? throw new InvalidOperationException("CRP_NOT_READY");
        if (string.Equals(crp.Status, "LOCKED", StringComparison.OrdinalIgnoreCase)
            && status is "DIRECTOR_APPROVED" or "APPROVED")
            return;
        if (status == "LOCKED" && crp.Status is not ("APPROVED" or "DIRECTOR_APPROVED" or "LOCKED"))
            throw new InvalidOperationException("CRP_NOT_APPROVED");
        if (status == "APPROVED" && crp.Coverage < 4)
            throw new InvalidOperationException("CRP_NOT_READY");
        if (string.Equals(crp.Status, status, StringComparison.OrdinalIgnoreCase)
            && crp.CanUse == canUse)
            return;
        await _authority.WriteCrpAsync(snap.AssetId, crp with { Status = status, CanUse = canUse }, ct);
    }
}
