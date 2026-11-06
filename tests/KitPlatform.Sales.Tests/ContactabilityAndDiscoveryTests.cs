using KitPlatform.Packs.Sales;
using Xunit;

namespace KitPlatform.Sales.Tests;

public sealed class ContactabilityAndDiscoveryTests
{
    [Fact]
    public void Worker_tick_does_not_run_discovery()
    {
        var worker = Read("src", "KitPlatform.Api", "KitSales", "KitSalesJourneyWorker.cs");
        Assert.DoesNotContain("IKitSalesProspectDiscovery", worker, StringComparison.Ordinal);
        Assert.DoesNotContain("DiscoverAsync", worker, StringComparison.Ordinal);
        Assert.Contains("SweepAsync", worker, StringComparison.Ordinal);
    }

    [Fact]
    public void Discovery_stays_reachable_only_through_manual_endpoint()
    {
        var controller = Read("src", "KitPlatform.Api", "Controllers", "KitSales", "KitSalesController.cs");
        Assert.Contains("[HttpPost(\"agent/discover\")]", controller, StringComparison.Ordinal);
        Assert.Contains("_discovery.DiscoverAsync", controller, StringComparison.Ordinal);

        var callers = Directory.GetFiles(Path.Combine(FindRepo(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains("DiscoverAsync(", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .OrderBy(name => name)
            .ToList();
        Assert.Equal(["KitSalesController.cs", "KitSalesProspectDiscovery.cs", "SalesContracts.cs"], callers);
    }

    [Theory]
    [InlineData("map", "osm")]
    [InlineData("legal", "license")]
    [InlineData("legal", null)]
    [InlineData("facebook", "profile")]
    [InlineData("facebook", null)]
    [InlineData("facebook", "")]
    [InlineData("website", "osm")]
    [InlineData(null, "page")]
    public void Non_business_identities_are_not_social_channels(string? identityType, string? platform)
    {
        Assert.False(SalesContactability.IsBusinessSocialChannel(identityType, platform));
    }

    [Theory]
    [InlineData("facebook", "page")]
    [InlineData("facebook", "messenger")]
    [InlineData("Facebook", " PAGE ")]
    public void Facebook_page_and_messenger_are_social_channels(string identityType, string platform)
    {
        Assert.True(SalesContactability.IsBusinessSocialChannel(identityType, platform));
    }

    [Fact]
    public void Inactive_business_social_identity_is_not_a_channel()
    {
        Assert.False(SalesContactability.IsBusinessSocialChannel("facebook", "page", "archived"));
        Assert.False(SalesContactability.IsBusinessSocialChannel("facebook", "messenger", null));
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("", "", "")]
    [InlineData("  ", "\t", " ")]
    public void No_channel_means_not_reachable(string? phone, string? email, string? website)
    {
        Assert.False(SalesContactability.IsReachable(phone, email, website, false));
    }

    [Theory]
    [InlineData("0900000000", null, null, false)]
    [InlineData(null, "shop@example.test", null, false)]
    [InlineData(null, null, "https://example.test", false)]
    [InlineData(null, null, null, true)]
    public void Any_real_channel_is_reachable(string? phone, string? email, string? website, bool social)
    {
        Assert.True(SalesContactability.IsReachable(phone, email, website, social));
    }

    [Fact]
    public void Social_sql_allows_only_active_page_or_messenger()
    {
        var sql = SalesContactability.BusinessSocialIdentitySql;
        Assert.Equal("i.identity_type = 'facebook' AND i.platform IN ('page', 'messenger') AND i.status = 'active'", sql);
        Assert.DoesNotContain("'map'", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("'osm'", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("'profile'", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("legal", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("KitSalesAgentSweep.cs")]
    [InlineData("KitSalesAgentService.cs")]
    public void Lead_queries_use_channel_filter_not_any_identity(string file)
    {
        var source = Read("src", "Packs", "Sales", "KitPlatform.Packs.Sales.Infrastructure", file);
        Assert.Contains("{SalesContactability.BusinessSocialIdentitySql}", source, StringComparison.Ordinal);
        Assert.Contains("SalesContactability.IsReachable(lead.Phone, lead.Email, lead.Website, lead.HasSocialChannel)", source, StringComparison.Ordinal);
        Assert.Contains("b.email AS Email, b.website AS Website", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HasIdentity", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Unreachable_lead_gets_no_channel_points()
    {
        var reachable = NovixaQualificationScore.Evaluate(Facts(true));
        var unreachable = NovixaQualificationScore.Evaluate(Facts(false));

        Assert.Equal(25m, reachable.Fit - unreachable.Fit);
        Assert.DoesNotContain(unreachable.Factors, factor => factor.Code == "fit.reachable");
        Assert.Contains(reachable.Factors, factor => factor.Code == "fit.reachable" && factor.Evidence == "contact_channel");
    }

    private static QualificationFacts Facts(bool reachable) => new(
        "pharmacy", true, reachable, false, true, [], false, false, false, false, false);

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([FindRepo(), .. parts]));

    private static string FindRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "deploy", "ubuntu", "migration-files.kit-sales.txt")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo root not found.");
    }
}
