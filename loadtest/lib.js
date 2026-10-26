// Shared helpers for the k6 scripts in this folder.
import { check } from 'k6';

export const BASE = (__ENV.BASE_URL || 'http://localhost:5244').replace(/\/$/, '');

// Persian route segments, percent-encoded once so every request uses the same URL.
export const LIST = `${BASE}/${encodeURIComponent('املاک')}`;
export const DETAIL = `${BASE}/${encodeURIComponent('ملک')}/`;

export const CITIES = ['تهران', 'کرج', 'شیراز', 'اصفهان', 'مشهد', 'تبریز'];
export const KINDS = ['Apartment', 'Villa', 'Land', 'Commercial', 'Office'];
export const SORTS = ['Newest', 'Cheapest', 'Priciest', 'Largest'];

export function pick(a) {
  return a[Math.floor(Math.random() * a.length)];
}

/** Percent-encoded detail paths of the listing cards on a list page. */
export function detailLinks(html) {
  return [...String(html).matchAll(/href="(\/%D9%85%D9%84%DA%A9\/[^"]+)"/g)].map((m) => m[1]);
}

/** Value of the antiforgery token in a Razor Pages form. */
export function csrf(html) {
  const m = String(html).match(/name="__RequestVerificationToken" type="hidden" value="([^"]+)"/);
  return m ? m[1] : '';
}

export function ok(res, name) {
  return check(res, { [`${name} 200`]: (r) => r.status === 200 });
}

/**
 * Load profile from PROFILE: smoke (1 VU, 30s), load (steady expected traffic),
 * stress (ramps past expected traffic to find the knee). VUS scales the target.
 */
export function stages(defaultVus) {
  const vus = Number(__ENV.VUS || defaultVus);
  switch (__ENV.PROFILE || 'load') {
    case 'smoke':
      return [{ duration: '30s', target: 1 }];
    case 'stress':
      return [
        { duration: '1m', target: vus },
        { duration: '2m', target: vus * 2 },
        { duration: '2m', target: vus * 4 },
        { duration: '30s', target: 0 },
      ];
    default:
      return [
        { duration: '30s', target: vus },
        { duration: '3m', target: vus },
        { duration: '30s', target: 0 },
      ];
  }
}
