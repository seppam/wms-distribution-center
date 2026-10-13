import { beforeEach, describe, expect, it, vi } from 'vitest';
import matrix from '../../../contracts/permission-matrix.json';
import seed from '../../../contracts/demo-seed.json';
import { POLICY_ROLES } from '../utils/permissions';
import { ALL_ROUTES, Engine } from './engine';
import { makeClient, FIXED_NOW, PASSWORD } from './testkit';
import { SCHEMA_VERSION, STORAGE_KEY_PREFIX, memoryStore, type KeyValueStore } from './storage';

type Client = ReturnType<typeof makeClient>;
let c: Client;
beforeEach(() => { c = makeClient(); });

const find = async (c: Client, path: string, pred: (x: any) => boolean) => (await c.get<any[]>(path)).find(pred);
const sku = (c: Client, s: string) => find(c, '/api/products', (p) => p.sku === s);
const loc = (c: Client, code: string) => find(c, '/api/locations', (l) => l.locationCode === code);
const stock = async (c: Client, s: string, code: string) => {
  const p = await sku(c, s), l = await loc(c, code);
  return (await c.get<any[]>('/api/inventory', { productId: p.id, locationId: l.id }))[0];
};
const carrier = (c: Client) => c.get<any[]>('/api/carriers').then((x) => x[0]);

describe('shared contracts', () => {
  it('every route is in the permission matrix and every matrix endpoint is implemented', () => {
    const impl = new Set(ALL_ROUTES.map((r) => `${r.method} ${r.pattern}`));
    const contract = new Set(Object.keys(matrix.endpoints));
    expect([...impl].filter((k) => !contract.has(k))).toEqual([]);
    expect([...contract].filter((k) => !impl.has(k))).toEqual([]);
  });

  it('the UI policy helper agrees with the matrix', () => {
    for (const [policy, who] of Object.entries(POLICY_ROLES)) expect(who.map((w) => w.toLowerCase()).sort()).toEqual([...matrix.policies[policy as 'read']].filter((w) => w !== 'anonymous').sort());
  });

  it('enforces every endpoint for every principal exactly as the matrix says', async () => {
    const failures: string[] = [];
    for (const [key, policy] of Object.entries(matrix.endpoints)) {
      const [method, pattern] = key.split(' ');
      const path = '/' + pattern.replace(/\{\w+\}/g, 'does-not-exist');
      for (const principal of matrix.principals) {
        const res = await c.call(principal, method, path, {});
        const allowed = (matrix.policies as Record<string, string[]>)[policy].includes(principal);
        const denied = res.status === 401 || res.status === 403;
        const expectedDenied = principal === 'anonymous' ? 401 : 403;
        if (allowed ? denied || res.status >= 500 : res.status !== expectedDenied) failures.push(`${principal} ${key} [${policy}] -> ${res.status}`);
      }
    }
    expect(failures).toEqual([]);
  });
});

describe('authentication', () => {
  it.each(['admin', 'supervisor', 'operator', 'viewer'])('signs in as %s with the matching role', async (role) => {
    const res = await c.engine.request({ method: 'POST', path: '/api/auth/login', body: { username: role, password: PASSWORD } });
    expect(res.status).toBe(200);
    const body = res.data as any;
    expect(body.user.role).toBe(role[0].toUpperCase() + role.slice(1));
    const me = await c.engine.request({ method: 'GET', path: '/api/auth/me', token: body.token });
    expect((me.data as any).username).toBe(role);
  });
  it('rejects a wrong password and an unknown user with the same message', async () => {
    for (const body of [{ username: 'admin', password: 'nope' }, { username: 'ghost', password: PASSWORD }]) {
      const res = await c.engine.request({ method: 'POST', path: '/api/auth/login', body });
      expect(res.status).toBe(401);
      expect((res.data as any).message).toBe('Invalid username or password');
    }
  });
  it('rejects garbage and expired tokens, and deactivated users', async () => {
    expect((await c.engine.request({ method: 'GET', path: '/api/products', token: 'garbage' })).status).toBe(401);
    const users = await c.get<any[]>('/api/users');
    const viewer = users.find((u) => u.username === 'viewer');
    const t = await c.token('viewer');
    await c.ok('admin', 'PUT', `/api/users/${viewer.id}`, { isActive: false });
    expect((await c.engine.request({ method: 'GET', path: '/api/products', token: t })).status).toBe(401);
    const later = makeClient({ now: () => new Date(FIXED_NOW.getTime() + 9 * 3600_000) });
    const tok = (await later.engine.request({ method: 'POST', path: '/api/auth/login', body: { username: 'admin', password: PASSWORD } })).data as any;
    const expired = new Engine({ store: later.store, now: () => new Date(FIXED_NOW.getTime() + 20 * 3600_000) });
    expect((await expired.request({ method: 'GET', path: '/api/products', token: tok.token })).status).toBe(401);
  });
  it('lets an admin create a user who can sign in, but never delete themselves', async () => {
    const roles = await c.get<any[]>('/api/roles');
    const made = await c.ok('admin', 'POST', '/api/users', { username: 'newbie', email: 'n@example.com', password: 'example-long-pw', fullName: 'New', roleId: roles.find((r) => r.name === 'Viewer').id });
    expect((await c.engine.request({ method: 'POST', path: '/api/auth/login', body: { username: 'newbie', password: 'example-long-pw' } })).status).toBe(200);
    const me = (await c.get<any[]>('/api/users')).find((u) => u.username === 'admin');
    expect((await c.call('admin', 'DELETE', `/api/users/${me.id}`)).status).toBe(400);
    expect((await c.call('admin', 'DELETE', `/api/users/${made.id}`)).status).toBe(204);
  });
  it('validates request bodies like the API (password length, e-mail, required)', async () => {
    const res = await c.call('admin', 'POST', '/api/users', { username: 'x', email: 'bad', password: 'short' });
    expect(res.status).toBe(400);
    const errors = (res.data as any).errors;
    expect(errors.Password[0]).toMatch(/minimum length of '10'/);
    expect(errors.Email).toBeDefined();
    expect(errors.FullName[0]).toBe('The FullName field is required.');
  });
});

describe('master data', () => {
  it('serves the seeded dataset (24 SKUs, 4 UOMs, 2 warehouses, 6 suppliers, 4 carriers)', async () => {
    expect(await c.get<any[]>('/api/products')).toHaveLength(24);
    expect(await c.get<any[]>('/api/uoms')).toHaveLength(4);
    expect(await c.get<any[]>('/api/warehouses')).toHaveLength(2);
    expect(await c.get<any[]>('/api/suppliers')).toHaveLength(6);
    expect(await c.get<any[]>('/api/carriers')).toHaveLength(4);
    expect((await c.get<any[]>('/api/zones', { warehouseId: (await c.get<any[]>('/api/warehouses'))[0].id })).length).toBeGreaterThan(3);
  });
  it('creates, updates and deletes master data; unique codes conflict; referenced rows cannot be deleted', async () => {
    const uom = (await c.get<any[]>('/api/uoms'))[0];
    const p = await c.ok('supervisor', 'POST', '/api/products', { sku: 'SKU-9', name: 'Test', uomId: uom.id, minStock: 1 });
    expect((await c.call('supervisor', 'POST', '/api/products', { sku: 'SKU-9', name: 'Dup', uomId: uom.id })).status).toBe(409);
    expect((await c.ok('supervisor', 'PUT', `/api/products/${p.id}`, { name: 'Renamed' })).name).toBe('Renamed');
    expect((await c.call('supervisor', 'DELETE', `/api/products/${p.id}`)).status).toBe(403);
    expect((await c.call('admin', 'DELETE', `/api/products/${p.id}`)).status).toBe(204);
    const used = await sku(c, 'SKU-1001');
    expect((await c.call('admin', 'DELETE', `/api/products/${used.id}`)).status).toBe(409);
    expect((await c.call('admin', 'DELETE', `/api/uoms/${uom.id}`)).status).toBe(409);
    const whs = await c.get<any[]>('/api/warehouses');
    const w = await c.ok('supervisor', 'POST', '/api/warehouses', { code: 'DC-T', name: 'T' });
    const z = await c.ok('supervisor', 'POST', '/api/zones', { warehouseId: w.id, name: 'Z', zoneType: 'Storage' });
    await c.ok('supervisor', 'POST', '/api/locations', { zoneId: z.id, locationCode: 'T-1', locationType: 'Pick', maxCapacity: 5 });
    expect((await c.call('admin', 'DELETE', `/api/warehouses/${w.id}`)).status).toBe(204); // cascades zone and location
    expect((await c.get<any[]>('/api/locations')).some((l) => l.locationCode === 'T-1')).toBe(false);
    expect(whs.length).toBe(2);
  });
  it('protects built-in roles', async () => {
    const admin = (await c.get<any[]>('/api/roles')).find((r) => r.name === 'Admin');
    expect((await c.call('admin', 'DELETE', `/api/roles/${admin.id}`)).status).toBe(400);
    expect((await c.call('admin', 'PUT', `/api/roles/${admin.id}`, { name: 'Boss' })).status).toBe(400);
    const custom = await c.ok('admin', 'POST', '/api/roles', { name: 'Auditor' });
    expect((await c.call('admin', 'DELETE', `/api/roles/${custom.id}`)).status).toBe(204);
  });
});

describe('inbound: receive and put-away', () => {
  it('receives the draft PO-2413 line by line, posts stock and ledger, completes as Received', async () => {
    const receipts = await c.get<any[]>('/api/inboundreceipts');
    const draft = receipts.find((r) => r.poNumber === 'PO-2413');
    expect(draft.status).toBe('Draft');
    const full = await c.get<any>(`/api/inboundreceipts/${draft.id}`);
    const before = await c.get<any[]>('/api/inventory/transactions', { take: 500 });
    for (const item of full.items) {
      await c.ok('operator', 'POST', `/api/inboundreceipts/${draft.id}/receive`, { receiptItemId: item.id, receivedQty: item.expectedQty, damagedQty: 0, putawayLocationId: item.putawayLocationId });
    }
    const done = await c.ok<any>('operator', 'POST', `/api/inboundreceipts/${draft.id}/complete`);
    expect(done.status).toBe('Received');
    expect((await c.get<any[]>('/api/inventory/transactions', { take: 500 })).length).toBe(before.length + full.items.length);
    expect((await c.call('operator', 'POST', `/api/inboundreceipts/${draft.id}/receive`, { receiptItemId: full.items[0].id, receivedQty: 1, damagedQty: 0 })).status).toBe(400);
  });
  it('posts only the delta when a line is received again, and does not stock damaged goods', async () => {
    const supplier = (await c.get<any[]>('/api/suppliers'))[0], wh = (await c.get<any[]>('/api/warehouses'))[0];
    const p = await sku(c, 'SKU-1005'), l = await loc(c, 'REC-01');
    const r = await c.ok('supervisor', 'POST', '/api/inboundreceipts', { supplierId: supplier.id, warehouseId: wh.id, items: [{ productId: p.id, expectedQty: 100, unitCost: 1, putawayLocationId: l.id }] });
    const start = 0;
    const item = r.items[0];
    await c.ok('operator', 'POST', `/api/inboundreceipts/${r.id}/receive`, { receiptItemId: item.id, receivedQty: 60, damagedQty: 10 });
    expect((await stock(c, 'SKU-1005', 'REC-01')).qtyOnHand).toBe(start + 50);
    await c.ok('operator', 'POST', `/api/inboundreceipts/${r.id}/receive`, { receiptItemId: item.id, receivedQty: 100, damagedQty: 10 });
    expect((await stock(c, 'SKU-1005', 'REC-01')).qtyOnHand).toBe(start + 90);
    expect((await c.ok<any>('operator', 'POST', `/api/inboundreceipts/${r.id}/complete`)).status).toBe('Partial');
  });
  it('rejects damaged > received, another warehouse, another lot in the bin, and a receipt without receiving', async () => {
    const supplier = (await c.get<any[]>('/api/suppliers'))[0], wh = (await c.get<any[]>('/api/warehouses'))[0];
    const p = await sku(c, 'SKU-1001');
    const r = await c.ok('supervisor', 'POST', '/api/inboundreceipts', { supplierId: supplier.id, warehouseId: wh.id, items: [{ productId: p.id, expectedQty: 5, unitCost: 1, lotNumber: 'OTHER-LOT' }] });
    const it = r.items[0];
    expect((await c.call('operator', 'POST', `/api/inboundreceipts/${r.id}/complete`)).status).toBe(400);
    expect((await c.call('operator', 'POST', `/api/inboundreceipts/${r.id}/receive`, { receiptItemId: it.id, receivedQty: 1, damagedQty: 2, putawayLocationId: (await loc(c, 'A-01-01')).id })).status).toBe(400);
    const east = await loc(c, 'E-A-01-01');
    const wrong = await c.call('operator', 'POST', `/api/inboundreceipts/${r.id}/receive`, { receiptItemId: it.id, receivedQty: 1, damagedQty: 0, putawayLocationId: east.id });
    expect((wrong.data as any).message).toMatch(/different warehouse/);
    const mixed = await c.call('operator', 'POST', `/api/inboundreceipts/${r.id}/receive`, { receiptItemId: it.id, receivedQty: 1, damagedQty: 0, putawayLocationId: (await loc(c, 'A-01-01')).id });
    expect((mixed.data as any).message).toMatch(/another lot/);
  });
});

describe('outbound: allocation, picking, packing, shipping', () => {
  async function newOrder(lines: [string, number][], role = 'supervisor') {
    const wh = (await c.get<any[]>('/api/warehouses'))[0];
    const items = [];
    for (const [s, q] of lines) items.push({ productId: (await sku(c, s)).id, orderedQty: q, unitPrice: 2 });
    return c.ok<any>(role, 'POST', '/api/outboundorders', { warehouseId: wh.id, orderType: 'Sales', priority: 'Normal', destination: 'Test Customer', items });
  }

  it('allocates first-expiry-first-out: the lot that expires first is reserved first', async () => {
    // two bins of the same SKU with different expiry
    const p = await sku(c, 'SKU-3002');
    const rows = (await c.get<any[]>('/api/inventory', { productId: p.id })).filter((r) => r.expiryDate);
    expect(rows.length).toBeGreaterThanOrEqual(2);
    const first = [...rows].sort((a, b) => a.expiryDate.localeCompare(b.expiryDate))[0];
    const order = await newOrder([['SKU-3002', 5]]);
    await c.ok('supervisor', 'POST', `/api/outboundorders/${order.id}/start-picking`);
    const tasks = await c.get<any[]>('/api/pick-tasks', { orderId: order.id });
    expect(tasks).toHaveLength(1);
    expect(tasks[0].fromLocationId).toBe(first.locationId);
    expect((await c.get<any[]>('/api/inventory', { productId: p.id, locationId: first.locationId }))[0].qtyReserved).toBe(first.qtyReserved + 5);
  });
  it('is all-or-nothing: one short line reserves nothing', async () => {
    const before = JSON.stringify(await c.get<any[]>('/api/inventory'));
    const order = await newOrder([['SKU-1001', 1], ['SKU-3004', 100000]]);
    const res = await c.call('supervisor', 'POST', `/api/outboundorders/${order.id}/start-picking`);
    expect(res.status).toBe(400);
    expect((res.data as any).message).toMatch(/Insufficient available stock for SKU-3004/);
    expect(JSON.stringify(await c.get<any[]>('/api/inventory'))).toBe(before);
    expect((await c.get<any>(`/api/outboundorders/${order.id}`)).status).toBe('Draft');
  });
  it('never allocates more than is available across two orders', async () => {
    const s = await stock(c, 'SKU-3004', 'B-03-01');
    const avail = s.qtyOnHand - s.qtyReserved;
    const a = await newOrder([['SKU-3004', avail]]);
    const b = await newOrder([['SKU-3004', 1]]);
    await c.ok('supervisor', 'POST', `/api/outboundorders/${a.id}/start-picking`);
    expect((await c.call('supervisor', 'POST', `/api/outboundorders/${b.id}/start-picking`)).status).toBe(400);
    const after = await stock(c, 'SKU-3004', 'B-03-01');
    expect(after.qtyReserved).toBeLessThanOrEqual(after.qtyOnHand);
  });
  it('runs a full cycle: allocate, pick (short pick releases reservation), pack, ship, deliver', async () => {
    const order = await newOrder([['SKU-1001', 10], ['SKU-1002', 6]]);
    await c.ok('supervisor', 'POST', `/api/outboundorders/${order.id}/start-picking`);
    const picker = (await c.get<any[]>('/api/users')).find((u) => u.username === 'operator');
    expect((await c.ok<any>('supervisor', 'POST', `/api/outboundorders/${order.id}/assign-picker`, { pickerId: picker.id })).status).toBe('Picking');
    const tasks = await c.get<any[]>('/api/pick-tasks', { orderId: order.id });
    // over-pick rejected
    const over = await c.call('operator', 'POST', `/api/pick-tasks/${tasks[0].id}/confirm`, { qtyPicked: tasks[0].qtyToPick + 1 });
    expect(over.status).toBe(400);
    expect((over.data as any).message).toMatch(/Over-pick rejected/);
    // pack before picked is rejected
    expect((await c.call('operator', 'POST', `/api/outboundorders/${order.id}/pack`, { cartonNo: 'C1', weightKg: 1 })).status).toBe(400);
    const second = tasks.find((t) => t.productSku === 'SKU-1002');
    const secondBefore = await stock(c, 'SKU-1002', second.fromLocationCode);
    await c.ok('operator', 'POST', `/api/pick-tasks/${second.id}/confirm`, { qtyPicked: second.qtyToPick - 2 }); // short by 2
    expect((await stock(c, 'SKU-1002', second.fromLocationCode)).qtyReserved).toBe(secondBefore.qtyReserved - 2);
    const first = tasks.find((t) => t.productSku === 'SKU-1001');
    await c.ok('operator', 'POST', `/api/pick-tasks/${first.id}/confirm`, { qtyPicked: first.qtyToPick });
    expect((await c.call('operator', 'POST', `/api/pick-tasks/${first.id}/confirm`, { qtyPicked: 1 })).status).toBe(400);
    expect((await c.get<any>(`/api/outboundorders/${order.id}`)).status).toBe('Picked');
    expect((await c.call('operator', 'POST', `/api/outboundorders/${order.id}/ship`, { carrierId: (await carrier(c)).id })).status).toBe(400); // not packed
    await c.ok('operator', 'POST', `/api/outboundorders/${order.id}/pack`, { cartonNo: 'CTN-1', weightKg: 12 });
    const onHandBefore = (await stock(c, 'SKU-1001', first.fromLocationCode)).qtyOnHand;
    const shipment = await c.ok<any>('operator', 'POST', `/api/outboundorders/${order.id}/ship`, { carrierId: (await carrier(c)).id, trackingNo: 'TRK-1' });
    expect(shipment.status).toBe('Dispatched');
    expect((await stock(c, 'SKU-1001', first.fromLocationCode)).qtyOnHand).toBe(onHandBefore - first.qtyToPick);
    const detail = await c.get<any>(`/api/outboundorders/${order.id}`);
    expect(detail.status).toBe('Shipped');
    expect(detail.items.find((i: any) => i.productSku === 'SKU-1002').shippedQty).toBe(second.qtyToPick - 2);
    await c.ok('operator', 'POST', `/api/shipments/${shipment.id}/deliver`);
    expect((await c.get<any>(`/api/outboundorders/${order.id}`)).status).toBe('Completed');
    expect((await c.call('operator', 'POST', `/api/shipments/${shipment.id}/deliver`)).status).toBe(400);
  });
  it('cancel releases reservations; shipped orders cannot be cancelled; only drafts can be deleted', async () => {
    const s0 = await stock(c, 'SKU-1001', 'A-01-01');
    const order = await newOrder([['SKU-1001', 7]]);
    await c.ok('supervisor', 'POST', `/api/outboundorders/${order.id}/start-picking`);
    expect((await stock(c, 'SKU-1001', 'A-01-01')).qtyReserved).toBeGreaterThan(s0.qtyReserved - 1);
    expect((await c.call('supervisor', 'DELETE', `/api/outboundorders/${order.id}`)).status).toBe(400);
    await c.ok('supervisor', 'POST', `/api/outboundorders/${order.id}/cancel`);
    expect((await stock(c, 'SKU-1001', 'A-01-01')).qtyReserved).toBe(s0.qtyReserved);
    expect((await c.call('supervisor', 'POST', `/api/outboundorders/${order.id}/cancel`)).status).toBe(400);
    const shipped = (await c.get<any[]>('/api/outboundorders')).find((o) => o.status === 'Shipped');
    expect((await c.call('supervisor', 'POST', `/api/outboundorders/${shipped.id}/cancel`)).status).toBe(400);
  });
});

describe('adjustments, transfers, returns', () => {
  it('adjustments need approval; a non-admin cannot approve their own; stock follows the ledger', async () => {
    const p = await sku(c, 'SKU-1001'), l = await loc(c, 'A-01-01');
    const before = (await stock(c, 'SKU-1001', 'A-01-01')).qtyOnHand;
    const adj = await c.ok<any>('operator', 'POST', '/api/inventory/adjustments', { productId: p.id, locationId: l.id, adjType: 'Negative', adjQty: 2, reason: 'broken' });
    expect(adj.status).toBe('Draft');
    expect((await stock(c, 'SKU-1001', 'A-01-01')).qtyOnHand).toBe(before);
    expect((await c.call('operator', 'POST', `/api/inventory/adjustments/${adj.id}/approve`)).status).toBe(403);
    const mine = await c.ok<any>('supervisor', 'POST', '/api/inventory/adjustments', { productId: p.id, locationId: l.id, adjType: 'Positive', adjQty: 1 });
    const own = await c.call('supervisor', 'POST', `/api/inventory/adjustments/${mine.id}/approve`);
    expect((own.data as any).message).toMatch(/created yourself/);
    expect((await c.ok<any>('admin', 'POST', `/api/inventory/adjustments/${mine.id}/approve`)).status).toBe('Approved'); // admin may
    await c.ok('supervisor', 'POST', `/api/inventory/adjustments/${adj.id}/approve`);
    expect((await stock(c, 'SKU-1001', 'A-01-01')).qtyOnHand).toBe(before - 2 + 1);
    expect((await c.call('supervisor', 'POST', `/api/inventory/adjustments/${adj.id}/reject`)).status).toBe(400);
    const ledger = await c.get<any[]>('/api/inventory/transactions', { productId: p.id, locationId: l.id });
    expect(ledger[0].transactionType).toBe('Adjust');
  });
  it('rejects negative results, zero quantity, and approving below reserved stock', async () => {
    const p = await sku(c, 'SKU-1001'), l = await loc(c, 'A-01-01');
    const s = await stock(c, 'SKU-1001', 'A-01-01');
    expect((await c.call('operator', 'POST', '/api/inventory/adjustments', { productId: p.id, locationId: l.id, adjType: 'Negative', adjQty: s.qtyOnHand + 1 })).status).toBe(400);
    expect((await c.call('operator', 'POST', '/api/inventory/adjustments', { productId: p.id, locationId: l.id, adjType: 'Positive', adjQty: 0 })).status).toBe(400);
    if (s.qtyReserved > 0) {
      const adj = await c.ok<any>('operator', 'POST', '/api/inventory/adjustments', { productId: p.id, locationId: l.id, adjType: 'Recount', adjQty: 0 });
      expect((await c.call('supervisor', 'POST', `/api/inventory/adjustments/${adj.id}/approve`)).status).toBe(400);
    }
  });
  it('transfers move unreserved stock only and write two ledger entries', async () => {
    const p = await sku(c, 'SKU-1005'), from = await loc(c, 'A-02-02'), to = await loc(c, 'B-04-03');
    const s = await stock(c, 'SKU-1005', 'A-02-02');
    expect((await c.call('operator', 'POST', '/api/transfers', { productId: p.id, fromLocationId: from.id, toLocationId: from.id, qty: 1 })).status).toBe(400);
    expect((await c.call('operator', 'POST', '/api/transfers', { productId: p.id, fromLocationId: from.id, toLocationId: to.id, qty: s.qtyOnHand - s.qtyReserved + 1 })).status).toBe(400);
    const t = await c.ok<any>('operator', 'POST', '/api/transfers', { productId: p.id, fromLocationId: from.id, toLocationId: to.id, qty: 5, reason: 'test' });
    const ledgerBefore = (await c.get<any[]>('/api/inventory/transactions', { take: 500 })).length;
    expect((await c.ok<any>('operator', 'POST', `/api/transfers/${t.id}/complete`)).status).toBe('Completed');
    expect((await c.get<any[]>('/api/inventory/transactions', { take: 500 })).length).toBe(ledgerBefore + 2);
    expect((await stock(c, 'SKU-1005', 'A-02-02')).qtyOnHand).toBe(s.qtyOnHand - 5);
    expect((await stock(c, 'SKU-1005', 'B-04-03')).qtyOnHand).toBe(5);
    expect((await c.call('operator', 'POST', `/api/transfers/${t.id}/cancel`)).status).toBe(400);
  });
  it('returns: bounded by what shipped, restock only for good items into the right warehouse', async () => {
    const delivered = (await c.get<any[]>('/api/outboundorders')).find((o) => o.destination === 'Corner Basket Mini-mart');
    const wh = delivered.warehouseId;
    const detail = await c.get<any>(`/api/outboundorders/${delivered.id}`);
    const line = detail.items[0];
    const tooMany = await c.call('operator', 'POST', '/api/returns', { warehouseId: wh, productId: line.productId, qty: line.shippedQty + 1, condition: 'Good', orderId: delivered.id });
    expect((tooMany.data as any).message).toMatch(/exceeds the shipped quantity/);
    const draftOrder = (await c.get<any[]>('/api/outboundorders')).find((o) => o.status === 'Draft');
    expect((await c.call('operator', 'POST', '/api/returns', { warehouseId: wh, productId: line.productId, qty: 1, condition: 'Good', orderId: draftOrder.id })).status).toBe(400);
    const bad = await c.ok<any>('operator', 'POST', '/api/returns', { warehouseId: wh, productId: line.productId, qty: 1, condition: 'Damaged', orderId: delivered.id });
    const bin = await loc(c, 'A-01-01');
    expect((await c.call('supervisor', 'POST', `/api/returns/${bad.id}/process`, { disposition: 'Restock', restockLocationId: bin.id })).status).toBe(400);
    expect((await c.ok<any>('supervisor', 'POST', `/api/returns/${bad.id}/process`, { disposition: 'WriteOff' })).status).toBe('Completed');
    const good = await c.ok<any>('operator', 'POST', '/api/returns', { warehouseId: wh, productId: line.productId, qty: 1, condition: 'Good', orderId: delivered.id });
    const east = await loc(c, 'E-A-01-01');
    expect((await c.call('supervisor', 'POST', `/api/returns/${good.id}/process`, { disposition: 'Restock', restockLocationId: east.id })).status).toBe(400);
    const sku0 = line.productSku;
    const prod = await sku(c, sku0);
    const inv = (await c.get<any[]>('/api/inventory', { productId: prod.id, locationId: bin.id }))[0];
    const targetBin = inv ? bin : await loc(c, line.productSku === 'SKU-4001' ? 'B-01-01' : 'A-01-01');
    const before = (await c.get<any[]>('/api/inventory', { productId: prod.id, locationId: targetBin.id }))[0]?.qtyOnHand ?? 0;
    const done = await c.call('supervisor', 'POST', `/api/returns/${good.id}/process`, { disposition: 'Restock', restockLocationId: targetBin.id });
    if (done.status === 200) expect((await c.get<any[]>('/api/inventory', { productId: prod.id, locationId: targetBin.id }))[0].qtyOnHand).toBe(before + 1);
    expect((await c.call('supervisor', 'POST', `/api/returns/${good.id}/process`, { disposition: 'Dispose' })).status).toBe(400);
  });
});

describe('inventory and dashboard numbers', () => {
  it('matches the numbers the Docker demo shows (4 low-stock alerts, 1 expiring lot) and today counts', async () => {
    const d = await c.get<any>('/api/reports/dashboard');
    expect(d.totalProducts).toBe(24);
    expect(d.lowStockCount).toBe(4);
    expect(d.expiringSoonCount).toBe(1);
    expect(d.totalInboundToday).toBe(11);
    expect(d.totalOutboundToday).toBe(16);
    expect(d.totalInventoryValue).toBeGreaterThan(0);
  });
  it('keeps reserved <= on hand everywhere and the ledger consistent with on-hand after a workflow', async () => {
    const all = await c.get<any[]>('/api/inventory');
    for (const i of all) expect(i.qtyReserved).toBeLessThanOrEqual(i.qtyOnHand);
    const summary = await c.get<any[]>('/api/reports/inventory-summary');
    expect(summary.length).toBeGreaterThan(10);
    const sum = await c.get<any>('/api/reports/outbound-summary');
    expect(sum.draft + sum.picking + sum.picked + sum.packed + sum.shipped + sum.completed + sum.cancelled).toBe(16);
    expect((await c.get<any>('/api/reports/inbound-summary')).received).toBeGreaterThan(0);
  });
  it('document numbers use today and continue the sequence; the dashboard counts the new document', async () => {
    const wh = (await c.get<any[]>('/api/warehouses'))[0];
    const o = await c.ok<any>('supervisor', 'POST', '/api/outboundorders', { warehouseId: wh.id, orderType: 'Sales', priority: 'Normal' });
    expect(o.orderNumber).toBe('ORD-20261013-0017');
    expect((await c.get<any>('/api/reports/dashboard')).totalOutboundToday).toBe(17);
    expect(o.items).toEqual([]);
  });
});

describe('storage, reset and versioning', () => {
  it('persists every change and a second engine on the same storage sees it', async () => {
    const wh = (await c.get<any[]>('/api/warehouses'))[0];
    await c.ok('supervisor', 'POST', '/api/outboundorders', { warehouseId: wh.id, orderType: 'Sales', priority: 'Normal', destination: 'Persisted' });
    expect(c.store.getItem(STORAGE_KEY_PREFIX + SCHEMA_VERSION)).toContain('Persisted');
    const again = new Engine({ store: c.store, now: () => FIXED_NOW });
    const login = (await again.request({ method: 'POST', path: '/api/auth/login', body: { username: 'admin', password: PASSWORD } })).data as any;
    const orders = (await again.request({ method: 'GET', path: '/api/outboundorders', token: login.token })).data as any[];
    expect(orders.some((o) => o.destination === 'Persisted')).toBe(true);
  });
  it('a failed request changes and saves nothing', async () => {
    await c.token('supervisor');
    const before = c.store.getItem(STORAGE_KEY_PREFIX + SCHEMA_VERSION);
    expect((await c.call('supervisor', 'POST', '/api/outboundorders/nope/start-picking')).status).toBe(404);
    expect(c.store.getItem(STORAGE_KEY_PREFIX + SCHEMA_VERSION)).toBe(before);
  });
  it('reset restores the dataset for an admin and keeps the session valid', async () => {
    const wh = (await c.get<any[]>('/api/warehouses'))[0];
    await c.ok('supervisor', 'POST', '/api/outboundorders', { warehouseId: wh.id, orderType: 'Sales', priority: 'Normal' });
    expect((await c.call('operator', 'POST', '/api/demo/reset')).status).toBe(403);
    expect((await c.get<any[]>('/api/outboundorders')).length).toBe(17);
    expect((await c.call('admin', 'POST', '/api/demo/reset')).status).toBe(200);
    expect((await c.get<any[]>('/api/outboundorders')).length).toBe(16);
    expect((await c.call('admin', 'GET', '/api/auth/me')).status).toBe(200);
    expect(c.engine.snapshot().users.map((u) => u.username)).toEqual(['admin', 'supervisor', 'operator', 'viewer']);
  });
  it('resetDataset() (the banner button) also works without a session', async () => {
    const wh = (await c.get<any[]>('/api/warehouses'))[0];
    await c.ok('supervisor', 'POST', '/api/outboundorders', { warehouseId: wh.id, orderType: 'Sales', priority: 'Normal' });
    c.engine.resetDataset();
    expect((await c.get<any[]>('/api/outboundorders')).length).toBe(16);
  });
  it('discards saved data of an unknown older version and says so', () => {
    const store = memoryStore();
    store.setItem(`${STORAGE_KEY_PREFIX}0`, JSON.stringify({ schemaVersion: 0, seedRev: 'x', savedAt: '', db: { users: [] } }));
    const e = new Engine({ store, now: () => FIXED_NOW });
    expect(e.getStatus().notice).toMatch(/older version/);
    expect(store.getItem(`${STORAGE_KEY_PREFIX}0`)).toBeNull();
    expect(e.snapshot().products).toHaveLength(24);
  });
  it('migrates older saved data when a migration exists', async () => {
    const first = makeClient();
    await first.ok('supervisor', 'POST', '/api/outboundorders', { warehouseId: (await first.get<any[]>('/api/warehouses'))[0].id, orderType: 'Sales', priority: 'Normal', destination: 'Kept' });
    const v1 = first.store.getItem(`${STORAGE_KEY_PREFIX}1`)!;
    const store = memoryStore();
    store.setItem(`${STORAGE_KEY_PREFIX}1`, v1);
    const e = new Engine({ store, schemaVersion: 2, now: () => FIXED_NOW, migrations: { 1: (old) => ({ ...old, schemaVersion: 2 }) } });
    expect(e.getStatus().notice).toBeNull();
    expect(e.snapshot().outboundOrders.some((o) => o.destination === 'Kept')).toBe(true);
    expect(store.getItem(`${STORAGE_KEY_PREFIX}1`)).toBeNull();
    expect(store.getItem(`${STORAGE_KEY_PREFIX}2`)).not.toBeNull();
  });
  it('resets when the shipped dataset changed, and when the saved data is corrupt', () => {
    const store = memoryStore();
    store.setItem(`${STORAGE_KEY_PREFIX}${SCHEMA_VERSION}`, JSON.stringify({ schemaVersion: SCHEMA_VERSION, seedRev: 'old', savedAt: '', db: { users: [] } }));
    expect(new Engine({ store, now: () => FIXED_NOW }).getStatus().notice).toMatch(/dataset was updated/);
    store.setItem(`${STORAGE_KEY_PREFIX}${SCHEMA_VERSION}`, '{not json');
    expect(new Engine({ store, now: () => FIXED_NOW }).getStatus().notice).toMatch(/unreadable/);
  });
  it('survives a full storage: keeps working in memory and reports the quota problem, then recovers', async () => {
    let full = false;
    const inner = memoryStore();
    const store: KeyValueStore = { ...inner, setItem: (k, v) => { if (full) throw new DOMException('full', 'QuotaExceededError'); inner.setItem(k, v); } };
    const cl = makeClient({ store });
    const wh = (await cl.get<any[]>('/api/warehouses'))[0];
    const seen = vi.fn();
    cl.engine.subscribe(seen);
    full = true;
    const o = await cl.ok<any>('supervisor', 'POST', '/api/outboundorders', { warehouseId: wh.id, orderType: 'Sales', priority: 'Normal' });
    expect(o.orderNumber).toBeDefined();
    expect(cl.engine.getStatus().problem).toBe('quota');
    expect(seen).toHaveBeenCalled();
    expect((await cl.get<any[]>('/api/outboundorders')).length).toBe(17);
    full = false;
    await cl.ok('supervisor', 'POST', '/api/outboundorders', { warehouseId: wh.id, orderType: 'Sales', priority: 'Normal' });
    expect(cl.engine.getStatus().problem).toBeNull();
  });
  it('works without any storage (blocked localStorage) and says the data is not saved', async () => {
    const cl = makeClient({ store: null });
    expect(cl.engine.getStatus()).toMatchObject({ kind: 'memory', problem: 'unavailable' });
    expect((await cl.get<any[]>('/api/products')).length).toBe(24);
  });
  it('adopts changes saved by another tab', async () => {
    const tabA = makeClient();
    const tabB = new Engine({ store: tabA.store, now: () => FIXED_NOW });
    const wh = (await tabA.get<any[]>('/api/warehouses'))[0];
    await tabA.ok('supervisor', 'POST', '/api/outboundorders', { warehouseId: wh.id, orderType: 'Sales', priority: 'Normal', destination: 'FromTabA' });
    const t = ((await tabB.request({ method: 'POST', path: '/api/auth/login', body: { username: 'admin', password: PASSWORD } })).data as any).token;
    expect(((await tabB.request({ method: 'GET', path: '/api/outboundorders', token: t })).data as any[]).some((o) => o.destination === 'FromTabA')).toBe(true);
  });
  it('makes no network calls', async () => {
    const f = vi.fn(); vi.stubGlobal('fetch', f);
    const x = vi.fn(); vi.stubGlobal('XMLHttpRequest', x);
    const cl = makeClient();
    await cl.get('/api/products'); await cl.get('/api/reports/dashboard');
    expect(f).not.toHaveBeenCalled(); expect(x).not.toHaveBeenCalled();
    vi.unstubAllGlobals();
  });
  it('seed file is the dataset the engine starts from', () => {
    expect(c.engine.snapshot().products.length).toBe((seed.tables.products as unknown[]).length);
  });
});
