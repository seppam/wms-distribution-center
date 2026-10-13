import { byId, countToday, deleteRow, logTransaction, must, nextNumber, syncLocationStatus, warehouseOfLocation, nowIso } from '../db';
import { receiptDto, receiptItemDto } from '../dto';
import { BusinessRuleError, NotFoundError } from '../errors';
import { created, noContent, ok, route, type Route, type RouteCtx } from '../route';
import type { InboundReceipt, ReceiptItem } from '../types';
import { asBody, num, optId, round, str, Validator } from '../util';

const has = (b: Record<string, unknown>, k: string) => b[k] !== undefined && b[k] !== null;

function validateLine(v: Validator): void {
  v.id('productId', 'ProductId').range('expectedQty', 'ExpectedQty', 0.0001, 1_000_000).range('unitCost', 'UnitCost', 0, 100_000_000).id('putawayLocationId', 'PutawayLocationId', false);
}

/** InboundService.BuildItemAsync */
function buildItem(c: RouteCtx, receiptId: string, b: Record<string, unknown>, existingProducts: string[]): ReceiptItem {
  const productId = String(b.productId);
  if (existingProducts.includes(productId)) throw new BusinessRuleError('This product is already on the receipt.');
  if (!c.db.products.some((p) => p.id === productId && p.isActive)) throw new BusinessRuleError('Product not found or inactive.');
  const putaway = optId(b.putawayLocationId);
  if (putaway && !c.db.locations.some((l) => l.id === putaway)) throw new BusinessRuleError('Put-away location not found.');
  return {
    id: c.newId(), receiptId, productId, expectedQty: num(b.expectedQty), receivedQty: 0, damagedQty: 0, lotNumber: str(b.lotNumber),
    expiryDate: str(b.expiryDate), unitCost: num(b.unitCost), status: 'Pending', putawayLocationId: putaway,
  };
}

const detail = (c: RouteCtx, r: InboundReceipt) => receiptDto(c.db, r, true);
const receiptOf = (c: RouteCtx) => byId(c.db.inboundReceipts, c.params.id);

export const inboundRoutes: Route[] = [
  route('GET', 'api/inboundreceipts', (c) =>
    ok([...c.db.inboundReceipts].sort((a, b) => b.createdAt.localeCompare(a.createdAt)).map((r) => receiptDto(c.db, r)))),

  route('GET', 'api/inboundreceipts/{id}', (c) => {
    const r = receiptOf(c);
    if (!r) throw new NotFoundError();
    return ok(detail(c, r));
  }),

  route('POST', 'api/inboundreceipts', (c) => {
    const b = asBody(c.body);
    new Validator(b).id('supplierId', 'SupplierId').id('warehouseId', 'WarehouseId').nested('items', 'Items', validateLine).done();
    const supplierId = String(b.supplierId);
    const warehouseId = String(b.warehouseId);
    if (!c.db.suppliers.some((s) => s.id === supplierId && s.isActive)) throw new BusinessRuleError('Supplier not found or inactive.');
    if (!c.db.warehouses.some((w) => w.id === warehouseId && w.isActive)) throw new BusinessRuleError('Warehouse not found or inactive.');

    const receipt: InboundReceipt = {
      id: c.newId(),
      receiptNo: nextNumber('RCV', countToday(c.db.inboundReceipts, (r) => r.createdAt, c.now), c.now, (n) => c.db.inboundReceipts.some((r) => r.receiptNo === n)),
      supplierId, warehouseId, poNumber: str(b.poNumber), asnNumber: str(b.asnNumber), status: 'Draft', expectedDate: str(b.expectedDate), receivedDate: null,
      receivedBy: null, dockDoor: str(b.dockDoor), notes: str(b.notes), createdAt: nowIso(c),
    };
    const lines: ReceiptItem[] = [];
    for (const line of Array.isArray(b.items) ? b.items : [])
      lines.push(buildItem(c, receipt.id, asBody(line), lines.map((i) => i.productId)));
    c.db.inboundReceipts.push(receipt);
    c.db.receiptItems.push(...lines);
    return created(detail(c, receipt));
  }),

  route('PUT', 'api/inboundreceipts/{id}', (c) => {
    const r = receiptOf(c);
    if (!r) throw new NotFoundError();
    if (r.status !== 'Draft') throw new BusinessRuleError('Only draft receipts can be edited.');
    const b = asBody(c.body);
    if (has(b, 'poNumber')) r.poNumber = String(b.poNumber);
    if (has(b, 'asnNumber')) r.asnNumber = String(b.asnNumber);
    if (has(b, 'expectedDate')) r.expectedDate = String(b.expectedDate);
    if (has(b, 'dockDoor')) r.dockDoor = String(b.dockDoor);
    if (has(b, 'notes')) r.notes = String(b.notes);
    return ok(detail(c, r));
  }),

  route('DELETE', 'api/inboundreceipts/{id}', (c) => {
    const r = receiptOf(c);
    if (!r) throw new NotFoundError();
    if (r.status !== 'Draft') throw new BusinessRuleError('Only draft receipts can be deleted.');
    deleteRow(c.db, 'inboundReceipts', r.id);
    return noContent();
  }),

  route('POST', 'api/inboundreceipts/{id}/items', (c) => {
    const b = asBody(c.body);
    new Validator(b).id('productId', 'ProductId').range('expectedQty', 'ExpectedQty', 0.0001, 1_000_000).range('unitCost', 'UnitCost', 0, 100_000_000).done();
    const r = must(c.db.inboundReceipts, c.params.id, 'Receipt not found.');
    if (r.status !== 'Draft' && r.status !== 'InProgress') throw new BusinessRuleError('Lines can only be added to open receipts.');
    const item = buildItem(c, r.id, b, c.db.receiptItems.filter((i) => i.receiptId === r.id).map((i) => i.productId));
    c.db.receiptItems.push(item);
    return ok(receiptItemDto(c.db, item));
  }),

  /** InboundService.ReceiveItemAsync: put-away in the same step; repeating a line posts only the change in good quantity. */
  route('POST', 'api/inboundreceipts/{id}/receive', (c) => {
    const b = asBody(c.body);
    new Validator(b).id('receiptItemId', 'ReceiptItemId').range('receivedQty', 'ReceivedQty', 0, 1_000_000).range('damagedQty', 'DamagedQty', 0, 1_000_000).id('putawayLocationId', 'PutawayLocationId', false).done();
    const receipt = receiptOf(c);
    if (!receipt) throw new NotFoundError();
    if (receipt.status === 'Received' || receipt.status === 'Cancelled') throw new BusinessRuleError(`A ${receipt.status} receipt cannot receive more goods.`);
    const item = c.db.receiptItems.find((i) => i.id === b.receiptItemId && i.receiptId === receipt.id);
    if (!item) throw new NotFoundError();

    const receivedQty = num(b.receivedQty);
    const damagedQty = num(b.damagedQty);
    if (damagedQty > receivedQty) throw new BusinessRuleError('Damaged quantity cannot exceed the received quantity.');

    const locationId = optId(b.putawayLocationId) ?? item.putawayLocationId;
    if (!locationId) throw new BusinessRuleError('Choose a put-away location.');
    const location = byId(c.db.locations, locationId);
    if (!location) throw new BusinessRuleError('Put-away location not found.');
    if (warehouseOfLocation(c.db, location) !== receipt.warehouseId) throw new BusinessRuleError('The put-away location belongs to a different warehouse.');
    if (location.status === 'Locked') throw new BusinessRuleError('The put-away location is locked.');

    const previousGood = item.receivedQty - item.damagedQty;
    const newGood = receivedQty - damagedQty;
    const delta = round(newGood - previousGood);

    let inv = c.db.inventories.find((i) => i.productId === item.productId && i.locationId === locationId);
    if (item.putawayLocationId && item.putawayLocationId !== locationId && previousGood > 0)
      throw new BusinessRuleError('Stock from this line was already put away to another location; keep the same location.');

    const lot = has(b, 'lotNumber') ? String(b.lotNumber) : item.lotNumber;
    if (inv && delta > 0 && (inv.lotNumber ?? null) !== (lot ?? null) && inv.qtyOnHand > 0)
      throw new BusinessRuleError('This location already holds another lot of the product; choose a different location.');
    if (inv && inv.qtyOnHand + delta < inv.qtyReserved) throw new BusinessRuleError('Reducing the received quantity would drop stock below what is reserved for orders.');
    if (!inv && delta < 0) throw new BusinessRuleError('There is no stock at this location to reduce.');

    item.receivedQty = receivedQty;
    item.damagedQty = damagedQty;
    item.putawayLocationId = locationId;
    if (has(b, 'lotNumber')) item.lotNumber = String(b.lotNumber);
    if (has(b, 'expiryDate')) item.expiryDate = String(b.expiryDate);
    item.status = damagedQty > 0 ? 'Damaged' : item.receivedQty < item.expectedQty ? 'Shortage' : 'Received';

    if (delta !== 0) {
      if (!inv) {
        inv = { id: c.newId(), productId: item.productId, locationId, qtyOnHand: 0, qtyReserved: 0, lotNumber: item.lotNumber, expiryDate: item.expiryDate, unitCost: item.unitCost, status: 'Available', lastCountedDate: null };
        c.db.inventories.push(inv);
      }
      const before = inv.qtyOnHand;
      inv.qtyOnHand = round(inv.qtyOnHand + delta);
      logTransaction(c, { productId: item.productId, locationId, referenceType: 'Inbound', referenceId: receipt.id, transactionType: 'In', qtyBefore: before, qtyAfter: inv.qtyOnHand, uomQty: delta, lotNumber: item.lotNumber, notes: null });
    }

    receipt.status = 'InProgress';
    receipt.receivedBy = c.userId;
    receipt.receivedDate = nowIso(c);
    syncLocationStatus(c.db, locationId);
    return ok(receiptItemDto(c.db, item));
  }),

  route('POST', 'api/inboundreceipts/{id}/complete', (c) => {
    const receipt = receiptOf(c);
    if (!receipt) throw new NotFoundError();
    const items = c.db.receiptItems.filter((i) => i.receiptId === receipt.id);
    if (receipt.status === 'Received' || receipt.status === 'Partial' || receipt.status === 'Cancelled') throw new BusinessRuleError(`The receipt is already ${receipt.status}.`);
    if (items.length === 0) throw new BusinessRuleError('A receipt without lines cannot be completed.');
    if (items.every((i) => i.status === 'Pending')) throw new BusinessRuleError('Receive at least one line before completing the receipt.');
    // Fully matching delivery -> Received; any shortage, damage or untouched line -> Partial
    receipt.status = items.every((i) => i.status === 'Received') ? 'Received' : 'Partial';
    receipt.receivedBy = c.userId;
    receipt.receivedDate = nowIso(c);
    return ok(detail(c, receipt));
  }),
];
