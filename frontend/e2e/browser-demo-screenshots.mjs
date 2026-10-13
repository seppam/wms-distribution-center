// Captures the README screenshots of the browser-only demo (fictional data only).
// Usage: node e2e/browser-demo-screenshots.mjs <output-dir>   (static build served on E2E_BASE_URL, default http://localhost:5176)
import { chromium } from '@playwright/test';
import process from 'node:process';

const WEB = process.env.E2E_BASE_URL || 'http://localhost:5176';
const out = process.argv[2];
if (!out) throw new Error('Usage: node e2e/browser-demo-screenshots.mjs <output-dir>');
const browser = await chromium.launch({ channel: process.env.PW_CHANNEL || 'chrome' });
const page = await browser.newPage({ viewport: { width: 1280, height: 800 } });
const shot = async (name) => { await page.waitForTimeout(500); await page.screenshot({ path: `${out}/${name}.png` }); };

await page.goto(`${WEB}/login`);
await page.getByRole('region', { name: 'Browser demo' }).waitFor();
await shot('11-browser-demo-login');
await page.getByRole('button', { name: 'Sign in as Supervisor' }).click();
await page.getByRole('heading', { name: 'Dashboard' }).waitFor();
await shot('12-browser-demo-dashboard');
await page.getByRole('button', { name: 'About this demo' }).click();
await page.getByRole('dialog').waitFor();
await shot('13-browser-demo-about');
await browser.close();
