import type { MetadataRoute } from 'next';
import { SITE_URL } from '@/lib/site';
import { SPECIALTY } from '@/lib/specialty-routes';
import { getGuideHubArticles } from '@/lib/guide/content';
import { getBlogArticles, getBlogHubMeta } from '@/lib/blog/content';

export const dynamic = 'force-static';

export default function sitemap(): MetadataRoute.Sitemap {
  const base = SITE_URL.replace(/\/$/, '');
  const now = new Date();

  const home: MetadataRoute.Sitemap = [
    {
      url: `${base}/vi/`,
      lastModified: now,
      changeFrequency: 'weekly',
      priority: 1,
      alternates: {
        languages: { vi: `${base}/vi/`, en: `${base}/en/` },
      },
    },
    {
      url: `${base}/en/`,
      lastModified: now,
      changeFrequency: 'weekly',
      priority: 0.95,
      alternates: {
        languages: { vi: `${base}/vi/`, en: `${base}/en/` },
      },
    },
    {
      url: `${base}/`,
      lastModified: now,
      changeFrequency: 'weekly',
      priority: 0.8,
    },
  ];

  const specialty: MetadataRoute.Sitemap = (Object.keys(SPECIALTY) as Array<keyof typeof SPECIALTY>).map(
    (key) => {
      const vi = `${base}${SPECIALTY[key].vi}`;
      const en = `${base}${SPECIALTY[key].en}`;
      return {
        url: vi,
        lastModified: now,
        changeFrequency: 'monthly' as const,
        priority: key === 'plans' || key === 'stories' ? 0.85 : 0.7,
        alternates: {
          languages: { vi, en },
        },
      };
    },
  );

  const specialtyEn: MetadataRoute.Sitemap = (Object.keys(SPECIALTY) as Array<keyof typeof SPECIALTY>).map(
    (key) => ({
      url: `${base}${SPECIALTY[key].en}`,
      lastModified: now,
      changeFrequency: 'monthly' as const,
      priority: key === 'plans' || key === 'stories' ? 0.8 : 0.65,
    }),
  );

  const guide: MetadataRoute.Sitemap = [
    {
      url: `${base}/vi/huong-dan/`,
      lastModified: now,
      changeFrequency: 'weekly',
      priority: 0.85,
    },
    ...getGuideHubArticles().map((article) => ({
      url: `${base}${article.slug}/`,
      lastModified: new Date(article.last_verified),
      changeFrequency: 'monthly' as const,
      priority: 0.7,
    })),
  ];

  const blogHub = getBlogHubMeta();
  const blog: MetadataRoute.Sitemap = [
    {
      url: `${base}${blogHub.slug}`,
      lastModified: now,
      changeFrequency: 'weekly',
      priority: 0.85,
    },
    ...getBlogArticles().map((article) => ({
      url: `${base}${article.slug}/`,
      lastModified: new Date(article.pubDate),
      changeFrequency: 'monthly' as const,
      priority: 0.75,
    })),
  ];

  return [...home, ...specialty, ...specialtyEn, ...guide, ...blog];
}
