namespace KitPlatform.Infrastructure.Kernel.Party;

/// <summary>Phase C read cutover — identity fields from <c>kit_common.party_party</c> with legacy fallback.</summary>
internal static class KernelPartyReader
{
    public const string CustomerPartyJoins = """
        LEFT JOIN kit_common.party_party p
            ON p.id = c.party_id
           AND p.tenant_id = c.tenant_id
           AND p.deleted_at IS NULL
        LEFT JOIN LATERAL (
            SELECT pi.identifier_value
            FROM kit_common.party_identifier pi
            WHERE pi.party_id = p.id
              AND pi.identifier_type = 'phone'
              AND pi.deleted_at IS NULL
              AND pi.status = 1
            ORDER BY pi.is_primary DESC, pi.created_at
            LIMIT 1
        ) pi_phone ON TRUE
        LEFT JOIN LATERAL (
            SELECT pi.identifier_value
            FROM kit_common.party_identifier pi
            WHERE pi.party_id = p.id
              AND pi.identifier_type = 'email'
              AND pi.deleted_at IS NULL
              AND pi.status = 1
            ORDER BY pi.is_primary DESC, pi.created_at
            LIMIT 1
        ) pi_email ON TRUE
        """;

    public const string CustomerListSelect = """
        c.id AS Id,
        COALESCE(p.party_code, c.customer_code) AS CustomerCode,
        COALESCE(p.display_name, c.full_name) AS FullName,
        COALESCE(pi_phone.identifier_value, c.phone) AS Phone,
        COALESCE(pi_email.identifier_value, c.email::text) AS Email,
        c.status AS Status,
        c.created_at AS CreatedAt
        """;

    public const string CustomerDetailSelect = """
        c.id AS Id,
        COALESCE(p.party_code, c.customer_code) AS CustomerCode,
        COALESCE(p.display_name, c.full_name) AS FullName,
        COALESCE(pi_phone.identifier_value, c.phone) AS Phone,
        COALESCE(pi_email.identifier_value, c.email::text) AS Email,
        c.date_of_birth AS DateOfBirth,
        c.gender AS Gender,
        c.status AS Status,
        c.created_at AS CreatedAt,
        c.allow_credit AS AllowCredit,
        c.credit_limit AS CreditLimit,
        c.address_line AS AddressLine,
        c.id_number AS IdNumber,
        c.emergency_contact_name AS EmergencyContactName,
        c.emergency_contact_phone AS EmergencyContactPhone,
        c.clinical_notes AS ClinicalNotes,
        (ca.id IS NOT NULL) AS HasAppAccount,
        ca.is_verified AS AppVerified,
        ca.last_login_at AS AppLastLoginAt
        """;

    public const string CustomerSearchFilter = """
        (
            c.full_name ILIKE @Search
            OR c.phone ILIKE @Search
            OR c.customer_code ILIKE @Search
            OR c.email::text ILIKE @Search
            OR COALESCE(c.id_number, '') ILIKE @Search
            OR COALESCE(c.address_line, '') ILIKE @Search
            OR p.display_name ILIKE @Search
            OR p.party_code ILIKE @Search
            OR EXISTS (
                SELECT 1
                FROM kit_common.party_identifier pi
                WHERE pi.party_id = p.id
                  AND pi.deleted_at IS NULL
                  AND pi.status = 1
                  AND pi.identifier_value ILIKE @Search
            )
        )
        """;
}
