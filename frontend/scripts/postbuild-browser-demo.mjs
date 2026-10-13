// Makes the static browser-demo build deployable on any static host: SPA fallback files, no host-specific config.
import { copyFileSync, writeFileSync } from 'node:fs';
const dir = 'dist-browser-demo';
copyFileSync(`${dir}/index.html`, `${dir}/404.html`);      // GitHub Pages SPA fallback
writeFileSync(`${dir}/_redirects`, '/* /index.html 200\n'); // Netlify / Cloudflare Pages
console.log('browser-demo: wrote 404.html and _redirects');
