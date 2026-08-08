import type { PharmacyTenantConfig } from './types';
import { xuanhoa } from './xuanhoa';

/** Visual/layout defaults cloned from Xuân Hòa pilot (identity fields wiped). */
export function createTemplateSeed(): PharmacyTenantConfig {
  const seed = structuredClone(xuanhoa) as PharmacyTenantConfig;
  seed.id = 'template';
  seed.slug = 'template';
  seed.tenantCode = '';
  seed.hosts = [];
  seed.brand = {
    ...seed.brand,
    name: 'Nhà thuốc',
    shortName: 'Nhà thuốc',
    logoText: 'NT',
    logoUrl: '',
  };
  seed.contact = {
    ...seed.contact,
    address: '',
    phone: '',
    email: '',
    notifyEmail: '',
    social: { facebook: '', zalo: '' },
    branches: [],
  };
  seed.nav = [
    { key: 'home', label: 'Trang chủ', href: '/' },
    { key: 'about', label: 'Giới thiệu', href: '/gioi-thieu' },
    { key: 'products', label: 'Sản phẩm', href: '/san-pham' },
    { key: 'services', label: 'Dịch vụ', href: '/dich-vu' },
    { key: 'contact', label: 'Liên hệ', href: '/lien-he' },
  ];
  // Core package: hide knowledge until Plus
  seed.articles = [];
  return seed;
}
