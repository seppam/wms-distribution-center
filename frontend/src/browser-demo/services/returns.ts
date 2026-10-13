import { byId, countToday, logTransaction, nextNumber, nowIso, syncLocationStatus, warehouseOfLocation } from '../db';
import { returnDto } from '../dto';
import { Disposition, ReturnCondition } from '../enums';
import { BusinessRuleError, NotFoundError } from '../errors';
import { created, ok, route, type Route } from '../route';
import type { ReturnOrder } from '../types';
import { asBody, cmp, fmt, num, optId, round, str, sum, Validator } from '../util';

export const returnRoutes: Route[] = [
  route('GET', 'api/returns', (c) => ok([...c.db.returnOrders].sort((a, b) => cmp(b.receivedAt ?? '', a.receivedAt ?? '')).map((r) => returnDto(c.db, r)))),

  route('GET', 'api/returns/{id}', (c) => {
    const r = byId(c.db.returnOrders, c.params.id);
    if (!r) throw new NotFoundError();
    return ok(returnDto(c.db, r));
  }),

  route('POST', 'api/returns', (c) => {
    const b = asBody(c.body);
    new Validator(b).id('warehouseId', 'WarehouseId').id('productId', 'ProductId').range('qty', 'Qty', 0.0001, 1_000_000).oneOf('condition', 'Condition', ReturnCondition).id('orderId', 'OrderId', false).done();
    const warehouseId = String(b.warehouseId), productId = String(b.productId), qty = num(b.qty);
    if (!c.db.warehouses.some((w) => w.id === warehouseId && w.isActive)) throw new BusinessRuleError('Warehouse not found or inactive.');
    if (!c.db.products.some((p) => p.id === productId)) throw new BusinessRuleError('Product not found.');

    const orderId = optId(b.orderId);
    if (orderId) {
      const order = byId(c.db.outboundOrders, orderId);
      if (!order) throw new BusinessRuleError('Order not found.');
      if (order.status !== 'Shipped' && order.status !== 'Completed') throw new BusinessRuleError('Only shipped orders can have returns.');
      const line = c.db.orderItems.find((i) => i.orderId === order.id && i.productId === productId);
      if (!line) throw new BusinessRuleError('The product is not on that order.');
      const alreadyReturned = sum(c.db.returnOrders.filter((r) => r.orderId === order.id && r.productId === productId).map((r) => r.qty));
      if (alreadyReturned + qty > line.shippedQty)
        throw new BusinessRuleError(`Return exceeds the shipped quantity: shipped ${fmt(line.shippedQty)}, already returned ${fmt(alreadyReturned)}.`);
    }

    const ret: ReturnOrder = {
      id: c.newId(), returnNo: nextNumber('RET', countToday(c.db.returnOrders, (r) => r.receivedAt, c.now), c.now, (n) => c.db.returnOrders.some((r) => r.returnNo === n)),
      orderId, warehouseId, productId, qty, reason: str(b.reason), condition: String(b.condition ?? ReturnCondition[0]), disposition: null, restockLocationId: null,
      processedBy: null, processedAt: null, status: 'Received', receivedBy: c.userId, receivedAt: nowIso(c), notes: str(b.notes),
    };
    c.db.returnOrders.push(ret);
    return created(returnDto(c.db, ret));
  }),

  /** ReturnService.ProcessAsync: only good items can be restocked, into an unlocked bin of the same warehouse; the other dispositions leave stock alone. */
  route('POST', 'api/returns/{id}/process', (c) => {
    const b = asBody(c.body);
    new Validator(b).oneOf('disposition', 'Disposition', Disposition).id('restockLocationId', 'RestockLocationId', false).done();
    const ret = byId(c.db.returnOrders, c.params.id);
    if (!ret) throw new NotFoundError();
    if (ret.status === 'Processed' || ret.status === 'Completed') throw new BusinessRuleError('This return has already been processed.');
    const disposition = String(b.disposition ?? Disposition[0]);

    if (disposition === 'Restock') {
      if (ret.condition !== 'Good') throw new BusinessRuleError('Only items returned in good condition can be restocked.');
      const locationId = optId(b.restockLocationId);
      if (!locationId) throw new BusinessRuleError('Choose a location to restock into.');
      const location = byId(c.db.locations, locationId);
      if (!location) throw new BusinessRuleError('Restock location not found.');
      if (warehouseOfLocation(c.db, location) !== ret.warehouseId) throw new BusinessRuleError('The restock location belongs to a different warehouse.');
      if (location.status === 'Locked') throw new BusinessRuleError('The restock location is locked.');

      let inv = c.db.inventories.find((i) => i.productId === ret.productId && i.locationId === locationId);
      if (!inv) {
        inv = { id: c.newId(), productId: ret.productId, locationId, qtyOnHand: 0, qtyReserved: 0, lotNumber: null, expiryDate: null, unitCost: 0, status: 'Available', lastCountedDate: null };
        c.db.inventories.push(inv);
      }
      const before = inv.qtyOnHand;
      inv.qtyOnHand = round(inv.qtyOnHand + ret.qty);
      logTransaction(c, { productId: ret.productId, locationId, referenceType: 'Return', referenceId: ret.id, transactionType: 'In', qtyBefore: before, qtyAfter: inv.qtyOnHand, uomQty: ret.qty, lotNumber: inv.lotNumber, notes: ret.returnNo });
      ret.restockLocationId = locationId;
    }

    ret.disposition = disposition;
    ret.processedBy = c.userId;
    ret.processedAt = nowIso(c);
    ret.status = 'Completed';
    if (typeof b.notes === 'string' && b.notes.trim()) ret.notes = ret.notes?.trim() ? `${ret.notes}\n${b.notes}` : b.notes;
    if (ret.restockLocationId) syncLocationStatus(c.db, ret.restockLocationId);
    return ok(returnDto(c.db, ret));
  }),
];
