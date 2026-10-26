import { z } from "zod";
import { parseNumber } from "@/lib/fa";
import type { PropertyDetail } from "@/features/listing/api";
import type { PropertyInput } from "@/features/admin-properties/api";

// هم‌قاعده با PropertyDetailsValidator در API تا خطا پیش از ارسال دیده شود؛ مرجع نهایی همان API است
const num = (schema: z.ZodNumber) => z.preprocess((v) => parseNumber(v as string), schema.optional());
const amount = num(z.number().int("عدد صحیح وارد کنید").min(0, "مبلغ نمی‌تواند منفی باشد"));

export const propertySchema = z
  .object({
    title: z.string().trim().min(1, "عنوان آگهی الزامی است").max(150, "عنوان حداکثر ۱۵۰ نویسه است"),
    description: z.string().trim().min(1, "توضیحات الزامی است").max(4000, "توضیحات حداکثر ۴۰۰۰ نویسه است"),
    deal: z.enum(["Sale", "Rent"]),
    kind: z.enum(["Apartment", "Villa", "Land", "Commercial", "Office"]),
    price: amount,
    deposit: amount,
    monthlyRent: amount,
    areaM2: z.preprocess((v) => parseNumber(v as string), z.number({ invalid_type_error: "متراژ الزامی است" }).int().min(1, "متراژ باید مثبت باشد")),
    bedrooms: num(z.number().int().min(0).max(50)),
    floor: num(z.number().int().min(-5).max(200)),
    parking: num(z.number().int().min(0).max(50)),
    yearBuilt: num(z.number().int().min(1300, "سال شمسی (۱۳۰۰ تا ۱۴۵۰)").max(1450, "سال شمسی (۱۳۰۰ تا ۱۴۵۰)")),
    city: z.string().trim().min(1, "شهر الزامی است").max(50),
    district: z.string().trim().min(1, "محله الزامی است").max(80),
    address: z.string().max(300).optional(),
    latitude: num(z.number().min(-90).max(90)),
    longitude: num(z.number().min(-180).max(180)),
  })
  .superRefine((v, ctx) => {
    if (v.deal === "Sale" && !v.price) ctx.addIssue({ code: "custom", path: ["price"], message: "برای فروش قیمت لازم است" });
    if (v.deal === "Rent" && !v.deposit && !v.monthlyRent)
      ctx.addIssue({ code: "custom", path: ["deposit"], message: "رهن یا اجارهٔ ماهانه لازم است" });
    if ((v.latitude === undefined) !== (v.longitude === undefined))
      ctx.addIssue({ code: "custom", path: ["latitude"], message: "عرض و طول جغرافیایی باید با هم وارد شوند" });
  });

export type PropertyFormValues = z.input<typeof propertySchema>;

export const emptyProperty: PropertyFormValues = {
  title: "",
  description: "",
  deal: "Sale",
  kind: "Apartment",
  city: "",
  district: "",
};

export function toFormValues(p: PropertyDetail): PropertyFormValues {
  const s = (n: number | null) => (n == null ? undefined : String(n));
  return {
    title: p.title,
    description: p.description,
    deal: p.deal,
    kind: p.kind,
    price: s(p.price),
    deposit: s(p.deposit),
    monthlyRent: s(p.monthlyRent),
    areaM2: String(p.areaM2),
    bedrooms: s(p.bedrooms),
    floor: s(p.floor),
    parking: s(p.parking),
    yearBuilt: s(p.yearBuilt),
    city: p.city,
    district: p.district,
    address: p.address ?? undefined,
    latitude: s(p.latitude),
    longitude: s(p.longitude),
  };
}

export const toInput = (v: z.output<typeof propertySchema>): PropertyInput => ({ ...v, address: v.address?.trim() || undefined });
