import { useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Button, Spinner, ErrorState } from "@/components/ui";
import { extractErrorMessage } from "@/lib/apiClient";
import { faDigits, parseNumber } from "@/lib/fa";
import { breadcrumbs, useSeo } from "@/lib/seo";
import { options, sortLabels, type ListingSort } from "@/types/enums";
import { useLocations, useSearchProperties, type SearchFilter } from "@/features/listing/api";
import { fromSearchParams, toQueryString } from "@/features/listing/filterParams";
import { PropertyGrid } from "@/features/listing/components/PropertyCardView";
import { SearchBar } from "@/features/listing/components/SearchBar";
import { LIST_PATH, listPath } from "@/features/listing/paths";

const PAGE_SIZE = 12;
const field = "min-h-[40px] w-full rounded-xl border border-brand-200 bg-white px-3 text-sm";

export function PropertiesPage() {
  const [sp, setSp] = useSearchParams();
  const filter = fromSearchParams(sp);
  const [showAdvanced, setShowAdvanced] = useState(Boolean(filter.district || filter.minArea || filter.maxArea || filter.minBedrooms || filter.maxRent));

  const result = useSearchProperties({ ...filter, pageSize: PAGE_SIZE });
  const locations = useLocations();
  const cities = [...new Set(locations.data?.map((l) => l.city) ?? [])];
  const districts = locations.data?.filter((l) => !filter.city || l.city === filter.city).map((l) => l.district) ?? [];

  const label = filter.deal === "Sale" ? "خرید ملک" : filter.deal === "Rent" ? "اجارهٔ ملک" : "آگهی‌های ملک";
  const title = label + (filter.city ? ` در ${filter.city}` : "");
  const total = result.data?.totalCount ?? 0;

  // نسخه‌های فیلترشده و صفحه‌بندی‌شده به لیست پایه اشاره می‌کنند تا محتوای تکراری ایندکس نشود
  const canonical = listPath(filter.deal);
  useSeo({
    title,
    description: `${label} — ${faDigits(total)} آگهی بررسی‌شده با عکس واقعی و قیمت شفاف.`,
    canonicalPath: canonical,
    jsonLd: [breadcrumbs([["خانه", "/"], [label, canonical]])],
  });

  function apply(next: SearchFilter) {
    setSp(new URLSearchParams(toQueryString({ ...next, page: undefined }).slice(1)));
  }

  function patch(changes: Partial<SearchFilter>) {
    apply({ ...filter, ...changes });
  }

  return (
    <div className="mx-auto max-w-6xl px-4">
      <h1 className="mt-6 text-2xl font-extrabold md:text-3xl">{title}</h1>
      <p className="text-sm text-slate-600">{faDigits(total)} آگهی</p>

      <SearchBar
        key={sp.toString()}
        className="my-4"
        initial={filter}
        cities={cities}
        submitLabel="اعمال فیلتر"
        onSearch={(f) => apply({ ...filter, ...f })}
      />

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <button type="button" className="text-sm font-medium text-brand-700" aria-expanded={showAdvanced} onClick={() => setShowAdvanced((v) => !v)}>
          {showAdvanced ? "بستن فیلتر پیشرفته" : "فیلتر پیشرفته"}
        </button>
        <label className="ms-auto flex items-center gap-2 text-sm">
          ترتیب:
          <select
            className="min-h-[36px] rounded-lg border border-brand-200 bg-white px-2"
            value={filter.sort ?? "Newest"}
            onChange={(e) => patch({ sort: e.target.value === "Newest" ? undefined : (e.target.value as ListingSort) })}
          >
            {options(sortLabels).map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </select>
        </label>
      </div>

      {showAdvanced && (
        <form
          key={`adv-${sp.toString()}`}
          aria-label="فیلتر پیشرفته"
          className="mb-6 grid grid-cols-2 gap-3 rounded-2xl border border-brand-200 bg-white p-4 md:grid-cols-6"
          onSubmit={(e) => {
            e.preventDefault();
            const f = new FormData(e.currentTarget);
            const num = (k: string) => parseNumber(String(f.get(k) ?? ""));
            patch({
              district: String(f.get("district") ?? "").trim() || undefined,
              minArea: num("minArea"),
              maxArea: num("maxArea"),
              minBedrooms: num("minBedrooms"),
              maxRent: num("maxRent"),
            });
          }}
        >
          <input name="district" aria-label="محله" placeholder="محله" list="amlak-districts" defaultValue={filter.district} className={field} />
          <datalist id="amlak-districts">
            {[...new Set(districts)].map((d) => (
              <option key={d} value={d} />
            ))}
          </datalist>
          <input name="minArea" aria-label="حداقل متراژ" placeholder="حداقل متراژ" inputMode="numeric" defaultValue={filter.minArea} className={field} />
          <input name="maxArea" aria-label="حداکثر متراژ" placeholder="حداکثر متراژ" inputMode="numeric" defaultValue={filter.maxArea} className={field} />
          <select name="minBedrooms" aria-label="حداقل خواب" defaultValue={filter.minBedrooms ?? ""} className={field}>
            <option value="">تعداد خواب</option>
            {[1, 2, 3, 4].map((n) => (
              <option key={n} value={n}>
                {faDigits(n)}+ خواب
              </option>
            ))}
          </select>
          <input name="maxRent" aria-label="سقف اجارهٔ ماهانه" placeholder="سقف اجاره ماهانه" inputMode="numeric" defaultValue={filter.maxRent} className={field} />
          <Button type="submit">اعمال</Button>
        </form>
      )}

      {result.isLoading && <Spinner />}
      {result.isError && <ErrorState message={extractErrorMessage(result.error)} />}
      {result.data && (
        <>
          {result.data.items.length ? <PropertyGrid items={result.data.items} /> : <p>آگهی‌ای با این فیلتر پیدا نشد.</p>}
          {result.data.totalPages > 1 && (
            <nav aria-label="صفحه‌بندی" className="my-8 flex flex-wrap justify-center gap-2">
              {Array.from({ length: result.data.totalPages }, (_, i) => i + 1).map((n) => (
                <a
                  key={n}
                  href={LIST_PATH + toQueryString({ ...filter, page: n === 1 ? undefined : n })}
                  aria-current={n === result.data.page ? "page" : undefined}
                  onClick={(e) => {
                    e.preventDefault();
                    setSp(new URLSearchParams(toQueryString({ ...filter, page: n === 1 ? undefined : n }).slice(1)));
                    window.scrollTo({ top: 0 });
                  }}
                  className="rounded-xl border border-brand-200 bg-white px-3.5 py-2 aria-[current=page]:bg-brand-700 aria-[current=page]:text-white"
                >
                  {faDigits(n)}
                </a>
              ))}
            </nav>
          )}
        </>
      )}
    </div>
  );
}
