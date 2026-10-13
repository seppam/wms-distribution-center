import { available, byId, logTransaction, nextNumber, nowIso, syncLocationStatus } from '../db';
import { transferDto } from '../dto';
import { BusinessRuleError, NotFoundError } from '../errors';
import { compactDay } from '../util';
import { ok, route, type Route, type RouteCtx } from '../route';
import type { Transfer } from '../types';
import { asBody, cmp, fmt, num, round, str, Validator } from '../util';

function ensureLocationUsable(c: RouteCtx, id: string, role: string): void {
  const loc = byId(c.db.locations, id);
  if (!loc) throw new BusinessRuleError(`${role} location not found.`);
  if (loc.status === 'Locked') throw new BusinessRuleError(`${role} location ${loc.locationCode} is locked.`);
}

export const transferRoutes: Route[] = [
  route('GET', 'api/transfers', (c) => ok([...c.db.transfers].sort((a, b) => cmp(b.transferNo, a.transferNo)).map((t) => transferDto(c.db, t)))),

  route('GET', 'api/transfers/{id}', (c) => {
    const t = byId(c.db.transfers, c.params.id);
    if (!t) throw new NotFoundError();
    return ok(transferDto(c.db, t));
  }),

  route('POST', 'api/transfers', (c) => {
    const b = asBody(c.body);
    new Validator(b).id('productId', 'ProductId').id('fromLocationId', 'FromLocationId').id('toLocationId', 'ToLocationId').range('qty', 'Qty', 0.0001, 1_000_000).done();
    const productId = String(b.productId), from = String(b.fromLocationId), to = String(b.toLocationId), qty = num(b.qty);
    if (from === to) throw new BusinessRuleError('Source and destination locations must be different.');
    if (!c.db.products.some((p) => p.id === productId && p.isActive)) throw new BusinessRuleError('Product not found or inactive.');
    ensureLocationUsable(c, to, 'Destination');
    ensureLocationUsable(c, from, 'Source');
    const source = c.db.inventories.find((i) => i.productId === productId && i.locationId === from);
    const avail = source ? available(source) : 0;
    if (avail < qty) throw new BusinessRuleError(`Only ${fmt(avail)} available at the source location (reserved stock cannot be moved).`);

    const prefix = `TRF-${compactDay(c.now)}`;
    const transfer: Transfer = {
      id: c.newId(), transferNo: nextNumber('TRF', c.db.transfers.filter((t) => t.transferNo.startsWith(prefix)).length, c.now, (n) => c.db.transfers.some((t) => t.transferNo === n)),
      productId, fromLocationId: from, toLocationId: to, qty, reason: str(b.reason), requestedBy: c.userId ?? '', status: 'Draft', transferredAt: null,
    };
    c.db.transfers.push(transfer);
    return ok(transferDto(c.db, transfer));
  }),

  route('POST', 'api/transfers/{id}/complete', (c) => {
    const t = byId(c.db.transfers, c.params.id);
    if (!t) throw new NotFoundError();
    if (t.status !== 'Draft') throw new BusinessRuleError(`Only draft transfers can be completed (this one is ${t.status}).`);
    ensureLocationUsable(c, t.toLocationId, 'Destination');
    ensureLocationUsable(c, t.fromLocationId, 'Source');

    const source = c.db.inventories.find((i) => i.productId === t.productId && i.locationId === t.fromLocationId);
    if (!source || available(source) < t.qty)
      throw new BusinessRuleError(`Only ${fmt(source ? available(source) : 0)} is available at the source now; the transfer cannot be completed.`);
    let target = c.db.inventories.find((i) => i.productId === t.productId && i.locationId === t.toLocationId);
    if (target && target.qtyOnHand > 0 && (target.lotNumber ?? null) !== (source.lotNumber ?? null))
      throw new BusinessRuleError('The destination already holds another lot of this product.');

    const sourceBefore = source.qtyOnHand;
    source.qtyOnHand = round(source.qtyOnHand - t.qty);
    if (!target) {
      target = { id: c.newId(), productId: t.productId, locationId: t.toLocationId, qtyOnHand: 0, qtyReserved: 0, lotNumber: source.lotNumber, expiryDate: source.expiryDate, unitCost: source.unitCost, status: source.status, lastCountedDate: null };
      c.db.inventories.push(target);
    }
    const targetBefore = target.qtyOnHand;
    target.qtyOnHand = round(target.qtyOnHand + t.qty);

    logTransaction(c, { productId: t.productId, locationId: t.fromLocationId, referenceType: 'Transfer', referenceId: t.id, transactionType: 'Out', qtyBefore: sourceBefore, qtyAfter: source.qtyOnHand, uomQty: t.qty, lotNumber: source.lotNumber, notes: t.transferNo });
    logTransaction(c, { productId: t.productId, locationId: t.toLocationId, referenceType: 'Transfer', referenceId: t.id, transactionType: 'In', qtyBefore: targetBefore, qtyAfter: target.qtyOnHand, uomQty: t.qty, lotNumber: source.lotNumber, notes: t.transferNo });
    t.status = 'Completed';
    t.transferredAt = nowIso(c);
    syncLocationStatus(c.db, t.fromLocationId, t.toLocationId);
    return ok(transferDto(c.db, t));
  }),

  route('POST', 'api/transfers/{id}/cancel', (c) => {
    const t = byId(c.db.transfers, c.params.id);
    if (!t) throw new NotFoundError();
    if (t.status !== 'Draft') throw new BusinessRuleError(`Only draft transfers can be cancelled (this one is ${t.status}).`);
    t.status = 'Cancelled';
    return ok(transferDto(c.db, t));
  }),
];
