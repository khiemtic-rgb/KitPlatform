export const SITE_NAME = 'Famixa';
export const SITE_URL = 'https://famixa.vn';
export const APP_URL = 'https://home.famixa.vn';
export const DEFAULT_OG_IMAGE = '/images/hero-family.png';

export function absoluteUrl(path: string): string {
  const base = SITE_URL.replace(/\/$/, '');
  const p = path.startsWith('/') ? path : `/${path}`;
  return `${base}${p}`;
}
