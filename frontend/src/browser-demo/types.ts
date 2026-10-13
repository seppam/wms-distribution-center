/** Row types of the in-browser database. They mirror the backend entities (camelCase); timestamps are ISO strings, dates are yyyy-MM-dd. */
export type Id = string;
type Ts = string;
type DateStr = string;

export interface Role { id: Id; name: string; description: string | null; permissions: string | null; isSystem: boolean; createdAt: Ts }
export interface User { id: Id; username: string; email: string; passwordHash: string; roleId: Id; warehouseId: Id | null; fullName: string; phone: string | null; isActive: boolean; lastLogin: Ts | null; createdAt: Ts; updatedAt: Ts }
export interface Warehouse { id: Id; code: string; name: string; address: string | null; city: string | null; capacity: number; isActive: boolean; createdAt: Ts }
export interface Zone { id: Id; warehouseId: Id; name: string; zoneType: string; description: string | null; isActive: boolean }
export interface Location { id: Id; zoneId: Id; locationCode: string; aisle: string | null; rack: string | null; level: string | null; bin: string | null; locationType: string; maxCapacity: number; status: string }
export interface Category { id: Id; name: string; parentId: Id | null; level: number; description: string | null }
export interface Uom { id: Id; code: string; name: string; baseUomId: Id | null; conversionFactor: number; isBase: boolean }
export interface Product { id: Id; sku: string; name: string; categoryId: Id | null; uomId: Id; weightKg: number; volumeM3: number; barcode: string | null; minStock: number; maxStock: number | null; isActive: boolean }
export interface Supplier { id: Id; code: string; name: string; contactPerson: string | null; email: string | null; phone: string | null; address: string | null; isActive: boolean }
export interface Carrier { id: Id; name: string; contactPerson: string | null; phone: string | null; email: string | null; vehicleType: string | null; isActive: boolean }
export interface Inventory { id: Id; productId: Id; locationId: Id; qtyOnHand: number; qtyReserved: number; lotNumber: string | null; expiryDate: DateStr | null; unitCost: number; status: string; lastCountedDate: DateStr | null }
export interface InventoryTransaction { id: number; productId: Id; locationId: Id; referenceType: string; referenceId: Id; transactionType: string; qtyBefore: number; qtyAfter: number; uomQty: number; userId: Id; lotNumber: string | null; notes: string | null; createdAt: Ts }
export interface InboundReceipt { id: Id; receiptNo: string; supplierId: Id; warehouseId: Id; poNumber: string | null; asnNumber: string | null; status: string; expectedDate: DateStr | null; receivedDate: Ts | null; receivedBy: Id | null; dockDoor: string | null; notes: string | null; createdAt: Ts }
export interface ReceiptItem { id: Id; receiptId: Id; productId: Id; expectedQty: number; receivedQty: number; damagedQty: number; lotNumber: string | null; expiryDate: DateStr | null; unitCost: number; status: string; putawayLocationId: Id | null }
export interface OutboundOrder { id: Id; orderNumber: string; warehouseId: Id; orderType: string; status: string; priority: string; destination: string | null; requestedDate: DateStr | null; dueDate: DateStr | null; createdBy: Id; notes: string | null; createdAt: Ts }
export interface OrderItem { id: Id; orderId: Id; productId: Id; orderedQty: number; pickedQty: number; shippedQty: number; unitPrice: number; status: string }
export interface PickTask { id: Id; orderItemId: Id; productId: Id; fromLocationId: Id; qtyToPick: number; qtyPicked: number; status: string; pickerId: Id | null; assignedAt: Ts | null; pickedAt: Ts | null }
export interface PackList { id: Id; orderId: Id; cartonNo: string; packedBy: Id; weightKg: number; packedAt: Ts | null; status: string }
export interface Shipment { id: Id; orderId: Id; carrierId: Id; trackingNo: string | null; bolNumber: string | null; vehiclePlate: string | null; driverName: string | null; status: string; shippedAt: Ts | null; deliveredAt: Ts | null }
export interface Adjustment { id: Id; adjNumber: string; productId: Id; locationId: Id; adjType: string; qtyBefore: number; adjQty: number; qtyAfter: number; reason: string | null; approvedBy: Id | null; createdBy: Id; status: string; createdAt: Ts }
export interface Transfer { id: Id; transferNo: string; productId: Id; fromLocationId: Id; toLocationId: Id; qty: number; reason: string | null; requestedBy: Id; status: string; transferredAt: Ts | null }
export interface ReturnOrder { id: Id; returnNo: string; orderId: Id | null; warehouseId: Id; productId: Id; qty: number; reason: string | null; condition: string; disposition: string | null; restockLocationId: Id | null; processedBy: Id | null; processedAt: Ts | null; status: string; receivedBy: Id | null; receivedAt: Ts | null; notes: string | null }

export const TABLES = [
  'roles', 'users', 'warehouses', 'zones', 'locations', 'uoms', 'categories', 'products', 'suppliers', 'carriers',
  'inventories', 'inventoryTransactions', 'inboundReceipts', 'receiptItems', 'outboundOrders', 'orderItems', 'pickTasks',
  'packLists', 'shipments', 'adjustments', 'transfers', 'returnOrders',
] as const;
export type TableName = (typeof TABLES)[number];

export interface Db {
  roles: Role[]; users: User[]; warehouses: Warehouse[]; zones: Zone[]; locations: Location[]; uoms: Uom[]; categories: Category[];
  products: Product[]; suppliers: Supplier[]; carriers: Carrier[]; inventories: Inventory[]; inventoryTransactions: InventoryTransaction[];
  inboundReceipts: InboundReceipt[]; receiptItems: ReceiptItem[]; outboundOrders: OutboundOrder[]; orderItems: OrderItem[];
  pickTasks: PickTask[]; packLists: PackList[]; shipments: Shipment[]; adjustments: Adjustment[]; transfers: Transfer[]; returnOrders: ReturnOrder[];
}

/** The shared permission contract (contracts/permission-matrix.json). */
export interface PermissionMatrix {
  principals: string[];
  policies: Record<string, string[]>;
  endpoints: Record<string, string>;
}

/** The shared dataset contract (contracts/demo-seed.json). */
export interface SeedFile { tables: Record<TableName, Record<string, unknown>[]> }

export interface ApiRequest {
  method: string;
  /** Path with or without the /api prefix and an optional query string. */
  path: string;
  query?: Record<string, unknown>;
  body?: unknown;
  /** Bearer token as sent by the web app. */
  token?: string | null;
}

export interface ApiResponse { status: number; data?: unknown; headers?: Record<string, string> }
