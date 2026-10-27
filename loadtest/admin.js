// Admin panel: each VU logs in once, then works through dashboard, listings, edit form, leads and tickets.
// Read-only on purpose (no publish/delete), so it is safe against a shared database.
// k6 run -e ADMIN_EMAIL=... -e ADMIN_PASSWORD=... loadtest/admin.js
import http from 'k6/http';
import { check, fail, sleep } from 'k6';
import exec from 'k6/execution';
import { BASE, csrf, stages } from './lib.js';

export const options = {
  // Keep the login cookie between iterations (k6 clears the jar every iteration by default).
  noCookiesReset: true,
  scenarios: {
    admins: { executor: 'ramping-vus', stages: stages(5), gracefulRampDown: '10s' },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    'http_req_duration{name:dashboard}': ['p(95)<500'],
    'http_req_duration{name:listings}': ['p(95)<1500'],
    'http_req_duration{name:edit}': ['p(95)<500'],
    'http_req_duration{name:leads}': ['p(95)<500'],
    'http_req_duration{name:tickets}': ['p(95)<500'],
  },
};

const EMAIL = __ENV.ADMIN_EMAIL;
const PASSWORD = __ENV.ADMIN_PASSWORD;
let loggedIn = false;
let ids = [];

// 200 that did not bounce to the login page.
function ok(res, name) {
  return check(res, { [`${name} 200`]: (r) => r.status === 200 && !r.url.includes('/Account/Login') });
}

function login() {
  const form = http.get(`${BASE}/Account/Login`, { tags: { name: 'login' } });
  const res = http.post(`${BASE}/Account/Login`, {
    Email: EMAIL, Password: PASSWORD, __RequestVerificationToken: csrf(form.body),
  }, { tags: { name: 'login' } });
  if (!check(res, { 'login lands on /Admin': (r) => r.status === 200 && r.url.endsWith('/Admin') })) {
    fail(`login failed (${res.status} ${res.url})`);
  }
  loggedIn = true;
}

export function setup() {
  if (!EMAIL || !PASSWORD) throw new Error('Set ADMIN_EMAIL and ADMIN_PASSWORD.');
}

export default function () {
  if (!loggedIn) login();

  ok(http.get(`${BASE}/Admin`, { tags: { name: 'dashboard' } }), 'dashboard');
  sleep(1);

  const list = http.get(`${BASE}/Admin/Listings`, { tags: { name: 'listings' }, timeout: '60s' });
  ok(list, 'listings');
  if (!ids.length) ids = [...String(list.body).matchAll(/href="\/Admin\/Edit\/(\d+)"/g)].map((m) => m[1]);
  sleep(1);

  if (ids.length) {
    const id = ids[(exec.vu.iterationInScenario * 7 + exec.vu.idInTest) % ids.length];
    ok(http.get(`${BASE}/Admin/Edit/${id}`, { tags: { name: 'edit' } }), 'edit');
    sleep(1);
  }

  ok(http.get(`${BASE}/Admin/Leads`, { tags: { name: 'leads' } }), 'leads');
  ok(http.get(`${BASE}/Admin/Tickets`, { tags: { name: 'tickets' } }), 'tickets');
  sleep(2);
}
