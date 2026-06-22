import { http } from '@/shared/api/http';
import type { AuthUser, LoginRequest, LoginResponse } from '@/shared/api/types';

export async function loginApi(body: LoginRequest): Promise<LoginResponse> {
  const { data } = await http.post<LoginResponse>('/auth/login', body);
  return data;
}

export async function logoutApi(refreshToken: string): Promise<void> {
  await http.post('/auth/logout', { refreshToken });
}

export async function meApi(): Promise<AuthUser> {
  const { data } = await http.get<AuthUser>('/auth/me');
  return data;
}
