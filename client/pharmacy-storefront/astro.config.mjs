// @ts-check
import { defineConfig } from 'astro/config';
import cloudflare from '@astrojs/cloudflare';

// Cloudflare Workers SSR (Astro 7 adapter is Workers-only — not Pages Functions).
export default defineConfig({
  site: 'https://novixa.vn',
  output: 'server',
  adapter: cloudflare({
    imageService: 'compile',
  }),
  session: false,
  trailingSlash: 'ignore',
  server: {
    port: 4330,
  },
});
