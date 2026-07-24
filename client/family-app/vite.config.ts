import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const rootDir = path.dirname(fileURLToPath(import.meta.url));

export default defineConfig({
  plugins: [react()],
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
