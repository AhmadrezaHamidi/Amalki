export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || "/api/v1";
export const SITE_NAME = import.meta.env.VITE_SITE_NAME || "آشیانه";

/** آدرس عمومی سایت برای canonical و JSON-LD؛ خالی یعنی همان دامنهٔ مرورگر */
export function siteUrl(): string {
  return (import.meta.env.VITE_SITE_URL || window.location.origin).replace(/\/+$/, "");
}
