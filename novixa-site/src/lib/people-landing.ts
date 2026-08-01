/** Copy & data — chuyên trang Dành cho người dân */

export const peopleLanding = {
  meta: {
    title: 'Dành cho người dân',
    description:
      'Ứng dụng Novixa đồng hành sức khỏe mỗi ngày — hồ sơ gia đình, nhắc uống thuốc, đặt thuốc nhanh và tư vấn dược sĩ.',
  },

  hero: {
    eyebrow: 'Ứng dụng sức khỏe cho bạn và gia đình',
    titleLine1: 'Sức khỏe của bạn,',
    titleLine2: 'Novixa đồng hành mỗi ngày',
    lead:
      'Quản lý hồ sơ sức khỏe, nhắc uống thuốc, đặt thuốc nhanh tại nhà thuốc tin cậy và chăm sóc sức khỏe cho cả gia đình – tất cả trong một ứng dụng.',
    primary: 'Tải ứng dụng miễn phí',
    primaryHref: '#pd-cta',
    secondary: 'Xem video 2 phút',
    secondaryHref: '#pd-features',
    storesLabel: 'Có trên',
    floats: [
      { label: 'Nhắc uống thuốc đúng giờ', tone: 'orange' },
      { label: 'Đặt thuốc nhanh chóng', tone: 'green' },
      { label: 'Tư vấn với dược sĩ', tone: 'blue' },
      { label: 'Theo dõi sức khỏe cả gia đình', tone: 'rose' },
    ],
    phoneHome: {
      greeting: 'Xin chào, Minh 👋',
      banner: 'Sức khỏe của bạn được cập nhật hôm nay',
      actions: ['Nhắc uống thuốc', 'Đặt thuốc', 'Hồ sơ sức khỏe', 'Tư vấn dược sĩ'],
      scriptsTitle: 'Đơn thuốc của bạn',
      scripts: [
        { name: 'Paracetamol 500mg', meta: 'Uống 1 viên — Còn 2 ngày', time: 'Sáng 08:00', tone: 'blue' },
        { name: 'Vitamin-C 1000mg', meta: 'Uống 1 viên — Còn 5 ngày', time: 'Sáng 08:00', tone: 'orange' },
      ],
    },
    phoneRemind: {
      title: 'Nhắc uống thuốc',
      name: 'Paracetamol 500mg',
      instruction: 'Uống 1 viên sau ăn sáng',
      time: '08:00',
      status: 'Đã đến giờ uống thuốc',
      cta: 'Tôi đã uống',
      snooze: 'Nhắc lại sau 10 phút',
    },
  },

  why: {
    title: 'Vì sao bạn nên sử dụng Novixa?',
    items: [
      {
        title: 'Quản lý sức khỏe dễ dàng',
        desc: 'Lưu hồ sơ, đơn thuốc và lịch sử điều trị của cả gia đình trên một ứng dụng.',
        tone: 'green',
      },
      {
        title: 'Nhắc uống thuốc thông minh',
        desc: 'Nhắc đúng giờ, đúng liều — giảm quên thuốc, tuân thủ liệu trình tốt hơn.',
        tone: 'blue',
      },
      {
        title: 'Đặt thuốc nhanh chóng',
        desc: 'Đặt lại đơn quen thuộc hoặc gửi đơn cho nhà thuốc chỉ trong vài thao tác.',
        tone: 'violet',
      },
      {
        title: 'Tư vấn với dược sĩ',
        desc: 'Kết nối nhà thuốc quen thuộc để hỏi đáp nhanh khi cần hỗ trợ.',
        tone: 'orange',
      },
      {
        title: 'Chăm sóc cả gia đình',
        desc: 'Theo dõi sức khỏe người thân — trẻ nhỏ, ông bà — ngay trên điện thoại của bạn.',
        tone: 'cyan',
      },
      {
        title: 'An toàn & bảo mật',
        desc: 'Thông tin sức khỏe được bảo vệ bằng xác thực và mã hóa dữ liệu.',
        tone: 'rose',
      },
    ],
  },

  journey: {
    title: 'Novixa đồng hành cùng bạn mỗi ngày',
    steps: [
      'Lưu hồ sơ sức khỏe',
      'Nhắc uống đúng giờ',
      'Theo dõi tình trạng',
      'Tư vấn dược sĩ',
      'Đặt thuốc nhanh',
      'Sức khỏe tốt hơn',
    ],
  },

  features: {
    id: 'pd-features',
    title: 'Tính năng nổi bật',
    items: [
      {
        title: 'Hồ sơ sức khỏe điện tử',
        desc: 'Lưu đơn thuốc, tiền sử bệnh và chỉ số sức khỏe khoa học.',
      },
      {
        title: 'Nhắc uống thuốc thông minh',
        desc: 'Nhắc liều, nhắc liệu trình và ghi nhận đã uống.',
      },
      {
        title: 'Quản lý gia đình',
        desc: 'Theo dõi nhiều thành viên trên cùng một tài khoản.',
      },
      {
        title: 'Đặt thuốc & mua lại',
        desc: 'Đặt nhanh từ lịch sử đơn hoặc gửi đơn cho nhà thuốc.',
      },
      {
        title: 'Tư vấn nhà thuốc',
        desc: 'Chat nhanh với dược sĩ khi cần hỗ trợ sử dụng thuốc.',
      },
      {
        title: 'Ưu đãi & tích điểm',
        desc: 'Nhận ưu đãi từ nhà thuốc liên kết và tích điểm dễ dàng.',
      },
    ],
    imageLeft: '/images/hero/home-devices.png',
    imageRight: '/images/audience/people.png',
    imageLeftAlt: 'Màn hình hồ sơ sức khỏe trên app Novixa',
    imageRightAlt: 'Người dùng chăm sóc sức khỏe với Novixa',
  },

  social: {
    title: 'Người dùng chọn Novixa đồng hành sức khỏe',
    quotes: [
      {
        quote: 'Nhắc uống thuốc giúp mình không còn quên liều khi bận việc.',
        name: 'Lan Anh',
        role: 'Nhân viên văn phòng',
      },
      {
        quote: 'Quản lý thuốc cho cả nhà trên một app — rất tiện với gia đình có trẻ nhỏ.',
        name: 'Minh Tuấn',
        role: 'Phụ huynh',
      },
      {
        quote: 'Đặt lại đơn quen thuộc chỉ vài bước, khỏi phải gọi điện nhà thuốc.',
        name: 'Thu Hà',
        role: 'Người chăm sóc gia đình',
      },
    ],
    stats: [
      { value: 'Miễn phí', label: 'Tải & sử dụng cơ bản' },
      { value: 'Gia đình', label: 'Quản lý nhiều thành viên' },
      { value: '24/7', label: 'Nhắc thuốc đồng hành' },
    ],
  },

  cta: {
    id: 'pd-cta',
    title: 'Tải Novixa ngay hôm nay để chăm sóc sức khỏe tốt hơn!',
    lead: 'Miễn phí — dễ dùng — đồng hành cùng bạn và gia đình mỗi ngày.',
    qrLabel: 'Quét QR để tải ứng dụng',
    qrHref: 'https://app.novixa.vn',
    image: '/images/audience/people.png',
    imageAlt: 'Gia đình sử dụng ứng dụng chăm sóc sức khỏe Novixa',
    stores: [
      { label: 'App Store', href: 'https://app.novixa.vn' },
      { label: 'Google Play', href: 'https://app.novixa.vn' },
    ],
  },
} as const;
