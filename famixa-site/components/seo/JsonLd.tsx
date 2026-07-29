import { absoluteUrl, SITE_NAME, SITE_URL } from '@/lib/site';
import type { LandingDocument } from '@/content/schema';
import type { AppLocale } from '@/lib/cms/getLanding';

type Props = {
  content: LandingDocument;
  locale?: AppLocale;
};

export function JsonLd({ content, locale = 'vi' }: Props) {
  const path = `/${locale}/`;
  const inLanguage = locale === 'en' ? 'en-US' : 'vi-VN';
  const currency = 'VND';

  const org = {
    '@context': 'https://schema.org',
    '@type': 'Organization',
    name: SITE_NAME,
    url: SITE_URL,
    logo: absoluteUrl(content.brand.logo),
    description: content.seo.description,
    parentOrganization: {
      '@type': 'Organization',
      name: content.footer.company.name,
    },
  };

  const software = {
    '@context': 'https://schema.org',
    '@type': 'SoftwareApplication',
    name: SITE_NAME,
    applicationCategory: 'LifestyleApplication',
    operatingSystem: 'Web, iOS, Android',
    url: absoluteUrl(path),
    inLanguage,
    description: content.seo.description,
    offers: content.chapter8.plans.map((p) => ({
      '@type': 'Offer',
      name: p.name,
      price: p.price.replace(/[^\d]/g, '') || '0',
      priceCurrency: currency,
      description: p.items.join(', '),
    })),
  };

  const faq = {
    '@context': 'https://schema.org',
    '@type': 'FAQPage',
    inLanguage,
    mainEntity: content.faq.items.map((item) => ({
      '@type': 'Question',
      name: item.q,
      acceptedAnswer: {
        '@type': 'Answer',
        text: item.a,
      },
    })),
  };

  const website = {
    '@context': 'https://schema.org',
    '@type': 'WebSite',
    name: SITE_NAME,
    url: SITE_URL,
    inLanguage,
  };

  const payloads = [org, software, faq, website];

  return (
    <>
      {payloads.map((data, i) => (
        <script
          key={i}
          type="application/ld+json"
          dangerouslySetInnerHTML={{ __html: JSON.stringify(data) }}
        />
      ))}
    </>
  );
}
