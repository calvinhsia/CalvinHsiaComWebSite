// Minimal development service worker for Blazor WASM
// Kept simple intentionally: complex fetch interception causes hangs on F5.
const SW_VERSION = 'v14';
const CACHE_NAME = `calvinhsia-games-${SW_VERSION}`;

self.addEventListener('install', event => {
  console.log(`[SW ${SW_VERSION}] install`);
  // Activate immediately, don't wait for old SW to finish
  self.skipWaiting();
});

self.addEventListener('activate', event => {
  console.log(`[SW ${SW_VERSION}] activate`);
  event.waitUntil(
    // Delete all old caches
    caches.keys().then(keys =>
      Promise.all(keys.filter(k => k !== CACHE_NAME).map(k => {
        console.log(`[SW ${SW_VERSION}] deleting old cache: ${k}`);
        return caches.delete(k);
      }))
    ).then(() => self.clients.claim())
  );
});

// Fetch: pass everything through to the network.
// Do NOT intercept _framework/ (WASM/DLLs) — Blazor handles integrity itself,
// and intercepting them with no-cache floods Chrome with parallel requests that
// get throttled when the tab is in the background, causing the F5 hang.
// Do NOT use retry loops — they add multi-second delays to cold starts.
self.addEventListener('fetch', event => {
  const url = new URL(event.request.url);

  // Only handle same-origin GET requests
  if (event.request.method !== 'GET' || url.origin !== location.origin) {
    return;
  }

  // Let these go straight to the network (no SW interception):
  //  - _framework/ : WASM runtime + DLLs
  //  - _content/   : Blazor component libraries
  //  - api/        : Azure Functions backend
  if (url.pathname.includes('_framework/') ||
      url.pathname.includes('_content/') ||
      url.pathname.startsWith('/api/')) {
    return;
  }

  // For everything else (app JS, CSS, images, SPA routes):
  // network-first, no retry loops, no cache override headers.
  event.respondWith(
    fetch(event.request)
      .catch(() => caches.match(event.request))
  );
});
