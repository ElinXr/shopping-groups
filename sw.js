// Cache the application shell only. Private API data, invitations and account pages are never cached.
const CACHE = "shopping-shell-" + (self.__SHELL_VERSION__ || "development");
const SHELL = [
  "/",
  "/index.html",
  "/icon.svg",
  "/theme-init.js",
  "/icon-192.png",
  "/icon-512.png",
  "/manifest.webmanifest",
  ...(self.__PRECACHE_ASSETS__ || []),
];
self.addEventListener("install", (event) => {
  event.waitUntil(caches.open(CACHE).then((cache) => cache.addAll(SHELL)));
  self.skipWaiting();
});
self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) => Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k))))
      .then(() => self.clients.claim()),
  );
});
self.addEventListener("fetch", (event) => {
  const url = new URL(event.request.url);
  if (
    event.request.method !== "GET" ||
    url.origin !== self.location.origin ||
    url.pathname.startsWith("/api") ||
    url.pathname.startsWith("/hubs") ||
    url.search
  )
    return;
  if (event.request.mode === "navigate") {
    event.respondWith(fetch(event.request).catch(() => caches.match("/index.html")));
  } else if (url.pathname.startsWith("/assets/") || SHELL.includes(url.pathname)) {
    event.respondWith(
      caches.match(event.request).then(
        (cached) =>
          cached ||
          fetch(event.request).then((response) => {
            if (response.ok) {
              const copy = response.clone();
              caches.open(CACHE).then((cache) => cache.put(event.request, copy));
            }
            return response;
          }),
      ),
    );
  }
});

// The browser decrypts aes128gcm Web Push before exposing event.data.
// Always show a notification, including when an old/malformed payload is received.
self.addEventListener("push", (event) => {
  let body = "Имате нови известия от вашите групи. Отворете приложението, за да ги видите.";
  let timestamp = Date.now();
  let title = "Shopping Groups";
  try {
    const data = event.data?.json();
    if (data?.version === 1 && typeof data.body === "string" && data.body.trim()) {
      body = Array.from(data.body).slice(0, 1800).join("");
      if (typeof data.title === "string" && data.title.trim()) title = Array.from(data.title.trim()).slice(0, 120).join("");
      const at = Date.parse(data.at);
      if (Number.isFinite(at)) timestamp = at;
    }
  } catch { /* A generic notification keeps older push subscriptions usable. */ }
  event.waitUntil(self.registration.showNotification(title, {
    body,
    timestamp,
    icon: "/icon-192.png",
    badge: "/icon-192.png",
    tag: "shopping-groups",
  }));
});
self.addEventListener("notificationclick", (event) => {
  event.notification.close();
  event.waitUntil((async () => {
    const windows = await self.clients.matchAll({ type: "window", includeUncontrolled: true });
    const existing = windows.find(client => new URL(client.url).origin === self.location.origin);
    if (existing) {
      await existing.focus();
      existing.postMessage({ type: "open-notifications" });
    } else {
      await self.clients.openWindow("/?notifications=1");
    }
  })());
});
