// Device context and display settings for OneLoaf Web (design-system-in-blazor.md §3–§5).
// ES module imported by DeviceContext (OneLoaf.Web.Shared/Device). No third-party code, no network access.

const DISPLAY_KEY = "oneloaf.display";
const queries = {
    watch: "(max-width: 17.49em)",
    medium: "(min-width: 37.5em)",
    expanded: "(min-width: 64em)",
    large: "(min-width: 90em)",
    ultra: "(min-width: 160em)",
    phoneLandscape: "(orientation: landscape) and (max-height: 30em) and (max-width: 63.99em)",
    landscape: "(orientation: landscape)",
    coarse: "(pointer: coarse)",
    hover: "(hover: hover)",
    dark: "(prefers-color-scheme: dark)",
    reduceMotion: "(prefers-reduced-motion: reduce)",
    segments: "(horizontal-viewport-segments: 2)"
};

let dotnet = null;
let lists = [];
const cleanups = [];

function sizeClass(m) {
    if (m.watch) return "watch";
    if (m.ultra) return "ultra";
    if (m.large) return "large";
    if (m.expanded) return "expanded";
    if (m.medium || m.phoneLandscape) return "medium";
    return "compact";
}

function snapshot() {
    const m = {};
    for (const [key, list] of lists) m[key] = list.matches;
    return {
        sizeClass: sizeClass(m),
        landscape: m.landscape,
        coarsePointer: m.coarse,
        canHover: m.hover,
        prefersDark: m.dark,
        prefersReducedMotion: m.reduceMotion,
        twoSegments: m.segments,
        pixelRatio: window.devicePixelRatio || 1
    };
}

// Only primitives cross the interop boundary (trimming-safe on the .NET side).
function report() {
    if (!dotnet) return;
    const d = snapshot();
    dotnet.invokeMethodAsync("OnDeviceChanged", d.sizeClass, d.landscape, d.coarsePointer, d.canHover,
        d.prefersDark, d.prefersReducedMotion, d.twoSegments, d.pixelRatio);
}

function reportConnectivity() {
    if (dotnet) dotnet.invokeMethodAsync("OnConnectivityChanged", navigator.onLine);
}

// Fallback for browsers without interactive-widget: expose the keyboard height as a CSS variable.
function updateKeyboardInset() {
    const vv = window.visualViewport;
    if (!vv) return;
    const inset = Math.max(0, window.innerHeight - vv.height - vv.offsetTop);
    document.documentElement.style.setProperty("--ol-keyboard-inset", inset + "px");
}

function applyThemeColor() {
    const meta = document.querySelector("meta[name=theme-color]");
    if (!meta) return;
    const dark = document.documentElement.getAttribute("data-theme") === "dark";
    meta.setAttribute("content", dark ? "#1a0f06" : "#faf7f2");
}

function listen(target, type, handler) {
    target.addEventListener(type, handler);
    cleanups.push(() => target.removeEventListener(type, handler));
}

export function start(dotnetRef) {
    stop();
    dotnet = dotnetRef;
    lists = Object.entries(queries).map(([key, q]) => [key, window.matchMedia(q)]);
    for (const [, list] of lists) listen(list, "change", report);
    listen(window, "online", reportConnectivity);
    listen(window, "offline", reportConnectivity);
    if (window.visualViewport) listen(window.visualViewport, "resize", updateKeyboardInset);
    updateKeyboardInset();
    const prefs = readDisplay();
    dotnet.invokeMethodAsync("OnDisplayLoaded", prefs.theme, prefs.scale, prefs.reduceMotion);
    report();
    reportConnectivity();
}

export function stop() {
    while (cleanups.length) cleanups.pop()();
    dotnet = null;
}

export function readDisplay() {
    try {
        const prefs = JSON.parse(localStorage.getItem(DISPLAY_KEY) || "{}") || {};
        return {
            theme: ["light", "dark"].includes(prefs.theme) ? prefs.theme : "system",
            scale: [90, 100, 125, 150].includes(prefs.scale) ? prefs.scale : 100,
            reduceMotion: prefs.reduceMotion === true
        };
    } catch {
        return { theme: "system", scale: 100, reduceMotion: false };
    }
}

// Writes data-theme / data-scale / data-motion on <html> and stores the (non-sensitive) preferences.
export function applyDisplay(theme, scale, reduceMotion) {
    const prefs = { theme, scale, reduceMotion };
    const root = document.documentElement;
    const dark = prefs.theme === "dark" || (prefs.theme === "system" && window.matchMedia(queries.dark).matches);
    root.setAttribute("data-theme", dark ? "dark" : "light");
    root.setAttribute("data-scale", String(prefs.scale));
    if (prefs.reduceMotion) root.setAttribute("data-motion", "reduce");
    else root.removeAttribute("data-motion");
    applyThemeColor();
    try {
        localStorage.setItem(DISPLAY_KEY, JSON.stringify(prefs));
    } catch {
        // Storage can be unavailable (private mode); the setting then lasts for this page only.
    }
}
