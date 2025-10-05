// Development-friendly Service Worker for Blazor WASM PWA
const CACHE_NAME = 'calvinhsia-games-v3'; // Increment version to clear old cache

// Core resources that should be cached
const CORE_CACHE_URLS = [
  '/',
  '/css/app.css',
  '/manifest.json',
  '/icon.svg'
];

// Install - cache only essential resources
self.addEventListener('install', event => {
  console.log('[SW] Installing service worker v3...');
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then(cache => {
        console.log('[SW] Caching core resources');
        return cache.addAll(CORE_CACHE_URLS);
      })
      .then(() => {
        console.log('[SW] Service worker installed, activating...');
        return self.skipWaiting();
      })
      .catch(error => {
        console.error('[SW] Install failed:', error);
        return self.skipWaiting();
      })
  );
});

// Activate - clean old caches
self.addEventListener('activate', event => {
  console.log('[SW] Activating service worker v3...');
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
      console.log('[SW] Service worker v3 activated');
      return self.clients.claim();
    })
  );
});

// Fetch - development-friendly strategy
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

  // ALWAYS fetch fresh for development resources during development
  const isDevelopmentResource = (
    url.pathname.endsWith('.js') ||
    url.pathname.endsWith('.css') ||
    url.pathname.endsWith('.razor') ||
    url.pathname.includes('_framework/blazor.webassembly.js') ||
    url.pathname.includes('_framework/dotnet.') ||
    url.pathname === '/' ||
    url.pathname.startsWith('/logo') ||
    url.pathname.startsWith('/wordscape') ||
    url.pathname.startsWith('/wordament')
  );

  if (isDevelopmentResource) {
    console.log('[SW] Force fresh fetch for development resource:', url.pathname);
    event.respondWith(
      fetch(event.request, {
        cache: 'no-cache',
        headers: {
          'Cache-Control': 'no-cache, no-store, must-revalidate',
          'Pragma': 'no-cache',
          'Expires': '0'
        }
      })
        .then(response => {
          console.log('[SW] Fresh resource loaded:', url.pathname);
          // Don't cache development resources
          return response;
        })
        .catch(error => {
          console.error('[SW] Failed to fetch development resource:', url.pathname, error);
          // Try cache as fallback for critical resources only
          if (CORE_CACHE_URLS.includes(url.pathname)) {
            return caches.match(event.request);
          }
          throw error;
        })
    );
    return;
  }

  // For everything else, use cache-first strategy
  event.respondWith(
    caches.match(event.request)
      .then(cachedResponse => {
        if (cachedResponse) {
          return cachedResponse;
        }
        
        return fetch(event.request)
          .then(response => {
            // Cache successful responses for non-development files
            if (response.status === 200 && response.type === 'basic') {
              const responseClone = response.clone();
              caches.open(CACHE_NAME)
                .then(cache => cache.put(event.request, responseClone))
                .catch(() => {});
            }
            return response;
          });
      })
  );
});

// Handle messages from main app
self.addEventListener('message', event => {
  if (event.data && event.data.type === 'SKIP_WAITING') {
    console.log('[SW] Received skip waiting message');
    self.skipWaiting();
  }
});

// Clear all caches on demand (for development)
self.addEventListener('message', event => {
  if (event.data && event.data.type === 'CLEAR_CACHE') {
    console.log('[SW] Clearing all caches...');
    caches.keys().then(cacheNames => {
      return Promise.all(cacheNames.map(name => caches.delete(name)));
    }).then(() => {
      console.log('[SW] All caches cleared');
      event.ports[0].postMessage({success: true});
    });
  }
});

// Force update all clients
self.addEventListener('message', event => {
  if (event.data && event.data.type === 'FORCE_UPDATE') {
    console.log('[SW] Force updating all clients...');
    self.clients.matchAll().then(clients => {
      clients.forEach(client => {
        client.postMessage({type: 'FORCE_RELOAD'});
      });
    });
  }
});