namespace PharmaCore.Application.Identity;

public sealed record BranchAdminListItemDto(
    Guid Id,
    string BranchCode,
    string BranchName,
    string? Address,
    string? Phone,
    bool IsHeadOffice,
    short Status,
    DateTimeOffset CreatedAt);

public sealed record BranchDetailDto(
    Guid Id,
    string BranchCode,
    string BranchName,
    string? Address,
    string? Phone,
    bool IsHeadOffice,
    short Status,
    DateTimeOffset CreatedAt);

public sealed record CreateBranchRequest(
    string BranchCode,
    string BranchName,
    string? Address,
    string? Phone,
    bool IsHeadOffice = false,
    short Status = 1);

public sealed record UpdateBranchRequest(
    string BranchCode,
    string BranchName,
    string? Address,
    string? Phone,
    bool IsHeadOffice,
    short Status);

public sealed record UserAdminListItemDto(
    Guid Id,
    string Username,
    string Email,
    short Status,
    string? EmployeeName,
    IReadOnlyList<string> RoleCodes,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt);

public sealed record UserDetailDto(
    Guid Id,
    string Username,
    string Email,
    short Status,
    Guid? EmployeeId,
    string? EmployeeName,
    IReadOnlyList<Guid> RoleIds,
    IReadOnlyList<string> RoleCodes,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt);

public sealed record CreateUserRequest(
    string Username,
    string Email,
    string Password,
    short Status,
    IReadOnlyList<Guid> RoleIds,
    Guid? EmployeeId = null);

public sealed record UpdateUserRequest(
    string Email,
    short Status,
    IReadOnlyList<Guid> RoleIds,
    Guid? EmployeeId = null,
    string? NewPassword = null);

public sealed record RoleAdminListItemDto(
    Guid Id,
    string RoleCode,
    string RoleName,
    string? Description,
    short Status,
    int UserCount,
    int PermissionCount);

public sealed record RoleDetailDto(
    Guid Id,
    string RoleCode,
    string RoleName,
    string? Description,
    short Status,
    IReadOnlyList<string> PermissionCodes);

public sealed record UpdateRolePermissionsRequest(IReadOnlyList<string> PermissionCodes);

public sealed record PermissionLookupDto(
    Guid Id,
    string PermissionCode,
    string PermissionName,
    string ModuleName);

public sealed record EmployeeLookupDto(
    Guid Id,
    string EmployeeCode,
    string FullName,
    bool HasUserAccount);

public sealed record PagedUsersResult(
    IReadOnlyList<UserAdminListItemDto> Items,
    int Total,
    int Page,
    int PageSize);
