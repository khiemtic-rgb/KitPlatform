// @ts-check
import { defineConfig } from 'astro/config';

// https://astro.build/config
export default defineConfig({
  site: 'https://xuanhoa.novixa.vn',
  trailingSlash: 'ignore',
  server: {
    port: 4330,
  },
});
