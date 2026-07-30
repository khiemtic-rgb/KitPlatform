import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { VitePWA } from 'vite-plugin-pwa';
import { writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const rootDir = path.dirname(fileURLToPath(import.meta.url));

// Same stamp lands in the bundle and in /version.json, so a stale client can
// detect it is behind and force a reload instead of waiting for the SW.
const BUILD_STAMP = process.env.VITE_APP_BUILD || new Date().toISOString();

function familyAppVersionJson() {
  return {
    name: 'family-app-version-json',
    closeBundle() {
      const outDir = path.resolve(rootDir, 'dist');
      writeFileSync(`${outDir}/version.json`, JSON.stringify({ build: BUILD_STAMP }));
    },
  };
}

function familyAppIconCacheBust() {
  return {
    name: 'family-app-icon-cache-bust',
    transformIndexHtml(html: string) {
      return html.replaceAll('__APP_BUILD__', encodeURIComponent(BUILD_STAMP));
    },
  };
}

export default defineConfig({
  define: {
    'import.meta.env.VITE_APP_BUILD': JSON.stringify(BUILD_STAMP),
  },
  plugins: [
    react(),
    familyAppVersionJson(),
    familyAppIconCacheBust(),
    VitePWA({
      registerType: 'autoUpdate',
      includeAssets: [
        'favicon.ico',
        'favicon-32.png',
        'favicon-48.png',
        'apple-touch-icon.png',
        'icon-192.png',
        'icon-512.png',
        'brand/fami-mark-48.png',
      ],
      strategies: 'injectManifest',
      srcDir: 'src',
      filename: 'sw.ts',
      manifest: {
        name: 'Famixa',
        short_name: 'Famixa',
        description: 'AI giúp gia đình hạnh phúc hơn mỗi ngày',
        theme_color: '#0B5C3A',
        background_color: '#ffffff',
        display: 'standalone',
        lang: 'vi',
        start_url: '/',
        icons: [
          {
            src: '/favicon-32.png',
            sizes: '32x32',
            type: 'image/png',
            purpose: 'any',
          },
          {
            src: '/icon-192.png',
            sizes: '192x192',
            type: 'image/png',
            purpose: 'any',
          },
          {
            src: '/icon-512.png',
            sizes: '512x512',
            type: 'image/png',
            purpose: 'any',
          },
          {
            src: '/apple-touch-icon.png',
            sizes: '180x180',
            type: 'image/png',
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
