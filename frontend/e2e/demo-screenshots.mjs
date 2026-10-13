// Captures the README screenshots from the public demo stack (fictional data only).
// Usage: node e2e/demo-screenshots.mjs <output-dir>      (demo stack running, see README; reset the demo data first)
import { chromium } from '@playwright/test';
import process from 'node:process';

const WEB = process.env.E2E_BASE_URL || 'http://localhost:5174';
const out = process.argv[2];
if (!out) throw new Error('Usage: node e2e/demo-screenshots.mjs <output-dir>');

const browser = await chromium.launch({ channel: process.env.PW_CHANNEL || 'chrome' });
const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
const shot = async (name) => { await page.waitForTimeout(600); await page.screenshot({ path: `${out}/${name}.png` }); };
const open = async (path, rowText) => {
  await page.goto(`${WEB}${path}`);
  await page.getByRole('row').filter({ hasText: rowText }).getByRole('button', { name: 'Open' }).click();
  await page.getByRole('dialog').waitFor();
};

await page.goto(`${WEB}/login`);
await page.getByRole('region', { name: 'Demo mode' }).waitFor();
await shot('01-login');

await page.getByRole('button', { name: 'Sign in as Supervisor' }).click();
await page.getByRole('heading', { name: 'Dashboard' }).waitFor();
await shot('02-dashboard');

await page.goto(`${WEB}/inventory`); await page.getByRole('row').nth(2).waitFor(); await shot('03-inventory');
await open('/inbound', 'PO-2411'); await shot('04-receipt-detail');
await page.goto(`${WEB}/outbound`); await page.getByRole('row').nth(5).waitFor(); await shot('05-outbound');
await open('/outbound', 'Central Canteen'); await shot('06-order-picking');
await page.goto(`${WEB}/picks`); await page.getByRole('row').nth(2).waitFor(); await shot('07-pick-tasks');
await page.goto(`${WEB}/adjustments`); await page.getByRole('row').nth(2).waitFor(); await shot('08-adjustments');
await page.goto(`${WEB}/transfers`); await page.getByRole('row').nth(2).waitFor(); await shot('09-transfers');
await page.goto(`${WEB}/returns`); await page.getByRole('row').nth(2).waitFor(); await shot('10-returns');

await browser.close();
console.log('screenshots written to', out);
