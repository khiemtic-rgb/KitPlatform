/** Copy & data — chuyên trang Dành cho nhà thuốc (mockup v2) */

export const pharmacyLanding = {
  meta: {
    title: 'Dành cho nhà thuốc',
    description:
      'Tăng doanh thu từ khách hàng hiện có với Novixa — CRM chủ động, nhắc thuốc, loyalty và báo cáo trên một nền tảng.',
  },

  hero: {
    eyebrow: 'Giải pháp cho nhà thuốc',
    title: 'Tăng doanh thu từ khách hàng hiện có',
    lead:
      'Novixa giúp nhà thuốc chăm sóc khách hàng chủ động sau mỗi lần mua — tăng tỷ lệ quay lại và phát triển doanh thu bền vững.',
    primary: 'Đăng ký demo miễn phí',
    primaryHref: '/vi/lien-he/',
    secondary: 'Xem video 2 phút',
    secondaryHref: '#ph-video',
    trusts: ['Không cần thẻ tín dụng', 'Dễ dùng, triển khai nhanh', 'Hỗ trợ tận tâm 24/7'],
    image: '/images/hero/dashboard-desktop.png',
    imageAlt: 'Dashboard Novixa trên laptop và app nhắc thuốc trên điện thoại',
  },

  compare: {
    title: 'Bạn có thể tăng bao nhiêu doanh thu từ chính khách hàng hiện có?',
    before: {
      label: 'Hiện tại',
      customers: '100 khách hàng',
      returnLabel: '20 khách quay lại',
      metric: '−40%',
      metricDesc: 'Doanh thu bị bỏ lỡ',
    },
    after: {
      label: 'Với Novixa',
      customers: '100 khách hàng',
      returnLabel: '45 khách quay lại',
      metric: '+125%',
      metricDesc: 'Doanh thu tăng thêm',
    },
    sideTitle: 'Novixa giúp bạn biến khách một lần thành khách hàng trung thành',
    bullets: [
      'Nhắc uống thuốc & nhắc mua lại đúng thời điểm',
      'Theo dõi hành vi, phát hiện khách sắp hết thuốc',
      'Chăm sóc chủ động thay vì chờ khách tự quay lại',
      'Tăng tỷ lệ tái mua từ chính tệp khách hiện có',
    ],
  },

  journey: {
    title: 'Hành trình giữ khách và tăng doanh thu cùng Novixa',
    steps: [
      'Khách mua thuốc tại nhà thuốc',
      'Lưu thông tin & lịch sử mua hàng',
      'Nhắc uống thuốc, theo dõi sức khỏe',
      'Nhắc mua lại đúng thời điểm',
      'Khách đặt thuốc (online hoặc tại quầy)',
      'Khách quay lại — Doanh thu tăng',
    ],
  },

  features: {
    title: 'Novixa giúp nhà thuốc làm được nhiều hơn',
    items: [
      {
        title: 'CRM chủ động',
        desc: 'Danh sách khách cần chăm sóc, đúng người — đúng lúc.',
        tone: 'violet',
        preview: 'customers',
      },
      {
        title: 'Nhắc uống & nhắc mua',
        desc: 'Thông báo tự động giúp khách tuân thủ liệu trình và tái mua.',
        tone: 'blue',
        preview: 'notify',
      },
      {
        title: 'Marketing & Loyalty',
        desc: 'Tích điểm, đổi quà, giữ chân khách hàng trung thành.',
        tone: 'orange',
        preview: 'loyalty',
      },
      {
        title: 'AI hỗ trợ bán hàng',
        desc: 'Gợi ý sản phẩm phù hợp ngay tại quầy.',
        tone: 'green',
        preview: 'ai',
      },
      {
        title: 'Báo cáo thông minh',
        desc: 'Doanh thu, tái mua và hiệu suất theo thời gian thực.',
        tone: 'navy',
        preview: 'chart',
      },
    ],
  },

  why: {
    title: 'Vì sao nhà thuốc chọn Novixa?',
    items: [
      {
        title: 'Tập trung vào kết quả kinh doanh',
        desc: 'Ưu tiên khách quay lại và doanh thu thực tế, không chỉ quản lý tồn kho.',
      },
      {
        title: 'Dễ dùng, triển khai nhanh',
        desc: 'Giao diện thân thiện, có đội ngũ hỗ trợ đồng hành khi bắt đầu.',
      },
      {
        title: 'Bảo mật & an toàn',
        desc: 'Dữ liệu nhà thuốc và khách hàng được bảo vệ theo tiêu chuẩn cao.',
      },
      {
        title: 'Kết nối hệ sinh thái',
        desc: 'Liên kết người dân – nhà thuốc – phòng khám trên một nền tảng.',
      },
    ],
  },

  video: {
    id: 'ph-video',
    title: 'Trải nghiệm Novixa chỉ trong 2 phút',
    lead: 'Xem cách Novixa giúp nhà thuốc giữ chân khách hàng và tăng tái mua — ngắn gọn, dễ hiểu.',
    cta: 'Xem video demo',
    ctaHref: '/vi/lien-he/',
    image: '/images/hero/dashboard-desktop.png',
    imageAlt: 'Demo giao diện Novixa trên máy tính bảng và điện thoại',
  },

  cta: {
    title: 'Sẵn sàng tăng doanh thu từ khách hàng hiện có?',
    trusts: ['Demo miễn phí', 'Không phí cài đặt', 'Hỗ trợ triển khai 1:1', 'Đồng hành lâu dài'],
    primary: 'Đăng ký demo miễn phí',
    primaryHref: '/vi/lien-he/',
    secondary: 'Liên hệ tư vấn',
    secondaryHref: '/vi/lien-he/#contact-form',
  },
} as const;
