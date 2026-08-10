import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios';
import { useSessionStore } from '@/shared/auth/session.store';

export const http = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
  timeout: 30_000,
});

http.interceptors.request.use((config) => {
  const token = useSessionStore.getState().accessToken;
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  const method = (config.method ?? 'get').toLowerCase();
  const url = config.url ?? '';
  const isMutate = method === 'post' || method === 'put' || method === 'patch' || method === 'delete';
  const allowWhileDemo =
    url.includes('/day-flows/ensure') ||
    url.includes('/auth/') ||
    url.includes('/overview') ||
    url.includes('/demo-house/ping');
  if (isMutate && !allowWhileDemo && !useSessionStore.getState().canWrite()) {
    return Promise.reject(
      Object.assign(new Error('Chế độ xem demo — nhà này chỉ xem, không sửa được.'), {
        code: 'demo_readonly',
        config,
        isAxiosError: true,
        response: {
          status: 403,
          data: {
            code: 'demo_readonly',
            message: 'Chế độ xem demo — nhà này chỉ xem, không sửa được.',
          },
        },
      }),
    );
  }

  return config;
});

type RefreshResponse = {
  accessToken?: string;
  AccessToken?: string;
  refreshToken?: string;
  RefreshToken?: string;
};

let refreshPromise: Promise<string | null> | null = null;

http.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const original = error.config as InternalAxiosRequestConfig & { _retry?: boolean };
    const status = error.response?.status;

    if (
      status === 401 &&
      original &&
      !original._retry &&
      !original.url?.includes('/auth/login') &&
      !original.url?.includes('/auth/refresh')
    ) {
      original._retry = true;

      if (!refreshPromise) {
        refreshPromise = refreshAccessToken().finally(() => {
          refreshPromise = null;
        });
      }

      const newToken = await refreshPromise;
      if (newToken) {
        original.headers.Authorization = `Bearer ${newToken}`;
        return http(original);
      }

      useSessionStore.getState().clear();
      if (!window.location.pathname.startsWith('/unlock')) {
        window.location.assign('/unlock');
      }
      return Promise.reject(error);
    }

    return Promise.reject(error);
  },
);

async function refreshAccessToken(): Promise<string | null> {
  const { refreshToken, setTokens, clear } = useSessionStore.getState();
  if (!refreshToken) return null;

  try {
    const { data } = await axios.post<RefreshResponse>(
      '/api/auth/refresh',
      { refreshToken },
      { timeout: 10_000 },
    );
    const access = String(data.accessToken ?? data.AccessToken ?? '');
    const nextRefresh = String(data.refreshToken ?? data.RefreshToken ?? refreshToken);
    if (!access) return null;
    setTokens({ accessToken: access, refreshToken: nextRefresh });
    return access;
  } catch (err) {
    if (axios.isAxiosError(err) && err.response?.status === 401) {
      clear();
    }
    return null;
  }
}
