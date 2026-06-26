import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { VitePWA } from 'vite-plugin-pwa';
import { fileURLToPath, URL } from 'node:url';

export default defineConfig({
  plugins: [
    react(),
    // Dev chạy http://localhost:5174 — localhost là secure context (push + SW).
    // basicSsl gây lỗi "SSL certificate error" khi fetch dev-sw.js.
    VitePWA({
      registerType: 'autoUpdate',
      includeAssets: ['icon.svg'],
      strategies: 'injectManifest',
      srcDir: 'src',
      filename: 'sw.ts',
      manifest: {
        name: 'PharmaCore Khách hàng',
        short_name: 'PharmaCore',
        description: 'App khách hàng — điểm thưởng, nhắc uống thuốc',
        theme_color: '#0f766e',
        background_color: '#f0fdfa',
        display: 'standalone',
        lang: 'vi',
        start_url: '/',
        icons: [
          {
            src: '/icon.svg',
            sizes: '512x512',
            type: 'image/svg+xml',
            purpose: 'any',
          },
          {
            src: '/icon.svg',
            sizes: '512x512',
            type: 'image/svg+xml',
            purpose: 'maskable',
          },
        ],
      },
      devOptions: {
        enabled: true,
        type: 'module',
      },
      injectRegister: 'auto',
    }),
  ],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
    dedupe: ['dayjs', 'react', 'react-dom'],
  },
  server: {
    port: 5174,
    host: true,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://localhost:5290',
        changeOrigin: true,
      },
    },
  },
});
