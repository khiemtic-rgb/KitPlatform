import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios';
import { useAuthStore } from '@/shared/auth/auth.store';
import { clearCustomerCachedData } from '@/shared/api/customer-session-cleanup';
import type { CustomerLoginResponse } from '@/shared/api/customer-app.types';
import { apiPath } from '@/shared/api/api-base';
import { forceCustomerLogout } from '@/shared/auth/force-logout';

export const http = axios.create({
  baseURL: apiPath('/api/customer-app'),
  headers: { 'Content-Type': 'application/json' },
  timeout: 8_000,
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

http.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const original = error.config as InternalAxiosRequestConfig & { _retry?: boolean };
    const status = error.response?.status;

    if (status === 401 && original && !original.url?.includes('/auth/')) {
      const { accessToken, refreshToken } = useAuthStore.getState();
      // Guest / anonymous browse: never bounce to login on protected-API 401.
      if (!accessToken && !refreshToken) {
        return Promise.reject(error);
      }

      if (original._retry) {
        forceCustomerLogout();
        return Promise.reject(error);
      }

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

      forceCustomerLogout();
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
    const { data } = await axios.post<CustomerLoginResponse>(
      apiPath('/api/customer-app/auth/refresh'),
      { refreshToken },
      { timeout: 10_000 },
    );
    setSession(data);
    return data.accessToken;
  } catch (error) {
    if (axios.isAxiosError(error) && error.response?.status === 401) {
      clearCustomerCachedData();
      clearSession();
    }
    return null;
  }
}
