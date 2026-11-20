import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { fileURLToPath, URL } from 'node:url';

const outDir = process.env.SB_HOST_OUT?.trim() || 'dist-second-brain-host';

export default defineConfig({
  plugins: [react()],
  define: {
    'import.meta.env.VITE_API_BASE_URL': JSON.stringify(''),
  },
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
    dedupe: ['dayjs', 'react', 'react-dom'],
  },
  build: {
    outDir,
    emptyOutDir: true,
    rollupOptions: {
      input: fileURLToPath(new URL('./second-brain-host/index.html', import.meta.url)),
    },
  },
});
