using Dapper;
using PharmaCore.Application.Abstractions;
using PharmaCore.Application.Identity;
using PharmaCore.Infrastructure.Data;

namespace PharmaCore.Infrastructure.Identity;

internal sealed class IdentityAdminRepository
{
    private const string PgEmptyArrayLiteral = "'{}'";

    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public IdentityAdminRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<IReadOnlyList<BranchAdminListItemDto>> ListBranchesAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                b.id AS Id,
                b.branch_code AS BranchCode,
                b.branch_name AS BranchName,
                b.address AS Address,
                b.phone AS Phone,
                b.is_head_office AS IsHeadOffice,
                b.status AS Status,
                b.created_at AS CreatedAt
            FROM branches b
            WHERE b.tenant_id = @TenantId AND b.deleted_at IS NULL
            ORDER BY b.is_head_office DESC, b.branch_name
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<BranchRow>(sql, new { TenantId });
        return rows.Select(MapBranchList).ToList();
    }

    public async Task<BranchDetailDto?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                b.id AS Id,
                b.branch_code AS BranchCode,
                b.branch_name AS BranchName,
                b.address AS Address,
                b.phone AS Phone,
                b.is_head_office AS IsHeadOffice,
                b.status AS Status,
                b.created_at AS CreatedAt
            FROM branches b
            WHERE b.id = @BranchId AND b.tenant_id = @TenantId AND b.deleted_at IS NULL
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<BranchRow>(sql, new { BranchId = branchId, TenantId });
        return row is null ? null : MapBranchDetail(row);
    }

    public async Task<bool> BranchCodeExistsAsync(string branchCode, Guid? excludeBranchId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM branches
                WHERE tenant_id = @TenantId
                  AND branch_code = @BranchCode
                  AND deleted_at IS NULL
                  AND (@ExcludeBranchId IS NULL OR id <> @ExcludeBranchId)
            )
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(sql, new { TenantId, BranchCode = branchCode, ExcludeBranchId = excludeBranchId });
    }

    public async Task<Guid> CreateBranchAsync(CreateBranchRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO branches (tenant_id, branch_code, branch_name, address, phone, is_head_office, status)
            VALUES (@TenantId, @BranchCode, @BranchName, @Address, @Phone, @IsHeadOffice, @Status)
            RETURNING id
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<Guid>(sql, new
        {
            TenantId,
            BranchCode = request.BranchCode.Trim(),
            BranchName = request.BranchName.Trim(),
            Address = request.Address?.Trim(),
            Phone = request.Phone?.Trim(),
            request.IsHeadOffice,
            request.Status,
        });
    }

    public async Task<bool> UpdateBranchAsync(Guid branchId, UpdateBranchRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE branches
            SET branch_code = @BranchCode,
                branch_name = @BranchName,
                address = @Address,
                phone = @Phone,
                is_head_office = @IsHeadOffice,
                status = @Status
            WHERE id = @BranchId AND tenant_id = @TenantId AND deleted_at IS NULL
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var affected = await conn.ExecuteAsync(sql, new
        {
            BranchId = branchId,
            TenantId,
            BranchCode = request.BranchCode.Trim(),
            BranchName = request.BranchName.Trim(),
            Address = request.Address?.Trim(),
            Phone = request.Phone?.Trim(),
            request.IsHeadOffice,
            request.Status,
        });
        return affected > 0;
    }

    public async Task<(IReadOnlyList<UserAdminListItemDto> Items, int Total)> ListUsersAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string> { "u.tenant_id = @TenantId", "u.deleted_at IS NULL" };
        var args = new DynamicParameters();
        args.Add("TenantId", TenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            conditions.Add("(u.username ILIKE @Search OR u.email ILIKE @Search OR e.full_name ILIKE @Search)");
            args.Add("Search", $"%{search.Trim()}%");
        }

        var where = string.Join(" AND ", conditions);
        var countSql = $"""
            SELECT COUNT(*)::int
            FROM users u
            LEFT JOIN employees e ON e.id = u.employee_id
            WHERE {where}
            """;

        var listSql = $"""
            SELECT
                u.id AS Id,
                u.username AS Username,
                u.email AS Email,
                u.status AS Status,
                e.full_name AS EmployeeName,
                u.last_login_at AS LastLoginAt,
                u.created_at AS CreatedAt,
                COALESCE(array_agg(DISTINCT r.role_code) FILTER (WHERE r.role_code IS NOT NULL), {PgEmptyArrayLiteral}) AS RoleCodes
            FROM users u
            LEFT JOIN employees e ON e.id = u.employee_id
            LEFT JOIN user_roles ur ON ur.user_id = u.id
            LEFT JOIN roles r ON r.id = ur.role_id
            WHERE {where}
            GROUP BY u.id, u.username, u.email, u.status, e.full_name, u.last_login_at, u.created_at
            ORDER BY u.username
            LIMIT @PageSize OFFSET @Offset
            """;

        args.Add("PageSize", pageSize);
        args.Add("Offset", (page - 1) * pageSize);

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var total = await conn.ExecuteScalarAsync<int>(countSql, args);
        var rows = await conn.QueryAsync<UserListRow>(listSql, args);
        return (rows.Select(MapUserList).ToList(), total);
    }

    public async Task<UserDetailDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                u.id AS Id,
                u.username AS Username,
                u.email AS Email,
                u.status AS Status,
                u.employee_id AS EmployeeId,
                e.full_name AS EmployeeName,
                u.last_login_at AS LastLoginAt,
                u.created_at AS CreatedAt,
                COALESCE(array_agg(DISTINCT r.id) FILTER (WHERE r.id IS NOT NULL), {PgEmptyArrayLiteral}) AS RoleIds,
                COALESCE(array_agg(DISTINCT r.role_code) FILTER (WHERE r.role_code IS NOT NULL), {PgEmptyArrayLiteral}) AS RoleCodes
            FROM users u
            LEFT JOIN employees e ON e.id = u.employee_id
            LEFT JOIN user_roles ur ON ur.user_id = u.id
            LEFT JOIN roles r ON r.id = ur.role_id
            WHERE u.id = @UserId AND u.tenant_id = @TenantId AND u.deleted_at IS NULL
            GROUP BY u.id, u.username, u.email, u.status, u.employee_id, e.full_name, u.last_login_at, u.created_at
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<UserDetailRow>(sql, new { UserId = userId, TenantId });
        return row is null ? null : MapUserDetail(row);
    }

    public async Task<bool> UsernameExistsAsync(string username, Guid? excludeUserId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM users
                WHERE tenant_id = @TenantId
                  AND username = @Username
                  AND deleted_at IS NULL
                  AND (@ExcludeUserId IS NULL OR id <> @ExcludeUserId)
            )
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(sql, new { TenantId, Username = username, ExcludeUserId = excludeUserId });
    }

    public async Task<bool> EmailExistsAsync(string email, Guid? excludeUserId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM users
                WHERE tenant_id = @TenantId
                  AND email = @Email
                  AND deleted_at IS NULL
                  AND (@ExcludeUserId IS NULL OR id <> @ExcludeUserId)
            )
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(sql, new { TenantId, Email = email, ExcludeUserId = excludeUserId });
    }

    public async Task<Guid> CreateUserAsync(
        string username,
        string email,
        string passwordHash,
        short status,
        Guid? employeeId,
        IReadOnlyList<Guid> roleIds,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        const string insertSql = """
            INSERT INTO users (tenant_id, employee_id, username, email, password_hash, status)
            VALUES (@TenantId, @EmployeeId, @Username, @Email, @PasswordHash, @Status)
            RETURNING id
            """;

        var userId = await conn.ExecuteScalarAsync<Guid>(insertSql, new
        {
            TenantId,
            EmployeeId = employeeId,
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            Status = status,
        }, tx);

        await ReplaceUserRolesAsync(conn, tx, userId, roleIds, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return userId;
    }

    public async Task<bool> UpdateUserAsync(
        Guid userId,
        string email,
        short status,
        Guid? employeeId,
        string? passwordHash,
        IReadOnlyList<Guid> roleIds,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        const string sql = """
            UPDATE users
            SET email = @Email,
                status = @Status,
                employee_id = @EmployeeId,
                password_hash = COALESCE(@PasswordHash, password_hash)
            WHERE id = @UserId AND tenant_id = @TenantId AND deleted_at IS NULL
            """;

        var affected = await conn.ExecuteAsync(sql, new
        {
            UserId = userId,
            TenantId,
            Email = email,
            Status = status,
            EmployeeId = employeeId,
            PasswordHash = passwordHash,
        }, tx);

        if (affected == 0)
        {
            await tx.RollbackAsync(cancellationToken);
            return false;
        }

        await ReplaceUserRolesAsync(conn, tx, userId, roleIds, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<RoleAdminListItemDto>> ListRolesAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                r.id AS Id,
                r.role_code AS RoleCode,
                r.role_name AS RoleName,
                r.description AS Description,
                r.status AS Status,
                COUNT(DISTINCT ur.user_id)::int AS UserCount,
                COUNT(DISTINCT rp.permission_id)::int AS PermissionCount
            FROM roles r
            LEFT JOIN user_roles ur ON ur.role_id = r.id
            LEFT JOIN role_permissions rp ON rp.role_id = r.id
            WHERE r.tenant_id = @TenantId
            GROUP BY r.id, r.role_code, r.role_name, r.description, r.status
            ORDER BY r.role_code
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<RoleListRow>(sql, new { TenantId });
        return rows.Select(r => new RoleAdminListItemDto(
            r.Id,
            r.RoleCode,
            r.RoleName,
            r.Description,
            r.Status,
            r.UserCount,
            r.PermissionCount)).ToList();
    }

    public async Task<RoleDetailDto?> GetRoleAsync(Guid roleId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                r.id AS Id,
                r.role_code AS RoleCode,
                r.role_name AS RoleName,
                r.description AS Description,
                r.status AS Status,
                COALESCE(array_agg(DISTINCT p.permission_code) FILTER (WHERE p.permission_code IS NOT NULL), {PgEmptyArrayLiteral}) AS PermissionCodes
            FROM roles r
            LEFT JOIN role_permissions rp ON rp.role_id = r.id
            LEFT JOIN permissions p ON p.id = rp.permission_id
            WHERE r.id = @RoleId AND r.tenant_id = @TenantId
            GROUP BY r.id, r.role_code, r.role_name, r.description, r.status
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<RoleDetailRow>(sql, new { RoleId = roleId, TenantId });
        return row is null
            ? null
            : new RoleDetailDto(
                row.Id,
                row.RoleCode,
                row.RoleName,
                row.Description,
                row.Status,
                row.PermissionCodes ?? Array.Empty<string>());
    }

    public async Task<bool> UpdateRolePermissionsAsync(
        Guid roleId,
        IReadOnlyList<string> permissionCodes,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        const string existsSql = "SELECT EXISTS(SELECT 1 FROM roles WHERE id = @RoleId AND tenant_id = @TenantId)";
        var exists = await conn.ExecuteScalarAsync<bool>(existsSql, new { RoleId = roleId, TenantId }, tx);
        if (!exists)
        {
            await tx.RollbackAsync(cancellationToken);
            return false;
        }

        await conn.ExecuteAsync("DELETE FROM role_permissions WHERE role_id = @RoleId", new { RoleId = roleId }, tx);

        if (permissionCodes.Count > 0)
        {
            const string insertSql = """
                INSERT INTO role_permissions (role_id, permission_id)
                SELECT @RoleId, p.id
                FROM permissions p
                WHERE p.permission_code = ANY(@PermissionCodes)
                """;

            await conn.ExecuteAsync(insertSql, new { RoleId = roleId, PermissionCodes = permissionCodes.ToArray() }, tx);
        }

        await tx.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<PermissionLookupDto>> ListPermissionsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS Id, permission_code AS PermissionCode, permission_name AS PermissionName, module_name AS ModuleName
            FROM permissions
            ORDER BY module_name, permission_code
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<PermissionLookupDto>(sql);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<EmployeeLookupDto>> ListEmployeesAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                e.id AS Id,
                e.employee_code AS EmployeeCode,
                e.full_name AS FullName,
                (u.id IS NOT NULL) AS HasUserAccount
            FROM employees e
            LEFT JOIN users u ON u.employee_id = e.id AND u.deleted_at IS NULL
            WHERE e.tenant_id = @TenantId AND e.deleted_at IS NULL
            ORDER BY e.full_name
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<EmployeeLookupDto>(sql, new { TenantId });
        return rows.ToList();
    }

    public async Task<bool> RoleIdsBelongToTenantAsync(IReadOnlyList<Guid> roleIds, CancellationToken cancellationToken)
    {
        if (roleIds.Count == 0) return true;

        const string sql = """
            SELECT COUNT(*)::int FROM roles
            WHERE tenant_id = @TenantId AND id = ANY(@RoleIds)
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var count = await conn.ExecuteScalarAsync<int>(sql, new { TenantId, RoleIds = roleIds.ToArray() });
        return count == roleIds.Count;
    }

    private static async Task ReplaceUserRolesAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        Guid userId,
        IReadOnlyList<Guid> roleIds,
        CancellationToken cancellationToken)
    {
        await conn.ExecuteAsync("DELETE FROM user_roles WHERE user_id = @UserId", new { UserId = userId }, tx);
        if (roleIds.Count == 0) return;

        const string insertSql = """
            INSERT INTO user_roles (user_id, role_id)
            SELECT @UserId, r.id
            FROM unnest(@RoleIds::uuid[]) AS rid
            INNER JOIN roles r ON r.id = rid
            """;

        await conn.ExecuteAsync(insertSql, new { UserId = userId, RoleIds = roleIds.ToArray() }, tx);
    }

    private static BranchAdminListItemDto MapBranchList(BranchRow row) =>
        new(row.Id, row.BranchCode, row.BranchName, row.Address, row.Phone, row.IsHeadOffice, row.Status, ToOffset(row.CreatedAt));

    private static BranchDetailDto MapBranchDetail(BranchRow row) =>
        new(row.Id, row.BranchCode, row.BranchName, row.Address, row.Phone, row.IsHeadOffice, row.Status, ToOffset(row.CreatedAt));

    private static UserAdminListItemDto MapUserList(UserListRow row) =>
        new(
            row.Id,
            row.Username,
            row.Email,
            row.Status,
            row.EmployeeName,
            row.RoleCodes ?? Array.Empty<string>(),
            row.LastLoginAt.HasValue ? ToOffset(row.LastLoginAt.Value) : null,
            ToOffset(row.CreatedAt));

    private static UserDetailDto MapUserDetail(UserDetailRow row) =>
        new(
            row.Id,
            row.Username,
            row.Email,
            row.Status,
            row.EmployeeId,
            row.EmployeeName,
            row.RoleIds ?? Array.Empty<Guid>(),
            row.RoleCodes ?? Array.Empty<string>(),
            row.LastLoginAt.HasValue ? ToOffset(row.LastLoginAt.Value) : null,
            ToOffset(row.CreatedAt));

    private static DateTimeOffset ToOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed class BranchRow
    {
        public Guid Id { get; init; }
        public string BranchCode { get; init; } = "";
        public string BranchName { get; init; } = "";
        public string? Address { get; init; }
        public string? Phone { get; init; }
        public bool IsHeadOffice { get; init; }
        public short Status { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    private sealed class UserListRow
    {
        public Guid Id { get; init; }
        public string Username { get; init; } = "";
        public string Email { get; init; } = "";
        public short Status { get; init; }
        public string? EmployeeName { get; init; }
        public string[]? RoleCodes { get; init; }
        public DateTime? LastLoginAt { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    private sealed class UserDetailRow
    {
        public Guid Id { get; init; }
        public string Username { get; init; } = "";
        public string Email { get; init; } = "";
        public short Status { get; init; }
        public Guid? EmployeeId { get; init; }
        public string? EmployeeName { get; init; }
        public Guid[]? RoleIds { get; init; }
        public string[]? RoleCodes { get; init; }
        public DateTime? LastLoginAt { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    private sealed class RoleListRow
    {
        public Guid Id { get; init; }
        public string RoleCode { get; init; } = "";
        public string RoleName { get; init; } = "";
        public string? Description { get; init; }
        public short Status { get; init; }
        public int UserCount { get; init; }
        public int PermissionCount { get; init; }
    }

    private sealed class RoleDetailRow
    {
        public Guid Id { get; init; }
        public string RoleCode { get; init; } = "";
        public string RoleName { get; init; } = "";
        public string? Description { get; init; }
        public short Status { get; init; }
        public string[]? PermissionCodes { get; init; }
    }
}
