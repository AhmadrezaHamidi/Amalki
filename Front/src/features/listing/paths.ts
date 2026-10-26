import type { DealType } from "@/types/enums";

// مسیرهای فارسی همان مسیرهای سایت قبلی‌اند تا لینک‌های ایندکس‌شده نشکنند
export const LIST_PATH = "/املاک";

export const propertyPath = (slug: string) => `/ملک/${slug}`;

export const listPath = (deal?: DealType) => (deal ? `${LIST_PATH}?deal=${deal}` : LIST_PATH);
