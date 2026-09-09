import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { fileURLToPath, URL } from 'node:url';

export default defineConfig({
  plugins: [react()],
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
          if (id.includes('antd') || id.includes('@ant-design')) return 'antd';
        },
      },
    },
  },
  server: {
    port: 5173,
    host: true,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://localhost:5290',
        changeOrigin: true,
        timeout: 1_080_000,
        proxyTimeout: 1_080_000,
      },
      '/uploads': {
        target: 'http://localhost:5290',
        changeOrigin: true,
      },
    },
  },
});
