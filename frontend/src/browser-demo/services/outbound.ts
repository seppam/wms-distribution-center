import { available, byId, countToday, deleteRow, logTransaction, must, nextNumber, nowIso, syncLocationStatus, warehouseOfLocation } from '../db';
import { orderDto, orderItemDto, pickTaskDto, shipmentDto } from '../dto';
import { FLOOR_ROLES, OrderType, Priority } from '../enums';
import { BusinessRuleError, ConflictError, NotFoundError } from '../errors';
import { created, noContent, ok, route, type Route, type RouteCtx } from '../route';
import type { OrderItem, OutboundOrder } from '../types';
import { asBody, cmp, fmt, num, optId, round, str, sum, Validator } from '../util';

const has = (b: Record<string, unknown>, k: string) => b[k] !== undefined && b[k] !== null;
const orderOf = (c: RouteCtx) => byId(c.db.outboundOrders, c.params.id);
const detail = (c: RouteCtx, o: OutboundOrder) => orderDto(c.db, o, true);
const tasksOf = (c: RouteCtx, orderId: string) => {
  const itemIds = new Set(c.db.orderItems.filter((i) => i.orderId === orderId).map((i) => i.id));
  return c.db.pickTasks.filter((t) => itemIds.has(t.orderItemId));
};

function validateLine(v: Validator): void {
  v.id('productId', 'ProductId').range('orderedQty', 'OrderedQty', 0.0001, 1_000_000).range('unitPrice', 'UnitPrice', 0, 100_000_000);
}

/** OutboundService.BuildItemAsync */
function buildItem(c: RouteCtx, orderId: string, b: Record<string, unknown>, existing: string[]): OrderItem {
  const productId = String(b.productId);
  if (existing.includes(productId)) throw new BusinessRuleError('This product is already on the order.');
  if (!c.db.products.some((p) => p.id === productId && p.isActive)) throw new BusinessRuleError('Product not found or inactive.');
  return { id: c.newId(), orderId, productId, orderedQty: num(b.orderedQty), pickedQty: 0, shippedQty: 0, unitPrice: num(b.unitPrice), status: 'Pending' };
}

export const outboundRoutes: Route[] = [
  route('GET', 'api/outboundorders', (c) => ok([...c.db.outboundOrders].sort((a, b) => b.createdAt.localeCompare(a.createdAt)).map((o) => orderDto(c.db, o)))),

  route('GET', 'api/outboundorders/{id}', (c) => {
    const o = orderOf(c);
    if (!o) throw new NotFoundError();
    return ok(detail(c, o));
  }),

  route('GET', 'api/outboundorders/{id}/items', (c) => ok(c.db.orderItems.filter((i) => i.orderId === c.params.id).map((i) => orderItemDto(c.db, i)))),

  route('POST', 'api/outboundorders', (c) => {
    const b = asBody(c.body);
    new Validator(b).id('warehouseId', 'WarehouseId').oneOf('orderType', 'OrderType', OrderType).oneOf('priority', 'Priority', Priority).nested('items', 'Items', validateLine).done();
    const warehouseId = String(b.warehouseId);
    if (!c.db.warehouses.some((w) => w.id === warehouseId && w.isActive)) throw new BusinessRuleError('Warehouse not found or inactive.');
    const order: OutboundOrder = {
      id: c.newId(),
      orderNumber: nextNumber('ORD', countToday(c.db.outboundOrders, (o) => o.createdAt, c.now), c.now, (n) => c.db.outboundOrders.some((o) => o.orderNumber === n)),
      warehouseId, orderType: String(b.orderType ?? OrderType[0]), status: 'Draft', priority: String(b.priority ?? Priority[0]), destination: str(b.destination),
      requestedDate: str(b.requestedDate), dueDate: str(b.dueDate), createdBy: c.userId ?? '', notes: str(b.notes), createdAt: nowIso(c),
    };
    const lines: OrderItem[] = [];
    for (const line of Array.isArray(b.items) ? b.items : []) lines.push(buildItem(c, order.id, asBody(line), lines.map((i) => i.productId)));
    c.db.outboundOrders.push(order);
    c.db.orderItems.push(...lines);
    return created(detail(c, order));
  }),

  route('PUT', 'api/outboundorders/{id}', (c) => {
    const o = orderOf(c);
    if (!o) throw new NotFoundError();
    if (o.status !== 'Draft') throw new BusinessRuleError('Only draft orders can be edited.');
    const b = asBody(c.body);
    new Validator(b).oneOf('priority', 'Priority', Priority).done();
    if (has(b, 'priority')) o.priority = String(b.priority);
    if (has(b, 'destination')) o.destination = String(b.destination);
    if (has(b, 'dueDate')) o.dueDate = String(b.dueDate);
    if (has(b, 'notes')) o.notes = String(b.notes);
    return ok(detail(c, o));
  }),

  route('DELETE', 'api/outboundorders/{id}', (c) => {
    const o = orderOf(c);
    if (!o) throw new NotFoundError();
    if (o.status !== 'Draft') throw new BusinessRuleError('Only draft orders can be deleted; cancel the order instead.');
    deleteRow(c.db, 'outboundOrders', o.id);
    return noContent();
  }),

  route('POST', 'api/outboundorders/{id}/items', (c) => {
    const b = asBody(c.body);
    new Validator(b).id('productId', 'ProductId').range('orderedQty', 'OrderedQty', 0.0001, 1_000_000).range('unitPrice', 'UnitPrice', 0, 100_000_000).done();
    const o = must(c.db.outboundOrders, c.params.id, 'Order not found.');
    if (o.status !== 'Draft') throw new BusinessRuleError('Lines can only be added to draft orders.');
    const item = buildItem(c, o.id, b, c.db.orderItems.filter((i) => i.orderId === o.id).map((i) => i.productId));
    c.db.orderItems.push(item);
    return ok(orderItemDto(c.db, item));
  }),

  /**
   * OutboundService.StartPickingAsync. Allocation is all-or-nothing: every line is planned against what is available
   * (first-expiry-first-out, then the fullest bin) before anything is reserved, so no order is ever over-allocated.
   */
  route('POST', 'api/outboundorders/{id}/start-picking', (c) => {
    const order = orderOf(c);
    if (!order) throw new NotFoundError();
    if (order.status !== 'Draft') throw new BusinessRuleError(`Only draft orders can start picking (this order is ${order.status}).`);
    const items = c.db.orderItems.filter((i) => i.orderId === order.id);
    if (items.length === 0) throw new BusinessRuleError('The order has no lines.');

    const plan: { item: OrderItem; inv: (typeof c.db.inventories)[number]; qty: number }[] = [];
    for (const item of items) {
      const candidates = c.db.inventories.filter((i) => {
        const loc = byId(c.db.locations, i.locationId);
        return i.productId === item.productId && i.status === 'Available' && i.qtyOnHand - i.qtyReserved > 0 && !!loc && warehouseOfLocation(c.db, loc) === order.warehouseId;
      });
      const ordered = [...candidates].sort((a, b) => cmp(a.expiryDate ?? '9999-12-31', b.expiryDate ?? '9999-12-31') || available(b) - available(a));
      const total = sum(ordered.map(available));
      if (total < item.orderedQty) {
        const sku = byId(c.db.products, item.productId)?.sku;
        throw new BusinessRuleError(`Insufficient available stock for ${sku}: ordered ${fmt(item.orderedQty)}, available ${fmt(total)}.`);
      }
      let remaining = item.orderedQty;
      for (const inv of ordered) {
        if (remaining <= 0) break;
        const qty = Math.min(remaining, available(inv));
        plan.push({ item, inv, qty });
        remaining = round(remaining - qty);
      }
    }

    for (const { item, inv, qty } of plan) {
      c.db.pickTasks.push({ id: c.newId(), orderItemId: item.id, productId: item.productId, fromLocationId: inv.locationId, qtyToPick: qty, qtyPicked: 0, status: 'Assigned', pickerId: null, assignedAt: nowIso(c), pickedAt: null });
      inv.qtyReserved = round(inv.qtyReserved + qty);
    }
    order.status = 'Picking';
    return ok(detail(c, order));
  }),

  route('POST', 'api/outboundorders/{id}/assign-picker', (c) => {
    const b = asBody(c.body);
    new Validator(b).id('pickerId', 'PickerId').done();
    const order = orderOf(c);
    if (!order) throw new NotFoundError();
    if (order.status !== 'Picking') throw new BusinessRuleError('Pickers can only be assigned while the order is being picked.');
    const picker = c.db.users.find((u) => u.id === b.pickerId && u.isActive);
    if (!picker) throw new BusinessRuleError('Picker not found or inactive.');
    if (!FLOOR_ROLES.includes(byId(c.db.roles, picker.roleId)?.name ?? '')) throw new BusinessRuleError("The picker's role cannot perform floor work.");
    for (const t of tasksOf(c, order.id).filter((x) => x.status !== 'Picked' && x.status !== 'Cancelled')) {
      t.pickerId = picker.id;
      t.assignedAt = nowIso(c);
    }
    return ok(detail(c, order));
  }),

  route('GET', 'api/pick-tasks', (c) => {
    const rank = (s: string) => (s === 'Picked' || s === 'Cancelled' ? 1 : 0);
    const mine = String(c.query.mine ?? '').toLowerCase() === 'true';
    const rows = c.db.pickTasks
      .filter((t) => !has(c.query, 'status') || t.status === c.query.status)
      .filter((t) => !has(c.query, 'orderId') || byId(c.db.orderItems, t.orderItemId)?.orderId === c.query.orderId)
      .filter((t) => !mine || t.pickerId === c.userId);
    const orderNo = (t: (typeof rows)[number]) => byId(c.db.outboundOrders, byId(c.db.orderItems, t.orderItemId)?.orderId)?.orderNumber ?? '';
    const loc = (t: (typeof rows)[number]) => byId(c.db.locations, t.fromLocationId)?.locationCode ?? '';
    return ok(rows.sort((a, b) => rank(a.status) - rank(b.status) || cmp(orderNo(a), orderNo(b)) || cmp(loc(a), loc(b))).map((t) => pickTaskDto(c.db, t)));
  }),

  /** OutboundService.ConfirmPickAsync: over-pick rejected, a short pick releases the reservation for what was not found. */
  route('POST', 'api/pick-tasks/{id}/confirm', (c) => {
    const b = asBody(c.body);
    new Validator(b).range('qtyPicked', 'QtyPicked', 0, 1_000_000).done();
    const task = byId(c.db.pickTasks, c.params.id);
    if (!task) throw new NotFoundError();
    const item = must(c.db.orderItems, task.orderItemId, '');
    const order = must(c.db.outboundOrders, item.orderId, '');
    const qtyPicked = num(b.qtyPicked);
    if (order.status !== 'Picking') throw new BusinessRuleError(`The order is ${order.status}; picks can only be confirmed while it is in Picking.`);
    if (task.status === 'Picked' || task.status === 'Cancelled') throw new BusinessRuleError(`This pick task is already ${task.status}.`);
    if (qtyPicked < 0 || qtyPicked > task.qtyToPick)
      throw new BusinessRuleError(`Over-pick rejected: you can pick at most ${fmt(task.qtyToPick)} (requested ${fmt(qtyPicked)}).`);

    const inv = c.db.inventories.find((i) => i.productId === task.productId && i.locationId === task.fromLocationId);
    if (!inv) throw new ConflictError('The stock record for this pick no longer exists.');
    if (inv.qtyOnHand < qtyPicked) throw new BusinessRuleError('Not enough stock on hand at the pick location.');

    const shortfall = round(task.qtyToPick - qtyPicked);
    inv.qtyReserved = Math.max(0, round(inv.qtyReserved - shortfall));
    task.qtyPicked = qtyPicked;
    task.status = 'Picked';
    task.pickedAt = nowIso(c);
    task.pickerId ??= c.userId;
    item.pickedQty = round(item.pickedQty + qtyPicked);

    // When the last task of the order is done, the order is Picked
    const siblings = tasksOf(c, order.id).filter((t) => t.id !== task.id);
    if (siblings.every((t) => t.status === 'Picked' || t.status === 'Cancelled')) {
      for (const i of c.db.orderItems.filter((x) => x.orderId === order.id)) i.status = i.pickedQty >= i.orderedQty ? 'Picked' : 'Partial';
      order.status = 'Picked';
    }
    return ok(pickTaskDto(c.db, task));
  }),

  route('POST', 'api/outboundorders/{id}/pack', (c) => {
    const b = asBody(c.body);
    new Validator(b).required('cartonNo', 'CartonNo').maxLength('cartonNo', 'CartonNo', 100).range('weightKg', 'WeightKg', 0, 100_000).done();
    const order = orderOf(c);
    if (!order) throw new NotFoundError();
    if (order.status !== 'Picked') throw new BusinessRuleError(`Only picked orders can be packed (this order is ${order.status}).`);
    if (sum(c.db.orderItems.filter((i) => i.orderId === order.id).map((i) => i.pickedQty)) <= 0)
      throw new BusinessRuleError('Nothing was picked for this order; cancel it instead of packing.');
    c.db.packLists.push({ id: c.newId(), orderId: order.id, cartonNo: String(b.cartonNo).trim(), packedBy: c.userId ?? '', weightKg: num(b.weightKg), packedAt: nowIso(c), status: 'Open' });
    order.status = 'Packed';
    return ok(detail(c, order));
  }),

  route('POST', 'api/outboundorders/{id}/ship', (c) => {
    const b = asBody(c.body);
    new Validator(b).id('carrierId', 'CarrierId').done();
    const order = must(c.db.outboundOrders, c.params.id, 'Order not found.');
    if (order.status !== 'Packed') throw new BusinessRuleError('Only packed orders can be shipped.');
    if (!c.db.carriers.some((x) => x.id === b.carrierId && x.isActive)) throw new BusinessRuleError('Carrier not found or inactive.');

    const touched: string[] = [];
    for (const task of tasksOf(c, order.id).filter((t) => t.status === 'Picked' && t.qtyPicked > 0)) {
      const inv = c.db.inventories.find((i) => i.productId === task.productId && i.locationId === task.fromLocationId);
      if (!inv) throw new ConflictError('Stock record for a picked line is missing.');
      if (inv.qtyOnHand < task.qtyPicked) throw new ConflictError('Stock on hand is lower than the picked quantity; check recent adjustments.');
      const before = inv.qtyOnHand;
      inv.qtyOnHand = round(inv.qtyOnHand - task.qtyPicked);
      inv.qtyReserved = Math.max(0, round(inv.qtyReserved - task.qtyPicked));
      logTransaction(c, { productId: task.productId, locationId: task.fromLocationId, referenceType: 'Outbound', referenceId: order.id, transactionType: 'Out', qtyBefore: before, qtyAfter: inv.qtyOnHand, uomQty: task.qtyPicked, lotNumber: inv.lotNumber, notes: null });
      touched.push(task.fromLocationId);
    }
    for (const item of c.db.orderItems.filter((i) => i.orderId === order.id)) { item.shippedQty = item.pickedQty; item.status = 'Shipped'; }
    const shipment = {
      id: c.newId(), orderId: order.id, carrierId: String(b.carrierId), trackingNo: str(b.trackingNo), bolNumber: str(b.bolNumber), vehiclePlate: str(b.vehiclePlate),
      driverName: str(b.driverName), status: 'Dispatched', shippedAt: nowIso(c), deliveredAt: null,
    };
    order.status = 'Shipped';
    c.db.shipments.push(shipment);
    syncLocationStatus(c.db, ...touched);
    return ok(shipmentDto(c.db, shipment));
  }),

  route('POST', 'api/outboundorders/{id}/cancel', (c) => {
    const order = orderOf(c);
    if (!order) throw new NotFoundError();
    if (order.status === 'Shipped' || order.status === 'Completed' || order.status === 'Cancelled') throw new BusinessRuleError(`A ${order.status} order cannot be cancelled.`);
    // Release everything still reserved for this order
    for (const task of tasksOf(c, order.id).filter((t) => t.status !== 'Cancelled')) {
      const held = task.status === 'Picked' ? task.qtyPicked : task.qtyToPick;
      const inv = c.db.inventories.find((i) => i.productId === task.productId && i.locationId === task.fromLocationId);
      if (inv) inv.qtyReserved = Math.max(0, round(inv.qtyReserved - held));
      task.status = 'Cancelled';
    }
    order.status = 'Cancelled';
    return ok(detail(c, order));
  }),

  route('GET', 'api/shipments', (c) => ok([...c.db.shipments].sort((a, b) => cmp(b.shippedAt ?? '', a.shippedAt ?? '')).map((s) => shipmentDto(c.db, s)))),

  route('POST', 'api/shipments/{id}/deliver', (c) => {
    const shipment = byId(c.db.shipments, c.params.id);
    if (!shipment) throw new NotFoundError();
    if (shipment.status === 'Delivered') throw new BusinessRuleError('The shipment is already delivered.');
    shipment.status = 'Delivered';
    shipment.deliveredAt = nowIso(c);
    const order = byId(c.db.outboundOrders, shipment.orderId);
    if (order) order.status = 'Completed';
    return ok(shipmentDto(c.db, shipment));
  }),
];
