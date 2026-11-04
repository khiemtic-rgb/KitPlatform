using Dapper;
using Npgsql;
using Xunit;

namespace KitPlatform.Platform.Tests.Pharmacy;

/// <summary>
/// Read-only session gate. Runs before any validation insert.
/// </summary>
internal static class NovixaValidationSession
{
    private static readonly string[] DeniedUsers = ["kitplatform", "postgres", "pharmacore"];

    private static readonly string[] DeniedConnectDatabases =
        ["kitplatform", "kitplatform_test", "pharmacore", "postgres", "novixa_prod"];

    public static async Task EnsureIsolatedRoleAsync(NpgsqlConnection connection)
    {
        var role = await connection.QuerySingleAsync<RoleHead>(
            """
            SELECT r.rolname AS Name,
                   r.rolsuper AS Super,
                   r.rolcreatedb AS CreateDb,
                   r.rolcreaterole AS CreateRole
            FROM pg_roles r
            WHERE r.rolname = current_user
            """);

        if (DeniedUsers.Contains(role.Name, StringComparer.OrdinalIgnoreCase)
            || role.Super
            || role.CreateDb
            || role.CreateRole)
        {
            Assert.Fail($"Refusing role '{role.Name}'. No writes were attempted.");
        }

        var inherited = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int
            FROM pg_auth_members m
            JOIN pg_roles granted ON granted.oid = m.roleid
            JOIN pg_roles member ON member.oid = m.member
            WHERE member.rolname = current_user
              AND granted.rolname IN ('kitplatform', 'postgres', 'pharmacore')
            """);
        if (inherited != 0)
            Assert.Fail("Refusing a role that belongs to kitplatform, postgres, or pharmacore. No writes were attempted.");

        var reachable = (await connection.QueryAsync<string>(
            """
            SELECT datname
            FROM pg_database
            WHERE datname = ANY(@Names)
              AND has_database_privilege(current_user, oid, 'CONNECT')
            """,
            new { Names = DeniedConnectDatabases })).AsList();
        if (reachable.Count > 0)
        {
            Assert.Fail(
                "Refusing a role that can still connect to "
                + string.Join(", ", reachable)
                + ". No writes were attempted.");
        }
    }

    private sealed class RoleHead
    {
        public string Name { get; set; } = "";
        public bool Super { get; set; }
        public bool CreateDb { get; set; }
        public bool CreateRole { get; set; }
    }
}
