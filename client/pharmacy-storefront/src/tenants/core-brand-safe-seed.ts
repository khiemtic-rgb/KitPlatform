import type { PharmacyTenantConfig } from './types';
import { createTemplateSeed } from './template-seed';

/**
 * Core white-label baseline for CMS-driven tenants (non–Xuân Hòa).
 * Starts from the layout skeleton, then strips pilot-specific copy/media
 * so deep-merge never leaks Xuân Hòa branding into other pharmacies.
 * Do not use for slug `xuanhoa` (pilot keeps full template overlay).
 */
export function createCoreBrandSafeSeed(): PharmacyTenantConfig {
  const seed = createTemplateSeed();
  const appUrl = 'https://app.novixa.vn/';

  seed.hero = {
    ...seed.hero,
    headline: '',
    subhead: '',
    imageUrl: '',
    trustItems: [
      { icon: 'badge', label: 'Thuốc chính hãng' },
      { icon: 'advisor', label: 'Dược sĩ tận tâm' },
      { icon: 'scooter', label: 'Giao hàng nhanh' },
      { icon: 'privacy', label: 'Bảo mật thông tin' },
    ],
    heroStats: [
      { icon: 'pharmacist', label: 'Dược sĩ', value: 'Tận tâm' },
      { icon: 'orders', label: 'Đặt thuốc', value: 'Nhanh chóng' },
      { icon: 'heart', label: 'Gia đình', value: 'Được chăm sóc' },
    ],
    ctaPrimary: { label: 'Đặt thuốc ngay', href: appUrl },
    ctaSecondary: { label: 'Tải App Novixa', href: appUrl },
  };

  seed.trustBand = {
    title: 'Cam kết chăm sóc sức khỏe gia đình',
    titleHighlight: 'cam kết',
    items: [
      { icon: 'badge', label: 'Chính hãng', value: 'Nguồn gốc rõ ràng' },
      { icon: 'pharmacist', label: 'Tư vấn', value: 'Dược sĩ tận tâm' },
      { icon: 'customers', label: 'Phục vụ', value: 'Tận nơi & tại quầy' },
      { icon: 'hours', label: 'Giờ mở cửa', value: 'Linh hoạt mỗi ngày' },
    ],
  };

  seed.whyUs = [
    'Thuốc chính hãng, nguồn gốc rõ ràng',
    'Dược sĩ tư vấn tận tâm',
    'Đặt thuốc nhanh qua App Novixa',
    'Chăm sóc sức khỏe lâu dài cho gia đình',
  ];

  seed.services = [];
  seed.products = [];
  seed.articles = [];

  seed.appPromo = {
    ...seed.appPromo,
    title: 'Quản lý sức khỏe cả gia đình dễ dàng hơn với App Novixa',
    appUrl,
    appStoreUrl: appUrl,
    playStoreUrl: appUrl,
  };

  seed.appSection = {
    ...seed.appSection,
    title: 'Quản lý sức khỏe gia đình với App Novixa',
    titleHighlight: 'App Novixa',
  };

  seed.platformPromo = {
    eyebrow: 'Nền tảng Novixa',
    title: 'Website nhà thuốc liên kết App',
    subtitle: 'Khách xem thông tin trên web và đặt thuốc / theo dõi sức khỏe trên App Novixa.',
    features: seed.platformPromo.features,
    ctaLead: 'Bắt đầu ngay với App Novixa',
    ctaLeadHighlight: 'App Novixa',
    ctaPrimary: { label: 'Tải App Novixa', href: appUrl },
    ctaSecondary: { label: 'Liên hệ nhà thuốc', href: '/lien-he' },
  };

  seed.pages = {
    ...seed.pages,
    about: {
      ...seed.pages.about,
      intro: '',
      hero: {
        ...seed.pages.about.hero,
        eyebrow: 'Giới thiệu',
        title: 'Về nhà thuốc',
        subtitle: '',
        body: '',
        imageUrl: '',
        imageAlt: 'Nhà thuốc',
        ctaPrimary: { label: 'Đặt thuốc trên App', href: appUrl },
        ctaSecondary: { label: 'Liên hệ', href: '/lien-he' },
      },
      valuesTitle: 'Giá trị cốt lõi',
      values: [
        { icon: 'badge', title: 'Chính hãng', description: 'Cam kết nguồn gốc rõ ràng, kiểm soát chất lượng.' },
        { icon: 'heart', title: 'Tận tâm', description: 'Dược sĩ lắng nghe và tư vấn phù hợp nhu cầu.' },
        { icon: 'phone', title: 'Tiện lợi', description: 'Đặt thuốc, nhắc uống và theo dõi sức khỏe trên App.' },
      ],
      reasonsTitle: 'Vì sao khách hàng tin tưởng',
      reasons: [
        {
          icon: 'badge',
          label: 'Chính hãng',
          description: 'Cam kết thuốc chính hãng, hóa đơn rõ ràng và tư vấn minh bạch.',
        },
        {
          icon: 'pharmacist',
          label: 'Dược sĩ',
          description: 'Chuyên môn vững, hỗ trợ tại quầy và trên App Novixa.',
        },
        {
          icon: 'customers',
          label: 'Gần gũi',
          description: 'Đồng hành chăm sóc sức khỏe lâu dài cho cả gia đình.',
        },
        {
          icon: 'hours',
          label: 'Linh hoạt',
          description: 'Giờ mở cửa thuận tiện, phục vụ theo nhịp sống gia đình.',
        },
      ],
      team: {
        title: 'Đội ngũ dược sĩ',
        body: 'Đội ngũ dược sĩ được đào tạo chuyên môn, sẵn sàng tư vấn tận tâm tại quầy và trên App Novixa.',
        highlights: ['Tư vấn 1-1 tại quầy và trên App', 'Giải thích cách dùng thuốc dễ hiểu', 'Đồng hành cùng gia đình'],
        ctaPrimary: { label: 'Gặp dược sĩ trên App', href: appUrl },
        ctaSecondary: { label: 'Liên hệ nhà thuốc', href: '/lien-he' },
      },
      digital: {
        title: 'Ứng dụng công nghệ để chăm sóc sức khỏe gia đình tốt hơn',
        bullets: [
          'Đặt thuốc nhanh trên App Novixa',
          'Lưu lịch sử mua thuốc và đơn thuốc',
          'Nhắc uống thuốc đúng giờ',
          'Theo dõi chỉ số sức khỏe',
          'Quản lý hồ sơ sức khỏe gia đình',
          'Kết nối trực tiếp với dược sĩ',
        ],
      },
      gallery: {
        title: 'Không gian nhà thuốc',
        images: [],
        ctaLabel: 'Liên hệ',
        ctaHref: '/lien-he',
      },
      certificates: {
        title: 'Giấy phép & chứng nhận',
        items: [],
        ctaLabel: 'Liên hệ',
        ctaHref: '/lien-he',
      },
      supportCta: {
        title: 'Cần tư vấn thêm?',
        imageUrl: '',
        imageAlt: 'Tư vấn nhà thuốc',
        ctaPrimary: { label: 'Đặt thuốc nhanh', href: appUrl },
        ctaSecondary: { label: 'Liên hệ với chúng tôi', href: '/lien-he' },
      },
      sections: [
        {
          id: 'su-menh',
          title: 'Sứ mệnh',
          body: 'Mang đến dịch vụ nhà thuốc gần gũi, minh bạch và an toàn — từ tư vấn tại quầy đến đặt thuốc trên App Novixa.',
        },
        {
          id: 'cam-ket',
          title: 'Cam kết',
          body: 'Thuốc chính hãng, tư vấn đúng nhu cầu và đồng hành cùng sức khỏe gia đình trên nền tảng số.',
        },
      ],
    },
    services: {
      ...seed.pages.services,
      featured: [],
      hero: {
        ...seed.pages.services.hero,
        title: 'Dịch vụ của chúng tôi',
        body: 'Lựa chọn dịch vụ phù hợp — đặt trên App hoặc ghé nhà thuốc.',
        imageUrl: '',
        imageAlt: 'Dịch vụ nhà thuốc',
      },
    },
    products: {
      ...seed.pages.products,
      hero: {
        ...seed.pages.products.hero,
        title: 'Sản phẩm chăm sóc sức khỏe',
        body: 'Xem sản phẩm nổi bật và đặt nhanh trên App Novixa.',
        bullets: ['Thuốc chính hãng', 'Tư vấn dược sĩ', 'Đặt trên App'],
      },
    },
    contact: {
      ...seed.pages.contact,
      mapEmbedUrl: '',
      directionsUrl: '',
      mapNote: '',
      hero: {
        ...seed.pages.contact.hero,
        title: 'Liên hệ nhà thuốc',
        body: 'Ghé thăm hoặc gửi tin nhắn — chúng tôi sẵn sàng hỗ trợ.',
        imageUrl: '',
        imageAlt: 'Liên hệ nhà thuốc',
      },
    },
  };

  seed.footer = {
    ...seed.footer,
    mission: 'Cam kết cung cấp sản phẩm chính hãng, tư vấn tận tâm và dịch vụ chăm sóc sức khỏe toàn diện.',
    newsletterNote: 'Nhận thông tin khuyến mãi và kiến thức sức khỏe từ nhà thuốc.',
  };

  return seed;
}
