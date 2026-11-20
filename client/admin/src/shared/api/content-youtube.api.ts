import { http } from '@/shared/api/http';
import type { ContentChannelTarget } from '@/shared/api/content.api';

export type YoutubeVerify = {
  ok: boolean;
  status: string;
  channelId?: string | null;
  channelTitle?: string | null;
  message?: string | null;
  lastVerifiedAt?: string | null;
};

export type YoutubeVideo = { assetId: string; topicId: string; fileName: string; sizeBytes: number };

export async function startYoutubeOAuth(brandId: string) {
  const { data } = await http.get<{ url: string; state: string }>('/content/youtube/oauth/start', {
    params: { brandId },
  });
  return data;
}

export async function completeYoutubeOAuth(code: string, state: string) {
  const { data } = await http.post<ContentChannelTarget>('/content/youtube/oauth/complete', { code, state });
  return data;
}

export async function verifyYoutubeChannel(channelId: string) {
  const { data } = await http.post<YoutubeVerify>(`/content/channels/${channelId}/youtube/verify`);
  return data;
}

export async function uploadTopicVideo(topicId: string, file: File) {
  const form = new FormData();
  form.append('file', file);
  const { data } = await http.post<YoutubeVideo>(`/content/topics/${topicId}/video`, form, {
    headers: { 'Content-Type': 'multipart/form-data' },
    timeout: 10 * 60 * 1000,
  });
  return data;
}

/** Thông tin đăng kèm video: ảnh bìa ngang (YouTube, video Fanpage), ảnh bìa dọc (Shorts, Reels), danh sách phát, khung hình bìa TikTok. */
export type PublishExtrasInput = {
  thumbnail?: Blob | null;
  cover?: Blob | null;
  youtubePlaylist?: string;
  coverTimestampMs?: number;
};

export type PublishExtras = {
  topicId: string;
  thumbnailAssetId?: string | null;
  coverAssetId?: string | null;
  youtubePlaylist?: string | null;
  coverTimestampMs?: number | null;
};

export function publishExtrasForm(input: PublishExtrasInput) {
  const form = new FormData();
  if (input.thumbnail) form.append('thumbnail', input.thumbnail, input.thumbnail.type === 'image/png' ? 'thumbnail.png' : 'thumbnail.jpg');
  if (input.cover) form.append('cover', input.cover, input.cover.type === 'image/png' ? 'cover.png' : 'cover.jpg');
  if (input.youtubePlaylist?.trim()) form.append('youtubePlaylist', input.youtubePlaylist.trim());
  if (typeof input.coverTimestampMs === 'number') form.append('coverTimestampMs', String(Math.max(0, Math.round(input.coverTimestampMs))));
  return form;
}

export async function savePublishExtras(topicId: string, input: PublishExtrasInput) {
  const { data } = await http.post<PublishExtras>(`/content/topics/${topicId}/publish-extras`, publishExtrasForm(input), {
    headers: { 'Content-Type': 'multipart/form-data' },
    timeout: 120_000,
  });
  return data;
}

export type ImportVariantItem = { kind: string; title?: string; bodyMarkdown: string };

export async function importTopicVariants(topicId: string, variants: ImportVariantItem[], status = 'Review') {
  const { data } = await http.post<{ topicId: string; imported: number; status: string }>(
    `/content/topics/${topicId}/variants/import`,
    { variants, status },
  );
  return data;
}
