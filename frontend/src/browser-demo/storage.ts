import type { Db } from './types';

export const STORAGE_KEY_PREFIX = 'wms-browser-demo:v';
/** Bump when the shape of the saved database changes and add a migration below. */
export const SCHEMA_VERSION = 1;

export interface Envelope { schemaVersion: number; seedRev: string; savedAt: string; db: Db }

/** Upgrades the saved database from version N to N + 1. Versions without a migration are discarded (the demo data is reset). */
export type Migration = (old: Envelope) => Envelope;
export const MIGRATIONS: Record<number, Migration> = {
  // 1: (old) => ({ ...old, schemaVersion: 2, db: { ...old.db, newTable: [] } }),
};

/** The few Storage methods the demo needs (so tests can pass a fake, and a blocked localStorage can be replaced by memory). */
export interface KeyValueStore {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
  keys(): string[];
}

export function memoryStore(): KeyValueStore {
  const m = new Map<string, string>();
  return { getItem: (k) => m.get(k) ?? null, setItem: (k, v) => void m.set(k, v), removeItem: (k) => void m.delete(k), keys: () => [...m.keys()] };
}

/** localStorage when the browser allows it (it can throw in private windows or with blocked site data), otherwise null. */
export function browserStore(): KeyValueStore | null {
  try {
    const s = globalThis.localStorage;
    if (!s) return null;
    const probe = `${STORAGE_KEY_PREFIX}probe`;
    s.setItem(probe, '1');
    s.removeItem(probe);
    return {
      getItem: (k) => s.getItem(k),
      setItem: (k, v) => s.setItem(k, v),
      removeItem: (k) => s.removeItem(k),
      keys: () => Array.from({ length: s.length }, (_, i) => s.key(i)).filter((k): k is string => k !== null),
    };
  } catch {
    return null;
  }
}

export type StorageProblem = 'quota' | 'unavailable' | null;

export interface StorageStatus {
  /** Where the data lives: "localStorage", or "memory" when the browser does not allow storage (data is lost on reload). */
  kind: 'localStorage' | 'memory';
  /** null while saving works; "quota" when the browser refused the write (storage full); "unavailable" for any other failure. */
  problem: StorageProblem;
  /** A one-time message, e.g. after saved data was discarded because the demo was updated. */
  notice: string | null;
  /** Size of the saved database in characters (localStorage allows roughly 5 million). */
  bytes: number;
}

export const isQuotaError = (e: unknown): boolean => {
  const err = e as { name?: string; code?: number } | null;
  return !!err && (err.name === 'QuotaExceededError' || err.name === 'NS_ERROR_DOM_QUOTA_REACHED' || err.code === 22 || err.code === 1014);
};

export interface LoadResult { envelope: Envelope | null; notice: string | null; raw: string | null }

function parse(raw: string | null): Envelope | null {
  if (!raw) return null;
  try {
    const e = JSON.parse(raw) as Envelope;
    return e && typeof e === 'object' && typeof e.schemaVersion === 'number' && e.db && typeof e.db === 'object' ? e : null;
  } catch {
    return null;
  }
}

/**
 * Reads the saved database. Saved data of the current version is returned as is; data saved by an older version is migrated
 * step by step when every step exists, otherwise discarded; corrupt data is discarded. Stale keys of other versions are removed.
 */
export function loadEnvelope(store: KeyValueStore, schemaVersion: number, migrations: Record<number, Migration>, seedRev: string): LoadResult {
  const key = STORAGE_KEY_PREFIX + schemaVersion;
  const raw = store.getItem(key);
  let notice: string | null = null;

  const current = parse(raw);
  if (raw && !current) notice = 'The saved demo data was unreadable and has been reset.';
  if (current && current.schemaVersion === schemaVersion) {
    if (current.seedRev === seedRev) return { envelope: current, notice, raw };
    notice = 'The demo dataset was updated, so your earlier changes were cleared.';
  }

  let migrated: Envelope | null = null;
  const stale = store.keys().filter((k) => k.startsWith(STORAGE_KEY_PREFIX) && k !== key);
  const olderVersions = stale
    .map((k) => ({ k, v: Number(k.slice(STORAGE_KEY_PREFIX.length)) }))
    .filter((x) => Number.isInteger(x.v) && x.v < schemaVersion)
    .sort((a, b) => b.v - a.v);
  for (const { k, v } of olderVersions) {
    let env = parse(store.getItem(k));
    if (!env) continue;
    let version = v;
    while (env && version < schemaVersion && migrations[version]) { env = migrations[version](env); version++; }
    if (env && version === schemaVersion && env.seedRev === seedRev) { migrated = { ...env, schemaVersion }; break; }
  }
  if (!migrated && olderVersions.length) notice = 'The demo was updated and saved data from an older version could not be kept. It was reset.';
  for (const k of stale) { try { store.removeItem(k); } catch { /* ignore */ } }
  return { envelope: migrated, notice: migrated ? null : notice, raw: null };
}

export function serialize(db: Db, schemaVersion: number, seedRev: string, now: Date): string {
  const envelope: Envelope = { schemaVersion, seedRev, savedAt: now.toISOString(), db };
  return JSON.stringify(envelope);
}
