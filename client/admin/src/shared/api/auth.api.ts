import { http } from '@/shared/api/http';
import type {
  AuthUser,
  AuthWorkspace,
  LoginRequest,
  LoginResponse,
  LoginWorkspaceChoice,
} from '@/shared/api/types';

export type LoginApiResult =
  | { kind: 'session'; session: LoginResponse }
  | { kind: 'choice'; choice: LoginWorkspaceChoice };

function isWorkspaceChoice(data: unknown): data is LoginWorkspaceChoice {
  if (!data || typeof data !== 'object') return false;
  const row = data as Record<string, unknown>;
  return row.requiresWorkspaceChoice === true && typeof row.selectionToken === 'string';
}

export async function loginApi(body: LoginRequest): Promise<LoginApiResult> {
  const { data } = await http.post<LoginResponse | LoginWorkspaceChoice>('/auth/login', body);
  if (isWorkspaceChoice(data)) {
    return { kind: 'choice', choice: data };
  }
  const session = data as LoginResponse;
  if (!session?.accessToken) {
    throw new Error('Đăng nhập không trả về token');
  }
  return { kind: 'session', session };
}

export async function selectWorkspaceApi(input: {
  selectionToken: string;
  userId: string;
}): Promise<LoginResponse> {
  const { data } = await http.post<LoginResponse>('/auth/select-workspace', {
    selectionToken: input.selectionToken,
    userId: input.userId,
  });
  return data;
}

export async function listWorkspacesApi(): Promise<AuthWorkspace[]> {
  const { data } = await http.get<AuthWorkspace[]>('/auth/workspaces');
  return data ?? [];
}

export async function logoutApi(refreshToken: string): Promise<void> {
  await http.post('/auth/logout', { refreshToken });
}

export async function meApi(): Promise<AuthUser> {
  const { data } = await http.get<AuthUser>('/auth/me');
  return data;
}
