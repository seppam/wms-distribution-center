import { ConflictError, REFERENCED_MESSAGE, NotFoundError } from './errors';
import type { Db, Id, InventoryTransaction, TableName, Location } from './types';
import { compactDay, isSameDay } from './util';

export interface Ctx {
  db: Db;
  now: Date;
  newId: () => string;
  userId: string | null;
}

export const nowIso = (c: Ctx) => c.now.toISOString();

// ----------------------------------------------------------------------------- lookups

export function must<T extends { id: Id }>(rows: T[], id: Id | null | undefined, message: string): T {
  const r = id ? rows.find((x) => x.id === id) : undefined;
  if (!r) throw new NotFoundError(message);
  return r;
}
export const byId = <T extends { id: Id }>(rows: T[], id: Id | null | undefined): T | undefined => (id ? rows.find((x) => x.id === id) : undefined);

export const warehouseOfLocation = (db: Db, loc: Location): Id | undefined => db.zones.find((z) => z.id === loc.zoneId)?.warehouseId;

// ----------------------------------------------------------------------------- numbering (StockHelper.NextNumber)

/** PREFIX-yyyymmdd-0001 from the number of documents created today; skips numbers already taken (see README: known differences). */
export function nextNumber(prefix: string, todaysCount: number, now: Date, taken: (n: string) => boolean): string {
  const day = compactDay(now);
  let n = todaysCount + 1;
  let candidate = `${prefix}-${day}-${String(n).padStart(4, '0')}`;
  while (taken(candidate)) candidate = `${prefix}-${day}-${String(++n).padStart(4, '0')}`;
  return candidate;
}
export const countToday = <T>(rows: T[], at: (r: T) => string | null, now: Date): number => rows.filter((r) => isSameDay(at(r), now)).length;

// ----------------------------------------------------------------------------- stock bookkeeping (StockHelper)

export const available = (i: { qtyOnHand: number; qtyReserved: number }): number => i.qtyOnHand - i.qtyReserved;

export function logTransaction(c: Ctx, t: Omit<InventoryTransaction, 'id' | 'createdAt' | 'userId'> & { userId?: string }): void {
  const id = c.db.inventoryTransactions.reduce((m, x) => Math.max(m, x.id), 0) + 1;
  c.db.inventoryTransactions.push({ ...t, id, userId: t.userId ?? c.userId ?? '', createdAt: nowIso(c) });
}

/** Marks locations Occupied when they hold stock and Empty when they do not (Locked locations are left alone). */
export function syncLocationStatus(db: Db, ...locationIds: Id[]): void {
  for (const id of new Set(locationIds)) {
    const loc = db.locations.find((l) => l.id === id);
    if (!loc || loc.status === 'Locked') continue;
    const hasStock = db.inventories.some((i) => i.locationId === id && i.qtyOnHand > 0);
    const target = hasStock ? 'Occupied' : 'Empty';
    if (loc.status !== target) loc.status = target;
  }
}

// ----------------------------------------------------------------------------- referential integrity (EF OnDelete behaviours)

type OnDelete = 'restrict' | 'cascade' | 'setnull';
interface Ref { table: TableName; field: string; on: OnDelete }

/** Foreign keys of WmsDbContext: which rows point at a table and what happens when the target is deleted. */
const REFS: Partial<Record<TableName, Ref[]>> = {
  roles: [{ table: 'users', field: 'roleId', on: 'restrict' }],
  warehouses: [
    { table: 'users', field: 'warehouseId', on: 'setnull' }, { table: 'zones', field: 'warehouseId', on: 'cascade' },
    { table: 'inboundReceipts', field: 'warehouseId', on: 'restrict' }, { table: 'outboundOrders', field: 'warehouseId', on: 'restrict' },
    { table: 'returnOrders', field: 'warehouseId', on: 'restrict' },
  ],
  zones: [{ table: 'locations', field: 'zoneId', on: 'cascade' }],
  locations: [
    { table: 'inventories', field: 'locationId', on: 'cascade' }, { table: 'inventoryTransactions', field: 'locationId', on: 'restrict' },
    { table: 'receiptItems', field: 'putawayLocationId', on: 'setnull' }, { table: 'pickTasks', field: 'fromLocationId', on: 'restrict' },
    { table: 'returnOrders', field: 'restockLocationId', on: 'setnull' }, { table: 'adjustments', field: 'locationId', on: 'restrict' },
    { table: 'transfers', field: 'fromLocationId', on: 'restrict' }, { table: 'transfers', field: 'toLocationId', on: 'restrict' },
  ],
  categories: [{ table: 'categories', field: 'parentId', on: 'restrict' }, { table: 'products', field: 'categoryId', on: 'setnull' }],
  uoms: [{ table: 'uoms', field: 'baseUomId', on: 'restrict' }, { table: 'products', field: 'uomId', on: 'restrict' }],
  products: [
    { table: 'inventories', field: 'productId', on: 'cascade' }, { table: 'inventoryTransactions', field: 'productId', on: 'restrict' },
    { table: 'receiptItems', field: 'productId', on: 'restrict' }, { table: 'orderItems', field: 'productId', on: 'restrict' },
    { table: 'pickTasks', field: 'productId', on: 'restrict' }, { table: 'returnOrders', field: 'productId', on: 'restrict' },
    { table: 'adjustments', field: 'productId', on: 'restrict' }, { table: 'transfers', field: 'productId', on: 'restrict' },
  ],
  suppliers: [{ table: 'inboundReceipts', field: 'supplierId', on: 'restrict' }],
  carriers: [{ table: 'shipments', field: 'carrierId', on: 'restrict' }],
  users: [
    { table: 'inventoryTransactions', field: 'userId', on: 'restrict' }, { table: 'inboundReceipts', field: 'receivedBy', on: 'setnull' },
    { table: 'outboundOrders', field: 'createdBy', on: 'restrict' }, { table: 'pickTasks', field: 'pickerId', on: 'setnull' },
    { table: 'packLists', field: 'packedBy', on: 'restrict' }, { table: 'returnOrders', field: 'receivedBy', on: 'setnull' },
    { table: 'adjustments', field: 'approvedBy', on: 'setnull' }, { table: 'adjustments', field: 'createdBy', on: 'restrict' },
    { table: 'transfers', field: 'requestedBy', on: 'restrict' },
  ],
  inboundReceipts: [{ table: 'receiptItems', field: 'receiptId', on: 'cascade' }],
  outboundOrders: [
    { table: 'orderItems', field: 'orderId', on: 'cascade' }, { table: 'packLists', field: 'orderId', on: 'cascade' },
    { table: 'shipments', field: 'orderId', on: 'cascade' }, { table: 'returnOrders', field: 'orderId', on: 'setnull' },
  ],
  orderItems: [{ table: 'pickTasks', field: 'orderItemId', on: 'cascade' }],
};

type AnyRow = Record<string, unknown> & { id: Id };
const rowsOf = (db: Db, t: TableName) => db[t] as unknown as AnyRow[];

/**
 * Deletes a row the way PostgreSQL would: cascades follow, SET NULL is applied, and a RESTRICT reference that remains
 * aborts the whole delete with the API's 409 "still referenced" answer.
 */
export function deleteRow(db: Db, table: TableName, id: Id): void {
  const doomed = new Map<TableName, Set<Id>>([[table, new Set([id])]]);
  const queue: [TableName, Id][] = [[table, id]];
  while (queue.length) {
    const [t, rid] = queue.shift()!;
    for (const ref of REFS[t] ?? []) {
      if (ref.on !== 'cascade') continue;
      for (const child of rowsOf(db, ref.table)) {
        if (child[ref.field] !== rid) continue;
        const set = doomed.get(ref.table) ?? new Set<Id>();
        if (!set.has(child.id)) { set.add(child.id); doomed.set(ref.table, set); queue.push([ref.table, child.id]); }
      }
    }
  }
  const isDoomed = (t: TableName, rid: Id) => doomed.get(t)?.has(rid) ?? false;
  for (const [t, ids] of doomed)
    for (const rid of ids)
      for (const ref of REFS[t] ?? [])
        if (ref.on === 'restrict' && rowsOf(db, ref.table).some((r) => r[ref.field] === rid && !isDoomed(ref.table, r.id)))
          throw new ConflictError(REFERENCED_MESSAGE);

  for (const [t, ids] of doomed)
    for (const rid of ids)
      for (const ref of REFS[t] ?? [])
        if (ref.on === 'setnull')
          for (const r of rowsOf(db, ref.table)) if (r[ref.field] === rid && !isDoomed(ref.table, r.id)) r[ref.field] = null;
  for (const [t, ids] of doomed) {
    const rows = rowsOf(db, t);
    for (let i = rows.length - 1; i >= 0; i--) if (ids.has(rows[i].id)) rows.splice(i, 1);
  }
}

/** A foreign key that points nowhere is a database error (409) in the API. */
export function ensureRef(rows: { id: Id }[], id: Id | null | undefined): void {
  if (id && !rows.some((r) => r.id === id)) throw new ConflictError(REFERENCED_MESSAGE);
}

/** Unique index violation (409). */
export function ensureUnique<T extends { id: Id }>(rows: T[], pick: (r: T) => string | null | undefined, value: string, selfId?: Id): void {
  if (rows.some((r) => r.id !== selfId && pick(r) === value)) throw new ConflictError(REFERENCED_MESSAGE);
}
