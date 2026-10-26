// Crawler traffic: sitemap.xml, llms.txt and robots.txt at a fixed arrival rate.
// k6 run -e RATE=2 loadtest/seo.js
import http from 'k6/http';
import { BASE, ok } from './lib.js';

export const options = {
  scenarios: {
    sitemap: { executor: 'constant-arrival-rate', exec: 'sitemap', rate: Number(__ENV.RATE || 1), timeUnit: '1s',
      duration: __ENV.DURATION || '1m', preAllocatedVUs: 5, maxVUs: 50 },
    small: { executor: 'constant-arrival-rate', exec: 'small', rate: Number(__ENV.RATE || 1) * 5, timeUnit: '1s',
      duration: __ENV.DURATION || '1m', preAllocatedVUs: 5, maxVUs: 50 },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    'http_req_duration{name:sitemap}': ['p(95)<2000'],
    'http_req_duration{name:llms}': ['p(95)<300'],
    'http_req_duration{name:robots}': ['p(95)<100'],
  },
};

export function sitemap() {
  ok(http.get(`${BASE}/sitemap.xml`, { tags: { name: 'sitemap' }, timeout: '60s' }), 'sitemap');
}

export function small() {
  ok(http.get(`${BASE}/llms.txt`, { tags: { name: 'llms' } }), 'llms');
  ok(http.get(`${BASE}/robots.txt`, { tags: { name: 'robots' } }), 'robots');
}
