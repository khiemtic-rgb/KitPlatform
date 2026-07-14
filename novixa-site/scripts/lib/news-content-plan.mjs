/**
 * Lịch đăng tin tự động Novixa — mỗi ngày 1 bài từ hàng đợi A–H (100 tiêu đề).
 */
import { TITLE_GROUPS } from './news-queue-titles.mjs';

export const EDITORIAL_HUB = {
  name: 'Novixa Tin tức — Series A–H',
  /** Ngày bắt đầu đăng bài đầu tiên trong hàng đợi mới */
  startDate: '2026-07-15',
  publishPerDay: 1,
};

/** Bài đã có trong src/content/tin-tuc — không tạo lại */
export const PUBLISHED_SLUGS = new Set([
  'gioi-thieu-novixa',
  'pos-ban-hang',
  'quan-ly-lo-fefo',
  'lo-trinh-phat-trien-2026',
  'vi-sao-excel-khong-con-phu-hop-de-quan-ly-nha-thuoc',
  '7-sai-lam-khien-nha-thuoc-that-thoat-loi-nhuan-moi-thang',
  'quan-ly-nha-thuoc-thong-minh-tu-quay-ban-den-ton-kho-lo',
  'quan-ly-ton-kho-thuoc-hieu-qua-cho-nha-thuoc-hien-dai',
  'giam-that-thoat-tu-hang-can-date-va-het-han-su-dung',
  'fefo-la-gi-nguyen-tac-ban-het-han-truoc-cho-nha-thuoc',
  'quan-ly-nhieu-chi-nhanh-nha-thuoc-thach-thuc-va-giai-phap',
  'cach-giam-ton-kho-chet-trong-nha-thuoc',
  'chuyen-doi-so-cho-nha-thuoc-bat-dau-tu-dau',
  'cac-kpi-quan-trong-khi-quan-ly-nha-thuoc',
  'tuan-thu-gpp-trong-quan-ly-nha-thuoc',
  'canh-bao-hang-can-date-quy-trinh-5-buoc',
  'pos-nha-thuoc-5-tinh-nang-khong-the-thieu',
  'crm-cho-nha-thuoc-bat-dau-tu-dau-lieu-khach-hang',
  'bao-cao-doanh-thu-real-time-doc-so-lieu-dung',
  'mo-rong-chuoi-nha-thuoc-checklist-he-thong',
  'ai-ho-tro-van-hanh-nha-thuoc',
  'kiem-ke-ton-kho-dinh-ky-quy-trinh-chuan',
  'lo-trinh-novixa-q3-2026',
]);

function slugify(title) {
  return title
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'd')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 72);
}

function uniqueSlug(base, used) {
  let slug = base || 'bai-viet';
  if (!used.has(slug) && !PUBLISHED_SLUGS.has(slug)) {
    used.add(slug);
    return slug;
  }
  let n = 2;
  while (used.has(`${slug}-${n}`) || PUBLISHED_SLUGS.has(`${slug}-${n}`)) n += 1;
  const next = `${slug}-${n}`;
  used.add(next);
  return next;
}

function addDaysIso(startIso, dayOffset) {
  const date = new Date(`${startIso}T12:00:00Z`);
  date.setUTCDate(date.getUTCDate() + dayOffset);
  return date.toISOString().slice(0, 10);
}

function buildScheduledPool() {
  const used = new Set();
  /** @type {Array<{id:string,slug:string,title:string,topic:string,category:string,categoryLabel:string,targetWords:number}>} */
  const items = [];
  let globalIndex = 0;

  for (const group of TITLE_GROUPS) {
    group.titles.forEach((rawTitle, indexInGroup) => {
      const title = rawTitle.replace(/\s+/g, ' ').trim().replace(/\.$/, '');
      const slug = uniqueSlug(slugify(title), used);
      globalIndex += 1;
      items.push({
        id: `nv-${group.category.toLowerCase()}${String(indexInGroup + 1).padStart(2, '0')}`,
        slug,
        title,
        topic: `${group.categoryLabel} — góc nhìn thực tế cho chủ nhà thuốc Việt Nam; liên hệ Novixa khi phù hợp`,
        category: group.category,
        categoryLabel: group.categoryLabel,
        targetWords: 1100,
        _order: globalIndex - 1,
      });
    });
  }

  return items;
}

/** Mỗi ngày đúng 1 bài, liên tiếp từ startDate */
function assignSequentialDailyDates(items, startIso) {
  return items.map((item, index) => {
    const { _order, ...rest } = item;
    return {
      ...rest,
      publishDate: addDaysIso(startIso, index),
    };
  });
}

const SCHEDULED_POOL = buildScheduledPool();

export const EDITORIAL_PLAN = assignSequentialDailyDates(
  SCHEDULED_POOL,
  EDITORIAL_HUB.startDate,
);

EDITORIAL_HUB.endDate =
  EDITORIAL_PLAN.length > 0
    ? EDITORIAL_PLAN[EDITORIAL_PLAN.length - 1].publishDate
    : EDITORIAL_HUB.startDate;

export function getPlanById(id) {
  return EDITORIAL_PLAN.find((item) => item.id === id);
}

export function getPlanForDate(isoDate) {
  return EDITORIAL_PLAN.filter(
    (item) => item.publishDate === isoDate && !PUBLISHED_SLUGS.has(item.slug),
  );
}

export function vnTodayIso(fromDate = new Date()) {
  return fromDate.toLocaleDateString('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh' });
}

export function getUpcomingPlan(fromDate = new Date()) {
  const iso = typeof fromDate === 'string' ? fromDate : vnTodayIso(fromDate);
  return EDITORIAL_PLAN.filter(
    (item) => !PUBLISHED_SLUGS.has(item.slug) && item.publishDate >= iso,
  ).sort((a, b) => a.publishDate.localeCompare(b.publishDate));
}

export function getPlanStats() {
  const byCategory = Object.fromEntries(
    TITLE_GROUPS.map((g) => [g.category, { label: g.categoryLabel, count: g.titles.length }]),
  );
  return {
    total: EDITORIAL_PLAN.length,
    startDate: EDITORIAL_HUB.startDate,
    endDate: EDITORIAL_HUB.endDate,
    byCategory,
  };
}
