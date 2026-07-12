using System.Data;
using System.Text.Json;
using Dapper;

namespace KitPlatform.Infrastructure.Kernel.Party;

/// <summary>
/// Keeps <c>kit_common.party_party</c> in sync when legacy <c>customers</c> rows are created or updated.
/// Phase D: <see cref="CreateCustomerPartyFirstAsync"/> inserts party before customer row.
/// </summary>
internal static class KernelPartyWriter
{
    /// <summary>Party-first create — caller inserts customer with returned party id and pre-assigned customer id.</summary>
    public static async Task<Guid> CreateCustomerPartyFirstAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid tenantId,
        Guid customerId,
        string customerCode,
        string fullName,
        string phone,
        string? email,
        Guid? workspaceId = null,
        CancellationToken cancellationToken = default)
    {
        var partyId = Guid.NewGuid();
        var metadata = JsonSerializer.Serialize(new { phone, email, source = "party_first_create" });

        const string insertPartySql = """
            INSERT INTO kit_common.party_party (
                id, tenant_id, workspace_id, party_type, party_code, display_name,
                legacy_entity_type, legacy_entity_id, metadata
            )
            VALUES (
                @Id, @TenantId, @WorkspaceId, 'person', @PartyCode, @DisplayName,
                'customer', @CustomerId, @Metadata::jsonb
            )
            """;

        await conn.ExecuteAsync(new CommandDefinition(insertPartySql, new
        {
            Id = partyId,
            TenantId = tenantId,
            WorkspaceId = workspaceId,
            PartyCode = customerCode,
            DisplayName = fullName,
            CustomerId = customerId,
            Metadata = metadata,
        }, tx, cancellationToken: cancellationToken));

        await UpsertCustomerIdentifiersAsync(
            conn, tx, tenantId, partyId, phone, email, workspaceId, cancellationToken);

        return partyId;
    }

    public static async Task<Guid> EnsureCustomerPartyAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid tenantId,
        Guid customerId,
        string customerCode,
        string fullName,
        string phone,
        string? email,
        Guid? workspaceId = null,
        CancellationToken cancellationToken = default)
    {
        const string existingSql = """
            SELECT party_id
            FROM public.customers
            WHERE id = @CustomerId AND tenant_id = @TenantId AND deleted_at IS NULL
            """;

        var existingPartyId = await conn.QuerySingleOrDefaultAsync<Guid?>(
            existingSql,
            new { CustomerId = customerId, TenantId = tenantId },
            tx);

        if (existingPartyId is Guid linked)
        {
            await SyncCustomerPartyAsync(
                conn, tx, tenantId, customerId, customerCode, fullName, phone, email, workspaceId, cancellationToken);
            return linked;
        }

        const string legacyPartySql = """
            SELECT id
            FROM kit_common.party_party
            WHERE tenant_id = @TenantId
              AND legacy_entity_type = 'customer'
              AND legacy_entity_id = @CustomerId
              AND deleted_at IS NULL
            LIMIT 1
            """;

        var legacyPartyId = await conn.QuerySingleOrDefaultAsync<Guid?>(
            legacyPartySql,
            new { TenantId = tenantId, CustomerId = customerId },
            tx);

        Guid partyId;
        if (legacyPartyId is Guid found)
        {
            partyId = found;
        }
        else
        {
            partyId = Guid.NewGuid();
            var metadata = JsonSerializer.Serialize(new { phone, email, source = "app_customer_create" });

            const string insertPartySql = """
                INSERT INTO kit_common.party_party (
                    id, tenant_id, workspace_id, party_type, party_code, display_name,
                    legacy_entity_type, legacy_entity_id, metadata
                )
                VALUES (
                    @Id, @TenantId, @WorkspaceId, 'person', @PartyCode, @DisplayName,
                    'customer', @CustomerId, @Metadata::jsonb
                )
                """;

            await conn.ExecuteAsync(new CommandDefinition(insertPartySql, new
            {
                Id = partyId,
                TenantId = tenantId,
                WorkspaceId = workspaceId,
                PartyCode = customerCode,
                DisplayName = fullName,
                CustomerId = customerId,
                Metadata = metadata,
            }, tx, cancellationToken: cancellationToken));
        }

        await conn.ExecuteAsync(new CommandDefinition("""
            UPDATE public.customers
            SET party_id = @PartyId, updated_at = NOW()
            WHERE id = @CustomerId AND tenant_id = @TenantId AND party_id IS NULL
            """, new { PartyId = partyId, CustomerId = customerId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));

        await UpsertCustomerIdentifiersAsync(
            conn, tx, tenantId, partyId, phone, email, workspaceId, cancellationToken);

        return partyId;
    }

    public static async Task SyncCustomerPartyAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid tenantId,
        Guid customerId,
        string customerCode,
        string fullName,
        string phone,
        string? email,
        Guid? workspaceId = null,
        CancellationToken cancellationToken = default)
    {
        const string partyIdSql = """
            SELECT party_id
            FROM public.customers
            WHERE id = @CustomerId AND tenant_id = @TenantId AND deleted_at IS NULL
            """;

        var partyId = await conn.QuerySingleOrDefaultAsync<Guid?>(
            partyIdSql,
            new { CustomerId = customerId, TenantId = tenantId },
            tx);

        if (partyId is null)
        {
            await EnsureCustomerPartyAsync(
                conn, tx, tenantId, customerId, customerCode, fullName, phone, email, workspaceId, cancellationToken);
            return;
        }

        var metadata = JsonSerializer.Serialize(new { phone, email, source = "app_customer_update" });

        await conn.ExecuteAsync(new CommandDefinition("""
            UPDATE kit_common.party_party
            SET party_code = @PartyCode,
                display_name = @DisplayName,
                workspace_id = COALESCE(workspace_id, @WorkspaceId),
                metadata = @Metadata::jsonb,
                updated_at = NOW()
            WHERE id = @PartyId AND tenant_id = @TenantId AND deleted_at IS NULL
            """, new
        {
            PartyId = partyId.Value,
            TenantId = tenantId,
            WorkspaceId = workspaceId,
            PartyCode = customerCode,
            DisplayName = fullName,
            Metadata = metadata,
        }, tx, cancellationToken: cancellationToken));

        await UpsertCustomerIdentifiersAsync(
            conn, tx, tenantId, partyId.Value, phone, email, workspaceId, cancellationToken);
    }

    private static async Task UpsertCustomerIdentifiersAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid tenantId,
        Guid partyId,
        string phone,
        string? email,
        Guid? workspaceId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(phone))
        {
            await conn.ExecuteAsync(new CommandDefinition("""
                DELETE FROM kit_common.party_identifier
                WHERE party_id = @PartyId AND identifier_type = 'phone'
                """, new { PartyId = partyId }, tx, cancellationToken: cancellationToken));

            await conn.ExecuteAsync(new CommandDefinition("""
                INSERT INTO kit_common.party_identifier (
                    tenant_id, party_id, identifier_type, identifier_value, is_primary
                )
                SELECT @TenantId, @PartyId, 'phone', @Value, TRUE
                WHERE NOT EXISTS (
                    SELECT 1 FROM kit_common.party_identifier
                    WHERE tenant_id = @TenantId
                      AND identifier_type = 'phone'
                      AND identifier_value = @Value
                      AND deleted_at IS NULL
                      AND status = 1
                )
                """, new { TenantId = tenantId, PartyId = partyId, Value = phone }, tx, cancellationToken: cancellationToken));
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            await conn.ExecuteAsync(new CommandDefinition("""
                DELETE FROM kit_common.party_identifier
                WHERE party_id = @PartyId AND identifier_type = 'email'
                """, new { PartyId = partyId }, tx, cancellationToken: cancellationToken));

            await conn.ExecuteAsync(new CommandDefinition("""
                INSERT INTO kit_common.party_identifier (
                    tenant_id, party_id, identifier_type, identifier_value, is_primary
                )
                SELECT @TenantId, @PartyId, 'email', @Value, FALSE
                WHERE NOT EXISTS (
                    SELECT 1 FROM kit_common.party_identifier
                    WHERE tenant_id = @TenantId
                      AND identifier_type = 'email'
                      AND identifier_value = @Value
                      AND deleted_at IS NULL
                      AND status = 1
                )
                """, new { TenantId = tenantId, PartyId = partyId, Value = email.Trim() }, tx, cancellationToken: cancellationToken));
        }
    }
}
