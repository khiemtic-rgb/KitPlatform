import { formatEventLine, hrefFor, listListings, type LocalListing } from './api';
import { isIntern, isPartTime, isStudentJob, jobPay } from './job-filters';
import { SCHOOLS, schoolOf } from './room-filters';

export const WEEK_GUIDE = {
  slug: 'tuan-nay',
  href: '/kham-pha/cam-nang/tuan-nay',
  title: 'Tuần này ở Thái Nguyên có gì?',
  kicker: 'Cẩm nang',
} as const;

const WEEK_MS = 7 * 24 * 60 * 60 * 1000;
const LIMIT = 6;

export type DigestItem = {
  href: string;
  title: string;
  meta: string;
};

export type DigestSection = {
  id: 'event' | 'job' | 'room';
  title: string;
  empty: string;
  moreHref: string;
  moreLabel: string;
  items: DigestItem[];
  total: number;
};

export type WeekDigest = {
  asOf: string;
  blurb: string;
  events: DigestSection;
  jobs: DigestSection;
  rooms: DigestSection;
};

function startMs(item: LocalListing): number {
  const t = item.startAt ? new Date(item.startAt).getTime() : Number.NaN;
  return Number.isNaN(t) ? Number.POSITIVE_INFINITY : t;
}

function inNextWeek(iso?: string | null): boolean {
  if (!iso) return false;
  const t = new Date(iso).getTime();
  if (Number.isNaN(t)) return false;
  const now = Date.now();
  return t >= now - 12 * 60 * 60 * 1000 && t <= now + WEEK_MS;
}

function studentJob(item: LocalListing): boolean {
  return isPartTime(item) || isStudentJob(item) || isIntern(item);
}

function schoolLabel(item: LocalListing): string {
  const id = schoolOf({
    ...item,
    summary: `${item.summary ?? ''} ${item.placeText ?? ''}`,
  });
  return SCHOOLS.find((s) => s.id === id)?.short ?? '';
}

function nearSchool(item: LocalListing): boolean {
  if (schoolLabel(item)) return true;
  return /trường|đại học|cao đẳng|ictu|sinh viên/i.test(
    `${item.title} ${item.summary ?? ''} ${item.placeText ?? ''}`,
  );
}

function pick<T>(preferred: T[], rest: T[], limit: number): T[] {
  const out: T[] = [];
  for (const row of [...preferred, ...rest]) {
    if (out.includes(row)) continue;
    out.push(row);
    if (out.length >= limit) break;
  }
  return out;
}

export async function buildWeekDigest(): Promise<WeekDigest> {
  const [allEvents, allJobs, allRooms] = await Promise.all([
    listListings('event'),
    listListings('job'),
    listListings('room'),
  ]);

  const dated = allEvents.filter((e) => inNextWeek(e.startAt)).sort((a, b) => startMs(a) - startMs(b));
  const eventPool = dated.length ? dated : [...allEvents].sort((a, b) => startMs(a) - startMs(b));
  const eventItems = eventPool.slice(0, LIMIT);

  const studentJobs = allJobs.filter(studentJob);
  const jobPool = studentJobs.length >= 3 ? studentJobs : allJobs;
  const jobItems = pick(studentJobs, allJobs, LIMIT);

  const schoolRooms = allRooms.filter(nearSchool);
  const roomPool = schoolRooms.length >= 3 ? schoolRooms : allRooms;
  const roomItems = pick(schoolRooms, allRooms, LIMIT);

  const asOf = new Date().toISOString().slice(0, 10);
  const blurb = 'Cuối tuần đi đâu, đang tuyển gì, phòng nào gần trường.';

  return {
    asOf,
    blurb,
    events: {
      id: 'event',
      title: dated.length ? 'Sự kiện tuần này' : 'Sự kiện',
      empty: 'Tuần này chưa có sự kiện.',
      moreHref: '/su-kien',
      moreLabel: 'Xem tất cả sự kiện',
      total: eventPool.length,
      items: eventItems.map((item) => ({
        href: hrefFor(item),
        title: item.title,
        meta: [formatEventLine(item.startAt) || (item.startAt ? '' : 'Chưa rõ giờ — xem tin'), item.placeText]
          .filter(Boolean)
          .join(' · '),
      })),
    },
    jobs: {
      id: 'job',
      title: studentJobs.length >= 3 ? 'Việc làm thêm / sinh viên' : 'Việc đang tuyển',
      empty: 'Tuần này chưa có việc.',
      moreHref: studentJobs.length >= 3 ? '/viec?q=part-time' : '/viec',
      moreLabel: 'Xem tất cả việc làm',
      total: jobPool.length,
      items: jobItems.map((item) => {
        const pay = jobPay(item);
        return {
          href: hrefFor(item),
          title: item.title,
          meta: [item.placeText, `Lương: ${pay}`]
            .filter(Boolean)
            .join(' · '),
        };
      }),
    },
    rooms: {
      id: 'room',
      title: schoolRooms.length >= 3 ? 'Phòng gần trường' : 'Phòng đang cho thuê',
      empty: 'Tuần này chưa có phòng.',
      moreHref: schoolRooms.length >= 3 ? '/tro?q=trường' : '/tro',
      moreLabel: 'Xem tất cả phòng trọ',
      total: roomPool.length,
      items: roomItems.map((item) => ({
        href: hrefFor(item),
        title: item.title,
        meta: [item.placeText, schoolLabel(item), 'Giá thuê: liên hệ'].filter(Boolean).join(' · '),
      })),
    },
  };
}

export function digestCardSummary(digest: WeekDigest): string {
  const bits = [
    digest.events.total ? `${digest.events.total} sự kiện` : '',
    digest.jobs.total ? `${digest.jobs.total} việc` : '',
    digest.rooms.total ? `${digest.rooms.total} phòng` : '',
  ].filter(Boolean);
  if (!bits.length) return 'Chưa có tin tuần này.';
  return `${bits.join(' · ')} tuần này.`;
}
