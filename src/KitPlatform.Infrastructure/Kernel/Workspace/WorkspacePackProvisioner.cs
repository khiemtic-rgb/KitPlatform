using Dapper;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Infrastructure.Kernel.Workspace;

/// <summary>Provisions <c>kit_workspace</c> + subscription when pack modules are enabled (P0.4).</summary>
internal sealed class WorkspacePackProvisioner
{
    private static readonly string[] ClinicPackModules = ["clinic_appointments", "clinic_emr_lite", "crm_leads"];
    private static readonly string[] ConnectPackModules = ["novixa_connect"];

    private readonly IDbConnectionFactory _db;

    public WorkspacePackProvisioner(IDbConnectionFactory db) => _db = db;

    public async Task EnsurePacksForTenantAsync(
        Guid tenantId,
        IReadOnlyList<string> enabledModules,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        await conn.ExecuteAsync(
            "SELECT kit_provision_pack_workspace(@TenantId, 'novixa_pharmacy')",
            new { TenantId = tenantId });

        if (enabledModules.Any(m => ClinicPackModules.Contains(m, StringComparer.OrdinalIgnoreCase)))
        {
            await conn.ExecuteAsync(
                "SELECT kit_provision_pack_workspace(@TenantId, 'clinic_crm')",
                new { TenantId = tenantId });
        }

        if (enabledModules.Any(m => ConnectPackModules.Contains(m, StringComparer.OrdinalIgnoreCase)))
        {
            await conn.ExecuteAsync(
                "SELECT kit_provision_pack_workspace(@TenantId, 'novixa_connect')",
                new { TenantId = tenantId });
        }
    }
}
