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
