// @ts-check
import { defineConfig } from 'astro/config';

// Static Pages deploy (Astro 7 Cloudflare adapter is Workers-only).
// Multi-tenant Host SSR will return via wrangler deploy (Workers) later.
export default defineConfig({
  site: 'https://xuanhoa.novixa.vn',
  trailingSlash: 'ignore',
  server: {
    port: 4330,
  },
});
