import { useState, type FormEvent } from "react";
import { clsx } from "@/lib/clsx";
import { parseNumber } from "@/lib/fa";
import { kindLabels, options, type DealType, type PropertyKind } from "@/types/enums";
import type { SearchFilter } from "@/features/listing/api";

interface Props {
  initial?: SearchFilter;
  onSearch: (filter: SearchFilter) => void;
  submitLabel?: string;
  /** شهرهای دارای آگهی برای پیشنهاد در فیلد شهر */
  cities?: string[];
  className?: string;
}

const field = "min-h-[44px] w-full rounded-xl border border-brand-200 bg-brand-50 px-3 text-sm";

/** فرم جست‌وجوی اصلی: معامله، شهر، نوع ملک، سقف قیمت */
export function SearchBar({ initial = {}, onSearch, submitLabel = "جستجو", cities = [], className }: Props) {
  const [deal, setDeal] = useState<DealType | "">(initial.deal ?? "");
  const [city, setCity] = useState(initial.city ?? "");
  const [kind, setKind] = useState<PropertyKind | "">(initial.kind ?? "");
  const [maxPrice, setMaxPrice] = useState(initial.maxPrice?.toString() ?? "");

  function submit(e: FormEvent) {
    e.preventDefault();
    onSearch({
      deal: deal || undefined,
      city: city.trim() || undefined,
      kind: kind || undefined,
      maxPrice: parseNumber(maxPrice),
    });
  }

  return (
    <form
      onSubmit={submit}
      role="search"
      aria-label="جستجوی ملک"
      className={clsx(
        "grid grid-cols-2 items-center gap-3 rounded-2xl border border-brand-200 bg-white/90 p-4 shadow-soft backdrop-blur md:grid-cols-[repeat(4,1fr)_auto]",
        className
      )}
    >
      <select aria-label="نوع معامله" className={field} value={deal} onChange={(e) => setDeal(e.target.value as DealType | "")}>
        <option value="">همهٔ معاملات</option>
        <option value="Sale">خرید</option>
        <option value="Rent">اجاره / رهن</option>
      </select>
      <input aria-label="شهر" placeholder="شهر (مثلاً تهران)" className={field} list="amlak-cities" value={city} onChange={(e) => setCity(e.target.value)} />
      <datalist id="amlak-cities">
        {cities.map((c) => (
          <option key={c} value={c} />
        ))}
      </datalist>
      <select aria-label="نوع ملک" className={field} value={kind} onChange={(e) => setKind(e.target.value as PropertyKind | "")}>
        <option value="">همهٔ انواع</option>
        {options(kindLabels).map((o) => (
          <option key={o.value} value={o.value}>
            {o.label}
          </option>
        ))}
      </select>
      <input
        aria-label="سقف قیمت یا رهن (تومان)"
        placeholder="سقف قیمت / رهن (تومان)"
        inputMode="numeric"
        className={field}
        value={maxPrice}
        onChange={(e) => setMaxPrice(e.target.value)}
      />
      <button className="col-span-2 min-h-[44px] rounded-2xl bg-brand-700 px-6 font-bold text-white transition hover:-translate-y-0.5 hover:shadow-soft md:col-span-1">
        {submitLabel}
      </button>
    </form>
  );
}
