using System.Data.Common;

namespace KitPlatform.Packs.Sales.Infrastructure;

/// <summary>
/// Kernel audit writes on its own connection, so it cannot join the settings transaction.
/// The change stays uncommitted until the audit row exists; a failed audit rolls it back.
/// The remaining gap is the reverse: audit written, then the commit fails.
/// </summary>
internal static class KitSalesAuditedCommit
{
    public static async Task CommitAfterAuditAsync(DbTransaction tx, Func<Task> writeAudit)
    {
        try
        {
            await writeAudit();
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }

        await tx.CommitAsync(CancellationToken.None);
    }
}
