import { compact, type SearchFilter } from "@/features/listing/api";
import { dealLabels, kindLabels, sortLabels, type DealType, type ListingSort, type PropertyKind } from "@/types/enums";

/** فیلتر ← query string صفحهٔ لیست (همان نام پارامترهای API) */
export function toQueryString(filter: SearchFilter): string {
  const params = new URLSearchParams(Object.entries(compact(filter)).map(([k, v]) => [k, String(v)]));
  const qs = params.toString();
  return qs ? `?${qs}` : "";
}

function oneOf<T extends string>(value: string | null, labels: Record<T, string>): T | undefined {
  return value && value in labels ? (value as T) : undefined;
}

function positive(value: string | null): number | undefined {
  if (!value) return undefined;
  const n = Number(value);
  return Number.isFinite(n) && n > 0 ? Math.floor(n) : undefined;
}

/** query string ← فیلتر؛ مقدار ناشناخته دور ریخته می‌شود تا URL دستکاری‌شده خطای ۴۰۰ ندهد */
export function fromSearchParams(sp: URLSearchParams): SearchFilter {
  return compact({
    deal: oneOf<DealType>(sp.get("deal"), dealLabels),
    kind: oneOf<PropertyKind>(sp.get("kind"), kindLabels),
    sort: oneOf<ListingSort>(sp.get("sort"), sortLabels),
    city: sp.get("city")?.trim() || undefined,
    district: sp.get("district")?.trim() || undefined,
    minPrice: positive(sp.get("minPrice")),
    maxPrice: positive(sp.get("maxPrice")),
    maxRent: positive(sp.get("maxRent")),
    minArea: positive(sp.get("minArea")),
    maxArea: positive(sp.get("maxArea")),
    minBedrooms: positive(sp.get("minBedrooms")),
    page: positive(sp.get("page")),
  });
}
