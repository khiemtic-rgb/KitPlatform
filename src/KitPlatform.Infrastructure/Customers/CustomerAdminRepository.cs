using System.Globalization;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Customers;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Infrastructure.Kernel.Party;
using KitPlatform.Infrastructure.Kernel.Pharmacy;
using Npgsql;

namespace KitPlatform.Infrastructure.Customers;

internal sealed class CustomerAdminRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public CustomerAdminRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;
    private Guid? WorkspaceId => _tenant.WorkspaceId;

    public async Task<(IReadOnlyList<CustomerAdminListItemDto> Items, int Total)> ListAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string> { "c.tenant_id = @TenantId", "c.deleted_at IS NULL" };
        var args = new DynamicParameters();
        args.Add("TenantId", TenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            conditions.Add(KernelPartyReader.CustomerSearchFilter);
            args.Add("Search", $"%{search.Trim()}%");
        }

        var where = string.Join(" AND ", conditions);
        var countSql = $"""
            SELECT COUNT(*)::int
            FROM customers c
            {KernelPartyReader.CustomerPartyJoins}
            WHERE {where}
            """;
        var listSql = $"""
            SELECT
                {KernelPartyReader.CustomerListSelect}
            FROM customers c
            {KernelPartyReader.CustomerPartyJoins}
            WHERE {where}
            ORDER BY FullName
            LIMIT @PageSize OFFSET @Offset
            """;

        args.Add("PageSize", pageSize);
        args.Add("Offset", (page - 1) * pageSize);

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var total = await conn.ExecuteScalarAsync<int>(countSql, args);
        var rows = await conn.QueryAsync<CustomerListRow>(listSql, args);
        var items = rows.Select(MapListItem).ToList();
        return (items, total);
    }

    public async Task<SimilarCustomerClustersResult> GetSimilarClustersAsync(
        double similarityThreshold,
        CancellationToken cancellationToken)
    {
        var threshold = Math.Clamp(similarityThreshold, 0.5, 0.99);

        // Must match ix_customers_full_name_trgm expression exactly for GIN/% to apply.
        const string nameNormExpr =
            "lower(trim(regexp_replace(coalesce({0}.full_name, ''), '\\s+', ' ', 'g')))";
        const string phoneDigitsExpr = """
            CASE
                WHEN regexp_replace(coalesce({0}.phone, ''), '[^0-9]', '', 'g') ~ '^84[0-9]{8,}$'
                    THEN '0' || substring(regexp_replace(coalesce({0}.phone, ''), '[^0-9]', '', 'g') from 3)
                ELSE regexp_replace(coalesce({0}.phone, ''), '[^0-9]', '', 'g')
            END
            """;

        var phoneDigitsC = phoneDigitsExpr.Replace("{0}", "c", StringComparison.Ordinal);
        var nameNormA = nameNormExpr.Replace("{0}", "a", StringComparison.Ordinal);
        var nameNormB = nameNormExpr.Replace("{0}", "b", StringComparison.Ordinal);

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        // 1) Phone duplicate keys only (no per-row order counts — that made ~3k customers hang).
        var phoneKeysSql = $"""
            WITH active AS (
                SELECT {phoneDigitsC} AS PhoneDigits
                FROM customers c
                WHERE c.tenant_id = @TenantId
                  AND c.deleted_at IS NULL
                  AND c.status = 1
            )
            SELECT PhoneDigits
            FROM active
            WHERE length(PhoneDigits) >= 9
            GROUP BY PhoneDigits
            HAVING COUNT(*) >= 2
            LIMIT 500
            """;
        var phoneKeys = (await conn.QueryAsync<string>(
                new CommandDefinition(phoneKeysSql, new { TenantId }, cancellationToken: cancellationToken)))
            .ToArray();

        // 2) Fuzzy name pairs via pg_trgm `%` + GIN (same pattern as product similar-clusters).
        // Full nested CTE cross-join is O(n²) and times out (~3k customers → 30s+ / empty UI).
        List<(Guid IdA, Guid IdB, double Score)> pairs = [];
        try
        {
            await conn.ExecuteAsync(
                new CommandDefinition(
                    "SELECT set_config('pg_trgm.similarity_threshold', @Th, true)",
                    new { Th = threshold.ToString("0.###", CultureInfo.InvariantCulture) },
                    cancellationToken: cancellationToken));

            var pairSql = $"""
                SELECT
                    a.id AS IdA,
                    b.id AS IdB,
                    similarity({nameNormA}, {nameNormB})::float8 AS Score
                FROM customers a
                INNER JOIN customers b
                  ON a.tenant_id = b.tenant_id
                 AND a.id < b.id
                 AND {nameNormA} % {nameNormB}
                WHERE a.tenant_id = @TenantId
                  AND a.deleted_at IS NULL AND a.status = 1
                  AND b.deleted_at IS NULL AND b.status = 1
                  AND length(trim(coalesce(a.full_name, ''))) >= 3
                  AND length(trim(coalesce(b.full_name, ''))) >= 3
                  AND {nameNormA} <> {nameNormB}
                  AND similarity({nameNormA}, {nameNormB}) >= @Threshold
                ORDER BY Score DESC
                LIMIT 3000
                """;
            pairs = (await conn.QueryAsync<(Guid IdA, Guid IdB, double Score)>(
                    new CommandDefinition(
                        pairSql,
                        new { TenantId, Threshold = threshold },
                        cancellationToken: cancellationToken,
                        commandTimeout: 45)))
                .ToList();
        }
        catch (PostgresException ex) when (ex.SqlState is "42883" or "42704")
        {
            // pg_trgm missing — still return phone clusters below
            pairs = [];
        }

        var pairIds = pairs
            .SelectMany(p => new[] { p.IdA, p.IdB })
            .Distinct()
            .ToArray();

        if (phoneKeys.Length == 0 && pairIds.Length == 0)
            return new SimilarCustomerClustersResult([], 0, 0, threshold);

        // Load only customers that participate in a cluster candidate.
        var loadSql = $"""
            SELECT
                c.id AS Id,
                c.customer_code AS CustomerCode,
                c.full_name AS FullName,
                c.phone AS Phone,
                c.email::text AS Email,
                c.status AS Status,
                c.created_at AS CreatedAt,
                {nameNormExpr.Replace("{0}", "c", StringComparison.Ordinal)} AS NameNorm,
                {phoneDigitsC} AS PhoneDigits,
                0 AS OrderCount
            FROM customers c
            WHERE c.tenant_id = @TenantId
              AND c.deleted_at IS NULL
              AND c.status = 1
              AND (
                    (@HasPhoneKeys AND ({phoneDigitsC}) = ANY(@PhoneDigits))
                 OR (@HasPairIds AND c.id = ANY(@Ids))
              )
            """;

        var all = (await conn.QueryAsync<SimilarCustomerRow>(
                new CommandDefinition(
                    loadSql,
                    new
                    {
                        TenantId,
                        HasPhoneKeys = phoneKeys.Length > 0,
                        PhoneDigits = phoneKeys,
                        HasPairIds = pairIds.Length > 0,
                        Ids = pairIds,
                    },
                    cancellationToken: cancellationToken)))
            .ToList();

        if (all.Count == 0)
            return new SimilarCustomerClustersResult([], 0, 0, threshold);

        // Batch order counts for cluster members only (not the whole tenant).
        var orderCounts = (await conn.QueryAsync<(Guid CustomerId, int OrderCount)>(
                new CommandDefinition(
                    """
                    SELECT o.customer_id AS CustomerId, COUNT(*)::int AS OrderCount
                    FROM pack_pharmacy.v_sales_order o
                    WHERE o.tenant_id = @TenantId
                      AND o.customer_id = ANY(@Ids)
                    GROUP BY o.customer_id
                    """,
                    new { TenantId, Ids = all.Select(x => x.Id).ToArray() },
                    cancellationToken: cancellationToken)))
            .ToDictionary(x => x.CustomerId, x => x.OrderCount);

        var byId = all.ToDictionary(
            x => x.Id,
            x => new SimilarCustomerRow
            {
                Id = x.Id,
                CustomerCode = x.CustomerCode,
                FullName = x.FullName,
                Phone = x.Phone,
                Email = x.Email,
                Status = x.Status,
                CreatedAt = x.CreatedAt,
                NameNorm = x.NameNorm,
                PhoneDigits = x.PhoneDigits,
                OrderCount = orderCounts.GetValueOrDefault(x.Id),
            });

        var clusters = new List<SimilarCustomerClusterDto>();

        foreach (var group in byId.Values
            .Where(x => x.PhoneDigits.Length >= 9)
            .GroupBy(x => x.PhoneDigits)
            .Where(g => g.Count() >= 2))
        {
            var members = group.OrderBy(x => x.CustomerCode).ToList();
            clusters.Add(new SimilarCustomerClusterDto(
                $"phone:{group.Key}",
                "phone",
                $"Trùng SĐT · {members[0].Phone}",
                MaxSimilarity: 1,
                members.Select(ToMember).ToList()));
        }

        var inPhoneCluster = clusters
            .Where(c => c.MatchKind == "phone")
            .SelectMany(c => c.Customers.Select(m => m.Id))
            .ToHashSet();

        if (pairs.Count > 0)
        {
            var parent = new Dictionary<Guid, Guid>();
            Guid Find(Guid x)
            {
                if (!parent.ContainsKey(x)) parent[x] = x;
                while (parent[x] != x)
                {
                    parent[x] = parent[parent[x]];
                    x = parent[x];
                }
                return x;
            }

            void Union(Guid a, Guid b)
            {
                var ra = Find(a);
                var rb = Find(b);
                if (ra != rb) parent[rb] = ra;
            }

            var maxScore = new Dictionary<Guid, double>();
            foreach (var (a, b, score) in pairs)
            {
                Union(a, b);
                maxScore[a] = Math.Max(maxScore.GetValueOrDefault(a), score);
                maxScore[b] = Math.Max(maxScore.GetValueOrDefault(b), score);
            }

            var groups = new Dictionary<Guid, List<SimilarCustomerRow>>();
            var groupMax = new Dictionary<Guid, double>();
            foreach (var id in parent.Keys)
            {
                if (!byId.TryGetValue(id, out var row)) continue;
                var root = Find(id);
                if (!groups.TryGetValue(root, out var list))
                {
                    list = [];
                    groups[root] = list;
                }
                list.Add(row);
                groupMax[root] = Math.Max(groupMax.GetValueOrDefault(root), maxScore.GetValueOrDefault(id));
            }

            foreach (var (root, members) in groups.Where(g => g.Value.Count >= 2))
            {
                if (members.All(m => inPhoneCluster.Contains(m.Id))
                    && members.Select(m => m.PhoneDigits).Distinct().Count() == 1)
                    continue;

                members.Sort((a, b) => string.Compare(a.CustomerCode, b.CustomerCode, StringComparison.Ordinal));
                var sim = Math.Round(groupMax.GetValueOrDefault(root), 4);
                clusters.Add(new SimilarCustomerClusterDto(
                    $"name:{root:N}",
                    "name",
                    $"Giống tên · {members[0].FullName}",
                    sim,
                    members.Select(ToMember).ToList()));
            }
        }

        clusters = clusters
            .OrderBy(c => c.MatchKind == "phone" ? 0 : 1)
            .ThenByDescending(c => c.MaxSimilarity ?? 0)
            .ThenBy(c => c.DisplayLabel, StringComparer.OrdinalIgnoreCase)
            .Take(200)
            .ToList();

        return new SimilarCustomerClustersResult(
            clusters,
            clusters.Count,
            clusters.Sum(c => c.Customers.Count),
            threshold);

        static SimilarCustomerMemberDto ToMember(SimilarCustomerRow r) =>
            new(r.Id, r.CustomerCode, r.FullName, r.Phone, r.Email, r.Status, ToOffset(r.CreatedAt), r.OrderCount);
    }

    public async Task<SimilarCustomerNamesResult> FindSimilarNamesAsync(
        string fullName,
        Guid? excludeCustomerId,
        double similarityThreshold,
        CancellationToken cancellationToken)
    {
        var raw = fullName.Trim();
        var normalized = System.Text.RegularExpressions.Regex.Replace(raw.ToLowerInvariant(), @"\s+", " ").Trim();
        if (normalized.Length < 2)
            return new SimilarCustomerNamesResult([], false);

        var threshold = Math.Clamp(similarityThreshold, 0.5, 0.99);

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        const string exactSql = """
            SELECT
                c.id AS Id,
                c.customer_code AS CustomerCode,
                c.full_name AS FullName,
                c.phone AS Phone,
                1.0::float8 AS SimilarityScore
            FROM customers c
            WHERE c.tenant_id = @TenantId
              AND c.deleted_at IS NULL
              AND c.status = 1
              AND lower(trim(regexp_replace(c.full_name, '\s+', ' ', 'g'))) = @Normalized
              AND (@ExcludeId IS NULL OR c.id <> @ExcludeId)
            LIMIT 5
            """;

        var exact = (await conn.QueryAsync<SimilarNameRow>(exactSql, new
        {
            TenantId,
            Normalized = normalized,
            ExcludeId = excludeCustomerId,
        })).ToList();

        List<SimilarNameRow> fuzzy = [];
        try
        {
            const string fuzzySql = """
                SELECT
                    c.id AS Id,
                    c.customer_code AS CustomerCode,
                    c.full_name AS FullName,
                    c.phone AS Phone,
                    similarity(
                        lower(trim(regexp_replace(c.full_name, '\s+', ' ', 'g'))),
                        @Normalized
                    )::float8 AS SimilarityScore
                FROM customers c
                WHERE c.tenant_id = @TenantId
                  AND c.deleted_at IS NULL
                  AND c.status = 1
                  AND length(trim(c.full_name)) >= 2
                  AND similarity(
                        lower(trim(regexp_replace(c.full_name, '\s+', ' ', 'g'))),
                        @Normalized
                      ) >= @Threshold
                  AND (@ExcludeId IS NULL OR c.id <> @ExcludeId)
                ORDER BY SimilarityScore DESC
                LIMIT 5
                """;
            fuzzy = (await conn.QueryAsync<SimilarNameRow>(fuzzySql, new
            {
                TenantId,
                Normalized = normalized,
                Threshold = threshold,
                ExcludeId = excludeCustomerId,
            })).ToList();
        }
        catch
        {
            fuzzy = [];
        }

        var matches = exact
            .Concat(fuzzy.Where(f => exact.All(e => e.Id != f.Id)))
            .OrderByDescending(m => m.SimilarityScore)
            .Take(5)
            .Select(m => new SimilarCustomerNameDto(
                m.Id, m.CustomerCode, m.FullName, m.Phone, m.SimilarityScore))
            .ToList();

        return new SimilarCustomerNamesResult(matches, exact.Count > 0);
    }

    public async Task<CustomerDetailDto?> GetAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT
                {KernelPartyReader.CustomerDetailSelect}
            FROM customers c
            {KernelPartyReader.CustomerPartyJoins}
            LEFT JOIN customer_accounts ca
                ON ca.customer_id = c.id
               AND ca.tenant_id = c.tenant_id
               AND ca.status = 1
            WHERE c.id = @CustomerId
              AND c.tenant_id = @TenantId
              AND c.deleted_at IS NULL
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<CustomerDetailRow>(
            sql,
            new { CustomerId = customerId, TenantId });
        return row is null ? null : MapDetail(row);
    }

    public async Task<(IReadOnlyList<CustomerOrderListItemDto> Items, int Total)> GetOrdersAsync(
        Guid customerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        const string countSql = """
            SELECT COUNT(*)::int
            FROM pack_pharmacy.v_sales_order o
            WHERE o.tenant_id = @TenantId
              AND o.customer_id = @CustomerId
            """;

        var listSql = $"""
            SELECT
                o.id AS Id,
                o.order_number AS OrderNumber,
                o.status AS Status,
                o.order_date AS OrderDate,
                o.total_amount AS TotalAmount,
                (SELECT COUNT(*)::int FROM sales_order_items i WHERE i.sales_order_id = o.id) AS ItemCount
            FROM {PackPharmacyReadViews.SalesOrder} o
            WHERE o.tenant_id = @TenantId
              AND o.customer_id = @CustomerId
            ORDER BY o.order_date DESC
            LIMIT @PageSize OFFSET @Offset
            """;

        var args = new
        {
            TenantId,
            CustomerId = customerId,
            PageSize = pageSize,
            Offset = (page - 1) * pageSize,
        };

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var total = await conn.ExecuteScalarAsync<int>(countSql, args);
        var rows = await conn.QueryAsync<CustomerOrderRow>(listSql, args);
        var items = rows.Select(MapOrder).ToList();
        return (items, total);
    }

    public async Task<string> GenerateCustomerCodeAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(MAX(
                CASE
                    WHEN customer_code ~ '^KH[0-9]+$'
                    THEN CAST(SUBSTRING(customer_code FROM 3) AS INT)
                END
            ), 0) + 1
            FROM customers
            WHERE tenant_id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var next = await conn.ExecuteScalarAsync<int>(sql, new { TenantId });
        return $"KH{next:D3}";
    }

    public async Task<bool> PhoneExistsAsync(
        string phone,
        Guid? excludeCustomerId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM customers
                WHERE tenant_id = @TenantId
                  AND phone = @Phone
                  AND deleted_at IS NULL
                  AND (@ExcludeCustomerId IS NULL OR id <> @ExcludeCustomerId)
            )
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(
            sql,
            new { TenantId, Phone = phone, ExcludeCustomerId = excludeCustomerId });
    }

    public async Task<bool> CustomerCodeExistsAsync(
        string customerCode,
        Guid? excludeCustomerId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM customers
                WHERE tenant_id = @TenantId
                  AND UPPER(customer_code) = UPPER(@CustomerCode)
                  AND deleted_at IS NULL
                  AND (@ExcludeCustomerId IS NULL OR id <> @ExcludeCustomerId)
            )
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(
            sql,
            new { TenantId, CustomerCode = customerCode, ExcludeCustomerId = excludeCustomerId });
    }

    public async Task<Guid> CreateAsync(
        string customerCode,
        string fullName,
        string phone,
        string? email,
        DateOnly? dateOfBirth,
        short? gender,
        string? addressLine,
        string? idNumber,
        string? emergencyContactName,
        string? emergencyContactPhone,
        string? clinicalNotes,
        Guid? customerGroupId,
        CancellationToken cancellationToken)
    {
        var customerId = Guid.NewGuid();

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var partyId = await KernelPartyWriter.CreateCustomerPartyFirstAsync(
            conn,
            tx,
            TenantId,
            customerId,
            customerCode,
            fullName,
            phone,
            email,
            WorkspaceId,
            cancellationToken);

        const string sql = """
            INSERT INTO customers (
                id, tenant_id, party_id, customer_code, full_name, phone, email, date_of_birth, gender, status,
                address_line, id_number, emergency_contact_name, emergency_contact_phone, clinical_notes,
                customer_group_id
            )
            VALUES (
                @CustomerId, @TenantId, @PartyId, @CustomerCode, @FullName, @Phone, @Email, @DateOfBirth, @Gender, 1,
                @AddressLine, @IdNumber, @EmergencyContactName, @EmergencyContactPhone, @ClinicalNotes,
                @CustomerGroupId
            )
            RETURNING id
            """;

        var insertedId = await conn.QuerySingleAsync<Guid>(
            sql,
            new
            {
                CustomerId = customerId,
                TenantId,
                PartyId = partyId,
                CustomerCode = customerCode,
                FullName = fullName,
                Phone = phone,
                Email = email,
                DateOfBirth = dateOfBirth,
                Gender = gender,
                AddressLine = addressLine,
                IdNumber = idNumber,
                EmergencyContactName = emergencyContactName,
                EmergencyContactPhone = emergencyContactPhone,
                ClinicalNotes = clinicalNotes,
                CustomerGroupId = customerGroupId,
            },
            tx);

        await tx.CommitAsync(cancellationToken);
        return insertedId;
    }

    public async Task<bool> UpdateAsync(
        Guid customerId,
        string customerCode,
        string fullName,
        string phone,
        string? email,
        DateOnly? dateOfBirth,
        short? gender,
        short status,
        bool allowCredit,
        decimal? creditLimit,
        string? addressLine,
        string? idNumber,
        string? emergencyContactName,
        string? emergencyContactPhone,
        string? clinicalNotes,
        Guid? customerGroupId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE customers SET
                customer_code = @CustomerCode,
                full_name = @FullName,
                phone = @Phone,
                email = @Email,
                date_of_birth = @DateOfBirth,
                gender = @Gender,
                status = @Status,
                allow_credit = @AllowCredit,
                credit_limit = @CreditLimit,
                address_line = @AddressLine,
                id_number = @IdNumber,
                emergency_contact_name = @EmergencyContactName,
                emergency_contact_phone = @EmergencyContactPhone,
                clinical_notes = @ClinicalNotes,
                customer_group_id = @CustomerGroupId,
                updated_at = NOW()
            WHERE id = @CustomerId
              AND tenant_id = @TenantId
              AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var rows = await conn.ExecuteAsync(
            sql,
            new
            {
                CustomerId = customerId,
                TenantId,
                CustomerCode = customerCode,
                FullName = fullName,
                Phone = phone,
                Email = email,
                DateOfBirth = dateOfBirth,
                Gender = gender,
                Status = status,
                AllowCredit = allowCredit,
                CreditLimit = creditLimit,
                AddressLine = addressLine,
                IdNumber = idNumber,
                EmergencyContactName = emergencyContactName,
                EmergencyContactPhone = emergencyContactPhone,
                ClinicalNotes = clinicalNotes,
                CustomerGroupId = customerGroupId,
            },
            tx);

        if (rows > 0)
        {
            await KernelPartyWriter.SyncCustomerPartyAsync(
                conn,
                tx,
                TenantId,
                customerId,
                customerCode,
                fullName,
                phone,
                email,
                WorkspaceId,
                cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        return rows > 0;
    }

    private static CustomerAdminListItemDto MapListItem(CustomerListRow row) =>
        new(
            row.Id,
            row.CustomerCode,
            row.FullName,
            row.Phone,
            row.Email,
            row.Status,
            ToOffset(row.CreatedAt),
            row.CustomerGroupId,
            row.CustomerGroupName,
            row.GroupDiscountPercent);

    private static CustomerDetailDto MapDetail(CustomerDetailRow row) =>
        new(
            row.Id,
            row.CustomerCode,
            row.FullName,
            row.Phone,
            row.Email,
            row.DateOfBirth,
            row.Gender,
            row.Status,
            ToOffset(row.CreatedAt),
            row.HasAppAccount,
            row.AppVerified,
            row.AppLastLoginAt.HasValue ? ToOffset(row.AppLastLoginAt.Value) : null,
            row.AllowCredit,
            row.CreditLimit,
            row.AddressLine,
            row.IdNumber,
            row.EmergencyContactName,
            row.EmergencyContactPhone,
            row.ClinicalNotes,
            row.CustomerGroupId,
            row.CustomerGroupName,
            row.GroupDiscountPercent);

    private static CustomerOrderListItemDto MapOrder(CustomerOrderRow row) =>
        new(
            row.Id,
            row.OrderNumber,
            row.Status,
            ToOffset(row.OrderDate),
            row.TotalAmount,
            row.ItemCount);

    private static DateTimeOffset ToOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed class CustomerListRow
    {
        public Guid Id { get; init; }
        public string CustomerCode { get; init; } = "";
        public string FullName { get; init; } = "";
        public string Phone { get; init; } = "";
        public string? Email { get; init; }
        public short Status { get; init; }
        public DateTime CreatedAt { get; init; }
        public Guid? CustomerGroupId { get; init; }
        public string? CustomerGroupName { get; init; }
        public decimal GroupDiscountPercent { get; init; }
    }

    private sealed class SimilarNameRow
    {
        public Guid Id { get; init; }
        public string CustomerCode { get; init; } = "";
        public string FullName { get; init; } = "";
        public string Phone { get; init; } = "";
        public double SimilarityScore { get; init; }
    }

    private sealed class SimilarCustomerRow
    {
        public Guid Id { get; init; }
        public string CustomerCode { get; init; } = "";
        public string FullName { get; init; } = "";
        public string Phone { get; init; } = "";
        public string? Email { get; init; }
        public short Status { get; init; }
        public DateTime CreatedAt { get; init; }
        public string NameNorm { get; init; } = "";
        public string PhoneDigits { get; init; } = "";
        public int OrderCount { get; init; }
    }

    private sealed class CustomerDetailRow
    {
        public Guid Id { get; init; }
        public string CustomerCode { get; init; } = "";
        public string FullName { get; init; } = "";
        public string Phone { get; init; } = "";
        public string? Email { get; init; }
        public DateOnly? DateOfBirth { get; init; }
        public short? Gender { get; init; }
        public short Status { get; init; }
        public DateTime CreatedAt { get; init; }
        public bool HasAppAccount { get; init; }
        public bool? AppVerified { get; init; }
        public DateTime? AppLastLoginAt { get; init; }
        public bool AllowCredit { get; init; }
        public decimal? CreditLimit { get; init; }
        public string? AddressLine { get; init; }
        public string? IdNumber { get; init; }
        public string? EmergencyContactName { get; init; }
        public string? EmergencyContactPhone { get; init; }
        public string? ClinicalNotes { get; init; }
        public Guid? CustomerGroupId { get; init; }
        public string? CustomerGroupName { get; init; }
        public decimal GroupDiscountPercent { get; init; }
    }

    private sealed class CustomerOrderRow
    {
        public Guid Id { get; init; }
        public string OrderNumber { get; init; } = "";
        public short Status { get; init; }
        public DateTime OrderDate { get; init; }
        public decimal TotalAmount { get; init; }
        public int ItemCount { get; init; }
    }
}
