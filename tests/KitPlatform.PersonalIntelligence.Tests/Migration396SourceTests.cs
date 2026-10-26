using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class Migration396SourceTests
{
    [Fact]
    public void Guard_is_transactional_and_refuses_every_database_except_the_isolated_one()
    {
        var root = RepoRoot();
        var sql = File.ReadAllText(Path.Combine(root, "migrations", "396_pack_personal_intelligence_exchange_connection_audit_v1.sql"));
        var begin = sql.IndexOf("BEGIN;", StringComparison.Ordinal);
        var guard = sql.IndexOf("current_database() NOT IN ('kit_pi_validation')", StringComparison.Ordinal);
        var create = sql.IndexOf("CREATE TABLE IF NOT EXISTS pack_personal_intelligence.exchange_connection_audit", StringComparison.Ordinal);
        var commit = sql.LastIndexOf("COMMIT;", StringComparison.Ordinal);
        Assert.True(begin >= 0 && guard > begin && create > guard && commit > create);
        Assert.DoesNotContain("novixa_prod", sql, StringComparison.Ordinal);

        var prod = File.ReadAllText(Path.Combine(root, "deploy", "ubuntu", "migration-files.prod.txt"));
        Assert.DoesNotContain("396_pack_personal_intelligence_exchange_connection_audit_v1.sql", prod, StringComparison.Ordinal);
        var personal = File.ReadAllText(Path.Combine(root, "deploy", "ubuntu", "migration-files.personal-intelligence.txt"));
        Assert.Contains("396_pack_personal_intelligence_exchange_connection_audit_v1.sql", personal, StringComparison.Ordinal);

        var runner = File.ReadAllText(Path.Combine(root, "deploy", "ubuntu", "run-incremental-migrations-prod.sh"));
        Assert.Contains("set -euo pipefail", runner, StringComparison.Ordinal);
        Assert.Contains("psql \"$CONN\" -v ON_ERROR_STOP=1 -f \"$path\"", runner, StringComparison.Ordinal);
        Assert.Contains("migration-files.prod.txt", runner, StringComparison.Ordinal);
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "migrations", "396_pack_personal_intelligence_exchange_connection_audit_v1.sql")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root was not found from the test output directory.");
    }
}
