using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyBlueprintService : IFamilyBlueprintService
{
    private readonly FamilyBlueprintRepository _repo;
    private readonly FamilyGraphRepository _families;
    private readonly FamilyValueRepository _values;
    private readonly IFamilyCommercialService _commercial;

    public FamilyBlueprintService(
        FamilyBlueprintRepository repo,
        FamilyGraphRepository families,
        FamilyValueRepository values,
        IFamilyCommercialService commercial)
    {
        _repo = repo;
        _families = families;
        _values = values;
        _commercial = commercial;
    }

    public async Task<FamilyBlueprintDto?> GetAsync(
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        _ = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var row = await _repo.GetAsync(familyId, cancellationToken);
        return row is null ? null : ToDto(row);
    }

    public async Task<FamilyDnaCardDto> GetDnaCardAsync(
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        _ = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var pack = await _commercial.GetCapabilityPackAsync(familyId, cancellationToken);
        var isTeaser = pack.TierCode is FamilyPlanTiers.Free;
        var row = await _repo.GetAsync(familyId, cancellationToken);

        // Auto-hydrate from onboarding when Blueprint row is missing (Setup Wizard đã xong).
        if (row is null)
        {
            try
            {
                var onboarding = await _values.GetOnboardingAsync(familyId, cancellationToken);
                if (onboarding is not null && !string.IsNullOrWhiteSpace(onboarding.PayloadJson))
                {
                    await HydrateFromOnboardingAsync(
                        familyId,
                        new FamilyBlueprintHydrateRequest(onboarding.PayloadJson),
                        cancellationToken);
                    row = await _repo.GetAsync(familyId, cancellationToken);
                }
            }
            catch
            {
                // Best-effort — DNA card still returns empty state.
            }
        }

        // DNA stage used to stick to onboarding ageBand (often default 7-9 → Tiểu học).
        // When children have DOB, recompute from the oldest child and persist if stale.
        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var childDobs = members
            .Where(m =>
                string.Equals(m.RoleCode, "child", StringComparison.OrdinalIgnoreCase)
                || string.Equals(m.RoleCode, "kid", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.DateOfBirth);
        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);
        var dobAgeBand = FamilyBlueprintHydrator.ResolvePrimaryAgeBandFromChildDobs(childDobs, asOf);
        if (dobAgeBand is not null && row is not null)
        {
            var currentBand = FamilyBlueprintHydrator.ReadPrimaryAgeBandPublic(row.LayersJson);
            if (!string.Equals(currentBand, dobAgeBand, StringComparison.OrdinalIgnoreCase))
            {
                var (layers, dna) = FamilyBlueprintHydrator.ApplyPrimaryAgeBand(
                    row.LayersJson,
                    row.DnaJson,
                    dobAgeBand);
                await _repo.UpsertAsync(
                    familyId,
                    layers,
                    dna,
                    FamilyBlueprintSchema.CurrentVersion,
                    hydratedAt: row.HydratedAt,
                    cancellationToken);
                row = await _repo.GetAsync(familyId, cancellationToken) ?? row;
            }
        }

        var card = FamilyBlueprintHydrator.ToDnaCard(
            familyId,
            row?.LayersJson,
            row?.DnaJson,
            hasBlueprint: row is not null,
            tierCode: pack.TierCode,
            isTeaser: isTeaser,
            upgradeHintVi: isTeaser
                ? "Nâng Family Peace Plan để xem Focus & bước tiếp theo — AI hiểu nhà bạn sâu hơn."
                : pack.UpgradeHintVi);

        if (dobAgeBand is not null)
        {
            var label = FamilyBlueprintHydrator.LabelViFromAgeBand(dobAgeBand);
            if (!string.Equals(card.StageLabelVi, label, StringComparison.Ordinal))
                return card with { StageLabelVi = label };
        }

        return card;
    }

    public async Task<FamilyBlueprintDto> UpsertAsync(
        Guid familyId,
        FamilyBlueprintUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var existing = await _repo.GetAsync(familyId, cancellationToken);
        var layers = string.IsNullOrWhiteSpace(request.LayersJson)
            ? existing?.LayersJson ?? "{}"
            : FamilyBlueprintHydrator.MergeJson(
                existing?.LayersJson ?? "{}",
                request.LayersJson!,
                request.Replace);
        var dna = string.IsNullOrWhiteSpace(request.DnaJson)
            ? existing?.DnaJson ?? "{}"
            : FamilyBlueprintHydrator.MergeJson(
                existing?.DnaJson ?? "{}",
                request.DnaJson!,
                request.Replace);

        await _repo.UpsertAsync(
            familyId,
            layers,
            dna,
            FamilyBlueprintSchema.CurrentVersion,
            hydratedAt: existing?.HydratedAt,
            cancellationToken);

        var row = await _repo.GetAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không lưu được Blueprint.");
        return ToDto(row);
    }

    public async Task<FamilyBlueprintDto> HydrateFromOnboardingAsync(
        Guid familyId,
        FamilyBlueprintHydrateRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var payload = request?.OnboardingPayloadJson;
        if (string.IsNullOrWhiteSpace(payload))
        {
            var onboarding = await _values.GetOnboardingAsync(familyId, cancellationToken)
                ?? throw new InvalidOperationException(
                    "Chưa có onboarding — hoàn tất Setup Wizard trước khi hydrate Blueprint.");
            payload = onboarding.PayloadJson;
        }

        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var childMembers = members
            .Where(m =>
                string.Equals(m.RoleCode, "child", StringComparison.OrdinalIgnoreCase)
                || string.Equals(m.RoleCode, "kid", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var childCount = childMembers.Count;

        var (layers, dna) = FamilyBlueprintHydrator.FromOnboardingPayload(
            payload!,
            family.Timezone,
            childCount);

        // Prefer real DOBs over wizard ageBand default (often "7-9").
        var dobBand = FamilyBlueprintHydrator.ResolvePrimaryAgeBandFromChildDobs(
            childMembers.Select(m => m.DateOfBirth),
            DateOnly.FromDateTime(DateTime.UtcNow));
        if (dobBand is not null)
            (layers, dna) = FamilyBlueprintHydrator.ApplyPrimaryAgeBand(layers, dna, dobBand);

        var existing = await _repo.GetAsync(familyId, cancellationToken);
        // Keep L3/L4 calibration if already captured; refresh L1/L6/L8 from onboarding.
        var mergedLayers = existing is null
            ? layers
            : FamilyBlueprintHydrator.MergeJson(layers, PreserveCalibrationPatch(existing.LayersJson), replace: false);

        if (existing is not null)
        {
            var signals = FamilyBlueprintHydrator.ReadCalibrationSignals(mergedLayers);
            if (signals.SchoolCode != FamilySelfCalibration.SchoolCodes.Unknown
                || signals.SelfView != FamilySelfCalibration.SelfViewCodes.Unknown
                || signals.PeerShock != FamilySelfCalibration.PeerShockCodes.Unknown
                || signals.IllusionHits7d > 0)
            {
                (mergedLayers, dna) = FamilyBlueprintHydrator.RefreshCalibrationDna(
                    mergedLayers,
                    dna,
                    FamilyBlueprintHydrator.ReadPrimaryChildShortName(mergedLayers));
            }
        }

        await _repo.UpsertAsync(
            familyId,
            mergedLayers,
            dna,
            FamilyBlueprintSchema.CurrentVersion,
            hydratedAt: DateTimeOffset.UtcNow,
            cancellationToken);

        var row = await _repo.GetAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không hydrate được Blueprint.");
        return ToDto(row);
    }

    public async Task<FamilyDnaCardDto> CaptureCalibrationAsync(
        Guid familyId,
        FamilyCalibrationCaptureRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var existing = await _repo.GetAsync(familyId, cancellationToken);
        var (layers, dna) = FamilyBlueprintHydrator.ApplyCalibrationCapture(
            existing?.LayersJson,
            existing?.DnaJson,
            request);

        await _repo.UpsertAsync(
            familyId,
            layers,
            dna,
            FamilyBlueprintSchema.CurrentVersion,
            hydratedAt: existing?.HydratedAt ?? DateTimeOffset.UtcNow,
            cancellationToken);

        return await GetDnaCardAsync(familyId, cancellationToken);
    }

    public async Task NoteIllusionRiskAsync(
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repo.GetAsync(familyId, cancellationToken);
        if (existing is null) return;

        var (layers, dna) = FamilyBlueprintHydrator.NoteIllusionHit(
            existing.LayersJson,
            existing.DnaJson);

        await _repo.UpsertAsync(
            familyId,
            layers,
            dna,
            FamilyBlueprintSchema.CurrentVersion,
            hydratedAt: existing.HydratedAt,
            cancellationToken);
    }

    /// <summary>Patch that only carries child.selfCalibration + context.school from prior layers.</summary>
    private static string PreserveCalibrationPatch(string existingLayersJson)
    {
        var src = System.Text.Json.Nodes.JsonNode.Parse(
            string.IsNullOrWhiteSpace(existingLayersJson) ? "{}" : existingLayersJson)
            as System.Text.Json.Nodes.JsonObject
            ?? new System.Text.Json.Nodes.JsonObject();
        var patch = new System.Text.Json.Nodes.JsonObject();
        if (src["context"] is System.Text.Json.Nodes.JsonObject ctx)
            patch["context"] = ctx.DeepClone();
        if (src["child"] is System.Text.Json.Nodes.JsonObject child)
            patch["child"] = child.DeepClone();
        if (src["resources"] is System.Text.Json.Nodes.JsonObject resources)
            patch["resources"] = resources.DeepClone();
        if (src["growthBalance"] is System.Text.Json.Nodes.JsonObject gb)
            patch["growthBalance"] = gb.DeepClone();
        return patch.ToJsonString();
    }

    private static FamilyBlueprintDto ToDto(FamilyBlueprintRepository.BlueprintRow row) =>
        new(row.FamilyId, row.LayersJson, row.DnaJson, row.SchemaVersion, row.HydratedAt, row.UpdatedAt);
}
