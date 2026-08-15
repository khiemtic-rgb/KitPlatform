import { defineConfig } from 'astro/config';
import cloudflare from '@astrojs/cloudflare';

export default defineConfig({
  site: 'https://thainguyenlife.vn',
  output: 'server',
  adapter: cloudflare({
    imageService: 'compile',
  }),
  trailingSlash: 'ignore',
  server: {
    host: '127.0.0.1',
    port: 4322,
  },
});
