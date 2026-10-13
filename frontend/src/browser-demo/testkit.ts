import { Engine, type EngineOptions } from './engine';
import { memoryStore, type KeyValueStore } from './storage';

export const FIXED_NOW = new Date('2026-10-13T08:00:00.000Z');
export const PASSWORD = 'Demo!Pass123';

/** A test client around the engine: signs in once per role and exposes the API as call(role, method, path, body). */
export function makeClient(options: EngineOptions = {}) {
  const store: KeyValueStore = options.store === undefined ? memoryStore() : (options.store as KeyValueStore);
  let ticks = 0;
  const engine = new Engine({ now: () => new Date(FIXED_NOW.getTime() + ticks * 1000), store: options.store === null ? null : store, ...options, ...(options.store === undefined ? { store } : {}) });
  const tokens: Record<string, string> = {};

  async function token(role: string): Promise<string | null> {
    if (role === 'anonymous') return null;
    if (!tokens[role]) {
      const res = await engine.request({ method: 'POST', path: '/api/auth/login', body: { username: role, password: PASSWORD } });
      if (res.status !== 200) throw new Error(`login ${role} -> ${res.status}`);
      tokens[role] = (res.data as { token: string }).token;
    }
    return tokens[role];
  }
  async function call(role: string, method: string, path: string, body?: unknown, query?: Record<string, unknown>) {
    ticks++;
    return engine.request({ method, path, body, query, token: await token(role) });
  }
  /** Calls and expects success; returns the body. */
  async function ok<T = any>(role: string, method: string, path: string, body?: unknown, query?: Record<string, unknown>): Promise<T> {
    const res = await call(role, method, path, body, query);
    if (res.status >= 300) throw new Error(`${method} ${path} -> ${res.status} ${JSON.stringify(res.data)}`);
    return res.data as T;
  }
  const get = <T = any>(path: string, query?: Record<string, unknown>, role = 'admin') => ok<T>(role, 'GET', path, undefined, query);
  return { engine, store, call, ok, get, token };
}
