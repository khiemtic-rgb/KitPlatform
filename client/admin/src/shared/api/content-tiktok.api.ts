import { http } from '@/shared/api/http';
import type { ContentChannelTarget } from '@/shared/api/content.api';

export type TiktokVerify = {
  ok: boolean;
  status: string;
  openId?: string | null;
  displayName?: string | null;
  message?: string | null;
  lastVerifiedAt?: string | null;
};

export async function startTiktokOAuth(brandId: string) {
  const { data } = await http.get<{ url: string; state: string }>('/content/tiktok/oauth/start', {
    params: { brandId },
  });
  return data;
}

export async function completeTiktokOAuth(code: string, state: string) {
  const { data } = await http.post<ContentChannelTarget>('/content/tiktok/oauth/complete', { code, state });
  return data;
}

export async function verifyTiktokChannel(channelId: string) {
  const { data } = await http.post<TiktokVerify>(`/content/channels/${channelId}/tiktok/verify`);
  return data;
}
