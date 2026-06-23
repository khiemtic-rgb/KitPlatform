export const SITE_URL = 'https://novixa.vn';
export const SITE_NAME = 'Novixa';
export const DEFAULT_OG_IMAGE = '/images/banner.png';

export function absoluteUrl(path: string): string {
  if (path.startsWith('http')) return path;
  return `${SITE_URL}${path.startsWith('/') ? path : `/${path}`}`;
}
