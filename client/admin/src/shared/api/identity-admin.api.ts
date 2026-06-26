import { http } from '@/shared/api/http';
import type {
  BranchDetail,
  BranchListItem,
  CreateBranchPayload,
  CreateUserPayload,
  EmployeeLookup,
  PagedUsersResult,
  PermissionLookup,
  RoleDetail,
  RoleListItem,
  UpdateBranchPayload,
  UpdateUserPayload,
  UserDetail,
  UserListItem,
} from '@/shared/api/identity-admin.types';

function normalizeBranch(row: Record<string, unknown>): BranchListItem {
  return {
    id: String(row.id ?? row.Id),
    branchCode: String(row.branchCode ?? row.BranchCode ?? ''),
    branchName: String(row.branchName ?? row.BranchName ?? ''),
    address: (row.address ?? row.Address) as string | undefined,
    phone: (row.phone ?? row.Phone) as string | undefined,
    isHeadOffice: Boolean(row.isHeadOffice ?? row.IsHeadOffice),
    status: Number(row.status ?? row.Status ?? 1),
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
  };
}

function normalizeUserListItem(row: Record<string, unknown>): UserListItem {
  const roleCodes = (row.roleCodes ?? row.RoleCodes ?? []) as string[];
  return {
    id: String(row.id ?? row.Id),
    username: String(row.username ?? row.Username ?? ''),
    email: String(row.email ?? row.Email ?? ''),
    status: Number(row.status ?? row.Status ?? 1),
    employeeName: (row.employeeName ?? row.EmployeeName) as string | undefined,
    roleCodes: Array.isArray(roleCodes) ? roleCodes : [],
    lastLoginAt: (row.lastLoginAt ?? row.LastLoginAt) as string | undefined,
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
  };
}

function normalizeUserDetail(row: Record<string, unknown>): UserDetail {
  const roleIds = (row.roleIds ?? row.RoleIds ?? []) as string[];
  const roleCodes = (row.roleCodes ?? row.RoleCodes ?? []) as string[];
  return {
    id: String(row.id ?? row.Id),
    username: String(row.username ?? row.Username ?? ''),
    email: String(row.email ?? row.Email ?? ''),
    status: Number(row.status ?? row.Status ?? 1),
    employeeId: (row.employeeId ?? row.EmployeeId) as string | undefined,
    employeeName: (row.employeeName ?? row.EmployeeName) as string | undefined,
    roleIds: Array.isArray(roleIds) ? roleIds.map(String) : [],
    roleCodes: Array.isArray(roleCodes) ? roleCodes : [],
    lastLoginAt: (row.lastLoginAt ?? row.LastLoginAt) as string | undefined,
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
  };
}

function normalizeRoleListItem(row: Record<string, unknown>): RoleListItem {
  return {
    id: String(row.id ?? row.Id),
    roleCode: String(row.roleCode ?? row.RoleCode ?? ''),
    roleName: String(row.roleName ?? row.RoleName ?? ''),
    description: (row.description ?? row.Description) as string | undefined,
    status: Number(row.status ?? row.Status ?? 1),
    userCount: Number(row.userCount ?? row.UserCount ?? 0),
    permissionCount: Number(row.permissionCount ?? row.PermissionCount ?? 0),
  };
}

function normalizeRoleDetail(row: Record<string, unknown>): RoleDetail {
  const permissionCodes = (row.permissionCodes ?? row.PermissionCodes ?? []) as string[];
  return {
    id: String(row.id ?? row.Id),
    roleCode: String(row.roleCode ?? row.RoleCode ?? ''),
    roleName: String(row.roleName ?? row.RoleName ?? ''),
    description: (row.description ?? row.Description) as string | undefined,
    status: Number(row.status ?? row.Status ?? 1),
    permissionCodes: Array.isArray(permissionCodes) ? permissionCodes : [],
  };
}

export async function fetchBranches(): Promise<BranchListItem[]> {
  const { data } = await http.get<Record<string, unknown>[]>('/system/branches');
  return (data ?? []).map((row) => normalizeBranch(row));
}

export async function fetchBranch(branchId: string): Promise<BranchDetail> {
  const { data } = await http.get<Record<string, unknown>>(`/system/branches/${branchId}`);
  return normalizeBranch(data);
}

export async function createBranch(payload: CreateBranchPayload): Promise<BranchDetail> {
  const { data } = await http.post<Record<string, unknown>>('/system/branches', payload);
  return normalizeBranch(data);
}

export async function updateBranch(branchId: string, payload: UpdateBranchPayload): Promise<BranchDetail> {
  const { data } = await http.put<Record<string, unknown>>(`/system/branches/${branchId}`, payload);
  return normalizeBranch(data);
}

export async function fetchUsers(params: {
  search?: string;
  page?: number;
  pageSize?: number;
}): Promise<PagedUsersResult> {
  const { data } = await http.get<Record<string, unknown>>('/system/users', { params });
  const items = ((data.items ?? data.Items ?? []) as Record<string, unknown>[]).map(normalizeUserListItem);
  return {
    items,
    total: Number(data.total ?? data.Total ?? 0),
    page: Number(data.page ?? data.Page ?? 1),
    pageSize: Number(data.pageSize ?? data.PageSize ?? 20),
  };
}

export async function fetchUser(userId: string): Promise<UserDetail> {
  const { data } = await http.get<Record<string, unknown>>(`/system/users/${userId}`);
  return normalizeUserDetail(data);
}

export async function createUser(payload: CreateUserPayload): Promise<UserDetail> {
  const { data } = await http.post<Record<string, unknown>>('/system/users', payload);
  return normalizeUserDetail(data);
}

export async function updateUser(userId: string, payload: UpdateUserPayload): Promise<UserDetail> {
  const { data } = await http.put<Record<string, unknown>>(`/system/users/${userId}`, payload);
  return normalizeUserDetail(data);
}

export async function fetchRoles(): Promise<RoleListItem[]> {
  const { data } = await http.get<Record<string, unknown>[]>('/system/roles');
  return (data ?? []).map(normalizeRoleListItem);
}

export async function fetchRole(roleId: string): Promise<RoleDetail> {
  const { data } = await http.get<Record<string, unknown>>(`/system/roles/${roleId}`);
  return normalizeRoleDetail(data);
}

export async function updateRolePermissions(
  roleId: string,
  permissionCodes: string[],
): Promise<RoleDetail> {
  const { data } = await http.put<Record<string, unknown>>(`/system/roles/${roleId}/permissions`, {
    permissionCodes,
  });
  return normalizeRoleDetail(data);
}

export async function fetchPermissions(): Promise<PermissionLookup[]> {
  const { data } = await http.get<Record<string, unknown>[]>('/system/permissions');
  return (data ?? []).map((row) => ({
    id: String(row.id ?? row.Id),
    permissionCode: String(row.permissionCode ?? row.PermissionCode ?? ''),
    permissionName: String(row.permissionName ?? row.PermissionName ?? ''),
    moduleName: String(row.moduleName ?? row.ModuleName ?? ''),
  }));
}

export async function fetchEmployees(): Promise<EmployeeLookup[]> {
  const { data } = await http.get<Record<string, unknown>[]>('/system/employees');
  return (data ?? []).map((row) => ({
    id: String(row.id ?? row.Id),
    employeeCode: String(row.employeeCode ?? row.EmployeeCode ?? ''),
    fullName: String(row.fullName ?? row.FullName ?? ''),
    hasUserAccount: Boolean(row.hasUserAccount ?? row.HasUserAccount),
  }));
}
