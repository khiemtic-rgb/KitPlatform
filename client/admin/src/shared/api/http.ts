import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios';
import { useAuthStore } from '@/shared/auth/auth.store';
import type { LoginResponse } from '@/shared/api/types';
import { apiPath } from '@/shared/api/api-base';

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
  return config;
});

let refreshPromise: Promise<string | null> | null = null;

http.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
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
        window.location.href = '/login';
      }
      return Promise.reject(error);
    }

    return Promise.reject(error);
  },
);

async function refreshAccessToken(): Promise<string | null> {
  const { refreshToken, setSession, clearSession } = useAuthStore.getState();
  if (!refreshToken) {
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
