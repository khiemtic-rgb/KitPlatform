import type { PharmacyTenantConfig } from './types';

const APP = 'https://app.novixa.vn/?tenantCode=NT_XUANHOA';

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
    primaryColor: '#0d6b5c',
    accentColor: '#148f77',
  },

  contact: {
    address: 'Khu Xuân Hòa · hỗ trợ giao khu vực lân cận',
    hours: 'Mở cửa 7:00 – 22:00 mỗi ngày',
    phone: '0900 000 000',
    email: 'care@xuanhoa.novixa.vn',
    social: {
      facebook: 'https://facebook.com/',
      zalo: 'https://zalo.me/',
      whatsapp: 'https://wa.me/84900000000',
      tiktok: 'https://tiktok.com/',
      youtube: 'https://youtube.com/',
    },
  },

  nav: [
    { key: 'home', label: 'Trang chủ', href: '/' },
    { key: 'about', label: 'Giới thiệu', href: '/gioi-thieu' },
    { key: 'products', label: 'Sản phẩm', href: '/san-pham' },
    { key: 'knowledge', label: 'Kiến thức sức khỏe', href: '/kien-thuc' },
    { key: 'order', label: 'Đặt thuốc', href: APP },
    { key: 'services', label: 'Dịch vụ', href: '/#dich-vu' },
    { key: 'contact', label: 'Liên hệ', href: '/lien-he' },
  ],

  hero: {
    headline: 'Đồng hành chăm sóc sức khỏe gia đình bạn',
    subhead:
      'Thuốc chính hãng · tư vấn dược sĩ tận tâm · đặt hàng và quản lý sức khỏe gia đình trên App Novixa.',
    imageUrl:
      'https://images.unsplash.com/photo-1576602976047-174e57a47881?auto=format&fit=crop&w=1200&q=80',
    trustItems: [
      { icon: 'shield', label: 'Thuốc chính hãng' },
      { icon: 'heart', label: 'Tư vấn tận tâm' },
      { icon: 'truck', label: 'Giao hàng nhanh' },
      { icon: 'lock', label: 'Bảo mật dữ liệu' },
    ],
    ctaPrimary: { label: 'Đặt thuốc ngay', href: APP },
    ctaSecondary: { label: 'Tải App Novixa', href: APP },
  },

  appPromo: {
    title: 'Quản lý sức khỏe cả gia đình với App Novixa',
    qrImageUrl: '',
    appStoreUrl: APP,
    playStoreUrl: APP,
    appUrl: APP,
  },

  services: [
    {
      title: 'Đặt thuốc nhanh',
      description: 'Gửi đơn thuốc hoặc đặt lại từ lịch sử mua trên App Novixa.',
      icon: 'cart',
    },
    {
      title: 'Tư vấn trực tuyến',
      description: 'Chat / gọi dược sĩ khi cần hỗ trợ dùng thuốc an toàn.',
      icon: 'chat',
    },
    {
      title: 'Giao tận nơi',
      description: 'Giao nhanh trong khu vực — theo dõi trạng thái đơn trên app.',
      icon: 'delivery',
    },
    {
      title: 'Theo dõi sức khỏe',
      description: 'Lịch sử mua, nhắc uống thuốc và hồ sơ gia đình trên Novixa.',
      icon: 'pulse',
    },
  ],

  whyUs: [
    'Hơn 15 năm đồng hành cùng khách hàng địa phương',
    'Dược sĩ chuyên môn, tư vấn dễ hiểu',
    'Thuốc chính hãng, nguồn gốc rõ ràng',
    'Giá minh bạch, hóa đơn đầy đủ',
    'Không ngừng cải tiến dịch vụ với nền tảng Novixa',
  ],

  products: [
    {
      name: 'Eugica Fort',
      category: 'Hô hấp',
      price: '48.000đ',
      imageUrl: 'https://images.unsplash.com/photo-1584308666744-24d5c474f2ae?auto=format&fit=crop&w=400&q=80',
      href: APP,
    },
    {
      name: 'Panadol Extra',
      category: 'Giảm đau hạ sốt',
      price: '32.000đ',
      imageUrl: 'https://images.unsplash.com/photo-1587854692152-cbe660dbde88?auto=format&fit=crop&w=400&q=80',
      href: APP,
    },
    {
      name: 'Vitamin tổng hợp',
      category: 'Vitamin',
      price: '125.000đ',
      imageUrl: 'https://images.unsplash.com/photo-1550572017-edd951aa8f72?auto=format&fit=crop&w=400&q=80',
      href: APP,
    },
    {
      name: 'Máy đo huyết áp',
      category: 'Thiết bị',
      price: '890.000đ',
      imageUrl: 'https://images.unsplash.com/photo-1615486511484-92e172cc4fe0?auto=format&fit=crop&w=400&q=80',
      href: APP,
    },
  ],

  articles: [
    {
      title: 'Cách phòng cảm cúm hiệu quả mùa giao mùa',
      date: '2026-08-01',
      excerpt: 'Những thói quen đơn giản giúp gia đình giảm nguy cơ cảm cúm.',
      imageUrl: 'https://images.unsplash.com/photo-1584820927498-cfe5211fd8bf?auto=format&fit=crop&w=200&q=80',
      href: '/kien-thuc/phong-cam-cum',
    },
    {
      title: '5 thói quen giúp kiểm soát huyết áp',
      date: '2026-07-20',
      excerpt: 'Ăn uống, vận động và theo dõi chỉ số tại nhà đúng cách.',
      imageUrl: 'https://images.unsplash.com/photo-1576091160399-112ba8d25d1d?auto=format&fit=crop&w=200&q=80',
      href: '/kien-thuc/kiem-soat-huyet-ap',
    },
    {
      title: 'Khi nào nên gặp dược sĩ trước khi tự mua thuốc?',
      date: '2026-07-10',
      excerpt: 'Dấu hiệu giúp bạn biết lúc nào cần tư vấn chuyên môn.',
      imageUrl: 'https://images.unsplash.com/photo-1631217868264-e5b90bb7e133?auto=format&fit=crop&w=200&q=80',
      href: '/kien-thuc/gap-duoc-si',
    },
  ],

  appSection: {
    bullets: [
      'Lịch sử mua hàng và đặt lại nhanh',
      'Nhắc uống thuốc theo liệu trình',
      'Theo dõi sức khỏe & hồ sơ gia đình',
      'Kết nối trực tiếp với nhà thuốc Xuân Hòa',
    ],
    stats: [
      { value: '10.000+', label: 'Khách hàng tin dùng' },
      { value: '500+', label: 'Gia đình dùng App' },
      { value: '98%', label: 'Hài lòng dịch vụ' },
    ],
    phoneMockImageUrl:
      'https://images.unsplash.com/photo-1512941937669-90a1b58e7e9c?auto=format&fit=crop&w=600&q=80',
  },

  footer: {
    aboutLinks: [
      { label: 'Giới thiệu', href: '/gioi-thieu' },
      { label: 'Đội ngũ dược sĩ', href: '/gioi-thieu#doi-ngu' },
      { label: 'Giấy phép kinh doanh', href: '/gioi-thieu#giay-phep' },
      { label: 'Tuyển dụng', href: '/lien-he' },
    ],
    supportLinks: [
      { label: 'Hướng dẫn đặt thuốc', href: APP },
      { label: 'Chính sách giao hàng', href: '/lien-he' },
      { label: 'Đổi trả', href: '/lien-he' },
      { label: 'Bảo mật', href: 'https://novixa.vn/vi/chinh-sach-bao-mat/' },
      { label: 'Điều khoản', href: 'https://novixa.vn/vi/dieu-khoan-su-dung/' },
    ],
    knowledgeLinks: [
      { label: 'Tin sức khỏe', href: '/kien-thuc' },
      { label: 'Bệnh thường gặp', href: '/kien-thuc' },
      { label: 'Cách dùng thuốc', href: '/kien-thuc' },
      { label: 'Dinh dưỡng', href: '/kien-thuc' },
      { label: 'Sống khỏe', href: '/kien-thuc' },
    ],
    mission:
      'Nhà thuốc Xuân Hòa đồng hành chăm sóc sức khỏe gia đình — thuốc chính hãng, tư vấn tận tâm, hiện diện số trên nền tảng Novixa.',
  },

  poweredBy: {
    label: 'POWERED BY NOVIXA',
    href: 'https://novixa.vn',
    blurb: 'Nhà thuốc vận hành trên nền tảng Novixa — POS, App khách, CRM và hiện diện số trong một hệ thống.',
  },
};
