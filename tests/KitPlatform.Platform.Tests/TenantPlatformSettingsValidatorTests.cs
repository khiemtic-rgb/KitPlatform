using KitPlatform.Application.Configuration;
using KitPlatform.Application.Core;
using Xunit;

namespace KitPlatform.Platform.Tests;

public sealed class TenantPlatformSettingsValidatorTests
{
    private static readonly HashSet<string> Registry = new(StringComparer.OrdinalIgnoreCase)
    {
        PlatformModuleCodes.Sales,
        PlatformModuleCodes.Medication,
        PlatformModuleCodes.ClinicEmrLite,
        "unknown_pack",
    };

    [Fact]
    public void NormalizeModules_accepts_registry_codes()
    {
        var (accepted, ignored) = TenantPlatformSettingsValidator.NormalizeModules(
            [PlatformModuleCodes.Sales, PlatformModuleCodes.Medication],
            Registry);

        Assert.Equal(2, accepted.Count);
        Assert.Empty(ignored);
    }

    [Fact]
    public void NormalizeModules_ignores_unknown_codes()
    {
        var (accepted, ignored) = TenantPlatformSettingsValidator.NormalizeModules(
            [PlatformModuleCodes.Sales, "not_a_module"],
            Registry);

        Assert.Single(accepted);
        Assert.Equal("not_a_module", ignored[0]);
    }

    [Fact]
    public void NormalizeModules_rejects_codes_outside_allowed_ceiling()
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            PlatformModuleCodes.ClinicEmrLite,
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            TenantPlatformSettingsValidator.NormalizeModules(
                [PlatformModuleCodes.ClinicEmrLite, PlatformModuleCodes.Sales],
                Registry,
                allowed));

        Assert.Contains("allowed_modules", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormalizeModules_accepts_within_allowed_ceiling()
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            PlatformModuleCodes.ClinicEmrLite,
            PlatformModuleCodes.Medication,
        };

        var (accepted, ignored) = TenantPlatformSettingsValidator.NormalizeModules(
            [PlatformModuleCodes.ClinicEmrLite],
            Registry,
            allowed);

        Assert.Equal([PlatformModuleCodes.ClinicEmrLite], accepted);
        Assert.Empty(ignored);
    }

    [Fact]
    public void ClampEnabledToAllowed_intersects_and_falls_back_to_allowed()
    {
        var clamped = TenantPlatformSettingsValidator.ClampEnabledToAllowed(
            [PlatformModuleCodes.Sales, PlatformModuleCodes.ClinicEmrLite],
            [PlatformModuleCodes.ClinicEmrLite, PlatformModuleCodes.Medication]);

        Assert.Equal([PlatformModuleCodes.ClinicEmrLite], clamped);

        var fallback = TenantPlatformSettingsValidator.ClampEnabledToAllowed(
            [PlatformModuleCodes.Sales],
            [PlatformModuleCodes.ClinicEmrLite]);

        Assert.Equal([PlatformModuleCodes.ClinicEmrLite], fallback);
    }

    [Fact]
    public void NormalizeVertical_rejects_invalid()
    {
        Assert.Throws<InvalidOperationException>(() =>
            TenantPlatformSettingsValidator.NormalizeVertical("retail_store"));
    }

    [Fact]
    public void MergeFeatures_only_updates_known_keys()
    {
        var current = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            [PlatformFeatureCodes.NationalDrugCatalog] = true,
            [PlatformFeatureCodes.BatchTracking] = true,
        };

        var merged = TenantPlatformSettingsValidator.MergeFeatures(
            current,
            new Dictionary<string, bool>
            {
                [PlatformFeatureCodes.NationalDrugCatalog] = false,
                ["custom_unknown"] = true,
            });

        Assert.False(merged[PlatformFeatureCodes.NationalDrugCatalog]);
        Assert.True(merged[PlatformFeatureCodes.BatchTracking]);
        Assert.False(merged.ContainsKey("custom_unknown"));
    }

    [Fact]
    public void NormalizeMaxBranches_null_means_unlimited()
    {
        Assert.Null(TenantPlatformSettingsValidator.NormalizeMaxBranches(null));
    }

    [Fact]
    public void NormalizeMaxBranches_rejects_zero_or_negative()
    {
        Assert.Throws<InvalidOperationException>(() =>
            TenantPlatformSettingsValidator.NormalizeMaxBranches(0));
        Assert.Throws<InvalidOperationException>(() =>
            TenantPlatformSettingsValidator.NormalizeMaxBranches(-1));
    }

    [Fact]
    public void EnsureWithinBranchQuota_blocks_when_over_ceiling()
    {
        TenantPlatformSettingsValidator.EnsureWithinBranchQuota(1, maxBranches: 1);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            TenantPlatformSettingsValidator.EnsureWithinBranchQuota(2, maxBranches: 1));

        Assert.Contains("hạn mức", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureWithinBranchQuota_allows_when_unlimited()
    {
        TenantPlatformSettingsValidator.EnsureWithinBranchQuota(99, maxBranches: null);
    }

    [Fact]
    public void ToColumnValue_keeps_hybrid_until_family_promoted()
    {
        Assert.Equal(
            PlatformVerticalCodes.Hybrid,
            PlatformVerticalCodes.ToColumnValue(PlatformVerticalCodes.Family, PlatformVerticalCodes.Hybrid));
        Assert.Equal(
            PlatformVerticalCodes.Family,
            PlatformVerticalCodes.ToColumnValue(PlatformVerticalCodes.Family, PlatformVerticalCodes.Family));
        Assert.Equal(
            PlatformVerticalCodes.Pharmacy,
            PlatformVerticalCodes.ToColumnValue(PlatformVerticalCodes.Pharmacy, PlatformVerticalCodes.Hybrid));
    }
}
