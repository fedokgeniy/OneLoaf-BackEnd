// Web Push in the browser (US-29) and saving downloaded files (US-50). Called from SettingsService; the app never
// asks for notification permission without a click.

function keyBytes(base64Url) {
    const padding = '='.repeat((4 - (base64Url.length % 4)) % 4);
    const raw = atob((base64Url + padding).replace(/-/g, '+').replace(/_/g, '/'));
    return Uint8Array.from(raw, c => c.charCodeAt(0));
}

function toSubscription(subscription) {
    if (!subscription) {
        return null;
    }
    const json = subscription.toJSON();
    return { endpoint: json.endpoint, p256dh: json.keys.p256dh, auth: json.keys.auth };
}

async function registration() {
    if (!('serviceWorker' in navigator) || !('PushManager' in window) || !('Notification' in window)) {
        return null;
    }
    return (await navigator.serviceWorker.getRegistration()) ?? null;
}

// "unsupported" (no service worker or push in this browser/build), "denied", "on" or "off".
export async function status() {
    const reg = await registration();
    if (!reg) {
        return 'unsupported';
    }
    if (Notification.permission === 'denied') {
        return 'denied';
    }
    return (await reg.pushManager.getSubscription()) ? 'on' : 'off';
}

export async function current() {
    const reg = await registration();
    return reg ? toSubscription(await reg.pushManager.getSubscription()) : null;
}

export async function subscribe(vapidPublicKey) {
    const reg = await registration();
    if (!reg || (await Notification.requestPermission()) !== 'granted') {
        return null;
    }
    const subscription = await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: keyBytes(vapidPublicKey) });
    return toSubscription(subscription);
}

export async function unsubscribe() {
    const reg = await registration();
    const subscription = reg ? await reg.pushManager.getSubscription() : null;
    if (subscription) {
        await subscription.unsubscribe();
    }
}

export function saveFile(fileName, contentType, base64) {
    const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
    const url = URL.createObjectURL(new Blob([bytes], { type: contentType }));
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}
