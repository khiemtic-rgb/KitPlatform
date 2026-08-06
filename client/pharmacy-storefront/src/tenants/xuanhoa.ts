import type { PharmacyTenantConfig } from './types';

const APP = 'https://app.novixa.vn/?tenantCode=NT_XUANHOA';
const QR = `https://api.qrserver.com/v1/create-qr-code/?size=160x160&data=${encodeURIComponent(APP)}`;

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
    logoUrl: '/brand/xuanhoa-logo.png',
    primaryColor: '#0a6b45',
    accentColor: '#2f9e44',
  },

  contact: {
    address: 'Số 30, đường Xuân Hoà, phường Phan Đình Phùng, tỉnh Thái Nguyên',
    hours: 'Mở cửa 7:00 – 22:00 mỗi ngày',
    phone: '0914.960.069',
    email: 'care@xuanhoa.novixa.vn',
    social: {
      facebook: 'https://facebook.com/',
      zalo: 'https://zalo.me/0914960069',
      whatsapp: 'https://wa.me/84914960069',
      tiktok: undefined,
      youtube: undefined,
    },
    branches: [
      {
        name: 'Nhà thuốc Xuân Hòa',
        address: 'Số 30, đường Xuân Hoà, phường Phan Đình Phùng, tỉnh Thái Nguyên',
        hours: '7:00 – 22:00',
        phone: '0914.960.069',
      },
    ],
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
    qrImageUrl: QR,
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
      price: 'Từ 48.000đ',
      imageUrl: 'https://images.unsplash.com/photo-1584308666744-24d5c474f2ae?auto=format&fit=crop&w=400&q=80',
      href: APP,
    },
    {
      name: 'Panadol Extra',
      category: 'Giảm đau hạ sốt',
      price: 'Từ 32.000đ',
      imageUrl: 'https://images.unsplash.com/photo-1587854692152-cbe660dbde88?auto=format&fit=crop&w=400&q=80',
      href: APP,
    },
    {
      name: 'Vitamin tổng hợp',
      category: 'Vitamin',
      price: 'Liên hệ / đặt trên App',
      imageUrl: 'https://images.unsplash.com/photo-1550572017-edd951aa8f72?auto=format&fit=crop&w=400&q=80',
      href: APP,
    },
    {
      name: 'Máy đo huyết áp',
      category: 'Thiết bị',
      price: 'Liên hệ / đặt trên App',
      imageUrl: 'https://images.unsplash.com/photo-1615486511484-92e172cc4fe0?auto=format&fit=crop&w=400&q=80',
      href: APP,
    },
  ],

  articles: [
    {
      slug: 'phong-cam-cum',
      title: 'Cách phòng cảm cúm hiệu quả mùa giao mùa',
      date: '2026-08-01',
      excerpt: 'Những thói quen đơn giản giúp gia đình giảm nguy cơ cảm cúm.',
      imageUrl: 'https://images.unsplash.com/photo-1584820927498-cfe5211fd8bf?auto=format&fit=crop&w=200&q=80',
      href: '/kien-thuc/phong-cam-cum',
      body: [
        'Mùa giao mùa, virus cảm cúm dễ lây lan trong gia đình, trường học và nơi làm việc. Phòng bệnh đúng cách giúp giảm nguy cơ và giảm gánh nặng điều trị.',
        'Rửa tay thường xuyên bằng xà phòng, che miệng khi ho/hắt hơi, và hạn chế đưa tay lên mặt là các bước đơn giản nhưng hiệu quả.',
        'Giữ nhà thoáng, ngủ đủ giấc, uống đủ nước và ăn đủ chất giúp cơ thể có sức đề kháng tốt hơn.',
        'Nếu có người cao tuổi, trẻ nhỏ hoặc bệnh nền trong nhà, hãy hỏi dược sĩ về biện pháp phòng ngừa phù hợp — bao gồm khi nào cần đến cơ sở y tế.',
        'Nhà thuốc Xuân Hòa hỗ trợ tư vấn dùng thuốc cảm an toàn trên App Novixa và tại quầy. Không tự ý phối nhiều loại giảm đau/hạ sốt cùng lúc.',
      ],
    },
    {
      slug: 'kiem-soat-huyet-ap',
      title: '5 thói quen giúp kiểm soát huyết áp',
      date: '2026-07-20',
      excerpt: 'Ăn uống, vận động và theo dõi chỉ số tại nhà đúng cách.',
      imageUrl: 'https://images.unsplash.com/photo-1576091160399-112ba8d25d1d?auto=format&fit=crop&w=200&q=80',
      href: '/kien-thuc/kiem-soat-huyet-ap',
      body: [
        'Tăng huyết áp thường diễn biến thầm lặng. Theo dõi và duy trì thói quen lành mạnh giúp giảm biến chứng tim mạch, đột quỵ.',
        '1) Đo huyết áp đúng cách tại nhà theo lịch bác sĩ/dược sĩ hướng dẫn, ghi lại chỉ số để theo dõi xu hướng.',
        '2) Giảm muối, hạn chế đồ ăn chế biến sẵn; ưu tiên rau xanh, trái cây và nguồn đạm lành mạnh.',
        '3) Vận động đều đặn (đi bộ nhanh 30 phút/ngày khi được phép) và giữ cân nặng hợp lý.',
        '4) Hạn chế rượu bia, bỏ thuốc lá; ngủ đủ và giảm stress.',
        '5) Uống thuốc huyết áp đúng giờ theo chỉ định — không tự ý ngưng khi thấy “ổn”. Liên hệ dược sĩ Xuân Hòa nếu quên liều hoặc có tác dụng phụ.',
      ],
    },
    {
      slug: 'gap-duoc-si',
      title: 'Khi nào nên gặp dược sĩ trước khi tự mua thuốc?',
      date: '2026-07-10',
      excerpt: 'Dấu hiệu giúp bạn biết lúc nào cần tư vấn chuyên môn.',
      imageUrl: 'https://images.unsplash.com/photo-1631217868264-e5b90bb7e133?auto=format&fit=crop&w=200&q=80',
      href: '/kien-thuc/gap-duoc-si',
      body: [
        'Nhiều thuốc bán tại nhà thuốc vẫn cần tư vấn để dùng đúng và tránh tương tác.',
        'Nên hỏi dược sĩ khi: đang mang thai/cho con bú; có bệnh nền (gan, thận, tim, tiểu đường); đang dùng nhiều loại thuốc; dị ứng thuốc trước đây.',
        'Trẻ em, người cao tuổi, và người vừa xuất viện cũng nên được hướng dẫn liều và cách dùng cụ thể.',
        'Nếu triệu chứng kéo dài, có sốt cao, khó thở, đau ngực hoặc đột ngột nặng hơn — hãy đến cơ sở y tế, không chỉ dựa vào thuốc giảm triệu chứng.',
        'Trên App Novixa bạn có thể chat với nhà thuốc Xuân Hòa để được hỗ trợ nhanh trước khi đến quầy.',
      ],
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
      { value: '15+', label: 'Năm đồng hành' },
      { value: 'TN', label: 'Phục vụ Thái Nguyên' },
      { value: '24/7', label: 'Hỗ trợ qua App' },
    ],
    phoneMockImageUrl:
      'https://images.unsplash.com/photo-1512941937669-90a1b58e7e9c?auto=format&fit=crop&w=600&q=80',
  },

  pages: {
    about: {
      intro:
        'Nhà thuốc Xuân Hòa đồng hành chăm sóc sức khỏe gia đình với thuốc chính hãng, tư vấn dược sĩ tận tâm và trải nghiệm số trên nền tảng Novixa.',
      sections: [
        {
          id: 'su-menh',
          title: 'Sứ mệnh',
          body: 'Mang đến dịch vụ nhà thuốc gần gũi, minh bạch và an toàn — từ tư vấn tại quầy đến đặt thuốc, nhắc uống thuốc trên App Novixa.',
        },
        {
          id: 'doi-ngu',
          title: 'Đội ngũ dược sĩ',
          body: 'Đội ngũ được đào tạo chuyên môn, sẵn sàng giải thích cách dùng thuốc dễ hiểu, hỗ trợ khách hàng chọn lựa phù hợp và nhắc các trường hợp cần gặp bác sĩ.',
        },
        {
          id: 'giay-phep',
          title: 'Giấy phép & cam kết',
          body: 'Nhà thuốc tuân thủ quy định kinh doanh dược. Thông tin giấy phép chi tiết có thể bổ sung trên trang này hoặc xuất trình tại quầy khi khách yêu cầu.',
        },
        {
          id: 'cong-nghe',
          title: 'Vận hành trên Novixa',
          body: 'Xuân Hòa dùng Novixa cho POS, kho, khách hàng và App — giúp hóa đơn rõ ràng, theo dõi đơn hàng và kết nối bền vững với từng gia đình.',
        },
      ],
    },
    contact: {
      intro:
        'Liên hệ nhà thuốc Xuân Hòa để được tư vấn, đặt thuốc hoặc hỗ trợ dùng App Novixa. Ưu tiên chat/đặt hàng trên App để được phục vụ nhanh theo đúng nhà thuốc của bạn.',
      mapNote: 'Số 30, đường Xuân Hoà, phường Phan Đình Phùng, tỉnh Thái Nguyên.',
    },
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
