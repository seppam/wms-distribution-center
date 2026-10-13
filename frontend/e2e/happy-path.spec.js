import { expect, test } from '@playwright/test';
import { dialog, login, pick, toast } from './helpers';

// One full warehouse cycle through the UI:
// receive -> put-away -> create order -> pick -> pack -> ship -> adjustment -> transfer -> return
test('complete warehouse cycle from the browser', async ({ page }) => {
  const run = Date.now().toString().slice(-6);
  const destination = `E2E Store ${run}`;
  await login(page);

  // ---- inbound: plan a delivery of 60 rice bags, receive and put away into an empty bin
  await page.goto('/inbound');
  await page.getByRole('button', { name: 'New receipt' }).click();
  const newReceipt = dialog(page, 'New inbound receipt');
  await pick(newReceipt.getByLabel('Supplier'), 'Demo Foods');
  await pick(newReceipt.getByLabel('Warehouse'), 'Main Distribution');
  await newReceipt.getByLabel('PO number').fill(`PO-${run}`);
  await pick(newReceipt.locator('select').nth(2), 'SKU-1001');
  await newReceipt.getByLabel('Expected quantity line 1').fill('60');
  await newReceipt.getByRole('button', { name: 'Create receipt' }).click();
  await expect(toast(page, 'Receipt created')).toBeVisible();

  const receiptRow = page.getByRole('row').filter({ hasText: `PO-${run}` });
  await receiptRow.getByRole('button', { name: 'Open' }).click();
  const receipt = dialog(page, /Receipt RCV-/);
  await receipt.getByRole('button', { name: 'Receive' }).click();
  const receive = dialog(page, /Receive Long Grain Rice/);
  await pick(receive.getByLabel('Put-away location'), /C-01-01/);
  await receive.getByRole('button', { name: 'Receive and put away' }).click();
  await expect(toast(page, 'Goods received and put away')).toBeVisible();
  await receipt.getByRole('button', { name: 'Complete receipt' }).click();
  await expect(toast(page, 'Receipt completed as Received')).toBeVisible();
  await receipt.getByRole('button', { name: 'Close' }).first().click();

  // ---- outbound: create a 30-bag order and allocate stock
  await page.goto('/outbound');
  await page.getByRole('button', { name: 'New order' }).click();
  const newOrder = dialog(page, 'New outbound order');
  await pick(newOrder.getByLabel('Warehouse'), 'Main Distribution');
  await newOrder.getByLabel('Destination').fill(destination);
  await pick(newOrder.locator('select').nth(2), 'SKU-1001');
  await newOrder.getByLabel('Quantity line 1').fill('30');
  await newOrder.getByLabel('Unit price line 1').fill('4.5');
  await newOrder.getByRole('button', { name: 'Create order' }).click();
  await expect(toast(page, 'Order created as a draft')).toBeVisible();

  const orderRow = () => page.getByRole('row').filter({ hasText: destination });
  await orderRow().getByRole('button', { name: 'Open' }).click();
  const order = dialog(page, /Order ORD-/);
  await order.getByRole('button', { name: 'Allocate stock and start picking' }).click();
  await expect(toast(page, 'Stock allocated: pick tasks created')).toBeVisible();
  await expect(order.getByText('Pick tasks', { exact: true })).toBeVisible();
  const orderNumber = (await order.getByRole('heading').first().textContent()).replace('Order ', '').trim();
  await order.getByRole('button', { name: 'Close' }).first().click();

  // ---- picking
  await page.goto('/picks');
  const taskRows = page.getByRole('row').filter({ hasText: orderNumber });
  await expect(taskRows.first()).toBeVisible();
  const taskCount = await taskRows.count();
  expect(taskCount).toBeGreaterThan(0);
  for (let i = 0; i < taskCount; i++) {
    await page.getByRole('row').filter({ hasText: orderNumber }).first().getByRole('button', { name: 'Confirm' }).click();
    await expect(toast(page, 'Pick confirmed').first()).toBeVisible();
    await expect(taskRows).toHaveCount(taskCount - i - 1);
  }

  // ---- pack and ship
  await page.goto('/outbound');
  await orderRow().getByRole('button', { name: 'Open' }).click();
  await dialog(page, /Order ORD-/).getByRole('button', { name: 'Pack' }).click();
  const pack = dialog(page, 'Pack order');
  await pack.getByLabel('Weight (kg)').fill('18.5');
  await pack.getByRole('button', { name: 'Pack', exact: true }).click();
  await expect(toast(page, 'Order packed')).toBeVisible();
  await dialog(page, /Order ORD-/).getByRole('button', { name: 'Ship', exact: true }).click();
  const ship = dialog(page, 'Ship order');
  await pick(ship.getByLabel('Carrier'), 'Demo Logistics');
  await ship.getByLabel('Tracking number').fill(`TRK-${run}`);
  await ship.getByRole('button', { name: 'Ship order' }).click();
  await expect(toast(page, 'Order shipped: stock deducted')).toBeVisible();
  await dialog(page, /Order ORD-/).getByRole('button', { name: 'Close' }).first().click();
  await expect(orderRow().getByText('Shipped')).toBeVisible();

  // ---- delivery
  await page.goto('/shipments');
  await page.getByRole('row').filter({ hasText: orderNumber }).getByRole('button', { name: 'Mark delivered' }).click();
  await expect(toast(page, 'Shipment marked as delivered')).toBeVisible();

  // ---- adjustment (recount) and approval
  await page.goto('/adjustments');
  await page.getByRole('button', { name: 'New adjustment' }).click();
  const adj = dialog(page, 'New stock adjustment');
  await pick(adj.getByLabel('Product'), 'SKU-1001');
  await pick(adj.getByLabel('Location'), /C-01-01/);
  await pick(adj.getByLabel('Adjustment type'), 'Decrease');
  await adj.getByLabel('Quantity').fill('2');
  await adj.getByLabel('Reason').fill(`Damaged in aisle ${run}`);
  await adj.getByRole('button', { name: 'Submit adjustment' }).click();
  await expect(toast(page, 'Adjustment submitted for approval')).toBeVisible();
  await page.getByRole('row').filter({ hasText: `Damaged in aisle ${run}` }).getByRole('button', { name: 'Approve' }).click();
  await expect(toast(page, 'Adjustment approved and applied to stock')).toBeVisible();

  // ---- transfer
  await page.goto('/transfers');
  await page.getByRole('button', { name: 'New transfer' }).click();
  const transfer = dialog(page, 'New stock transfer');
  await pick(transfer.getByLabel('Stock to move'), /SKU-1001.*C-01-01/);
  await pick(transfer.getByLabel('Destination location'), /C-01-02/);
  await transfer.getByLabel('Quantity').fill('5');
  await transfer.getByLabel('Reason').fill(`Move ${run}`);
  await transfer.getByRole('button', { name: 'Create transfer' }).click();
  await expect(toast(page, 'Transfer created. Complete it to move the stock.')).toBeVisible();
  await page.getByRole('row').filter({ hasText: `Move ${run}` }).getByRole('button', { name: 'Complete' }).click();
  await expect(toast(page, 'Transfer completed: stock moved')).toBeVisible();

  // ---- return of 3 bags from the shipped order, restocked
  await page.goto('/returns');
  await page.getByRole('button', { name: 'Receive return' }).click();
  const ret = dialog(page, 'Receive a return');
  await pick(ret.getByLabel('Warehouse'), 'Main Distribution');
  await pick(ret.getByLabel('Original order (optional)'), orderNumber);
  await pick(ret.getByLabel('Product'), 'SKU-1001');
  await ret.getByLabel('Quantity').fill('3');
  await ret.getByLabel('Reason').fill(`Wrong size ${run}`);
  await ret.getByRole('button', { name: 'Receive return' }).click();
  await expect(toast(page, /Return received/)).toBeVisible();
  await page.getByRole('row').filter({ hasText: orderNumber }).getByRole('button', { name: 'Process' }).click();
  const processReturn = dialog(page, /Process RET-/);
  await pick(processReturn.getByLabel('Restock location'), /C-01-01/);
  await processReturn.getByRole('button', { name: 'Process return' }).click();
  await expect(toast(page, 'Return restocked')).toBeVisible();
  await expect(page.getByRole('row').filter({ hasText: orderNumber }).getByText('Completed')).toBeVisible();

  // ---- the ledger shows every kind of stock movement
  await page.goto('/inventory');
  await page.getByRole('tab', { name: 'Movement ledger' }).click();
  for (const source of ['Inbound', 'Outbound', 'Adjustment', 'Transfer', 'Return']) {
    await expect(page.getByRole('row').filter({ hasText: source }).first()).toBeVisible();
  }
});

test('a viewer can browse but not change anything', async ({ page }) => {
  await login(page, 'viewer');
  for (const path of ['/inbound', '/outbound', '/transfers', '/returns', '/adjustments']) {
    await page.goto(path);
    await expect(page.getByRole('button', { name: /^(New|Receive return)/ })).toHaveCount(0);
  }
  await expect(page.getByRole('link', { name: 'Users' })).toHaveCount(0);
  await page.goto('/users');
  await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible(); // redirected away
});
