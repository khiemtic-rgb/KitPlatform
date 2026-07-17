import { defineConfig } from 'astro/config';
import sitemap from '@astrojs/sitemap';

const isProd = process.env.NODE_ENV === 'production';

// https://astro.build/config
export default defineConfig({
  site: 'https://novixa.vn',
  // Production: always trailing slash (matches Cloudflare Pages + canonicals).
  // Local: ignore so /vi/giai-phap and /vi/giai-phap/ both work in `astro dev`.
  trailingSlash: isProd ? 'always' : 'ignore',
  integrations: [
    sitemap({
      filter: (page) => {
        try {
          const path = new URL(page).pathname;
          if (path === '/') return false;
          if (path.includes('/404')) return false;
          if (path.includes('/thong-ke')) return false;
          if (path.includes('/health-check')) return false;
          if (path.includes('/spa-health-check')) return false;
          return true;
        } catch {
          return false;
        }
      },
    }),
  ],
  i18n: {
    defaultLocale: 'vi',
    locales: ['vi'],
    routing: {
      prefixDefaultLocale: true,
    },
  },
});
