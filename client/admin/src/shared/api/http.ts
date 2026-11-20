import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios';
import { useAuthStore } from '@/shared/auth/auth.store';
import type { LoginResponse } from '@/shared/api/types';
import { apiPath } from '@/shared/api/api-base';
import { isGenericAxiosStatusMessage } from '@/shared/api/api-error';

export const http = axios.create({
  baseURL: apiPath('/api'),
  headers: { 'Content-Type': 'application/json' },
  timeout: 30_000,
  paramsSerializer: {
    indexes: null,
  },
});

http.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = useAuthStore.getState().accessToken;
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  if (config.data instanceof FormData) {
    delete config.headers['Content-Type'];
  }
  return config;
});

let refreshPromise: Promise<string | null> | null = null;

async function clarifyAxiosError(error: AxiosError) {
  const status = error.response?.status;
  if (!status || !isGenericAxiosStatusMessage(error.message)) return;
  const path = String(error.config?.url || '').split('?')[0];
  const data = error.response?.data;
  let detail = '';
  if (typeof Blob !== 'undefined' && data instanceof Blob) {
    try {
      const text = (await data.text()).trim();
      if (text.startsWith('{')) {
        const parsed = JSON.parse(text) as { message?: string; title?: string; detail?: string };
        detail = String(parsed.message || parsed.detail || parsed.title || '').trim();
      } else if (text) {
        detail = text.slice(0, 180);
      }
    } catch {
      /* keep generic */
    }
  } else if (data && typeof data === 'object') {
    const row = data as {
      message?: string;
      title?: string;
      detail?: string;
      errors?: Record<string, string[] | string>;
    };
    const fieldLines = row.errors
      ? Object.entries(row.errors).flatMap(([field, msgs]) => {
          const list = Array.isArray(msgs) ? msgs : [String(msgs)];
          return list.filter(Boolean).map((msg) => `${field}: ${msg}`);
        })
      : [];
    detail = (fieldLines.slice(0, 2).join(' · ') || row.message || row.detail || row.title || '').trim();
  } else if (typeof data === 'string' && data.trim()) {
    detail = data.trim().slice(0, 180);
  }
  error.message = detail && !/^(Forbidden|Unauthorized|Access Denied)$/i.test(detail)
    ? detail
    : `HTTP ${status}${path ? ` · ${path}` : ''}`;
}

http.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    await clarifyAxiosError(error);
    const original = error.config as InternalAxiosRequestConfig & { _retry?: boolean };
    const status = error.response?.status;

    // Chi refresh token khi 401 ro rang — khong logout khi API mat ket noi (khong co response).
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

      if (!useAuthStore.getState().accessToken) {
        window.location.assign('/login');
      }
      return Promise.reject(error);
    }

    return Promise.reject(error);
  },
);

async function refreshAccessToken(): Promise<string | null> {
  const { refreshToken, setSession, clearSession } = useAuthStore.getState();
  if (!refreshToken) {
    clearSession();
    return null;
  }

  try {
    const { data } = await axios.post<LoginResponse>(
      apiPath('/api/auth/refresh'),
      { refreshToken },
      { timeout: 10_000 },
    );
    setSession(data);
    return data.accessToken;
  } catch (error) {
    if (axios.isAxiosError(error) && error.response?.status === 401) {
      clearSession();
    }
    return null;
  }
}
