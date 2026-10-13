import api from './api';

/**
 * Public demo mode. The API says whether it is a demo (GET /api/demo-info: a flag and the role names, never a password).
 * The shared demo password is public by design and comes from the demo build configuration (VITE_DEMO_PASSWORD, set by
 * docker-compose.demo.yml / scripts/demo.sh). A normal build has neither, so none of this shows up.
 */
export const demoPassword = () => import.meta.env.VITE_DEMO_PASSWORD || '';

let cached;
export function loadDemoInfo() {
  cached ??= api.get('/demo-info').then((res) => res.data).catch(() => ({ enabled: false }));
  return cached;
}
export const forgetDemoInfo = () => { cached = undefined; };

export const resetDemoData = () => api.post('/demo/reset', {});
export const reloadPage = () => window.location.reload();
