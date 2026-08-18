import { parseFbGroupRef } from '@/modules/content/content-channels';
import type { ContentChannelTarget, ContentSiteTarget, ContentVariant } from '@/shared/api/content.api';

export type ManualLane = {
  key: string;
  label: string;
  alwaysManual: boolean;
  variantKinds: string[];
};

export type ManualDest = {
  key: string;
  lane: string;
  name: string;
  url: string | null;
  hint?: string;
};

export const MANUAL_LANES: ManualLane[] = [
  { key: 'facebook_group', label: 'Nhóm Facebook', alwaysManual: true, variantKinds: ['group_suggested'] },
  { key: 'facebook_page', label: 'Fanpage', alwaysManual: false, variantKinds: ['fb_page', 'fb_short', 'social_caption'] },
  { key: 'linkedin', label: 'LinkedIn', alwaysManual: true, variantKinds: ['linkedin', 'social_caption', 'fb_short'] },
  { key: 'instagram', label: 'Instagram', alwaysManual: true, variantKinds: ['instagram', 'social_caption'] },
  { key: 'threads', label: 'Threads', alwaysManual: true, variantKinds: ['fb_short', 'social_caption'] },
  { key: 'zalo_oa', label: 'Zalo OA', alwaysManual: true, variantKinds: ['fb_short', 'social_caption'] },
  { key: 'tiktok', label: 'TikTok', alwaysManual: true, variantKinds: ['tiktok_script', 'social_caption'] },
  { key: 'youtube', label: 'YouTube', alwaysManual: true, variantKinds: ['tiktok_script', 'social_caption'] },
  { key: 'other', label: 'MXH khác', alwaysManual: true, variantKinds: ['social_caption', 'fb_short'] },
  { key: 'website', label: 'Website', alwaysManual: false, variantKinds: ['web_long', 'seo_meta'] },
];

function parseCfg(raw?: string | null): Record<string, unknown> {
  if (!raw?.trim()) return {};
  try {
    const v = JSON.parse(raw) as unknown;
    return v && typeof v === 'object' && !Array.isArray(v) ? (v as Record<string, unknown>) : {};
  } catch {
    return {};
  }
}

function cfgUrl(cfg: Record<string, unknown>): string | null {
  const u = cfg.url;
  return typeof u === 'string' && /^https?:\/\//i.test(u.trim()) ? u.trim() : null;
}

export function channelOpenUrl(ch: ContentChannelTarget): string | null {
  const cfg = parseCfg(ch.configJson);
  const fromCfg = cfgUrl(cfg);
  if (fromCfg) return fromCfg;
  const id = (ch.externalId ?? '').trim();
  if (!id) return null;
  if (/^https?:\/\//i.test(id)) return id;
  switch (ch.channelType) {
    case 'facebook_group':
      return parseFbGroupRef(id)?.url ?? null;
    case 'facebook_page':
      return `https://www.facebook.com/${id}`;
    case 'instagram':
      return `https://www.instagram.com/${id.replace(/^@/, '')}/`;
    case 'linkedin':
      return id.includes('linkedin.com') ? id : `https://www.linkedin.com/company/${id}`;
    case 'threads':
      return `https://www.threads.net/@${id.replace(/^@/, '')}`;
    case 'tiktok':
      return `https://www.tiktok.com/@${id.replace(/^@/, '')}`;
    case 'youtube':
      return id.startsWith('@') || id.startsWith('c/') || id.startsWith('channel/')
        ? `https://www.youtube.com/${id}`
        : `https://www.youtube.com/${id}`;
    default:
      return null;
  }
}

export function lanesFromTargets(
  channels: ContentChannelTarget[],
  sites: ContentSiteTarget[],
): ManualLane[] {
  const activeCh = channels.filter((c) => c.isActive);
  const activeSites = sites.filter((s) => s.isActive);
  return MANUAL_LANES.filter((lane) => {
    if (lane.key === 'website') return activeSites.length > 0;
    return activeCh.some((c) => c.channelType === lane.key);
  });
}

export function destsForLane(
  lane: string,
  channels: ContentChannelTarget[],
  sites: ContentSiteTarget[],
): ManualDest[] {
  if (lane === 'website') {
    return sites
      .filter((s) => s.isActive)
      .map((s) => ({
        key: `site:${s.id}`,
        lane,
        name: s.name,
        url: s.baseUrl?.trim() || null,
        hint: s.connectorType === 'manual' ? 'Chép tay' : s.connectorType,
      }));
  }
  return channels
    .filter((c) => c.isActive && c.channelType === lane)
    .map((c) => ({
      key: `ch:${c.id}`,
      lane,
      name: c.name,
      url: channelOpenUrl(c),
      hint: c.secretConfigured ? 'Đã kết nối (Kết nối lại nếu Meta thu hồi)' : undefined,
    }));
}

export function pickManualPostText(variants: ContentVariant[], kinds: string[]): { text: string; kind: string | null } {
  for (const kind of kinds) {
    const v = variants.find((x) => x.kind === kind && (x.bodyMarkdown ?? '').trim());
    if (!v) continue;
    return {
      kind,
      text:
        kind === 'group_suggested'
          ? (v.bodyMarkdown ?? '').trim()
          : [v.title, v.bodyMarkdown].filter((x) => (x ?? '').trim()).join('\n\n'),
    };
  }
  return { text: '', kind: null };
}

export async function writeClipboardImage(blob: Blob): Promise<void> {
  // Chrome/Edge clipboard.write only accepts image/png (JPEG throws).
  const png = blob.type === 'image/png' ? blob : await blobToPng(blob);
  await navigator.clipboard.write([
    new ClipboardItem({ 'image/png': Promise.resolve(png) }),
  ]);
}

async function blobToPng(blob: Blob): Promise<Blob> {
  const url = URL.createObjectURL(blob);
  try {
    const img = await new Promise<HTMLImageElement>((resolve, reject) => {
      const el = new Image();
      el.onload = () => resolve(el);
      el.onerror = () => reject(new Error('Không đọc được ảnh'));
      el.src = url;
    });
    const canvas = document.createElement('canvas');
    canvas.width = img.naturalWidth || img.width;
    canvas.height = img.naturalHeight || img.height;
    const ctx = canvas.getContext('2d');
    if (!ctx) throw new Error('Canvas không sẵn');
    ctx.drawImage(img, 0, 0);
    const out = await new Promise<Blob>((resolve, reject) => {
      canvas.toBlob((b) => (b ? resolve(b) : reject(new Error('Không đổi PNG'))), 'image/png');
    });
    return out;
  } finally {
    URL.revokeObjectURL(url);
  }
}
