import { expect, test } from '@playwright/test';
import { dialog, toast } from '../helpers';

// The "Try it in 3 minutes" tour from the README, against the demo stack (docker-compose.demo.yml).

async function signInAs(page, role) {
  await page.goto('/login');
  const panel = page.getByRole('region', { name: 'Demo mode' });
  await expect(panel).toBeVisible();
  await panel.getByRole('button', { name: `Sign in as ${role}` }).click();
  await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible();
}

test('login shows the demo panel with one button per role', async ({ page }) => {
  await page.goto('/login');
  const panel = page.getByRole('region', { name: 'Demo mode' });
  await expect(panel).toBeVisible();
  for (const role of ['Admin', 'Supervisor', 'Operator', 'Viewer'])
    await expect(panel.getByRole('button', { name: `Sign in as ${role}` })).toBeVisible();
  await page.screenshot({ path: 'test-results/demo-login.png' });
});

test('orders exist in every workflow state', async ({ page }) => {
  await signInAs(page, 'Supervisor');
  await expect(page.getByRole('region', { name: 'Demo mode' })).toContainText('fictional data that resets');
  await page.goto('/outbound');
  for (const status of ['Draft', 'Picking', 'Picked', 'Packed', 'Shipped', 'Completed', 'Cancelled'])
    await expect(page.getByRole('row').filter({ hasText: status }).first()).toBeVisible();
});

test('a viewer can look around but not reset or change anything', async ({ page }) => {
  await signInAs(page, 'Viewer');
  await expect(page.getByRole('button', { name: 'Reset demo data' })).toHaveCount(0);
  await page.goto('/outbound');
  await expect(page.getByRole('button', { name: /^New/ })).toHaveCount(0);
});

test('supervisor allocates a draft order and the operator picks and packs it', async ({ page }) => {
  await signInAs(page, 'Supervisor');
  await page.goto('/outbound');
  const row = () => page.getByRole('row').filter({ hasText: 'Brightside Kindergarten' });
  await expect(row().getByText('Draft')).toBeVisible();
  await row().getByRole('button', { name: 'Open' }).click();
  const order = dialog(page, /Order ORD-/);
  await order.getByRole('button', { name: 'Allocate stock and start picking' }).click();
  await expect(toast(page, 'Stock allocated: pick tasks created')).toBeVisible();
  const orderNumber = (await order.getByRole('heading').first().textContent()).replace('Order ', '').trim();
  await order.getByRole('button', { name: 'Close' }).first().click();

  await page.getByRole('button', { name: 'Logout' }).click();
  await signInAs(page, 'Operator');
  await page.goto('/picks');
  const tasks = page.getByRole('row').filter({ hasText: orderNumber });
  await expect(tasks.first()).toBeVisible();
  const count = await tasks.count();
  expect(count).toBeGreaterThan(0);
  for (let i = 0; i < count; i++) {
    await page.getByRole('row').filter({ hasText: orderNumber }).first().getByRole('button', { name: 'Confirm' }).click();
    await expect(toast(page, 'Pick confirmed').first()).toBeVisible();
    await expect(tasks).toHaveCount(count - i - 1);
  }
  await page.goto('/outbound');
  await row().getByRole('button', { name: 'Open' }).click();
  await dialog(page, /Order ORD-/).getByRole('button', { name: 'Pack' }).click();
  const pack = dialog(page, 'Pack order');
  await pack.getByLabel('Weight (kg)').fill('9');
  await pack.getByRole('button', { name: 'Pack', exact: true }).click();
  await expect(toast(page, 'Order packed')).toBeVisible();
});

test('admin receives a planned delivery, then resets the demo data', async ({ page }) => {
  await signInAs(page, 'Admin');

  await page.goto('/inbound');
  const row = () => page.getByRole('row').filter({ hasText: 'PO-2413' });
  await expect(row().getByText('Draft')).toBeVisible();
  await row().getByRole('button', { name: 'Open' }).click();
  const receipt = dialog(page, /Receipt RCV-/);
  for (let i = 0; i < 2; i++) {
    await receipt.getByRole('button', { name: 'Receive', exact: true }).first().click();
    await dialog(page, /Receive /).getByRole('button', { name: 'Receive and put away' }).click();
    await expect(toast(page, 'Goods received and put away').first()).toBeVisible();
  }
  await receipt.getByRole('button', { name: 'Complete receipt' }).click();
  await expect(toast(page, 'Receipt completed as Received')).toBeVisible();
  await receipt.getByRole('button', { name: 'Close' }).first().click();
  await expect(row().getByText('Received')).toBeVisible();

  // reset restores the planned delivery as a draft
  await page.getByRole('button', { name: 'Reset demo data' }).click();
  await dialog(page, 'Reset demo data?').getByRole('button', { name: 'Reset demo data' }).click();
  await expect(toast(page, 'Demo data restored')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Receipts' }).or(page.getByRole('heading', { name: /Inbound/ })).first()).toBeVisible();
  await expect(row().getByText('Draft')).toBeVisible();
});
