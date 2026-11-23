// Web Push handlers shared by both service workers (US-29). The Notification service sends JSON:
// { title, body, url } — the click opens that page of the app (or focuses an open tab).
self.addEventListener('push', event => {
    let data = {};
    try {
        data = event.data ? event.data.json() : {};
    } catch {
        data = { body: event.data ? event.data.text() : '' };
    }
    const title = data.title || 'OneLoaf';
    event.waitUntil(self.registration.showNotification(title, {
        body: data.body || '',
        icon: 'app-icons/icon-192.png',
        badge: 'app-icons/icon-192.png',
        data: { url: data.url || './' },
        tag: data.tag,
    }));
});

self.addEventListener('notificationclick', event => {
    event.notification.close();
    const target = new URL(event.notification.data?.url || './', self.registration.scope).href;
    event.waitUntil((async () => {
        const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
        const open = windows.find(w => w.url.startsWith(self.registration.scope));
        if (open) {
            await open.focus();
            return open.navigate(target);
        }
        return self.clients.openWindow(target);
    })());
});
