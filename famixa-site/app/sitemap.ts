import type { MetadataRoute } from 'next';
import { SITE_URL } from '@/lib/site';

export const dynamic = 'force-static';

export default function sitemap(): MetadataRoute.Sitemap {
  const base = SITE_URL.replace(/\/$/, '');
  return [
    {
      url: `${base}/vi/`,
      lastModified: new Date(),
      changeFrequency: 'weekly',
      priority: 1,
      alternates: {
        languages: {
          vi: `${base}/vi/`,
          en: `${base}/en/`,
        },
      },
    },
    {
      url: `${base}/en/`,
      lastModified: new Date(),
      changeFrequency: 'weekly',
      priority: 0.95,
      alternates: {
        languages: {
          vi: `${base}/vi/`,
          en: `${base}/en/`,
        },
      },
    },
    {
      url: `${base}/`,
      lastModified: new Date(),
      changeFrequency: 'weekly',
      priority: 0.8,
    },
  ];
}
