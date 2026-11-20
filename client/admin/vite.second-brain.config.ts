import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { fileURLToPath, URL } from 'node:url';

const api = process.env.SB_TRADING_API_URL?.trim() ?? '';
let target: URL;
try {
  target = new URL(api);
} catch {
  throw new Error('SB_TRADING_API_URL must be an absolute http URL for the Second Brain validation API.');
}
if (target.protocol !== 'http:' || (target.hostname !== '127.0.0.1' && target.hostname !== 'localhost')) {
  throw new Error('The Second Brain UI proxy only accepts a loopback HTTP API.');
}
if (target.port === '' || target.port === '5290' || target.port === '7224' || target.port === '5432' || target.port === '5433') {
  throw new Error('The Second Brain UI proxy must not use the shared API or database port.');
}
const uiPort = Number(process.env.SB_TRADING_UI_PORT || '5174');

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
  server: {
    host: '127.0.0.1',
    port: uiPort,
    strictPort: true,
    proxy: {
      '/api': {
        target: target.origin,
        changeOrigin: true,
        timeout: 1_080_000,
        proxyTimeout: 1_080_000,
      },
      '/uploads': {
        target: target.origin,
        changeOrigin: true,
      },
    },
  },
});
