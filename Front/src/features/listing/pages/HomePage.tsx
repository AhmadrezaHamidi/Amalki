import { Link, useNavigate } from "react-router-dom";
import { Spinner, ErrorState } from "@/components/ui";
import { extractErrorMessage } from "@/lib/apiClient";
import { SITE_NAME, siteUrl } from "@/lib/config";
import { useSeo } from "@/lib/seo";
import { useLocations, useSearchProperties } from "@/features/listing/api";
import { toQueryString } from "@/features/listing/filterParams";
import { PropertyGrid } from "@/features/listing/components/PropertyCardView";
import { SearchBar } from "@/features/listing/components/SearchBar";
import { LIST_PATH } from "@/features/listing/paths";

export function HomePage() {
  const navigate = useNavigate();
  const latest = useSearchProperties({ pageSize: 8 });
  const locations = useLocations();
  const cities = [...new Set(locations.data?.map((l) => l.city) ?? [])];

  useSeo({
    title: "خرید، فروش و اجارهٔ ملک",
    canonicalPath: "/",
    jsonLd: [
      { "@context": "https://schema.org", "@type": "RealEstateAgent", name: SITE_NAME, url: `${siteUrl()}/`, areaServed: "IR", inLanguage: "fa" },
      { "@context": "https://schema.org", "@type": "WebSite", name: SITE_NAME, url: `${siteUrl()}/`, inLanguage: "fa" },
    ],
  });

  return (
    <div className="mx-auto max-w-6xl px-4">
      <section className="rounded-2xl bg-gradient-to-br from-brand-700 to-accent-700 px-6 pb-24 pt-14 text-white md:px-10">
        <h1 className="max-w-2xl text-3xl font-extrabold leading-snug md:text-5xl md:leading-tight">
          خانهٔ مناسبت را سریع و مطمئن پیدا کن
        </h1>
        <p className="mt-3 max-w-xl opacity-90">آگهی‌های خرید، رهن و اجارهٔ بررسی‌شده، با عکس واقعی و مشاور اختصاصی.</p>
      </section>

      <SearchBar
        className="relative mx-2 -mt-12 md:mx-5"
        cities={cities}
        onSearch={(f) => navigate(LIST_PATH + toQueryString(f))}
      />

      <h2 className="mb-5 mt-12 flex items-end justify-between text-2xl font-bold">
        جدیدترین آگهی‌ها
        <Link to={LIST_PATH} className="text-base font-medium text-brand-700">
          مشاهدهٔ همه ←
        </Link>
      </h2>
      {latest.isLoading && <Spinner />}
      {latest.isError && <ErrorState message={extractErrorMessage(latest.error)} />}
      {latest.data && (latest.data.items.length ? <PropertyGrid items={latest.data.items} /> : <p>هنوز آگهی‌ای منتشر نشده است.</p>)}
    </div>
  );
}
