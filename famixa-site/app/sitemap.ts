import type { MetadataRoute } from 'next';
import { SITE_URL } from '@/lib/site';
import { SPECIALTY } from '@/lib/specialty-routes';

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

  return [...home, ...specialty, ...specialtyEn];
}
