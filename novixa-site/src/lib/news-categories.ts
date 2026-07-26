/** Nhóm bài viết — schema CMS + listing. */
export const NEWS_CATEGORIES = [
  {
    id: 'quan-tri-nha-thuoc',
    label: 'Quản trị nhà thuốc',
    description: 'Quản lý, KPI, chiến lược và phát triển nhà thuốc.',
    navGroup: 'knowledge' as const,
  },
  {
    id: 'van-hanh',
    label: 'Vận hành nhà thuốc',
    description: 'Quy trình, tồn kho, FEFO, SOP và vận hành hàng ngày.',
    navGroup: 'knowledge' as const,
  },
  {
    id: 'novixa-academy',
    label: 'Novixa Academy',
    description: 'Đào tạo nhân sự, lộ trình năng lực và Academy.',
    navGroup: 'knowledge' as const,
  },
  {
    id: 'ban-hang-cskh',
    label: 'Bán hàng & Chăm sóc khách hàng',
    description: 'POS, tư vấn, CRM và chăm sóc khách hàng tại quầy.',
    navGroup: 'knowledge' as const,
  },
  {
    id: 'ai-trong-nha-thuoc',
    label: 'AI trong nhà thuốc',
    description: 'Ứng dụng AI hỗ trợ vận hành và tư vấn.',
    navGroup: 'knowledge' as const,
  },
  {
    id: 'connect',
    label: 'Chăm sóc khách hàng (Connect)',
    description: 'Kết nối nhà thuốc – phòng khám – bệnh nhân trên Novixa Connect.',
    navGroup: 'knowledge' as const,
  },
  {
    id: 'cau-chuyen-khach-hang',
    label: 'Câu chuyện khách hàng',
    description: 'Câu chuyện triển khai và kết quả từ nhà thuốc thực tế.',
    navGroup: 'knowledge' as const,
  },
  {
    id: 'tin-tuc-novixa',
    label: 'Tin tức Novixa',
    description: 'Cập nhật sản phẩm, lộ trình và tin tức từ Novixa.',
    navGroup: 'company' as const,
  },
] as const;

/** Menu Kiến thức nhà thuốc (không gồm Tin tức Novixa). */
export const KNOWLEDGE_CATEGORIES = NEWS_CATEGORIES.filter((c) => c.navGroup === 'knowledge');

export type NewsCategoryId = (typeof NEWS_CATEGORIES)[number]['id'];

export const DEFAULT_NEWS_CATEGORY: NewsCategoryId = 'tin-tuc-novixa';
export const DEFAULT_KNOWLEDGE_CATEGORY: NewsCategoryId = 'quan-tri-nha-thuoc';

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

/**
 * Phân mục con của Quản trị nhà thuốc — chỉ dùng trong CMS để phân loại.
 * Trên novixa.vn vẫn hiển thị theo mục cha `quan-tri-nha-thuoc`.
 */
export const QUAN_TRI_SUBCATEGORIES = [
  { id: 'chien-luoc-phat-trien', label: 'Chiến lược phát triển' },
  { id: 'kpi', label: 'KPI' },
  { id: 'doanh-thu', label: 'Doanh thu' },
  { id: 'loi-nhuan', label: 'Lợi nhuận' },
  { id: 'dong-tien', label: 'Dòng tiền' },
  { id: 'nhan-su', label: 'Nhân sự' },
  { id: 'mo-chuoi', label: 'Mở chuỗi' },
  { id: 'chuyen-doi-so', label: 'Chuyển đổi số' },
] as const;

export type QuanTriSubcategoryId = (typeof QUAN_TRI_SUBCATEGORIES)[number]['id'];

export const QUAN_TRI_SUBCATEGORY_IDS = QUAN_TRI_SUBCATEGORIES.map((c) => c.id) as [
  QuanTriSubcategoryId,
  ...QuanTriSubcategoryId[],
];

export function getQuanTriSubcategory(id: string | undefined | null) {
  return QUAN_TRI_SUBCATEGORIES.find((c) => c.id === id) ?? null;
}
