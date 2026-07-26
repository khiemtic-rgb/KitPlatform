export interface AuthUser {
  id: string;
  tenantId: string;
  tenantCode: string;
  username: string;
  email: string;
  roles: string[];
  permissions?: string[];
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  user: AuthUser;
}

export interface LoginRequest {
  username: string;
  password: string;
  tenantCode?: string;
}

export interface AuthWorkspace {
  userId: string;
  tenantId: string;
  tenantCode: string;
  tenantName: string;
  productCode: string;
  username: string;
  isDefault: boolean;
}

export interface LoginWorkspaceChoice {
  requiresWorkspaceChoice: true;
  selectionToken: string;
  workspaces: AuthWorkspace[];
}
