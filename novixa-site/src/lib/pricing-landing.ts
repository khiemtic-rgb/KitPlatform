/** Copy & data — chuyên trang Giải pháp và Chi phí */

export const pricingLanding = {
  meta: {
    title: 'Giải pháp và Chi phí',
    description:
      'Chi phí Novixa phù hợp quy mô nhà thuốc — tư vấn giải pháp, triển khai nhanh và đồng hành dài hạn. Nhận báo giá chi tiết sau buổi demo.',
  },

  hero: {
    title: 'Chi phí phù hợp với quy mô nhà thuốc',
    lead:
      'Novixa không áp một mức giá chung cho mọi nhà thuốc. Chúng tôi tư vấn giải pháp theo quy mô, nhu cầu vận hành và lộ trình phát triển của bạn.',
    checks: ['Tư vấn giải pháp phù hợp', 'Triển khai nhanh', 'Đồng hành dài hạn'],
    primary: 'Đăng ký demo miễn phí',
    primaryHref: '/vi/lien-he/',
    secondary: 'Nhận tư vấn báo giá',
    secondaryHref: '/vi/lien-he/',
    image: '/images/hero/dashboard-desktop.png',
    imageAlt: 'Dashboard Novixa trên máy tính và ứng dụng trên điện thoại',
  },

  tiers: {
    title: 'Giải pháp dành cho mọi quy mô nhà thuốc',
    items: [
      {
        id: 'doc-lap',
        tone: 'green',
        name: 'Nhà thuốc độc lập',
        scale: '1 cơ sở · dưới 5 nhân sự',
        desc: 'Vận hành gọn: bán hàng, kho, khách hàng và báo cáo cơ bản trên một nền tảng.',
        features: [
          'POS bán hàng tại quầy',
          'Quản lý kho & lô hạn',
          'CRM khách hàng cơ bản',
          'Báo cáo doanh thu hàng ngày',
          'Hỗ trợ AI cơ bản',
        ],
      },
      {
        id: 'chuoi-nho',
        tone: 'blue',
        name: 'Chuỗi nhỏ',
        scale: '2–10 cơ sở',
        desc: 'Quản lý đa điểm, đồng bộ dữ liệu và theo dõi hiệu quả từng cửa hàng.',
        features: [
          'Tất cả tính năng gói độc lập',
          'Dashboard đa chi nhánh',
          'Phân quyền theo vai trò',
          'Báo cáo so sánh cửa hàng',
          'Tích hợp API mở rộng',
        ],
      },
      {
        id: 'enterprise',
        tone: 'violet',
        name: 'Chuỗi lớn & Enterprise',
        scale: 'Trên 10 cơ sở',
        desc: 'Tùy biến sâu, bảo mật nâng cao và đồng hành bởi Account Manager riêng.',
        features: [
          'Tất cả tính năng chuỗi nhỏ',
          'Tùy biến quy trình & báo cáo',
          'Bảo mật & tuân thủ nâng cao',
          'Account Manager riêng',
          'Hỗ trợ triển khai ưu tiên',
        ],
      },
    ],
    quoteLabel: 'Liên hệ nhận báo giá chi tiết',
    quoteHref: '/vi/lien-he/',
    cta: 'Nhận tư vấn & demo',
    ctaHref: '/vi/lien-he/',
    note: 'Mức chi phí được báo rõ sau khi hiểu nhu cầu và quy mô nhà thuốc — không công bố giá cố định trên website.',
  },

  includes: {
    title: 'Mỗi gói giải pháp đều bao gồm',
    items: [
      { title: 'Cài đặt miễn phí', desc: 'Hỗ trợ thiết lập ban đầu trên hệ thống của bạn.' },
      { title: 'Chuyển dữ liệu', desc: 'Hỗ trợ nhập liệu / chuyển đổi dữ liệu cần thiết.' },
      { title: 'Đào tạo sử dụng', desc: 'Hướng dẫn đội ngũ vận hành nhanh, dễ nắm.' },
      { title: 'Hỗ trợ vận hành', desc: 'Đồng hành khi gặp vướng mắc trong quá trình dùng.' },
      { title: 'Cập nhật miễn phí', desc: 'Nhận cải tiến sản phẩm trong thời gian sử dụng.' },
      { title: 'Bảo mật & an toàn', desc: 'Xác thực, phân quyền và bảo vệ dữ liệu nhà thuốc.' },
      { title: 'Sao lưu dữ liệu', desc: 'Sao lưu định kỳ giúp giảm rủi ro mất dữ liệu.' },
      { title: 'Đa thiết bị', desc: 'Truy cập trên máy tính bảng, laptop và điện thoại.' },
    ],
  },

  roadmap: {
    title: 'Lộ trình nâng cấp theo sự phát triển của nhà thuốc',
    steps: [
      { title: 'Giai đoạn khởi đầu', desc: 'Quản lý bán hàng – kho – tiết kiệm thời gian vận hành.' },
      { title: 'Giai đoạn tăng trưởng', desc: 'Giữ chân khách hàng, nhắc thuốc và chăm sóc tái mua.' },
      { title: 'Giai đoạn phát triển', desc: 'Tối ưu doanh thu, mở rộng chi nhánh bằng dữ liệu.' },
      { title: 'Giai đoạn dẫn đầu', desc: 'Ứng dụng công nghệ toàn diện trên chuỗi nhà thuốc.' },
    ],
  },

  social: {
    title: 'Nhà thuốc đồng hành cùng Novixa',
    quotes: [
      {
        quote: 'Triển khai gọn, đội ngũ nắm POS và kho nhanh — báo cáo sáng rõ hơn trước.',
        name: 'Anh Minh',
        role: 'Chủ nhà thuốc độc lập',
      },
      {
        quote: 'Theo dõi được nhiều cửa hàng trên một dashboard, phân quyền nhân sự rõ ràng.',
        name: 'Chị Hương',
        role: 'Quản lý chuỗi nhỏ',
      },
      {
        quote: 'Buổi demo giúp chọn đúng phạm vi triển khai, không bị ép gói không cần thiết.',
        name: 'Anh Tuấn',
        role: 'Chủ chuỗi nhà thuốc',
      },
    ],
  },

  tech: {
    title: 'Nền tảng & công nghệ',
    items: ['Cloud doanh nghiệp', 'Bảo mật tầng ứng dụng', 'API mở rộng', 'Đồng bộ đa thiết bị'],
  },

  cta: {
    title: 'Sẵn sàng phát triển nhà thuốc của bạn?',
    lead: 'Đăng ký demo để nhận tư vấn giải pháp và báo giá phù hợp quy mô.',
    primary: 'Đăng ký demo miễn phí',
    primaryHref: '/vi/lien-he/',
    secondary: 'Liên hệ tư vấn',
    secondaryHref: '/vi/lien-he/',
    trusts: ['Demo miễn phí', 'Tư vấn 1:1', 'Giải pháp phù hợp', 'Triển khai nhanh'],
    image: '/images/audience/pharmacy.png',
    imageAlt: 'Nhà thuốc hiện đại vận hành cùng Novixa',
  },
} as const;
