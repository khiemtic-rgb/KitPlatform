export const SITE_NAME = 'Famixa';
export const SITE_URL = 'https://famixa.vn';
export const APP_URL = 'https://family.kittech.vn';
export const DEFAULT_OG_IMAGE = '/images/hero-family.png';

export function absoluteUrl(path: string): string {
  if (path.startsWith('http')) return path;
  return new URL(path, SITE_URL).href;
}
