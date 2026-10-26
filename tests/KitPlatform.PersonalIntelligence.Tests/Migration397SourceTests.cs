using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class Migration397SourceTests
{
    [Fact]
    public void Narrow_account_migration_is_transactional_and_does_not_create_the_ledger()
    {
        var root = RepoRoot();
        var sql = File.ReadAllText(Path.Combine(root, "migrations", "397_pack_personal_intelligence_exchange_account_min_v1.sql"));
        var begin = sql.IndexOf("BEGIN;", StringComparison.Ordinal);
        var guard = sql.IndexOf("current_database() NOT IN ('kit_pi_validation')", StringComparison.Ordinal);
        var person = sql.IndexOf("CREATE TABLE IF NOT EXISTS pack_personal_intelligence.person", StringComparison.Ordinal);
        var connection = sql.IndexOf("CREATE TABLE IF NOT EXISTS pack_personal_intelligence.exchange_connection", StringComparison.Ordinal);
        var job = sql.IndexOf("CREATE TABLE IF NOT EXISTS pack_personal_intelligence.exchange_backfill_job", StringComparison.Ordinal);
        var audit = sql.IndexOf("CREATE TABLE IF NOT EXISTS pack_personal_intelligence.exchange_connection_audit", StringComparison.Ordinal);
        var commit = sql.LastIndexOf("COMMIT;", StringComparison.Ordinal);
        Assert.True(begin >= 0 && guard > begin && person > guard && connection > person && job > connection && audit > job && commit > audit);
        Assert.DoesNotContain("novixa_prod", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE IF NOT EXISTS pack_personal_intelligence.trade", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE IF NOT EXISTS pack_personal_intelligence.trading_account", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE IF NOT EXISTS pack_personal_intelligence.exchange (", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("REFERENCES pack_personal_intelligence.trading_account", sql, StringComparison.Ordinal);

        var prod = File.ReadAllText(Path.Combine(root, "deploy", "ubuntu", "migration-files.prod.txt"));
        var personal = File.ReadAllText(Path.Combine(root, "deploy", "ubuntu", "migration-files.personal-intelligence.txt"));
        Assert.DoesNotContain("397_pack_personal_intelligence_exchange_account_min_v1.sql", prod, StringComparison.Ordinal);
        Assert.DoesNotContain("397_pack_personal_intelligence_exchange_account_min_v1.sql", personal, StringComparison.Ordinal);
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "migrations", "397_pack_personal_intelligence_exchange_account_min_v1.sql")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root was not found from the test output directory.");
    }
}
