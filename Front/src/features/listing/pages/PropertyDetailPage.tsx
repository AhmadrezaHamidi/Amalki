import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { Spinner, ErrorState } from "@/components/ui";
import { extractErrorMessage } from "@/lib/apiClient";
import { faDigits, faMoney } from "@/lib/fa";
import { absoluteUrl, breadcrumbs, useSeo } from "@/lib/seo";
import { kindLabels } from "@/types/enums";
import { usePropertyBySlug, useSimilarProperties, type PropertyDetail } from "@/features/listing/api";
import { PriceTag } from "@/features/listing/components/PriceTag";
import { PropertyGrid } from "@/features/listing/components/PropertyCardView";
import { listPath, propertyPath } from "@/features/listing/paths";
import { LeadForm } from "@/features/leads/components/LeadForm";
import { NotFoundPage } from "@/pages/NotFoundPage";

function describe(p: PropertyDetail): string {
  const price = p.deal === "Sale"
    ? `قیمت ${faMoney(p.price)} تومان`
    : `رهن ${faMoney(p.deposit)} و اجارهٔ ماهانه ${faMoney(p.monthlyRent)} تومان`;
  return `${p.title}، ${faDigits(p.areaM2)} متر در ${p.district}، ${p.city}. ${price}`;
}

function listingJsonLd(p: PropertyDetail): object {
  return {
    "@context": "https://schema.org",
    "@type": "RealEstateListing",
    name: p.title,
    description: p.description,
    url: absoluteUrl(propertyPath(p.slug)),
    datePosted: p.publishedAt?.slice(0, 10),
    image: p.photos.map((x) => absoluteUrl(x.url)),
    address: { "@type": "PostalAddress", addressLocality: p.city, addressRegion: p.district, addressCountry: "IR" },
    floorSize: { "@type": "QuantitativeValue", value: p.areaM2, unitCode: "MTK" },
    offers: {
      "@type": "Offer",
      price: p.deal === "Sale" ? p.price : p.monthlyRent,
      priceCurrency: "IRT",
      availability: "https://schema.org/InStock",
    },
  };
}

function Gallery({ photos, title }: { photos: PropertyDetail["photos"]; title: string }) {
  const [active, setActive] = useState(0);
  if (photos.length === 0)
    return <div className="aspect-[4/3] rounded-2xl bg-gradient-to-br from-brand-300 to-accent-700" aria-hidden />;
  const current = photos[Math.min(active, photos.length - 1)];
  return (
    <div className="space-y-2">
      <img src={current.url} alt={current.alt || title} className="aspect-[4/3] w-full rounded-2xl object-cover" />
      {photos.length > 1 && (
        <div className="flex gap-2 overflow-x-auto pb-1">
          {photos.map((ph, i) => (
            <button
              key={ph.id}
              type="button"
              onClick={() => setActive(i)}
              aria-label={`عکس ${faDigits(i + 1)}`}
              aria-pressed={i === active}
              className="shrink-0 overflow-hidden rounded-lg border-2 border-transparent aria-pressed:border-brand-700"
            >
              <img src={ph.url} alt="" loading="lazy" width={96} height={72} className="h-[72px] w-24 object-cover" />
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

export function PropertyDetailPage() {
  const { slug = "" } = useParams();
  const query = usePropertyBySlug(slug);
  const p = query.data;
  const similar = useSimilarProperties(p?.id);

  const dealWord = p?.deal === "Sale" ? "فروش" : "اجارهٔ";
  useSeo(
    p
      ? {
          title: `${p.title} | ${dealWord} در ${p.district} ${p.city}`,
          description: describe(p),
          canonicalPath: propertyPath(p.slug),
          image: p.photos[0]?.url,
          jsonLd: [
            listingJsonLd(p),
            breadcrumbs([["خانه", "/"], [p.deal === "Sale" ? "خرید" : "اجاره", listPath(p.deal)], [p.title, propertyPath(p.slug)]]),
          ],
        }
      : { robots: query.data === null ? "noindex" : undefined }
  );

  if (query.isLoading) return <Spinner />;
  if (query.isError) return <div className="mx-auto max-w-6xl px-4 py-8"><ErrorState message={extractErrorMessage(query.error)} /></div>;
  if (!p) return <NotFoundPage />;

  const specs: [string, number | string | null][] = [
    ["نوع", kindLabels[p.kind]],
    ["متراژ", `${faDigits(p.areaM2)} متر`],
    ["خواب", p.bedrooms != null ? faDigits(p.bedrooms) : null],
    ["طبقه", p.floor != null ? faDigits(p.floor) : null],
    ["پارکینگ", p.parking != null ? faDigits(p.parking) : null],
    ["سال ساخت", p.yearBuilt != null ? faDigits(p.yearBuilt) : null],
  ];

  return (
    <div className="mx-auto max-w-6xl px-4">
      <nav aria-label="مسیر" className="mt-3 text-sm text-slate-600">
        <Link to="/">خانه</Link> / <Link to={listPath(p.deal)}>{p.deal === "Sale" ? "خرید" : "اجاره"}</Link> / {p.city}
      </nav>
      <h1 className="mt-2 text-2xl font-extrabold md:text-3xl">{p.title}</h1>
      <p className="text-slate-600">
        {p.city}، {p.district}
      </p>

      <div className="mt-5 grid gap-7 md:grid-cols-[2fr_1fr]">
        <div>
          <Gallery photos={p.photos} title={p.title} />
          <h2 className="mb-3 mt-8 text-xl font-bold">توضیحات</h2>
          <p className="whitespace-pre-line">{p.description}</p>
          {p.latitude != null && p.longitude != null && (
            <a
              className="mt-4 inline-block text-sm font-medium text-brand-700"
              href={`https://www.openstreetmap.org/?mlat=${p.latitude}&mlon=${p.longitude}#map=17/${p.latitude}/${p.longitude}`}
              target="_blank"
              rel="noopener noreferrer"
            >
              مشاهدهٔ موقعیت روی نقشه ←
            </a>
          )}
        </div>

        <aside className="space-y-5 self-start rounded-2xl border border-brand-200 bg-white p-5 md:sticky md:top-4">
          <PriceTag deal={p.deal} price={p.price} deposit={p.deposit} monthlyRent={p.monthlyRent} large />
          <ul className="grid grid-cols-2 gap-2 text-sm">
            {specs
              .filter(([, v]) => v != null)
              .map(([k, v]) => (
                <li key={k}>
                  <span className="text-slate-500">{k}:</span> {v}
                </li>
              ))}
          </ul>
          <LeadForm propertyId={p.id} />
        </aside>
      </div>

      {similar.data && similar.data.length > 0 && (
        <>
          <h2 className="mb-5 mt-12 text-xl font-bold">آگهی‌های مشابه</h2>
          <PropertyGrid items={similar.data} />
        </>
      )}
    </div>
  );
}
