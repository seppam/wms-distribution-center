import { deleteRow, ensureRef, ensureUnique, must, byId } from '../db';
import * as dto from '../dto';
import { LocationType, ZoneType, LocationStatus } from '../enums';
import { BusinessRuleError, ConflictError, NotFoundError, HttpError } from '../errors';
import { created, noContent, ok, route, type Route, type RouteCtx } from '../route';
import type { Category, Role, TableName } from '../types';
import { asBody, hashPassword, num, optId, str, Validator, cmp } from '../util';

const has = (b: Record<string, unknown>, k: string) => b[k] !== undefined && b[k] !== null;
const notFound = () => new NotFoundError();

/** Registers GET list, GET by id, POST, PUT and DELETE for a table, as the controllers do (DELETE is Admin-only in the matrix). */
function crud<R extends { id: string }>(opts: {
  path: string; table: TableName; rows: (c: RouteCtx) => R[]; map: (c: RouteCtx, r: R) => unknown;
  list?: (c: RouteCtx, rows: R[]) => R[];
  create: (c: RouteCtx, body: Record<string, unknown>) => R;
  update: (c: RouteCtx, row: R, body: Record<string, unknown>) => void;
}): Route[] {
  const p = `api/${opts.path}`;
  return [
    route('GET', p, (c) => ok((opts.list ? opts.list(c, opts.rows(c)) : opts.rows(c)).map((r) => opts.map(c, r)))),
    route('GET', `${p}/{id}`, (c) => {
      const row = byId(opts.rows(c), c.params.id);
      if (!row) throw notFound();
      return ok(opts.map(c, row));
    }),
    route('POST', p, (c) => {
      const row = opts.create(c, asBody(c.body));
      opts.rows(c).push(row);
      return created(opts.map(c, row));
    }),
    route('PUT', `${p}/{id}`, (c) => {
      const row = byId(opts.rows(c), c.params.id);
      if (!row) throw notFound();
      opts.update(c, row, asBody(c.body));
      return ok(opts.map(c, row));
    }),
    route('DELETE', `${p}/{id}`, (c) => {
      if (!byId(opts.rows(c), c.params.id)) throw notFound();
      deleteRow(c.db, opts.table, c.params.id);
      return noContent();
    }),
  ];
}

// ----------------------------------------------------------------------------- carriers, suppliers, warehouses

const carriers = crud({
  path: 'carriers', table: 'carriers', rows: (c) => c.db.carriers, map: (_c, r) => dto.carrierDto(r),
  create: (c, b) => {
    new Validator(b).required('name', 'Name').email('email', 'Email').done();
    return { id: c.newId(), name: String(b.name), contactPerson: str(b.contactPerson), phone: str(b.phone), email: str(b.email), vehicleType: str(b.vehicleType), isActive: true };
  },
  update: (_c, r, b) => {
    new Validator(b).email('email', 'Email').done();
    if (has(b, 'name')) r.name = String(b.name);
    if (has(b, 'contactPerson')) r.contactPerson = String(b.contactPerson);
    if (has(b, 'phone')) r.phone = String(b.phone);
    if (has(b, 'email')) r.email = String(b.email);
    if (has(b, 'vehicleType')) r.vehicleType = String(b.vehicleType);
    if (has(b, 'isActive')) r.isActive = Boolean(b.isActive);
  },
});

const suppliers = crud({
  path: 'suppliers', table: 'suppliers', rows: (c) => c.db.suppliers, map: (_c, r) => dto.supplierDto(r),
  create: (c, b) => {
    new Validator(b).required('code', 'Code').required('name', 'Name').email('email', 'Email').done();
    ensureUnique(c.db.suppliers, (s) => s.code, String(b.code));
    return { id: c.newId(), code: String(b.code), name: String(b.name), contactPerson: str(b.contactPerson), email: str(b.email), phone: str(b.phone), address: str(b.address), isActive: true };
  },
  update: (_c, r, b) => {
    new Validator(b).email('email', 'Email').done();
    if (has(b, 'name')) r.name = String(b.name);
    if (has(b, 'contactPerson')) r.contactPerson = String(b.contactPerson);
    if (has(b, 'email')) r.email = String(b.email);
    if (has(b, 'phone')) r.phone = String(b.phone);
    if (has(b, 'address')) r.address = String(b.address);
    if (has(b, 'isActive')) r.isActive = Boolean(b.isActive);
  },
});

const warehouses = crud({
  path: 'warehouses', table: 'warehouses', rows: (c) => c.db.warehouses, map: (_c, r) => dto.warehouseDto(r),
  create: (c, b) => {
    new Validator(b).required('code', 'Code').required('name', 'Name').done();
    ensureUnique(c.db.warehouses, (w) => w.code, String(b.code));
    return { id: c.newId(), code: String(b.code), name: String(b.name), address: str(b.address), city: str(b.city), capacity: num(b.capacity), isActive: true, createdAt: c.now.toISOString() };
  },
  update: (_c, r, b) => {
    if (has(b, 'name')) r.name = String(b.name);
    if (has(b, 'address')) r.address = String(b.address);
    if (has(b, 'city')) r.city = String(b.city);
    if (has(b, 'capacity')) r.capacity = num(b.capacity);
    if (has(b, 'isActive')) r.isActive = Boolean(b.isActive);
  },
});

// ----------------------------------------------------------------------------- zones, locations

const zones = crud({
  path: 'zones', table: 'zones', rows: (c) => c.db.zones, map: (c, r) => dto.zoneDto(c.db, r),
  list: (c, rows) => (has(c.query, 'warehouseId') ? rows.filter((z) => z.warehouseId === c.query.warehouseId) : rows),
  create: (c, b) => {
    new Validator(b).id('warehouseId', 'WarehouseId').required('name', 'Name').oneOf('zoneType', 'ZoneType', ZoneType).done();
    ensureRef(c.db.warehouses, String(b.warehouseId));
    return { id: c.newId(), warehouseId: String(b.warehouseId), name: String(b.name), zoneType: String(b.zoneType ?? ZoneType[0]), description: str(b.description), isActive: true };
  },
  update: (_c, r, b) => {
    new Validator(b).oneOf('zoneType', 'ZoneType', ZoneType).done();
    if (has(b, 'name')) r.name = String(b.name);
    if (has(b, 'zoneType')) r.zoneType = String(b.zoneType);
    if (has(b, 'description')) r.description = String(b.description);
    if (has(b, 'isActive')) r.isActive = Boolean(b.isActive);
  },
});

const locations = crud({
  path: 'locations', table: 'locations', rows: (c) => c.db.locations, map: (c, r) => dto.locationDto(c.db, r),
  list: (c, rows) => rows
    .filter((l) => !has(c.query, 'zoneId') || l.zoneId === c.query.zoneId)
    .filter((l) => !String(c.query.code ?? '').trim() || l.locationCode.includes(String(c.query.code))),
  create: (c, b) => {
    new Validator(b).id('zoneId', 'ZoneId').required('locationCode', 'LocationCode').oneOf('locationType', 'LocationType', LocationType).done();
    ensureRef(c.db.zones, String(b.zoneId));
    ensureUnique(c.db.locations, (l) => l.locationCode, String(b.locationCode));
    return {
      id: c.newId(), zoneId: String(b.zoneId), locationCode: String(b.locationCode), aisle: str(b.aisle), rack: str(b.rack), level: str(b.level), bin: str(b.bin),
      locationType: String(b.locationType ?? LocationType[0]), maxCapacity: num(b.maxCapacity), status: 'Empty',
    };
  },
  update: (c, r, b) => {
    new Validator(b).oneOf('locationType', 'LocationType', LocationType).oneOf('status', 'Status', LocationStatus).done();
    if (has(b, 'locationCode')) { ensureUnique(c.db.locations, (l) => l.locationCode, String(b.locationCode), r.id); r.locationCode = String(b.locationCode); }
    if (has(b, 'aisle')) r.aisle = String(b.aisle);
    if (has(b, 'rack')) r.rack = String(b.rack);
    if (has(b, 'level')) r.level = String(b.level);
    if (has(b, 'bin')) r.bin = String(b.bin);
    if (has(b, 'locationType')) r.locationType = String(b.locationType);
    if (has(b, 'maxCapacity')) r.maxCapacity = num(b.maxCapacity);
    if (has(b, 'status')) r.status = String(b.status);
  },
});

// ----------------------------------------------------------------------------- categories

function categoryTree(all: Category[], parent: Category): unknown {
  return {
    id: parent.id, name: parent.name, level: parent.level, description: parent.description,
    children: all.filter((c) => c.parentId === parent.id).map((c) => categoryTree(all, c)),
  };
}

const categories = [
  route('GET', 'api/categories/tree', (c) => ok(c.db.categories.filter((x) => x.parentId === null).map((x) => categoryTree(c.db.categories, x)))),
  ...crud({
    path: 'categories', table: 'categories', rows: (c) => c.db.categories, map: (_c, r) => dto.categoryDto(r),
    create: (c, b) => {
      new Validator(b).required('name', 'Name').done();
      const parentId = optId(b.parentId);
      ensureRef(c.db.categories, parentId);
      const parent = byId(c.db.categories, parentId);
      return { id: c.newId(), name: String(b.name), parentId, level: parentId === null ? 1 : (parent?.level ?? 1) + 1, description: str(b.description) };
    },
    update: (c, r, b) => {
      if (has(b, 'name')) r.name = String(b.name);
      if (has(b, 'parentId')) { ensureRef(c.db.categories, String(b.parentId)); r.parentId = String(b.parentId); }
      if (has(b, 'description')) r.description = String(b.description);
    },
  }),
];

// ----------------------------------------------------------------------------- units of measure

const uoms = crud({
  path: 'uoms', table: 'uoms', rows: (c) => c.db.uoms, map: (c, r) => dto.uomDto(c.db, r),
  list: (_c, rows) => [...rows].sort((a, b) => cmp(a.code, b.code)),
  create: (c, b) => {
    new Validator(b).required('code', 'Code').required('name', 'Name').done();
    const factor = num(b.conversionFactor);
    if (factor <= 0) throw new BusinessRuleError('The conversion factor must be greater than zero.');
    if (c.db.uoms.some((u) => u.code === b.code)) throw new ConflictError('A unit with this code already exists.');
    const baseUomId = optId(b.baseUomId);
    ensureRef(c.db.uoms, baseUomId);
    const code = String(b.code).trim().toUpperCase();
    ensureUnique(c.db.uoms, (u) => u.code, code);
    return { id: c.newId(), code, name: String(b.name), baseUomId, conversionFactor: factor, isBase: Boolean(b.isBase) };
  },
  update: (c, r, b) => {
    if (has(b, 'conversionFactor') && num(b.conversionFactor) <= 0) throw new BusinessRuleError('The conversion factor must be greater than zero.');
    if (b.baseUomId === r.id) throw new BusinessRuleError('A unit cannot be its own base unit.');
    if (has(b, 'name')) r.name = String(b.name);
    if (has(b, 'baseUomId')) { ensureRef(c.db.uoms, String(b.baseUomId)); r.baseUomId = String(b.baseUomId); }
    if (has(b, 'conversionFactor')) r.conversionFactor = num(b.conversionFactor);
    if (has(b, 'isBase')) r.isBase = Boolean(b.isBase);
  },
});

// ----------------------------------------------------------------------------- products

const products = [
  route('GET', 'api/products/search', (c) => {
    const q = String(c.query.q ?? '');
    return ok(c.db.products
      .filter((p) => !q.trim() || p.name.includes(q) || p.sku.includes(q) || (p.barcode !== null && p.barcode.includes(q)))
      .map((p) => dto.productDto(c.db, p)));
  }),
  ...crud({
    path: 'products', table: 'products', rows: (c) => c.db.products, map: (c, r) => dto.productDto(c.db, r),
    create: (c, b) => {
      new Validator(b).required('sku', 'Sku').required('name', 'Name').id('uomId', 'UomId').done();
      ensureRef(c.db.uoms, String(b.uomId));
      ensureRef(c.db.categories, optId(b.categoryId));
      ensureUnique(c.db.products, (p) => p.sku, String(b.sku));
      return {
        id: c.newId(), sku: String(b.sku), name: String(b.name), categoryId: optId(b.categoryId), uomId: String(b.uomId), weightKg: num(b.weightKg), volumeM3: num(b.volumeM3),
        barcode: str(b.barcode), minStock: num(b.minStock), maxStock: has(b, 'maxStock') ? num(b.maxStock) : null, isActive: true,
      };
    },
    update: (c, r, b) => {
      if (has(b, 'name')) r.name = String(b.name);
      if (has(b, 'categoryId')) { ensureRef(c.db.categories, String(b.categoryId)); r.categoryId = String(b.categoryId); }
      if (has(b, 'uomId')) { ensureRef(c.db.uoms, String(b.uomId)); r.uomId = String(b.uomId); }
      if (has(b, 'weightKg')) r.weightKg = num(b.weightKg);
      if (has(b, 'volumeM3')) r.volumeM3 = num(b.volumeM3);
      if (has(b, 'barcode')) r.barcode = String(b.barcode);
      if (has(b, 'minStock')) r.minStock = num(b.minStock);
      if (has(b, 'maxStock')) r.maxStock = num(b.maxStock);
      if (has(b, 'isActive')) r.isActive = Boolean(b.isActive);
    },
  }),
];

// ----------------------------------------------------------------------------- users and roles

const users = [
  route('GET', 'api/users', (c) => ok(c.db.users.map((u) => dto.userDto(c.db, u)))),
  route('GET', 'api/users/{id}', (c) => {
    const u = byId(c.db.users, c.params.id);
    if (!u) throw notFound();
    return ok(dto.userDto(c.db, u));
  }),
  route('POST', 'api/users', (c) => {
    const b = asBody(c.body);
    new Validator(b).required('username', 'Username').required('email', 'Email').email('email', 'Email').required('password', 'Password').minLength('password', 'Password', 10)
      .required('fullName', 'FullName').id('roleId', 'RoleId').id('warehouseId', 'WarehouseId', false).done();
    ensureRef(c.db.roles, String(b.roleId));
    ensureRef(c.db.warehouses, optId(b.warehouseId));
    const user = {
      id: c.newId(), username: String(b.username), email: String(b.email), passwordHash: hashPassword(String(b.password)), roleId: String(b.roleId),
      warehouseId: optId(b.warehouseId), fullName: String(b.fullName), phone: str(b.phone), isActive: true, lastLogin: null, createdAt: c.now.toISOString(), updatedAt: c.now.toISOString(),
    };
    c.db.users.push(user);
    return created(dto.userDto(c.db, user));
  }),
  route('PUT', 'api/users/{id}', (c) => {
    const b = asBody(c.body);
    new Validator(b).email('email', 'Email').minLength('password', 'Password', 10).done();
    const u = byId(c.db.users, c.params.id);
    if (!u) throw notFound();
    if (has(b, 'email')) u.email = String(b.email);
    if (has(b, 'fullName')) u.fullName = String(b.fullName);
    if (has(b, 'password')) u.passwordHash = hashPassword(String(b.password));
    if (has(b, 'roleId')) { ensureRef(c.db.roles, String(b.roleId)); u.roleId = String(b.roleId); }
    if (has(b, 'warehouseId')) { ensureRef(c.db.warehouses, String(b.warehouseId)); u.warehouseId = String(b.warehouseId); }
    if (has(b, 'phone')) u.phone = String(b.phone);
    if (has(b, 'isActive')) u.isActive = Boolean(b.isActive);
    u.updatedAt = c.now.toISOString();
    return ok(dto.userDto(c.db, u));
  }),
  route('DELETE', 'api/users/{id}', (c) => {
    if (c.user?.id === c.params.id) throw new HttpError(400, 'You cannot delete your own account.');
    if (!byId(c.db.users, c.params.id)) throw notFound();
    deleteRow(c.db, 'users', c.params.id);
    return noContent();
  }),
];

function parsePermissions(raw: unknown): string | null {
  if (raw === undefined || raw === null || raw === '') return null;
  try { JSON.parse(String(raw)); } catch { throw new HttpError(400, 'The permissions value is not valid JSON.'); }
  return String(raw);
}

const roles = [
  route('GET', 'api/roles', (c) => ok(c.db.roles.map(dto.roleDto))),
  route('GET', 'api/roles/{id}', (c) => ok(dto.roleDto(must(c.db.roles, c.params.id, ''))))
  ,
  route('POST', 'api/roles', (c) => {
    const b = asBody(c.body);
    new Validator(b).required('name', 'Name').done();
    ensureUnique(c.db.roles, (r) => r.name, String(b.name));
    const role: Role = { id: c.newId(), name: String(b.name), description: str(b.description), permissions: parsePermissions(b.permissions), isSystem: false, createdAt: c.now.toISOString() };
    c.db.roles.push(role);
    return created(dto.roleDto(role));
  }),
  route('PUT', 'api/roles/{id}', (c) => {
    const b = asBody(c.body);
    const role = must(c.db.roles, c.params.id, '');
    // Authorization policies reference the built-in role names, so those can never be renamed or demoted.
    if (role.isSystem && ((has(b, 'name') && b.name !== role.name) || b.isSystem === false))
      throw new BusinessRuleError('Built-in roles cannot be renamed or turned into custom roles.');
    if (has(b, 'name')) { ensureUnique(c.db.roles, (r) => r.name, String(b.name), role.id); role.name = String(b.name); }
    if (has(b, 'description')) role.description = String(b.description);
    if (has(b, 'permissions')) role.permissions = parsePermissions(b.permissions);
    if (b.isSystem === true && !role.isSystem) throw new BusinessRuleError('Custom roles cannot be promoted to built-in roles.');
    return ok(dto.roleDto(role));
  }),
  route('DELETE', 'api/roles/{id}', (c) => {
    const role = must(c.db.roles, c.params.id, '');
    if (role.isSystem) throw new BusinessRuleError('Built-in roles cannot be deleted.');
    deleteRow(c.db, 'roles', role.id);
    return noContent();
  }),
];

export const masterDataRoutes: Route[] = [...carriers, ...suppliers, ...warehouses, ...zones, ...locations, ...categories, ...uoms, ...products, ...users, ...roles];
