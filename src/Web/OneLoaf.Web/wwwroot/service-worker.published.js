// Published service worker (US-44): the app shell from the build's asset manifest is cached at install, so the app
// starts offline; recipe reads from the Core API are network-first with a cached copy as fallback, so recipes viewed
// before are readable offline. Writes are never cached or queued. Push handlers come from push-handlers.js.
self.importScripts('./service-worker-assets.js');
self.importScripts('./push-handlers.js');

const cacheNamePrefix = 'oneloaf-offline-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const dataCacheName = 'oneloaf-recipes';
const offlineAssetsInclude = [/\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff2?$/, /\.png$/, /\.jpe?g$/, /\.svg$/, /\.webmanifest$/, /\.dat$/, /\.blat$/];
const offlineAssetsExclude = [/^service-worker\.js$/, /appsettings\.Development\.json$/];
// Recipe reads (list, detail, calculator) — the "last viewed recipes" of US-44.
const cachedApi = /\/core\/api\/v1\/(recipes|reference)(\/|\?|$)/;

self.addEventListener('install', event => event.waitUntil(onInstall()));
self.addEventListener('activate', event => event.waitUntil(onActivate()));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));
self.addEventListener('message', event => {
    // Sent on sign out: cached recipe data belongs to the user who signed out.
    if (event.data === 'clear-user-data') {
        event.waitUntil(caches.delete(dataCacheName));
    }
});

async function onInstall() {
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
    await caches.open(cacheName).then(cache => cache.addAll(assetsRequests));
    self.skipWaiting();
}

async function onActivate() {
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
    await self.clients.claim();
}

async function onFetch(event) {
    const request = event.request;
    if (request.method !== 'GET') {
        return fetch(request);
    }

    const url = new URL(request.url);
    if (url.origin === self.location.origin && cachedApi.test(url.pathname + url.search)) {
        return networkFirst(request);
    }

    if (request.mode === 'navigate' && !url.pathname.startsWith('/core/') && !url.pathname.startsWith('/assistant/api')
        && !url.pathname.startsWith('/notify/')) {
        // Every client route is index.html (SPA fallback).
        const cache = await caches.open(cacheName);
        return (await cache.match('index.html')) ?? fetch(request);
    }

    const cache = await caches.open(cacheName);
    return (await cache.match(request)) ?? fetch(request);
}

async function networkFirst(request) {
    const cache = await caches.open(dataCacheName);
    try {
        const response = await fetch(request);
        if (response.ok) {
            await cache.put(request, response.clone());
        }
        return response;
    } catch (error) {
        const cached = await cache.match(request);
        if (cached) {
            return cached;
        }
        throw error;
    }
}
