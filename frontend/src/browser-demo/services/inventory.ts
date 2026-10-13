import { byId, countToday, logTransaction, nextNumber, nowIso, syncLocationStatus, warehouseOfLocation } from '../db';
import { adjustmentDto, inventoryDto, transactionDto } from '../dto';
import { AdjType, InventoryStatus } from '../enums';
import { BusinessRuleError, NotFoundError } from '../errors';
import { ok, route, type Route, type RouteCtx } from '../route';
import type { Adjustment } from '../types';
import { asBody, cmp, dayString, fmt, num, round, str, Validator } from '../util';

const resulting = (type: string, current: number, qty: number): number =>
  type === 'Positive' ? current + qty : type === 'Negative' ? current - qty : type === 'Recount' ? qty : current;

const clamp = (v: number, lo: number, hi: number) => Math.min(hi, Math.max(lo, v));
const q = (c: RouteCtx, k: string): string | undefined => (c.query[k] === undefined || c.query[k] === null || c.query[k] === '' ? undefined : String(c.query[k]));

export const inventoryRoutes: Route[] = [
  route('GET', 'api/inventory', (c) => {
    const productId = q(c, 'productId'), locationId = q(c, 'locationId'), warehouseId = q(c, 'warehouseId'), status = q(c, 'status');
    if (status && !InventoryStatus.includes(status as never)) throw new BusinessRuleError('The value is not valid for status.');
    const page = Math.max(1, num(q(c, 'page'), 1));
    const pageSize = clamp(num(q(c, 'pageSize'), 100), 1, 500);
    const name = (i: { productId: string }) => byId(c.db.products, i.productId)?.name ?? '';
    const code = (i: { locationId: string }) => byId(c.db.locations, i.locationId)?.locationCode ?? '';
    const rows = c.db.inventories
      .filter((i) => !productId || i.productId === productId)
      .filter((i) => !locationId || i.locationId === locationId)
      .filter((i) => !warehouseId || warehouseOfLocation(c.db, byId(c.db.locations, i.locationId)!) === warehouseId)
      .filter((i) => !status || i.status === status)
      .sort((a, b) => cmp(name(a), name(b)) || cmp(code(a), code(b)))
      .slice((page - 1) * pageSize, page * pageSize);
    return ok(rows.map((i) => inventoryDto(c.db, i)));
  }),

  route('GET', 'api/inventory/{id}', (c) => {
    const i = byId(c.db.inventories, c.params.id);
    if (!i) throw new NotFoundError();
    return ok(inventoryDto(c.db, i));
  }),

  route('GET', 'api/inventory/transactions', (c) => {
    const productId = q(c, 'productId'), locationId = q(c, 'locationId');
    const take = clamp(num(q(c, 'take'), 100), 1, 500);
    const rows = c.db.inventoryTransactions
      .filter((t) => !productId || t.productId === productId)
      .filter((t) => !locationId || t.locationId === locationId)
      .sort((a, b) => cmp(b.createdAt, a.createdAt) || b.id - a.id)
      .slice(0, take);
    return ok(rows.map((t) => transactionDto(c.db, t)));
  }),

  route('GET', 'api/inventory/adjustments', (c) => ok([...c.db.adjustments].sort((a, b) => cmp(b.createdAt, a.createdAt)).map((a) => adjustmentDto(c.db, a)))),

  route('POST', 'api/inventory/adjustments', (c) => {
    const b = asBody(c.body);
    new Validator(b).id('productId', 'ProductId').id('locationId', 'LocationId').oneOf('adjType', 'AdjType', AdjType).range('adjQty', 'AdjQty', 0, 1_000_000).done();
    const productId = String(b.productId), locationId = String(b.locationId);
    const adjType = String(b.adjType ?? AdjType[0]);
    const adjQty = num(b.adjQty);
    if (!c.db.products.some((p) => p.id === productId)) throw new BusinessRuleError('Product not found.');
    if (!c.db.locations.some((l) => l.id === locationId)) throw new BusinessRuleError('Location not found.');
    if (adjType !== 'Recount' && adjQty <= 0) throw new BusinessRuleError('The adjustment quantity must be greater than zero.');

    const inv = c.db.inventories.find((i) => i.productId === productId && i.locationId === locationId);
    const qtyBefore = inv?.qtyOnHand ?? 0;
    const qtyAfter = round(resulting(adjType, qtyBefore, adjQty));
    if (qtyAfter < 0) throw new BusinessRuleError(`The adjustment would make stock negative (on hand ${fmt(qtyBefore)}).`);

    const adj: Adjustment = {
      id: c.newId(), adjNumber: nextNumber('ADJ', countToday(c.db.adjustments, (a) => a.createdAt, c.now), c.now, (n) => c.db.adjustments.some((a) => a.adjNumber === n)),
      productId, locationId, adjType, qtyBefore, adjQty, qtyAfter, reason: str(b.reason), approvedBy: null, createdBy: c.userId ?? '', status: 'Draft', createdAt: nowIso(c),
    };
    c.db.adjustments.push(adj);
    return ok(adjustmentDto(c.db, adj));
  }),

  /** InventoryService.ApproveAdjustmentAsync: recalculated against today's stock, never below what is reserved, and not by its own creator (unless Admin). */
  route('POST', 'api/inventory/adjustments/{id}/approve', (c) => {
    const adj = byId(c.db.adjustments, c.params.id);
    if (!adj) throw new NotFoundError();
    const isAdmin = c.roleName === 'Admin';
    if (adj.status !== 'Draft') throw new BusinessRuleError(`Only draft adjustments can be approved (this one is ${adj.status}).`);
    if (adj.createdBy === c.userId && !isAdmin) throw new BusinessRuleError('You cannot approve an adjustment you created yourself.');

    let inv = c.db.inventories.find((i) => i.productId === adj.productId && i.locationId === adj.locationId);
    const current = inv?.qtyOnHand ?? 0;
    const after = round(resulting(adj.adjType, current, adj.adjQty));
    if (after < 0) throw new BusinessRuleError(`The adjustment would make stock negative (on hand ${fmt(current)}).`);
    if (inv && after < inv.qtyReserved) throw new BusinessRuleError(`Stock cannot drop below the ${fmt(inv.qtyReserved)} reserved for open orders.`);

    if (!inv) {
      inv = { id: c.newId(), productId: adj.productId, locationId: adj.locationId, qtyOnHand: 0, qtyReserved: 0, lotNumber: null, expiryDate: null, unitCost: 0, status: 'Available', lastCountedDate: null };
      c.db.inventories.push(inv);
    }
    inv.qtyOnHand = after;
    if (adj.adjType === 'Recount') inv.lastCountedDate = dayString(c.now);

    adj.qtyBefore = current;
    adj.qtyAfter = after;
    adj.status = 'Approved';
    adj.approvedBy = c.userId;
    logTransaction(c, { productId: adj.productId, locationId: adj.locationId, referenceType: 'Adjustment', referenceId: adj.id, transactionType: 'Adjust', qtyBefore: current, qtyAfter: after, uomQty: round(after - current), lotNumber: inv.lotNumber, notes: adj.reason });
    syncLocationStatus(c.db, adj.locationId);
    return ok(adjustmentDto(c.db, adj));
  }),

  route('POST', 'api/inventory/adjustments/{id}/reject', (c) => {
    const adj = byId(c.db.adjustments, c.params.id);
    if (!adj) throw new NotFoundError();
    if (adj.status !== 'Draft') throw new BusinessRuleError(`Only draft adjustments can be rejected (this one is ${adj.status}).`);
    adj.status = 'Cancelled';
    adj.approvedBy = c.userId;
    return ok(adjustmentDto(c.db, adj));
  }),
];
