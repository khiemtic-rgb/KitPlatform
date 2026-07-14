using KitPlatform.Application.Core;
using KitPlatform.Packs.Clinic;
using KitPlatform.Packs.Connect;
using KitPlatform.Packs.Pharmacy;
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
                "sales",
                "clinic_appointments",
                "clinic_emr_lite",
                "novixa_connect",
                "reports",
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
        ];

        Assert.Equal(expected, PlatformModuleCodes.All.ToArray());
    }

    [Fact]
    public void Pack_owned_modules_are_subset_of_platform_catalog()
    {
        foreach (var code in PharmacyPackDefinition.PackModuleCodes
                     .Concat(ClinicPackDefinition.PackModuleCodes)
                     .Concat(ConnectPackDefinition.PackModuleCodes)
                     .Concat(SurveyPackDefinition.PackModuleCodes))
        {
            Assert.Contains(code, PlatformModuleCodes.All);
        }
    }
}
