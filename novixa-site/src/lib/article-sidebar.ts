/**
 * Sidebar sản phẩm trên trang chi tiết bài viết.
 * Chỉnh copy / link tại đây — không cần sửa layout.
 */

export const PHC_SURVEY_URL =
  'https://survey.novixa.vn/survey/21f67b68-f877-4f65-99a8-e32a6ea8de7d';

/** Bài ưu tiên CTA Health Check (diagnostic trước demo). */
const PHC_CTA_SLUGS = new Set([
  'nha-thuoc-van-ban-tot-nhung-ban-co-chac-dang-quan-ly-tot',
]);

export const articleSidebar = {
  cta: {
    title: 'Dùng thử Novixa',
    lead: 'Đăng ký demo để xem POS, kho lô, CRM và báo cáo trên một nền tảng.',
    primary: 'Đăng ký demo',
    primaryHref: '/vi/lien-he/#contact-form',
  },

  /** CTA chẩn đoán — dùng cho bài pain/PHC */
  ctaPhc: {
    title: 'Bán tốt chưa đủ',
    lead: 'Bạn có chắc quản lý đang ổn? 7 phút — biết đang hở tồn kho, khách quen hay quy trình ca.',
    primary: 'Kiểm tra nhà thuốc miễn phí',
    primaryHref: PHC_SURVEY_URL,
  },

  /** Sản phẩm / giải pháp — khách click chuyển ngay */
  products: {
    title: 'Giải pháp Novixa',
    items: [
      {
        title: 'Giải pháp & chi phí',
        desc: 'Gói theo quy mô nhà thuốc — nhận tư vấn báo giá.',
        href: '/vi/giai-phap-chi-phi/',
      },
      {
        title: 'Dành cho nhà thuốc',
        desc: 'Tăng khách quay lại, nhắc thuốc, loyalty.',
        href: '/vi/nha-thuoc/',
      },
      {
        title: 'Dành cho người dân',
        desc: 'Nhắc uống thuốc, đặt thuốc, tư vấn.',
        href: '/vi/nguoi-dan/',
      },
      {
        title: 'Kiến thức nhà thuốc',
        desc: 'Bài viết vận hành, quản trị, Academy.',
        href: '/vi/kien-thuc/',
      },
    ],
  },

  docs: {
    title: 'Tài liệu nhà thuốc',
    items: [
      { title: 'Tài liệu & biểu mẫu', href: '/vi/tai-lieu/' },
      { title: 'Văn bản pháp luật', href: '/vi/tai-lieu/van-ban-phap-luat/' },
      { title: 'Tất cả kiến thức', href: '/vi/kien-thuc/' },
    ],
  },

  recentTitle: 'Bài liên quan',
  recentCount: 5,
} as const;

export type ArticleSidebarCta = {
  title: string;
  lead: string;
  primary: string;
  primaryHref: string;
};

export function resolveArticleSidebarCta(slug: string): ArticleSidebarCta {
  if (PHC_CTA_SLUGS.has(slug)) {
    return articleSidebar.ctaPhc;
  }
  return articleSidebar.cta;
}
