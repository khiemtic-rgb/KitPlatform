namespace PharmaCore.Application.Identity;

public interface IIdentityAdminService
{
    Task<IReadOnlyList<BranchAdminListItemDto>> ListBranchesAsync(CancellationToken cancellationToken = default);
    Task<BranchDetailDto?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default);
    Task<BranchDetailDto> CreateBranchAsync(CreateBranchRequest request, CancellationToken cancellationToken = default);
    Task<BranchDetailDto?> UpdateBranchAsync(Guid branchId, UpdateBranchRequest request, CancellationToken cancellationToken = default);

    Task<PagedUsersResult> ListUsersAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<UserDetailDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserDetailDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<UserDetailDto?> UpdateUserAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleAdminListItemDto>> ListRolesAsync(CancellationToken cancellationToken = default);
    Task<RoleDetailDto?> GetRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<RoleDetailDto?> UpdateRolePermissionsAsync(
        Guid roleId,
        UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PermissionLookupDto>> ListPermissionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmployeeLookupDto>> ListEmployeesAsync(CancellationToken cancellationToken = default);
}
