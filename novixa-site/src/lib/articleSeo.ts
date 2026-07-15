import { absoluteUrl, SITE_NAME, SITE_URL } from './site';

export const BRAND_TAGLINE = 'Novixa — Nền tảng quản trị nhà thuốc thế hệ mới';

export function articleOgImagePath(slug: string): string {
  return `/images/tin-tuc/${slug}.png`;
}

export function articleOgImageUrl(slug: string): string {
  return absoluteUrl(articleOgImagePath(slug));
}

export function buildArticleJsonLd(post: {
  id: string;
  data: { title: string; description: string; pubDate: Date };
}) {
  const url = absoluteUrl(`/vi/tin-tuc/${post.id}/`);
  const image = articleOgImageUrl(post.id);

  return {
    '@context': 'https://schema.org',
    '@type': 'Article',
    headline: post.data.title,
    description: post.data.description,
    datePublished: post.data.pubDate.toISOString(),
    dateModified: post.data.pubDate.toISOString(),
    inLanguage: 'vi-VN',
    mainEntityOfPage: { '@type': 'WebPage', '@id': url },
    url,
    image: [image],
    author: {
      '@type': 'Organization',
      name: SITE_NAME,
      url: SITE_URL,
    },
    publisher: {
      '@type': 'Organization',
      name: SITE_NAME,
      url: SITE_URL,
      logo: {
        '@type': 'ImageObject',
        url: absoluteUrl('/images/logo.png'),
      },
    },
  };
}
