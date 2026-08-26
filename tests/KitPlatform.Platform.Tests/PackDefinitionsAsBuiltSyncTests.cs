using KitPlatform.Application.Core;
using KitPlatform.Packs.Clinic;
using KitPlatform.Packs.Connect;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.FamilyOs;
using KitPlatform.Packs.LocalOs;
using KitPlatform.Packs.Pharmacy;
using KitPlatform.Packs.Sales;
using KitPlatform.Packs.Survey;
using Xunit;

namespace KitPlatform.Platform.Tests;

/// <summary>
/// Keeps pack metadata aligned with KIT-BP-ASBUILT (docs + YAML).
/// If a test fails after an intentional product change, update the blueprint in the same PR.
/// </summary>
public sealed class PackDefinitionsAsBuiltSyncTests
{
    [Fact]
    public void Pharmacy_defaults_match_asbuilt_blueprint()
    {
        Assert.Equal(
            [
                "inventory",
                "procurement",
                "sales",
                "loyalty",
                "customer_app",
                "medication",
                "health_wallet",
                "reservations",
                "reports",
                "e_rx",
                "prescriber_network",
                "prescriber_portal",
            ],
            PharmacyPackDefinition.DefaultEnabledModules);

        Assert.Equal(
            [
                "medication",
                "health_wallet",
                "reservations",
                "e_rx",
                "prescriber_network",
                "prescriber_portal",
            ],
            PharmacyPackDefinition.PackModuleCodes);

        Assert.Equal("pharmacy", PharmacyPackDefinition.PackCode);
        Assert.Equal("novixa_pharmacy", PharmacyPackDefinition.TenantPackageCode);
    }

    [Fact]
    public void Clinic_defaults_and_pack_codes_match_asbuilt_blueprint()
    {
        Assert.Equal(
            [
                "clinic_appointments",
                "clinic_emr_lite",
                "novixa_connect",
            ],
            ClinicPackDefinition.DefaultEnabledModules);

        Assert.Equal(
            [
                "clinic_appointments",
                "clinic_emr_lite",
                "crm_leads",
            ],
            ClinicPackDefinition.PackModuleCodes);

        // Pack-owned but not default-on (documented in blueprint 2.1.1).
        Assert.Contains("crm_leads", ClinicPackDefinition.PackModuleCodes);
        Assert.DoesNotContain("crm_leads", ClinicPackDefinition.DefaultEnabledModules);

        Assert.Equal("clinic_crm", ClinicPackDefinition.PackCode);
    }

    [Fact]
    public void Connect_and_survey_match_asbuilt_blueprint()
    {
        Assert.Equal(["novixa_connect"], ConnectPackDefinition.DefaultEnabledModules);
        Assert.Equal(["novixa_connect"], ConnectPackDefinition.PackModuleCodes);
        Assert.Equal("novixa_connect", ConnectPackDefinition.PackCode);

        Assert.Equal(
            ["assessment", "pharmacy_survey", "reports"],
            SurveyPackDefinition.DefaultEnabledModules);
        Assert.Equal(
            ["assessment", "pharmacy_survey"],
            SurveyPackDefinition.PackModuleCodes);
        Assert.Equal("pharmacy_survey", SurveyPackDefinition.PackCode);
    }

    [Fact]
    public void FamilyOs_match_starter_pack_definition()
    {
        Assert.Equal(["family_os"], FamilyOsPackDefinition.DefaultEnabledModules);
        Assert.Equal(["family_os"], FamilyOsPackDefinition.PackModuleCodes);
        Assert.Equal("family_os", FamilyOsPackDefinition.PackCode);
        Assert.Equal("family_os", FamilyOsPackDefinition.TenantPackageCode);
    }

    [Fact]
    public void MarketingPark_match_product_isolation_definition()
    {
        Assert.Equal(["kit_content"], ContentPackDefinition.DefaultEnabledModules);
        Assert.Equal(["kit_content"], ContentPackDefinition.PackModuleCodes);
        Assert.Equal("kit_content", ContentPackDefinition.PackCode);
        Assert.Equal("marketing_park", ContentPackDefinition.TenantPackageCode);
        Assert.Equal("KIT_MKT", ContentPackDefinition.DedicatedTenantCode);
        Assert.Equal(PlatformModuleCodes.KitContent, ContentPackDefinition.PrimaryModuleCode);
    }

    [Fact]
    public void LocalOs_match_product_isolation_definition()
    {
        Assert.Equal(["local_os"], LocalOsPackDefinition.DefaultEnabledModules);
        Assert.Equal(["local_os"], LocalOsPackDefinition.PackModuleCodes);
        Assert.Equal("local_os", LocalOsPackDefinition.PackCode);
        Assert.Equal("local_os", LocalOsPackDefinition.TenantPackageCode);
        Assert.Equal("KIT_LOCAL", LocalOsPackDefinition.DedicatedTenantCode);
        Assert.Equal(PlatformModuleCodes.LocalOs, LocalOsPackDefinition.PrimaryModuleCode);
    }

    [Fact]
    public void KitSales_match_product_isolation_definition()
    {
        Assert.Equal(["kit_sales"], SalesPackDefinition.DefaultEnabledModules);
        Assert.Equal(["kit_sales"], SalesPackDefinition.PackModuleCodes);
        Assert.Equal("kit_sales", SalesPackDefinition.PackCode);
        Assert.Equal("kit_sales", SalesPackDefinition.TenantPackageCode);
        Assert.Equal("KIT_SALES", SalesPackDefinition.DedicatedTenantCode);
        Assert.Equal(PlatformModuleCodes.KitSales, SalesPackDefinition.PrimaryModuleCode);
    }

    [Fact]
    public void Platform_module_codes_include_all_asbuilt_catalog_entries()
    {
        string[] expected =
        [
            "inventory",
            "procurement",
            "sales",
            "loyalty",
            "customer_app",
            "medication",
            "health_wallet",
            "reservations",
            "reports",
            "clinic",
            "clinic_appointments",
            "clinic_emr_lite",
            "crm_leads",
            "lab",
            "spa",
            "assessment",
            "pharmacy_survey",
            "e_rx",
            "prescriber_network",
            "prescriber_portal",
            "telehealth",
            "novixa_connect",
            "family_os",
            "care_os",
            "kit_content",
            "local_os",
            "kit_sales",
            "learning",
        ];

        Assert.Equal(expected, PlatformModuleCodes.All.ToArray());
    }

    [Fact]
    public void Pack_owned_modules_are_subset_of_platform_catalog()
    {
        foreach (var code in PharmacyPackDefinition.PackModuleCodes
                     .Concat(ClinicPackDefinition.PackModuleCodes)
                     .Concat(ConnectPackDefinition.PackModuleCodes)
                     .Concat(SurveyPackDefinition.PackModuleCodes)
                     .Concat(FamilyOsPackDefinition.PackModuleCodes)
                     .Concat(ContentPackDefinition.PackModuleCodes)
                     .Concat(LocalOsPackDefinition.PackModuleCodes)
                     .Concat(SalesPackDefinition.PackModuleCodes))
        {
            Assert.Contains(code, PlatformModuleCodes.All);
        }
    }
}
