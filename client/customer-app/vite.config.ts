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
      includeAssets: ['icon-512.png', 'icon-192.png', 'apple-touch-icon.png'],
      strategies: 'injectManifest',
      srcDir: 'src',
      filename: 'sw.ts',
      // Đăng ký SW thủ công (sw-registration) — tránh inject thêm script race critical path.
      injectRegister: false,
      injectManifest: {
        // Precache shell + vendor; bỏ lazy route/icon chunks (tải khi mở trang).
        globPatterns: ['**/*.{js,css,html,ico,png,svg,webp,woff2,webmanifest}'],
        globIgnores: [
          '**/assets/*Page-*.js',
          '**/assets/*Inbox-*.js',
          '**/assets/*Panel-*.js',
          '**/assets/*Outlined-*.js',
          '**/assets/*Filled-*.js',
          '**/assets/*TwoTone-*.js',
        ],
      },
      manifest: {
        name: 'Novixa Khách hàng',
        short_name: 'Novixa',
        description: 'App khách hàng — điểm thưởng, nhắc uống thuốc',
        theme_color: '#0f766e',
        background_color: '#ffffff',
        display: 'standalone',
        lang: 'vi',
        start_url: '/',
        icons: [
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
            src: '/icon-512.png',
            sizes: '512x512',
            type: 'image/png',
            purpose: 'maskable',
          },
        ],
      },
      devOptions: {
        enabled: true,
        type: 'module',
      },
    }),
  ],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
    dedupe: ['dayjs', 'react', 'react-dom'],
  },
  build: {
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (!id.includes('node_modules')) return;
          // Chỉ tách React — đừng nhét cả antd vào một chunk (mất tree-shake, phình ~1MB).
          if (
            id.includes('node_modules/react/') ||
            id.includes('node_modules/react-dom/') ||
            id.includes('node_modules/scheduler/') ||
            id.includes('react-router')
          ) {
            return 'react-vendor';
          }
        },
      },
    },
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
      '/uploads': {
        target: 'http://localhost:5290',
        changeOrigin: true,
      },
    },
  },
});
