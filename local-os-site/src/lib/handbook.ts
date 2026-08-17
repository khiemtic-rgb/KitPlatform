import { cleanPlace, formatPhone, listingPhone, type LocalListing } from './api';
import type { DiscoverGuide } from './discover';
import { GUIDES } from './discover';
import { isIntern, isPartTime, isStudentJob, jobPay } from './job-filters';
import { SCHOOLS, schoolOf } from './room-filters';

export const HANDBOOK_CATS = [
  { id: '', label: 'Tất cả' },
  { id: 'sinh-vien', label: 'Sinh viên' },
  { id: 'viec', label: 'Việc làm' },
  { id: 'nha', label: 'Nhà ở' },
  { id: 'an', label: 'Ăn uống' },
  { id: 'du-lich', label: 'Du lịch' },
  { id: 'doi-song', label: 'Đời sống' },
] as const;

export type HandbookCat = (typeof HANDBOOK_CATS)[number]['id'];

export function handbookCatOf(raw?: string | null): HandbookCat {
  const id = (raw ?? '').trim();
  return HANDBOOK_CATS.some((c) => c.id === id) ? (id as HandbookCat) : '';
}

export function guideHandbookCats(g: DiscoverGuide): string[] {
  const cats = new Set<string>();
  if (g.slug === 'sinh-vien-moi') {
    cats.add('sinh-vien');
    cats.add('viec');
    cats.add('nha');
  }
  if (g.pillars.includes('am-thuc')) cats.add('an');
  if (g.pillars.includes('du-lich') || g.pillars.includes('dia-diem')) cats.add('du-lich');
  if (g.pillars.includes('van-hoa')) cats.add('doi-song');
  return [...cats];
}

export function handbookGuides(cat: HandbookCat, bucket: 'job' | 'room' | 'life'): DiscoverGuide[] {
  return GUIDES.filter((g) => {
    const cats = guideHandbookCats(g);
    if (bucket === 'job') return g.slug === 'sinh-vien-moi' && (!cat || cats.includes(cat) || cat === 'viec' || cat === 'sinh-vien');
    if (bucket === 'room') return g.slug === 'sinh-vien-moi' && (!cat || cats.includes(cat) || cat === 'nha' || cat === 'sinh-vien');
    if (cat && !cats.includes(cat) && cat !== '') return false;
    if (g.slug === 'sinh-vien-moi' && cat === '') return false;
    return true;
  });
}

export function handbookJobs(jobs: LocalListing[], cat: HandbookCat): LocalListing[] {
  if (cat === 'sinh-vien') return jobs.filter((j) => isPartTime(j) || isStudentJob(j) || isIntern(j));
  return jobs;
}

export function handbookRooms(rooms: LocalListing[], cat: HandbookCat): LocalListing[] {
  if (cat !== 'sinh-vien') return rooms;
  return rooms.filter((r) => {
    if (schoolOf({ ...r, summary: `${r.summary ?? ''} ${r.placeText ?? ''}` })) return true;
    return /trường|đại học|cao đẳng|ictu|sinh viên/i.test(`${r.title} ${r.summary ?? ''} ${r.placeText ?? ''}`);
  });
}

export function showHandbookSection(
  cat: HandbookCat,
  section: 'event' | 'job' | 'room' | 'life',
): boolean {
  if (!cat) return true;
  if (section === 'event') return cat === 'du-lich' || cat === 'doi-song' || cat === 'sinh-vien';
  if (section === 'job') return cat === 'viec' || cat === 'sinh-vien';
  if (section === 'room') return cat === 'nha' || cat === 'sinh-vien';
  return cat === 'an' || cat === 'du-lich' || cat === 'doi-song' || cat === 'sinh-vien';
}

function clipLead(s: string): string {
  const t = s.replace(/\s+/g, ' ').replace(/\.\s*\./g, '.').trim();
  return t.length > 150 ? `${t.slice(0, 147).trimEnd()}…` : t;
}

export function handbookPlaceLine(item: LocalListing): string {
  const raw = cleanPlace(item.placeText);
  const first = raw.split(/\s*[·|•]\s*|,\s*tiện ích|\s+-\s+tiện ích/i)[0]?.trim() ?? '';
  return first.length > 48 ? `${first.slice(0, 46).trimEnd()}…` : first || 'Thái Nguyên';
}

/** Số điện thoại / Lương / Giá thuê — đã gắn nhãn. Việc/phòng không số đã bị lọc ở listListings. */
export function handbookHits(item: LocalListing): { pay: string; phone: string } {
  const raw = listingPhone(item);
  const digits = raw.replace(/\D/g, '');
  const phone = digits.length >= 9 ? `Số điện thoại: ${formatPhone(raw)}` : '';
  if (item.kind === 'job') return { pay: `Lương: ${jobPay(item)}`, phone };
  return { pay: 'Giá thuê: liên hệ', phone };
}

/** Dòng tin — không nhắc «đã duyệt», không chép lương/SĐT. */
export function handbookNewsLead(item: LocalListing): string {
  const place = handbookPlaceLine(item);
  const org = (item.organizationName ?? '').replace(/\s+/g, ' ').trim();
  const title = (item.title ?? '').trim();

  if (item.kind === 'job') {
    const who = org && !title.toLowerCase().includes(org.toLowerCase()) ? org : '';
    const head = who ? `${who} đang tuyển` : 'Đang tuyển';
    const extra = isIntern(item)
      ? ' Thực tập.'
      : isPartTime(item) || isStudentJob(item)
        ? ' Phù hợp làm thêm / sinh viên.'
        : item.workingTime
          ? ` ${item.workingTime.replace(/^thời gian\s*:\s*/i, '').trim()}.`
          : '';
    return clipLead(`${head} tại ${place}.${extra}`);
  }

  if (item.kind === 'room') {
    const blob = `${item.summary ?? ''} ${title}`;
    const amen: string[] = [];
    if (/khép kín|khep kin/i.test(blob)) amen.push('khép kín');
    if (/nóng lạnh|nong lanh/i.test(blob)) amen.push('nóng lạnh');
    const schoolId = schoolOf({ ...item, summary: `${item.summary ?? ''} ${item.placeText ?? ''}` });
    const school = SCHOOLS.find((s) => s.id === schoolId)?.short;
    const mid = [amen.join(', '), school ? `gần ${school}` : ''].filter(Boolean).join(', ');
    return clipLead(mid ? `${place}, ${mid}.` : `${place}.`);
  }

  return clipLead(place);
}

export function handbookEventDate(iso?: string | null): { day: string; mon: string } {
  if (!iso) return { day: '—', mon: 'SẮP TỚI' };
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return { day: '—', mon: 'SẮP TỚI' };
  return {
    day: String(d.getDate()).padStart(2, '0'),
    mon: `THÁNG ${d.getMonth() + 1}`,
  };
}

export function handbookEventTone(catId: string): string {
  if (catId === 'sport') return 'sport';
  if (catId === 'fair') return 'fair';
  if (catId === 'music') return 'music';
  if (catId === 'tourism') return 'travel';
  if (catId === 'workshop' || catId === 'conference') return 'talk';
  return 'fest';
}
