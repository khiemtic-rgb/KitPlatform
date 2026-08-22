/** Public origin. Do not invent dates, prices, or a site-wide search URL. */
export const SITE_FALLBACK = 'https://thainguyenlife.vn';
export const DEFAULT_DESCRIPTION = 'Việc làm, sự kiện, phòng trọ và khám phá Thái Nguyên.';
export const DEFAULT_OG_IMAGE = '/banner-ho-nui-coc.png';

export function siteOrigin(site?: URL | string | null): string {
  if (site instanceof URL) return site.origin.replace(/\/$/, '');
  if (typeof site === 'string' && site.trim()) {
    try {
      return new URL(site).origin.replace(/\/$/, '');
    } catch {
      /* keep fallback */
    }
  }
  return SITE_FALLBACK;
}

export function absUrl(origin: string, pathOrUrl: string): string {
  if (/^https?:\/\//i.test(pathOrUrl)) return pathOrUrl;
  const path = pathOrUrl.startsWith('/') ? pathOrUrl : `/${pathOrUrl}`;
  return new URL(path, `${origin}/`).href;
}

export function clipMeta(text: string, max = 160): string {
  const one = text.replace(/\s+/g, ' ').trim();
  if (!one) return DEFAULT_DESCRIPTION;
  if (one.length <= max) return one;
  const slice = one.slice(0, max);
  const cut = Math.max(slice.lastIndexOf('. '), slice.lastIndexOf('! '), slice.lastIndexOf('? '), slice.lastIndexOf(' '));
  return `${(cut > 72 ? slice.slice(0, cut) : slice).trim()}…`;
}

export function listingDescription(opts: {
  title: string;
  summary?: string | null;
  place?: string | null;
}): string {
  const body = (opts.summary ?? '').replace(/\s+/g, ' ').trim();
  const place = (opts.place ?? '').replace(/\s+/g, ' ').trim();
  const raw = body || opts.title;
  const withPlace = place && !raw.toLowerCase().includes(place.toLowerCase())
    ? `${raw} ${place}, Thái Nguyên.`
    : raw;
  return clipMeta(withPlace);
}

export type BreadcrumbItem = { name: string; path: string };

export function organizationJsonLd(origin: string): Record<string, unknown> {
  return {
    '@type': 'Organization',
    '@id': `${origin}/#org`,
    name: 'Thái Nguyên Life',
    url: `${origin}/`,
    logo: absUrl(origin, '/logo-thai-nguyen-life.png'),
    description: DEFAULT_DESCRIPTION,
  };
}

export function websiteJsonLd(origin: string): Record<string, unknown> {
  return {
    '@type': 'WebSite',
    '@id': `${origin}/#website`,
    name: 'Thái Nguyên Life',
    url: `${origin}/`,
    inLanguage: 'vi-VN',
    publisher: { '@id': `${origin}/#org` },
  };
}

export function breadcrumbJsonLd(origin: string, crumbs: BreadcrumbItem[]): Record<string, unknown> {
  return {
    '@type': 'BreadcrumbList',
    itemListElement: crumbs.map((c, i) => ({
      '@type': 'ListItem',
      position: i + 1,
      name: c.name,
      item: absUrl(origin, c.path),
    })),
  };
}

export function collectionJsonLd(opts: {
  origin: string;
  path: string;
  name: string;
  description: string;
}): Record<string, unknown> {
  return {
    '@type': 'CollectionPage',
    name: opts.name,
    description: opts.description,
    url: absUrl(opts.origin, opts.path),
    isPartOf: { '@id': `${opts.origin}/#website` },
  };
}

function isoOrNull(raw?: string | null): string | null {
  if (!raw) return null;
  const t = new Date(raw).getTime();
  if (Number.isNaN(t)) return null;
  return new Date(t).toISOString();
}

function thaiNguyenAddress(placeText?: string | null): Record<string, unknown> {
  const street = (placeText ?? '').trim();
  return {
    '@type': 'PostalAddress',
    ...(street ? { streetAddress: street } : {}),
    addressLocality: 'Thái Nguyên',
    addressRegion: 'Thái Nguyên',
    addressCountry: 'VN',
  };
}

/** Event rich result only when a real startAt exists. */
export function eventJsonLd(opts: {
  origin: string;
  path: string;
  name: string;
  description: string;
  startAt?: string | null;
  endAt?: string | null;
  placeText?: string | null;
  organizationName?: string | null;
  image?: string;
}): Record<string, unknown> | null {
  const startDate = isoOrNull(opts.startAt);
  if (!startDate) return null;
  const node: Record<string, unknown> = {
    '@type': 'Event',
    name: opts.name,
    description: opts.description,
    startDate,
    url: absUrl(opts.origin, opts.path),
    eventAttendanceMode: 'https://schema.org/OfflineEventAttendanceMode',
    eventStatus: 'https://schema.org/EventScheduled',
  };
  const endDate = isoOrNull(opts.endAt);
  if (endDate) node.endDate = endDate;
  if (opts.image) node.image = [absUrl(opts.origin, opts.image)];
  if (opts.placeText?.trim()) {
    node.location = {
      '@type': 'Place',
      name: opts.placeText.trim(),
      address: thaiNguyenAddress(opts.placeText),
    };
  }
  if (opts.organizationName?.trim()) {
    node.organizer = { '@type': 'Organization', name: opts.organizationName.trim() };
  }
  return node;
}

function employmentSchema(type?: string | null): string | undefined {
  if (type === 'full_time') return 'FULL_TIME';
  if (type === 'part_time' || type === 'weekend') return 'PART_TIME';
  if (type === 'internship') return 'INTERN';
  return undefined;
}

/** JobPosting only with a real org + datePosted. No invented salary. */
export function jobJsonLd(opts: {
  origin: string;
  path: string;
  title: string;
  description: string;
  publishedAt?: string | null;
  expiresAt?: string | null;
  organizationName?: string | null;
  sourceName?: string | null;
  placeText?: string | null;
  employmentType?: string | null;
}): Record<string, unknown> | null {
  const datePosted = isoOrNull(opts.publishedAt);
  const org = (opts.organizationName || opts.sourceName || '').trim();
  if (!datePosted || !org) return null;
  const node: Record<string, unknown> = {
    '@type': 'JobPosting',
    title: opts.title,
    description: opts.description,
    datePosted,
    url: absUrl(opts.origin, opts.path),
    hiringOrganization: { '@type': 'Organization', name: org },
    jobLocation: {
      '@type': 'Place',
      address: thaiNguyenAddress(opts.placeText),
    },
  };
  const validThrough = isoOrNull(opts.expiresAt);
  if (validThrough) node.validThrough = validThrough;
  const emp = employmentSchema(opts.employmentType);
  if (emp) node.employmentType = emp;
  return node;
}

/** Room / lodging — never emit a price. */
export function roomJsonLd(opts: {
  origin: string;
  path: string;
  name: string;
  description: string;
  placeText?: string | null;
}): Record<string, unknown> {
  return {
    '@type': 'Accommodation',
    name: opts.name,
    description: opts.description,
    url: absUrl(opts.origin, opts.path),
    address: thaiNguyenAddress(opts.placeText),
  };
}

export function placeJsonLd(opts: {
  origin: string;
  path: string;
  name: string;
  description: string;
  placeText?: string | null;
  image?: string;
}): Record<string, unknown> {
  const node: Record<string, unknown> = {
    '@type': 'TouristAttraction',
    name: opts.name,
    description: opts.description,
    url: absUrl(opts.origin, opts.path),
    address: thaiNguyenAddress(opts.placeText),
  };
  if (opts.image) node.image = absUrl(opts.origin, opts.image);
  return node;
}

export function articleJsonLd(opts: {
  origin: string;
  path: string;
  headline: string;
  description: string;
  image?: string;
}): Record<string, unknown> {
  const node: Record<string, unknown> = {
    '@type': 'Article',
    headline: opts.headline,
    description: opts.description,
    url: absUrl(opts.origin, opts.path),
    inLanguage: 'vi-VN',
    publisher: { '@id': `${opts.origin}/#org` },
  };
  if (opts.image) node.image = absUrl(opts.origin, opts.image);
  return node;
}

export function jsonLdGraph(origin: string, extra: Array<Record<string, unknown> | null | undefined>): Record<string, unknown> {
  return {
    '@context': 'https://schema.org',
    '@graph': [organizationJsonLd(origin), websiteJsonLd(origin), ...extra.filter((n): n is Record<string, unknown> => Boolean(n))],
  };
}
