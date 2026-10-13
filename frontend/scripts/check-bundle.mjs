// Bundle check: the normal build (dist) must not contain the in-browser adapter; the demo build must.
import { readdirSync, readFileSync, existsSync } from 'node:fs';
import process from 'node:process';
const read = (dir) => readdirSync(dir, { recursive: true }).filter((f) => /\.(js|html|css)$/.test(f)).map((f) => readFileSync(`${dir}/${f}`, 'utf8')).join('\n');
const MARKERS = ['wms-browser-demo:v', 'runs entirely in your browser'];
let failed = false;
if (existsSync('dist')) {
  const text = read('dist');
  const hits = MARKERS.filter((m) => text.includes(m));
  console.log(hits.length ? `FAIL normal build contains browser-demo code: ${hits.join(', ')}` : 'ok   normal build (dist) has no browser-demo code');
  failed ||= hits.length > 0;
} else { console.log('skip dist missing'); }
if (existsSync('dist-browser-demo')) {
  const text = read('dist-browser-demo');
  const missing = MARKERS.filter((m) => !text.includes(m));
  console.log(missing.length ? `FAIL demo build lacks: ${missing.join(', ')}` : 'ok   demo build contains the adapter');
  const net = /fonts\.googleapis|fonts\.gstatic/.test(text);
  console.log(net ? 'FAIL demo build references Google Fonts' : 'ok   demo build references no external hosts for fonts');
  failed ||= missing.length > 0 || net;
}
process.exit(failed ? 1 : 0);
