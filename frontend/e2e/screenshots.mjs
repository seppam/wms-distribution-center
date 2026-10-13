// Seeds a believable (fictional) warehouse state through the API and captures the README screenshots.
// Usage: node e2e/screenshots.mjs <output-dir>
// Requires a freshly seeded Development API on :5300 and the Vite dev server on :5173 (see README).
import { chromium } from '@playwright/test';
import process from 'node:process';

const API = process.env.E2E_API_URL || 'http://localhost:5300/api';
const WEB = process.env.E2E_BASE_URL || 'http://localhost:5173';
const PASSWORD = process.env.E2E_DEMO_PASSWORD || 'Demo!Pass123';
const out = process.argv[2];
if (!out) throw new Error('Usage: node e2e/screenshots.mjs <output-dir>');

async function call(token, method, path, body) {
  const res = await fetch(`${API}${path}`, {
    method,
    headers: { 'content-type': 'application/json', ...(token ? { authorization: `Bearer ${token}` } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  });
  const text = await res.text();
  if (!res.ok) throw new Error(`${method} ${path} -> ${res.status} ${text}`);
  return text ? JSON.parse(text) : null;
}
const login = async (username) => (await call(null, 'POST', '/auth/login', { username, password: PASSWORD })).token;

const sup = await login('supervisor');
const op = await login('operator');
const adm = await login('admin');

const [wh] = await call(sup, 'GET', '/warehouses');
const suppliers = await call(sup, 'GET', '/suppliers');
const carriers = await call(sup, 'GET', '/carriers');
const products = Object.fromEntries((await call(sup, 'GET', '/products')).map((p) => [p.sku, p]));
const locations = await call(sup, 'GET', '/locations');
const empty = locations.filter((l) => l.status === 'Empty');
const loc = (code) => locations.find((l) => l.locationCode === code);

// --- inbound: one fully received delivery, one planned, one with a shortage
async function receive(po, supplier, lines, quantities) {
  const r = await call(sup, 'POST', '/inboundreceipts', {
    supplierId: supplier.id, warehouseId: wh.id, poNumber: po, expectedDate: new Date(Date.now() + 86400000).toISOString().slice(0, 10),
    items: lines.map(([sku, qty, cost]) => ({ productId: products[sku].id, expectedQty: qty, unitCost: cost })),
  });
  if (quantities) {
    for (const [i, item] of r.items.entries()) {
      await call(op, 'POST', `/inboundreceipts/${r.id}/receive`, {
        receiptItemId: item.id, receivedQty: quantities[i][0], damagedQty: quantities[i][1] || 0, putawayLocationId: quantities[i][2].id,
      });
    }
    await call(op, 'POST', `/inboundreceipts/${r.id}/complete`);
  }
  return r;
}
await receive('PO-24-0117', suppliers[0], [['SKU-1001', 120, 3.2], ['SKU-1002', 60, 2.1]], [[120, 0, empty[0]], [60, 0, empty[1]]]);
await receive('PO-24-0121', suppliers[1], [['SKU-2001', 200, 1.1], ['SKU-2002', 80, 0.9]], [[180, 0, empty[2]], [80, 4, empty[3]]]);
await receive('PO-24-0125', suppliers[0], [['SKU-1003', 400, 0.4]], null);

// --- outbound orders in different states
async function order(destination, priority, lines) {
  return call(sup, 'POST', '/outboundorders', {
    warehouseId: wh.id, orderType: 'Sales', priority, destination, dueDate: new Date(Date.now() + 2 * 86400000).toISOString().slice(0, 10),
    items: lines.map(([sku, qty, price]) => ({ productId: products[sku].id, orderedQty: qty, unitPrice: price })),
  });
}
const pickAll = async (o) => {
  for (const t of await call(op, 'GET', `/pick-tasks?orderId=${o.id}`)) await call(op, 'POST', `/pick-tasks/${t.id}/confirm`, { qtyPicked: t.qtyToPick });
};

const shipped = await order('Store 12 (Demo City)', 'Normal', [['SKU-1001', 40, 4.5], ['SKU-1002', 24, 3.2]]);
await call(sup, 'POST', `/outboundorders/${shipped.id}/start-picking`);
await pickAll(shipped);
await call(op, 'POST', `/outboundorders/${shipped.id}/pack`, { cartonNo: 'CTN-0001', weightKg: 62.4 });
const shipment = await call(op, 'POST', `/outboundorders/${shipped.id}/ship`, { carrierId: carriers[0].id, trackingNo: 'DL-880213', vehiclePlate: 'DEMO 1234', driverName: 'Sample Driver' });
await call(op, 'POST', `/shipments/${shipment.id}/deliver`);

const packed = await order('Store 7 (Sample Town)', 'Urgent', [['SKU-2001', 60, 1.6]]);
await call(sup, 'POST', `/outboundorders/${packed.id}/start-picking`);
await pickAll(packed);
await call(op, 'POST', `/outboundorders/${packed.id}/pack`, { cartonNo: 'CTN-0002', weightKg: 61.2 });

const picking = await order('Store 3 (Example City)', 'Rush', [['SKU-1001', 30, 4.5], ['SKU-1003', 150, 0.7], ['SKU-2002', 20, 1.4]]);
await call(sup, 'POST', `/outboundorders/${picking.id}/start-picking`);
const firstTask = (await call(op, 'GET', `/pick-tasks?orderId=${picking.id}`))[0];
await call(op, 'POST', `/pick-tasks/${firstTask.id}/confirm`, { qtyPicked: firstTask.qtyToPick });

await order('Store 21 (Demo City)', 'Normal', [['SKU-1002', 12, 3.2]]);

// --- adjustments, transfers, returns
const rice = products['SKU-1001'];
const riceStock = (await call(sup, 'GET', `/inventory?productId=${rice.id}`)).find((s) => s.qtyOnHand - s.qtyReserved > 20);
const a1 = await call(op, 'POST', '/inventory/adjustments', { productId: rice.id, locationId: riceStock.locationId, adjType: 'Negative', adjQty: 3, reason: 'Torn bags found during cycle count' });
await call(sup, 'POST', `/inventory/adjustments/${a1.id}/approve`);
await call(op, 'POST', '/inventory/adjustments', { productId: products['SKU-1002'].id, locationId: empty[1].id, adjType: 'Recount', adjQty: 58, reason: 'Recount after shelf reorganisation' });

const t1 = await call(op, 'POST', '/transfers', { productId: rice.id, fromLocationId: riceStock.locationId, toLocationId: empty[4].id, qty: 25, reason: 'Move fast mover to pick face' });
await call(op, 'POST', `/transfers/${t1.id}/complete`);
await call(op, 'POST', '/transfers', { productId: products['SKU-1002'].id, fromLocationId: empty[1].id, toLocationId: empty[5].id, qty: 10, reason: 'Consolidate half-empty bins' });

const r1 = await call(op, 'POST', '/returns', { warehouseId: wh.id, productId: rice.id, qty: 4, condition: 'Good', orderId: shipped.id, reason: 'Store ordered too many' });
await call(sup, 'POST', `/returns/${r1.id}/process`, { disposition: 'Restock', restockLocationId: riceStock.locationId });
await call(op, 'POST', '/returns', { warehouseId: wh.id, productId: products['SKU-1002'].id, qty: 2, condition: 'Damaged', orderId: shipped.id, reason: 'Leaking bottles on arrival' });

// --- screenshots
const browser = await chromium.launch({ channel: process.env.PW_CHANNEL || undefined });
const page = await browser.newPage({ viewport: { width: 1440, height: 1040 } });
const shot = async (name) => { await page.waitForTimeout(500); await page.screenshot({ path: `${out}/${name}.png` }); };

await page.goto(`${WEB}/login`);
await shot('01-login');
await page.getByLabel('Username').fill('admin');
await page.getByLabel('Password').fill(PASSWORD);
await page.getByRole('button', { name: 'Sign In' }).click();
await page.getByRole('heading', { name: 'Dashboard' }).waitFor();
await shot('02-dashboard');

await page.goto(`${WEB}/inventory`);
await page.getByRole('row').nth(1).waitFor();
await shot('03-inventory');

await page.goto(`${WEB}/inbound`);
await page.getByRole('row').filter({ hasText: 'PO-24-0121' }).getByRole('button', { name: 'Open' }).click();
await page.getByRole('dialog').getByText('Lines').waitFor();
await shot('04-receipt-detail');

await page.goto(`${WEB}/outbound`);
await page.getByRole('row').nth(1).waitFor();
await shot('05-outbound');
await page.getByRole('row').filter({ hasText: 'Store 3' }).getByRole('button', { name: 'Open' }).click();
await page.getByRole('dialog').getByText('Pick tasks', { exact: true }).waitFor();
await shot('06-order-picking');

await page.goto(`${WEB}/picks`);
await page.getByRole('row').nth(1).waitFor();
await shot('07-pick-tasks');

await page.goto(`${WEB}/adjustments`);
await page.getByRole('row').nth(1).waitFor();
await shot('08-adjustments');

await page.goto(`${WEB}/transfers`);
await page.getByRole('row').nth(1).waitFor();
await shot('09-transfers');

await page.goto(`${WEB}/returns`);
await page.getByRole('row').nth(1).waitFor();
await shot('10-returns');

await browser.close();
console.log('screenshots written to', out);
