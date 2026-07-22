/** Nhóm kiến thức nhà thuốc — dùng cho nav, listing, CMS. */
export const NEWS_CATEGORIES = [
  {
    id: 'quan-tri-nha-thuoc',
    label: 'Quản trị nhà thuốc',
    description: 'Quản lý, KPI, chiến lược và phát triển nhà thuốc.',
  },
  {
    id: 'van-hanh',
    label: 'Vận hành',
    description: 'Quy trình, tồn kho, FEFO, SOP và vận hành hàng ngày.',
  },
  {
    id: 'ban-hang-cskh',
    label: 'Bán hàng & CSKH',
    description: 'POS, tư vấn, CRM và chăm sóc khách hàng.',
  },
  {
    id: 'ai-trong-nha-thuoc',
    label: 'AI trong nhà thuốc',
    description: 'Ứng dụng AI hỗ trợ vận hành và tư vấn.',
  },
  {
    id: 'connect',
    label: 'Connect',
    description: 'Kết nối nhà thuốc – phòng khám – bệnh nhân.',
  },
  {
    id: 'novixa-academy',
    label: 'Novixa Academy',
    description: 'Đào tạo nhân sự, lộ trình năng lực và Academy.',
  },
  {
    id: 'cau-chuyen-khach-hang',
    label: 'Câu chuyện khách hàng',
    description: 'Câu chuyện triển khai và kết quả từ nhà thuốc thực tế.',
  },
  {
    id: 'tin-tuc-novixa',
    label: 'Tin tức Novixa',
    description: 'Cập nhật sản phẩm, lộ trình và tin tức từ Novixa.',
  },
] as const;

export type NewsCategoryId = (typeof NEWS_CATEGORIES)[number]['id'];

export const DEFAULT_NEWS_CATEGORY: NewsCategoryId = 'tin-tuc-novixa';

export const NEWS_CATEGORY_IDS = NEWS_CATEGORIES.map((c) => c.id) as [
  NewsCategoryId,
  ...NewsCategoryId[],
];

export function getNewsCategory(id: string | undefined | null) {
  const found = NEWS_CATEGORIES.find((c) => c.id === id);
  return found ?? NEWS_CATEGORIES.find((c) => c.id === DEFAULT_NEWS_CATEGORY)!;
}

export function newsCategoryPath(id: NewsCategoryId | string): string {
  return `/vi/kien-thuc/${id}/`;
}

export function isNewsCategoryId(value: string): value is NewsCategoryId {
  return NEWS_CATEGORY_IDS.includes(value as NewsCategoryId);
}
