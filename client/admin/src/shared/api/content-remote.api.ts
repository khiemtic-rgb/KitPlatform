import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios';
import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { apiPath } from '@/shared/api/api-base';
import { useAuthStore } from '@/shared/auth/auth.store';
import type { AuthUser, AuthWorkspace, LoginResponse, LoginWorkspaceChoice } from '@/shared/api/types';
import type { ContentBrand, ContentChannelTarget } from '@/shared/api/content.api';
import { publishExtrasForm, type ImportVariantItem, type PublishExtras, type PublishExtrasInput, type YoutubeVideo } from '@/shared/api/content-youtube.api';
import type { AgentScheduleTopicsResult, TopicPublishPlanInput } from '@/shared/api/content-agent.api';

/**
 * "Đồng bộ lên máy chủ": Studio trên máy dựng phim gửi thành phẩm lên máy chủ đăng bài.
 * Trình duyệt gọi API local (/api/content-remote/...), API local chuyển tiếp sang máy chủ
 * (Content:RemotePublishBaseUrl). Token máy chủ chỉ nằm ở trình duyệt này; không lưu mật khẩu.
 */

type RemoteSession = {
  baseUrl: string | null;
  accessToken: string | null;
  refreshToken: string | null;
  user: AuthUser | null;
  setSession: (baseUrl: string, payload: LoginResponse) => void;
  clear: () => void;
};

export const useRemoteSession = create<RemoteSession>()(
  persist(
    (set) => ({
      baseUrl: null,
      accessToken: null,
      refreshToken: null,
      user: null,
      setSession: (baseUrl, payload) =>
        set({ baseUrl, accessToken: payload.accessToken, refreshToken: payload.refreshToken, user: payload.user }),
      clear: () => set({ accessToken: null, refreshToken: null, user: null }),
    }),
    { name: 'kitplatform-remote-publish' },
  ),
);

const relay = axios.create({ baseURL: apiPath('/api/content-remote'), timeout: 30_000 });

relay.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const local = useAuthStore.getState().accessToken;
  if (local) config.headers.Authorization = `Bearer ${local}`;
  const remote = useRemoteSession.getState().accessToken;
  if (remote) config.headers['X-Remote-Authorization'] = `Bearer ${remote}`;
  if (config.data instanceof FormData) delete config.headers['Content-Type'];
  return config;
});

let refreshing: Promise<boolean> | null = null;

async function refreshRemote(): Promise<boolean> {
  const { refreshToken, baseUrl, setSession, clear } = useRemoteSession.getState();
  if (!refreshToken || !baseUrl) return false;
  try {
    const { data } = await axios.post<LoginResponse>(
      apiPath('/api/content-remote/auth/refresh'),
      { refreshToken },
      { headers: { Authorization: `Bearer ${useAuthStore.getState().accessToken ?? ''}` } },
    );
    setSession(baseUrl, data);
    return true;
  } catch {
    clear();
    return false;
  }
}

relay.interceptors.response.use(undefined, async (error: AxiosError) => {
  const config = error.config as (InternalAxiosRequestConfig & { _retried?: boolean }) | undefined;
  const isAuthCall = String(config?.url || '').startsWith('/auth/');
  if (error.response?.status === 401 && config && !config._retried && !isAuthCall) {
    config._retried = true;
    refreshing ??= refreshRemote().finally(() => {
      refreshing = null;
    });
    if (await refreshing) return relay(config);
  }
  return Promise.reject(error);
});

export type RemoteStatus = { enabled: boolean; baseUrl: string | null };

export async function fetchRemoteStatus() {
  const { data } = await relay.get<RemoteStatus>('/status');
  return data;
}

export type RemoteLoginResult = { ok: true } | { ok: false; selectionToken: string; workspaces: AuthWorkspace[] };

/** Người dùng tự gõ tài khoản máy chủ đăng bài. Nhiều workspace thì trả danh sách để chọn. */
export async function remoteLogin(baseUrl: string, body: { username: string; password: string; tenantCode?: string }): Promise<RemoteLoginResult> {
  const { data } = await relay.post<LoginResponse | LoginWorkspaceChoice>('/auth/login', body);
  if ('requiresWorkspaceChoice' in data && data.requiresWorkspaceChoice) {
    return { ok: false, selectionToken: data.selectionToken, workspaces: data.workspaces };
  }
  useRemoteSession.getState().setSession(baseUrl, data as LoginResponse);
  return { ok: true };
}

export async function remoteSelectWorkspace(baseUrl: string, selectionToken: string, tenantId: string) {
  const { data } = await relay.post<LoginResponse>('/auth/select-workspace', { selectionToken, tenantId });
  useRemoteSession.getState().setSession(baseUrl, data);
}

export async function remoteBrands() {
  const { data } = await relay.get<ContentBrand[]>('/api/content/brands');
  return data;
}

export async function remoteChannels(brandId: string) {
  const { data } = await relay.get<ContentChannelTarget[]>(`/api/content/brands/${brandId}/channels`);
  return data;
}

export async function remoteCreateTopic(body: { brandId: string; title: string; status?: string; bodyOutline?: string }) {
  const { data } = await relay.post<{ id: string }>('/api/content/topics', body);
  return data;
}

export async function remoteImportVariants(topicId: string, variants: ImportVariantItem[], status = 'Review') {
  const { data } = await relay.post<{ topicId: string; imported: number; status: string }>(
    `/api/content/topics/${topicId}/variants/import`,
    { variants, status },
  );
  return data;
}

export async function remoteUploadVideo(topicId: string, file: File, onProgress?: (percent: number) => void) {
  const form = new FormData();
  form.append('file', file);
  const { data } = await relay.post<YoutubeVideo>(`/api/content/topics/${topicId}/video`, form, {
    timeout: 20 * 60 * 1000,
    onUploadProgress: (e) => {
      if (onProgress && e.total) onProgress(Math.round((e.loaded / e.total) * 100));
    },
  });
  return data;
}

export async function remotePublishExtras(topicId: string, input: PublishExtrasInput) {
  const { data } = await relay.post<PublishExtras>(`/api/content/topics/${topicId}/publish-extras`, publishExtrasForm(input), {
    timeout: 120_000,
  });
  return data;
}

export async function remoteScheduleTopics(brandId: string, topics: TopicPublishPlanInput[]) {
  const { data } = await relay.post<AgentScheduleTopicsResult>(
    '/api/content/agent/schedule-topics',
    { brand: brandId, topics },
    { timeout: 120_000 },
  );
  return data;
}
