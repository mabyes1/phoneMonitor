const CACHE_NAME = "vibedeck-app-v34";
const APP_SHELL = [
  "/",
  "/index.html",
  "/offline.html",
  "/manifest.json",
  "/manifest.json?v=22",
  "/silent-loop.mp4",
  "/icons/icon-192.png",
  "/icons/icon-192.png?v=22",
  "/icons/icon-512.png",
  "/icons/icon-512.png?v=22",
  "/icons/maskable-512.png",
  "/icons/maskable-512.png?v=22",
  "/icons/apple-touch-icon.png",
  "/icons/apple-touch-icon.png?v=22",
  "/sideboard-skins/command.png",
  "/sideboard-skins/dial.png",
  "/sideboard-skins/focus.png"
];
const NETWORK_ONLY_PREFIXES = [
  "/api/",
  "/stream/",
  "/ws/"
];
const NETWORK_ONLY_PATHS = new Set([
  "/health",
  "/qr.svg",
  "/cert/phone-monitor-root.cer",
  "/cert/phone-monitor-host.cer",
  // Always fetch the live phone UI so layout fixes ship immediately.
  "/",
  "/index.html",
  "/service-worker.js"
]);

self.addEventListener("install", event => {
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then(cache => cache.addAll(APP_SHELL))
      .then(() => self.skipWaiting())
  );
});

self.addEventListener("activate", event => {
  event.waitUntil(
    caches.keys()
      .then(keys => Promise.all(
        keys
          .filter(key => key !== CACHE_NAME)
          .map(key => caches.delete(key))
      ))
      .then(() => self.clients.claim())
  );
});

function isNetworkOnly(url) {
  return NETWORK_ONLY_PATHS.has(url.pathname) ||
    NETWORK_ONLY_PREFIXES.some(prefix => url.pathname.startsWith(prefix));
}

self.addEventListener("fetch", event => {
  const request = event.request;
  const url = new URL(request.url);

  if (request.method !== "GET" || url.origin !== self.location.origin) {
    return;
  }

  if (isNetworkOnly(url)) {
    event.respondWith(
      fetch(request).catch(() => caches.match(request).then(response => response || caches.match("/offline.html")))
    );
    return;
  }

  if (request.mode === "navigate") {
    event.respondWith(
      fetch(request)
        .then(response => response)
        .catch(() => caches.match("/index.html").then(response => response || caches.match("/offline.html")))
    );
    return;
  }

  event.respondWith(
    caches.match(request)
      .then(cached => cached || fetch(request).then(response => {
        if (response.ok && response.type === "basic") {
          const copy = response.clone();
          caches.open(CACHE_NAME).then(cache => cache.put(request, copy));
        }
        return response;
      }).catch(() => cached || caches.match("/offline.html")))
  );
});
