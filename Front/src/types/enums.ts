// نتیجهٔ Query ها enum را به‌صورت نام (رشته) می‌دهد، ولی بدنهٔ JSON Command ها عدد می‌خواهد
// (API مبدل رشته‌ای enum ندارد) — نگاشت‌های *Ordinal همین را پوشش می‌دهند.

export type UserRole = "Admin" | "Customer";

export type DealType = "Sale" | "Rent";
export type PropertyKind = "Apartment" | "Villa" | "Land" | "Commercial" | "Office";
export type ListingStatus = "Draft" | "Published" | "Closed";
export type ListingSort = "Newest" | "PriceAsc" | "PriceDesc" | "AreaDesc";
export type LeadStatus = "New" | "Contacted" | "Closed" | "Spam";
export type Channel = "Telegram" | "Bale";

export const dealOrdinal: Record<DealType, number> = { Sale: 1, Rent: 2 };
export const kindOrdinal: Record<PropertyKind, number> = { Apartment: 1, Villa: 2, Land: 3, Commercial: 4, Office: 5 };
export const leadStatusOrdinal: Record<LeadStatus, number> = { New: 1, Contacted: 2, Closed: 3, Spam: 4 };

export const dealLabels: Record<DealType, string> = { Sale: "فروش", Rent: "رهن و اجاره" };
export const kindLabels: Record<PropertyKind, string> = {
  Apartment: "آپارتمان",
  Villa: "ویلا",
  Land: "زمین",
  Commercial: "تجاری",
  Office: "اداری",
};
export const statusLabels: Record<ListingStatus, string> = { Draft: "پیش‌نویس", Published: "منتشرشده", Closed: "بسته‌شده" };
export const statusColor: Record<ListingStatus, BadgeColor> = { Draft: "gray", Published: "green", Closed: "red" };
export const sortLabels: Record<ListingSort, string> = {
  Newest: "جدیدترین",
  PriceAsc: "ارزان‌ترین",
  PriceDesc: "گران‌ترین",
  AreaDesc: "بزرگ‌ترین",
};
export const leadStatusLabels: Record<LeadStatus, string> = { New: "جدید", Contacted: "تماس گرفته شد", Closed: "بسته شد", Spam: "هرزنامه" };
export const leadStatusColor: Record<LeadStatus, BadgeColor> = { New: "amber", Contacted: "blue", Closed: "green", Spam: "gray" };
export const channelLabels: Record<Channel, string> = { Telegram: "تلگرام", Bale: "بله" };

export type BadgeColor = "gray" | "green" | "red" | "amber" | "blue" | "brand";

export const options = <T extends string>(labels: Record<T, string>) =>
  (Object.keys(labels) as T[]).map((value) => ({ value, label: labels[value] }));
