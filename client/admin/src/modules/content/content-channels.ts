import type { ContentWriteSlot } from '@/shared/api/content.api';

export const CHANNEL_GROUPS = [
  { key: 'web', label: 'Website', kinds: ['web_long', 'seo_meta'] },
  { key: 'facebook', label: 'Facebook', kinds: ['fb_page', 'fb_short', 'social_caption', 'group_suggested'] },
  { key: 'video', label: 'Video / TikTok', kinds: ['tiktok_script'] },
] as const;

export const DEST_TYPE_LABEL: Record<string, string> = {
  astro_git: 'Astro',
  wordpress_rest: 'WordPress',
  manual: 'Web chép tay',
  buffer: 'Buffer',
  facebook_page: 'Fanpage',
  facebook_group: 'Nhóm',
  instagram: 'Instagram',
  linkedin: 'LinkedIn',
  threads: 'Threads',
  zalo_oa: 'Zalo OA',
  tiktok: 'TikTok',
  youtube: 'YouTube',
  other: 'MXH khác',
};

export function writeSlotLabel(slot: ContentWriteSlot) {
  const kind = DEST_TYPE_LABEL[slot.destType] ?? slot.destType;
  return `${slot.label} · ${kind}`;
}

export function kindsFromWriteSlots(slots: ContentWriteSlot[]) {
  const set = new Set<string>();
  for (const slot of slots) {
    for (const kind of slot.variantKinds) set.add(kind);
  }
  return [...set];
}

export type FbGroupLink = { name: string; url: string };

export function parseFbGroupLines(raw?: string | null): Array<{ name: string; id: string; url: string }> {
  const out: Array<{ name: string; id: string; url: string }> = [];
  const seen = new Set<string>();
  for (const line of (raw ?? '').split(/\r?\n/)) {
    const t = line.trim();
    if (!t) continue;
    const pipe = t.split('|').map((s) => s.trim()).filter(Boolean);
    const urlPart = pipe.length >= 2 ? pipe.slice(1).join('|') : t;
    const namePart = pipe.length >= 2 ? pipe[0] : '';
    const parsed = parseFbGroupRef(urlPart) ?? (pipe.length < 2 ? parseFbGroupRef(t) : null);
    if (!parsed || seen.has(parsed.url)) continue;
    seen.add(parsed.url);
    out.push({ name: namePart || parsed.id, id: parsed.id, url: parsed.url });
  }
  return out;
}

export function parseFbGroupRef(raw?: string | null): { id: string; url: string } | null {
  const s = (raw ?? '').trim();
  if (!s) return null;
  const m = s.match(/facebook\.com\/groups\/([^/?#]+)/i);
  if (m?.[1]) {
    const id = decodeURIComponent(m[1]).replace(/\/+$/, '');
    if (!id) return null;
    return { id, url: `https://www.facebook.com/groups/${id}` };
  }
  if (/^https?:\/\//i.test(s)) return { id: s, url: s };
  if (/^[\w.-]+$/.test(s)) return { id: s, url: `https://www.facebook.com/groups/${s}` };
  return null;
}

export function groupUrlFromChannel(channel: {
  name: string;
  channelType: string;
  isActive: boolean;
  externalId?: string | null;
  configJson: string;
}): FbGroupLink | null {
  if (!channel.isActive || channel.channelType !== 'facebook_group') return null;
  let fromCfg = '';
  try {
    const cfg = JSON.parse(channel.configJson || '{}') as { url?: unknown };
    if (typeof cfg.url === 'string') fromCfg = cfg.url;
  } catch {
    /* ignore */
  }
  const parsed = parseFbGroupRef(fromCfg || channel.externalId);
  if (!parsed) return null;
  return { name: channel.name.trim() || parsed.id, url: parsed.url };
}

export function groupLinksFromChannels(
  channels: Array<{
    name: string;
    channelType: string;
    isActive: boolean;
    externalId?: string | null;
    configJson: string;
  }>,
): FbGroupLink[] {
  return channels.map(groupUrlFromChannel).filter((x): x is FbGroupLink => x != null);
}

export type FacebookLink = {
  status: string;
  pageId?: string;
  pageName?: string;
  lastError?: string;
  lastVerifiedAt?: string;
  connectedAt?: string;
};

export function parseFacebookLink(configJson?: string | null): FacebookLink | null {
  try {
    const v = JSON.parse(configJson || '{}') as { facebook?: FacebookLink };
    const fb = v.facebook;
    if (!fb || typeof fb !== 'object') return null;
    const status = (fb.status ?? '').toString().trim();
    if (!status) return null;
    return {
      status,
      pageId: fb.pageId,
      pageName: fb.pageName,
      lastError: fb.lastError,
      lastVerifiedAt: fb.lastVerifiedAt,
      connectedAt: fb.connectedAt,
    };
  } catch {
    return null;
  }
}

export function facebookStatusLabel(status?: string | null): { color: string; text: string } {
  switch ((status ?? '').toUpperCase()) {
    case 'CONNECTED':
      return { color: 'success', text: 'Đã kết nối' };
    case 'NEED_RECONNECT':
    case 'REVOKED':
    case 'EXPIRED':
    case 'PERMISSION_ERROR':
      return { color: 'warning', text: 'Cần kết nối lại' };
    case 'DISCONNECTED':
      return { color: 'default', text: 'Đã ngắt' };
    default:
      return { color: 'default', text: 'Chưa kết nối' };
  }
}

export function kindsFromChannelKeys(keys: string[]): string[] {
  const set = new Set<string>();
  for (const g of CHANNEL_GROUPS) {
    if (keys.includes(g.key)) {
      for (const k of g.kinds) set.add(k);
    }
  }
  return [...set];
}
