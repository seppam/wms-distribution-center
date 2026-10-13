import process from 'node:process';
import { expect, test } from '@playwright/test';
import { dialog, toast } from '../helpers';

// The browser-only demo: static files, no API, no database. Every request must stay on the page's own origin and none may hit /api.

const ORIGIN = (process.env.DEMO_ORIGIN || 'http://localhost:5176').replace(/\/$/, '');
let requests;
let cspViolations;

test.beforeEach(async ({ page }) => {
  requests = [];
  cspViolations = [];
  page.on('console', (m) => { if (/Content Security Policy|Refused to/i.test(m.text())) cspViolations.push(m.text()); });
  page.on('request', (r) => requests.push(r.url()));
});
test.afterEach(() => {
  const external = requests.filter((u) => !u.startsWith(ORIGIN) && !u.startsWith('data:') && !u.startsWith('blob:'));
  expect(external, 'requests that left the page origin').toEqual([]);
  expect(cspViolations, 'Content-Security-Policy violations').toEqual([]);
  expect(requests.filter((u) => new URL(u).pathname.startsWith('/api')), 'requests to an API').toEqual([]);
});

async function signInAs(page, role) {
  await page.goto('/login');
  const panel = page.getByRole('region', { name: 'Demo mode' });
  await expect(panel).toBeVisible();
  await panel.getByRole('button', { name: `Sign in as ${role}` }).click();
  await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible();
}

test('the login page shows the browser-demo banner and one button per role', async ({ page }) => {
  await page.goto('/login');
  await expect(page.getByRole('region', { name: 'Browser demo' })).toContainText('Demo version: runs entirely in your browser. No server and no database; data is saved in your browser (localStorage).');
  const repo = 'https://github.com/seppam/wms-distribution-center';
  for (const scope of [page.getByRole('region', { name: 'Browser demo' }), page.getByRole('region', { name: 'Demo mode' })]) {
    const link = scope.getByRole('link', { name: /Run the full project on your own computer/ });
    await expect(link).toBeVisible();
    await expect(link).toHaveAttribute('href', repo);
    await expect(link).toHaveAttribute('target', '_blank');
    await expect(link).toHaveAttribute('rel', /noopener/);
  }
  await expect(page).toHaveTitle('WMS Distribution Center (browser demo)');
  await expect(page.locator('meta[name=description]')).toHaveAttribute('content', /fictional data/);
  for (const role of ['Admin', 'Supervisor', 'Operator', 'Viewer'])
    await expect(page.getByRole('button', { name: `Sign in as ${role}` })).toBeVisible();
});

for (const role of ['Admin', 'Supervisor', 'Operator', 'Viewer']) {
  test(`sign in as ${role}`, async ({ page }) => {
    await signInAs(page, role);
    await expect(page.getByRole('region', { name: 'Browser demo' })).toBeVisible();
    await page.goto('/outbound');
    const create = page.getByRole('button', { name: /^New/ });
    if (role === 'Admin' || role === 'Supervisor') await expect(create).toBeVisible();
    else await expect(create).toHaveCount(0);
  });
}

test('the typed login form works too, and a wrong password is refused', async ({ page }) => {
  await page.goto('/login');
  await page.getByLabel('Username').fill('operator');
  await page.getByLabel('Password').fill('wrong');
  await page.getByRole('button', { name: 'Sign In', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Invalid username or password');
  await page.getByLabel('Password').fill('Demo!Pass123');
  await page.getByRole('button', { name: 'Sign In', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible();
});

test('receive to ship happy path, persists across a reload, then reset', async ({ page }) => {
  await signInAs(page, 'Supervisor');

  // receive the planned delivery
  await page.goto('/inbound');
  const rcv = () => page.getByRole('row').filter({ hasText: 'PO-2413' });
  await rcv().getByRole('button', { name: 'Open' }).click();
  const receipt = dialog(page, /Receipt RCV-/);
  for (let i = 0; i < 2; i++) {
    await receipt.getByRole('button', { name: 'Receive', exact: true }).first().click();
    await dialog(page, /Receive /).getByRole('button', { name: 'Receive and put away' }).click();
    await expect(toast(page, 'Goods received and put away').first()).toBeVisible();
  }
  await receipt.getByRole('button', { name: 'Complete receipt' }).click();
  await expect(toast(page, 'Receipt completed as Received')).toBeVisible();
  await receipt.getByRole('button', { name: 'Close' }).first().click();
  await expect(rcv().getByText('Received')).toBeVisible();

  // allocate a draft order, then pick, pack, ship and deliver it
  await page.goto('/outbound');
  const row = () => page.getByRole('row').filter({ hasText: 'Brightside Kindergarten' });
  await row().getByRole('button', { name: 'Open' }).click();
  const order = dialog(page, /Order ORD-/);
  await order.getByRole('button', { name: 'Allocate stock and start picking' }).click();
  await expect(toast(page, 'Stock allocated: pick tasks created')).toBeVisible();
  const orderNumber = (await order.getByRole('heading').first().textContent()).replace('Order ', '').trim();
  await order.getByRole('button', { name: 'Close' }).first().click();

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
  await dialog(page, /Order ORD-/).getByRole('button', { name: 'Ship' }).click();
  const ship = dialog(page, /Ship/);
  await ship.locator('select').first().selectOption({ index: 1 });
  await ship.getByRole('button', { name: /^Ship/ }).last().click();
  await expect(toast(page, /shipped/i)).toBeVisible();
  await dialog(page, /Order ORD-/).getByRole('button', { name: 'Close' }).first().click();
  await expect(row().getByText('Shipped')).toBeVisible();

  // the data survives a reload (localStorage)
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Dashboard' }).or(page.getByRole('heading', { name: /Outbound|Orders/ })).first()).toBeVisible();
  await page.goto('/outbound');
  await expect(row().getByText('Shipped')).toBeVisible();
  await page.goto('/inbound');
  await expect(rcv().getByText('Received')).toBeVisible();

  // reset restores the original dataset
  await page.getByRole('button', { name: 'Reset demo data' }).click();
  await dialog(page, 'Reset demo data?').getByRole('button', { name: 'Reset demo data' }).click();
  await expect(toast(page, 'Demo data restored')).toBeVisible();
  await expect(rcv().getByText('Draft')).toBeVisible();
  await page.goto('/outbound');
  await expect(row().getByText('Draft')).toBeVisible();
});

test('About this demo explains the simulation and links to the repo', async ({ page }) => {
  await signInAs(page, 'Viewer');
  await page.getByRole('button', { name: 'About this demo' }).click();
  const about = dialog(page, 'About this demo');
  await expect(about).toContainText('reimplemented in TypeScript');
  await expect(about).toContainText('ASP.NET Core 9 API + PostgreSQL');
  await expect(about).toContainText('docker compose -f docker-compose.demo.yml up --build');
  const readme = about.getByRole('link', { name: /repository README/ });
  await expect(readme).toHaveAttribute('href', 'https://github.com/seppam/wms-distribution-center#readme');
  await expect(readme).toHaveAttribute('target', '_blank');
  await expect(readme).toHaveAttribute('rel', /noopener/);
});

test('deep links work (SPA fallback) and a stale session is sent to login', async ({ page }) => {
  await page.goto('/inventory');
  await expect(page).toHaveURL(/\/login$/);
});
