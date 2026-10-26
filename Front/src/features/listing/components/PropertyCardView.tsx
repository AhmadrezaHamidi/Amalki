import { Link } from "react-router-dom";
import { clsx } from "@/lib/clsx";
import { faDigits } from "@/lib/fa";
import type { PropertyCard } from "@/features/listing/api";
import { PriceTag } from "@/features/listing/components/PriceTag";
import { propertyPath } from "@/features/listing/paths";

export function PropertyCardView({ p }: { p: PropertyCard }) {
  return (
    <article className="group relative overflow-hidden rounded-2xl border border-brand-200 bg-white transition hover:-translate-y-1 hover:shadow-soft">
      <div className="relative aspect-[4/3] bg-gradient-to-br from-brand-300 to-accent-700">
        {p.coverPhotoUrl && (
          <img src={p.coverPhotoUrl} alt={p.title} loading="lazy" width={640} height={480} className="h-full w-full object-cover" />
        )}
        <span
          className={clsx(
            "absolute start-3 top-3 rounded-full px-3 py-0.5 text-xs font-bold text-white",
            p.deal === "Rent" ? "bg-accent-700" : "bg-brand-700"
          )}
        >
          {p.deal === "Sale" ? "فروش" : "اجاره"}
        </span>
      </div>
      <div className="p-4">
        <h3 className="font-bold">{p.title}</h3>
        <div className="text-sm text-slate-600">
          {p.city}، {p.district}
        </div>
        <div className="my-2 flex gap-4 border-b border-brand-200 pb-2 text-sm text-slate-600">
          <span>{faDigits(p.areaM2)} متر</span>
          {p.bedrooms != null && <span>{faDigits(p.bedrooms)} خواب</span>}
        </div>
        <PriceTag deal={p.deal} price={p.price} deposit={p.deposit} monthlyRent={p.monthlyRent} />
      </div>
      {/* کل کارت قابل کلیک؛ لینک واقعی برای خزنده‌ها و صفحه‌خوان */}
      <Link to={propertyPath(p.slug)} className="absolute inset-0 z-10" aria-label={p.title} />
    </article>
  );
}

export function PropertyGrid({ items }: { items: PropertyCard[] }) {
  return (
    <div className="grid grid-cols-[repeat(auto-fill,minmax(260px,1fr))] gap-5">
      {items.map((p) => (
        <PropertyCardView key={p.id} p={p} />
      ))}
    </div>
  );
}
