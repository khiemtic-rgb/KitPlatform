import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import viCommon from '@/shared/i18n/locales/vi-VN/common.json';
import viDashboard from '@/shared/i18n/locales/vi-VN/dashboard.json';
import viSales from '@/shared/i18n/locales/vi-VN/sales.json';
import viCatalog from '@/shared/i18n/locales/vi-VN/catalog.json';
import viInventory from '@/shared/i18n/locales/vi-VN/inventory.json';
import viProcurement from '@/shared/i18n/locales/vi-VN/procurement.json';
import viCustomer from '@/shared/i18n/locales/vi-VN/customer.json';
import viSystem from '@/shared/i18n/locales/vi-VN/system.json';
import viAuth from '@/shared/i18n/locales/vi-VN/auth.json';
import viReports from '@/shared/i18n/locales/vi-VN/reports.json';
import enCommon from '@/shared/i18n/locales/en-US/common.json';
import enDashboard from '@/shared/i18n/locales/en-US/dashboard.json';
import enSales from '@/shared/i18n/locales/en-US/sales.json';
import enCatalog from '@/shared/i18n/locales/en-US/catalog.json';
import enInventory from '@/shared/i18n/locales/en-US/inventory.json';
import enProcurement from '@/shared/i18n/locales/en-US/procurement.json';
import enCustomer from '@/shared/i18n/locales/en-US/customer.json';
import enSystem from '@/shared/i18n/locales/en-US/system.json';
import enAuth from '@/shared/i18n/locales/en-US/auth.json';
import enReports from '@/shared/i18n/locales/en-US/reports.json';

export const ADMIN_LOCALE_STORAGE_KEY = 'admin-locale';

export type AdminLocale = 'vi-VN' | 'en-US';

export function resolveInitialAdminLocale(): AdminLocale {
  const fromUrl = new URLSearchParams(window.location.search).get('locale');
  if (fromUrl === 'en-US' || fromUrl === 'vi-VN') {
    localStorage.setItem(ADMIN_LOCALE_STORAGE_KEY, fromUrl);
    return fromUrl;
  }
  const stored = localStorage.getItem(ADMIN_LOCALE_STORAGE_KEY);
  if (stored === 'en-US' || stored === 'vi-VN') return stored;
  return 'vi-VN';
}

void i18n.use(initReactI18next).init({
  resources: {
    'vi-VN': {
      common: viCommon,
      dashboard: viDashboard,
      sales: viSales,
      catalog: viCatalog,
      inventory: viInventory,
      procurement: viProcurement,
      customer: viCustomer,
      system: viSystem,
      auth: viAuth,
      reports: viReports,
    },
    'en-US': {
      common: enCommon,
      dashboard: enDashboard,
      sales: enSales,
      catalog: enCatalog,
      inventory: enInventory,
      procurement: enProcurement,
      customer: enCustomer,
      system: enSystem,
      auth: enAuth,
      reports: enReports,
    },
  },
  lng: resolveInitialAdminLocale(),
  fallbackLng: 'vi-VN',
  defaultNS: 'common',
  ns: [
    'common',
    'dashboard',
    'sales',
    'catalog',
    'inventory',
    'procurement',
    'customer',
    'system',
    'auth',
    'reports',
  ],
  interpolation: { escapeValue: false },
});

export function commonT() {
  return i18n.getFixedT(i18n.language, 'common');
}

export function dashboardT() {
  return i18n.getFixedT(i18n.language, 'dashboard');
}

export function salesT() {
  return i18n.getFixedT(i18n.language, 'sales');
}

export function catalogT() {
  return i18n.getFixedT(i18n.language, 'catalog');
}

export function inventoryT() {
  return i18n.getFixedT(i18n.language, 'inventory');
}

export function procurementT() {
  return i18n.getFixedT(i18n.language, 'procurement');
}

export function customerT() {
  return i18n.getFixedT(i18n.language, 'customer');
}

export function systemT() {
  return i18n.getFixedT(i18n.language, 'system');
}

export function authT() {
  return i18n.getFixedT(i18n.language, 'auth');
}

export function reportsT() {
  return i18n.getFixedT(i18n.language, 'reports');
}

export default i18n;
