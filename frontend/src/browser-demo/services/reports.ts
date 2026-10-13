import { addDays, dayString, isSameDay, round, startOfDay, sum } from '../util';
import { ok, route, type Route } from '../route';

const DAY = 86_400_000;

export const reportRoutes: Route[] = [
  route('GET', 'api/reports/dashboard', (c) => {
    const { db, now } = c;
    const weekFromNow = dayString(addDays(new Date(startOfDay(now)), 7));
    const onHand = (productId: string) => sum(db.inventories.filter((i) => i.productId === productId).map((i) => i.qtyOnHand));
    return ok({
      totalProducts: db.products.filter((p) => p.isActive).length,
      totalLocations: db.locations.length,
      totalInboundToday: db.inboundReceipts.filter((r) => isSameDay(r.createdAt, now)).length,
      totalOutboundToday: db.outboundOrders.filter((o) => isSameDay(o.createdAt, now)).length,
      totalInventoryValue: round(db.inventories.reduce((a, i) => a + i.qtyOnHand * i.unitCost, 0)),
      lowStockCount: db.products.filter((p) => p.isActive && onHand(p.id) < p.minStock).length,
      expiringSoonCount: db.inventories.filter((i) => i.expiryDate !== null && i.expiryDate <= weekFromNow && i.status !== 'Damaged').length,
    });
  }),

  route('GET', 'api/reports/inventory-summary', (c) => {
    const groups = new Map<string, typeof c.db.inventories>();
    for (const i of c.db.inventories) groups.set(i.productId, [...(groups.get(i.productId) ?? []), i]);
    return ok([...groups].map(([productId, rows]) => {
      const p = c.db.products.find((x) => x.id === productId);
      return {
        productId, productName: p?.name ?? '', productSku: p?.sku ?? '', totalOnHand: sum(rows.map((r) => r.qtyOnHand)), totalReserved: sum(rows.map((r) => r.qtyReserved)),
        available: sum(rows.map((r) => r.qtyOnHand - r.qtyReserved)), locationCount: rows.length,
      };
    }));
  }),

  route('GET', 'api/reports/inbound-summary', (c) => {
    const since = startOfDay(c.now) - 30 * DAY;
    const rows = c.db.inboundReceipts.filter((r) => Date.parse(r.createdAt) >= since);
    const n = (s: string) => rows.filter((r) => r.status === s).length;
    return ok({ draft: n('Draft'), inProgress: n('InProgress'), received: n('Received'), partial: n('Partial'), cancelled: n('Cancelled') });
  }),

  route('GET', 'api/reports/outbound-summary', (c) => {
    const since = startOfDay(c.now) - 30 * DAY;
    const rows = c.db.outboundOrders.filter((o) => Date.parse(o.createdAt) >= since);
    const n = (s: string) => rows.filter((o) => o.status === s).length;
    return ok({ draft: n('Draft'), picked: n('Picked'), packed: n('Packed'), shipped: n('Shipped'), completed: n('Completed'), cancelled: n('Cancelled'), picking: n('Picking') });
  }),
];
