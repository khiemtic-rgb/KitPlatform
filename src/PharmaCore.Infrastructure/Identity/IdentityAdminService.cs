using PharmaCore.Application.Identity;

namespace PharmaCore.Infrastructure.Identity;

internal sealed class IdentityAdminService : IIdentityAdminService
{
    private const short ActiveStatus = 1;
    private const int MinPasswordLength = 8;

    private readonly IdentityAdminRepository _repository;

    public IdentityAdminService(IdentityAdminRepository repository) => _repository = repository;

    public Task<IReadOnlyList<BranchAdminListItemDto>> ListBranchesAsync(CancellationToken cancellationToken = default) =>
        _repository.ListBranchesAsync(cancellationToken);

    public Task<BranchDetailDto?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) =>
        _repository.GetBranchAsync(branchId, cancellationToken);

    public async Task<BranchDetailDto> CreateBranchAsync(
        CreateBranchRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateBranch(request.BranchCode, request.BranchName);

        var code = request.BranchCode.Trim().ToUpperInvariant();
        if (await _repository.BranchCodeExistsAsync(code, excludeBranchId: null, cancellationToken))
            throw new InvalidOperationException($"Mã chi nhánh «{code}» đã tồn tại.");

        var id = await _repository.CreateBranchAsync(
            request with { BranchCode = code, BranchName = request.BranchName.Trim() },
            cancellationToken);

        return (await _repository.GetBranchAsync(id, cancellationToken))!;
    }

    public async Task<BranchDetailDto?> UpdateBranchAsync(
        Guid branchId,
        UpdateBranchRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateBranch(request.BranchCode, request.BranchName);

        if (await _repository.GetBranchAsync(branchId, cancellationToken) is null)
            return null;

        var code = request.BranchCode.Trim().ToUpperInvariant();
        if (await _repository.BranchCodeExistsAsync(code, branchId, cancellationToken))
            throw new InvalidOperationException($"Mã chi nhánh «{code}» đã tồn tại.");

        var updated = await _repository.UpdateBranchAsync(
            branchId,
            request with { BranchCode = code, BranchName = request.BranchName.Trim() },
            cancellationToken);

        return updated ? await _repository.GetBranchAsync(branchId, cancellationToken) : null;
    }

    public async Task<PagedUsersResult> ListUsersAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, total) = await _repository.ListUsersAsync(search, page, pageSize, cancellationToken);
        return new PagedUsersResult(items, total, page, pageSize);
    }

    public Task<UserDetailDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _repository.GetUserAsync(userId, cancellationToken);

    public async Task<UserDetailDto> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateUserFields(request.Username, request.Email, request.Password, request.RoleIds);

        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _repository.UsernameExistsAsync(username, excludeUserId: null, cancellationToken))
            throw new InvalidOperationException("Tên đăng nhập đã tồn tại.");

        if (await _repository.EmailExistsAsync(email, excludeUserId: null, cancellationToken))
            throw new InvalidOperationException("Email đã được dùng cho tài khoản khác.");

        if (!await _repository.RoleIdsBelongToTenantAsync(request.RoleIds, cancellationToken))
            throw new InvalidOperationException("Một hoặc nhiều vai trò không hợp lệ.");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var userId = await _repository.CreateUserAsync(
            username,
            email,
            passwordHash,
            request.Status,
            request.EmployeeId,
            request.RoleIds,
            cancellationToken);

        return (await _repository.GetUserAsync(userId, cancellationToken))!;
    }

    public async Task<UserDetailDto?> UpdateUserAsync(
        Guid userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await _repository.GetUserAsync(userId, cancellationToken) is null)
            return null;

        if (string.IsNullOrWhiteSpace(request.Email))
            throw new InvalidOperationException("Email không được để trống.");

        if (request.RoleIds.Count == 0)
            throw new InvalidOperationException("Chọn ít nhất một vai trò.");

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _repository.EmailExistsAsync(email, userId, cancellationToken))
            throw new InvalidOperationException("Email đã được dùng cho tài khoản khác.");

        if (!await _repository.RoleIdsBelongToTenantAsync(request.RoleIds, cancellationToken))
            throw new InvalidOperationException("Một hoặc nhiều vai trò không hợp lệ.");

        string? passwordHash = null;
        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            if (request.NewPassword.Length < MinPasswordLength)
                throw new InvalidOperationException($"Mật khẩu mới tối thiểu {MinPasswordLength} ký tự.");
            passwordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        }

        var updated = await _repository.UpdateUserAsync(
            userId,
            email,
            request.Status,
            request.EmployeeId,
            passwordHash,
            request.RoleIds,
            cancellationToken);

        return updated ? await _repository.GetUserAsync(userId, cancellationToken) : null;
    }

    public Task<IReadOnlyList<RoleAdminListItemDto>> ListRolesAsync(CancellationToken cancellationToken = default) =>
        _repository.ListRolesAsync(cancellationToken);

    public Task<RoleDetailDto?> GetRoleAsync(Guid roleId, CancellationToken cancellationToken = default) =>
        _repository.GetRoleAsync(roleId, cancellationToken);

    public async Task<RoleDetailDto?> UpdateRolePermissionsAsync(
        Guid roleId,
        UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await _repository.GetRoleAsync(roleId, cancellationToken) is null)
            return null;

        var codes = request.PermissionCodes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var updated = await _repository.UpdateRolePermissionsAsync(roleId, codes, cancellationToken);
        return updated ? await _repository.GetRoleAsync(roleId, cancellationToken) : null;
    }

    public Task<IReadOnlyList<PermissionLookupDto>> ListPermissionsAsync(CancellationToken cancellationToken = default) =>
        _repository.ListPermissionsAsync(cancellationToken);

    public Task<IReadOnlyList<EmployeeLookupDto>> ListEmployeesAsync(CancellationToken cancellationToken = default) =>
        _repository.ListEmployeesAsync(cancellationToken);

    private static void ValidateBranch(string branchCode, string branchName)
    {
        if (string.IsNullOrWhiteSpace(branchCode))
            throw new InvalidOperationException("Mã chi nhánh không được để trống.");
        if (string.IsNullOrWhiteSpace(branchName))
            throw new InvalidOperationException("Tên chi nhánh không được để trống.");
    }

    private static void ValidateUserFields(string username, string email, string password, IReadOnlyList<Guid> roleIds)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException("Tên đăng nhập không được để trống.");
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("Email không được để trống.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinPasswordLength)
            throw new InvalidOperationException($"Mật khẩu tối thiểu {MinPasswordLength} ký tự.");
        if (roleIds.Count == 0)
            throw new InvalidOperationException("Chọn ít nhất một vai trò.");
    }
}
