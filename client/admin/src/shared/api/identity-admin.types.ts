export type BranchListItem = {
  id: string;
  branchCode: string;
  branchName: string;
  address?: string;
  phone?: string;
  retailFacilityCode?: string;
  isHeadOffice: boolean;
  status: number;
  createdAt: string;
};

export type BranchDetail = BranchListItem;

export type CreateBranchPayload = {
  branchCode: string;
  branchName: string;
  address?: string;
  phone?: string;
  retailFacilityCode?: string;
  isHeadOffice?: boolean;
  status?: number;
};

export type UpdateBranchPayload = {
  branchCode: string;
  branchName: string;
  address?: string;
  phone?: string;
  retailFacilityCode?: string;
  isHeadOffice: boolean;
  status: number;
};

export type UserListItem = {
  id: string;
  username: string;
  email: string;
  status: number;
  employeeName?: string;
  employeePhone?: string;
  roleCodes: string[];
  lastLoginAt?: string;
  createdAt: string;
};

export type UserDetail = {
  id: string;
  username: string;
  email: string;
  status: number;
  employeeId?: string;
  employeeName?: string;
  employeePhone?: string;
  roleIds: string[];
  roleCodes: string[];
  lastLoginAt?: string;
  createdAt: string;
};

export type CreateUserPayload = {
  username: string;
  email: string;
  password: string;
  status: number;
  roleIds: string[];
  employeeId?: string;
  employeeFullName?: string;
  employeePhone?: string;
  branchIds?: string[];
  primaryBranchId?: string;
};

export type UpdateUserPayload = {
  username: string;
  email: string;
  status: number;
  roleIds: string[];
  employeeId?: string;
  employeeFullName?: string;
  employeePhone?: string;
  newPassword?: string;
  branchIds?: string[];
  primaryBranchId?: string;
};

export type RoleListItem = {
  id: string;
  roleCode: string;
  roleName: string;
  description?: string;
  status: number;
  userCount: number;
  permissionCount: number;
};

export type RoleDetail = {
  id: string;
  roleCode: string;
  roleName: string;
  description?: string;
  status: number;
  permissionCodes: string[];
};

export type CreateRolePayload = {
  roleCode: string;
  roleName: string;
  description?: string;
  status?: number;
};

export type UpdateRolePayload = {
  roleCode: string;
  roleName: string;
  description?: string;
  status: number;
};

export type PermissionLookup = {
  id: string;
  permissionCode: string;
  permissionName: string;
  moduleName: string;
};

export type EmployeeLookup = {
  id: string;
  employeeCode: string;
  fullName: string;
  phone?: string;
  hasUserAccount: boolean;
  branchCount?: number;
};

export type EmployeeBranchAssignment = {
  branchId: string;
  branchCode: string;
  branchName: string;
  isPrimary: boolean;
};

export type EmployeeDetail = {
  id: string;
  employeeCode: string;
  fullName: string;
  phone?: string;
  hasUserAccount: boolean;
  branches: EmployeeBranchAssignment[];
};

export type PagedUsersResult = {
  items: UserListItem[];
  total: number;
  page: number;
  pageSize: number;
};

export const USER_STATUS_LABELS: Record<number, string> = {
  1: 'Hoạt động',
  0: 'Ngưng',
};

export const BRANCH_STATUS_LABELS: Record<number, string> = {
  1: 'Hoạt động',
  0: 'Ngưng',
};

export const USER_STATUS_OPTIONS = [
  { value: 1, label: 'Hoạt động' },
  { value: 0, label: 'Ngưng' },
];

export const BRANCH_STATUS_OPTIONS = [
  { value: 1, label: 'Hoạt động' },
  { value: 0, label: 'Ngưng' },
];
