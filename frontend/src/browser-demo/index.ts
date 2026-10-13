/**
 * Entry of the browser-only demo. The app imports this through the "#browser-demo" alias, which Vite points at this file
 * only for `--mode browser-demo`; every other build gets src/browser-demo-stub instead, so none of this code is bundled.
 */
export const BROWSER_DEMO = true;
export { browserDemoAdapter, getEngine } from './axiosAdapter';
export { default as BrowserDemoNotice } from './ui/BrowserDemoNotice.jsx';
