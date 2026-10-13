import { byId, warehouseOfLocation } from './db';
import type * as T from './types';
import { cmp } from './util';

/** Entity -> response DTO mapping, field for field the same as backend/Mapping/Mappers.cs (nulls are dropped by the engine). */

export const roleDto = (r: T.Role) => ({ id: r.id, name: r.name, description: r.description, permissions: r.permissions, isSystem: r.isSystem, createdAt: r.createdAt });

export const userDto = (db: T.Db, u: T.User) => ({
  id: u.id, username: u.username, email: u.email, fullName: u.fullName, roleId: u.roleId,
  roleName: byId(db.roles, u.roleId)?.name ?? '', warehouseId: u.warehouseId, warehouseName: byId(db.warehouses, u.warehouseId)?.name,
  phone: u.phone, isActive: u.isActive, lastLogin: u.lastLogin, createdAt: u.createdAt,
});

export const userInfo = (db: T.Db, u: T.User) => ({
  id: u.id, username: u.username, email: u.email, fullName: u.fullName, role: byId(db.roles, u.roleId)?.name ?? '',
  warehouse: byId(db.warehouses, u.warehouseId)?.name, phone: u.phone,
});

export const warehouseDto = (w: T.Warehouse) => ({ id: w.id, code: w.code, name: w.name, address: w.address, city: w.city, capacity: w.capacity, isActive: w.isActive, createdAt: w.createdAt });

export const zoneDto = (db: T.Db, z: T.Zone) => ({ id: z.id, warehouseId: z.warehouseId, warehouseName: byId(db.warehouses, z.warehouseId)?.name ?? '', name: z.name, zoneType: z.zoneType, description: z.description, isActive: z.isActive });

export const locationDto = (db: T.Db, l: T.Location) => {
  const zone = byId(db.zones, l.zoneId);
  return {
    id: l.id, zoneId: l.zoneId, zoneName: zone?.name ?? '', warehouseName: byId(db.warehouses, zone?.warehouseId)?.name ?? '',
    locationCode: l.locationCode, aisle: l.aisle, rack: l.rack, level: l.level, bin: l.bin, locationType: l.locationType, maxCapacity: l.maxCapacity, status: l.status,
  };
};

/** The category controller passes no parent name, so it is never part of the answer. */
export const categoryDto = (c: T.Category) => ({ id: c.id, name: c.name, parentId: c.parentId, level: c.level, description: c.description });

export const uomDto = (db: T.Db, u: T.Uom) => ({
  id: u.id, code: u.code, name: u.name, baseUomId: u.baseUomId, baseUomName: byId(db.uoms, u.baseUomId)?.name, conversionFactor: u.conversionFactor, isBase: u.isBase,
});

export const productDto = (db: T.Db, p: T.Product) => ({
  id: p.id, sku: p.sku, name: p.name, categoryId: p.categoryId, categoryName: byId(db.categories, p.categoryId)?.name, uomId: p.uomId,
  uomName: byId(db.uoms, p.uomId)?.name ?? '', weightKg: p.weightKg, volumeM3: p.volumeM3, barcode: p.barcode, minStock: p.minStock, maxStock: p.maxStock, isActive: p.isActive,
});

export const supplierDto = (s: T.Supplier) => ({ id: s.id, code: s.code, name: s.name, contactPerson: s.contactPerson, email: s.email, phone: s.phone, address: s.address, isActive: s.isActive });
export const carrierDto = (c: T.Carrier) => ({ id: c.id, name: c.name, contactPerson: c.contactPerson, phone: c.phone, email: c.email, vehicleType: c.vehicleType, isActive: c.isActive });

export const inventoryDto = (db: T.Db, i: T.Inventory) => {
  const loc = byId(db.locations, i.locationId);
  return {
    id: i.id, productId: i.productId, productSku: byId(db.products, i.productId)?.sku ?? '', productName: byId(db.products, i.productId)?.name ?? '',
    locationId: i.locationId, locationCode: loc?.locationCode ?? '', zoneName: byId(db.zones, loc?.zoneId)?.name ?? '',
    qtyOnHand: i.qtyOnHand, qtyReserved: i.qtyReserved, lotNumber: i.lotNumber, expiryDate: i.expiryDate, unitCost: i.unitCost, status: i.status, lastCountedDate: i.lastCountedDate,
  };
};

export const receiptItemDto = (db: T.Db, i: T.ReceiptItem) => ({
  id: i.id, receiptId: i.receiptId, productId: i.productId, productName: byId(db.products, i.productId)?.name ?? '', productSku: byId(db.products, i.productId)?.sku ?? '',
  expectedQty: i.expectedQty, receivedQty: i.receivedQty, damagedQty: i.damagedQty, lotNumber: i.lotNumber, expiryDate: i.expiryDate, unitCost: i.unitCost, status: i.status,
  putawayLocationId: i.putawayLocationId, putawayLocationCode: byId(db.locations, i.putawayLocationId)?.locationCode,
});

export const receiptDto = (db: T.Db, r: T.InboundReceipt, withItems = false) => ({
  id: r.id, receiptNo: r.receiptNo, supplierId: r.supplierId, supplierName: byId(db.suppliers, r.supplierId)?.name ?? '', warehouseId: r.warehouseId,
  warehouseName: byId(db.warehouses, r.warehouseId)?.name ?? '', poNumber: r.poNumber, asnNumber: r.asnNumber, status: r.status, expectedDate: r.expectedDate,
  receivedDate: r.receivedDate, receivedBy: r.receivedBy, receiverName: byId(db.users, r.receivedBy)?.fullName, dockDoor: r.dockDoor, notes: r.notes, createdAt: r.createdAt,
  items: withItems
    ? db.receiptItems.filter((i) => i.receiptId === r.id).sort((a, b) => cmp(byId(db.products, a.productId)?.sku ?? '', byId(db.products, b.productId)?.sku ?? '')).map((i) => receiptItemDto(db, i))
    : undefined,
});

export const orderItemDto = (db: T.Db, i: T.OrderItem) => ({
  id: i.id, orderId: i.orderId, productId: i.productId, productName: byId(db.products, i.productId)?.name ?? '', productSku: byId(db.products, i.productId)?.sku ?? '',
  orderedQty: i.orderedQty, pickedQty: i.pickedQty, shippedQty: i.shippedQty, unitPrice: i.unitPrice, status: i.status,
});

export const packListDto = (db: T.Db, p: T.PackList) => ({ id: p.id, cartonNo: p.cartonNo, weightKg: p.weightKg, packedAt: p.packedAt, packedByName: byId(db.users, p.packedBy)?.fullName ?? '', status: p.status });

export const shipmentDto = (db: T.Db, s: T.Shipment) => ({
  id: s.id, orderId: s.orderId, orderNumber: byId(db.outboundOrders, s.orderId)?.orderNumber ?? '', carrierId: s.carrierId, carrierName: byId(db.carriers, s.carrierId)?.name ?? '',
  trackingNo: s.trackingNo, bolNumber: s.bolNumber, vehiclePlate: s.vehiclePlate, driverName: s.driverName, status: s.status, shippedAt: s.shippedAt, deliveredAt: s.deliveredAt,
});

export const orderDto = (db: T.Db, o: T.OutboundOrder, withDetail = false) => ({
  id: o.id, orderNumber: o.orderNumber, warehouseId: o.warehouseId, warehouseName: byId(db.warehouses, o.warehouseId)?.name ?? '', orderType: o.orderType, status: o.status,
  priority: o.priority, destination: o.destination, requestedDate: o.requestedDate, dueDate: o.dueDate, createdBy: o.createdBy, creatorName: byId(db.users, o.createdBy)?.fullName ?? '',
  notes: o.notes, createdAt: o.createdAt,
  items: withDetail ? db.orderItems.filter((i) => i.orderId === o.id).sort((a, b) => cmp(byId(db.products, a.productId)?.sku ?? '', byId(db.products, b.productId)?.sku ?? '')).map((i) => orderItemDto(db, i)) : undefined,
  packLists: withDetail ? db.packLists.filter((p) => p.orderId === o.id).sort((a, b) => cmp(a.packedAt ?? '', b.packedAt ?? '')).map((p) => packListDto(db, p)) : undefined,
  shipments: withDetail ? db.shipments.filter((s) => s.orderId === o.id).sort((a, b) => cmp(a.shippedAt ?? '', b.shippedAt ?? '')).map((s) => shipmentDto(db, s)) : undefined,
});

export const pickTaskDto = (db: T.Db, t: T.PickTask) => {
  const item = byId(db.orderItems, t.orderItemId);
  const order = byId(db.outboundOrders, item?.orderId);
  return {
    id: t.id, orderId: item?.orderId ?? '', orderNumber: order?.orderNumber ?? '', orderItemId: t.orderItemId, productId: t.productId,
    productSku: byId(db.products, t.productId)?.sku ?? '', productName: byId(db.products, t.productId)?.name ?? '', fromLocationId: t.fromLocationId,
    fromLocationCode: byId(db.locations, t.fromLocationId)?.locationCode ?? '', qtyToPick: t.qtyToPick, qtyPicked: t.qtyPicked, status: t.status,
    pickerId: t.pickerId, pickerName: byId(db.users, t.pickerId)?.fullName, assignedAt: t.assignedAt, pickedAt: t.pickedAt,
  };
};

export const returnDto = (db: T.Db, r: T.ReturnOrder) => ({
  id: r.id, returnNo: r.returnNo, orderId: r.orderId, orderNumber: byId(db.outboundOrders, r.orderId)?.orderNumber, warehouseId: r.warehouseId,
  warehouseName: byId(db.warehouses, r.warehouseId)?.name ?? '', productId: r.productId, productSku: byId(db.products, r.productId)?.sku ?? '',
  productName: byId(db.products, r.productId)?.name ?? '', qty: r.qty, reason: r.reason, condition: r.condition, disposition: r.disposition,
  restockLocationId: r.restockLocationId, restockLocationCode: byId(db.locations, r.restockLocationId)?.locationCode, status: r.status,
  receiverName: byId(db.users, r.receivedBy)?.fullName, receivedAt: r.receivedAt, processedAt: r.processedAt, notes: r.notes,
});

export const transactionDto = (db: T.Db, t: T.InventoryTransaction) => ({
  id: t.id, productId: t.productId, productSku: byId(db.products, t.productId)?.sku ?? '', productName: byId(db.products, t.productId)?.name ?? '',
  locationId: t.locationId, locationCode: byId(db.locations, t.locationId)?.locationCode ?? '', referenceType: t.referenceType, referenceId: t.referenceId,
  transactionType: t.transactionType, qtyBefore: t.qtyBefore, qtyAfter: t.qtyAfter, uomQty: t.uomQty, userName: byId(db.users, t.userId)?.fullName ?? '', notes: t.notes, createdAt: t.createdAt,
});

export const transferDto = (db: T.Db, t: T.Transfer) => ({
  id: t.id, transferNo: t.transferNo, productId: t.productId, productName: byId(db.products, t.productId)?.name ?? '', productSku: byId(db.products, t.productId)?.sku ?? '',
  fromLocationId: t.fromLocationId, fromLocationCode: byId(db.locations, t.fromLocationId)?.locationCode ?? '', toLocationId: t.toLocationId,
  toLocationCode: byId(db.locations, t.toLocationId)?.locationCode ?? '', qty: t.qty, reason: t.reason, requestedBy: t.requestedBy,
  requesterName: byId(db.users, t.requestedBy)?.fullName ?? '', status: t.status, transferredAt: t.transferredAt,
});

export const adjustmentDto = (db: T.Db, a: T.Adjustment) => ({
  id: a.id, adjNumber: a.adjNumber, productId: a.productId, productName: byId(db.products, a.productId)?.name ?? '', locationId: a.locationId,
  locationCode: byId(db.locations, a.locationId)?.locationCode ?? '', adjType: a.adjType, qtyBefore: a.qtyBefore, adjQty: a.adjQty, qtyAfter: a.qtyAfter, reason: a.reason,
  createdBy: a.createdBy, creatorName: byId(db.users, a.createdBy)?.fullName ?? '', approvedBy: a.approvedBy, approverName: byId(db.users, a.approvedBy)?.fullName,
  status: a.status, createdAt: a.createdAt,
});

export { warehouseOfLocation };
