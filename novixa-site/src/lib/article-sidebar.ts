/**
 * Sidebar sản phẩm trên trang chi tiết bài viết.
 * Chỉnh copy / link tại đây — không cần sửa layout.
 */
export const articleSidebar = {
  cta: {
    title: 'Dùng thử Novixa',
    lead: 'Đăng ký demo để xem POS, kho lô, CRM và báo cáo trên một nền tảng.',
    primary: 'Đăng ký demo',
    primaryHref: '/vi/lien-he/#contact-form',
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
