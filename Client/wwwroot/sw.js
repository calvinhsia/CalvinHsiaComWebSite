// Development-friendly Service Worker for Blazor WASM PWA
const SW_VERSION = 'v10'; // ?? INCREMENTED to force reload for FreeCell touch/pen fix
const CACHE_NAME = `calvinhsia-games-${SW_VERSION}`;

// Core resources that should be cached
const CORE_CACHE_URLS = [
  '/',
  '/css/app.css',
  '/manifest.json',
  '/icon.svg'
];

// Install - cache only essential resources
self.addEventListener('install', event => {
  console.log(`[SW] Installing service worker ${SW_VERSION}...`);
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then(cache => {
        console.log('[SW] Caching core resources');
        return cache.addAll(CORE_CACHE_URLS);
      })
      .then(() => {
        console.log(`[SW] Service worker ${SW_VERSION} installed, activating immediately...`);
        return self.skipWaiting();
      })
      .catch(error => {
        console.error('[SW] Install failed:', error);
        return self.skipWaiting();
      })
  );
});

// Activate - clean old caches aggressively
self.addEventListener('activate', event => {
  console.log(`[SW] Activating service worker ${SW_VERSION}...`);
  event.waitUntil(
    caches.keys().then(cacheNames => {
      return Promise.all(
        cacheNames.map(cacheName => {
          if (cacheName !== CACHE_NAME) {
            console.log('[SW] Deleting old cache:', cacheName);
            return caches.delete(cacheName);
          }
        })
      );
    }).then(() => {
      console.log(`[SW] Service worker ${SW_VERSION} activated, claiming all clients...`);
      return self.clients.claim();
    }).then(() => {
      // Reload all clients to ensure they get the new service worker
      return self.clients.matchAll().then(clients => {
        clients.forEach(client => {
          console.log('[SW] Notifying client to reload for service worker update');
          client.postMessage({type: 'SW_UPDATED'});
        });
      });
    })
  );
});

// Fetch - very aggressive development strategy for Blazor WASM
self.addEventListener('fetch', event => {
  const url = new URL(event.request.url);
  
  // Skip non-GET requests
  if (event.request.method !== 'GET') {
    return;
  }
  
  // Skip cross-origin requests
  if (url.origin !== location.origin) {
    return;
  }

  // ALWAYS fetch fresh for ALL Blazor and development resources
  const isBlazorOrDevResource = (
    url.pathname.endsWith('.js') ||
    url.pathname.endsWith('.css') ||
    url.pathname.endsWith('.razor') ||
    url.pathname.endsWith('.dll') ||
    url.pathname.endsWith('.wasm') ||
    url.pathname.endsWith('.dat') ||
    url.pathname.endsWith('.blat') ||
    url.pathname.includes('_framework/') ||
    url.pathname.includes('_content/') ||
    url.pathname === '/' ||
    url.pathname.startsWith('/logo') ||
    url.pathname.startsWith('/wordscape') ||
    url.pathname.startsWith('/wordament') ||
    url.pathname.includes('.dll.br') ||
    url.pathname.includes('.dll.gz') ||
    url.pathname.includes('.wasm.br') ||
    url.pathname.includes('.wasm.gz')
  );

  if (isBlazorOrDevResource) {
    console.log('[SW] Force fresh fetch for Blazor/dev resource:', url.pathname);
    event.respondWith(
      fetch(event.request, {
        cache: 'no-cache',
        headers: {
          'Cache-Control': 'no-cache, no-store, must-revalidate, max-age=0',
          'Pragma': 'no-cache',
          'Expires': '0'
        }
      })
        .then(response => {
          console.log('[SW] Fresh Blazor resource loaded:', url.pathname, 'Status:', response.status);
          // Don't cache any development resources in development mode
          return response;
        })
        .catch(error => {
          console.error('[SW] Failed to fetch Blazor resource:', url.pathname, error);
          // Only try cache as fallback for core resources
          if (CORE_CACHE_URLS.includes(url.pathname)) {
            return caches.match(event.request);
          }
          throw error;
        })
    );
    return;
  }

  // For static assets (images, fonts, etc.), use cache-first strategy
  event.respondWith(
    caches.match(event.request)
      .then(cachedResponse => {
        if (cachedResponse) {
          console.log('[SW] Serving cached static asset:', url.pathname);
          return cachedResponse;
        }
        
        return fetch(event.request)
          .then(response => {
            // Cache successful responses for static assets only
            if (response.status === 200 && response.type === 'basic') {
              const responseClone = response.clone();
              caches.open(CACHE_NAME)
                .then(cache => cache.put(event.request, responseClone))
                .catch(() => {});
            }
            return response;
          })
          .catch(error => {
            console.error('[SW] Failed to fetch static asset:', url.pathname, error);
            throw error;
          });
      })
  );
});
