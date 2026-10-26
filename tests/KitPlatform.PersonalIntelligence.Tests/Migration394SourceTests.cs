using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class Migration394SourceTests
{
    [Fact]
    public void Guard_refuses_production_and_collector_databases()
    {
        var root = RepoRoot();
        var sql = File.ReadAllText(Path.Combine(root, "migrations", "394_pack_personal_intelligence_paper_host_receipt_v1.sql"));
        var guard = sql.IndexOf("current_database() IN ('novixa_prod', 'trading_research')", StringComparison.Ordinal);
        var table = sql.IndexOf("CREATE TABLE IF NOT EXISTS pack_personal_intelligence.paper_host_receipt", StringComparison.Ordinal);

        Assert.True(guard >= 0 && table > guard);
        Assert.Contains("394_pack_personal_intelligence_paper_host_receipt_v1 must not run on %", sql, StringComparison.Ordinal);

        var production = File.ReadAllText(Path.Combine(root, "deploy", "ubuntu", "migration-files.prod.txt"));
        var research = File.ReadAllText(Path.Combine(root, "deploy", "ubuntu", "migration-files.trading-research.txt"));
        Assert.DoesNotContain("394_pack_personal_intelligence_paper_host_receipt_v1.sql", production, StringComparison.Ordinal);
        Assert.DoesNotContain("394_pack_personal_intelligence_paper_host_receipt_v1.sql", research, StringComparison.Ordinal);
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "migrations", "394_pack_personal_intelligence_paper_host_receipt_v1.sql")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root was not found from the test output directory.");
    }
}
