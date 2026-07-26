using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Auth;
using KitPlatform.Application.Configuration;
using KitPlatform.Application.Identity;

namespace KitPlatform.Infrastructure.Identity;

internal sealed class IdentityAdminService : IIdentityAdminService
{
    private const short ActiveStatus = 1;
    private const int MinPasswordLength = 8;

    private readonly IdentityAdminRepository _repository;
    private readonly ITenantPlatformSettings _platformSettings;
    private readonly IKitAccountService _kitAccounts;
    private readonly ITenantContext _tenant;

    public IdentityAdminService(
        IdentityAdminRepository repository,
        ITenantPlatformSettings platformSettings,
        IKitAccountService kitAccounts,
        ITenantContext tenant)
    {
        _repository = repository;
        _platformSettings = platformSettings;
        _kitAccounts = kitAccounts;
        _tenant = tenant;
    }

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

        var platform = await _platformSettings.GetAsync(cancellationToken);
        var activeCount = await _repository.CountActiveBranchesAsync(cancellationToken);
        TenantPlatformSettingsValidator.EnsureWithinBranchQuota(activeCount + 1, platform.MaxBranches);

        var id = await _repository.CreateBranchAsync(
            request with { BranchCode = code, BranchName = request.BranchName.Trim() },
            cancellationToken);

        if (request.IsHeadOffice)
            await _repository.ClearHeadOfficeExceptAsync(id, cancellationToken);

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

        if (updated && request.IsHeadOffice)
            await _repository.ClearHeadOfficeExceptAsync(branchId, cancellationToken);

        return updated ? await _repository.GetBranchAsync(branchId, cancellationToken) : null;
    }

    public async Task<bool> DeleteBranchAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        var branch = await _repository.GetBranchAsync(branchId, cancellationToken);
        if (branch is null)
            return false;

        if (branch.IsHeadOffice)
            throw new InvalidOperationException("Không thể xóa chi nhánh trụ sở chính.");

        if (await _repository.BranchHasActiveWarehousesAsync(branchId, cancellationToken))
            throw new InvalidOperationException("Chi nhánh đang có kho — vô hiệu hóa hoặc chuyển kho trước khi xóa.");

        return await _repository.SoftDeleteBranchAsync(branchId, cancellationToken);
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

        var employeeId = await ResolveEmployeeIdAsync(
            request.EmployeeId,
            request.EmployeeFullName,
            request.EmployeePhone,
            existingEmployeeId: null,
            cancellationToken);

        await _kitAccounts.AssertEmailPasswordCompatibleAsync(email, request.Password, cancellationToken);

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var userId = await _repository.CreateUserAsync(
            username,
            email,
            passwordHash,
            request.Status,
            employeeId,
            request.RoleIds,
            cancellationToken);

        await _kitAccounts.EnsureAccountForUserAsync(
            userId,
            _tenant.TenantId,
            email,
            request.Password,
            passwordHash,
            username,
            connection: null,
            transaction: null,
            cancellationToken);

        if (employeeId is not null)
            await AssignEmployeeBranchesAsync(employeeId.Value, request.BranchIds, request.PrimaryBranchId, cancellationToken);

        return (await _repository.GetUserAsync(userId, cancellationToken))!;
    }

    public async Task<UserDetailDto?> UpdateUserAsync(
        Guid userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await _repository.GetUserAsync(userId, cancellationToken) is null)
            return null;

        if (string.IsNullOrWhiteSpace(request.Username))
            throw new InvalidOperationException("Tên đăng nhập không được để trống.");

        if (string.IsNullOrWhiteSpace(request.Email))
            throw new InvalidOperationException("Email không được để trống.");

        if (request.RoleIds.Count == 0)
            throw new InvalidOperationException("Chọn ít nhất một vai trò.");

        var username = request.Username.Trim();
        if (await _repository.UsernameExistsAsync(username, userId, cancellationToken))
            throw new InvalidOperationException("Tên đăng nhập đã tồn tại.");

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

        var currentEmployeeId = await _repository.GetUserEmployeeIdAsync(userId, cancellationToken);
        var employeeId = await ResolveEmployeeIdAsync(
            request.EmployeeId,
            request.EmployeeFullName,
            request.EmployeePhone,
            currentEmployeeId,
            cancellationToken);

        var updated = await _repository.UpdateUserAsync(
            userId,
            username,
            email,
            request.Status,
            employeeId,
            passwordHash,
            request.RoleIds,
            cancellationToken);

        if (updated && passwordHash is not null && !string.IsNullOrWhiteSpace(request.NewPassword))
        {
            await _kitAccounts.SyncPasswordForUserAsync(
                userId,
                request.NewPassword,
                passwordHash,
                cancellationToken);
        }

        if (updated && employeeId is not null && request.BranchIds is not null)
            await AssignEmployeeBranchesAsync(employeeId.Value, request.BranchIds, request.PrimaryBranchId, cancellationToken);

        return updated ? await _repository.GetUserAsync(userId, cancellationToken) : null;
    }

    public async Task<bool> DeleteUserAsync(
        Guid userId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (userId == currentUserId)
            throw new InvalidOperationException("Không thể xóa tài khoản đang đăng nhập.");

        if (await _repository.GetUserAsync(userId, cancellationToken) is null)
            return false;

        return await _repository.SoftDeleteUserAsync(userId, cancellationToken);
    }

    public async Task<bool> DeleteEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        if (await _repository.EmployeeHasActiveUserAsync(employeeId, cancellationToken))
            throw new InvalidOperationException("Nhân viên đang có tài khoản — xóa hoặc vô hiệu hóa tài khoản trước.");

        return await _repository.SoftDeleteEmployeeAsync(employeeId, cancellationToken);
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

    public async Task<RoleDetailDto> CreateRoleAsync(
        CreateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRoleFields(request.RoleCode, request.RoleName);

        var code = request.RoleCode.Trim().ToUpperInvariant();
        if (await _repository.RoleCodeExistsAsync(code, excludeRoleId: null, cancellationToken))
            throw new InvalidOperationException($"Mã vai trò «{code}» đã tồn tại.");

        var id = await _repository.CreateRoleAsync(
            code,
            request.RoleName.Trim(),
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            request.Status,
            cancellationToken);

        return (await _repository.GetRoleAsync(id, cancellationToken))!;
    }

    public async Task<RoleDetailDto?> UpdateRoleAsync(
        Guid roleId,
        UpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRoleFields(request.RoleCode, request.RoleName);

        if (await _repository.GetRoleAsync(roleId, cancellationToken) is null)
            return null;

        var code = request.RoleCode.Trim().ToUpperInvariant();
        if (await _repository.RoleCodeExistsAsync(code, roleId, cancellationToken))
            throw new InvalidOperationException($"Mã vai trò «{code}» đã tồn tại.");

        var updated = await _repository.UpdateRoleAsync(
            roleId,
            code,
            request.RoleName.Trim(),
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            request.Status,
            cancellationToken);

        return updated ? await _repository.GetRoleAsync(roleId, cancellationToken) : null;
    }

    public Task<IReadOnlyList<PermissionLookupDto>> ListPermissionsAsync(CancellationToken cancellationToken = default) =>
        _repository.ListPermissionsAsync(cancellationToken);

    public Task<IReadOnlyList<EmployeeLookupDto>> ListEmployeesAsync(CancellationToken cancellationToken = default) =>
        _repository.ListEmployeesAsync(cancellationToken);

    public Task<string> GetNextEmployeeCodeAsync(CancellationToken cancellationToken = default) =>
        _repository.GenerateEmployeeCodeAsync(cancellationToken);

    public async Task<EmployeeLookupDto> CreateEmployeeAsync(
        CreateEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new InvalidOperationException("Họ tên nhân viên không được để trống.");

        var code = string.IsNullOrWhiteSpace(request.EmployeeCode)
            ? await _repository.GenerateEmployeeCodeAsync(cancellationToken)
            : request.EmployeeCode.Trim().ToUpperInvariant();

        if (await _repository.EmployeeCodeExistsAsync(code, cancellationToken))
            throw new InvalidOperationException($"Mã nhân viên «{code}» đã tồn tại.");

        var id = await _repository.CreateEmployeeAsync(
            code,
            request.FullName.Trim(),
            string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            cancellationToken);

        await AssignEmployeeBranchesAsync(id, request.BranchIds, request.PrimaryBranchId, cancellationToken);

        return (await _repository.GetEmployeeLookupAsync(id, cancellationToken))!;
    }

    public Task<EmployeeDetailDto?> GetEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default) =>
        _repository.GetEmployeeDetailAsync(employeeId, cancellationToken);

    public async Task<EmployeeDetailDto?> UpdateEmployeeBranchesAsync(
        Guid employeeId,
        UpdateEmployeeBranchesRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await _repository.GetEmployeeLookupAsync(employeeId, cancellationToken) is null)
            return null;

        await AssignEmployeeBranchesAsync(employeeId, request.BranchIds, request.PrimaryBranchId, cancellationToken);
        return await _repository.GetEmployeeDetailAsync(employeeId, cancellationToken);
    }

    private async Task AssignEmployeeBranchesAsync(
        Guid employeeId,
        IReadOnlyList<Guid>? branchIds,
        Guid? primaryBranchId,
        CancellationToken cancellationToken)
    {
        if (branchIds is null)
            return;

        var distinct = branchIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (distinct.Count == 0)
        {
            await _repository.ReplaceEmployeeBranchesAsync(employeeId, [], null, cancellationToken);
            return;
        }

        if (!await _repository.BranchIdsBelongToTenantAsync(distinct, cancellationToken))
            throw new InvalidOperationException("Một hoặc nhiều chi nhánh không hợp lệ.");

        if (primaryBranchId is Guid primary && !distinct.Contains(primary))
            throw new InvalidOperationException("Chi nhánh chính phải nằm trong danh sách được gán.");

        await _repository.ReplaceEmployeeBranchesAsync(employeeId, distinct, primaryBranchId, cancellationToken);
    }

    private async Task<Guid?> ResolveEmployeeIdAsync(
        Guid? employeeId,
        string? employeeFullName,
        string? employeePhone,
        Guid? existingEmployeeId,
        CancellationToken cancellationToken)
    {
        var fullName = employeeFullName?.Trim();
        var phone = string.IsNullOrWhiteSpace(employeePhone) ? null : employeePhone.Trim();
        var targetId = employeeId ?? existingEmployeeId;

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            if (targetId is not null)
            {
                await _repository.UpdateEmployeeContactAsync(targetId.Value, fullName, phone, cancellationToken);
                return targetId;
            }

            var code = await _repository.GenerateEmployeeCodeAsync(cancellationToken);
            return await _repository.CreateEmployeeAsync(code, fullName, phone, email: null, cancellationToken);
        }

        if (targetId is not null && phone is not null)
        {
            var lookup = await _repository.GetEmployeeLookupAsync(targetId.Value, cancellationToken);
            if (lookup is not null)
                await _repository.UpdateEmployeeContactAsync(targetId.Value, lookup.FullName, phone, cancellationToken);
        }

        return targetId;
    }

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

    private static void ValidateRoleFields(string roleCode, string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleCode))
            throw new InvalidOperationException("Mã vai trò không được để trống.");
        if (string.IsNullOrWhiteSpace(roleName))
            throw new InvalidOperationException("Tên vai trò không được để trống.");
    }
}
