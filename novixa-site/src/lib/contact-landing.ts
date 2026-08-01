/** Copy & data — trang Liên hệ theo mockup */

export const contactLanding = {
  meta: {
    title: 'Liên hệ',
    description:
      'Đăng ký tư vấn và demo Novixa miễn phí — đội ngũ đồng hành cùng nhà thuốc chọn giải pháp phù hợp quy mô.',
  },

  hero: {
    eyebrow: 'Liên hệ Novixa',
    titleBefore: 'Chúng tôi luôn sẵn sàng ',
    titleAccent: 'đồng hành',
    titleAfter: ' cùng nhà thuốc phát triển bền vững',
    lead:
      'Để lại thông tin — chuyên gia Novixa sẽ liên hệ sớm để tư vấn giải pháp và sắp xếp buổi demo phù hợp với nhà thuốc của bạn.',
    highlights: [
      { title: 'Tư vấn giải pháp phù hợp', tone: 'teal' },
      { title: 'Demo thực tế 1:1', tone: 'green' },
      { title: 'Triển khai nhanh chóng', tone: 'teal' },
    ],
  },

  form: {
    id: 'contact-form',
    titleBefore: 'Đăng ký nhận tư vấn & ',
    titleAccent: 'Demo miễn phí',
    name: 'Họ và tên',
    phone: 'Số điện thoại',
    email: 'Email',
    role: 'Bạn là ai?',
    rolePlaceholder: 'Chọn vai trò',
    roleOptions: ['Chủ nhà thuốc', 'Quản lý chuỗi', 'Dược sĩ / nhân viên', 'Đối tác / khác'],
    scale: 'Quy mô nhà thuốc',
    scalePlaceholder: 'Chọn quy mô',
    scaleOptions: [
      'Nhà thuốc độc lập (1 cơ sở)',
      'Chuỗi nhỏ (2–10 cơ sở)',
      'Chuỗi lớn / Enterprise',
      'Đang tìm hiểu',
    ],
    needs: 'Nhu cầu của bạn',
    needsPlaceholder: 'Ví dụ: muốn demo POS + kho, hoặc tư vấn cho chuỗi…',
    consent: 'Tôi đồng ý để Novixa liên hệ tư vấn qua số điện thoại / email đã cung cấp.',
    submit: 'Gửi thông tin',
    note: 'Thông tin được gửi tới care@novixa.vn. Ghi “FOUNDING” trong nhu cầu nếu bạn quan tâm Early Access.',
  },

  info: {
    title: 'Thông tin liên hệ',
    items: [
      { label: 'Hotline', valueKey: 'phone' as const },
      { label: 'Email', valueKey: 'email' as const },
      { label: 'Giờ làm việc', value: 'Thứ 2 – Thứ 7 · 8:00 – 17:30' },
      { label: 'Hỗ trợ kỹ thuật', value: 'Qua hotline / Zalo trong giờ hành chính' },
    ],
  },

  office: {
    title: 'Văn phòng Novixa',
    directions: 'Chỉ đường',
  },

  why: {
    title: 'Vì sao nhà thuốc tin chọn Novixa?',
    items: [
      { title: 'Nền tảng ổn định', desc: 'Vận hành quầy – kho – khách hàng trên một hệ thống.' },
      { title: 'Hỗ trợ tận tâm', desc: 'Đồng hành khi triển khai và khi vận hành hàng ngày.' },
      { title: 'Triển khai dễ', desc: 'Đào tạo nhanh, đội ngũ nắm thao tác trong thời gian ngắn.' },
      { title: 'Cập nhật liên tục', desc: 'Nhận cải tiến sản phẩm theo nhu cầu thực tế nhà thuốc.' },
      { title: 'Đồng hành dài hạn', desc: 'Tư vấn theo quy mô — từ độc lập đến chuỗi.' },
    ],
  },

  faq: {
    id: 'faq',
    title: 'Câu hỏi thường gặp',
    items: [
      {
        q: 'Novixa phù hợp nhà thuốc quy mô nào?',
        a: 'Phù hợp nhà thuốc độc lập, chuỗi nhỏ đến chuỗi lớn. Trong buổi demo chúng tôi sẽ gợi ý phạm vi triển khai phù hợp — xem thêm tại trang Giải pháp & chi phí.',
      },
      {
        q: 'Thời gian triển khai mất bao lâu?',
        a: 'Thường từ vài ngày đến vài tuần tùy quy mô, dữ liệu cần chuyển đổi và số điểm bán. Lịch cụ thể được thống nhất sau buổi tư vấn.',
      },
      {
        q: 'Dữ liệu nhà thuốc có được bảo mật?',
        a: 'Có. Hệ thống dùng xác thực, phân quyền theo vai trò và sao lưu định kỳ. Lõi vận hành tách biệt website công khai.',
      },
      {
        q: 'Đang dùng phần mềm khác thì chuyển sang được không?',
        a: 'Được. Novixa hỗ trợ chuyển dữ liệu cần thiết và đào tạo đội ngũ — phạm vi migrate trao đổi rõ trước khi triển khai.',
      },
      {
        q: 'Chi phí sử dụng Novixa tính như thế nào?',
        a: 'Chi phí theo quy mô và phạm vi dùng (một cửa hay chuỗi). Báo giá chi tiết sau buổi demo.',
        linkHref: '/vi/giai-phap-chi-phi/',
        linkLabel: 'Xem Giải pháp & chi phí',
      },
    ],
  },
} as const;
