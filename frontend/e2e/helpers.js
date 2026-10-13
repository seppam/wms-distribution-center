import { expect } from '@playwright/test';
import process from 'node:process';

export const DEMO_PASSWORD = process.env.E2E_DEMO_PASSWORD || 'Demo!Pass123';

export async function login(page, username = 'admin') {
  await page.goto('/login');
  await page.getByLabel('Username').fill(username);
  await page.getByLabel('Password').fill(DEMO_PASSWORD);
  await page.getByRole('button', { name: 'Sign In' }).click();
  await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible();
}

/** Selects the option whose visible text matches the pattern (waits for async options to load). */
export async function pick(select, pattern) {
  const re = pattern instanceof RegExp ? pattern : new RegExp(pattern.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'));
  let hit;
  let seen = '';
  await expect.poll(async () => {
    const options = await select.locator('option').evaluateAll((els) => els.map((e) => ({ value: e.value, text: e.textContent })));
    hit = options.find((o) => o.value && re.test(o.text));
    seen = options.map((o) => o.text).join(' | ');
    return Boolean(hit);
  }, { timeout: 10_000, message: () => `no option matching ${re} in: ${seen}` }).toBe(true);
  await select.selectOption(hit.value);
}

export const dialog = (page, name) => page.getByRole('dialog', { name });
export const toast = (page, text) => page.getByRole('status').getByText(text);
