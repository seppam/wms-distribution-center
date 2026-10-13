import matrixFile from '../../../contracts/permission-matrix.json';
import { byId, type Ctx } from './db';
import { ForbiddenError, HttpError, NotFoundError, UnauthorizedError, ValidationError } from './errors';
import type { Result, Route, RouteCtx } from './route';
import { buildSeed, seedRevision, SEED } from './seed';
import { authRoutes, parseToken } from './services/auth';
import { inboundRoutes } from './services/inbound';
import { inventoryRoutes } from './services/inventory';
import { masterDataRoutes } from './services/masterdata';
import { outboundRoutes } from './services/outbound';
import { reportRoutes } from './services/reports';
import { returnRoutes } from './services/returns';
import { transferRoutes } from './services/transfers';
import {
  browserStore, isQuotaError, loadEnvelope, memoryStore, MIGRATIONS, SCHEMA_VERSION, serialize, STORAGE_KEY_PREFIX,
  type KeyValueStore, type Migration, type StorageStatus,
} from './storage';
import type { ApiRequest, ApiResponse, Db, PermissionMatrix, SeedFile } from './types';
import { dropNulls, newId } from './util';

export const PERMISSION_MATRIX = matrixFile as unknown as PermissionMatrix;

export const ALL_ROUTES: Route[] = [
  ...authRoutes, ...masterDataRoutes, ...inboundRoutes, ...outboundRoutes, ...inventoryRoutes, ...transferRoutes, ...returnRoutes, ...reportRoutes,
];

export interface EngineOptions {
  /** Where the data is saved. Defaults to localStorage, or memory when the browser does not allow it. */
  store?: KeyValueStore | null;
  now?: () => Date;
  newId?: () => string;
  seed?: SeedFile;
  matrix?: PermissionMatrix;
  schemaVersion?: number;
  migrations?: Record<number, Migration>;
}

interface CompiledRoute { route: Route; regex: RegExp; names: string[]; policy: string; key: string; literals: number }

const cap = (s: string) => s.charAt(0).toUpperCase() + s.slice(1);

/**
 * The in-browser replacement of the WMS API. It answers the same requests as the ASP.NET backend (same routes, same JSON,
 * same status codes and messages), enforces the shared permission matrix, and keeps the whole database in localStorage.
 * It never touches the network.
 */
export class Engine {
  private readonly routes: CompiledRoute[];
  private readonly store: KeyValueStore;
  private readonly kind: 'localStorage' | 'memory';
  private readonly now: () => Date;
  private readonly genId: () => string;
  private readonly seed: SeedFile;
  private readonly matrix: PermissionMatrix;
  private readonly schemaVersion: number;
  private readonly migrations: Record<number, Migration>;
  private readonly seedRev: string;
  private readonly key: string;
  private state: Db;
  private lastRaw: string | null = null;
  private listeners = new Set<() => void>();
  private resetRequested = false;
  private status: StorageStatus;

  constructor(options: EngineOptions = {}) {
    this.now = options.now ?? (() => new Date());
    this.genId = options.newId ?? newId;
    this.seed = options.seed ?? SEED;
    this.matrix = options.matrix ?? PERMISSION_MATRIX;
    this.schemaVersion = options.schemaVersion ?? SCHEMA_VERSION;
    this.migrations = options.migrations ?? MIGRATIONS;
    this.seedRev = seedRevision(this.seed);
    this.key = STORAGE_KEY_PREFIX + this.schemaVersion;

    const provided = options.store === undefined ? browserStore() : options.store;
    this.store = provided ?? memoryStore();
    this.kind = provided ? 'localStorage' : 'memory';
    this.status = { kind: this.kind, problem: provided ? null : 'unavailable', notice: null, bytes: 0 };

    this.routes = ALL_ROUTES.map((r) => this.compile(r)).sort((a, b) => b.literals - a.literals);

    const loaded = loadEnvelope(this.store, this.schemaVersion, this.migrations, this.seedRev);
    this.state = loaded.envelope ? loaded.envelope.db : buildSeed(this.now(), this.seed);
    this.status.notice = loaded.notice;
    if (loaded.envelope && loaded.raw !== null) {
      this.lastRaw = loaded.raw;
      this.status.bytes = loaded.raw?.length ?? 0;
    } else {
      this.persist();
    }
  }

  private compile(route: Route): CompiledRoute {
    const key = `${route.method} ${route.pattern}`;
    const policy = this.matrix.endpoints[key];
    if (!policy) throw new Error(`Route "${key}" is missing from the permission matrix (contracts/permission-matrix.json).`);
    const names: string[] = [];
    const regex = new RegExp('^' + route.pattern.replace(/\{(\w+)\}/g, (_m, n: string) => { names.push(n); return '([^/]+)'; }) + '$');
    return { route, regex, names, policy, key, literals: route.pattern.split('/').filter((s) => !s.startsWith('{')).length };
  }

  // ------------------------------------------------------------------ public API

  getStatus(): StorageStatus { return { ...this.status }; }

  subscribe(listener: () => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  /** Restores the shipped dataset (all data, including users and custom roles). Sessions stay valid because the account ids are stable. */
  resetDataset(): void {
    this.state = buildSeed(this.now(), this.seed);
    this.status.notice = null;
    this.persist();
  }

  /** Read-only copy of the current database (tests and the About dialog). */
  snapshot(): Db { return JSON.parse(JSON.stringify(this.state)) as Db; }

  async request(req: ApiRequest): Promise<ApiResponse> {
    try {
      const result = this.handle(req);
      return { status: result.status, data: result.data === undefined ? undefined : dropNulls(result.data) };
    } catch (err) {
      return this.errorResponse(err);
    }
  }

  // ------------------------------------------------------------------ internals

  private handle(req: ApiRequest): Result {
    this.syncFromStorage();
    const [rawPath, rawQuery = ''] = req.path.split('?');
    const path = rawPath.replace(/^\/+/, '').replace(/\/+$/, '').toLowerCase();
    const normalized = path.startsWith('api/') || path === 'api' ? path : `api/${path}`;
    const method = req.method.toUpperCase();

    const query: Record<string, unknown> = { ...Object.fromEntries(new URLSearchParams(rawQuery)) };
    for (const [k, v] of Object.entries(req.query ?? {})) if (v !== undefined && v !== null) query[k] = v;

    let matched: CompiledRoute | undefined;
    let params: Record<string, string> = {};
    // The original-case path supplies the ids (they are case-sensitive here, unlike the route words).
    const original = rawPath.replace(/^\/+/, '').replace(/\/+$/, '');
    const originalNormalized = original.toLowerCase().startsWith('api/') ? original : `api/${original}`;
    const idSegments = originalNormalized.split('/');
    for (const r of this.routes) {
      if (r.route.method !== method) continue;
      const m = r.regex.exec(normalized);
      if (!m) continue;
      matched = r;
      const segs = r.route.pattern.split('/');
      params = {};
      segs.forEach((s, i) => { const n = /^\{(\w+)\}$/.exec(s); if (n) params[n[1]] = decodeURIComponent(idSegments[i] ?? ''); });
      break;
    }
    if (!matched) throw new NotFoundError();

    // Authentication, then authorization, then (inside the handler) validation: the same order as the ASP.NET pipeline.
    let userId: string | null = null;
    let roleName: string | null = null;
    if (matched.policy !== 'anon') {
      userId = parseToken(req.token ?? null, this.now());
      const user = userId ? byId(this.state.users, userId) : undefined;
      if (!user || !user.isActive) throw new UnauthorizedError();
      roleName = byId(this.state.roles, user.roleId)?.name ?? null;
      if (matched.policy !== 'read') {
        const allowed = (this.matrix.policies[matched.policy] ?? []).map(cap);
        if (!roleName || !allowed.includes(roleName)) throw new ForbiddenError();
      }
    }

    const mutating = method !== 'GET';
    const draft: Db = mutating ? (JSON.parse(JSON.stringify(this.state)) as Db) : this.state;
    const ctx: RouteCtx = {
      db: draft, now: this.now(), newId: this.genId, userId, params, query,
      body: (req.body && typeof req.body === 'object' ? (req.body as Record<string, unknown>) : {}),
      user: userId ? (byId(draft.users, userId) ?? null) : null, roleName,
      resetDataset: () => { this.resetRequested = true; },
    };
    this.resetRequested = false;
    const out = matched.route.handler(ctx) as Result | undefined;
    const result: Result = out && typeof out === 'object' && 'status' in out && typeof (out as Result).status === 'number' ? (out as Result) : { status: 200, data: out };

    if (mutating) {
      if (this.resetRequested) { this.resetRequested = false; this.resetDataset(); }
      else { this.state = draft; this.persist(); }
    }
    return result;
  }

  private errorResponse(err: unknown): ApiResponse {
    if (err instanceof ValidationError)
      return { status: 400, data: { type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1', title: err.message, status: 400, errors: err.errors } };
    if (err instanceof HttpError) {
      if (!err.message) return { status: err.status };
      return { status: err.status, data: { title: err.message, status: err.status, detail: err.message, message: err.message } };
    }
    console.error('Browser demo: unexpected error', err);
    return { status: 500, data: { title: 'An unexpected error occurred.', status: 500, detail: 'An unexpected error occurred.', message: 'An unexpected error occurred.' } };
  }

  /** Another tab may have saved or reset the data: adopt what is stored before answering. */
  private syncFromStorage(): void {
    if (this.kind !== 'localStorage') return;
    let raw: string | null;
    try { raw = this.store.getItem(this.key); } catch { return; }
    if (raw === this.lastRaw) return;
    if (raw === null) { this.state = buildSeed(this.now(), this.seed); this.persist(); return; }
    try {
      const env = JSON.parse(raw) as { db?: Db };
      if (env.db) { this.state = env.db; this.lastRaw = raw; this.status.bytes = raw.length; this.notify(); }
    } catch { /* keep the in-memory data */ }
  }

  private persist(): void {
    const json = serialize(this.state, this.schemaVersion, this.seedRev, this.now());
    this.status.bytes = json.length;
    const before = `${this.status.problem}|${this.status.notice}`;
    try {
      this.store.setItem(this.key, json);
      this.lastRaw = json;
      if (this.kind === 'localStorage') this.status.problem = null;
    } catch (e) {
      // Storage is full or blocked: keep working from memory and tell the user instead of failing the action.
      this.status.problem = isQuotaError(e) ? 'quota' : 'unavailable';
    }
    if (before !== `${this.status.problem}|${this.status.notice}`) this.notify();
  }

  private notify(): void { for (const l of this.listeners) l(); }
}

export type { Ctx };
