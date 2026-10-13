import { defineConfig } from '@playwright/test';
import process from 'node:process';

// Runs against the public demo stack: `docker compose -f docker-compose.demo.yml up --build` (web on :5174),
// or scripts/demo.sh (web on :5173). Set E2E_BASE_URL to point somewhere else.
export default defineConfig({
  testDir: './e2e/demo',
  testMatch: /.*\.spec\.js/,
  timeout: 90_000,
  expect: { timeout: 10_000 },
  workers: 1, // the specs share one database and one of them resets it
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: process.env.E2E_BASE_URL || 'http://localhost:5174',
    channel: process.env.PW_CHANNEL || undefined,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
});
