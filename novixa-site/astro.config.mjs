import { defineConfig } from 'astro/config';
import sitemap from '@astrojs/sitemap';

// https://astro.build/config
export default defineConfig({
  site: 'https://novixa.vn',
  trailingSlash: 'never',
  integrations: [
    sitemap({
      filter: (page) => !page.includes('/404') && !page.includes('/thong-ke'),
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
