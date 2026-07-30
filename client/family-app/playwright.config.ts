import { defineConfig, devices } from '@playwright/test';

/**
 * UI smoke for the family app. Assumes the Vite dev server and the API are
 * already running locally (`npm run dev`), since the API needs a seeded
 * DEMO_FAMILY tenant that the test suite does not provision itself.
 */
export default defineConfig({
  testDir: './tests',
  timeout: 90_000,
  expect: { timeout: 15_000 },
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [['list']],
  use: {
    baseURL: process.env.APP_BASE ?? 'http://localhost:5178',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'phone',
      use: { ...devices['Pixel 7'] },
    },
    {
      name: 'desktop',
      use: { viewport: { width: 1024, height: 720 } },
    },
  ],
});
