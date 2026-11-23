// Development service worker: no caching (every reload gets fresh code), only Web Push so reminders can be tried
// locally. The published build uses service-worker.published.js (offline shell, cached recipes, push).
self.importScripts('./push-handlers.js');
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));
self.addEventListener('fetch', () => { });
