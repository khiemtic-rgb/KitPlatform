import { roomNoteChips, type LocalListing } from './api';

export const WARDS = [
  { id: 'phan-dinh-phung', label: 'Phường Phan Đình Phùng', aliases: ['phan đình phùng', 'phan dinh phung'] },
  { id: 'quyet-thang', label: 'Phường Quyết Thắng', aliases: ['quyết thắng', 'quyet thang', 'quết thắng', 'quyết thắbg'] },
  { id: 'quan-trieu', label: 'Phường Quan Triều', aliases: ['quan triều', 'quan trieu'] },
  { id: 'gia-sang', label: 'Phường Gia Sàng', aliases: ['gia sàng', 'gia sang'] },
  { id: 'linh-son', label: 'Phường Linh Sơn', aliases: ['linh sơn', 'linh son'] },
  { id: 'tich-luong', label: 'Phường Tích Lương', aliases: ['tích lương', 'tich luong'] },
  { id: 'tan-cuong', label: 'Xã Tân Cương', aliases: ['tân cương', 'tan cuong'] },
  { id: 'dai-phuc', label: 'Xã Đại Phúc', aliases: ['đại phúc', 'dai phuc'] },
] as const;

export const SCHOOLS = [
  { id: 'sp', label: 'Trường Đại học Sư phạm', short: 'ĐH Sư phạm', code: 'TNUE', aliases: ['sư phạm', 'su pham', 'tnue'] },
  { id: 'ktcn', label: 'Trường Đại học Kỹ thuật Công nghiệp', short: 'ĐH Kỹ thuật CN', code: 'TNUT', aliases: ['kỹ thuật công nghiệp', 'ky thuat cong nghiep', 'tnut', 'tkcn'] },
  { id: 'nl', label: 'Trường Đại học Nông Lâm', short: 'ĐH Nông Lâm', code: 'TUAF', aliases: ['nông lâm', 'nong lam', 'tuaf'] },
  { id: 'yd', label: 'Trường Đại học Y Dược', short: 'ĐH Y Dược', code: 'TUMP', aliases: ['y dược', 'y duoc', 'tump'] },
  { id: 'ktqtkd', label: 'Trường Đại học Kinh tế và Quản trị Kinh doanh', short: 'ĐH Kinh tế & QTKD', code: 'TUEBA', aliases: ['quản trị kinh doanh', 'quan tri kinh doanh', 'tueba', 'qtkd'] },
  { id: 'kh', label: 'Trường Đại học Khoa học', short: 'ĐH Khoa học', code: 'TNUS', aliases: ['khoa học', 'khoa hoc', 'tnus'] },
  { id: 'cntt', label: 'Trường Đại học Công nghệ Thông tin và Truyền thông', short: 'ĐH CNTT & TT', code: 'ICTU', aliases: ['công nghệ thông tin', 'cong nghe thong tin', 'truyền thông', 'truyen thong', 'ictu', 'cntt'] },
  { id: 'nn', label: 'Trường Ngoại ngữ', short: 'Ngoại ngữ', code: 'SFL', aliases: ['ngoại ngữ', 'ngoai ngu', 'sfl'] },
  { id: 'kqt', label: 'Khoa Quốc tế', short: 'Khoa Quốc tế', code: 'IS', aliases: ['khoa quốc tế', 'khoa quoc te'] },
  { id: 'ktct', label: 'Trường Đại học Kinh tế - Công nghệ Thái Nguyên', short: 'ĐH KT-CN', code: 'TUETECH', aliases: ['kinh tế - công nghệ', 'kinh te - cong nghe', 'kinh tế công nghệ', 'kinh te cong nghe', 'tuetech'] },
] as const;

export const DISTANCES = [
  { id: '500', label: 'Trong 500 m', meters: 500 },
  { id: '1000', label: 'Trong 1 km', meters: 1000 },
  { id: '2000', label: 'Trong 2 km', meters: 2000 },
  { id: '5000', label: 'Trong 5 km', meters: 5000 },
] as const;

function fold(raw: string): string {
  return raw
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/đ/g, 'd');
}

export function schoolOf(item: LocalListing): string {
  const blob = fold(`${item.title} ${item.summary ?? ''} ${item.sourceUrl ?? ''} ${item.sourceName ?? ''}`);
  if (blob.includes('1zav9iswr')) return 'kh';
  const order = ['yd', 'nl', 'ktcn', 'ktqtkd', 'ktct', 'kh', 'cntt', 'nn', 'kqt', 'sp'] as const;
  for (const id of order) {
    const row = SCHOOLS.find((s) => s.id === id);
    if (row?.aliases.some((a) => blob.includes(fold(a)))) return id;
  }
  return '';
}

export function wardOf(item: LocalListing): string {
  const place = fold(`${item.placeText ?? ''} ${item.title} ${item.summary ?? ''}`);
  for (const w of WARDS) {
    if (place.includes(fold(w.label)) || w.aliases.some((a) => place.includes(fold(a)))) return w.id;
  }
  return '';
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

export function wardsIn(_items: LocalListing[]): string[] {
  return WARDS.map((w) => w.id);
}

export function schoolLabel(id?: string | null): string {
  const key = id === 'ictu' ? 'cntt' : id;
  const row = SCHOOLS.find((s) => s.id === key);
  return row ? `${row.short} (${row.code})` : '';
}

export function wardLabel(id?: string | null): string {
  return WARDS.find((w) => w.id === id)?.label ?? '';
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
  const schoolId = opts.school === 'ictu' ? 'cntt' : opts.school;
  return items.filter((item) => {
    if (schoolId && schoolOf(item) !== schoolId) return false;
    if (opts.ward && wardOf(item) !== opts.ward) return false;
    if (maxM != null) {
      const m = distanceMeters(item);
      if (m == null || m > maxM) return false;
    }
    return true;
  });
}
