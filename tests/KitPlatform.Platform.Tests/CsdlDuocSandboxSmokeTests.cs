using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using KitPlatform.Packs.Pharmacy.Catalog;
using KitPlatform.Packs.Pharmacy.Infrastructure;
using Xunit;

namespace KitPlatform.Platform.Tests;

public sealed class CsdlDuocSandboxSmokeTests
{
    [Fact]
    public async Task Sandbox_connection_and_lookup_by_drug_id()
    {
        var repoRoot = FindRepoRoot();
        var secrets = Path.Combine(repoRoot, ".dev", "national-drug.secrets.json");
        if (!File.Exists(secrets))
        {
            // Local-only smoke; skip on CI without secrets.
            return;
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NationalDrugCatalog:Mode"] = "sandbox",
                ["NationalDrugCatalog:BaseUrl"] = "https://api-sandbox.csdlduoc.com.vn/v2",
                ["NationalDrugCatalog:Username"] = "4601239671",
                ["NationalDrugCatalog:MaxSearchScanPages"] = "2",
                ["NationalDrugCatalog:TimeoutSeconds"] = "45",
            })
            .AddJsonFile(secrets, optional: false, reloadOnChange: false)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug());
        services.AddPharmacyPack(config);
        await using var sp = services.BuildServiceProvider();

        var catalog = sp.GetRequiredService<INationalDrugCatalogService>();
        Assert.IsType<KitPlatform.Packs.Pharmacy.Infrastructure.Catalog.CsdlDuoc.CsdlDuocNationalDrugCatalogService>(
            catalog);

        var status = await catalog.GetConnectionStatusAsync();
        Assert.Equal("sandbox", status.Mode);
        Assert.Contains("Kết nối OK", status.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        var browse = await catalog.SearchAsync(null, 1, 3);
        Assert.True(browse.Total > 1000);
        Assert.NotEmpty(browse.Items);

        var id = browse.Items[0].DrugId;
        var detail = await catalog.GetAsync(id);
        Assert.NotNull(detail);
        Assert.Equal(id, detail!.DrugId);

        var bySearch = await catalog.SearchAsync(id, 1, 5);
        Assert.Equal(1, bySearch.Total);
        Assert.Equal(id, bySearch.Items[0].DrugId);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "KitPlatform.sln"))
                || Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;
            dir = dir.Parent;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }
}
