import { defineConfig } from '@playwright/test';
import process from 'node:process';

// The end-to-end test expects the API (demo seed, Development) and the Vite dev server to be running.
// See the README ("Tests") or .github/workflows/ci.yml for how they are started.
export default defineConfig({
  testDir: './e2e',
  testMatch: /.*\.spec\.js/,
  testIgnore: /demo\//, // the demo specs need demo mode: see playwright.demo.config.js
  timeout: 90_000,
  expect: { timeout: 10_000 },
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: process.env.E2E_BASE_URL || 'http://localhost:5173',
    // PW_CHANNEL=chrome reuses an installed Google Chrome instead of the downloaded Chromium
    channel: process.env.PW_CHANNEL || undefined,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
});
