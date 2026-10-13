import { defineConfig } from '@playwright/test';
import process from 'node:process';

// Runs against the STATIC build of the browser-only demo (npm run build:browser-demo), served by `vite preview` on :5176.
// No API, no database: the page itself answers every request.
export default defineConfig({
  testDir: './e2e/browser-demo',
  testMatch: /.*\.spec\.js/,
  timeout: 90_000,
  expect: { timeout: 10_000 },
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  webServer: {
    command: 'npm run build:browser-demo && npm run preview:browser-demo',
    url: 'http://localhost:5176/login',
    reuseExistingServer: false,
    timeout: 180_000,
  },
  use: {
    baseURL: 'http://localhost:5176',
    channel: process.env.PW_CHANNEL || undefined,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
});
