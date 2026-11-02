export type FactoryJobLabel = {
  id: string;
  brandCode?: string | null;
  templateCode?: string | null;
  provider?: string | null;
  status: string;
  outputUrl?: string | null;
  storyboardJson?: string | null;
};

const STATUS_LABEL: Record<string, { text: string; color: string }> = {
  Draft: { text: 'Nháp', color: 'default' },
  GeneratingScript: { text: 'Đang viết script…', color: 'processing' },
  GeneratingAssets: { text: 'Đang tạo ảnh…', color: 'processing' },
  GeneratingVoice: { text: 'Đang tạo voice…', color: 'processing' },
  PreparingRender: { text: 'Chuẩn bị render…', color: 'processing' },
  Queued: { text: 'Hàng đợi', color: 'processing' },
  Rendering: { text: 'Đang render…', color: 'blue' },
  Ready: { text: 'Sẵn sàng', color: 'cyan' },
  Failed: { text: 'Lỗi', color: 'red' },
  Approved: { text: 'Đã duyệt', color: 'green' },
};

export function playableMarketingVideoUrl(url?: string | null) {
  const value = (url || '').trim();
  if (/^\/api\/content\/video\/media\/[0-9a-f-]{36}\/output\.mp4$/i.test(value)) return value;
  if (!/^https?:\/\//i.test(value) && !value.toLowerCase().startsWith('data:video')) return '';
  if (/\.(jpg|jpeg|png|webp|gif)(\?|$)/i.test(value)) return '';
  return value;
}

export function isNovixaTiktokHooksGate(job: Pick<FactoryJobLabel, 'brandCode' | 'templateCode' | 'provider'>) {
  return (job.brandCode || '').toLowerCase() === 'novixa'
    && (job.templateCode || '').toLowerCase() === 'tiktok_45s_hooks'
    && (job.provider || '').toLowerCase() !== 'creatomate';
}

export function videoSourceForJob(job: FactoryJobLabel) {
  const playable = playableMarketingVideoUrl(job.outputUrl);
  if (!playable) return '';
  if (!isNovixaTiktokHooksGate(job)) return playable;
  const suffix = `/api/content/video/media/${job.id}/output.mp4`.toLowerCase();
  return playable.split('?')[0].toLowerCase().endsWith(suffix) ? playable : '';
}

export function factoryVideoTag(job: FactoryJobLabel) {
  const base = STATUS_LABEL[job.status] ?? { text: job.status, color: 'default' };
  if (!isNovixaTiktokHooksGate(job)) return base;
  if (job.status === 'Approved') return base;
  if (job.status === 'Ready' && videoSourceForJob(job)) return { text: 'Sẵn sàng', color: 'cyan' };
  if (job.status === 'Ready' || job.status === 'PreparingRender') return { text: 'Storyboard', color: 'blue' };
  return base;
}
