// Visitor journeys on the public site: home -> filtered list -> detail (+ some paging).
// k6 run loadtest/public.js                       (load: 50 VUs for 3 minutes)
// k6 run -e PROFILE=stress -e VUS=50 loadtest/public.js
import http from 'k6/http';
import { group, sleep } from 'k6';
import { BASE, LIST, DETAIL, CITIES, KINDS, SORTS, pick, detailLinks, ok, stages } from './lib.js';

export const options = {
  scenarios: {
    visitors: { executor: 'ramping-vus', stages: stages(50), gracefulRampDown: '10s' },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    'http_req_duration{page:home}': ['p(95)<400'],
    'http_req_duration{page:list}': ['p(95)<500'],
    'http_req_duration{page:detail}': ['p(95)<400'],
  },
};

const THINK = Number(__ENV.THINK || 1); // seconds between clicks

function listQuery() {
  const q = [`deal=${pick(['Sale', 'Rent'])}`];
  if (Math.random() < 0.6) q.push(`city=${encodeURIComponent(pick(CITIES))}`);
  if (Math.random() < 0.4) q.push(`kind=${pick(KINDS)}`);
  if (Math.random() < 0.2) q.push(`minArea=${pick([60, 100, 150])}`);
  if (Math.random() < 0.2) q.push(`beds=${pick([1, 2, 3])}`);
  if (Math.random() < 0.3) q.push(`sort=${pick(SORTS)}`);
  return q.join('&');
}

export default function () {
  group('home', () => {
    ok(http.get(`${BASE}/`, { tags: { page: 'home' } }), 'home');
  });
  sleep(THINK);

  let links = [];
  group('list', () => {
    const query = listQuery();
    const res = http.get(`${LIST}?${query}`, { tags: { page: 'list', name: 'list' } });
    ok(res, 'list');
    links = detailLinks(res.body);
    // A third of visitors page deeper into the results.
    if (Math.random() < 0.33) {
      sleep(THINK);
      const p = 2 + Math.floor(Math.random() * 5);
      const r2 = http.get(`${LIST}?${query}&p=${p}`, { tags: { page: 'list', name: 'list' } });
      ok(r2, 'list page n');
      if (detailLinks(r2.body).length) links = detailLinks(r2.body);
    }
  });
  sleep(THINK);

  group('detail', () => {
    // Fallback slug matches loadtest/seed.sql when a filter returned no cards.
    const url = links.length ? BASE + pick(links) : `${DETAIL}lt-${1 + Math.floor(Math.random() * 9) * 10 + 1}`;
    ok(http.get(url, { tags: { page: 'detail', name: 'detail' } }), 'detail');
  });
  sleep(THINK);
}
