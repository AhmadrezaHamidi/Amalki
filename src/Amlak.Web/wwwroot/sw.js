// Ashianeh service worker: makes the site installable and usable on a flaky connection.
// Pages: network first, then the last copy seen, then /offline.html. Static files and photos:
// cached copy first, refreshed in the background. Admin, account and form posts never touch the cache.
const VERSION = 'v1';
const PAGES = `pages-${VERSION}`;
const ASSETS = `assets-${VERSION}`;
const OFFLINE = '/offline.html';
const PRECACHE = [OFFLINE, '/icons/icon-192.png', '/manifest.webmanifest'];
const MAX_PAGES = 40;
const MAX_ASSETS = 150;

self.addEventListener('install', (e) => {
  e.waitUntil(caches.open(ASSETS).then((c) => c.addAll(PRECACHE)).then(() => self.skipWaiting()));
});

self.addEventListener('activate', (e) => {
  e.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(keys.filter((k) => k !== PAGES && k !== ASSETS).map((k) => caches.delete(k))))
      .then(() => self.clients.claim()),
  );
});

const trim = async (name, max) => {
  const c = await caches.open(name);
  const keys = await c.keys();
  await Promise.all(keys.slice(0, Math.max(0, keys.length - max)).map((k) => c.delete(k)));
};

const isPrivate = (url) => /^\/(admin|account)(\/|$)/i.test(decodeURIComponent(url.pathname));
const isAsset = (url) =>
  /^\/(css|js|icons|uploads|lib)\//.test(url.pathname) ||
  url.hostname === 'fonts.googleapis.com' || url.hostname === 'fonts.gstatic.com';

self.addEventListener('fetch', (e) => {
  const req = e.request;
  if (req.method !== 'GET') return;
  const url = new URL(req.url);
  const sameOrigin = url.origin === self.location.origin;
  if (sameOrigin && isPrivate(url)) return;

  if (req.mode === 'navigate' && sameOrigin) {
    e.respondWith((async () => {
      try {
        const res = await fetch(req);
        if (res.ok && !res.headers.get('Cache-Control')?.includes('no-store')) {
          const c = await caches.open(PAGES);
          await c.put(req, res.clone());
          trim(PAGES, MAX_PAGES);
        }
        return res;
      } catch {
        return (await caches.match(req, { ignoreVary: true })) || (await caches.match(OFFLINE));
      }
    })());
    return;
  }

  if (isAsset(url)) {
    e.respondWith((async () => {
      const c = await caches.open(ASSETS);
      const hit = await c.match(req);
      const fresh = fetch(req).then((res) => {
        if (res.ok || res.type === 'opaque') { c.put(req, res.clone()); trim(ASSETS, MAX_ASSETS); }
        return res;
      }).catch(() => hit || Response.error());
      return hit || fresh;
    })());
  }
});
