import process from 'node:process';
import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

const here = (p) => fileURLToPath(new URL(p, import.meta.url));

// `--mode browser-demo` builds the browser-only demo (in-browser API on localStorage, no network). Every other mode aliases
// "#browser-demo" to an empty stub, so a normal build never contains the adapter.
export default defineConfig(({ mode }) => {
  const browserDemo = mode === 'browser-demo';
  return {
    plugins: [
      react(),
      {
        name: 'browser-demo-no-network',
        transformIndexHtml(html) {
          // The demo makes no network calls at all: drop the Google Fonts links (the UI falls back to system fonts).
          return browserDemo ? html.replace(/\s*<link[^>]+(fonts\.googleapis|fonts\.gstatic)[^>]*>/g, '') : html;
        },
      },
    ],
    base: process.env.VITE_BASE_PATH || '/',
    build: browserDemo ? { outDir: 'dist-browser-demo' } : undefined,
    resolve: {
      alias: {
        '#browser-demo': here(browserDemo ? './src/browser-demo/index.ts' : './src/browser-demo-stub/index.js'),
      },
    },
    test: {
      environment: 'jsdom',
      setupFiles: './src/test/setup.js',
      include: ['src/**/*.test.{js,jsx,ts}'],
      css: false,
    },
    server: {
      port: 5173,
      strictPort: true,
      fs: { allow: ['..'] },
      proxy: {
        '/api': {
          target: process.env.VITE_PROXY_TARGET || 'http://localhost:5300',
          changeOrigin: true,
          rewrite: (path) => path,
        },
      },
    },
  };
});
