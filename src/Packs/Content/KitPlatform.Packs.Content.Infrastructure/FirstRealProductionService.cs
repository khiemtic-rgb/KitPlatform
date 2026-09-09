using System.Linq;
using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class FirstRealProductionService : IFirstRealProductionService
{
    private readonly KitVideoProductionShotRepository _shots;
    private readonly ProductionShotContractRepository _contracts;
    private readonly ICharacterReferencePackService _crp;
    private readonly ICharacterIdentityGovernanceService _governance;
    private readonly ImageGenerationExecutionRepository _executions;

    public FirstRealProductionService(
        KitVideoProductionShotRepository shots,
        ProductionShotContractRepository contracts,
        ICharacterReferencePackService crp,
        ICharacterIdentityGovernanceService governance,
        ImageGenerationExecutionRepository executions)
    {
        _shots = shots;
        _contracts = contracts;
        _crp = crp;
        _governance = governance;
        _executions = executions;
    }

    public IReadOnlyList<string> RunRegression() => FirstRealProductionV1Regression.Run();
    public IReadOnlyList<string> RunV2Regression() => FirstRealProductionV2Regression.Run();

    public async Task<FirstRealProductionDto> GetAsync(Guid shotId, string? provider, CancellationToken cancellationToken = default)
    {
        var shot = await _shots.GetByIdAsync(shotId, cancellationToken)
            ?? throw new InvalidOperationException("SHOT_NOT_FOUND: Không tìm thấy Shot 01.");
        var gov = await _governance.GetAsync(shot.CharacterId, shot.EraId, cancellationToken);
        CharacterReferencePackGetDto? crp = null;
        try { crp = await _crp.GetAsync(shot.CharacterId, shot.EraId, cancellationToken); }
        catch (InvalidOperationException) { crp = null; }
        var contract = await _contracts.GetLatestAsync(shotId, cancellationToken);
        var payload = JsonSerializer.Deserialize<JsonElement>(
            string.IsNullOrWhiteSpace(contract?.PayloadJson) ? "{}" : contract!.PayloadJson);
        var liveContractSha = contract is null ? "" : ProductionShotContractRules.HashCanonical(payload);
        var referenceSha = crp?.Pack?.PackSha256
            ?? crp?.Pack?.MasterSha256
            ?? gov.PrpSha256
            ?? "";
        var intent = FirstRealProductionRules.TryResolveIntent(
            shot.CharacterId, payload,
            gov.MasterSha256 ?? "", gov.DnaSha256 ?? "", referenceSha, liveContractSha);
        var intentValid = intent is not null;
        var intentSha = intent is null ? null : ProductionOsRules.IntentSha(intent);
        var canonicalOk = intent is not null;
        if (intent is not null)
            _ = ProductionOsRules.Canonical(intent);

        var picked = (provider ?? "").Trim().ToUpperInvariant();
        var capabilityReady = picked.Length == 0
            || (FirstRealProductionRules.AllowsImageProvider(picked)
                && FirstRealProductionRules.ImageCapabilityReady(ProductionOsRules.GeminiImageProfile));
        var existing = await _executions.GetLatestAsync(shotId, cancellationToken);
        if (existing is not null && FirstRealProductionV2Rules.IsHistoricalStill(existing.Id.ToString()))
            existing = null;
        var fingerprint = intent is null || picked.Length == 0
            ? null
            : ProductionOsRules.ExecutionFingerprint(intent, picked, "IMAGE_ONLY");
        var dup = existing is not null
            && ImageGenerationExecutionRules.IsTerminalSuccess(existing.ExecutionStatus)
            && !string.IsNullOrWhiteSpace(fingerprint)
            && (ProductionOsRules.DuplicatePolicy(fingerprint, existing.ExecutionFingerprint) == "BLOCK_DUPLICATE"
                || (!string.IsNullOrWhiteSpace(existing.ExecutionFingerprint)
                    && string.Equals(existing.ExecutionFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase)));

        var packCanUse = crp?.Pack?.CanUse == true;
        var crpStatus = crp?.Pack?.Status ?? "";
        var coverage = crp?.Pack?.RequiredReady ?? 0;
        var crpReady = FirstRealProductionV2Rules.CrpReadyForProduction(crpStatus, packCanUse, coverage);
        var masterMatch = FirstRealProductionV2Rules.ShaMatch(
            string.IsNullOrWhiteSpace(crp?.Pack?.MasterSha256) ? gov.MasterSha256 : crp!.Pack!.MasterSha256,
            gov.MasterSha256);
        var dnaMatch = FirstRealProductionV2Rules.ShaMatch(
            string.IsNullOrWhiteSpace(crp?.Pack?.DnaSha256) ? gov.DnaSha256 : crp!.Pack!.DnaSha256,
            gov.DnaSha256);
        var prpMatch = FirstRealProductionV2Rules.ShaMatch(
            string.IsNullOrWhiteSpace(crp?.Pack?.PrpSha256) ? gov.PrpSha256 : crp!.Pack!.PrpSha256,
            gov.PrpSha256);

        var gate = FirstRealProductionV2Rules.Evaluate(new FirstRealProductionV2Rules.GateInput(
            true,
            FirstRealProductionV2Rules.IsShot001(shot.ShotCode),
            shot.ShotCode,
            string.Equals(gov.Master, "LOCKED", StringComparison.OrdinalIgnoreCase),
            string.Equals(gov.Dna, "LOCKED", StringComparison.OrdinalIgnoreCase),
            string.Equals(gov.Prp, "LOCKED", StringComparison.OrdinalIgnoreCase),
            masterMatch,
            dnaMatch,
            prpMatch,
            packCanUse,
            crpStatus,
            coverage,
            contract is not null && ProductionShotContractRules.IsApproved(contract.ContractStatus),
            intentValid,
            picked.Length == 0 ? null : picked,
            capabilityReady,
            capabilityReady ? null : "PROVIDER_CAPABILITY_UNSUPPORTED",
            false,
            dup,
            shot.CharacterId,
            shot.CharacterId));

        var missingFullBody = crp?.Pack?.MissingTypes?.Any(t =>
            string.Equals(t, "FULL_BODY", StringComparison.OrdinalIgnoreCase)) == true;
        var staff = crpReady
            ? gate.StaffMessage
            : missingFullBody
                ? CharacterReferenceCompletionRules.StaffMissingFullBody
                : gate.StaffMessage;
        var next = crpReady
            ? CharacterReferenceCompletionRules.StaffNext
            : missingFullBody
                ? CharacterReferenceCompletionRules.StaffMissingFullBody
                : "Hoàn thiện bộ ảnh chuẩn.";
        var location = ReadNested(payload, "scene", "location");
        if (location.Length == 0) location = ReadNested(payload, "scene", "summary");
        var action = ReadNested(payload, "story", "action");
        var duration = intent?.DurationSeconds ?? 5;
        return new FirstRealProductionDto(
            FirstRealProductionV2Rules.DocumentId,
            shot.Id,
            shot.ShotCode,
            shot.CharacterId,
            FirstRealProductionRules.StaffCharacterLine(crp?.CharacterName ?? shot.CharacterName, shot.CharacterId),
            location,
            action,
            $"{duration:0} giây",
            crpStatus.Length == 0 ? "MISSING" : crpStatus,
            crpReady,
            picked,
            gate.CapabilityReady ? "READY" : (gate.Code ?? "BLOCKED"),
            intentValid ? "VALID" : "INVALID",
            intentSha,
            canonicalOk ? "VALID" : "INVALID",
            gate.AuthorityValid,
            false,
            false,
            staff,
            next,
            false,
            false,
            false,
            false,
            gov.MasterSha256 ?? "",
            gov.DnaSha256 ?? "",
            referenceSha,
            liveContractSha,
            fingerprint);
    }

    private static string ReadNested(JsonElement payload, string obj, string name)
    {
        if (payload.ValueKind != JsonValueKind.Object) return "";
        if (!payload.TryGetProperty(obj, out var o) || o.ValueKind != JsonValueKind.Object) return "";
        return o.TryGetProperty(name, out var n) && n.ValueKind == JsonValueKind.String ? n.GetString()?.Trim() ?? "" : "";
    }
}
