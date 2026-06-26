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
          if (id.includes('react-dom') || id.includes('react-router')) return 'react-vendor';
          if (id.includes('axios') || id.includes('zustand') || id.includes('dayjs')) return 'utils-vendor';
          return 'vendor';
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
      },
      '/uploads': {
        target: 'http://localhost:5290',
        changeOrigin: true,
      },
    },
  },
});
