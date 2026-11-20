import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { fileURLToPath, URL } from 'node:url';

/** Local knowledge demo only. Proxies to the disposable API, not the shared dev server. */
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
    dedupe: ['dayjs', 'react', 'react-dom'],
  },
  server: {
    port: 5176,
    host: '127.0.0.1',
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://127.0.0.1:5297',
        changeOrigin: true,
      },
      '/uploads': {
        target: 'http://127.0.0.1:5297',
        changeOrigin: true,
      },
    },
  },
});
