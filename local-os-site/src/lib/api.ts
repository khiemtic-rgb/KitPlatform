export type LocalListing = {
  id: string;
  kind: string;
  title: string;
  summary?: string | null;
  organizationName?: string | null;
  placeText?: string | null;
  contactPhone?: string | null;
  salaryText?: string | null;
  workingTime?: string | null;
  employmentType?: string | null;
  startAt?: string | null;
  endAt?: string | null;
  registrationUrl?: string | null;
  priceMonth?: number | null;
  roomType?: string | null;
  trust: string;
  sourceUrl?: string | null;
  publishedAt?: string | null;
  lastCheckedAt?: string | null;
};

const API = (import.meta.env.PUBLIC_LOCAL_OS_API as string | undefined)?.replace(/\/$/, '')
  || 'http://127.0.0.1:5290/api/public/local-os';

export async function listListings(kind?: string, q?: string): Promise<LocalListing[]> {
  const url = new URL(`${API}/listings`);
  if (kind) url.searchParams.set('kind', kind);
  if (q) url.searchParams.set('q', q);
  const res = await fetch(url);
  if (!res.ok) return [];
  return (await res.json()) as LocalListing[];
}

export async function getListing(id: string): Promise<LocalListing | null> {
  const res = await fetch(`${API}/listings/${id}`);
  if (!res.ok) return null;
  return (await res.json()) as LocalListing;
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
  token: string;
  template: string;
  categories?: string[];
  title: string;
  quantity?: string;
  placeText: string;
  workingTime: string;
  salaryText?: string;
  requirements?: string;
  contactName: string;
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
  return `/viec/${item.id}`;
}

export function kindLabel(kind: string): string {
  if (kind === 'event') return 'Sự kiện';
  if (kind === 'room') return 'Phòng trọ';
  return 'Việc làm';
}

export function formatPriceMonth(n?: number | null): string {
  if (n == null) return '';
  return `${n.toLocaleString('vi-VN')} đ/tháng`;
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
    mon: d.toLocaleString('en-US', { month: 'short' }).toUpperCase(),
  };
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
  if (item.kind === 'event') return formatWhen(item.startAt);
  return formatPriceMonth(item.priceMonth);
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

export function telHref(phone?: string | null): string | null {
  if (!phone) return null;
  const first = phone.split(/[/,;]/)[0]?.replace(/\D/g, '') ?? '';
  return first ? `tel:${first}` : null;
}
