import type { Ctx } from './db';
import type { User } from './types';

export interface RouteCtx extends Ctx {
  params: Record<string, string>;
  query: Record<string, unknown>;
  body: Record<string, unknown>;
  /** The authenticated user (null on anonymous routes). */
  user: User | null;
  roleName: string | null;
  /** Restores the seeded dataset (only used by POST api/demo/reset). */
  resetDataset: () => void;
}

export interface Result { status: number; data?: unknown }
export const ok = (data?: unknown): Result => ({ status: 200, data });
export const created = (data: unknown): Result => ({ status: 201, data });
export const noContent = (): Result => ({ status: 204 });

export interface Route {
  method: 'GET' | 'POST' | 'PUT' | 'DELETE';
  /** Lower-case route with {param} placeholders, e.g. api/inboundreceipts/{id}/receive. Must appear in contracts/permission-matrix.json. */
  pattern: string;
  handler: (c: RouteCtx) => Result | unknown;
}
export const route = (method: Route['method'], pattern: string, handler: Route['handler']): Route => ({ method, pattern, handler });
