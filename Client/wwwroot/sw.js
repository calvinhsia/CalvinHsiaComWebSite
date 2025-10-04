// Service Worker for PWA Updates and Caching
// Dynamic cache name based on manifest version
let CACHE_NAME = 'wordscape-pwa-v1.0.0'; // Default fallback

// Fetch version from manifest and update cache name
fetch('/manifest.json')
  .then(response => response.json())
  .then(manifest => {
    CACHE_NAME = `wordscape-pwa-v${manifest.version || '1.0.0'}`;
    console.log('[SW] Cache name updated to:', CACHE_NAME);
  })
  .catch(error => {
    console.warn('[SW] Could not fetch manifest for version, using default cache name:', error);
  });

const urlsToCache = [
  '/',
  '/wordscape',
  '/css/app.css',
  '/css/wordscape-game.css',
  '/js/wordscape-game.js',
  '/js/address-bar-manager.js',
  '/manifest.json',
  '/icon.svg',
  '/_framework/blazor.webassembly.js'
];

// Install event - cache resources
self.addEventListener('install', event => {
  console.log('[SW] Installing service worker...');
  event.waitUntil(
    // Fetch manifest first to get correct version
    fetch('/manifest.json')
      .then(response => response.json())
      .then(manifest => {
        CACHE_NAME = `wordscape-pwa-v${manifest.version || '1.0.0'}`;
        console.log('[SW] Installing with cache name:', CACHE_NAME);
        
        return caches.open(CACHE_NAME)
          .then(cache => {
            console.log('[SW] Caching app resources');
            return cache.addAll(urlsToCache);
          });
      })
      .then(() => {
        console.log('[SW] Service worker installed, will skip waiting');
        return self.skipWaiting(); // Force activate new service worker
      })
      .catch(error => {
        console.error('[SW] Install failed:', error);
        // Fallback to default cache name
        return caches.open(CACHE_NAME)
          .then(cache => cache.addAll(urlsToCache))
          .then(() => self.skipWaiting());
      })
  );
});

// Activate event - clean up old caches
self.addEventListener('activate', event => {
  console.log('[SW] Activating service worker...');
  event.waitUntil(
    Promise.all([
      // Update cache name from manifest
      fetch('/manifest.json')
        .then(response => response.json())
        .then(manifest => {
          CACHE_NAME = `wordscape-pwa-v${manifest.version || '1.0.0'}`;
          console.log('[SW] Activating with cache name:', CACHE_NAME);
        })
        .catch(() => console.warn('[SW] Could not update cache name during activation')),
      
      // Clean up old caches
      caches.keys().then(cacheNames => {
        return Promise.all(
          cacheNames.map(cacheName => {
            if (cacheName.startsWith('wordscape-pwa-v') && cacheName !== CACHE_NAME) {
              console.log('[SW] Deleting old cache:', cacheName);
              return caches.delete(cacheName);
            }
          })
        );
      })
    ]).then(() => {
      console.log('[SW] Service worker activated');
      return self.clients.claim(); // Take control of all pages
    })
  );
});

// Fetch event - serve from cache, fallback to network
self.addEventListener('fetch', event => {
  event.respondWith(
    caches.match(event.request)
      .then(response => {
        // Return cached version or fetch from network
        if (response) {
          console.log('[SW] Serving from cache:', event.request.url);
          return response;
        }
        
        console.log('[SW] Fetching from network:', event.request.url);
        return fetch(event.request).then(response => {
          // Don't cache non-successful responses
          if (!response || response.status !== 200 || response.type !== 'basic') {
            return response;
          }

          // Clone the response for caching
          const responseToCache = response.clone();
          caches.open(CACHE_NAME)
            .then(cache => {
              cache.put(event.request, responseToCache);
            });

          return response;
        });
      })
  );
});

// Listen for update messages from main app
self.addEventListener('message', event => {
  if (event.data && event.data.type === 'SKIP_WAITING') {
    console.log('[SW] Received skip waiting message');
    self.skipWaiting();
  }
});

// Check for manifest changes (version updates)
self.addEventListener('sync', event => {
  if (event.tag === 'version-check') {
    event.waitUntil(
      fetch('/manifest.json')
        .then(response => response.json())
        .then(manifest => {
          const newCacheName = `wordscape-pwa-v${manifest.version || '1.0.0'}`;
          if (newCacheName !== CACHE_NAME) {
            console.log('[SW] Version change detected:', CACHE_NAME, '->', newCacheName);
            
            // Notify clients about update
            self.clients.matchAll().then(clients => {
              clients.forEach(client => {
                client.postMessage({
                  type: 'UPDATE_AVAILABLE',
                  message: `New version ${manifest.version} available!`,
                  version: manifest.version
                });
              });
            });
          }
        })
        .catch(error => console.warn('[SW] Version check failed:', error))
    );
  }
});

// Periodic version check
setInterval(() => {
  if (self.registration && self.registration.sync) {
    self.registration.sync.register('version-check')
      .catch(error => console.warn('[SW] Could not register version check sync:', error));
  }
}, 300000); // Check every 5 minutes