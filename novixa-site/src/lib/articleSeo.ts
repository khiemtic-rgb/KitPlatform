import { absoluteUrl, SITE_NAME, SITE_URL } from './site';

export const BRAND_TAGLINE = 'Novixa — Nền tảng quản trị nhà thuốc thế hệ mới';

/** Ảnh mặc định theo slug nếu bài không khai báo `image` trong frontmatter. */
export function articleOgImagePath(slug: string): string {
  return `/images/tin-tuc/${slug}.png`;
}

export function resolveArticleImage(
  slug: string,
  image?: string | null,
): string {
  let raw = image?.trim();
  if (!raw) return articleOgImagePath(slug);
  // CMS đôi khi lưu thiếu dấu / đầu
  if (!raw.startsWith('http://') && !raw.startsWith('https://') && !raw.startsWith('/')) {
    raw = `/${raw}`;
  }
  // Bỏ prefix monorepo nếu CMS lỡ ghi path repo
  raw = raw.replace(/^\/?novixa-site\/public/, '');
  if (raw.startsWith('http://') || raw.startsWith('https://')) return raw;
  return raw.startsWith('/') ? raw : `/${raw}`;
}

export function articleOgImageUrl(slug: string, image?: string | null): string {
  const path = resolveArticleImage(slug, image);
  if (path.startsWith('http://') || path.startsWith('https://')) return path;
  return absoluteUrl(path);
}

export function buildArticleJsonLd(post: {
  id: string;
  data: { title: string; description: string; pubDate: Date; image?: string };
}) {
  const url = absoluteUrl(`/vi/tin-tuc/${post.id}/`);
  const image = articleOgImageUrl(post.id, post.data.image);

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
