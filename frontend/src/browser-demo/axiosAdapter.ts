import { AxiosError, type AxiosResponse, type InternalAxiosRequestConfig } from 'axios';
import { Engine } from './engine';

let engine: Engine | undefined;

/** The single in-browser API for this page (created on first use). */
export function getEngine(): Engine {
  engine ??= new Engine();
  return engine;
}

/** Test hook: forget the shared engine so the next request starts from storage again. */
export function resetEngineForTests(): void { engine = undefined; }

function headerValue(config: InternalAxiosRequestConfig, name: string): string | null {
  const h = config.headers as unknown as { get?: (n: string) => unknown } | Record<string, unknown> | undefined;
  const v = typeof (h as { get?: unknown })?.get === 'function' ? (h as { get: (n: string) => unknown }).get(name) : (h as Record<string, unknown> | undefined)?.[name];
  return typeof v === 'string' ? v : null;
}

/**
 * An axios adapter: instead of sending the request over the network it hands it to the in-browser engine and returns the
 * answer in the shape axios expects (including the rejection for 4xx/5xx), so the UI code is identical in both modes.
 */
export async function browserDemoAdapter(config: InternalAxiosRequestConfig): Promise<AxiosResponse> {
  const auth = headerValue(config, 'Authorization');
  let body: unknown = config.data;
  if (typeof body === 'string') { try { body = JSON.parse(body); } catch { body = undefined; } }
  const base = (config.baseURL ?? '').replace(/\/+$/, '');
  const path = `${base}/${(config.url ?? '').replace(/^\/+/, '')}`;

  const res = await getEngine().request({
    method: config.method ?? 'get',
    path,
    query: (config.params as Record<string, unknown> | undefined) ?? undefined,
    body,
    token: auth?.startsWith('Bearer ') ? auth.slice(7) : null,
  });

  const response: AxiosResponse = {
    data: res.data ?? '', status: res.status, statusText: String(res.status), headers: {}, config,
    request: {},
  };
  if (res.status >= 200 && res.status < 300) return response;
  throw new AxiosError(`Request failed with status code ${res.status}`, res.status >= 500 ? AxiosError.ERR_BAD_RESPONSE : AxiosError.ERR_BAD_REQUEST, config, response.request, response);
}
