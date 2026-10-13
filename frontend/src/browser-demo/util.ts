import { ValidationError } from './errors';

/** Decimal(18,4) like the database: avoids 0.1 + 0.2 style artefacts in quantities and money. */
export const round = (n: number): number => Math.round((n + Number.EPSILON) * 10000) / 10000;

/** {0:0.##} of the backend messages: at most two decimals, no trailing zeros. */
export const fmt = (n: number): string => String(Math.round((n + Number.EPSILON) * 100) / 100);

export const dayString = (d: Date): string => d.toISOString().slice(0, 10);
export const compactDay = (d: Date): string => dayString(d).replaceAll('-', '');
export const addDays = (d: Date, days: number): Date => new Date(d.getTime() + days * 86_400_000);
export const startOfDay = (d: Date): number => Date.parse(`${dayString(d)}T00:00:00.000Z`);
/** True when the ISO timestamp falls on the same UTC day as `now`. */
export const isSameDay = (iso: string | null, now: Date): boolean => {
  if (!iso) return false;
  const t = Date.parse(iso);
  const s = startOfDay(now);
  return t >= s && t < s + 86_400_000;
};

export const sum = (xs: number[]): number => round(xs.reduce((a, b) => a + b, 0));
export const cmp = (a: string, b: string): number => (a < b ? -1 : a > b ? 1 : 0);

export function newId(): string {
  const c = globalThis.crypto;
  if (c?.randomUUID) return c.randomUUID();
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (ch) => {
    const r = (Math.random() * 16) | 0;
    return (ch === 'x' ? r : (r & 0x3) | 0x8).toString(16);
  });
}

/** Removes null/undefined members like the API's WhenWritingNull serialisation. */
export function dropNulls<T>(value: T): T {
  if (Array.isArray(value)) return value.map(dropNulls) as unknown as T;
  if (value && typeof value === 'object') {
    const out: Record<string, unknown> = {};
    for (const [k, v] of Object.entries(value as Record<string, unknown>)) if (v !== null && v !== undefined) out[k] = dropNulls(v);
    return out as T;
  }
  return value;
}

const K = new Uint32Array([
  0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5, 0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3,
  0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174, 0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
  0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967, 0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13,
  0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85, 0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
  0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3, 0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208,
  0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2,
]);

/** Small synchronous SHA-256 (the demo stores a digest, never a clear-text password; this is not a security feature). */
export function sha256Hex(text: string): string {
  const bytes = new TextEncoder().encode(text);
  const l = bytes.length;
  const padded = new Uint8Array((((l + 9 + 63) >> 6) << 6));
  padded.set(bytes);
  padded[l] = 0x80;
  const view = new DataView(padded.buffer);
  view.setUint32(padded.length - 4, (l * 8) >>> 0);
  view.setUint32(padded.length - 8, Math.floor((l * 8) / 4294967296));
  const h = new Uint32Array([0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a, 0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19]);
  const w = new Uint32Array(64);
  const rotr = (x: number, n: number) => (x >>> n) | (x << (32 - n));
  for (let i = 0; i < padded.length; i += 64) {
    for (let t = 0; t < 16; t++) w[t] = view.getUint32(i + t * 4);
    for (let t = 16; t < 64; t++) {
      const s0 = rotr(w[t - 15], 7) ^ rotr(w[t - 15], 18) ^ (w[t - 15] >>> 3);
      const s1 = rotr(w[t - 2], 17) ^ rotr(w[t - 2], 19) ^ (w[t - 2] >>> 10);
      w[t] = (w[t - 16] + s0 + w[t - 7] + s1) >>> 0;
    }
    let [a, b, c, d, e, f, g, hh] = h;
    for (let t = 0; t < 64; t++) {
      const S1 = rotr(e, 6) ^ rotr(e, 11) ^ rotr(e, 25);
      const ch = (e & f) ^ (~e & g);
      const t1 = (hh + S1 + ch + K[t] + w[t]) >>> 0;
      const S0 = rotr(a, 2) ^ rotr(a, 13) ^ rotr(a, 22);
      const maj = (a & b) ^ (a & c) ^ (b & c);
      const t2 = (S0 + maj) >>> 0;
      hh = g; g = f; f = e; e = (d + t1) >>> 0; d = c; c = b; b = a; a = (t1 + t2) >>> 0;
    }
    h[0] += a; h[1] += b; h[2] += c; h[3] += d; h[4] += e; h[5] += f; h[6] += g; h[7] += hh;
  }
  return [...h].map((x) => x.toString(16).padStart(8, '0')).join('');
}

export const hashPassword = (password: string): string => sha256Hex(`wms-browser-demo:${password}`);

// ----------------------------------------------------------------------------- request validation (ASP.NET data annotations)

const GUID_LIKE = /^[0-9a-zA-Z-]{1,64}$/;

/** Collects data-annotation style errors for a request body and throws one ValidationError (HTTP 400) at the end. */
export class Validator {
  private errors: Record<string, string[]> = {};
  constructor(private body: Record<string, unknown>, private prefix = '') {}

  private add(field: string, message: string) { (this.errors[this.prefix + field] ??= []).push(message); }

  get(key: string): unknown { return this.body[key]; }

  required(key: string, field: string): this {
    const v = this.body[key];
    if (v === undefined || v === null || (typeof v === 'string' && v.trim() === '')) this.add(field, `The ${field} field is required.`);
    return this;
  }
  id(key: string, field: string, required = true): this {
    const v = this.body[key];
    if (v === undefined || v === null || v === '') { if (required) this.add(field, `The ${field} field is required.`); }
    else if (typeof v !== 'string' || !GUID_LIKE.test(v)) this.add(field, `The value '${String(v)}' is not valid for ${field}.`);
    return this;
  }
  range(key: string, field: string, min: number, max: number): this {
    const v = this.body[key];
    if (v === undefined || v === null) return this;
    const n = typeof v === 'number' ? v : typeof v === 'string' && v.trim() !== '' ? Number(v) : NaN;
    if (!Number.isFinite(n)) this.add(field, `The JSON value could not be converted to System.Decimal. Path: ${field}.`);
    else if (n < min || n > max) this.add(field, `The field ${field} must be between ${min} and ${max}.`);
    return this;
  }
  email(key: string, field: string): this {
    const v = this.body[key];
    if (typeof v === 'string' && v !== '' && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v)) this.add(field, `The ${field} field is not a valid e-mail address.`);
    return this;
  }
  minLength(key: string, field: string, min: number): this {
    const v = this.body[key];
    if (typeof v === 'string' && v.length < min) this.add(field, `The field ${field} must be a string or array type with a minimum length of '${min}'.`);
    return this;
  }
  maxLength(key: string, field: string, max: number): this {
    const v = this.body[key];
    if (typeof v === 'string' && v.length > max) this.add(field, `The field ${field} must be a string with a maximum length of ${max}.`);
    return this;
  }
  oneOf(key: string, field: string, values: readonly string[]): this {
    const v = this.body[key];
    if (v !== undefined && v !== null && (typeof v !== 'string' || !values.includes(v)))
      this.add(field, `The JSON value could not be converted to the ${field} enumeration. Path: ${field}.`);
    return this;
  }
  nested(key: string, field: string, fn: (v: Validator, index: number) => void): this {
    const list = this.body[key];
    if (Array.isArray(list)) list.forEach((item, i) => {
      const child = new Validator((item ?? {}) as Record<string, unknown>, `${this.prefix}${field}[${i}].`);
      fn(child, i);
      for (const [k, v] of Object.entries(child.errors)) this.errors[k] = v;
    });
    return this;
  }
  done(): void { if (Object.keys(this.errors).length) throw new ValidationError(this.errors); }
}

export const asBody = (b: unknown): Record<string, unknown> => (b && typeof b === 'object' && !Array.isArray(b) ? (b as Record<string, unknown>) : {});
export const num = (v: unknown, fallback = 0): number => (v === undefined || v === null || v === '' ? fallback : Number(v));
export const str = (v: unknown): string | null => (v === undefined || v === null ? null : String(v));
export const optId = (v: unknown): string | null => (v === undefined || v === null || v === '' ? null : String(v));
