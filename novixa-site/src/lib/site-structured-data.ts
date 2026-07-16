import { absoluteUrl, SITE_NAME, SITE_URL } from './site';

const LOGO_PATH = '/images/logo.png';

export function buildOrganizationStructuredData() {
  return {
    '@type': 'Organization',
    '@id': `${SITE_URL}/#organization`,
    name: SITE_NAME,
    url: SITE_URL,
    logo: {
      '@type': 'ImageObject',
      url: absoluteUrl(LOGO_PATH),
    },
    sameAs: ['https://kittech.vn'],
    contactPoint: [
      {
        '@type': 'ContactPoint',
        telephone: '+84-984-660-399',
        contactType: 'customer service',
        areaServed: 'VN',
        availableLanguage: ['Vietnamese'],
        email: 'care@novixa.vn',
      },
    ],
  };
}

export function buildWebSiteStructuredData() {
  return {
    '@type': 'WebSite',
    '@id': `${SITE_URL}/#website`,
    url: `${SITE_URL}/vi/`,
    name: SITE_NAME,
    inLanguage: 'vi-VN',
    publisher: { '@id': `${SITE_URL}/#organization` },
  };
}

export function buildSiteGraphStructuredData(): Record<string, unknown> {
  return {
    '@context': 'https://schema.org',
    '@graph': [buildOrganizationStructuredData(), buildWebSiteStructuredData()],
  };
}
