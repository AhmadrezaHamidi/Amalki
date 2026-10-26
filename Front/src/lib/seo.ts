import { useEffect } from "react";
import { SITE_NAME, siteUrl } from "@/lib/config";

export interface SeoInput {
  title?: string;
  description?: string;
  /** مسیر canonical (مثل "/املاک?deal=Sale")؛ پیش‌فرض مسیر فعلی بدون query */
  canonicalPath?: string;
  image?: string | null;
  robots?: string;
  jsonLd?: object[];
}

const DEFAULT_DESCRIPTION = "خرید، فروش، رهن و اجارهٔ آپارتمان، ویلا و ملک تجاری با آگهی‌های بررسی‌شده.";

/** JSON برای تگ script؛ «&lt;/» escape می‌شود تا محتوا نتواند تگ را ببندد */
export function serializeJsonLd(data: object): string {
  return JSON.stringify(data).replace(/<\//g, "<\\/");
}

export function absoluteUrl(path: string): string {
  return /^https?:\/\//i.test(path) ? path : siteUrl() + (path.startsWith("/") ? path : `/${path}`);
}

export function breadcrumbs(items: [name: string, path: string][]): object {
  return {
    "@context": "https://schema.org",
    "@type": "BreadcrumbList",
    itemListElement: items.map(([name, path], i) => ({ "@type": "ListItem", position: i + 1, name, item: absoluteUrl(path) })),
  };
}

function setMeta(attr: "name" | "property", key: string, content: string | undefined) {
  let el = document.head.querySelector<HTMLMetaElement>(`meta[${attr}="${key}"]`);
  if (!content) {
    el?.remove();
    return;
  }
  if (!el) {
    el = document.createElement("meta");
    el.setAttribute(attr, key);
    document.head.appendChild(el);
  }
  el.content = content;
}

/**
 * عنوان، توضیح، canonical، Open Graph و JSON-LD هر صفحه.
 * چون سایت SPA است، این‌ها پس از اجرای JS ساخته می‌شوند؛ گوگل آن را رندر می‌کند،
 * ولی برای خزنده‌های بدون JS باید پیش‌رندر اضافه شود (docs/FRONTEND.md).
 */
export function useSeo(input: SeoInput) {
  const { title, description, canonicalPath, image, robots, jsonLd } = input;
  const ldKey = JSON.stringify(jsonLd ?? []);

  useEffect(() => {
    const fullTitle = title ? `${title} | ${SITE_NAME}` : `${SITE_NAME} | خرید و اجارهٔ ملک`;
    const desc = description ?? DEFAULT_DESCRIPTION;
    const canonical = absoluteUrl(canonicalPath ?? window.location.pathname);

    document.title = fullTitle;
    setMeta("name", "description", desc);
    setMeta("name", "robots", robots);
    setMeta("property", "og:title", fullTitle);
    setMeta("property", "og:description", desc);
    setMeta("property", "og:url", canonical);
    setMeta("property", "og:image", image ? absoluteUrl(image) : undefined);

    let link = document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]');
    if (!link) {
      link = document.createElement("link");
      link.rel = "canonical";
      document.head.appendChild(link);
    }
    link.href = canonical;

    const scripts = (JSON.parse(ldKey) as object[]).map((node) => {
      const s = document.createElement("script");
      s.type = "application/ld+json";
      s.dataset.seo = "page";
      s.textContent = serializeJsonLd(node);
      document.head.appendChild(s);
      return s;
    });
    return () => scripts.forEach((s) => s.remove());
  }, [title, description, canonicalPath, image, robots, ldKey]);
}
