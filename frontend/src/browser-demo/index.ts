/**
 * Entry of the browser-only demo. The app imports this through the "#browser-demo" alias, which Vite points at this file
 * only for `--mode browser-demo`; every other build gets src/browser-demo-stub instead, so none of this code is bundled.
 */
export const BROWSER_DEMO = true;
export { browserDemoAdapter, getEngine } from './axiosAdapter';
export { default as BrowserDemoNotice } from './ui/BrowserDemoNotice.jsx';
export { BrowserDemoExplainer } from './ui/AboutDemoDialog.jsx';
import { DEMO_ACCOUNTS } from './enums';
/** Known at build time, so the login page can render the demo panel on the first paint (no layout shift). */
export const browserDemoInfo = { enabled: true, accounts: DEMO_ACCOUNTS.map((a) => ({ role: a.role, username: a.username })) };
