import type { PharmacyTenantConfig } from './types';

const APP = 'https://app.novixa.vn/?tenantCode=NT_XUANHOA';
const NOVIXA = 'https://novixa.vn';

/** Nhà thuốc Xuân Hòa — config white-label (đổi file / đăng ký host = NT mới). */
export const xuanhoa: PharmacyTenantConfig = {
  id: 'xuanhoa',
  slug: 'xuanhoa',
  tenantCode: 'NT_XUANHOA',
  hosts: ['xuanhoa.novixa.vn', 'xuanhoa.localhost', 'xuanhoa', '127.0.0.1', 'localhost'],

  brand: {
    name: 'Nhà thuốc Xuân Hòa',
    shortName: 'Xuân Hòa',
    logoText: 'XH',
    primaryColor: '#004d40',
    accentColor: '#0a7a5c',
  },

  contact: {
    address: 'Số 30, đường Xuân Hòa, phường Phan Đình Phùng, tỉnh Thái Nguyên',
    hours: '07:00 - 22:00 (Tất cả các ngày)',
    phone: '0914.960.069',
    email: 'xuanhoa.pharmacy@gmail.com',
    social: {
      facebook: 'https://www.facebook.com/nhathuocxuanhoa',
      zalo: 'https://zalo.me/0914960069',
    },
    branches: [
      {
        name: 'Nhà thuốc Xuân Hòa',
        address: 'Số 30, đường Xuân Hòa, phường Phan Đình Phùng, tỉnh Thái Nguyên',
        hours: '07:00 - 22:00',
        phone: '0914.960.069',
      },
    ],
  },

  nav: [
    { key: 'home', label: 'Trang chủ', href: '/' },
    { key: 'about', label: 'Giới thiệu', href: '/gioi-thieu' },
    { key: 'products', label: 'Sản phẩm', href: '/san-pham' },
    { key: 'services', label: 'Dịch vụ', href: '/#dich-vu' },
    { key: 'knowledge', label: 'Kiến thức sức khỏe', href: '/kien-thuc' },
    { key: 'contact', label: 'Liên hệ', href: '/lien-he' },
  ],

  hero: {
    headline: 'Nhà thuốc số đồng hành cùng sức khỏe gia đình',
    subhead:
      'Mua thuốc chính hãng, theo dõi sức khỏe, nhắc uống thuốc và kết nối trực tiếp với dược sĩ trong một ứng dụng.',
    imageUrl: '/brand/xuanhoa-hero.png?v=13',
    trustItems: [
      { icon: 'badge', label: 'Thuốc chính hãng', sublabel: 'Đạt chuẩn GPP' },
      { icon: 'advisor', label: 'Dược sĩ tận tâm', sublabel: 'Tư vấn 1-1' },
      { icon: 'scooter', label: 'Giao hàng nhanh', sublabel: 'Giao tận nơi' },
      { icon: 'privacy', label: 'Bảo mật thông tin', sublabel: 'An toàn tuyệt đối' },
    ],
    ctaPrimary: { label: 'Đặt thuốc ngay', href: APP },
    ctaSecondary: { label: 'Tải App Novixa', href: APP },
    heroStats: [
      {
        icon: 'pharmacist',
        value: '',
        label: 'Dược sĩ chuyên môn cao',
        sublabel: 'Tận tâm vì sức khỏe bạn',
      },
      {
        icon: 'orders',
        value: '15.000+',
        label: 'đơn thuốc đã phục vụ',
      },
      {
        icon: 'heart',
        value: '98%',
        label: 'khách hàng hài lòng',
      },
    ],
  },

  trustBand: {
    title: 'Được hơn 3.500 gia đình tin tưởng và lựa chọn',
    titleHighlight: '3.500',
    items: [
      { icon: 'badge', label: 'Thành lập từ', value: '2015' },
      { icon: 'pharmacist', label: 'Đội ngũ', value: '15+ dược sĩ' },
      { icon: 'customers', label: 'Phục vụ', value: '3.500+ khách hàng' },
      { icon: 'hours', label: 'Mở cửa', value: '07:00 - 22:00', sublabel: 'tất cả các ngày' },
    ],
  },

  appPromo: {
    title: 'Quản lý sức khỏe cả gia đình dễ dàng hơn với App Novixa',
    qrImageUrl: '/brand/xuanhoa-app-qr.png',
    appStoreUrl: APP,
    playStoreUrl: APP,
    appUrl: APP,
  },

  services: [
    {
      title: 'Tư vấn sức khỏe',
      description: 'Dược sĩ tư vấn 1-1 tận tâm qua chat hoặc tại quầy',
      icon: 'chat',
    },
    {
      title: 'Đặt thuốc nhanh',
      description: 'Gửi đơn thuốc hoặc đặt lại chỉ trong vài bước',
      icon: 'rx',
    },
    {
      title: 'Giao hàng tận nơi',
      description: 'Giao nhanh nội thành, theo dõi trạng thái đơn',
      icon: 'delivery',
    },
    {
      title: 'Theo dõi sức khỏe',
      description: 'Lưu chỉ số, nhắc uống thuốc theo liệu trình',
      icon: 'health',
    },
    {
      title: 'Hồ sơ gia đình',
      description: 'Quản lý thuốc và lịch sử mua cho cả nhà',
      icon: 'family',
    },
    {
      title: 'Tích điểm & ưu đãi',
      description: 'Tích lũy điểm thưởng, nhận ưu đãi theo mùa',
      icon: 'gift',
    },
  ],

  whyUs: [
    'Hơn 15 năm phục vụ cộng đồng',
    'Dược sĩ chuyên môn cao',
    'Sản phẩm đa dạng, chính hãng',
    'Giá cả minh bạch, hợp lý',
    'Không ngừng cải thiện dịch vụ',
  ],

  products: [
    {
      name: 'Eugica Green',
      category: 'Hỗ trợ điều trị ho',
      price: '45.000đ',
      imageUrl: '/brand/products/eugica-cough.jpg?v=2',
      href: APP,
    },
    {
      name: 'Panadol Extra',
      category: 'Giảm đau - Hạ sốt',
      price: '32.000đ',
      imageUrl: '/brand/products/panadol-pain.jpg?v=2',
      href: APP,
    },
    {
      name: 'Vitabiotics Wellwoman',
      category: 'Vitamin tổng hợp',
      price: '350.000đ',
      imageUrl: '/brand/products/wellwoman-vitamin.jpg?v=2',
      href: APP,
    },
    {
      name: 'Accu-Chek Instant',
      category: 'Máy đo đường huyết',
      price: '820.000đ',
      imageUrl: '/brand/products/accuchek-device.jpg?v=2',
      href: APP,
    },
    {
      name: 'Hapacol 650',
      category: 'Giảm đau - Hạ sốt',
      price: '35.000đ',
      imageUrl: '/brand/products/hapacol-pain.jpg?v=2',
      href: APP,
    },
    {
      name: 'Berocca Performance',
      category: 'Vitamin & khoáng chất',
      price: '125.000đ',
      imageUrl: '/brand/products/berocca-vitamin.jpg?v=2',
      href: APP,
    },
  ],

  articles: [
    {
      slug: 'phong-cam-cum',
      title: 'Cách phòng ngừa cảm cúm khi thời tiết thay đổi',
      date: '2024-05-20',
      excerpt: 'Những thói quen đơn giản giúp gia đình giảm nguy cơ cảm cúm khi giao mùa.',
      imageUrl: '/brand/articles/phong-cam-cum.jpg?v=2',
      href: '/kien-thuc/phong-cam-cum',
      body: [
        'Mùa giao mùa, virus cảm cúm dễ lây lan. Phòng bệnh đúng cách giúp giảm nguy cơ và gánh nặng điều trị.',
        'Rửa tay thường xuyên, che miệng khi ho/hắt hơi, hạn chế đưa tay lên mặt.',
        'Giữ nhà thoáng, ngủ đủ, uống đủ nước và ăn đủ chất.',
        'Nhà thuốc Xuân Hòa hỗ trợ tư vấn dùng thuốc cảm an toàn trên App Novixa và tại quầy.',
      ],
    },
    {
      slug: 'kiem-soat-huyet-ap',
      title: '5 thói quen giúp kiểm soát huyết áp hiệu quả',
      date: '2024-05-18',
      excerpt: 'Ăn uống, vận động và theo dõi chỉ số tại nhà đúng cách.',
      imageUrl: '/brand/articles/kiem-soat-huyet-ap.jpg?v=2',
      href: '/kien-thuc/kiem-soat-huyet-ap',
      body: [
        'Tăng huyết áp thường diễn biến thầm lặng. Duy trì thói quen lành mạnh giúp giảm biến chứng.',
        'Đo huyết áp đúng cách, giảm muối, vận động đều, hạn chế rượu bia và tuân thủ thuốc theo chỉ định.',
      ],
    },
    {
      slug: 'dinh-duong-tieu-duong',
      title: 'Dinh dưỡng cho người tiểu đường: Nguyên tắc vàng',
      date: '2024-05-16',
      excerpt: 'Nguyên tắc vàng giúp ổn định đường huyết qua bữa ăn hàng ngày.',
      imageUrl: '/brand/articles/dinh-duong-tieu-duong.jpg?v=2',
      href: '/kien-thuc/dinh-duong-tieu-duong',
      body: [
        'Chế độ ăn đóng vai trò quan trọng trong kiểm soát đường huyết.',
        'Ưu tiên chất xơ, kiểm soát tinh bột, hạn chế đường thêm; hỏi dược sĩ trước khi dùng thực phẩm chức năng.',
      ],
    },
    {
      slug: 'gap-duoc-si',
      title: 'Khi nào nên gặp dược sĩ trước khi tự mua thuốc?',
      date: '2024-03-15',
      excerpt: 'Dấu hiệu giúp bạn biết lúc nào cần tư vấn chuyên môn.',
      imageUrl: '/brand/articles/gap-duoc-si.jpg?v=2',
      href: '/kien-thuc/gap-duoc-si',
      body: [
        'Nên hỏi dược sĩ khi mang thai, có bệnh nền, đang dùng nhiều thuốc hoặc dị ứng thuốc trước đây.',
        'Trẻ em, người cao tuổi và người vừa xuất viện cần hướng dẫn liều cụ thể.',
      ],
    },
  ],

  appSection: {
    title: 'Quản lý sức khỏe cả gia đình dễ dàng hơn với App Novixa',
    titleHighlight: 'App Novixa',
    features: [
      { icon: 'history', title: 'Lưu lịch sử mua thuốc và đơn thuốc' },
      { icon: 'bell', title: 'Nhắc uống thuốc đúng giờ' },
      { icon: 'pulse', title: 'Theo dõi chỉ số sức khỏe' },
      { icon: 'family', title: 'Quản lý hồ sơ sức khỏe gia đình' },
      { icon: 'chat', title: 'Kết nối trực tiếp với dược sĩ' },
      { icon: 'cart', title: 'Đặt thuốc nhanh giao tận nơi' },
    ],
    familyImageUrl: '/brand/family-health.png?v=7',
  },

  platformPromo: {
    eyebrow: 'Website này được tạo tự động bởi',
    title: 'Novixa Digital Pharmacy Platform',
    subtitle: 'Nền tảng chuyển đổi số toàn diện cho nhà thuốc',
    features: [
      { icon: 'seo', title: 'Website chuyên nghiệp chuẩn SEO' },
      { icon: 'sync', title: 'Đồng bộ quản lý bán hàng kho & khách hàng' },
      { icon: 'care', title: 'Kết nối App, chăm sóc khách hàng toàn diện' },
    ],
    ctaLead: 'Tạo website đẹp như Nhà thuốc Xuân Hòa cho nhà thuốc của bạn chỉ trong 5 phút!',
    ctaLeadHighlight: 'Nhà thuốc Xuân Hòa',
    ctaPrimary: { label: 'Xem demo nhà thuốc', href: NOVIXA },
    ctaSecondary: { label: 'Đăng ký ngay', href: `${NOVIXA}/vi/lien-he/` },
  },

  pages: {
    about: {
      intro:
        'Nhà thuốc Xuân Hòa đồng hành chăm sóc sức khỏe gia đình với thuốc chính hãng, tư vấn dược sĩ tận tâm và trải nghiệm số trên nền tảng Novixa.',
      sections: [
        {
          id: 'su-menh',
          title: 'Sứ mệnh',
          body: 'Mang đến dịch vụ nhà thuốc gần gũi, minh bạch và an toàn — từ tư vấn tại quầy đến đặt thuốc trên App Novixa.',
        },
        {
          id: 'doi-ngu',
          title: 'Đội ngũ dược sĩ',
          body: 'Đội ngũ được đào tạo chuyên môn, sẵn sàng giải thích cách dùng thuốc dễ hiểu.',
        },
        {
          id: 'giay-phep',
          title: 'Giấy phép & cam kết',
          body: 'Nhà thuốc tuân thủ quy định kinh doanh dược. Thông tin giấy phép có thể bổ sung trên trang hoặc tại quầy.',
        },
        {
          id: 'cong-nghe',
          title: 'Vận hành trên Novixa',
          body: 'Xuân Hòa dùng Novixa cho POS, kho, khách hàng và App — hóa đơn rõ ràng, kết nối bền vững với từng gia đình.',
        },
      ],
    },
    contact: {
      intro:
        'Liên hệ nhà thuốc Xuân Hòa để được tư vấn, đặt thuốc hoặc hỗ trợ dùng App Novixa.',
      mapNote: 'Số 30, đường Xuân Hòa, phường Phan Đình Phùng, tỉnh Thái Nguyên.',
    },
  },

  footer: {
    aboutLinks: [
      { label: 'Giới thiệu', href: '/gioi-thieu' },
      { label: 'Đội ngũ dược sĩ', href: '/gioi-thieu#doi-ngu' },
      { label: 'Giấy phép kinh doanh', href: '/gioi-thieu#giay-phep' },
    ],
    categoryLinks: [
      { label: 'Giới thiệu', href: '/gioi-thieu' },
      { label: 'Sản phẩm', href: '/san-pham' },
      { label: 'Dịch vụ', href: '/#dich-vu' },
      { label: 'Kiến thức sức khỏe', href: '/kien-thuc' },
      { label: 'Liên hệ', href: '/lien-he' },
    ],
    supportLinks: [
      { label: 'Hướng dẫn đặt thuốc', href: APP },
      { label: 'Chính sách giao hàng', href: '/lien-he' },
      { label: 'Chính sách đổi trả', href: '/lien-he' },
      { label: 'Chính sách bảo mật', href: 'https://novixa.vn/vi/chinh-sach-bao-mat/' },
      { label: 'Điều khoản sử dụng', href: 'https://novixa.vn/vi/dieu-khoan-su-dung/' },
    ],
    tagline: 'Đồng hành sức khỏe gia đình',
    mission:
      'Nhà thuốc Xuân Hòa cam kết cung cấp sản phẩm chính hãng, tư vấn tận tâm và dịch vụ chăm sóc sức khỏe toàn diện.',
    newsletterNote: 'Nhận thông tin khuyến mãi và kiến thức sức khỏe mới nhất từ Nhà thuốc Xuân Hòa.',
    copyright: 'Tất cả quyền được bảo lưu.',
  },

  poweredBy: {
    label: 'POWERED BY NOVIXA',
    href: NOVIXA,
    blurb: 'Nhà thuốc vận hành trên nền tảng Novixa — POS, App khách, CRM và hiện diện số.',
  },
};
