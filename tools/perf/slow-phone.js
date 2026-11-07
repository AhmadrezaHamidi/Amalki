// Page speed on a mid-range phone over a slow mobile network (Lighthouse "Slow 4G" + 4x CPU slowdown).
// Usage: node tools/perf/slow-phone.js [baseUrl] [path ...]
// Prints, per page, first visit and repeat visit: TTFB, FCP, LCP, CLS, requests, bytes over the wire.
const { chromium } = require('playwright');

const base = (process.argv[2] || 'http://localhost:5244').replace(/\/$/, '');
const paths = process.argv.length > 3 ? process.argv.slice(3) : ['/', '/املاک', '/املاک/تهران/خرید-آپارتمان'];
const SLOW_4G = { offline: false, latency: 150, downloadThroughput: (1.6 * 1024 * 1024) / 8, uploadThroughput: (750 * 1024) / 8 };

async function visit(page, cdp, url) {
  let bytes = 0, requests = 0;
  const onData = (e) => { bytes += e.encodedDataLength; requests++; };
  cdp.on('Network.loadingFinished', onData);
  await page.addInitScript(() => {
    window.__v = { lcp: 0, cls: 0 };
    new PerformanceObserver((l) => { for (const e of l.getEntries()) window.__v.lcp = e.startTime; }).observe({ type: 'largest-contentful-paint', buffered: true });
    new PerformanceObserver((l) => { for (const e of l.getEntries()) if (!e.hadRecentInput) window.__v.cls += e.value; }).observe({ type: 'layout-shift', buffered: true });
  });
  await page.goto(url, { waitUntil: 'networkidle', timeout: 120000 });
  await page.waitForTimeout(500);
  const m = await page.evaluate(() => {
    const nav = performance.getEntriesByType('navigation')[0];
    const fcp = performance.getEntriesByName('first-contentful-paint')[0];
    return { ttfb: nav.responseStart, fcp: fcp?.startTime ?? 0, lcp: window.__v.lcp, cls: window.__v.cls, load: nav.loadEventEnd };
  });
  cdp.off('Network.loadingFinished', onData);
  return { ...m, requests, kb: bytes / 1024 };
}

(async () => {
  const browser = await chromium.launch();
  const context = await browser.newContext({ viewport: { width: 412, height: 823 }, isMobile: true, hasTouch: true, locale: 'fa-IR',
    userAgent: 'Mozilla/5.0 (Linux; Android 11; moto g power) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Mobile Safari/537.36' });
  const page = await context.newPage();
  const cdp = await context.newCDPSession(page);
  await cdp.send('Network.enable');
  await cdp.send('Network.emulateNetworkConditions', SLOW_4G);
  await cdp.send('Emulation.setCPUThrottlingRate', { rate: 4 });
  const rows = [];
  for (const p of paths) {
    const url = base + encodeURI(p);
    await cdp.send('Network.clearBrowserCache');
    const first = await visit(page, cdp, url);
    const repeat = await visit(page, cdp, url);
    rows.push({ page: p, visit: 'first', ...first }, { page: p, visit: 'repeat', ...repeat });
  }
  const f = (n) => Math.round(n);
  console.log('page | visit | TTFB ms | FCP ms | LCP ms | CLS | load ms | requests | KB');
  for (const r of rows) console.log([r.page, r.visit, f(r.ttfb), f(r.fcp), f(r.lcp), r.cls.toFixed(3), f(r.load), r.requests, r.kb.toFixed(1)].join(' | '));
  await browser.close();
})();
