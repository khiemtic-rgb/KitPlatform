export type LocalListing = {
  id: string;
  kind: string;
  title: string;
  summary?: string | null;
  organizationName?: string | null;
  placeText?: string | null;
  audience?: string[] | null;
  category?: string | null;
  contactPhone?: string | null;
  contactName?: string | null;
  sourceName?: string | null;
  salaryText?: string | null;
  workingTime?: string | null;
  employmentType?: string | null;
  requirements?: string | null;
  startAt?: string | null;
  endAt?: string | null;
  registrationUrl?: string | null;
  priceMonth?: number | null;
  roomType?: string | null;
  trust: string;
  sourceUrl?: string | null;
  publishedAt?: string | null;
  lastCheckedAt?: string | null;
  expiresAt?: string | null;
};

const API = (import.meta.env.PUBLIC_LOCAL_OS_API as string | undefined)?.replace(/\/$/, '')
  || 'http://127.0.0.1:5290/api/public/local-os';

/** Shop deals + scholarships parked until brand partners. Flip to show nav/pages again. */
export const OFFERS_PUBLIC = false;

type PublicFeed = { listings?: LocalListing[]; groups?: CommunityGroup[] };

let feedCache: PublicFeed | null = null;

async function bundledFeed(): Promise<PublicFeed> {
  if (feedCache) return feedCache;
  try {
    const mod = await import('../data/public-feed.json');
    feedCache = (mod.default ?? mod) as PublicFeed;
  } catch {
    feedCache = { listings: [], groups: [] };
  }
  return feedCache;
}

async function kvFeed(): Promise<PublicFeed | null> {
  try {
    const { readLiveFeed } = await import('./live-feed');
    const live = await readLiveFeed();
    if (live?.listings?.length) return live as PublicFeed;
  } catch {
    /* local Astro has no KV */
  }
  return null;
}

function matchListing(item: LocalListing, q?: string): boolean {
  if (!q) return true;
  const n = q.trim().toLowerCase();
  if (!n) return true;
  return [item.title, item.summary, item.placeText, item.organizationName]
    .some((v) => (v ?? '').toLowerCase().includes(n));
}

export async function listListings(kind?: string, q?: string): Promise<LocalListing[]> {
  const live = await kvFeed();
  if (live?.listings?.length) {
    return live.listings.filter((item) => (!kind || item.kind === kind) && matchListing(item, q));
  }
  try {
    const url = new URL(`${API}/listings`);
    if (kind) url.searchParams.set('kind', kind);
    if (q) url.searchParams.set('q', q);
    const res = await fetch(url);
    if (res.ok) return (await res.json()) as LocalListing[];
  } catch {
    /* Cloudflare cannot reach the local API — use deploy snapshot. */
  }
  const feed = await bundledFeed();
  return (feed.listings ?? []).filter((item) => (!kind || item.kind === kind) && matchListing(item, q));
}

export async function getListing(id: string): Promise<LocalListing | null> {
  const live = await kvFeed();
  if (live?.listings?.length) {
    return live.listings.find((item) => item.id === id) ?? null;
  }
  try {
    const res = await fetch(`${API}/listings/${id}`);
    if (res.ok) return (await res.json()) as LocalListing;
  } catch {
    /* use snapshot */
  }
  const feed = await bundledFeed();
  return (feed.listings ?? []).find((item) => item.id === id) ?? null;
}

export type CommunityGroup = {
  id: string;
  name: string;
  url: string;
  platform: string;
  category: string;
  audience: string;
  geo: string;
};

export async function listGroups(): Promise<CommunityGroup[]> {
  const live = await kvFeed();
  if (live?.groups?.length) return live.groups;
  try {
    const res = await fetch(`${API}/groups`);
    if (res.ok) return (await res.json()) as CommunityGroup[];
  } catch {
    /* use snapshot */
  }
  const feed = await bundledFeed();
  return feed.groups ?? [];
}

export type PublishJobResult = {
  listing: LocalListing;
  shareText: string;
  publicUrl: string;
  groups: CommunityGroup[];
  reviewNote: string;
};

export async function requestPublisherOtp(phone: string): Promise<{ sent: boolean; debugCode?: string | null }> {
  const res = await fetch(`${API}/publisher/otp`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ phone }),
  });
  if (!res.ok) throw new Error(await readApiError(res, 'Không gửi được mã.'));
  return (await res.json()) as { sent: boolean; debugCode?: string | null };
}

export async function verifyPublisherOtp(phone: string, code: string): Promise<{ token: string }> {
  const res = await fetch(`${API}/publisher/verify`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ phone, code }),
  });
  if (!res.ok) throw new Error(await readApiError(res, 'Mã không đúng hoặc hết hạn.'));
  return (await res.json()) as { token: string };
}

export async function publishJob(body: {
  phone: string;
  token?: string;
  kind?: 'job' | 'event' | 'room';
  template?: string;
  categories?: string[];
  title: string;
  quantity?: string;
  placeText: string;
  workingTime?: string;
  salaryText?: string;
  requirements?: string;
  contactName: string;
  roomType?: string;
  startAt?: string;
  endAt?: string;
  registrationUrl?: string;
}): Promise<PublishJobResult> {
  const res = await fetch(`${API}/listings/jobs`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!res.ok) throw new Error(await readApiError(res, 'Không gửi được tin.'));
  return (await res.json()) as PublishJobResult;
}

export async function trackShare(listingId: string, groupId: string | null, eventKind: 'copy' | 'open'): Promise<void> {
  await fetch(`${API}/share-events`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ listingId, groupId, eventKind }),
  });
}

async function readApiError(res: Response, fallback: string): Promise<string> {
  try {
    const body = (await res.json()) as { message?: string };
    return body.message || fallback;
  } catch {
    return fallback;
  }
}

export function hrefFor(item: Pick<LocalListing, 'id' | 'kind'>): string {
  if (item.kind === 'event') return `/su-kien/${item.id}`;
  if (item.kind === 'room') return `/tro/${item.id}`;
  if (item.kind === 'grant') return `/uu-dai/${item.id}`;
  return `/viec/${item.id}`;
}

export function kindLabel(kind: string): string {
  if (kind === 'event') return 'Sự kiện';
  if (kind === 'room') return 'Phòng trọ';
  if (kind === 'offer') return 'Ưu đãi';
  if (kind === 'grant') return 'Học bổng';
  return 'Việc làm';
}

export function trustLabel(trust?: string | null): string {
  switch ((trust ?? '').toUpperCase()) {
    case 'OFFICIAL':
      return 'Nguồn chính thức';
    case 'SOURCE_TRUSTED':
      return 'Nguồn đã đăng ký';
    case 'VERIFIED':
      return 'Đã xác minh';
    case 'COMMUNITY':
      return 'Cộng đồng';
    default:
      return '';
  }
}

export function cleanPlace(place?: string | null): string {
  if (!place) return '';
  let s = place.replace(/,\s*$/g, '').trim();
  s = s.replace(/,\s*Thành phố Thái Nguyên,\s*Thái Nguyên$/i, ', TP. Thái Nguyên');
  s = s.replace(/,\s*Thái Nguyên,\s*Thái Nguyên$/i, ', Thái Nguyên');
  s = s.replace(/Thành phố Thái Nguyên/gi, 'TP. Thái Nguyên');
  return s;
}

export function cleanTitle(title?: string | null, _place?: string | null): string {
  let s = (title ?? '').replace(/,\s*$/g, '').trim();
  const cut = s.split(/\s+[—–-]\s+/)[0]?.trim();
  if (cut && cut.length >= 8) s = cut;
  s = s.replace(/Thành phố Thái Nguyên/gi, 'TP. Thái Nguyên');
  return s;
}

/** Amenities / notes only — drop repeated price + source boilerplate. */
export function roomNoteChips(summary?: string | null): string[] {
  if (!summary) return [];
  return summary
    .split(/[·•]|(?:\.\s+)/)
    .map((x) => x.replace(/\.$/, '').trim())
    .filter((x) => x.length > 1 && x.length < 80)
    .filter((x) => !/^giá\s*:/i.test(x))
    .filter((x) => !/^nguồn\s*:/i.test(x))
    .filter((x) => !/liên hệ chủ/i.test(x))
    .filter((x) => !/đổi theo thời điểm/i.test(x));
}

export function formatPriceMonth(n?: number | null): string {
  if (n == null) return '';
  if (n >= 1_000_000) {
    const trieu = n / 1_000_000;
    const s = Number.isInteger(trieu) ? String(trieu) : trieu.toFixed(1).replace('.', ',');
    return `${s} triệu/tháng`;
  }
  return `${n.toLocaleString('vi-VN')} đ/tháng`;
}

export function formatEventLine(iso?: string | null): string {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  const time = d.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
  const weekday = ['Chủ nhật', 'Thứ 2', 'Thứ 3', 'Thứ 4', 'Thứ 5', 'Thứ 6', 'Thứ 7'][d.getDay()];
  const date = `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}`;
  return `${time} • ${weekday}, ${date}`;
}

export function trendSubtitle(item: LocalListing): string {
  if (item.kind === 'job') return [item.salaryText, item.placeText].filter(Boolean).join(' • ');
  if (item.kind === 'event') return formatEventLine(item.startAt) || item.placeText || '';
  return formatPriceMonth(item.priceMonth) || item.placeText || '';
}

export function trendFoot(item: LocalListing): { kind: 'fire' | 'party' | 'pin'; text: string } {
  if (item.kind === 'job') {
    return { kind: 'fire', text: formatRelative(item.lastCheckedAt || item.publishedAt) || 'Tin mới' };
  }
  if (item.kind === 'event') {
    return { kind: 'party', text: item.placeText || 'Sự kiện Thái Nguyên' };
  }
  return { kind: 'pin', text: item.placeText || 'Phòng trọ Thái Nguyên' };
}

export function formatWhen(iso?: string | null): string {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString('vi-VN', { dateStyle: 'short', timeStyle: 'short' });
}

export function formatDayBox(iso?: string | null): { day: string; mon: string } | null {
  if (!iso) return null;
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return null;
  return {
    day: String(d.getDate()).padStart(2, '0'),
    mon: ['CN', 'Th2', 'Th3', 'Th4', 'Th5', 'Th6', 'Th7'][d.getDay()],
  };
}

export function formatClock(iso?: string | null): string {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  return d.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
}

export function isRecent(iso?: string | null, hours = 48): boolean {
  if (!iso) return false;
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return false;
  return Date.now() - d.getTime() < hours * 3600 * 1000;
}

export function groupInitials(name: string): string {
  const parts = name.replace(/[()]/g, '').split(/\s+/).filter(Boolean);
  const first = parts[0]?.[0] ?? 'G';
  const last = parts.length > 1 ? parts[parts.length - 1][0] : (parts[0]?.[1] ?? '');
  return (first + last).toUpperCase();
}

export function coverFor(kind: string, index = 0): string {
  if (kind === 'event' || kind === 'grant') return index % 2 === 0 ? '/trend/event.jpg' : '/trend/event2.jpg';
  if (kind === 'room') return index % 2 === 0 ? '/trend/room.jpg' : '/trend/room2.jpg';
  return ['/trend/job.jpg', '/trend/job2.jpg', '/trend/job3.jpg'][index % 3];
}

export function orgInitial(item: Pick<LocalListing, 'organizationName' | 'title'>): string {
  const src = (item.organizationName || item.title || 'V').trim();
  return src[0]?.toUpperCase() ?? 'V';
}

export function groupCover(index: number): string {
  return [`/trend/comm1.jpg`, `/trend/comm2.jpg`, `/trend/comm3.jpg`, `/trend/comm4.jpg`][index % 4];
}

export function groupHint(group: Pick<CommunityGroup, 'category' | 'audience' | 'platform'>): string {
  if (group.category === 'job' && group.audience === 'student') return 'Group việc / sinh viên';
  if (group.category === 'job') return 'Group việc làm';
  if (group.category === 'room') return 'Group phòng trọ';
  if (group.category === 'event') return 'Group sự kiện';
  return group.platform === 'facebook' ? 'Facebook group' : 'Group gợi ý';
}

export function formatRelative(iso?: string | null): string {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  const mins = Math.round((Date.now() - d.getTime()) / 60000);
  if (mins < 1) return 'Vừa xong';
  if (mins < 60) return `${mins} phút trước`;
  const hours = Math.round(mins / 60);
  if (hours < 24) return `${hours} giờ trước`;
  const days = Math.round(hours / 24);
  if (days < 7) return `${days} ngày trước`;
  return d.toLocaleDateString('vi-VN');
}

export function cardHighlight(item: LocalListing): string {
  if (item.kind === 'job') return item.salaryText || item.workingTime || '';
  if (item.kind === 'event') return formatWhen(item.startAt) || item.workingTime || '';
  if (item.kind === 'grant') return item.salaryText || 'Xem điều kiện trên tin gốc';
  return formatPriceMonth(item.priceMonth) || 'Giá liên hệ';
}

export const ROOM_TYPE_LABEL: Record<string, string> = {
  private: 'Phòng riêng',
  shared: 'Ở ghép',
  transfer: 'Chuyển nhượng',
};

export const EMP_LABEL: Record<string, string> = {
  part_time: 'Part-time',
  full_time: 'Full-time',
  weekend: 'Cuối tuần',
};

export const JOB_CAT_LABEL: Record<string, string> = {
  internship: 'Thực tập',
  part_time: 'Part-time',
  full_time: 'Full-time',
  weekend: 'Cuối tuần',
  service: 'Phục vụ',
  sales: 'Bán hàng',
};

export function listingPhone(item: Pick<LocalListing, 'contactPhone' | 'sourceUrl' | 'summary'>): string {
  const field = (item.contactPhone ?? '').trim();
  if (field) return field;
  const hash = item.sourceUrl?.match(/[#&]p=(\d{8,12})/i);
  if (hash?.[1]) return hash[1];
  const inText = `${item.summary ?? ''}`.match(/(?:\+84|0)[\d\s.]{8,14}/);
  return inText?.[0]?.replace(/[^\d+]/g, '') ?? '';
}

export function formatPhone(phone?: string | null): string {
  const d = (phone ?? '').replace(/\D/g, '');
  if (d.length === 10) return `${d.slice(0, 4)} ${d.slice(4, 7)} ${d.slice(7)}`;
  if (d.length === 11) return `${d.slice(0, 4)} ${d.slice(4, 7)} ${d.slice(7)}`;
  return (phone ?? '').trim();
}

export function telHref(phone?: string | null): string | null {
  if (!phone) return null;
  const first = phone.split(/[/,;]/)[0]?.replace(/\D/g, '') ?? '';
  return first ? `tel:${first}` : null;
}
