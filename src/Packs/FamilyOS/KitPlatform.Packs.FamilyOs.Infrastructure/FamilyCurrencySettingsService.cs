using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyCurrencySettingsService : IFamilyCurrencySettingsService
{
    private readonly FamilyCurrencySettingsRepository _settings;
    private readonly FamilyGraphRepository _families;

    public FamilyCurrencySettingsService(
        FamilyCurrencySettingsRepository settings,
        FamilyGraphRepository families)
    {
        _settings = settings;
        _families = families;
    }

    public async Task<FamilyCurrencySettingsDto> GetSettingsAsync(
        Guid familyId,
        Guid? memberId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureFamilyAsync(familyId, cancellationToken);
        var row = await _settings.GetAsync(familyId, cancellationToken);
        return await MapAsync(familyId, row, memberId, isConfigured: row is not null, cancellationToken);
    }

    public async Task<FamilyCurrencySettingsDto> UpsertSettingsAsync(
        Guid familyId,
        UpdateFamilyCurrencySettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureFamilyAsync(familyId, cancellationToken);
        var presetId = string.IsNullOrWhiteSpace(request.PresetId)
            ? FamilyCurrencyPreset.BalancedV1Id
            : request.PresetId.Trim();

        if (request.AgeBand is { } band
            && band is not (
                FamilyCurrencyAgeBands.Age6To10
                or FamilyCurrencyAgeBands.Age11To15
                or FamilyCurrencyAgeBands.Age16To18
                or FamilyCurrencyAgeBands.Custom))
        {
            throw new InvalidOperationException("Nhóm tuổi không hợp lệ (6_10 | 11_15 | 16_18 | custom).");
        }

        if (request.DailyBudgetOverride is int o && o is < 10 or > 80)
            throw new InvalidOperationException("Ngân sách ngày tùy chỉnh phải từ 10 đến 80 sao.");

        var row = await _settings.UpsertAsync(
            familyId,
            request.Enabled,
            presetId,
            request.AgeBand,
            request.DailyBudgetOverride,
            cancellationToken);

        return await MapAsync(familyId, row, memberId: null, isConfigured: true, cancellationToken);
    }

    public Task<FamilyCurrencySettingsDto> ApplyPresetAsync(
        Guid familyId,
        string presetId = FamilyCurrencyPreset.BalancedV1Id,
        CancellationToken cancellationToken = default) =>
        UpsertSettingsAsync(
            familyId,
            new UpdateFamilyCurrencySettingsRequest(
                Enabled: true,
                PresetId: presetId,
                AgeBand: null,
                DailyBudgetOverride: null),
            cancellationToken);

    private async Task EnsureFamilyAsync(Guid familyId, CancellationToken cancellationToken)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken);
        if (family is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");
    }

    private async Task<FamilyCurrencySettingsDto> MapAsync(
        Guid familyId,
        FamilyCurrencySettingsRepository.FamilyCurrencySettingsRow? row,
        Guid? memberId,
        bool isConfigured,
        CancellationToken cancellationToken)
    {
        var enabled = row?.Enabled ?? true;
        var presetId = row?.PresetId ?? FamilyCurrencyPreset.BalancedV1Id;
        var config = FamilyCurrencyPreset.Resolve(presetId);
        var family = await _families.GetFamilyAsync(familyId, cancellationToken);
        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family?.Timezone).DateTime);

        string ageBand;
        if (!string.IsNullOrWhiteSpace(row?.AgeBand)
            && row!.AgeBand != FamilyCurrencyAgeBands.Custom)
        {
            ageBand = row.AgeBand!;
        }
        else if (memberId is Guid mid)
        {
            var dob = await _settings.GetMemberDobAsync(mid, cancellationToken);
            ageBand = FamilyCurrencyAgeBands.FromDateOfBirth(dob, today);
        }
        else
        {
            ageBand = FamilyCurrencyAgeBands.Age11To15;
        }

        var budget = FamilyCurrencyPreset.ResolveBudget(config, ageBand, row?.DailyBudgetOverride);

        return new FamilyCurrencySettingsDto(
            enabled,
            presetId,
            row?.AgeBand,
            row?.DailyBudgetOverride,
            budget,
            ageBand,
            config,
            isConfigured);
    }
}
