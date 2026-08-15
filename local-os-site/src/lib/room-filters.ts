import { roomNoteChips, type LocalListing } from './api';

export const SCHOOLS = [
  { id: 'sp', label: 'ĐH Sư phạm' },
  { id: 'kh', label: 'ĐH Khoa học (TNUS)' },
  { id: 'ictu', label: 'ICTU' },
] as const;

export const DISTANCES = [
  { id: '500', label: 'Trong 500 m', meters: 500 },
  { id: '1000', label: 'Trong 1 km', meters: 1000 },
  { id: '2000', label: 'Trong 2 km', meters: 2000 },
  { id: '5000', label: 'Trong 5 km', meters: 5000 },
] as const;

const WARD_NAMES = [
  'Quang Trung',
  'Phan Đình Phùng',
  'Tân Thịnh',
  'Quyết Thắng',
  'Hoàng Văn Thụ',
  'Tân Long',
  'Quang Vinh',
  'Đồng Quang',
  'Túc Duyên',
  'Gia Sàng',
  'Tân Thành',
  'Phú Xá',
  'Trung Thành',
  'Tích Lương',
  'Cam Giá',
  'Hương Sơn',
  'Thịnh Đán',
  'Phú Lương',
];

export function schoolOf(item: LocalListing): string {
  const blob = `${item.title} ${item.summary ?? ''} ${item.sourceUrl ?? ''} ${item.sourceName ?? ''}`.toLowerCase();
  if (blob.includes('sư phạm') || blob.includes('su pham') || blob.includes('tnue')) return 'sp';
  if (blob.includes('khoa học') || blob.includes('khoa hoc') || blob.includes('tnus') || blob.includes('1zav9iswr'))
    return 'kh';
  if (blob.includes('ictu') || blob.includes('cntt') || blob.includes('truyền thông')) return 'ictu';
  return '';
}

export function wardOf(item: LocalListing): string {
  const place = item.placeText ?? '';
  const prefixed = place.match(/(?:phường|ph\.|p\.|xã|xa)\s+([^,/]+)/i);
  if (prefixed?.[1]) return tidyWard(prefixed[1]);
  const lower = place.toLowerCase();
  for (const name of WARD_NAMES) {
    if (lower.includes(name.toLowerCase())) return name;
  }
  return '';
}

function tidyWard(raw: string): string {
  const s = raw.replace(/\s+/g, ' ').trim().replace(/\.$/, '');
  const hit = WARD_NAMES.find((w) => w.toLowerCase() === s.toLowerCase());
  return hit ?? s.replace(/^(phường|xã)\s+/i, '');
}

export function distanceMeters(item: LocalListing): number | null {
  const text = `${item.summary ?? ''} ${item.title}`;
  const range = text.match(/(\d+(?:[.,]\d+)?)\s*[-–]\s*(\d+(?:[.,]\d+)?)\s*(km|m)\b/i);
  if (range) {
    const a = toMeters(range[1], range[3]);
    const b = toMeters(range[2], range[3]);
    return Math.max(a, b);
  }
  const one = text.match(/(\d+(?:[.,]\d+)?)\s*(km|m)\b/i);
  if (one) return toMeters(one[1], one[2]);
  return null;
}

function toMeters(raw: string | undefined, unit: string | undefined): number {
  const n = Number((raw ?? '0').replace(',', '.'));
  if (!Number.isFinite(n)) return 0;
  return (unit ?? 'm').toLowerCase() === 'km' ? n * 1000 : n;
}

export function wardsIn(items: LocalListing[]): string[] {
  return [...new Set(items.map(wardOf).filter(Boolean))].sort((a, b) => a.localeCompare(b, 'vi'));
}

export function schoolLabel(id?: string | null): string {
  return SCHOOLS.find((s) => s.id === id)?.label ?? '';
}

export function formatDistance(item: LocalListing): string {
  const m = distanceMeters(item);
  if (m == null) return '';
  if (m >= 1000) {
    const km = m / 1000;
    const s = Number.isInteger(km) ? String(km) : km.toFixed(1).replace('.', ',');
    return `${s} km`;
  }
  return `${Math.round(m)} m`;
}

export function roomCardSpecs(item: LocalListing): string[] {
  const out: string[] = [];
  const dist = formatDistance(item);
  if (dist) out.push(dist);
  const prefer = [/wifi/i, /điều hoà|điều hòa/i, /nóng lạnh/i, /nội thất|giường|tủ/i];
  const chips = roomNoteChips(item.summary);
  for (const re of prefer) {
    const hit = chips.find((c) => re.test(c));
    if (hit && !out.includes(hit) && out.length < 3) out.push(shortSpec(hit));
  }
  for (const c of chips) {
    if (out.length >= 3) break;
    const s = shortSpec(c);
    if (s && !out.includes(s)) out.push(s);
  }
  return out;
}

function shortSpec(raw: string): string {
  const s = raw.replace(/\s+/g, ' ').trim();
  if (s.length <= 22) return s;
  return `${s.slice(0, 20).trim()}…`;
}

const AMENITY_DEFS = [
  { id: 'wifi', label: 'Wifi', re: /wifi|wi-fi/i },
  { id: 'ac', label: 'Điều hòa', re: /điều hoà|điều hòa/i },
  { id: 'hot', label: 'Nóng lạnh', re: /nóng lạnh/i },
  { id: 'bed', label: 'Giường', re: /giường/i },
  { id: 'park', label: 'Chỗ để xe', re: /để xe|chỗ xe|nhà xe/i },
  { id: 'wash', label: 'Máy giặt', re: /máy giặt/i },
  { id: 'cook', label: 'Bếp', re: /bếp|nấu/i },
  { id: 'safe', label: 'An ninh', re: /an ninh|camera|tự khóa/i },
] as const;

export function roomAmenities(item: LocalListing): { id: string; label: string }[] {
  const text = `${item.summary ?? ''} ${item.title}`;
  return AMENITY_DEFS.filter((a) => a.re.test(text)).map((a) => ({ id: a.id, label: a.label }));
}

export function roomArea(item: LocalListing): string {
  const m = `${item.summary ?? ''} ${item.title}`.match(/(\d+(?:[.,]\d+)?)\s*m(?:2|²)/i);
  return m ? `${m[1].replace('.', ',')} m²` : '';
}

export function similarRooms(current: LocalListing, all: LocalListing[], limit = 4): LocalListing[] {
  const school = schoolOf(current);
  const ward = wardOf(current);
  const others = all.filter((r) => r.id !== current.id);
  const ranked = others.filter((r) => (school && schoolOf(r) === school) || (ward && wardOf(r) === ward));
  return (ranked.length > 0 ? ranked : others).slice(0, limit);
}

export function filterRooms(
  items: LocalListing[],
  opts: { school?: string; ward?: string; dist?: string },
): LocalListing[] {
  const maxM = DISTANCES.find((d) => d.id === opts.dist)?.meters;
  return items.filter((item) => {
    if (opts.school && schoolOf(item) !== opts.school) return false;
    if (opts.ward && wardOf(item) !== opts.ward) return false;
    if (maxM != null) {
      const m = distanceMeters(item);
      if (m == null || m > maxM) return false;
    }
    return true;
  });
}
