/** Trang chủ: một việc cho chủ nhà thuốc. Giá chỉ nói ở trang chi phí. */

export const homeLanding = {
  hero: {
    badge: 'Dành cho chủ nhà thuốc',
    titleBefore: 'Bán đúng lô, ',
    titleHighlight1: 'nhắc khách quay lại',
    titleMid: ', chủ không phải ',
    titleHighlight2: 'đứng quầy cả ngày',
    lead:
      'Novixa gom bán hàng, tồn theo lô và chăm sóc khách cho nhà thuốc độc lập và chuỗi nhỏ.',
    primaryCta: 'Đăng ký demo',
    primaryHref: '/vi/lien-he/',
    trusts: ['Bán theo lô và hạn dùng', 'Có người hỗ trợ khi triển khai'],
    stats: [
      { value: 'Đúng lô', label: 'Bán theo hạn dùng' },
      { value: 'Khách quay lại', label: 'Nhắc tái mua' },
      { value: 'Báo cáo trong ngày', label: 'Doanh thu và tồn' },
      { value: 'Một nền', label: 'Quầy, kho, khách' },
    ],
  },

  problems: {
    eyebrow: 'Nhà thuốc đang gặp',
    titleBefore: 'Nhà thuốc đang gặp những ',
    titleHighlight: 'vấn đề gì?',
    titleAfter: '',
    lead: 'Ba việc này khiến chủ nhà thuốc mất khách, mất hàng và không rời được quầy.',
    items: [
      {
        title: 'Khách mua một lần rồi không quay lại',
        desc: 'Không biết khách nào sắp hết thuốc để nhắc mua lại.',
        tone: 'blue',
      },
      {
        title: 'Hàng cận hạn nằm trong kho',
        desc: 'Lô cũ không được bán trước, đến hạn phải hủy.',
        tone: 'orange',
      },
      {
        title: 'Chủ không rời được quầy',
        desc: 'Nhân viên không tra được việc, nên việc gì cũng gọi chủ.',
        tone: 'green',
      },
    ],
  },

  flow: {
    eyebrow: 'Ba việc trên quầy',
    titleBefore: 'Novixa làm ',
    titleHighlight: 'ba việc này',
    lead: 'Bán đúng lô, biết khách nào sắp mua lại, và xem số trong ngày mà không cộng sổ tay.',
    brandTitle: 'Novixa',
    brandTagline: 'Quầy, kho và khách trên một nền',
    steps: [
      {
        title: 'Bán đúng lô',
        desc: 'Xuất hàng theo hạn dùng, không bán lô mới trước lô cũ.',
      },
      {
        title: 'Nhắc khách quay lại',
        desc: 'Biết khách nào sắp hết thuốc để chăm sóc lại.',
      },
      {
        title: 'Báo cáo trong ngày',
        desc: 'Doanh thu và tồn xem được khi chủ không đứng quầy.',
      },
    ],
  },

  pharmacy: {
    eyebrow: 'Dành cho',
    heading: 'Nhà thuốc',
    title: 'Quản lý toàn diện – Chăm sóc khách hàng – Tăng trưởng doanh thu',
    items: [
      'Bán hàng tại quầy',
      'Tồn theo lô và hạn dùng',
      'Nhắc khách tái mua',
      'Báo cáo doanh thu và tồn trong ngày',
    ],
    cta: 'Khám phá ngay',
    ctaHref: '/vi/nha-thuoc/',
    image: '/images/audience/pharmacy.png',
    imageAlt: 'Dược sĩ sử dụng Novixa trên máy tính bảng',
  },

  people: {
    eyebrow: 'Dành cho',
    heading: 'Người dân',
    title: 'Ứng dụng chăm sóc sức khỏe cá nhân và gia đình hoàn toàn miễn phí',
    items: [
      'Nhắc uống thuốc',
      'Xem lại đơn đã mua',
      'Theo dõi cho người trong nhà',
    ],
    cta: 'Tải app miễn phí',
    ctaHref: '/vi/nguoi-dan/',
    image: '/images/audience/people.png',
    imageAlt: 'Người dùng theo dõi sức khỏe trên app Novixa',
  },

  ecosystem: {
    eyebrow: 'Đang nhận triển khai',
    title: 'Nhà thuốc độc lập và chuỗi nhỏ',
    lead: 'Hai quy mô Novixa đang làm cùng chủ nhà thuốc. Chi phí nói ở trang Giải pháp và chi phí.',
    cta: 'Xem quy mô',
    cards: [
      {
        tone: 'green',
        title: 'Nhà thuốc độc lập',
        desc: 'Một cơ sở. Bán hàng, kho lô, khách và báo cáo trong ngày.',
        href: '/vi/giai-phap-chi-phi/',
      },
      {
        tone: 'blue',
        title: 'Chuỗi 2–10 cơ sở',
        desc: 'Nhiều cửa hàng trên một nền, phân quyền và so sánh từng cửa.',
        href: '/vi/giai-phap-chi-phi/',
      },
    ],
    stats: [
      { value: '1 cơ sở', label: 'Nhà thuốc độc lập' },
      { value: '2–10 cơ sở', label: 'Chuỗi nhỏ' },
    ],
  },

  knowledge: {
    title: 'Ba bài cho đúng việc trên quầy',
    cta: 'Xem kiến thức',
    href: '/vi/kien-thuc/',
  },

  cta: {
    title: 'Đăng ký demo cho nhà thuốc của bạn',
    subtitle: 'Buổi demo xem quầy, kho lô và khách. Chi phí báo sau khi rõ quy mô.',
    primary: 'Đăng ký demo',
    primaryHref: '/vi/lien-he/',
    secondary: 'Xem chi phí',
    secondaryHref: '/vi/giai-phap-chi-phi/',
    trusts: [
      {
        icon: 'shield',
        title: 'Phân quyền',
        desc: 'Nhân viên thấy việc của mình. Chủ thấy số của cửa hàng.',
      },
      {
        icon: 'easy',
        title: 'Đúng việc quầy',
        desc: 'Bán hàng, lô hạn và khách nằm trên một màn.',
      },
      {
        icon: 'stable',
        title: 'Dùng từ quầy',
        desc: 'Mở trên máy tính tại cửa hàng.',
      },
      {
        icon: 'support',
        title: 'Có người triển khai',
        desc: 'Có người hỗ trợ lúc đưa dữ liệu và tập cho quầy.',
      },
    ],
  },
} as const;
