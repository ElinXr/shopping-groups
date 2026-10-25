// Cache the application shell only. Private API data, invitations and account pages are never cached.
const CACHE = "shopping-shell-" + (self.__SHELL_VERSION__ || "development");
const SHELL = [
  "/",
  "/index.html",
  "/icon.svg",
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

// Empty-body Web Push: always display immediately, including on Safari.
// Private group/list text and subscription credentials are never cached or displayed.
self.addEventListener("push", (event) => {
  event.waitUntil(self.registration.showNotification("Shopping Groups", {
    body: "Имате нови известия от вашите групи. Отворете приложението, за да ги видите.",
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
