/** Copy & data for Novixa homepage landing (mockup). */

export const homeLanding = {
  hero: {
    titleBefore: 'Giúp nhà thuốc ',
    titleHighlight1: 'tăng khách hàng quay lại',
    titleMid: ' và chăm sóc sức khỏe cộng đồng ',
    titleHighlight2: 'bằng AI',
    lead:
      'Novixa là nền tảng toàn diện kết nối nhà thuốc – người dân – phòng khám, giúp tối ưu vận hành và gia tăng doanh thu bền vững.',
    primaryCta: 'Trải nghiệm ngay',
    primaryHref: 'https://app.novixa.vn',
    secondaryCta: 'Đăng ký demo',
    secondaryHref: '/vi/lien-he/',
    stats: [
      { value: 'POS · Kho · CRM', label: 'Vận hành trên một nền tảng' },
      { value: 'App khách hàng', label: 'Nhắc uống thuốc & tái mua' },
      { value: 'AI đồng hành', label: 'Hỗ trợ tư vấn & chăm sóc' },
    ],
  },

  problems: {
    title: 'Nhà thuốc đang gặp những vấn đề gì?',
    items: [
      { title: 'Khách mua một lần rồi thôi', desc: 'Khó giữ khách quay lại mua thuốc định kỳ.' },
      { title: 'Marketing kém hiệu quả', desc: 'Chi phí cao nhưng khó đo lường kết quả.' },
      { title: 'Hàng cận date / tồn ứ', desc: 'Thất thoát do không theo dõi FEFO tốt.' },
      { title: 'Nhân viên tư vấn chưa đồng đều', desc: 'Thiếu quy trình và công cụ hỗ trợ bán.' },
      { title: 'Chăm sóc sau bán thủ công', desc: 'Nhắc uống thuốc, tái mua phụ thuộc nhớ cá nhân.' },
      { title: 'Dữ liệu phân mảnh', desc: 'POS, kho, CRM và app khách không liền mạch.' },
    ],
  },

  solutions: {
    title: 'Novixa giải quyết như thế nào?',
    items: [
      { title: 'Giữ chân khách hàng', desc: 'CRM + loyalty theo hành trình mua thuốc.' },
      { title: 'Nhắc uống thuốc bằng AI', desc: 'Đúng giờ, đúng liệu trình, giảm bỏ điều trị.' },
      { title: 'Chăm sóc sau bán chuyên nghiệp', desc: 'Tái mua, theo dõi sức khỏe, CSKH chủ động.' },
      { title: 'Doanh thu bền vững', desc: 'Tăng khách quay lại và giá trị vòng đời.' },
    ],
  },

  lifecycle: {
    title: 'Một vòng đời khách hàng cùng Novixa',
    steps: [
      'Khám bệnh',
      'Kê đơn',
      'Mua tại nhà thuốc',
      'Chăm sóc Novixa',
      'Nhắc uống / theo dõi',
      'Mua lại dễ dàng',
    ],
  },

  people: {
    title: 'Người dân nhận được gì từ Novixa?',
    items: [
      'Hồ sơ sức khỏe cá nhân',
      'Lưu đơn thuốc & lịch sử mua',
      'Nhắc uống thuốc thông minh',
      'Tư vấn sức khỏe bằng AI',
      'Đặt mua thuốc / tái mua nhanh',
    ],
    cta: 'Tải app miễn phí',
    ctaHref: '/vi/giai-phap/app-khach-hang/',
  },

  pharmacy: {
    title: 'Nhà thuốc nhận được gì từ Novixa?',
    items: [
      'CRM thông minh & giữ chân khách',
      'Hỗ trợ bán hàng bằng AI',
      'Quản lý tồn kho FEFO',
      'KPI nhân sự & đào tạo',
      'Báo cáo trực quan theo thời gian thực',
    ],
    cta: 'Đăng ký demo ngay',
    ctaHref: '/vi/lien-he/',
  },

  aiChat: {
    title: 'AI Health đồng hành 24/7',
    greeting: 'Xin chào! Tôi là Novixa AI — bạn cần hỗ trợ gì hôm nay?',
    prompts: ['Triệu chứng thường gặp', 'Tác dụng phụ thuốc', 'Nhắc tái mua đơn thuốc'],
  },

  why: {
    title: 'Vì sao nhà thuốc chọn Novixa?',
    stats: [
      { value: 'CRM', label: 'Giữ chân khách quay lại' },
      { value: 'FEFO', label: 'Kiểm soát lô & hạn dùng' },
      { value: 'App', label: 'Chăm sóc sau bán tự động' },
      { value: 'AI', label: 'Hỗ trợ tư vấn tại quầy' },
      { value: 'Cloud', label: 'Triển khai nhanh, dữ liệu an toàn' },
    ],
  },

  ecosystem: {
    title: 'Hệ sinh thái Novixa',
    center: 'AI Healthcare Platform',
    nodes: ['Người dân', 'Nhà thuốc', 'Phòng khám'],
  },

  knowledge: {
    title: 'Kiến thức chuyên môn dành cho nhà thuốc',
    cta: 'Xem tất cả',
    href: '/vi/kien-thuc/',
  },

  cta: {
    title: 'Sẵn sàng cùng Novixa tăng trưởng cùng nhà thuốc của bạn?',
    subtitle: 'Đăng ký demo miễn phí và trải nghiệm ngay hôm nay!',
    primary: 'Đăng ký demo',
    primaryHref: '/vi/lien-he/',
    secondary: 'Liên hệ tư vấn',
    secondaryHref: '/vi/lien-he/#contact-form',
  },
} as const;
