import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { VitePWA } from 'vite-plugin-pwa';
import { writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const rootDir = path.dirname(fileURLToPath(import.meta.url));

function familyAppVersionJson() {
  return {
    name: 'family-app-version-json',
    closeBundle() {
      const outDir = path.resolve(rootDir, 'dist');
      const build = process.env.VITE_APP_BUILD || new Date().toISOString();
      writeFileSync(`${outDir}/version.json`, JSON.stringify({ build }));
    },
  };
}

export default defineConfig({
  plugins: [
    react(),
    familyAppVersionJson(),
    VitePWA({
      registerType: 'autoUpdate',
      includeAssets: ['icon.svg'],
      strategies: 'injectManifest',
      srcDir: 'src',
      filename: 'sw.ts',
      manifest: {
        name: 'FamilyOS',
        short_name: 'FamilyOS',
        description: 'One Family. One Plan. One Daily Flow.',
        theme_color: '#1d6a6a',
        background_color: '#f3f7f4',
        display: 'standalone',
        lang: 'vi',
        start_url: '/',
        icons: [
          {
            src: '/icon.svg',
            sizes: 'any',
            type: 'image/svg+xml',
            purpose: 'any',
          },
        ],
      },
      devOptions: {
        enabled: true,
        type: 'module',
      },
      injectRegister: false,
    }),
  ],
  resolve: {
    alias: {
      '@': path.resolve(rootDir, 'src'),
    },
  },
  server: {
    port: 5178,
    host: true,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://localhost:5290',
        changeOrigin: true,
      },
      '/uploads': {
        target: 'http://localhost:5290',
        changeOrigin: true,
      },
    },
  },
});
